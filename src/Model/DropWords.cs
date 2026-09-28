using System.Collections.Generic;

namespace Scry
{
    /// <summary>One item of a drop table: what, how many at a time, and its weight among the others.</summary>
    public struct DropInfo
    {
        public string Item;
        public int StackMin;
        public int StackMax;
        public float Weight;

        public DropInfo(string item, int stackMin, int stackMax, float weight)
        {
            Item = item;
            StackMin = stackMin;
            StackMax = stackMax;
            Weight = weight;
        }
    }

    /// <summary>A drop table as the game rolls it (<c>DropTable.GetDropList</c>).</summary>
    public sealed class DropTableInfo
    {
        /// <summary>How many times it is rolled, from and to.</summary>
        public int Min = 1;
        public int Max = 1;

        /// <summary>How often it drops anything at all.</summary>
        public float Chance = 1f;

        /// <summary>Whether an item once dropped is left out of the next roll.</summary>
        public bool OneOfEach;

        public List<DropInfo> Drops = new List<DropInfo>();
    }

    /// <summary>
    /// A drop table in words, the same for every tree, rock, bush and creature: how many times it
    /// is rolled and how often, and for each item how many it gives and its share of a roll. A
    /// chest's is told as what it holds.
    /// </summary>
    public static class DropWords
    {
        public static bool IsEmpty(DropTableInfo table) => table?.Drops == null || table.Drops.Count == 0;

        /// <summary>The title of a table's row: "Drops", "Drops 2–3 times, 50% of the time", "Drops each of these once".</summary>
        public static string Title(DropTableInfo table)
        {
            string title;
            if (table.OneOfEach)
            {
                title = table.Min >= table.Drops.Count ? "Drops each of these once" : $"Drops {Range(table.Min, table.Max)} of these, each at most once";
            }
            else
            {
                title = table.Max <= 1 ? "Drops" : $"Drops {Range(table.Min, table.Max)} times";
            }
            if (table.Chance < 1f) title += $", {Percent(table.Chance)}% of the time";
            return title;
        }

        /// <summary>
        /// The title of a chest's contents, the same table told as what it holds: "Holds 2–3 of
        /// these, each at most once", "Holds one of these", "Holds" for a single sure item.
        /// </summary>
        public static string HoldsTitle(DropTableInfo table)
        {
            string title;
            if (table.OneOfEach)
            {
                title = table.Min >= table.Drops.Count ? "Holds each of these once" : $"Holds {Range(table.Min, table.Max)} of these, each at most once";
            }
            else if (table.Max > 1)
            {
                title = $"Holds {Range(table.Min, table.Max)} of these";
            }
            else
            {
                title = table.Drops.Count > 1 ? "Holds one of these" : "Holds";
            }
            if (table.Chance < 1f) title += $", {Percent(table.Chance)}% of the time";
            return title;
        }

        /// <summary>How many of an item a roll gives, and its share of a roll where there is a choice.</summary>
        public static string Amount(DropTableInfo table, DropInfo drop)
        {
            var amount = Range(drop.StackMin, drop.StackMax);
            if (table.OneOfEach || table.Drops.Count < 2) return amount;

            var total = 0f;
            foreach (var each in table.Drops) total += each.Weight;
            return total > 0f ? $"{amount} ({Percent(drop.Weight / total)}%)" : amount;
        }

        /// <summary>
        /// How many of an item a creature drops. <c>CharacterDrop</c> rolls its amount with
        /// <c>Random.Range(int, int)</c>, which never returns the top value, so a drop set to 1 and
        /// 3 gives one or two; one set to drop per player gives one for each player online.
        /// </summary>
        public static string CreatureAmount(int min, int max, bool onePerPlayer) =>
            onePerPlayer ? "1 per player" : Range(min, max - 1);

        /// <summary>A range of counts, written as the rest of the panel writes them: "3", "1–4".</summary>
        public static string Range(int min, int max) => max <= min ? min.ToString() : $"{min}–{max}";

        private static int Percent(float share) => (int)System.Math.Round(share * 100f);
    }
}
