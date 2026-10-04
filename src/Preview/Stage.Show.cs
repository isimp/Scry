using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>What the stage shows: the entry asked for, made at once or a little each frame for a large place, then presented on its ground with its floors, creatures and paints.</summary>
    internal static partial class Stage
    {
        /// <summary>The floors found in the place shown, kept for when a dungeon's example is left for its entrance.</summary>
        private static List<float> _placeFloors = new List<float>();

        private static Entry _lastShown;
        private static GameObject _subject;
        private static bool _subjectIsPerson;
        private static Vector3 _baseScale = Vector3.one;
        private static float _scale = 1f;
        private static float _madeAt;
        private static int _wantedFrame = -10;
        private static int _width = 512;
        private static int _height = 512;
        private static float _sizeSince;
        private static readonly List<ParticleSystem> Particles = new List<ParticleSystem>();
        private static readonly List<AudioSource> Sources = new List<AudioSource>();
        private static readonly List<Animator> Animators = new List<Animator>();

        // A creature that walks is measured for a short moment once its animation runs, a flyer
        // over its flight; the floor is under what it draws then.
        private static bool _onFeet;
        private static bool _followEffect;

        /// <summary>Takes the copy shown off the stage, with what plays on it and a place still being built.</summary>
        public static void ClearSubject()
        {
            _building?.Cancel();
            _buildingSpawns = null;
            ForgetCreatures();
            _building = null;
            _buildingWith = null;
            ForgetExample();
            ForgetGroundPaints(rooms: false);
            if (_subject != null) Object.Destroy(_subject);
            _subject = null;
            ClearPlayed();
        }

        /// <summary>Lets go of the entry last shown and the paints of a place being built, as the stage is taken down.</summary>
        private static void ForgetShown()
        {
            _lastShown = null;
            _buildingPaints = null;
        }

        /// <summary>Asked by the panel on each frame it shows the stage, with the size it shows it at.</summary>
        public static void Request(int width, int height)
        {
            _wantedFrame = Time.frameCount;
            width = Mathf.Clamp(width, 64, 2048);
            height = Mathf.Clamp(height, 64, 2048);
            if (width == _width && height == _height) return;
            _width = width;
            _height = height;
            _sizeSince = Time.unscaledTime;
        }

        /// <summary>The entry the stage was last asked to show, whose copy is <see cref="Subject"/> once made.</summary>
        public static Entry Showing => _lastShown;

        /// <summary>How often the stage was asked to show something, and how often a copy came of it, for the self-test to tell.</summary>
        public static int Shows { get; private set; }

        public static int CopiesMade { get; private set; }

        /// <summary>Puts a fresh copy of the entry on the stage, with the modifiers applied.</summary>
        public static void Show(Entry entry, Modifiers modifiers)
        {
            Shows++;
            ClearSubject();
            if (entry != _lastShown)
            {
                _lastShown = entry;
                ResetView();
            }
            if (!IsStaged(entry) || !(entry.Source is GameObject || entry.Source is StatusEffect || entry.Source is PlaceSource || entry.Source is RandomEvent)) return;
            if (!Ensure()) return;

            // Making it and dressing it are told as "selection copy" and "selection dress",
            // measuring it and applying the modifiers as "selection measure" and "selection apply".
            // A location or room is made once its bundle has loaded (PlaceAssets), which shows it
            // again, and then over the next frames, a little each (StepBuild).
            _subjectIsPerson = Looks.IsWorn(entry) || entry.Kind == Kind.StatusEffect;
            if (entry.Source is PlaceSource place)
            {
                var asset = PlaceAssets.Asset(place);
                if (asset == null) return;
                _buildingSpawns = new List<SpawnHere>();
                _buildingPaints = new List<GroundPaintAt>();
                _building = PlaceCopy.Begin(asset, _root.transform, Origin, Quaternion.identity, _layer, keepColliders: true, spawns: _buildingSpawns, paints: _buildingPaints);
                _buildingWith = modifiers;
                StepBuild();
                return;
            }
            if (entry.Source is RandomEvent raid)
            {
                // A raid as the first roll of each of its creatures, rolled anew with each copy.
                var made = Timing.Start();
                _subject = RaidCrowd.Make(entry, raid, _root.transform, Origin, _layer);
                Timing.Add("selection copy", made);
            }
            else
            {
                _subject = Looks.Copy(entry, modifiers, _root.transform, Origin, Quaternion.identity, _layer, "selection");
            }
            if (_subject == null) return;
            Present(entry, modifiers);
        }

        /// <summary>A few milliseconds a frame for making a location or room.</summary>
        private const double BuildBudgetMs = 8.0;

        private static Ghost.Building _building;
        private static Modifiers _buildingWith;

        /// <summary>The spawn points of the location or room being made, its creatures rolled once it stands.</summary>
        private static List<SpawnHere> _buildingSpawns;

        /// <summary>The paints on the ground read off the location's copy as it is made.</summary>
        private static List<GroundPaintAt> _buildingPaints;

        private static int _builtInFrame = -1;

        /// <summary>Whether a location or room is still being made, shown once it is.</summary>
        public static bool Building => _building != null;

        /// <summary>How many frames the last location or room took to make, and how many parts it has, for the self-test to tell.</summary>
        public static int LastBuildFrames { get; private set; }

        public static int LastBuildParts { get; private set; }

        /// <summary>
        /// Goes on making the location or room shown, a few milliseconds a frame, and shows it
        /// once all of it is awake, with its floors read from its colliders.
        /// </summary>
        public static void StepBuild()
        {
            if (_building == null || _builtInFrame == Time.frameCount) return;
            _builtInFrame = Time.frameCount;
            if (!_building.Go(BuildBudgetMs)) return;

            var built = _building;
            var modifiers = _buildingWith;
            _building = null;
            _buildingWith = null;
            LastBuildFrames = built.Frames;
            LastBuildParts = built.Parts;
            _subject = built.Result;
            if (_subject == null || !(_lastShown?.Source is PlaceSource place)) return;

            var probed = Timing.Start();
            _placeFloors = FloorsOf(_subject, place);
            Timing.Add("selection floors", probed);
            Present(_lastShown, modifiers);
            Populate(_buildingSpawns, null);
            _buildingSpawns = null;
            TakeGroundPaints(_buildingPaints, null);
            _buildingPaints = null;
        }

        /// <summary>Tunes, measures and stands the copy just made, and applies the modifiers.</summary>
        private static void Present(Entry entry, Modifiers modifiers)
        {
            CopiesMade++;
            Tune(_subject, entry.Kind == Kind.Effect);

            var started = Timing.Start();
            _madeAt = Time.unscaledTime;
            _followEffect = entry.Kind == Kind.Effect;
            var character = (entry.Source as GameObject).OrNull()?.GetComponent<Character>();
            _onFeet = character != null && !character.m_flying;
            // The first pose, before the animation has run, can stand far from where the model
            // stands after (lying, raised, off to a side), so the size is taken again once it
            // has run a moment; a flying creature is measured over its flight a while longer.
            _baseScale = _subject.transform.localScale;
            MeasureNew(entry);

            // A room opens on its top floor to be looked into; a location keeps its roof.
            if (entry.Source is PlaceSource shown) SetFloors(entry, _placeFloors, open: shown.IsRoom);
            else
            {
                ClearFloors();
            }
            Timing.Add("selection measure", started);

            started = Timing.Start();
            Apply(modifiers);
            Timing.Add("selection apply", started);
        }

        /// <summary>Whether an entry has something to put on the stage.</summary>
        public static bool IsStaged(Entry entry)
        {
            if (entry == null || entry.Kind == Kind.Sound || entry.Kind == Kind.Mod || entry.Kind == Kind.Biome) return false;
            if (entry.Kind == Kind.Raid) return RaidCrowd.Brings(entry.Source as RandomEvent);
            if (entry.Kind == Kind.StatusEffect) return Looks.ShowsOnPerson(entry);
            return !entry.Empty;
        }

        /// <summary>The modifiers that change without making a new copy.</summary>
        public static void Apply(Modifiers modifiers)
        {
            if (_subject == null) return;

            _scale = modifiers.Scale;
            _subject.transform.localScale = _baseScale * modifiers.Scale;
            _subject.GetComponentsInChildren(true, Animators);
            foreach (var animator in Animators) animator.speed = modifiers.AnimationSpeed;
            Animators.Clear();
            ClipPlayer.SetSpeed(_subject, modifiers.AnimationSpeed);
            PlacePerson();
        }

        /// <summary>
        /// Whether what is on the stage has played out: destroyed by its own timer, or with no
        /// particles alive and no sound playing. Only meaningful for effects.
        /// </summary>
        public static bool Finished
        {
            get
            {
                if (_subject == null) return true;
                if (Time.unscaledTime - _madeAt < 0.5f) return false;

                // Something with neither particles nor sound only ends when it is destroyed.
                _subject.GetComponentsInChildren(false, Particles);
                _subject.GetComponentsInChildren(false, Sources);
                var moving = Particles.Count > 0 || Sources.Count > 0;
                var playing = Particles.Exists(p => p.IsAlive(false)) || Sources.Exists(s => s.isPlaying);
                Particles.Clear();
                Sources.Clear();
                return moving && !playing;
            }
        }
    }
}
