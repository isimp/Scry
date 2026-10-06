using System.Collections.Generic;
using System.Linq;
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

        /// <summary>What the previews have noted in the log, each once a session.</summary>
        private static readonly HashSet<string> Told = new HashSet<string>();

        /// <summary>Whether a note is new this session, noting it as told.</summary>
        private static bool FirstTime(string key) => Told.Add(key);

        private static Explorer _explorer;
        private static int _selectionVersion = -1;
        private static int _modifierVersion = -1;
        private static Entry _entry;
        private static int _builtLevel = 1;
        private static Wear _builtWear;
        private static int _builtLook;
        private static float _replayAt = -1f;
        private static bool _stageStale;

        /// <summary>What each play button started, so it stays lit until all of it has finished.</summary>
        public static readonly Playback<GameObject> Playing = new Playback<GameObject>(Alive);

        private static readonly Dictionary<GameObject, float> Born = new Dictionary<GameObject, float>();
        private static readonly Dictionary<GameObject, bool> AliveNow = new Dictionary<GameObject, bool>();

        /// <summary>Copies gone since they were made, let go of once a frame; kept to be filled again rather than made each frame.</summary>
        private static readonly List<GameObject> Gone = new List<GameObject>();
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

        /// <summary>
        /// Stops what a lit button started: its copies go, a fallen or broken copy stands again,
        /// and an animation it started ends.
        /// </summary>
        public static void Stop(object key)
        {
            foreach (var thing in Playing.Take(key)) if (thing != null) Object.Destroy(thing);
            UndoStarted(key);
        }

        /// <summary>What a playing animation made of itself, lit under its clip.</summary>
        public static void Heard(AnimationClip clip, IEnumerable<GameObject> things)
        {
            if (clip == null) return;
            var made = things.ToList();
            Started(clip, made, pressed: false);
            if (_litWith.Clip == clip && _litWith.Key != null) Started(_litWith.Key, made, pressed: false);
        }

        /// <summary>The clip whose chip was pressed last, for showing what it plays.</summary>
        public static AnimationClip LastClip { get; set; }

        /// <summary>The ground footsteps are heard on: the one the stage shows (<see cref="StageGround.Footsteps"/>).</summary>
        public static FootStep.GroundMaterial StepGround => (FootStep.GroundMaterial)StageGround.Footsteps(Stage.GroundUnderfoot, Stage.GroundUnderfoot != null);

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
                foreach (var born in Born) if (born.Key == null) Gone.Add(born.Key);
                foreach (var key in Gone) Born.Remove(key);
                Gone.Clear();
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

        /// <summary>
        /// Whether what plays (a clip, an effect, a sound) plays again as soon as it ends: one
        /// switch for all of them, remembered by the panel. Setting it sets the clip playing too.
        /// </summary>
        public static bool Repeat
        {
            get => _repeat;
            set
            {
                _repeat = value;
                LoopPlayingClip(value);
            }
        }

        private static bool _repeat;

        public static float ProjectileSpeed { get; set; } = 40f;

        public static bool SoundPlaying => TheSound.Copy != null;
        public static void Update(Explorer explorer)
        {
            // How loud previews play follows the selection, which starts every one at the game's
            // own loudness; with the panel closed, whatever still plays does so at the game's own.
            Loudness.Follow(explorer?.Modifiers);
            Loudness.Gain = explorer != null ? explorer.Modifiers.Volume : 1f;

            // Each part on its own: one that fails does not keep the others from running.
            Guard.Run(Feature.Previews, "expiring previews", Expire);
            Guard.Run(Feature.Previews, "listening", Listen.Update);
            Guard.Run(Feature.LocationMusic, "playing a location's music", MusicPreview.Update);
            Guard.Run(Feature.SoundsAfterSeek, "seeking a sound", SettleSeek);
            Guard.Run(Feature.Previews, "making a copy", Ghost.Building.Tick);
            // Working out what clips play waits while the panel is closed, and goes on when it opens.
            var started = Timing.Start();
            if (explorer != null)
            {
                var step = Timing.Start();
                Guard.Run(Feature.Animations, "watching an animator", TriggerProbe.Update);
                Timing.Add("probe watch", step);
                step = Timing.Start();
                Guard.Run(Feature.Animations, "sorting clips", SortSomeClips);
                Timing.Add("probe clips", step);
            }
            Timing.Add("update probe", started);
            RunLaterSteps();
            // An attack asked for before its clip was known plays once it is.
            if (explorer != null) Guard.Run(Feature.Animations, "playing an attack asked for", PlayAttackAsked);

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
                Guard.Run(Feature.Previews, "showing the selection", e => Selected(e.Selected, e.Modifiers), explorer, "update selection");
            }
            else if (modifiers.Version != _modifierVersion)
            {
                _modifierVersion = modifiers.Version;
                Guard.Run(Feature.Previews, "changing the preview", Modified, modifiers, "update modifiers");
            }

            // A location or room selected: its bundle is loaded and held while it is shown, what it
            // holds read once it is in, and its stage copy made then. Its details say how far the
            // loading has got, so they are told again as that changes.
            Guard.Run(Feature.LocationsOnStage, "loading a location", LoadPlace, explorer);

            // A location or room is put on the stage a little each frame, shown once it is made, and its creatures after.
            Guard.Run(Feature.LocationsOnStage, "showing a location", Stage.StepBuild);
            Guard.Run(Feature.PlacesCreaturesOnStage, "putting a place's creatures on the stage", Stage.StepCreatures);

            // A dungeon or camp selected: its rooms are read for an example layout.
            Guard.Run(Feature.ExampleDungeonAndCampLayouts, "laying out an example dungeon", ExampleLayouts.Update, explorer);

            if (_stageStale)
            {
                _stageStale = false;
                Guard.Run(Feature.Stage, "showing the stage", m => Stage.Show(_entry, m), modifiers);
            }

            Guard.Run(Feature.Previews, "repeating", PlayAgain, modifiers);
        }

        /// <summary>The selected location's or room's bundle held and loaded, what it holds read once it is in.</summary>
        private static void LoadPlace(Explorer explorer)
        {
            if (PlaceAssets.Hold(explorer.Selected?.Source as PlaceSource)) Learned.About(explorer.Selected);
            switch (PlaceAssets.Update())
            {
                case PlaceLoad.Ready:
                    PlaceLoaded(explorer, explorer.Selected);
                    break;
                case PlaceLoad.Failed:
                    Learned.About(explorer.Selected);
                    break;
            }
        }

        /// <summary>
        /// Lets go of what was found out about the prefabs of the world left: they are made anew
        /// in the next, and a mod's may differ there.
        /// </summary>
        public static void Forget()
        {
            // The explorer holds the whole catalog of the world left; it is only given again while
            // the panel is open, so it is let go here rather than kept into the next world.
            _explorer = null;
            _entry = null;
            _selectionVersion = -1;
            TheSound.ForgetEntry();
            Born.Clear();
            ForgetEffects();
            ForgetClips();
            ForgetAttacks();
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

            // A location's bundle is loaded again when the panel opens on it, and any dungeon
            // room being read for its example layout.
            PlaceAssets.Release();
            ExampleLayouts.Pause();
        }

        /// <summary>
        /// The selected location or room has loaded: what it holds is read, the first time, and its
        /// entry named as the game names it (a Burial Chamber rather than "Crypt"), then its copy
        /// is put on the stage.
        /// </summary>
        private static void PlaceLoaded(Explorer explorer, Entry entry)
        {
            if (!(entry?.Source is PlaceSource place)) return;
            var asset = PlaceAssets.Asset(place);
            if (asset == null) return;
            if (place.Contents == null)
            {
                place.Contents = PlaceReader.Read(asset, place.IsRoom);
                if (!place.IsRoom)
                {
                    var facts = new PlaceFacts
                    {
                        Prefab = place.Prefab, Biome = PlaceEntries.BiomeWords(place.Biomes),
                        GameName = place.Contents.GameName, Boss = place.Contents.Boss, Trader = place.Contents.Trader,
                    };
                    PlaceEntries.Named(entry, new[] { Places.LocationLabel(facts, PlaceEntries.CreatureNames(explorer.Catalog)) });
                }
                Learned.About(entry);

                // A dungeon or camp read now gathers its rooms into its group.
                PlaceEntries.Arrange(explorer.Catalog);
                explorer.Entries.Regroup();
            }
            Stage.Show(entry, explorer.Modifiers);
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
                var ears = ClipPlayer.AnimatorOf(copy).OrNull()?.GetComponent<AnimationEars>();
                if (ears != null && ears.Prefab != null) shown.Add(ears.Prefab.name);
            }
            TriggerProbe.CancelAllBut(shown);

            PlayClipAsked();

            if (entry != null && entry.Kind == Kind.Sound && Settings.PlayOnSelect) PlaySound(entry);
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
        private static void PlayAgain(Modifiers modifiers)
        {
            if (_entry == null) return;

            if (Repeat && _entry.Kind == Kind.Effect && Stage.Finished)
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
            if (Repeat && _entry.Kind == Kind.Sound && TheSound.Entry == _entry && TheSound.Copy == null) PlaySound(_entry, TheSound.Chosen);
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
    }
}
