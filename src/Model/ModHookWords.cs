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
                default: return "Mods and its spawning";
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
            switch (rule)
            {
                case HookedRule.Drops: return $"{who} {hook} into them: it may drop more or other than listed";
                case HookedRule.Loot: return $"{who} {hook} into it: it may give more or other than listed";
                default: return $"{who} {hook} into it: it may spawn elsewhere or otherwise than told";
            }
        }
    }
}
