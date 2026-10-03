using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    internal static partial class Ghost
    {
        /// <summary>
        /// Takes off everything the policy does not keep (<see cref="StripPolicy"/>), at once or a
        /// little at a time while the copy sleeps: what is to go is noted part by part, then taken
        /// off. A component another one requires can only go after that one, so the list is gone
        /// through again while a pass took anything off; what must wait is kept in place, so going
        /// through it stays as quick as it is long.
        /// </summary>
        internal sealed class Stripping
        {
            private readonly GameObject _copy;
            private readonly bool _falling;
            private readonly bool _keepColliders;
            private readonly List<KeyValuePair<int, Component>> _doomed = new List<KeyValuePair<int, Component>>();
            private Transform[] _parts;
            private int _next;
            private bool _noted;
            private int _read;
            private int _kept;
            private bool _progress;

            private static readonly List<Component> Found = new List<Component>();

            /// <summary>How many components the copy had, once noted.</summary>
            public int Parts { get; private set; }

            public Stripping(GameObject copy, bool falling, bool keepColliders)
            {
                _copy = copy;
                _falling = falling;
                _keepColliders = keepColliders;
            }

            /// <summary>Notes what is to go, part by part, until the watch reaches the budget (no watch: all); true once all is noted, in the order it goes.</summary>
            public bool Note(Stopwatch watch, double budgetMs)
            {
                if (_noted) return true;
                if (_parts == null) _parts = _copy.GetComponentsInChildren<Transform>(true);
                while (_next < _parts.Length)
                {
                    if (watch != null && watch.Elapsed.TotalMilliseconds >= budgetMs) return false;
                    var part = _parts[_next++];
                    if (part == null) continue;
                    part.GetComponents(Found);
                    foreach (var component in Found)
                    {
                        Parts++;
                        if (component == null || _keepColliders && component is Collider) continue;
                        var pass = PassFor(component, _falling);
                        if (pass != StripPolicy.Keep) _doomed.Add(new KeyValuePair<int, Component>(pass, component));
                    }
                    Found.Clear();
                }
                _doomed.Sort((a, b) => a.Key.CompareTo(b.Key));
                _parts = null;
                _noted = true;
                return true;
            }

            /// <summary>Takes off what it can until the watch reaches the budget (no watch: all); true once nothing more can go.</summary>
            public bool Go(Stopwatch watch, double budgetMs)
            {
                if (!Note(watch, budgetMs)) return false;
                while (true)
                {
                    if (_read >= _doomed.Count)
                    {
                        // A pass is through: what had to wait is gone through again if anything went.
                        _doomed.RemoveRange(_kept, _doomed.Count - _kept);
                        _read = _kept = 0;
                        if (!_progress || _doomed.Count == 0) break;
                        _progress = false;
                        continue;
                    }
                    if (watch != null && watch.Elapsed.TotalMilliseconds >= budgetMs) return false;

                    var entry = _doomed[_read++];
                    var component = entry.Value;
                    if (component == null) continue;
                    if (IsRequired(component))
                    {
                        _doomed[_kept++] = entry;
                        continue;
                    }
                    Object.DestroyImmediate(component);
                    _progress = true;
                }

                if (_doomed.Count > 0)
                {
                    Plugin.Log.LogDebug($"Scry left {_doomed.Count} part(s) on the preview of {_copy.name} that something else needs.");
                }
                return true;
            }
        }

        /// <summary>
        /// A copy made over several frames, for a prefab so large (a location with thousands of
        /// parts) that making it at once would hold the game up for a third of a second. It is
        /// made asleep under the holder, by Unity on a thread of its own and taken into the game
        /// a slice each frame (<c>Object.InstantiateAsync</c>), else at once as <see cref="Make"/>
        /// makes one; then prepared, noted and stripped a little each frame, settled, then posed
        /// where it is shown and woken a few parts each frame (<see cref="WakeChunks"/>). Each
        /// step goes on until its time is up; waking any one of its parts cannot be split. A bundle
        /// is let go of only once no copy is being made (<see cref="WhenIdle"/>), and a copy
        /// dropped while Unity still makes it is taken down once made, nothing waiting on it. What
        /// <c>prepare</c> does to the copy is done while it still sleeps, before it is stripped
        /// (a location's parts rolled). With <c>keepColliders</c> its colliders stay, for the
        /// caller to read where its floors are (<see cref="FloorProbe"/>). Its pose is given in
        /// the world, or with <c>local</c> in its parent's space, which holds should the parent be
        /// moved or sized while the copy is made.
        /// The steps are told as "copy made", "copy prepared", "copy noted", "copy stripped",
        /// "copy settled" and "copy woken".
        /// </summary>
        internal sealed class Building
        {
            /// <summary>How many parts are woken together at most.</summary>
            private const int ChunkParts = 64;

            private enum Step { Make, Prepare, Strip, Settle, Wake, Done }

            /// <summary>The copies Unity is still making, to finish before what they are made from goes.</summary>
            private static readonly List<AsyncInstantiateOperation<GameObject>> Making = new List<AsyncInstantiateOperation<GameObject>>();

            /// <summary>Copies dropped while Unity still made them, taken down once it has.</summary>
            private static readonly List<AsyncInstantiateOperation<GameObject>> Dropped = new List<AsyncInstantiateOperation<GameObject>>();

            /// <summary>What waits to be let go of until no copy is being made: bundles the copies read from.</summary>
            private static readonly List<Action> AfterMaking = new List<Action>();

            /// <summary>Whether Unity is making any copy still, reading from the bundle it is made from.</summary>
            private static bool BeingMade => Making.Exists(m => !m.isDone) || Dropped.Exists(m => !m.isDone);

            /// <summary>Lets go of something a copy may be made from: at once when no copy is being made, else as soon as none is (<see cref="Tick"/>).</summary>
            public static void WhenIdle(Action letGo)
            {
                if (BeingMade) AfterMaking.Add(letGo);
                else letGo();
            }

            /// <summary>Each frame: copies dropped are taken down once made, and what waited for no copy to be made is let go of.</summary>
            public static void Tick()
            {
                // Every frame, the panel open or not: nothing is made for it while nothing waits.
                for (var i = Dropped.Count - 1; i >= 0; i--)
                {
                    var made = Dropped[i];
                    if (!made.isDone) continue;
                    Dropped.RemoveAt(i);
                    TakeDown(made);
                }
                if (AfterMaking.Count == 0 || BeingMade) return;
                var waiting = AfterMaking.ToArray();
                AfterMaking.Clear();
                foreach (var letGo in waiting) Guard.Run("letting go of a bundle", letGo);
            }

            /// <summary>Finishes every copy at once and lets go of what waited, as a world is left.</summary>
            public static void Flush()
            {
                foreach (var making in Making.Concat(Dropped).ToArray())
                {
                    try { if (!making.isDone) making.WaitForCompletion(); }
                    catch (Exception ex) { Faults.Tell("finishing a copy", ex); }
                }
                Tick();
            }

            /// <summary>Takes down what an operation made.</summary>
            private static void TakeDown(AsyncInstantiateOperation<GameObject> making)
            {
                try
                {
                    if (making.Result != null) foreach (var made in making.Result) if (made != null) Object.Destroy(made);
                }
                catch (Exception ex)
                {
                    Faults.Tell("taking down a copy", ex);
                }
            }

            private readonly GameObject _prefab;
            private readonly string _name;
            private readonly Transform _parent;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly int _layer;
            private readonly Action<GameObject> _prepare;
            private readonly bool _keepColliders;
            private readonly bool _local;
            private readonly List<GameObject> _asleep = new List<GameObject>();
            private Step _step;
            private GameObject _copy;
            private Stripping _stripping;
            private int _woken;
            private AsyncInstantiateOperation<GameObject> _making;

            /// <summary>Whether a step waits on Unity this frame, which ends the frame's work.</summary>
            private bool _waiting;

            public Building(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, int layer, Action<GameObject> prepare, bool keepColliders, bool local = false)
            {
                _local = local;
                _prefab = prefab;
                _name = prefab != null ? prefab.name : "";
                _parent = parent;
                _position = position;
                _rotation = rotation;
                _layer = layer;
                _prepare = prepare;
                _keepColliders = keepColliders;
            }

            /// <summary>The copy once all of it is awake; null while it is made, or when it could not be.</summary>
            public GameObject Result { get; private set; }

            public bool Done => _step == Step.Done;

            /// <summary>How many frames it was gone on in, and how many parts the copy has, for the self-test to tell.</summary>
            public int Frames { get; private set; }
            public int Parts { get; private set; }

            /// <summary>Goes on making the copy for about so many milliseconds, a step at the least; true once it is done.</summary>
            public bool Go(double budgetMs)
            {
                if (_step == Step.Done) return true;
                Frames++;
                var watch = Stopwatch.StartNew();
                // A network view left on the copy for any reason destroys itself on waking.
                var was = ZNetView.m_forceDisableInit;
                ZNetView.m_forceDisableInit = true;
                _waiting = false;
                try
                {
                    do Next(watch, budgetMs);
                    while (_step != Step.Done && !_waiting && watch.Elapsed.TotalMilliseconds < budgetMs);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"Scry could not make a preview of {_name}: {ex.Message}");
                    Cancel();
                }
                finally
                {
                    ZNetView.m_forceDisableInit = was;
                }
                return _step == Step.Done;
            }

            private void Next(Stopwatch watch, double budgetMs)
            {
                if (_prefab == null || _parent == null)
                {
                    Cancel();
                    return;
                }
                switch (_step)
                {
                    case Step.Make:
                    {
                        var made = Timing.Start();
                        if (_making == null && !Begin())
                        {
                            _copy = Object.Instantiate(_prefab, Holder().transform, false);
                        }
                        else
                        {
                            if (!_making.isDone)
                            {
                                Timing.Add("copy made", made);
                                _waiting = true;
                                return;
                            }
                            Making.Remove(_making);
                            var result = _making.Result;
                            _making = null;
                            _copy = result != null && result.Length > 0 ? result[0] : null;
                        }
                        Timing.Add("copy made", made);
                        if (_copy == null)
                        {
                            Cancel();
                            return;
                        }
                        _copy.name = _prefab.name;
                        _step = Step.Prepare;
                        return;
                    }
                    case Step.Prepare:
                    {
                        var prepared = Timing.Start();
                        _prepare?.Invoke(_copy);
                        _stripping = new Stripping(_copy, falling: false, _keepColliders);
                        Timing.Add("copy prepared", prepared);
                        _step = Step.Strip;
                        return;
                    }
                    case Step.Strip:
                    {
                        var noted = Timing.Start();
                        var all = _stripping.Note(watch, budgetMs);
                        Timing.Add("copy noted", noted);
                        if (!all) return;
                        var stripped = Timing.Start();
                        var done = _stripping.Go(watch, budgetMs);
                        Timing.Add("copy stripped", stripped);
                        if (!done) return;
                        _stripping = null;
                        _step = Step.Settle;
                        return;
                    }
                    case Step.Settle:
                    {
                        var settled = Timing.Start();
                        Settle(_copy, falling: false);
                        if (_layer >= 0) SetLayer(_copy.transform, _layer);
                        // Its sounds at the loudness the player chose; one that cannot take it plays as the game would.
                        Guard.Run("preview loudness", Loudness.Add, _copy);
                        PutToSleep();
                        Timing.Add("copy settled", settled);

                        // Posed and woken where it stands, all but the parts that wake later.
                        var woken = Timing.Start();
                        Place(_copy, _parent, _position, _rotation, _local);
                        Timing.Add("copy woken", woken);
                        _step = Step.Wake;
                        return;
                    }
                    case Step.Wake:
                    {
                        var woken = Timing.Start();
                        while (_woken < _asleep.Count)
                        {
                            var part = _asleep[_woken++];
                            if (part != null) part.SetActive(true);
                            if (watch.Elapsed.TotalMilliseconds >= budgetMs) break;
                        }
                        Timing.Add("copy woken", woken);
                        if (_woken < _asleep.Count) return;

                        Awake(_prefab, _copy);
                        Result = _copy;
                        _copy = null;
                        _asleep.Clear();
                        _step = Step.Done;
                        return;
                    }
                }
            }

            /// <summary>Switches off the parts to wake one after another, while the copy still sleeps, where nothing notices.</summary>
            private void PutToSleep()
            {
                var parts = _copy.GetComponentsInChildren<Transform>(true);
                Parts = parts.Length;
                var index = new Dictionary<Transform, int>(parts.Length);
                for (var i = 0; i < parts.Length; i++) index[parts[i]] = i;
                var parent = new List<int>(parts.Length);
                var on = new List<bool>(parts.Length);
                var root = _copy.transform;
                foreach (var part in parts)
                {
                    parent.Add(part != root && part.parent != null && index.TryGetValue(part.parent, out var up) ? up : -1);
                    on.Add(part.gameObject.activeSelf);
                }
                foreach (var chunk in WakeChunks.Pick(parent, on, ChunkParts))
                {
                    var part = parts[chunk].gameObject;
                    part.SetActive(false);
                    _asleep.Add(part);
                }
            }

            /// <summary>
            /// Has Unity make the copy on a thread of its own, under the sleeping holder, its pose
            /// as the prefab's; false where it cannot, and the copy is made at once.
            /// </summary>
            private bool Begin()
            {
                try
                {
                    _making = Object.InstantiateAsync(_prefab, new InstantiateParameters { parent = Holder().transform, worldSpace = false });
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogDebug($"Scry makes {_name} at once, as Unity could not make it on its own: {ex.Message}");
                    _making = null;
                }
                if (_making == null) return false;
                Making.Add(_making);
                return true;
            }

            /// <summary>Stops making the copy and takes down what was made of it; a copy already handed over stays.</summary>
            public void Cancel()
            {
                if (_making != null)
                {
                    // Taken down once Unity has made it, nothing waiting on it meanwhile.
                    var making = _making;
                    _making = null;
                    Making.Remove(making);
                    if (making.isDone) TakeDown(making);
                    else Dropped.Add(making);
                }
                if (_copy != null) Object.Destroy(_copy);
                _copy = null;
                _stripping = null;
                _asleep.Clear();
                _step = Step.Done;
            }
        }
    }
}
