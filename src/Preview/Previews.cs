using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Keeps the previews in step with what is selected, and carries out what the panel's buttons
    /// ask for: showing a copy in the world, playing sounds and effects, flying projectiles and
    /// showing a status effect's visuals on you.
    ///
    /// Everything made here is a <see cref="Ghost"/>: local, unsaved and unseen by anyone else.
    /// Whatever plays in the world is also on a timer, so nothing lingers if it never ends itself.
    /// </summary>
    internal static partial class Previews
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static Previews() => WorldCaches.Register(nameof(Previews), Forget);

        private const float EffectSeconds = 10f;

        private struct Timed
        {
            public GameObject Thing;
            public float Until;
        }

        private static Explorer _explorer;
        private static int _selectionVersion = -1;
        private static int _modifierVersion = -1;
        private static Entry _entry;
        private static int _builtLevel = 1;
        private static Wear _builtWear;
        private static int _builtLook;
        private static float _replayAt = -1f;
        private static bool _stageStale;

        private static GameObject _world;
        private static readonly List<GameObject> Pinned = new List<GameObject>();
        private static Vector3 _spot;
        private static Quaternion _facing = Quaternion.identity;

        private static readonly List<Timed> Played = new List<Timed>();
        private static GameObject _sound;
        private static float _soundWaitUntil;
        private static bool _soundPaused;
        private static Entry _soundEntry;
        private static readonly List<GameObject> StatusVisuals = new List<GameObject>();
        private static StatusEffect _status;

        /// <summary>What each play button started, so it stays lit until all of it has finished.</summary>
        public static readonly Playback<GameObject> Playing = new Playback<GameObject>(Alive);

        private static readonly Dictionary<GameObject, float> Born = new Dictionary<GameObject, float>();
        private static readonly Dictionary<GameObject, bool> AliveNow = new Dictionary<GameObject, bool>();
        private static int _aliveFrame = -1;

        /// <summary>Records what a button started, each part tagged with the prefab it copies.</summary>
        private static void Started(object key, IEnumerable<GameObject> things, bool pressed = true)
        {
            var tagged = new List<(string, GameObject)>();
            var now = Time.unscaledTime;
            foreach (var thing in things)
            {
                if (thing == null) continue;
                Born[thing] = now;
                tagged.Add((thing.name, thing));
            }
            Playing.Started(key, tagged, pressed);
        }

        private static readonly Dictionary<object, AnimationClip> ClipOf = new Dictionary<object, AnimationClip>();
        private static AnimationClip _startedClip;

        /// <summary>
        /// Stops what a lit button started: its copies go, a fallen or broken copy stands again,
        /// and an animation it started ends.
        /// </summary>
        public static void Stop(object key)
        {
            foreach (var thing in Playing.Take(key)) if (thing != null) Object.Destroy(thing);
            if (key != null && Undo.TryGetValue(key, out var undo))
            {
                Undo.Remove(key);
                undo();
            }
            if (key != null && ClipOf.TryGetValue(key, out var clip))
            {
                ClipOf.Remove(key);
                if (PlayingClip() == clip) StopClip();
            }
        }

        /// <summary>What a playing animation made of itself, lit under its clip.</summary>
        public static void Heard(AnimationClip clip, IEnumerable<GameObject> things)
        {
            if (clip == null) return;
            var made = things.ToList();
            Started(clip, made, pressed: false);
            if (_litWith.Clip == clip && _litWith.Key != null) Started(_litWith.Key, made, pressed: false);
        }

        /// <summary>A clip an effect list's button started, whose parts light that button too.</summary>
        private static (AnimationClip Clip, object Key) _litWith;

        /// <summary>The clip whose chip was pressed last, for showing what it plays.</summary>
        public static AnimationClip LastClip;

        /// <summary>The ground footsteps are heard on.</summary>
        public static FootStep.GroundMaterial StepGround = FootStep.GroundMaterial.Default;

        /// <summary>The grounds a creature's footsteps sound different on, each once.</summary>
        public static List<FootStep.GroundMaterial> Grounds(GameObject prefab)
        {
            // Asked on every event the panel draws; worked out once per prefab.
            if (prefab == null) return NoGrounds;
            if (GroundsOf.TryGetValue(prefab, out var known)) return known;
            var grounds = new List<FootStep.GroundMaterial>();
            GroundsOf[prefab] = grounds;
            var step = prefab.GetComponentInChildren<FootStep>(true);
            if (step == null || step.m_effects == null) return grounds;
            foreach (FootStep.GroundMaterial ground in System.Enum.GetValues(typeof(FootStep.GroundMaterial)))
            {
                if (ground == FootStep.GroundMaterial.None || ground == FootStep.GroundMaterial.Everything) continue;
                if (step.m_effects.Exists(e => e != null && (e.m_material & ground) != 0 && (e.m_material & FootStep.GroundMaterial.Everything) != FootStep.GroundMaterial.Everything)) grounds.Add(ground);
            }
            return grounds;
        }

        private static readonly Dictionary<GameObject, List<FootStep.GroundMaterial>> GroundsOf = new Dictionary<GameObject, List<FootStep.GroundMaterial>>();
        private static readonly List<FootStep.GroundMaterial> NoGrounds = new List<FootStep.GroundMaterial>();

        /// <summary>
        /// Whether a copy is still playing: while it is new (a sound starts a frame or a delay
        /// later), stands in for a fallen or broken copy, has particles alive, a sound playing or
        /// parts falling, or, having none of those, until it is destroyed. Asked many times a
        /// frame by the panel, so answered once per frame.
        /// </summary>
        private static bool Alive(GameObject thing)
        {
            if (thing == null) return false;
            if (_aliveFrame != Time.frameCount)
            {
                _aliveFrame = Time.frameCount;
                AliveNow.Clear();
                var gone = new List<GameObject>();
                foreach (var born in Born) if (born.Key == null) gone.Add(born.Key);
                foreach (var key in gone) Born.Remove(key);
            }
            if (AliveNow.TryGetValue(thing, out var known)) return known;

            var alive = Born.TryGetValue(thing, out var at) && Time.unscaledTime - at < 0.6f;
            if (!alive && thing.activeInHierarchy)
            {
                var moving = false;
                if (thing.GetComponent<Standin>() != null || thing.GetComponentInChildren<Rigidbody>() != null) alive = true;
                foreach (var particles in thing.GetComponentsInChildren<ParticleSystem>())
                {
                    moving = true;
                    if (particles.IsAlive(false)) alive = true;
                }
                foreach (var source in thing.GetComponentsInChildren<AudioSource>())
                {
                    moving = true;
                    if (source.isPlaying) alive = true;
                }
                if (!moving) alive = true;
            }
            AliveNow[thing] = alive;
            return alive;
        }

        /// <summary>Whether the selected model is also shown in the world, where you were looking.</summary>
        public static bool InWorld { get; private set; }

        /// <summary>Plays an effect on the stage again as soon as it ends.</summary>
        public static bool LoopEffects = true;

        /// <summary>Plays a sound again as soon as it ends.</summary>
        public static bool LoopSounds;

        public static float ProjectileSpeed = 40f;

        public static bool SoundPlaying => _sound != null;
        public static bool StatusShowing => _status != null && StatusVisuals.Count > 0;
        public static int PinnedCount => Pinned.Count;
        public static bool AnythingInWorld => _world != null || Pinned.Count > 0 || Played.Count > 0 || _sound != null || StatusVisuals.Count > 0;

        /// <summary>How many things Scry has out in the world or playing, for the Clear button.</summary>
        public static int OutCount => (_world != null ? 1 : 0) + Pinned.Count + Played.Count + (_sound != null ? 1 : 0) + StatusVisuals.Count;

        public static void Update(Explorer explorer)
        {
            // Each part on its own: one that fails does not keep the others from running.
            try { Expire(); } catch (System.Exception ex) { Faults.Tell("expiring previews", ex); }
            try { Listen.Update(); } catch (System.Exception ex) { Faults.Tell("listening", ex); }
            // Working out what clips play waits while the panel is closed, and goes on when it opens.
            var started = Timing.Start();
            if (explorer != null)
            {
                try { TriggerProbe.Update(); } catch (System.Exception ex) { Faults.Tell("watching an animator", ex); }
                try { SortSomeClips(); } catch (System.Exception ex) { Faults.Tell("sorting clips", ex); }
            }
            Timing.Add("update probe", started);
            for (var i = LaterOn.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < LaterOn[i].At) continue;
                var act = LaterOn[i].Act;
                LaterOn.RemoveAt(i);
                try { act(); } catch (System.Exception ex) { Faults.Tell("a later step", ex); }
            }

            if (explorer == null) return;

            if (explorer != _explorer)
            {
                _explorer = explorer;
                _selectionVersion = -1;
            }

            var modifiers = explorer.Modifiers;
            if (explorer.SelectionVersion != _selectionVersion)
            {
                _selectionVersion = explorer.SelectionVersion;
                _modifierVersion = modifiers.Version;
                _stageStale = false;
                started = Timing.Start();
                try { Selected(explorer.Selected, modifiers); } catch (System.Exception ex) { Faults.Tell("showing the selection", ex); }
                Timing.Add("update selection", started);
            }
            else if (modifiers.Version != _modifierVersion)
            {
                _modifierVersion = modifiers.Version;
                started = Timing.Start();
                try { Modified(modifiers); } catch (System.Exception ex) { Faults.Tell("changing the preview", ex); }
                Timing.Add("update modifiers", started);
            }

            if (_stageStale)
            {
                _stageStale = false;
                try { Stage.Show(_entry, modifiers); } catch (System.Exception ex) { Faults.Tell("showing the stage", ex); }
            }

            try { Repeat(modifiers); } catch (System.Exception ex) { Faults.Tell("repeating", ex); }
        }

        /// <summary>
        /// Lets go of what was found out about the prefabs of the world left: they are made anew
        /// in the next, and a mod's may differ there.
        /// </summary>
        public static void Forget()
        {
            Undo.Clear();
            ClipPlaysCache.Clear();
            Wholes.Clear();
            WeaponOf.Clear();
            OnGround.Clear();
            ClipOf.Clear();
            AttackOf.Clear();
            ToldEmpty.Clear();
            GroundsOf.Clear();
            Born.Clear();
            _carriedPrefab = null;
            _carried = null;
            _clipsOf = null;
            _clips = null;
            _ofList = null;
            _ofListPlays = null;
            _ofListCopy = null;
            _ofListClips = null;
            _plays = null;
            _playsPrefab = null;
            _playsCopy = null;
        }

        /// <summary>
        /// Takes the stage copy down while the panel is closed, so a looping effect does not go on
        /// sounding in your ears; it is put back when the panel opens again. A sound playing at
        /// your ears stops with it, looping ones such as location music included. What stands or
        /// plays in the world stays.
        /// </summary>
        public static void Suspend()
        {
            Stage.ClearSubject();
            _stageStale = true;
            StopSound();
        }

        private static void Selected(Entry entry, Modifiers modifiers)
        {
            _entry = entry;
            LastClip = null;
            _builtLevel = modifiers.Level;
            _builtWear = modifiers.Wear;
            _builtLook = modifiers.Look;
            _replayAt = -1f;

            StopSound();
            var started = Timing.Start();
            Stage.Show(entry, modifiers);
            Timing.Add("selection stage", started);
            started = Timing.Start();
            if (InWorld) RebuildWorld(modifiers);
            Timing.Add("selection world", started);

            // Only the animators now shown are still worth watching.
            var shown = new List<string>(2);
            foreach (var copy in new[] { Stage.Subject, _world })
            {
                var ears = ClipPlayer.AnimatorOf(copy)?.GetComponent<AnimationEars>();
                if (ears != null && ears.Prefab != null) shown.Add(ears.Prefab.name);
            }
            TriggerProbe.CancelAllBut(shown);

            if (_clipOnShow != null)
            {
                var clip = ClipNamed(_clipOnShow);
                _clipOnShow = null;
                if (clip != null) PlayClip(clip);
            }

            if (entry != null && entry.Kind == Kind.Sound && Plugin.PlayOnSelect) PlaySound(entry);
        }

        private static void Modified(Modifiers modifiers)
        {
            if (modifiers.Level != _builtLevel || modifiers.Wear != _builtWear || modifiers.Look != _builtLook)
            {
                _builtLevel = modifiers.Level;
                _builtWear = modifiers.Wear;
            _builtLook = modifiers.Look;
                Stage.Show(_entry, modifiers);
                if (InWorld) RebuildWorld(modifiers);
                return;
            }

            Stage.Apply(modifiers);
            ApplyLive(_world, modifiers);
        }

        /// <summary>Loops a finished effect on the stage, and a finished sound, while the panel is open.</summary>
        private static void Repeat(Modifiers modifiers)
        {
            if (_entry == null) return;

            if (LoopEffects && _entry.Kind == Kind.Effect && Stage.Finished)
            {
                if (_replayAt < 0f) _replayAt = Time.unscaledTime + 0.4f;
                else if (Time.unscaledTime >= _replayAt)
                {
                    _replayAt = -1f;
                    Stage.Show(_entry, modifiers);
                }
            }

            // A sound stopped by hand is not replayed; one that ran out is.
            // A chosen variant repeats as itself; a random play picks again each time, as in the game.
            if (LoopSounds && _entry.Kind == Kind.Sound && _soundEntry == _entry && _sound == null) PlaySound(_entry, _soundChosen);
        }

        // ----- Making again -----

        /// <summary>Makes the stage copy and the copy in the world again, after a switch that changes what they are.</summary>
        public static void Rebuild()
        {
            if (_explorer == null) return;
            Stage.Show(_entry, _explorer.Modifiers);
            if (InWorld) RebuildWorld(_explorer.Modifiers);
        }

        /// <summary>Restarts what is on the stage, for effects that have played out.</summary>
        public static void Replay()
        {
            if (_explorer != null) Stage.Show(_entry, _explorer.Modifiers);
        }

        // ----- Housekeeping -----

        private static void Remember(GameObject thing, float seconds)
        {
            if (thing != null) Played.Add(new Timed { Thing = thing, Until = Time.unscaledTime + seconds });
        }

        private static void Expire()
        {
            var now = Time.unscaledTime;
            for (var i = Played.Count - 1; i >= 0; i--)
            {
                var played = Played[i];
                if (played.Thing != null && now < played.Until) continue;
                if (played.Thing != null) Object.Destroy(played.Thing);
                Played.RemoveAt(i);
            }

            if (_sound != null && !SoundAlive(now)) Destroy(ref _sound);
            for (var i = StatusVisuals.Count - 1; i >= 0; i--) if (StatusVisuals[i] == null) StatusVisuals.RemoveAt(i);
            Pinned.RemoveAll(p => p == null);
        }

        private static void Destroy(ref GameObject thing)
        {
            if (thing != null) Object.Destroy(thing);
            thing = null;
        }
    }
}
