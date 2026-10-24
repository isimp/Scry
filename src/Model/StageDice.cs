using System;

namespace Scry
{
    /// <summary>
    /// The chance in a place's example: which of its chance parts stand and which object a spot
    /// picks (<c>PlaceCopy</c>), and where a dungeon example's site lies (<c>ExampleLayouts</c>).
    /// In play each roll draws from one source, as the game rolls afresh for every zone; while the
    /// self-test runs the dice are fixed (<see cref="Fix"/>), each place rolling by a seed from its
    /// own name, so one place looks the same in every run's pictures whatever came before it.
    /// </summary>
    internal static class StageDice
    {
        static StageDice() => WorldCaches.Register(nameof(StageDice), () => Fixed = false);

        private static readonly Random Chance = new Random();

        /// <summary>Whether the dice are fixed, each place rolling the same by its name (the self-test).</summary>
        public static bool Fixed { get; private set; }

        /// <summary>Fixes the dice, or leaves them to chance again.</summary>
        [ForTest]
        public static void Fix(bool fixedNow) => Fixed = fixedNow;

        /// <summary>The rolls a place's copy makes, by the place's prefab name: by chance, or fixed, the same for that name every time.</summary>
        public static Random RollsFor(string place) => Fixed ? new Random(StableHash(place)) : Chance;

        /// <summary>The seed a dungeon example's layout is laid out with, by the place's name: by chance, or fixed, the same for that name every time.</summary>
        public static int SeedFor(string place) => Fixed ? StableHash(place) : Chance.Next();

        /// <summary>A hash of a name that is the same in every process and run, unlike the runtime's own, which may be salted per process; never negative.</summary>
        public static int StableHash(string name)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var c in name ?? "") hash = (hash ^ c) * 16777619u;
                return (int)(hash & 0x7FFFFFFF);
            }
        }
    }
}
