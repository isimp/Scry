using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A trader's store as wares (<see cref="Ware"/>): what its own list holds (<c>Trader.m_items</c>),
    /// items and the upgrades the game sells as keys of the character (Wider and Deeper Pockets,
    /// <c>TradeItem.m_buyKey</c>, with an icon, name and tooltip of their own and no item), and
    /// what the store offers as it opens beyond that (<c>Trader.GetAvailableItems</c>, which mods
    /// add to on the fly, as OdinOnDemand does Skald's Girdle). The game's list lets a ware through
    /// once its world key is set and, for an upgrade, until the character has bought it, so what
    /// mods add for a key not yet set is not known before.
    /// </summary>
    internal static class TraderStore
    {
        private static readonly HarmonyLib.AccessTools.FieldRef<Player, HashSet<string>> Uniques = HarmonyLib.AccessTools.FieldRefAccess<Player, HashSet<string>>("m_uniques");

        /// <summary>How many keys of its own the character holds (<c>Player.m_uniques</c>), which buying an upgrade adds to, so a trader's page tells it bought; none without a character.</summary>
        public static int KeysHeld => Player.m_localPlayer != null ? Uniques(Player.m_localPlayer)?.Count ?? 0 : 0;

        /// <summary>A trader's wares by the world key each waits for, with each upgrade's icon.</summary>
        public static (List<(string Key, List<Ware> Wares)> Rows, Dictionary<Ware, Sprite> Icons) Rows(Trader trader)
        {
            var icons = new Dictionary<Ware, Sprite>();
            var own = new List<Ware>();
            if (trader.m_items != null) foreach (var trade in trader.m_items) Keep(own, icons, trade);
            var live = new List<Ware>();
            foreach (var trade in OnOffer(trader)) Keep(live, icons, trade);
            return (TraderWares.ByKey(own, live), icons);
        }

        /// <summary>
        /// What the store offers now, its own wares among them (<c>Trader.GetAvailableItems</c>);
        /// none without a character in a world, which the game's list reads, or where it fails.
        /// </summary>
        public static List<Trader.TradeItem> OnOffer(Trader trader)
        {
            if (trader == null || Player.m_localPlayer == null || ZoneSystem.instance == null) return new List<Trader.TradeItem>();
            List<Trader.TradeItem> offered = null;
            Guard.Run(Feature.WhatTradersSellAndWhen, "what a store offers", () => offered = trader.GetAvailableItems());
            return offered ?? new List<Trader.TradeItem>();
        }

        private static void Keep(List<Ware> into, Dictionary<Ware, Sprite> icons, Trader.TradeItem trade)
        {
            var ware = Of(trade);
            if (ware == null) return;
            into.Add(ware);
            if (ware.Prefab == null && trade.m_icon != null) icons[ware] = trade.m_icon;
        }

        /// <summary>A ware as its trade tells it: an item, or an upgrade by its own words; none for a trade of neither.</summary>
        private static Ware Of(Trader.TradeItem trade)
        {
            if (trade == null) return null;
            var key = trade.m_requiredGlobalKey ?? "";
            if (trade.m_prefab != null)
            {
                return new Ware { Prefab = trade.m_prefab.gameObject.name, Name = trade.m_prefab.gameObject.name, Stack = trade.m_stack, Price = trade.m_price, Key = key };
            }
            if (string.IsNullOrEmpty(trade.m_buyKey)) return null;
            var name = CatalogBuilder.Localize(trade.m_name);
            var tip = CatalogBuilder.Localize(trade.m_tooltip);
            return new Ware
            {
                Name = name.Length > 0 ? name : Naming.Readable(trade.m_buyKey),
                Tip = tip.Length > 0 ? tip : null,
                Stack = 1,
                Price = trade.m_price,
                Key = key,
                BuyKey = trade.m_buyKey,
                Bought = Player.m_localPlayer != null && Player.m_localPlayer.HaveUniqueKey(trade.m_buyKey),
            };
        }
    }
}
