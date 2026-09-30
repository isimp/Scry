using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A mod's page: its version, id and folder, what it adds kind by kind, its crafting stations
    /// and build tools with what Scry links for each, which of the game's rules it hooks into,
    /// and what Scry could not place (<see cref="ModReport"/>), each thing a chip going to it.
    /// </summary>
    internal sealed partial class Facts
    {
        private void Mod(ModSource mod)
        {
            Add("Version", mod.Version);
            Add("Id", mod.Guid);
            Add("Folder", mod.Folder);

            var explorer = Session.Explorer;
            if (explorer == null) return;
            var summary = ScryPanel.Report(explorer).FirstOrDefault(m => m.Mod == mod.Name);
            if (summary == null)
            {
                Add("Adds", "nothing Scry can see, and it hooks into none of the game's drops or spawning");
                return;
            }
            Add("Adds", ModReportWords.Counts(summary));
            if (summary.Hooks.Count > 0) Add("Hooks into", ModReportWords.Hooks(summary.Hooks) + ", so what Scry tells of these may differ from what happens");
            foreach (var station in summary.Stations) Add("Station: " + station.Shown, ModReportWords.Station(station), station.Key);
            foreach (var tool in summary.Tools) Add("Tool: " + tool.Shown, ModReportWords.Tool(tool), tool.Key);

            // What it adds, a row for each kind, in the tabs' order.
            foreach (var kind in explorer.Catalog.Where(e => e.ModName == mod.Name && e.Kind != Kind.Mod).GroupBy(e => e.Kind).OrderBy(g => (int)g.Key))
            {
                var row = new Row { Title = $"Adds {Kinds.Label(kind.Key).ToLowerInvariant()} ({kind.Count()})" };
                foreach (var entry in kind.OrderBy(e => e.DisplayName.Length > 0 ? e.DisplayName : e.Name, System.StringComparer.OrdinalIgnoreCase)) row.Items.Add(EntryChip(entry));
                Rows.Add(row);
            }

            GapRow("Stations nothing is made or built at", summary.IdleStations, explorer);
            GapRow("Items with no source Scry can see", summary.Sourceless, explorer);
            GapRow("Creatures that spawn nowhere Scry can see", summary.Unspawned, explorer);
            GapRow("Pieces in no build menu", summary.Unbuilt, explorer);
        }

        /// <summary>A row of what Scry could not place, each going to its entry.</summary>
        private void GapRow(string title, List<ModEntry> entries, Explorer explorer)
        {
            if (entries.Count == 0) return;
            var row = new Row { Title = $"{title} ({entries.Count})" };
            foreach (var one in entries)
            {
                var entry = explorer.Catalog.FirstOrDefault(e => e.Key == one.Key);
                if (entry != null) row.Items.Add(EntryChip(entry));
            }
            if (row.Items.Count > 0) Rows.Add(row);
        }

        /// <summary>A chip for any entry, prefab or not, going to it by its key.</summary>
        private static Ingredient EntryChip(Entry entry) => new Ingredient
        {
            Icon = AnyIcon(entry.Source as GameObject),
            Name = entry.DisplayName.Length > 0 ? entry.DisplayName : entry.Name,
            Amount = "",
            Prefab = entry.Key,
        };
    }
}
