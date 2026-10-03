using System.Collections.Generic;
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
    /// whole. The stage's Creatures chip puts them away and back. Which stand and wait is kept by
    /// <see cref="StageCreatures"/>; how each copy is made, here.
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

        /// <summary>The creatures standing on the stage and waiting to be made.</summary>
        private static readonly StageCreatures Creatures = new StageCreatures();

        /// <summary>Whether the creatures stand on the stage or are put away, as the stage's chip has it.</summary>
        public static bool CreaturesShown
        {
            get => Creatures.Shown;
            set => Creatures.Shown = value;
        }

        /// <summary>Whether the place shown puts any creature there.</summary>
        public static bool HasCreatures => Creatures.Any;

        /// <summary>How many creatures stand made, and how many wait to be, for the self-test.</summary>
        public static int CreaturesMade => Creatures.Made;
        public static int CreaturesWaiting => Creatures.WaitingCount;

        /// <summary>How many creatures stand switched on, for the self-test.</summary>
        public static int CreaturesStanding => Creatures.Standing;

        /// <summary>How many creatures rolled stand below their spawn point, dropped to the ground under it, and how many fly over it, for the self-test.</summary>
        public static int CreaturesDropped => Creatures.Dropped;
        public static int CreaturesFlying => Creatures.Flying;

        private static readonly List<Renderer> Counted = new List<Renderer>();

        /// <summary>How many parts draw on the stage, its copies, creatures and ground together, for the resource monitor.</summary>
        public static int PartsOnStage
        {
            get
            {
                if (_root == null) return 0;
                _root.GetComponentsInChildren(false, Counted);
                var count = Counted.Count;
                Counted.Clear();
                return count;
            }
        }

        /// <summary>Where each creature standing on the stage draws, for the self-test.</summary>
        public static List<Bounds> CreatureBoundsNow() => Creatures.Copies.Where(c => c.activeInHierarchy).Select(c => Measure(c)).ToList();

        /// <summary>The names of the creatures made, for the self-test to tell.</summary>
        public static IEnumerable<string> CreatureNames => Creatures.Copies.Select(c => c.name);

        /// <summary>The creatures that fly and how high over their ground, by kind, for the self-test.</summary>
        public static string FlyersTold() => Creatures.FlyersTold();

        /// <summary>How many creatures stand above the cut, cut away with their floor, for the self-test.</summary>
        public static int CreaturesAboveCut => CreatureLayer != _layer ? Creatures.OnLayer(_layer) : 0;

        /// <summary>Rolls which of a copy's spawn points put their creature there, to be made over the next frames.</summary>
        private static void Populate(List<SpawnHere> points, GameObject room) => Creatures.Populate(points, room, CreatureDice.NextDouble);

        /// <summary>Each frame: makes a few of the creatures waiting, where their points are, dimmed with a room dimmed.</summary>
        public static void StepCreatures()
        {
            if (Creatures.WaitingCount == 0 || _root == null) return;
            var made = Timing.Start();
            Creatures.Step(CreatureBudgetMs, waiting =>
            {
                var copy = MakeCreature(waiting.Creature, waiting.At, waiting.Rise, waiting.Level);
                if (copy != null && waiting.Room != null && ExampleRoomDimmed(waiting.Room)) Dim.Set(copy, true);
                return copy;
            });
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

        private static Entry EntryOf(string key) => WorldCatalog.Find(key);

        /// <summary>
        /// With a floor's cut laid, a creature standing above it, on a floor above, is cut away
        /// with that floor rather than drawn again over the picture (<see cref="StageCreatures.KeepToCut"/>).
        /// </summary>
        private static void KeepCreaturesToCut()
        {
            if (CreatureLayer == _layer) return;
            var cut = Cutting && _subject != null ? Origin.y + CutAt * _scale : float.PositiveInfinity;
            Creatures.KeepToCut(cut, _layer, CreatureLayer);
        }

        /// <summary>Lets go of the example's rooms' creatures, as its rooms go; the location's own stay.</summary>
        private static void ForgetRoomCreatures() => Creatures.ForgetRooms();

        /// <summary>Lets go of every creature, as the copy they stand on goes.</summary>
        private static void ForgetCreatures()
        {
            Creatures.Forget();
            FloorProbe.ForgetNotSolid();
        }
    }
}
