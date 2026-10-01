using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A mod's page: what its package says of it (what it is, who made it, its website), its
    /// version, id and folder, the mods it needs and works with and those needing or working
    /// with it, what it adds kind by kind, its crafting stations and build tools with what Scry
    /// links for each, which of the game's rules it hooks into, and what Scry could not place
    /// (<see cref="ModReport"/>), each thing a chip going to it.
    /// </summary>
    internal sealed partial class Facts
    {
        /// <summary>A link that opens a website in the browser, ahead of its address.</summary>
        public const string OpenWebsite = "web:";

        private void Mod(ModSource mod)
        {
            if (!string.IsNullOrEmpty(mod.Description)) Description = mod.Description;
            Add("By", mod.Author);
            if (ModWords.IsWebsite(mod.Website)) Add("Website", mod.Website, OpenWebsite + mod.Website);
            Add("Version", mod.Version);
            Add("Id", mod.Guid);
            Add("Folder", mod.Folder);

            var explorer = Session.Explorer;
            if (explorer == null) return;
            var relations = mod.Relations ?? new ModRelations();
            // Those here go to their pages; the rest, which are not, are named.
            ModRow("Will not run with", relations.WillNotRunWith, explorer);
            var away = relations.WillNotRunWith.Where(name => !explorer.Catalog.Any(e => e.Kind == Kind.Mod && e.Name == name)).ToArray();
            if (away.Length > 0) Add("Will not run with", string.Join(", ", away));
            ModRow("Needs", relations.Needs, explorer);
            ModRow("Needed by", relations.NeededBy, explorer);
            ModRow("Works with, when there", relations.WorksWith, explorer);
            ModRow("Works with it, when there", relations.WorkedWithBy, explorer);

            var summary = ScryPanel.Report(explorer).FirstOrDefault(m => m.Mod == mod.Name);
            if (summary == null)
            {
                Add("Adds", "nothing Scry can see, and it hooks into none of what Scry tells");
                return;
            }
            Add("Adds", ModReportWords.Counts(summary));
            if (summary.Hooks.Count > 0) Add("Hooks into", ModReportWords.Hooks(summary.Hooks) + ", so what Scry tells of these may differ from what happens");
            foreach (var station in summary.Stations) Add("Station: " + station.Shown, ModReportWords.Station(station), station.Key);
            foreach (var tool in summary.Tools) Add("Tool: " + tool.Shown, ModReportWords.Tool(tool), tool.Key);

            // What it adds, a row for each kind, in the tabs' order; what only clues match to it
            // in a row of its own, as Scry's best guess.
            foreach (var kind in explorer.Catalog.Where(e => e.ModName == mod.Name && e.Kind != Kind.Mod).GroupBy(e => e.Kind).OrderBy(g => (int)g.Key))
            {
                var label = Kinds.Label(kind.Key).ToLowerInvariant();
                var sure = kind.Where(e => UnsureWords.IsSureClue(e.ModClue)).ToList();
                var guessed = kind.Where(e => !UnsureWords.IsSureClue(e.ModClue)).ToList();
                if (sure.Count > 0) Rows.Add(ChipRow($"Adds {label} ({sure.Count})", sure, null));
                if (guessed.Count > 0) Rows.Add(ChipRow($"Adds {label}, matched by clues ({guessed.Count})", guessed, "Scry matched these to this mod by the scripts they carry or the assets they use; the mod does not say so itself"));
            }

            GapRow("Stations nothing is made or built at", summary.IdleStations, explorer);
            GapRow("Items with no source Scry can see", summary.Sourceless, explorer);
            GapRow("Creatures that spawn nowhere Scry can see", summary.Unspawned, explorer);
            GapRow("Pieces in no build menu", summary.Unbuilt, explorer);
        }

        /// <summary>A row of other mods, each going to its page.</summary>
        private void ModRow(string title, List<string> mods, Explorer explorer)
        {
            if (mods == null || mods.Count == 0) return;
            var row = new Row { Title = $"{title} ({mods.Count})" };
            foreach (var name in mods)
            {
                var entry = explorer.Catalog.FirstOrDefault(e => e.Kind == Kind.Mod && e.Name == name);
                if (entry != null) row.Items.Add(EntryChip(entry));
            }
            if (row.Items.Count > 0) Rows.Add(row);
        }

        /// <summary>A row of entries, each going to it, sorted by the name shown.</summary>
        private static Row ChipRow(string title, IEnumerable<Entry> entries, string unsure)
        {
            var row = new Row { Title = title, Unsure = unsure };
            foreach (var entry in entries.OrderBy(e => e.DisplayName.Length > 0 ? e.DisplayName : e.Name, System.StringComparer.OrdinalIgnoreCase)) row.Items.Add(EntryChip(entry));
            return row;
        }

        /// <summary>A row of what Scry could not place, each going to its entry: Scry found nothing, which may yet be there.</summary>
        private void GapRow(string title, List<ModEntry> entries, Explorer explorer)
        {
            if (entries.Count == 0) return;
            var row = new Row { Title = $"{title} ({entries.Count})", Unsure = "Scry found nothing for these; the mod's own code may still place them" };
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
            Icon = entry.Icon is Sprite own && own != null ? own : AnyIcon(entry.Source as GameObject),
            Name = entry.DisplayName.Length > 0 ? entry.DisplayName : entry.Name,
            Amount = "",
            Prefab = entry.Key,
        };
    }
}
