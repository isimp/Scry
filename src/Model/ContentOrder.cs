using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>A creature as lists of creatures are ordered by: whether it is a boss, and its health.</summary>
    internal struct Foe
    {
        public bool Boss;
        public float Health;

        public Foe(bool boss, float health)
        {
            Boss = boss;
            Health = health;
        }
    }

    /// <summary>
    /// The order content lists are told in, the same on every page: loot rarest first, creatures
    /// toughest first, where a thing comes from surest first, places rarest first, and what is
    /// gathered hardest first. Each keeps the order it was given among equals, so a list handed
    /// over by name stays by name where nothing else tells its items apart.
    /// </summary>
    /// <remarks>
    /// Every order reads its keys as numbers into one table and sorts the items' places in it
    /// (<see cref="Ordered{T}"/>). The game's runtime compiles generic code anew for each value
    /// type it is given, so LINQ's ordering over a tuple of each item and its key brought its own
    /// sorters and comparers to compile the first time each kind of list was told; this way an
    /// order of any class of item shares the code of every other.
    /// </remarks>
    internal static class ContentOrder
    {
        /// <summary>Loot, the least likely first.</summary>
        public static List<T> RarestFirst<T>(IEnumerable<T> items, Func<T, double> chance) =>
            Ordered(items, Least, (item, keys, at) => keys[at] = chance(item));

        /// <summary>
        /// Loot by what it is worth having: what nothing else gives first, then what a trader
        /// pays for, the most first, then the rest; each the least likely first.
        /// </summary>
        public static List<T> LootFirst<T>(IEnumerable<T> items, Func<T, double> chance, Func<T, bool> onlyHere, Func<T, int> worth) =>
            Ordered(items, LeastMostLeast, (item, keys, at) =>
            {
                keys[at] = onlyHere(item) ? 0 : 1;
                keys[at + 1] = worth(item);
                keys[at + 2] = chance(item);
            });

        /// <summary>
        /// What gives loot, a place's chests and pickups, by what it can give: what gives what
        /// nothing else does first, then what gives what a trader pays most for, then what gives
        /// more kinds of loot (a chest before barley), each the least likely first.
        /// </summary>
        public static List<T> GiversFirst<T>(IEnumerable<T> items, Func<T, double> chance, Func<T, bool> onlyHere, Func<T, int> worth, Func<T, int> kinds) =>
            Ordered(items, LeastMostMostLeast, (item, keys, at) =>
            {
                keys[at] = onlyHere(item) ? 0 : 1;
                keys[at + 1] = worth(item);
                keys[at + 2] = kinds(item);
                keys[at + 3] = chance(item);
            });

        /// <summary>Creatures, bosses first, then the most health; what is no creature (null) after them all.</summary>
        public static List<T> ToughestFirst<T>(IEnumerable<T> items, Func<T, Foe?> foe) =>
            Ordered(items, LeastMost, (item, keys, at) =>
            {
                var found = foe(item);
                keys[at] = found == null ? 2 : found.Value.Boss ? 0 : 1;
                keys[at + 1] = found?.Health ?? 0f;
            });

        /// <summary>
        /// Where a thing comes from, the surest first; among equally sure ones, the one whose
        /// biome players reach first (<see cref="Earliest"/>).
        /// </summary>
        public static List<T> SurestFirst<T>(IEnumerable<T> items, Func<T, double> chance, Func<T, int> biome) =>
            Ordered(items, MostLeast, (item, keys, at) =>
            {
                keys[at] = chance(item);
                keys[at + 1] = biome(item);
            });

        /// <summary>
        /// What is gathered, the highest tool tier it needs first, then the most health; what
        /// needs no tool at all (null: picked) after them all.
        /// </summary>
        public static List<T> HardestFirst<T>(IEnumerable<T> items, Func<T, (int Tier, float Health)?> toGather) =>
            Ordered(items, LeastMostMost, (item, keys, at) =>
            {
                var hard = toGather(item);
                keys[at] = hard == null ? 1 : 0;
                keys[at + 1] = hard?.Tier ?? 0;
                keys[at + 2] = hard?.Health ?? 0f;
            });

        /// <summary>Places, the fewest the world places first; one it never places by itself (0 or less) after them all.</summary>
        public static List<T> FewestFirst<T>(IEnumerable<T> items, Func<T, int> placed) =>
            Ordered(items, LeastLeast, (item, keys, at) =>
            {
                var times = placed(item);
                keys[at] = times > 0 ? 0 : 1;
                keys[at + 1] = Math.Max(0, times);
            });

        // Which way each key of an order goes, key by key: true where the most comes first.
        private static readonly bool[] Least = { false };
        private static readonly bool[] LeastLeast = { false, false };
        private static readonly bool[] LeastMost = { false, true };
        private static readonly bool[] MostLeast = { true, false };
        private static readonly bool[] LeastMostLeast = { false, true, false };
        private static readonly bool[] LeastMostMost = { false, true, true };
        private static readonly bool[] LeastMostMostLeast = { false, true, true, false };

        /// <summary>
        /// The items ordered by their keys, as LINQ's <c>OrderBy</c> and <c>ThenBy</c> order them:
        /// the first key decides, each next one only among items equal in all before it, and
        /// equals keep the order they came in. <paramref name="keys"/> writes an item's keys, one
        /// for each of <paramref name="most"/>, into the table at the place given; each is read once.
        /// </summary>
        private static List<T> Ordered<T>(IEnumerable<T> items, bool[] most, Action<T, double[], int> keys)
        {
            var given = new List<T>(items);
            var width = most.Length;
            var table = new double[given.Count * width];
            var order = new int[given.Count];
            for (var i = 0; i < given.Count; i++)
            {
                keys(given[i], table, i * width);
                order[i] = i;
            }
            Array.Sort(order, new ByKeys(table, most));
            var ordered = new List<T>(given.Count);
            foreach (var i in order) ordered.Add(given[i]);
            return ordered;
        }

        /// <summary>Compares two items by their places in a table of keys: key by key, each its own way, then the order they came in.</summary>
        private sealed class ByKeys : IComparer<int>
        {
            private readonly double[] _table;
            private readonly bool[] _most;

            public ByKeys(double[] table, bool[] most)
            {
                _table = table;
                _most = most;
            }

            public int Compare(int a, int b)
            {
                var width = _most.Length;
                for (var k = 0; k < width; k++)
                {
                    var x = _table[a * width + k];
                    var y = _table[b * width + k];
                    var told = _most[k] ? y.CompareTo(x) : x.CompareTo(y);
                    if (told != 0) return told;
                }
                return a.CompareTo(b);
            }
        }

        /// <summary>
        /// The earliest of some biomes in the order players meet them (<see cref="LocationWords.BiomeRank"/>);
        /// a biome a mod adds ranks after every one of the game's, and no biome at all last.
        /// </summary>
        public static int Earliest(IEnumerable<string> biomes)
        {
            var earliest = int.MaxValue;
            foreach (var biome in biomes ?? Enumerable.Empty<string>())
            {
                var rank = LocationWords.BiomeRank(biome);
                earliest = Math.Min(earliest, rank >= 0 ? rank : LocationWords.BiomeCount);
            }
            return earliest;
        }

        /// <summary>
        /// About how often one opening of a drop table gives an item, as <c>DropTable.GetDropList</c>
        /// rolls it: anything at all at the table's chance, then from its least to its most rolls,
        /// each as likely (fewer than none roll none), each roll picking the item at its share of
        /// the weights. One that gives each item at most once gives them all with as many rolls as
        /// it has items; with fewer, its rolls are taken as if each could pick again, which tells
        /// it a little rarer than it is. An item of no weight is never picked.
        /// </summary>
        public static double AtLeastOnce(double share, int rollsMin, int rollsMax, double tableChance, bool oneOfEach, int count)
        {
            if (share <= 0.0) return 0.0;
            var most = Math.Max(rollsMin, rollsMax);
            var sum = 0.0;
            for (var rolls = rollsMin; rolls <= most; rolls++)
            {
                var made = Math.Max(0, rolls);
                sum += oneOfEach && made >= count ? 1.0 : 1.0 - Math.Pow(1.0 - share, made);
            }
            return tableChance * sum / (most - rollsMin + 1);
        }
    }
}
