using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The mod report of a catalog (<see cref="ModReport"/>): each entry a mod added, with what is
    /// made at it, built near it and upgrades it where it is a crafting station, what it builds
    /// where it is a build tool, and whether Scry sees where an item comes from or where a
    /// creature spawns; and every mod hooking into the game's drops, loot or spawning
    /// (<see cref="ModHooks"/>). Made when the report is opened, not every frame.
    /// </summary>
    internal static class ModReportReader
    {
        /// <summary>What an entry from a mod whose name is not known goes under.</summary>
        public const string UnknownMod = "A mod not known by name";

        public static List<ModSummary> Read(IReadOnlyList<Entry> catalog)
        {
            // What each station has made at it, built near it and upgrading it, by its prefab name.
            var made = new Dictionary<string, int>();
            var built = new Dictionary<string, int>();
            var upgrades = new Dictionary<string, int>();
            void Count(Dictionary<string, int> counts, string station)
            {
                counts.TryGetValue(station, out var n);
                counts[station] = n + 1;
            }
            foreach (var entry in catalog)
            {
                if (entry.Stations != null)
                {
                    foreach (var station in entry.Stations.Select(s => s.Name).Distinct())
                    {
                        if (station == "hand") continue;
                        Count(entry.Kind == Kind.Piece ? built : made, station);
                    }
                }
                var extension = (entry.Source as GameObject)?.GetComponent<StationExtension>();
                if (extension != null && extension.m_craftingStation != null) Count(upgrades, extension.m_craftingStation.transform.root.name);
            }

            var entries = new List<ModEntry>();
            foreach (var entry in catalog)
            {
                var mod = entry.ModName.Length > 0 ? entry.ModName : entry.Origin == Origin.Mod ? UnknownMod : "";
                if (mod.Length == 0) continue;
                var prefab = entry.Source as GameObject;
                var one = new ModEntry { Key = entry.Key, Name = entry.Name, Shown = entry.ShownName, Kind = entry.Kind, Mod = mod };
                if (prefab != null && prefab.GetComponent<CraftingStation>() != null)
                {
                    one.Station = true;
                    made.TryGetValue(entry.Name, out one.MadeHere);
                    built.TryGetValue(entry.Name, out one.BuiltNear);
                    upgrades.TryGetValue(entry.Name, out one.Upgrades);
                }
                if (entry.Kind == Kind.Item) one.Builds = Knowledge.Tools.PiecesOf(entry.Name).Sum(t => t.Pieces.Count);
                if (entry.Kind == Kind.Piece) one.InBuildMenu = Knowledge.Tools.ToolsOf(entry.Name).Count > 0;
                if (entry.Kind == Kind.Item)
                {
                    one.HasSource = (entry.Stations != null && entry.Stations.Length > 0) || Knowledge.SourceLines(entry.Name).Count > 0 || entry.FoundIn.Length > 0
                                    || DropWatch.Seen.Sources(entry.Name).Count > 0;
                    one.Carried = entry.Links.Any(l => l.Group == Relations.CarriedBy);
                }
                if (entry.Kind == Kind.Creature)
                {
                    one.Spawns = Knowledge.WhereLines(entry.Name).Count > 0 || Knowledge.IsPlacedByWorld(entry.Name) || entry.FoundIn.Length > 0
                                 || Knowledge.Summons().Any(s => s.Boss == entry.Name);
                }
                entries.Add(one);
            }

            var hooks = new Dictionary<string, IReadOnlyList<HookedRule>>();
            foreach (var mod in ModHooks.AllMods) hooks[mod] = ModHooks.Rules(mod);
            return ModReport.Of(entries, hooks);
        }
    }
}
