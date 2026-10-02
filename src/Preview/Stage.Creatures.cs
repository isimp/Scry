using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The creatures a location's or room's spawn points put there, standing on the stage with it
    /// (<see cref="SpawnPoints"/>): rolled anew with every copy, an example's rooms each with
    /// their own, every point whatever the world's progress or the time of day. Each stands on
    /// the ground under its point, as the game drops what it spawns, one that flies as high over
    /// it as it keeps at the least, facing a way of its own, with the stars it rolled, held in its first pose,
    /// muted, dimmed with its room; a few are made each frame. They stand on a layer of their own:
    /// what of them is above a floor's cut is drawn again over the picture, so a tall one stands
    /// whole. The stage's Creatures chip puts them away and back.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>A spawn point read off a copy before it was stripped: where it is, what it puts there, and how.</summary>
        internal sealed class SpawnHere
        {
            public Transform At;
            public GameObject Creature;
            public SpawnPoint Point;

            /// <summary>How far below the point its creature stands, on the first solid thing under it (<see cref="FloorProbe"/>); whether anything was found.</summary>
            public float Drop;
            public bool Grounded;

            /// <summary>How high above that ground its creature flies; 0 for one that walks.</summary>
            public float Lift;

            /// <summary>How far above the point its creature stands: on the ground under it, or flying over it, as the game has it; at the point where no ground was found.</summary>
            public float Rise => SpawnPoints.Rise(Grounded, Drop, Lift);
        }

        private static readonly System.Random CreatureDice = new System.Random();

        /// <summary>A few milliseconds a frame for making creatures, and at least one.</summary>
        private const double CreatureBudgetMs = 4.0;

        /// <summary>The creatures rolled and waiting to be made, each with its point, how far above it it stands and flies, its level and the example's room it stands in, if any.</summary>
        private static readonly List<(Transform At, float Rise, float Lift, GameObject Creature, int Level, GameObject Room)> CreaturesToMake = new List<(Transform, float, float, GameObject, int, GameObject)>();

        /// <summary>How high over its ground each creature standing flies, 0 for one that walks.</summary>
        private static readonly Dictionary<GameObject, float> CreatureLift = new Dictionary<GameObject, float>();

        /// <summary>The creatures standing, each with the example's room it stands in, if any.</summary>
        private static readonly List<KeyValuePair<GameObject, GameObject>> CreatureCopies = new List<KeyValuePair<GameObject, GameObject>>();

        private static bool _creaturesShown = true;

        /// <summary>Whether the creatures stand on the stage or are put away, as the stage's chip has it.</summary>
        public static bool CreaturesShown
        {
            get => _creaturesShown;
            set
            {
                if (value == _creaturesShown) return;
                _creaturesShown = value;
                foreach (var pair in CreatureCopies) if (pair.Key != null) pair.Key.SetActive(value);
            }
        }

        /// <summary>Whether the place shown puts any creature there.</summary>
        public static bool HasCreatures => CreatureCopies.Count > 0 || CreaturesToMake.Count > 0;

        /// <summary>How many creatures stand made, and how many wait to be, for the self-test.</summary>
        public static int CreaturesMade => CreatureCopies.Count(pair => pair.Key != null);
        public static int CreaturesWaiting => CreaturesToMake.Count;

        /// <summary>How many creatures stand switched on, for the self-test.</summary>
        public static int CreaturesStanding => CreatureCopies.Count(pair => pair.Key != null && pair.Key.activeSelf);

        /// <summary>Where each creature standing on the stage draws, for the self-test.</summary>
        public static List<Bounds> CreatureBoundsNow() =>
            CreatureCopies.Where(pair => pair.Key != null && pair.Key.activeInHierarchy).Select(pair => Measure(pair.Key)).ToList();

        /// <summary>The names of the creatures made, for the self-test to tell.</summary>
        public static IEnumerable<string> CreatureNames => CreatureCopies.Where(pair => pair.Key != null).Select(pair => pair.Key.name);

        /// <summary>Rolls which of a copy's spawn points put their creature there, to be made over the next frames.</summary>
        private static void Populate(List<SpawnHere> points, GameObject room)
        {
            if (points == null || points.Count == 0) return;
            var facts = points.Select(p => p.Point).ToList();
            foreach (var (point, level) in SpawnPoints.Roll(facts, CreatureDice.NextDouble))
            {
                var here = points[point];
                CreaturesToMake.Add((here.At, here.Rise, here.Grounded ? here.Lift : 0f, here.Creature, level, room));
                if (here.Lift > 0f) CreaturesFlying++;
                else if (here.Grounded && here.Drop > 0.05f) CreaturesDropped++;
            }
        }

        /// <summary>Each frame: makes a few of the creatures waiting, where their points are.</summary>
        public static void StepCreatures()
        {
            if (CreaturesToMake.Count == 0 || _root == null) return;
            var made = Timing.Start();
            var watch = Stopwatch.StartNew();
            var any = false;
            while (CreaturesToMake.Count > 0 && (!any || watch.Elapsed.TotalMilliseconds < CreatureBudgetMs))
            {
                var (at, rise, lift, creature, level, room) = CreaturesToMake[0];
                CreaturesToMake.RemoveAt(0);
                if (at == null || creature == null) continue;
                any = true;
                var copy = MakeCreature(creature, at, rise, level);
                if (copy == null) continue;
                if (room != null && DimmedRooms.Contains(room)) Dim.Set(copy, true);
                if (!_creaturesShown) copy.SetActive(false);
                CreatureCopies.Add(new KeyValuePair<GameObject, GameObject>(copy, room));
                CreatureLift[copy] = lift;
            }
            Timing.Add("stage creatures", made);
        }

        /// <summary>
        /// A creature copy on the ground under a spawn point, as the game drops what it spawns,
        /// or flying over it as high as it keeps at the least, facing a way of its own as the game
        /// turns it, at the level rolled, dressed as a creature selected is; it hangs on the point,
        /// keeping its size, so it goes with what the point stands in.
        /// </summary>
        private static GameObject MakeCreature(GameObject prefab, Transform at, float rise, int level)
        {
            var turn = Quaternion.Euler(0f, (float)(CreatureDice.NextDouble() * 360.0), 0f);
            var standing = at.position + Vector3.up * rise;
            var entry = EntryOf(prefab.name);
            GameObject copy;
            if (entry != null)
            {
                var modifiers = new Modifiers();
                modifiers.ResetFor(entry);
                modifiers.Level = level;
                copy = Looks.Copy(entry, modifiers, _root.transform, standing, turn, _layer);
            }
            else
            {
                copy = Ghost.Make(prefab, _root.transform, standing, turn, _layer);
            }
            if (copy == null) return null;
            copy.transform.SetParent(at, true);
            Tune(copy, audible: false);
            Ghost.SetLayer(copy.transform, CreatureLayer);

            // Held in its first pose, to spare the frame an animator running for each.
            foreach (var animator in copy.GetComponentsInChildren<Animator>(true))
            {
                if (animator == null || !animator.isActiveAndEnabled) continue;
                animator.Update(0f);
                animator.enabled = false;
            }
            return copy;
        }

        private static Entry EntryOf(string key) => Session.Explorer?.Catalog.FirstOrDefault(e => e.Key == key);

        /// <summary>How many creatures rolled stand below their spawn point, dropped to the ground under it, and how many fly over it, for the self-test.</summary>
        public static int CreaturesDropped { get; private set; }
        public static int CreaturesFlying { get; private set; }

        private static float _creaturesCutAt = float.NaN;
        private static int _creaturesCutCount = -1;

        /// <summary>
        /// With a floor's cut laid, a creature standing above it, on a floor above, is cut away
        /// with that floor rather than drawn again over the picture: it stands on the stage's own
        /// layer while the cut is above its feet. Put right as the cut moves or creatures come.
        /// </summary>
        private static void KeepCreaturesToCut()
        {
            if (CreatureLayer == _layer) return;
            var cut = Cutting && _subject != null ? Origin.y + CutAt * _scale : float.PositiveInfinity;
            if (cut.Equals(_creaturesCutAt) && CreatureCopies.Count == _creaturesCutCount) return;
            _creaturesCutAt = cut;
            _creaturesCutCount = CreatureCopies.Count;
            foreach (var pair in CreatureCopies)
            {
                var creature = pair.Key;
                if (creature == null) continue;
                CreatureLift.TryGetValue(creature, out var lift);
                var layer = SpawnPoints.AboveCut(creature.transform.position.y, lift, cut) ? _layer : CreatureLayer;
                if (creature.layer != layer) Ghost.SetLayer(creature.transform, layer);
            }
        }

        /// <summary>How many creatures stand above the cut, cut away with their floor, for the self-test.</summary>
        public static int CreaturesAboveCut => CreatureCopies.Count(pair => pair.Key != null && pair.Key.layer == _layer && CreatureLayer != _layer);

        /// <summary>Lets go of the example's rooms' creatures, as its rooms go; the location's own stay.</summary>
        private static void ForgetRoomCreatures()
        {
            CreaturesToMake.RemoveAll(c => c.Room != null);
            foreach (var pair in CreatureCopies) if (pair.Value != null) CreatureLift.Remove(pair.Key);
            CreatureCopies.RemoveAll(pair => pair.Value != null);
            CreaturesDropped = 0;
            CreaturesFlying = 0;
        }

        /// <summary>Lets go of every creature, as the copy they stand on goes.</summary>
        private static void ForgetCreatures()
        {
            CreaturesToMake.Clear();
            CreatureCopies.Clear();
            CreatureLift.Clear();
            CreaturesDropped = 0;
            CreaturesFlying = 0;
            FloorProbe.ForgetNotSolid();
        }
    }
}
