using System.Collections.Generic;

namespace Scry
{
    /// <summary>One item of a drop table: what, how many at a time, and its weight among the others.</summary>
    internal struct DropInfo
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
    internal sealed class DropTableInfo
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
    internal static class DropWords
    {
        public static bool IsEmpty(DropTableInfo table) => table?.Drops == null || table.Drops.Count == 0;

        /// <summary>The title of a table's row: "Drops", "Drops 2–3 times, 50% of the time", "Drops each of these once".</summary>
        public static string Title(DropTableInfo table)
        {
            string title;
            if (table.OneOfEach)
            {
                title = table.Min >= table.Drops.Count ? "Drops each of these once" : $"Drops {Numbers.CountRange(table.Min, table.Max)} of these, each at most once";
            }
            else
            {
                title = table.Max <= 1 ? "Drops" : $"Drops {Numbers.CountRange(table.Min, table.Max)} times";
            }
            if (table.Chance < 1f) title += $", {Share(table.Chance)} of the time";
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
                title = table.Min >= table.Drops.Count ? "Holds each of these once" : $"Holds {Numbers.CountRange(table.Min, table.Max)} of these, each at most once";
            }
            else if (table.Max > 1)
            {
                title = $"Holds {Numbers.CountRange(table.Min, table.Max)} of these";
            }
            else
            {
                title = table.Drops.Count > 1 ? "Holds one of these" : "Holds";
            }
            if (table.Chance < 1f) title += $", {Share(table.Chance)} of the time";
            return title;
        }

        /// <summary>How many of an item a roll gives, and its share of a roll where there is a choice.</summary>
        public static string Amount(DropTableInfo table, DropInfo drop)
        {
            var amount = Numbers.CountRange(drop.StackMin, drop.StackMax);
            if (table.OneOfEach || table.Drops.Count < 2) return amount;

            var total = 0f;
            foreach (var each in table.Drops) total += each.Weight;
            return total > 0f ? $"{amount} ({Share(drop.Weight / total)})" : amount;
        }

        /// <summary>
        /// One item's odds in a table, as the item's details tell where it comes from: how many a
        /// roll gives, its share of a roll where there is a choice, how many rolls there are, and
        /// how often the table gives anything. From a table that gives each item at most once, how
        /// many of its items are picked instead.
        /// </summary>
        public static string ForItem(DropTableInfo table, DropInfo drop)
        {
            var amount = Numbers.CountRange(drop.StackMin, drop.StackMax);
            var odds = new List<string>();
            if (table.OneOfEach)
            {
                if (table.Min < table.Drops.Count) odds.Add($"at most once, {Numbers.CountRange(table.Min, table.Max)} of {Numbers.Count(table.Drops.Count)} picked");
            }
            else
            {
                var total = 0f;
                foreach (var each in table.Drops) total += each.Weight;
                if (table.Drops.Count > 1 && total > 0f) odds.Add(Share(drop.Weight / total) + " a roll");
                if (table.Max > 1) odds.Add($"{Numbers.CountRange(table.Min, table.Max)} rolls");
            }
            if (table.Chance < 1f) odds.Add(Share(table.Chance) + " of the time");
            return odds.Count > 0 ? $"{amount} ({string.Join(", ", odds.ToArray())})" : amount;
        }

        /// <summary>
        /// How many of an item a creature drops. <c>CharacterDrop</c> rolls its amount with
        /// <c>Random.Range(int, int)</c>, which never returns the top value, so a drop set to 1 and
        /// 3 gives one or two; one set to drop per player gives one for each player online.
        /// </summary>
        public static string CreatureAmount(int min, int max, bool onePerPlayer) =>
            onePerPlayer ? "1 per player" : Numbers.CountRange(min, max - 1);

        /// <summary>
        /// A creature's drop as its chip and its line tell it: how many, and how often where it is
        /// not sure, in the words every share is told in (<see cref="Share"/>).
        /// </summary>
        public static string CreatureDrop(int min, int max, bool onePerPlayer, float chance) =>
            CreatureAmount(min, max, onePerPlayer) + (chance < 1f ? $" ({Share(chance)})" : "");

        /// <summary>
        /// What stars do to a creature's drops: <c>CharacterDrop.GenerateDropList</c> multiplies
        /// the chance and the amount of each drop with <c>m_levelMultiplier</c> by 2 to the power
        /// of its stars; those without it, named, stay the same. Null without stars.
        /// </summary>
        public static string StarDrops(int maxStars, IList<string> unchanged)
        {
            if (maxStars <= 0) return null;
            var steps = new List<string>();
            for (var stars = 1; stars <= maxStars; stars++) steps.Add($"×{Numbers.Count(1 << stars)} at {Numbers.Count(stars)} {(stars == 1 ? "star" : "stars")}");
            var words = "amount and chance " + string.Join(", ", steps);
            if (unchanged != null && unchanged.Count > 0) words += $"; {Naming.Joined(new List<string>(unchanged))} {(unchanged.Count == 1 ? "stays" : "stay")} the same";
            return words;
        }

        /// <summary>
        /// A share as a percentage: whole where that says enough, with a decimal near none or all,
        /// so a rare drop never reads as 0% nor a likely one as 100%.
        /// </summary>
        /// <summary>A drop row's title led by when it drops ("When felled, drops 3 times"); the title as it is for no lead.</summary>
        public static string Led(string lead, string title) =>
            string.IsNullOrEmpty(lead) || string.IsNullOrEmpty(title) ? title : lead + char.ToLowerInvariant(title[0]) + title.Substring(1);

        public static string Share(float share)
        {
            var percent = share * 100f;
            if (percent > 0f && percent < 0.05f) return "under 0.1%";
            if (percent < 100f && percent > 99.95f) return "over 99.9%";
            return Numbers.Percent(share, percent < 10f || percent > 90f ? 1 : 0);
        }
    }
}
