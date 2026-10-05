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
    internal static class ContentOrder
    {
        /// <summary>Loot, the least likely first.</summary>
        public static List<T> RarestFirst<T>(IEnumerable<T> items, Func<T, double> chance) =>
            items.OrderBy(chance).ToList();

        /// <summary>
        /// Loot by what it is worth having: what nothing else gives first, then what a trader
        /// pays for, the most first, then the rest; each the least likely first.
        /// </summary>
        public static List<T> LootFirst<T>(IEnumerable<T> items, Func<T, double> chance, Func<T, bool> onlyHere, Func<T, int> worth) =>
            items.OrderBy(i => onlyHere(i) ? 0 : 1).ThenByDescending(worth).ThenBy(chance).ToList();

        /// <summary>Creatures, bosses first, then the most health; what is no creature (null) after them all.</summary>
        public static List<T> ToughestFirst<T>(IEnumerable<T> items, Func<T, Foe?> foe)
        {
            return items.Select(i => (Item: i, Foe: foe(i)))
                .OrderBy(x => x.Foe == null ? 2 : x.Foe.Value.Boss ? 0 : 1)
                .ThenByDescending(x => x.Foe?.Health ?? 0f)
                .Select(x => x.Item)
                .ToList();
        }

        /// <summary>
        /// Where a thing comes from, the surest first; among equally sure ones, the one whose
        /// biome players reach first (<see cref="Earliest"/>).
        /// </summary>
        public static List<T> SurestFirst<T>(IEnumerable<T> items, Func<T, double> chance, Func<T, int> biome) =>
            items.OrderByDescending(chance).ThenBy(biome).ToList();

        /// <summary>
        /// What is gathered, the highest tool tier it needs first, then the most health; what
        /// needs no tool at all (null: picked) after them all.
        /// </summary>
        public static List<T> HardestFirst<T>(IEnumerable<T> items, Func<T, (int Tier, float Health)?> toGather)
        {
            return items.Select(i => (Item: i, Hard: toGather(i)))
                .OrderBy(x => x.Hard == null ? 1 : 0)
                .ThenByDescending(x => x.Hard?.Tier ?? 0)
                .ThenByDescending(x => x.Hard?.Health ?? 0f)
                .Select(x => x.Item)
                .ToList();
        }

        /// <summary>Places, the fewest the world places first; one it never places by itself (0 or less) after them all.</summary>
        public static List<T> FewestFirst<T>(IEnumerable<T> items, Func<T, int> placed)
        {
            return items.Select(i => (Item: i, Placed: placed(i)))
                .OrderBy(x => x.Placed > 0 ? 0 : 1)
                .ThenBy(x => Math.Max(0, x.Placed))
                .Select(x => x.Item)
                .ToList();
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
