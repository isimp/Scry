using System.Collections.Generic;

namespace Scry
{
    /// <summary>What part of the game a mod hooks into, as the details tell it.</summary>
    public enum HookedRule
    {
        /// <summary>What a creature drops when it dies.</summary>
        Drops,

        /// <summary>What a chest, rock, tree, plant or anything with a drop table gives.</summary>
        Loot,

        /// <summary>Where and how creatures spawn.</summary>
        Spawns,

        /// <summary>How much comfort pieces give and how it is counted.</summary>
        Comfort,

        /// <summary>A smelter's, kiln's or the like's pace, what it takes and what it holds.</summary>
        Smelting,

        /// <summary>The same for a cooking station.</summary>
        Cooking,

        /// <summary>The same for a fermenter.</summary>
        Fermenting,

        /// <summary>How fast a beehive or sap extractor makes and how much it holds.</summary>
        Producing,

        /// <summary>How long a fire's fuel lasts.</summary>
        Burning,

        /// <summary>A piece's wear, support and need of a roof.</summary>
        Wear,

        /// <summary>What a recipe or piece costs, and the station it needs.</summary>
        Crafting,

        /// <summary>An item's damage, armour, weight, durability and blocking.</summary>
        ItemStats,

        /// <summary>What eating a food gives.</summary>
        Food,

        /// <summary>How plants grow and pickables grow back.</summary>
        Growth,

        /// <summary>Which weather comes and what it does.</summary>
        Weather,

        /// <summary>When and where raids come.</summary>
        Raids,

        /// <summary>How creatures are tamed and breed.</summary>
        Taming,

        /// <summary>What traders sell.</summary>
        Trading,

        /// <summary>How many slots a container holds.</summary>
        Storage,
    }

    /// <summary>
    /// The note a detail gets when mods hook into the game's own code for it (found by
    /// <c>ModHooks</c> through Harmony): which mods, and what may differ from what is told.
    /// </summary>
    public static class ModHookWords
    {
        /// <summary>The note's label for a rule.</summary>
        public static string Label(HookedRule rule)
        {
            switch (rule)
            {
                case HookedRule.Drops: return "Mods and its drops";
                case HookedRule.Loot: return "Mods and what it gives";
                case HookedRule.Spawns: return "Mods and its spawning";
                case HookedRule.Comfort: return "Mods and its comfort";
                case HookedRule.Burning: return "Mods and its fuel";
                case HookedRule.Wear: return "Mods and its wear";
                case HookedRule.Crafting: return "Mods and its cost";
                case HookedRule.ItemStats: return "Mods and its stats";
                case HookedRule.Food: return "Mods and eating it";
                case HookedRule.Growth: return "Mods and its growing";
                case HookedRule.Weather: return "Mods and the weather";
                case HookedRule.Raids: return "Mods and raids";
                case HookedRule.Taming: return "Mods and taming";
                case HookedRule.Trading: return "Mods and its wares";
                case HookedRule.Storage: return "Mods and its size";
                default: return "Mods and how it works";
            }
        }

        /// <summary>The note for the mods hooking into a rule, or null for none.</summary>
        public static string Note(HookedRule rule, IReadOnlyList<string> mods)
        {
            if (mods == null) return null;
            var names = new List<string>();
            foreach (var mod in mods) if (!string.IsNullOrEmpty(mod) && !names.Contains(mod)) names.Add(mod);
            if (names.Count == 0) return null;

            var who = names.Count == 1 ? names[0] : string.Join(", ", names.GetRange(0, names.Count - 1).ToArray()) + " and " + names[names.Count - 1];
            var hook = names.Count == 1 ? "hooks" : "hook";
            const string working = "its pace, what it takes and what it holds may differ from what is told";
            switch (rule)
            {
                case HookedRule.Drops: return $"{who} {hook} into them: it may drop more or other than listed";
                case HookedRule.Loot: return $"{who} {hook} into it: it may give more or other than listed";
                case HookedRule.Spawns: return $"{who} {hook} into it: it may spawn elsewhere or otherwise than told";
                case HookedRule.Comfort: return $"{who} {hook} into comfort: it may count otherwise than told";
                case HookedRule.Smelting: return $"{who} {hook} into smelting: {working}";
                case HookedRule.Cooking: return $"{who} {hook} into cooking: {working}";
                case HookedRule.Fermenting: return $"{who} {hook} into fermenting: {working}";
                case HookedRule.Producing: return $"{who} {hook} into it: how fast it makes and how much it holds may differ from what is told";
                case HookedRule.Burning: return $"{who} {hook} into fires: how long its fuel lasts may differ from what is told";
                case HookedRule.Wear: return $"{who} {hook} into wear and support: it may stand or wear otherwise than told";
                case HookedRule.Crafting: return $"{who} {hook} into crafting and building costs: it may cost otherwise than told";
                case HookedRule.ItemStats: return $"{who} {hook} into item stats: its figures may differ from those told";
                case HookedRule.Food: return $"{who} {hook} into eating: what it gives may differ from what is told";
                case HookedRule.Growth: return $"{who} {hook} into growing: it may grow or grow back otherwise than told";
                case HookedRule.Weather: return $"{who} {hook} into the weather: it may come or act otherwise than told";
                case HookedRule.Raids: return $"{who} {hook} into raids: they may come otherwise than told";
                case HookedRule.Taming: return $"{who} {hook} into taming and breeding: it may tame or breed otherwise than told";
                case HookedRule.Storage: return $"{who} {hook} into containers: it may hold more or less than told";
                default: return $"{who} {hook} into trading: what is sold may differ from what is told";
            }
        }
    }
}
