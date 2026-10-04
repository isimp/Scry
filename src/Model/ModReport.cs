using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What the mod report knows of one entry a mod added.</summary>
    internal sealed class ModEntry
    {
        public string Key = "", Name = "", Shown = "", Mod = "";
        public Kind Kind;

        /// <summary>A crafting station, and how many things are made at it, built near it and upgrade it.</summary>
        public bool Station;
        public int MadeHere, BuiltNear, Upgrades;

        /// <summary>A build tool's pieces; none for anything else.</summary>
        public int Builds;

        /// <summary>A piece some build tool builds.</summary>
        public bool InBuildMenu;

        /// <summary>An item Scry sees a source for: made, dropped, picked, sold, given or found somewhere.</summary>
        public bool HasSource;

        /// <summary>An item a creature carries, such as its own attack: placed with the creature, so never a gap.</summary>
        public bool Carried;

        /// <summary>A creature Scry sees spawn somewhere.</summary>
        public bool Spawns;
    }

    /// <summary>One mod in the report: what it adds, its stations and tools, what it hooks into, and what Scry could not place.</summary>
    internal sealed class ModSummary
    {
        public string Mod = "";
        public readonly Dictionary<Kind, int> Counts = new Dictionary<Kind, int>();
        public readonly List<ModEntry> Stations = new List<ModEntry>();
        public readonly List<ModEntry> IdleStations = new List<ModEntry>();
        public readonly List<ModEntry> Tools = new List<ModEntry>();
        public readonly List<ModEntry> Sourceless = new List<ModEntry>();
        public readonly List<ModEntry> Unspawned = new List<ModEntry>();
        public readonly List<ModEntry> Unbuilt = new List<ModEntry>();
        public readonly List<HookedRule> Hooks = new List<HookedRule>();
    }

    /// <summary>
    /// The mod report: for each mod, what it adds, what Scry links for its stations and tools,
    /// which of the game's rules it hooks into (<see cref="HookedRule"/>), and what Scry could
    /// not place. A mod that only hooks in is in it too; what no mod added is not, nor a mod
    /// adding nothing but having its page.
    /// </summary>
    internal static class ModReport
    {
        public static List<ModSummary> Of(IEnumerable<ModEntry> entries, IReadOnlyDictionary<string, IReadOnlyList<HookedRule>> hooks)
        {
            var mods = new Dictionary<string, ModSummary>(StringComparer.Ordinal);
            ModSummary Of(string mod)
            {
                if (!mods.TryGetValue(mod, out var summary)) mods[mod] = summary = new ModSummary { Mod = mod };
                return summary;
            }

            foreach (var entry in entries)
            {
                // A mod's own page is Scry's, not something the mod adds.
                if (entry == null || string.IsNullOrEmpty(entry.Mod) || entry.Kind == Kind.Mod) continue;
                var mod = Of(entry.Mod);
                mod.Counts.TryGetValue(entry.Kind, out var count);
                mod.Counts[entry.Kind] = count + 1;

                if (entry.Station)
                {
                    mod.Stations.Add(entry);
                    if (entry.MadeHere + entry.BuiltNear + entry.Upgrades == 0) mod.IdleStations.Add(entry);
                }
                if (entry.Builds > 0) mod.Tools.Add(entry);
                if (entry.Kind == Kind.Item && !entry.HasSource && !entry.Carried) mod.Sourceless.Add(entry);
                if (entry.Kind == Kind.Creature && !entry.Spawns) mod.Unspawned.Add(entry);
                if (entry.Kind == Kind.Piece && !entry.InBuildMenu) mod.Unbuilt.Add(entry);
            }

            if (hooks != null)
            {
                foreach (var pair in hooks)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value == null || pair.Value.Count == 0) continue;
                    var mod = Of(pair.Key);
                    foreach (var rule in pair.Value) if (!mod.Hooks.Contains(rule)) mod.Hooks.Add(rule);
                }
            }

            return mods.Values.OrderBy(m => m.Mod, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /// <summary>How the mod report, and a mod's page, word what they tell.</summary>
    internal static class ModReportWords
    {
        /// <summary>What a mod's page says it adds when Scry sees nothing it adds or hooks into.</summary>
        public const string NothingAdded = "nothing Scry can see, and it hooks into none of what Scry tells";

        /// <summary>Which of the rules Scry tells a mod hooks into, and that what Scry tells of them may differ.</summary>
        public static string HooksInto(IReadOnlyList<HookedRule> rules) => Hooks(rules) + ", so what Scry tells of these may differ from what happens";

        public static string StationLabel(string station) => "Station: " + station;

        public static string ToolLabel(string tool) => "Tool: " + tool;

        /// <summary>The title of a row of what a mod adds of a kind, those only clues match to it apart.</summary>
        public static string Adds(string kind, int count, bool byClues) => Naming.Counted(byClues ? $"Adds {kind}, matched by clues" : $"Adds {kind}", count);

        /// <summary>How many of each kind a mod adds, the most first and the rest in the kinds' order.</summary>
        public static string Counts(ModSummary mod)
        {
            var parts = mod.Counts.Where(c => c.Value > 0).OrderByDescending(c => c.Value).ThenBy(c => (int)c.Key)
                .Select(c => $"{Numbers.Count(c.Value)} {Noun(c.Key, c.Value)}").ToList();
            return parts.Count == 0 ? "adds nothing of its own" : And(parts);
        }

        /// <summary>What is made at a station, built near it and upgrades it; that nothing is, when so.</summary>
        public static string Station(ModEntry station)
        {
            var parts = new List<string>();
            if (station.MadeHere > 0) parts.Add($"{Numbers.Count(station.MadeHere)} made here");
            if (station.BuiltNear > 0) parts.Add($"{Numbers.Count(station.BuiltNear)} built near it");
            if (station.Upgrades > 0) parts.Add($"{Numbers.Count(station.Upgrades)} upgrading it");
            return parts.Count == 0 ? "nothing is made or built at it" : And(parts);
        }

        private static string Hook(HookedRule rule)
        {
            switch (rule)
            {
                case HookedRule.Drops: return "what creatures drop";
                case HookedRule.Loot: return "what drop tables, chests and plants give";
                case HookedRule.Spawns: return "where creatures spawn";
                case HookedRule.Comfort: return "comfort";
                case HookedRule.Smelting: return "smelting";
                case HookedRule.Cooking: return "cooking";
                case HookedRule.Fermenting: return "fermenting";
                case HookedRule.Producing: return "beehives and sap";
                case HookedRule.Burning: return "fires' fuel";
                case HookedRule.Wear: return "wear and support";
                case HookedRule.Crafting: return "crafting and building costs";
                case HookedRule.ItemStats: return "item stats";
                case HookedRule.Food: return "eating";
                case HookedRule.Growth: return "growing";
                case HookedRule.Weather: return "the weather";
                case HookedRule.Raids: return "raids";
                case HookedRule.Taming: return "taming and breeding";
                case HookedRule.Storage: return "container sizes";
                default: return "trading";
            }
        }

        public static string Tool(ModEntry tool) => $"builds {Numbers.Count(tool.Builds)} {(tool.Builds == 1 ? "piece" : "pieces")}";

        /// <summary>The rules a mod hooks into, as the report names them.</summary>
        public static string Hooks(IReadOnlyList<HookedRule> rules)
        {
            var parts = new List<string>();
            foreach (var rule in rules)
            {
                parts.Add(Hook(rule));
            }
            return And(parts);
        }

        private static string Noun(Kind kind, int count)
        {
            string one;
            switch (kind)
            {
                case Kind.StatusEffect: one = "status effect"; break;
                case Kind.Other: one = "other prefab"; break;
                default: one = kind.ToString().ToLowerInvariant(); break;
            }
            return count == 1 ? one : one + "s";
        }

        private static string And(List<string> parts) =>
            Naming.Joined(parts);
    }
}
