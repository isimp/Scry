using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The creatures a location's or room's spawn points put there, standing on the stage with it
    /// (<see cref="SpawnPoints"/>): rolled anew with every copy, an example's rooms each with
    /// their own, every point whatever the world's progress or the time of day. Each idles where
    /// its point is, facing a way of its own, with the stars it rolled, muted, dimmed with its
    /// room; a few are made each frame. The stage's Creatures chip puts them away and back.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>A spawn point read off a copy before it was stripped: where it is, what it puts there, and how.</summary>
        internal sealed class SpawnHere
        {
            public Transform At;
            public GameObject Creature;
            public SpawnPoint Point;
        }

        private static readonly System.Random CreatureDice = new System.Random();

        /// <summary>A few milliseconds a frame for making creatures, and at least one.</summary>
        private const double CreatureBudgetMs = 4.0;

        /// <summary>The creatures rolled and waiting to be made, each with its point, level and the example's room it stands in, if any.</summary>
        private static readonly List<(Transform At, GameObject Creature, int Level, GameObject Room)> CreaturesToMake = new List<(Transform, GameObject, int, GameObject)>();

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

        /// <summary>The names of the creatures made, for the self-test to tell.</summary>
        public static IEnumerable<string> CreatureNames => CreatureCopies.Where(pair => pair.Key != null).Select(pair => pair.Key.name);

        /// <summary>Rolls which of a copy's spawn points put their creature there, to be made over the next frames.</summary>
        private static void Populate(List<SpawnHere> points, GameObject room)
        {
            if (points == null || points.Count == 0) return;
            var facts = points.Select(p => p.Point).ToList();
            foreach (var (point, level) in SpawnPoints.Roll(facts, CreatureDice.NextDouble))
            {
                CreaturesToMake.Add((points[point].At, points[point].Creature, level, room));
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
                var (at, creature, level, room) = CreaturesToMake[0];
                CreaturesToMake.RemoveAt(0);
                if (at == null || creature == null) continue;
                any = true;
                var copy = MakeCreature(creature, at, level);
                if (copy == null) continue;
                if (room != null && DimmedRooms.Contains(room)) Dim.Set(copy, true);
                if (!_creaturesShown) copy.SetActive(false);
                CreatureCopies.Add(new KeyValuePair<GameObject, GameObject>(copy, room));
            }
            Timing.Add("stage creatures", made);
        }

        /// <summary>
        /// A creature copy at a spawn point, facing a way of its own as the game turns one it
        /// spawns, at the level rolled, dressed as a creature selected is; it hangs on the point,
        /// keeping its size, so it goes with what the point stands in.
        /// </summary>
        private static GameObject MakeCreature(GameObject prefab, Transform at, int level)
        {
            var turn = Quaternion.Euler(0f, (float)(CreatureDice.NextDouble() * 360.0), 0f);
            var entry = EntryOf(prefab.name);
            GameObject copy;
            if (entry != null)
            {
                var modifiers = new Modifiers();
                modifiers.ResetFor(entry);
                modifiers.Level = level;
                copy = Looks.Copy(entry, modifiers, _root.transform, at.position, turn, _layer);
            }
            else
            {
                copy = Ghost.Make(prefab, _root.transform, at.position, turn, _layer);
            }
            if (copy == null) return null;
            copy.transform.SetParent(at, true);
            Tune(copy, audible: false);
            return copy;
        }

        private static Entry EntryOf(string key) => Session.Explorer?.Catalog.FirstOrDefault(e => e.Key == key);

        /// <summary>Lets go of the example's rooms' creatures, as its rooms go; the location's own stay.</summary>
        private static void ForgetRoomCreatures()
        {
            CreaturesToMake.RemoveAll(c => c.Room != null);
            CreatureCopies.RemoveAll(pair => pair.Value != null);
        }

        /// <summary>Lets go of every creature, as the copy they stand on goes.</summary>
        private static void ForgetCreatures()
        {
            CreaturesToMake.Clear();
            CreatureCopies.Clear();
        }
    }
}
