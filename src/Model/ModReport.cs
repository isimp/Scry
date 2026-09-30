using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What the mod report knows of one entry a mod added.</summary>
    public sealed class ModEntry
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

        /// <summary>A creature Scry sees spawn somewhere.</summary>
        public bool Spawns;
    }

    /// <summary>One mod in the report: what it adds, its stations and tools, what it hooks into, and what Scry could not place.</summary>
    public sealed class ModSummary
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
    /// not place. A mod that only hooks in is in it too; what no mod added is not.
    /// </summary>
    public static class ModReport
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
                if (entry == null || string.IsNullOrEmpty(entry.Mod)) continue;
                var mod = Of(entry.Mod);
                mod.Counts.TryGetValue(entry.Kind, out var count);
                mod.Counts[entry.Kind] = count + 1;

                if (entry.Station)
                {
                    mod.Stations.Add(entry);
                    if (entry.MadeHere + entry.BuiltNear + entry.Upgrades == 0) mod.IdleStations.Add(entry);
                }
                if (entry.Builds > 0) mod.Tools.Add(entry);
                if (entry.Kind == Kind.Item && !entry.HasSource) mod.Sourceless.Add(entry);
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

    /// <summary>How the mod report words what it tells.</summary>
    public static class ModReportWords
    {
        /// <summary>How many of each kind a mod adds, the most first and the rest in the kinds' order.</summary>
        public static string Counts(ModSummary mod)
        {
            var parts = mod.Counts.Where(c => c.Value > 0).OrderByDescending(c => c.Value).ThenBy(c => (int)c.Key)
                .Select(c => $"{c.Value} {Noun(c.Key, c.Value)}").ToList();
            return parts.Count == 0 ? "adds nothing of its own" : And(parts);
        }

        /// <summary>What is made at a station, built near it and upgrades it; that nothing is, when so.</summary>
        public static string Station(ModEntry station)
        {
            var parts = new List<string>();
            if (station.MadeHere > 0) parts.Add($"{station.MadeHere} made here");
            if (station.BuiltNear > 0) parts.Add($"{station.BuiltNear} built near it");
            if (station.Upgrades > 0) parts.Add($"{station.Upgrades} upgrading it");
            return parts.Count == 0 ? "nothing is made or built at it" : And(parts);
        }

        public static string Tool(ModEntry tool) => $"builds {tool.Builds} {(tool.Builds == 1 ? "piece" : "pieces")}";

        /// <summary>The rules a mod hooks into, as the report names them.</summary>
        public static string Hooks(IReadOnlyList<HookedRule> rules)
        {
            var parts = new List<string>();
            foreach (var rule in rules)
            {
                switch (rule)
                {
                    case HookedRule.Drops: parts.Add("what creatures drop"); break;
                    case HookedRule.Loot: parts.Add("what drop tables, chests and plants give"); break;
                    default: parts.Add("where creatures spawn"); break;
                }
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
            parts.Count <= 1 ? string.Join("", parts) : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];
    }
}
