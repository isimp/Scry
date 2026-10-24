using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the character uses and what the game shows it, for the compendium: whatever it uses
    /// with success, pressing use on it or using an item on it (both end in
    /// <c>Humanoid.DoInteractAnimation</c>), is known; an item whose tooltip card shows, in the
    /// inventory, a chest or a trader's store, or whose card the crafting menu's recipe panel
    /// shows, is known; and the places a Vegvisir or a rune stone marks on the map are seen (both
    /// through <c>Game.DiscoverClosestLocation</c>).
    /// </summary>
    internal static class UseWatch
    {
        static UseWatch() => WorldCaches.Register(nameof(UseWatch), () => _recipeName = null);

        /// <summary>The recipe the crafting menu's panel showed last, by the name it shows, so a recipe is noted once as it is picked.</summary>
        private static string _recipeName;

        private static readonly FieldInfo SelectedRecipe = TypeFields.Named(typeof(InventoryGui), "m_selectedRecipe");
        private static readonly FieldInfo PairRecipe = SelectedRecipe != null ? TypeFields.Named(SelectedRecipe.FieldType, "Recipe") : null;

        /// <summary>
        /// The crafting menu's recipe panel showing an item's card (<c>InventoryGui.UpdateRecipe</c>
        /// fills it from the item's own tooltip): known, as the card hovered is, noted as the
        /// recipe shown changes.
        /// </summary>
        public static void RecipeShown(InventoryGui gui)
        {
            if (gui == null || gui.m_recipeName == null || PairRecipe == null) return;
            var name = gui.m_recipeName.text;
            if (string.Equals(name, _recipeName, System.StringComparison.Ordinal)) return;
            _recipeName = name;
            if (string.IsNullOrEmpty(name)) return;
            Guard.Run(Feature.CompendiumUse, "noting an item's card shown in the crafting menu", () =>
            {
                var recipe = TypeFields.Value(PairRecipe, TypeFields.Value(SelectedRecipe, gui)) as Recipe;
                var token = recipe.OrNull()?.m_item.OrNull()?.m_itemData?.m_shared?.m_name;
                if (!string.IsNullOrEmpty(token)) CompendiumKeeper.CardShown(token);
            });
        }

        public static void Used(Humanoid who, GameObject go)
        {
            if (who == null || who != Player.m_localPlayer || go == null) return;
            Guard.Run(Feature.CompendiumUse, "noting what the character used", () => CompendiumKeeper.Used(go));
        }

        public static void Hovered(UITooltip tooltip)
        {
            // A card has a text under its topic; a bare name is no card.
            if (tooltip == null || string.IsNullOrEmpty(tooltip.m_topic) || string.IsNullOrEmpty(tooltip.m_text)) return;
            Guard.Run(Feature.CompendiumUse, "noting an item's card shown", () => CompendiumKeeper.CardShown(tooltip.m_topic));
        }

        public static void Read(Vegvisir vegvisir, Humanoid who, bool done)
        {
            if (!done || vegvisir == null || who == null || who != Player.m_localPlayer) return;
            Guard.Run(Feature.CompendiumUse, "noting the places a Vegvisir marks", () =>
            {
                foreach (var place in vegvisir.m_locations) if (!string.IsNullOrEmpty(place?.m_locationName)) CompendiumKeeper.Marked(vegvisir.gameObject, place.m_locationName);
            });
        }

        /// <summary>
        /// A rune stone read (<see cref="CompendiumKeeper.ReadRunes"/>). The game answers false to
        /// every read (<c>RuneStone.Interact</c>), so nothing waits on its answer; a hold reads nothing.
        /// </summary>
        public static void Read(RuneStone stone, Humanoid who, bool hold)
        {
            if (hold || stone == null || who == null || who != Player.m_localPlayer) return;
            Guard.Run(Feature.CompendiumUse, "noting a rune stone read", () => CompendiumKeeper.ReadRunes(stone));
        }
    }

    [HarmonyPatch(typeof(Humanoid), "DoInteractAnimation")]
    internal static class UsingWatched
    {
        private static void Postfix(Humanoid __instance, GameObject obj) => UseWatch.Used(__instance, obj);
    }

    [HarmonyPatch(typeof(UITooltip), nameof(UITooltip.OnHoverStart))]
    internal static class CardWatched
    {
        private static void Postfix(UITooltip __instance) => UseWatch.Hovered(__instance);
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
    internal static class RecipeCardWatched
    {
        private static void Postfix(InventoryGui __instance) => UseWatch.RecipeShown(__instance);
    }

    [HarmonyPatch(typeof(RuneStone), nameof(RuneStone.Interact))]
    internal static class RuneStoneWatched
    {
        private static void Postfix(RuneStone __instance, Humanoid character, bool hold) => UseWatch.Read(__instance, character, hold);
    }

    [HarmonyPatch(typeof(Vegvisir), nameof(Vegvisir.Interact))]
    internal static class VegvisirWatched
    {
        private static void Postfix(Vegvisir __instance, Humanoid character, bool __result) => UseWatch.Read(__instance, character, __result);
    }
}
