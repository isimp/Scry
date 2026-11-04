using System.Collections.Generic;
using HarmonyLib;

namespace Scry
{
    /// <summary>
    /// What the character takes out of a container, for the compendium, where the game takes it
    /// rather than where it ends up: an item moved out of it into the character's inventory
    /// (<c>Inventory.MoveAll</c> for taking all, <c>Inventory.MoveItemToThis</c> for one or part of
    /// a stack), taken where it left the container; one picked up off the ground is taken where
    /// <c>Humanoid.Pickup</c> answers true (<see cref="TogetherWatch.PickedUp"/>).
    /// A mod may store what the character takes in an inventory of its own, as a backpack's, so
    /// the character's inventory never holds it, and the look at what it carries never sees it;
    /// the game's own known items miss it the same way (<c>Player.OnInventoryChanged</c> reads the
    /// character's inventory alone). The hooks only watch.
    /// </summary>
    internal static class TakeWatch
    {
        /// <summary>Whether a move goes into the character's own inventory, the only moves looked at, so others cost nothing.</summary>
        public static bool IntoTheCharacter(Inventory into) => into != null && Player.m_localPlayer != null && into == Player.m_localPlayer.GetInventory();

        /// <summary>What a container held before a move, each item with its stack, to tell after it what left.</summary>
        public static List<(ItemDrop.ItemData Item, int Stack)> Before(IEnumerable<ItemDrop.ItemData> items)
        {
            var held = new List<(ItemDrop.ItemData, int)>();
            if (items != null) foreach (var item in items) if (item != null) held.Add((item, item.m_stack));
            return held;
        }

        /// <summary>The items that left a container for the character's inventory, whole or part of a stack, of those it held before the move.</summary>
        public static void Moved(Inventory into, Inventory from, List<(ItemDrop.ItemData Item, int Stack)> before)
        {
            if (into == null || from == null || before == null || before.Count == 0 || Player.m_localPlayer == null || into != Player.m_localPlayer.GetInventory() || from == into) return;
            Guard.Run(Feature.CompendiumUse, "noting what was taken from a container", () =>
            {
                foreach (var (item, stack) in before)
                {
                    if (item.m_dropPrefab != null && (!from.ContainsItem(item) || item.m_stack < stack)) CompendiumKeeper.Took(item.m_dropPrefab.name);
                }
            });
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveAll))]
    internal static class TakeAllWatched
    {
        private static void Prefix(Inventory __instance, Inventory fromInventory, out List<(ItemDrop.ItemData, int)> __state) => __state = TakeWatch.IntoTheCharacter(__instance) && fromInventory != null ? TakeWatch.Before(fromInventory.GetAllItems()) : null;

        private static void Postfix(Inventory __instance, Inventory fromInventory, List<(ItemDrop.ItemData, int)> __state) => TakeWatch.Moved(__instance, fromInventory, __state);
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemDrop.ItemData))]
    internal static class TakeOneWatched
    {
        private static void Prefix(Inventory __instance, ItemDrop.ItemData item, out List<(ItemDrop.ItemData, int)> __state) => __state = TakeWatch.IntoTheCharacter(__instance) ? TakeWatch.Before(new[] { item }) : null;

        private static void Postfix(Inventory __instance, Inventory fromInventory, List<(ItemDrop.ItemData, int)> __state) => TakeWatch.Moved(__instance, fromInventory, __state);
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveItemToThis), typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int))]
    internal static class TakePartWatched
    {
        private static void Prefix(Inventory __instance, ItemDrop.ItemData item, out List<(ItemDrop.ItemData, int)> __state) => __state = TakeWatch.IntoTheCharacter(__instance) ? TakeWatch.Before(new[] { item }) : null;

        private static void Postfix(Inventory __instance, Inventory fromInventory, List<(ItemDrop.ItemData, int)> __state) => TakeWatch.Moved(__instance, fromInventory, __state);
    }
}
