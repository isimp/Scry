using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The mod report (<see cref="ModReport"/>), shown in the list's place as the search's help
    /// is: for each mod, what it adds, what Scry links for its crafting stations and build tools,
    /// which of the game's rules it hooks into, and what Scry could not place, each thing a chip
    /// that goes to it. It is offered above the list while the list shows only what mods added,
    /// and by <c>/scry mods</c>.
    /// </summary>
    internal static partial class ScryPanel
    {
        private static bool _modReport;
        private static Vector2 _modScroll;
        private static float _modHeight;

        /// <summary>The report shown, made when opened for a catalog and a state of the locations, not every frame.</summary>
        private static List<ModSummary> _report;
        private static Explorer _reportFor;
        private static int _reportCount;
        private static Locations.State _reportLocations;
        private static int _reportSeen;

        /// <summary>How many mods added to the catalog, for the bar above the list.</summary>
        private static int _modCount;
        private static Explorer _modCountFor;
        private static int _modCountOf;

        /// <summary>Whether the mod report is shown, and how many times it has been drawn, for the self-test.</summary>
        public static bool ModReportShown => _modReport;
        public static int ModReportsDrawn { get; private set; }

        /// <summary>Puts the list back in the report's place.</summary>
        public static void HideModReport() => _modReport = false;

        /// <summary>Shows the mod report in the list's place.</summary>
        public static void ShowModReport()
        {
            _modReport = true;
            _help = false;
            _offDetails = false;
            _modScroll = Vector2.zero;
        }

        /// <summary>The report of the explorer's catalog, made again once the catalog, the locations or the drops seen in play have changed.</summary>
        public static List<ModSummary> Report(Explorer explorer)
        {
            if (_report == null || !ReferenceEquals(_reportFor, explorer) || _reportCount != explorer.Catalog.Count || _reportLocations != Locations.Now || _reportSeen != DropWatch.Version)
            {
                _reportFor = explorer;
                _reportCount = explorer.Catalog.Count;
                _reportLocations = Locations.Now;
                _reportSeen = DropWatch.Version;
                _report = ModReportReader.Read(explorer.Catalog);
            }
            return _report;
        }

        /// <summary>Above a list showing only mods' entries: how many mods there are, and the report.</summary>
        private static Rect ModsBar(Explorer explorer, Rect inner)
        {
            if (!ReferenceEquals(_modCountFor, explorer) || _modCountOf != explorer.Catalog.Count)
            {
                _modCountFor = explorer;
                _modCountOf = explorer.Catalog.Count;
                _modCount = explorer.Catalog.Where(e => e.Origin == Origin.Mod && e.Kind != Kind.Mod).Select(e => e.ModName).Distinct().Count();
            }
            var barH = U(30f);
            const string open = "Mod report";
            var w = Skin.Width(Skin.Chip, open) + U(12f);
            GUI.Label(new Rect(inner.x + U(10f), inner.y + U(2f), inner.width - w - U(24f), U(24f)),
                _modCount == 1 ? "What 1 mod adds" : $"What {_modCount} mods add", Skin.DimLabel);
            var button = new Rect(inner.xMax - w - U(6f), inner.y, w, U(26f));
            if (GUI.Button(button, open, Skin.Chip)) ShowModReport();
            if (button.Contains(Event.current.mousePosition)) AskTip("mod-report", "What each mod adds, what Scry links for its stations and tools, what it hooks into, and what Scry could not place");
            return new Rect(inner.x, inner.y + barH, inner.width, inner.height - barH);
        }

        private static void ModReportCard(Explorer explorer, Rect rect)
        {
            Skin.Box(rect, Skin.Panel);
            var closeH = U(30f);
            var area = new Rect(rect.x + U(4f), rect.y + U(6f), rect.width - U(8f), rect.height - closeH - U(20f));
            var view = new Rect(0f, 0f, area.width - U(14f), Mathf.Max(_modHeight, area.height));
            _modScroll = GUI.BeginScrollView(area, _modScroll, view, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);
            var visible = new Rect(0f, _modScroll.y, view.width, area.height);

            var x = U(14f);
            var width = view.width - U(24f);
            var y = U(10f);
            GUI.Label(new Rect(x, y, width, U(26f)), "Mods", Skin.Big);
            y += U(34f);

            y = Paragraph("What each mod adds, what Scry links for its crafting stations and build tools, which of the game's rules it hooks into, and what Scry could not place. What could not be placed may come from the mod's own code, which only the mod knows.", x, y, width);
            if (Locations.Now != Locations.State.Read) y = Paragraph("Until every location is read, what is found only in locations counts as having no source.", x, y, width);

            var report = Report(explorer);
            if (report.Count == 0) y = Paragraph("No mod adds anything or hooks into the game's drops or spawning.", x, y + U(6f), width);
            foreach (var mod in report)
            {
                y += U(10f);
                var headingH = U(26f);
                var heading = new Rect(x, y, width, headingH);
                GUI.Label(heading, mod.Mod, Skin.Heading);
                // A mod's name goes to its own page; the report stays for the next.
                var page = EntryKeys.For(Kind.Mod, mod.Mod);
                if (mod.Mod != ModReportReader.UnknownMod && InCatalog(explorer, page))
                {
                    if (heading.Contains(Event.current.mousePosition)) AskTip("mod:" + mod.Mod, "Go to its page: what it adds and changes");
                    if (GUI.Button(heading, GUIContent.none, GUIStyle.none)) Go(explorer, page);
                }
                y += headingH + U(2f);
                y = Paragraph(char.ToUpperInvariant(ModReportWords.Counts(mod)[0]) + ModReportWords.Counts(mod).Substring(1) + ".", x, y, width);
                if (mod.Hooks.Count > 0) y = Paragraph("Hooks into " + ModReportWords.Hooks(mod.Hooks) + ".", x, y, width);

                foreach (var station in mod.Stations) y = ChipLine(explorer, station, ModReportWords.Station(station), x, y, width, visible);
                foreach (var tool in mod.Tools) y = ChipLine(explorer, tool, ModReportWords.Tool(tool), x, y, width, visible);
                y = ReportChips(explorer, mod.Mod, "Stations nothing is made or built at", mod.IdleStations, x, y, width, visible);
                y = ReportChips(explorer, mod.Mod, "Items with no source Scry can see", mod.Sourceless, x, y, width, visible);
                y = ReportChips(explorer, mod.Mod, "Creatures that spawn nowhere Scry can see", mod.Unspawned, x, y, width, visible);
                y = ReportChips(explorer, mod.Mod, "Pieces in no build menu", mod.Unbuilt, x, y, width, visible);
            }
            if (Event.current.type == EventType.Repaint)
            {
                _modHeight = y + U(16f);
                ModReportsDrawn++;
            }
            GUI.EndScrollView();

            var closeRect = new Rect(rect.xMax - U(96f), rect.yMax - closeH - U(10f), U(80f), closeH);
            if (GUI.Button(closeRect, "Close", Skin.Button)) _modReport = false;
            if (Locations.Now == Locations.State.NotRead)
            {
                var w = Skin.Width(Skin.Button, LocationsButtonText) + U(10f);
                var locRect = new Rect(closeRect.x - U(8f) - w, closeRect.y, w, closeH);
                if (locRect.x > rect.x + U(8f))
                {
                    if (GUI.Button(locRect, LocationsButtonText, Skin.Button)) StartReadingLocations();
                    if (locRect.Contains(Event.current.mousePosition)) AskTip("locations-mods", LocationsButtonTip);
                }
            }
        }

        private static float Paragraph(string text, float x, float y, float width)
        {
            var height = Skin.Height(Skin.DimWrap, text, width);
            GUI.Label(new Rect(x, y, width, height), text, Skin.DimWrap);
            return y + height + U(4f);
        }

        /// <summary>A chip for a station or tool, going to it, and what Scry links for it beside it.</summary>
        private static float ChipLine(Explorer explorer, ModEntry entry, string told, float x, float y, float width, Rect visible)
        {
            var rowH = U(26f);
            var w = Mathf.Min(width * 0.5f, LinkChipWidth(entry.Shown, true));
            var chip = new Rect(x, y, w, rowH);
            if (chip.yMax >= visible.yMin && chip.yMin <= visible.yMax)
            {
                if (LinkChip(chip, entry.Shown, entry.Kind, false, true)) Go(explorer, entry.Key);
                if (chip.Contains(Event.current.mousePosition)) AskTip("mods-go:" + entry.Key, "Go to " + entry.Shown);
                GUI.Label(new Rect(x + w + U(8f), y + U(3f), width - w - U(8f), U(22f)), told, Skin.DimLabel);
            }
            return y + rowH + U(5f);
        }

        /// <summary>A heading and a wrapping row of chips, each going to its entry, the first few until asked for the rest.</summary>
        private static float ReportChips(Explorer explorer, string mod, string title, List<ModEntry> entries, float x, float y, float width, Rect visible)
        {
            if (entries.Count == 0) return y;
            // What Scry could not place may yet be placed by the mod's own code: marked, with why on hover.
            var titleRect = new Rect(x, y, width, U(20f));
            GUI.Label(titleRect, UnsureWords.Marked($"{title} ({entries.Count})"), Skin.DimLabel);
            if (titleRect.Contains(Event.current.mousePosition)) AskTip("unsure:" + mod + title, "Scry found nothing for these; the mod's own code may still place them");
            y += U(24f);
            var rowH = U(26f);
            var key = "mods:" + mod + ":" + title;
            var count = ShownOf(key, entries.Count);
            var cx = 0f;
            for (var i = 0; i < count; i++)
            {
                var entry = entries[i];
                var w = Mathf.Min(width, LinkChipWidth(entry.Shown, true));
                if (cx + w > width && cx > 0f)
                {
                    cx = 0f;
                    y += rowH + U(5f);
                }
                var chip = new Rect(x + cx, y, w, rowH);
                if (chip.yMax >= visible.yMin && chip.yMin <= visible.yMax)
                {
                    if (LinkChip(chip, entry.Shown, entry.Kind, false, true)) Go(explorer, entry.Key);
                    if (chip.Contains(Event.current.mousePosition)) AskTip("mods-go:" + entry.Key, "Go to " + entry.Shown);
                }
                cx += w + U(5f);
            }
            // The rest behind a chip of their own, as the details' long rows have, drawn here
            // against the report's own scrolling.
            if (Shortlist.Long(entries.Count, FirstChips))
            {
                var open = OpenLists.Contains(key);
                var text = open ? "Show fewer" : $"{Shortlist.Hidden(entries.Count, FirstChips, false)} more";
                var w = Mathf.Min(width, Skin.Width(Skin.Chip, text) + U(16f));
                if (cx + w > width && cx > 0f)
                {
                    cx = 0f;
                    y += rowH + U(5f);
                }
                var more = new Rect(x + cx, y, w, rowH);
                if (more.yMax >= visible.yMin && more.yMin <= visible.yMax && GUI.Button(more, text, Skin.Chip))
                {
                    if (open) OpenLists.Remove(key);
                    else OpenLists.Add(key);
                }
            }
            return y + rowH + U(10f);
        }
    }
}
