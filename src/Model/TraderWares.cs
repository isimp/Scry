using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// One ware of a trader's store (<c>Trader.TradeItem</c>): an item and how many for how much,
    /// or an upgrade the game sells as a key of the character (<c>m_buyKey</c>: Wider and Deeper
    /// Pockets), named by its own words with no item; the world key it waits for, if any.
    /// </summary>
    internal sealed class Ware
    {
        /// <summary>The item's prefab; none for an upgrade.</summary>
        public string Prefab;

        /// <summary>The name it is sold by: the item's, or an upgrade's own.</summary>
        public string Name = "";

        /// <summary>What an upgrade's tooltip says it does; none for an item.</summary>
        public string Tip;

        public int Stack = 1;
        public int Price;

        /// <summary>The world key it waits for (<c>m_requiredGlobalKey</c>), "" for none.</summary>
        public string Key = "";

        /// <summary>The character's key buying an upgrade gives it, which the store then no longer offers; none for an item.</summary>
        public string BuyKey;

        /// <summary>Whether the character has bought the upgrade.</summary>
        public bool Bought;

        /// <summary>Whether a mod adds it to the store as it opens, rather than the trader's own list holding it.</summary>
        public bool FromMods;
    }

    /// <summary>What a trader sells, as its page tells it: a row for each world key its wares wait for.</summary>
    internal static class TraderWares
    {
        /// <summary>
        /// The trader's own wares (<c>Trader.m_items</c>), then what the store offers as it opens
        /// that its own list does not hold (<c>Trader.GetAvailableItems</c>, which mods add to on
        /// the fly), each once; a row for each world key, in the order its first ware came.
        /// </summary>
        public static List<(string Key, List<Ware> Wares)> ByKey(IEnumerable<Ware> own, IEnumerable<Ware> live)
        {
            var rows = new List<(string Key, List<Ware> Wares)>();
            var held = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ware in own ?? Array.Empty<Ware>()) Add(rows, held, ware, false);
            foreach (var ware in live ?? Array.Empty<Ware>()) Add(rows, held, ware, true);
            return rows;
        }

        private static void Add(List<(string Key, List<Ware> Wares)> rows, HashSet<string> held, Ware ware, bool live)
        {
            if (ware == null || !held.Add(IdOf(ware))) return;
            if (live) ware.FromMods = true;
            var key = ware.Key ?? "";
            var at = rows.FindIndex(r => r.Key == key);
            if (at < 0)
            {
                rows.Add((key, new List<Ware>()));
                at = rows.Count - 1;
            }
            rows[at].Wares.Add(ware);
        }

        /// <summary>What tells a ware from the others: its item, or an upgrade's key.</summary>
        private static string IdOf(Ware ware) => ware.Prefab != null ? "item:" + ware.Prefab : "key:" + (ware.BuyKey ?? ware.Name);

        /// <summary>A ware's chip amount: how many for how much, and an upgrade the character has saying so.</summary>
        public static string Amount(Ware ware) => SourceWords.Price(ware.Stack, ware.Price) + (ware.Bought ? " · bought" : "");
    }

    /// <summary>How a row of a page waiting for a world key shows, with the compendium on.</summary>
    internal enum UnlockShown
    {
        /// <summary>Whole: nothing waits, its key is set, or the page names everything.</summary>
        Whole,

        /// <summary>Its title and how many it holds, none named.</summary>
        Counted,
    }

    /// <summary>The rule for rows of wares waiting for a boss: named once it has fallen in the world, before that counted, by the compendium's rules.</summary>
    internal static class UnlockRows
    {
        /// <summary>
        /// A row waiting for a world key: whole where it waits for none, its key is set, or the
        /// page names more than what was met; else its title and how many, under either rules, as
        /// a store's wares are what the game shows.
        /// </summary>
        public static UnlockShown Of(string key, bool namesOnlyMet, Func<string, bool> has) =>
            string.IsNullOrEmpty(key) || !namesOnlyMet || (has != null && has(key)) ? UnlockShown.Whole : UnlockShown.Counted;

        /// <summary>
        /// How many wares a row counted under a title tells, rows counted under one title (as names
        /// not met are kept back) being one wherever they stand among the section's rows: the
        /// first tells all of theirs; none for each after it, nor for a row counted under none.
        /// </summary>
        public static int? Told<T>(IReadOnlyList<T> rows, int at, Func<T, string> countedAs, Func<T, int> wares)
        {
            var title = countedAs(rows[at]);
            if (title == null) return null;
            for (var before = 0; before < at; before++)
            {
                if (countedAs(rows[before]) == title) return null;
            }
            var told = 0;
            for (var row = at; row < rows.Count; row++)
            {
                if (countedAs(rows[row]) == title) told += wares(rows[row]);
            }
            return told;
        }
    }
}
