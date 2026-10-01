using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    internal static partial class Ghost
    {
        /// <summary>
        /// Takes off everything the policy does not keep (<see cref="StripPolicy"/>), at once or a
        /// little at a time while the copy sleeps. A component another one requires can only go
        /// after that one, so the list is gone through again while a pass took anything off; what
        /// must wait is kept in place, so going through it stays as quick as it is long.
        /// </summary>
        internal sealed class Stripping
        {
            private readonly GameObject _copy;
            private readonly List<KeyValuePair<int, Component>> _doomed = new List<KeyValuePair<int, Component>>();
            private int _read;
            private int _kept;
            private bool _progress;

            /// <summary>How many components the copy had.</summary>
            public int Parts { get; }

            public Stripping(GameObject copy, bool falling, bool keepColliders)
            {
                _copy = copy;
                var all = copy.GetComponentsInChildren<Component>(true);
                Parts = all.Length;
                foreach (var component in all)
                {
                    if (component == null || keepColliders && component is Collider) continue;
                    var pass = PassFor(component, falling);
                    if (pass != StripPolicy.Keep) _doomed.Add(new KeyValuePair<int, Component>(pass, component));
                }
                _doomed.Sort((a, b) => a.Key.CompareTo(b.Key));
            }

            /// <summary>Takes off what it can until the watch reaches the budget (no watch: all); true once nothing more can go.</summary>
            public bool Go(Stopwatch watch, double budgetMs)
            {
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
        /// made and prepared asleep under the holder as <see cref="Make"/> makes one, stripped a
        /// little each frame, settled, then posed where it is shown and woken a few parts each
        /// frame (<see cref="WakeChunks"/>). Each step goes on until its time is up; making the
        /// copy itself, and waking any one of its parts, cannot be split. What
        /// <c>prepare</c> does to the copy is done while it still sleeps, before it is stripped
        /// (a location's parts rolled). With <c>keepColliders</c> its colliders stay, for the
        /// caller to read where its floors are (<see cref="FloorProbe"/>). Its pose is given in
        /// the world, or with <c>local</c> in its parent's space, which holds should the parent be
        /// moved or sized while the copy is made.
        /// The steps are told as "copy made", "copy prepared", "copy stripped", "copy settled"
        /// and "copy woken".
        /// </summary>
        internal sealed class Building
        {
            /// <summary>How many parts are woken together at most.</summary>
            private const int ChunkParts = 64;

            private enum Step { Make, Strip, Settle, Wake, Done }

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
                try
                {
                    do Next(watch, budgetMs);
                    while (_step != Step.Done && watch.Elapsed.TotalMilliseconds < budgetMs);
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
                        _copy = Object.Instantiate(_prefab, Holder().transform, false);
                        _copy.name = _prefab.name;
                        Timing.Add("copy made", made);
                        var prepared = Timing.Start();
                        _prepare?.Invoke(_copy);
                        _stripping = new Stripping(_copy, falling: false, _keepColliders);
                        Timing.Add("copy prepared", prepared);
                        _step = Step.Strip;
                        return;
                    }
                    case Step.Strip:
                    {
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
                        try { Loudness.Add(_copy); }
                        catch (Exception ex) { Faults.Tell("preview loudness", ex); }
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

            /// <summary>Stops making the copy and takes down what was made of it; a copy already handed over stays.</summary>
            public void Cancel()
            {
                if (_copy != null) Object.Destroy(_copy);
                _copy = null;
                _stripping = null;
                _asleep.Clear();
                _step = Step.Done;
            }
        }
    }
}
