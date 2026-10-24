using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The rows of chips a page's facts are told in: a grid of every damage type, a row of items
    /// each going to its page (wares waiting for a boss counted rather than named under the
    /// compendium), a table by way of its own, and a chain of steps.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>
        /// A grid of every damage type: up to five cells a row, fewer where the side is narrow so
        /// none is cut, each its type over the share taken, coloured by how it lands, with the
        /// game's word on hover. Under the strict rules a creature's grid keeps back each type the
        /// game never told the character on a hit, ??? in its cell, and says faintly under it how
        /// many it keeps back.
        /// </summary>
        private static float GridRow(Explorer explorer, Facts.Row row, float width, float y)
        {
            y += U(6f);
            var titleRect = new Rect(0f, y, width, U(20f));
            GUI.Label(titleRect, Masked(explorer, row.Unsure != null ? UnsureWords.Marked(row.Title) : row.Title), Skin.DimLabel);
            if (row.Unsure != null && titleRect.Contains(Event.current.mousePosition)) AskTip("unsure:" + row.Title, row.Unsure);
            y += U(24f);

            var gap = U(4f);
            var columns = Mathf.Clamp(Mathf.FloorToInt((width + gap) / (U(62f) + gap)), 2, 5);
            var cellW = (width - gap * (columns - 1)) / columns;
            var cellH = U(38f);
            var keptBack = 0;
            for (var i = 0; i < row.Cells.Count; i++)
            {
                var cell = row.Cells[i];
                // A tool's cell is no secret: tools are for trees and rocks.
                var hidden = row.HitReveals && cell.Tone != Tone.Quiet && explorer.HidesDamage(cell.Type);
                if (hidden) keptBack++;
                var at = new Rect((i % columns) * (cellW + gap), y + (i / columns) * (cellH + gap), cellW, cellH);
                var cellSpot = Spotting ? EntryKeys.Damage(cell.Type) : null;
                NoteSpot(cellSpot, at);
                if (OutOfSight(at)) continue;
                // A quiet cell (tools' damage a creature takes none of) keeps its place but stands back.
                Skin.Box(at, cell.Tone == Tone.Quiet ? Skin.Panel : Skin.Raised);
                LitSpot(cellSpot, at);
                GUI.Label(new Rect(at.x + U(2f), at.y + U(3f), at.width - U(4f), U(16f)), cell.Type, Skin.CellType);
                Skin.LabelIn(new Rect(at.x + U(2f), at.y, at.width - U(4f), at.height - U(4f)), hidden ? CompendiumWords.Hidden : cell.Value, Skin.CellValue, ToneColor(hidden ? Tone.Plain : cell.Tone));
                if (at.Contains(Event.current.mousePosition)) AskTip("cell:" + row.Title + cell.Type + hidden, hidden ? CompendiumWords.NotYetHitWithTip(cell.Type) : cell.Tip);
            }
            CountDrawn(PanelPart.ResistanceGrid);
            var lines = (row.Cells.Count + columns - 1) / columns;
            y += lines * (cellH + gap) + U(4f);
            return HintLine(CompendiumWords.NotYetHitWith(keptBack), "damage-kept-back", "", PanelPart.DamageKeptBack, width, y);
        }

        /// <summary>Whether the titles of rows told as waiting for a boss are kept as drawn (<see cref="UnlockTitlesForTest"/>), for the self-test.</summary>
        [Diagnostic]
        public static bool KeepUnlockTitlesForTest { get; set; }

        /// <summary>The titles of rows told as waiting for a boss, with the frame each was drawn in, while kept for the self-test.</summary>
        [Diagnostic]
        public static List<(int Frame, string Title)> UnlockTitlesForTest { get; } = new List<(int, string)>();

        /// <summary>The title a row of wares waiting for a boss is counted under, as the compendium shows it; none for any other row.</summary>
        private static string CountedAs(Explorer explorer, Facts.Row row) =>
            row?.WaitsFor != null && UnlockRows.Of(row.WaitsFor, explorer.NamesOnlyMet, explorer.LeavingOut, WorldKeySet) == UnlockShown.Counted ? Masked(explorer, row.Title) : null;

        /// <summary>Whether the world played has a key set, a boss's fall; none without a world.</summary>
        private static readonly Func<string, bool> WorldKeySet = key => ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key);

        /// <summary>
        /// A titled row of chips, each an item with its amount that goes to it when clicked; the
        /// title goes to the station it names. A long row shows its first chips and one for the rest.
        /// Given the rows it stands among (<paramref name="rows"/>, it at <paramref name="rowAt"/>),
        /// rows of wares counted under one title as the compendium shows it are told as one.
        /// </summary>
        private static float FactRow(Explorer explorer, Facts.Row row, float width, float y, IReadOnlyList<Facts.Row> rows = null, int rowAt = -1)
        {
            if (row.AllPlay && explorer.LeavingOut) return y;
            if (row.WaitsFor != null && WaitingRow(explorer, row, width, ref y, rows, rowAt)) return y;
            if (row.Cells != null) return GridRow(explorer, row, width, y);
            if (row.Columns != null) return TableRow(explorer, row, width, y);
            if (row.Chain != null) return ChainRow(explorer, row, width, y);
            // A list of what there is shows only what the character has seen, and nothing where it saw none.
            var listing = row.Lists && explorer.CompendiumOn;
            var listed = row.Lists ? Listed(explorer, row.Items, i => i.Prefab) : row.Items;
            if (listing && listed.Count == 0)
            {
                CountDrawn(PanelPart.ListLeftOut);
                return y;
            }
            var items = RowItems(explorer, listed);
            if (items.Count == 0) return y;
            var title = RowTitle(explorer, row, listing, items.Count, listed.Count);
            var key = "facts:" + row.Title;
            var count = ShownOf(key, items.Count);
            y = RowTitleLine(explorer, row, title, width, y + U(6f));

            var notCounted = false;
            var flow = new ChipFlow(0f, width, y, U(30f), U(6f), U(5f));
            for (var i = 0; i < count; i++) notCounted |= ItemChip(explorer, row, items[i], width, ref flow);
            MoreChip(key, items.Count, FirstChips, width, ref flow);
            y = flow.RowBottom + U(6f);
            if (notCounted) y = HintLine(explorer.NotYetCounted(row.Tally), "not-counted:", row.Title, PanelPart.TallyCounted, width, y - U(2f));
            return y;
        }

        /// <summary>
        /// Wares waiting for a boss not yet fallen, not named with the compendium on: under the loose
        /// rules the row keeps its title and how many, under the strict rules they are counted at the
        /// section's end. True where the row is told so, and where it stands then.
        /// </summary>
        private static bool WaitingRow(Explorer explorer, Facts.Row row, float width, ref float y, IReadOnlyList<Facts.Row> rows, int rowAt)
        {
            var unlock = UnlockRows.Of(row.WaitsFor, explorer.NamesOnlyMet, explorer.LeavingOut, WorldKeySet);
            if (unlock == UnlockShown.Folded)
            {
                ToUnlock(row.Items.Count);
                return true;
            }
            if (unlock != UnlockShown.Counted) return false;
            // Rows whose titles read the same once names not met are kept back ("Sells once ??? is
            // defeated") are one wherever they stand: the first tells them all, the others add to its count.
            var countedAs = Masked(explorer, row.Title);
            var waiting = rows != null && rowAt >= 0 ? UnlockRows.Told(rows, rowAt, r => CountedAs(explorer, r), r => r.Items.Count) : row.Items.Count;
            if (waiting == null) return true;
            if (KeepUnlockTitlesForTest) UnlockTitlesForTest.Add((Time.frameCount, countedAs));
            y += U(6f);
            GUI.Label(new Rect(0f, y, width, U(20f)), countedAs, Skin.DimLabel);
            y = HintLine(CompendiumWords.ToUnlock(waiting.Value), "to-unlock:", row.Title, PanelPart.ToUnlock, width, y + U(20f));
            return true;
        }

        /// <summary>The items of a row shown under the compendium's rules; a thing to bring shows once what suggests it was found out, left out quietly, as a suggestion.</summary>
        private static IReadOnlyList<Facts.Ingredient> RowItems(Explorer explorer, IReadOnlyList<Facts.Ingredient> listed)
        {
            var items = ShownStrictly(explorer, listed, i => i.Prefab);
            if (!explorer.LeavingOut || !Suggests(items)) return items;
            Suggested.Clear();
            foreach (var item in items) if (!explorer.KeepsBackAll(item.ShownBy)) Suggested.Add(item);
            return Suggested;
        }

        /// <summary>A row's title: how many found where it lists what there is, uncounted where some are left out, as the character saw them under the strict rules, and a name not met kept back.</summary>
        private static string RowTitle(Explorer explorer, Facts.Row row, bool listing, int shown, int listed)
        {
            var title = listing ? CompendiumWords.Found(row.Title, shown) : shown < listed ? CompendiumWords.Uncounted(row.Title) : row.Title;
            // Under the strict rules drops and yields are told as the character saw them, not by the game's chances.
            var counted = explorer.StrictTitle(row.Tally, row.TallyOf, row.TallyLead);
            if (counted != null) title = counted;
            // A name in the title the character has not met is kept back, as in any text on the page.
            title = Masked(explorer, title);
            return title;
        }

        /// <summary>A row's title line: marked where Scry is not sure of the row, saying why on hover; a link to the station it names; else plain. Where the chips start.</summary>
        private static float RowTitleLine(Explorer explorer, Facts.Row row, string title, float width, float y)
        {
            if (row.Unsure != null)
            {
                var unsureRect = new Rect(0f, y, width, U(20f));
                GUI.Label(unsureRect, UnsureWords.Marked(title), Skin.DimLabel);
                if (unsureRect.Contains(Event.current.mousePosition)) AskTip("unsure:" + row.Title, row.Unsure);
            }
            else if (!string.IsNullOrEmpty(row.TitleLink) && Goes(explorer, row.TitleLink))
            {
                var titleMark = ChoiceMark(explorer, row.TitleLink);
                var titleFloor = titleMark != null;
                var titleW = Mathf.Min(width - (titleFloor ? FloorRoom : 0f), Skin.Width(Skin.DimLabel, title) + U(4f));
                var titleRect = new Rect(0f, y, titleW, U(20f));
                LinkLabel(titleRect, title, Skin.DimLabel, LinkText(KindOf(explorer, row.TitleLink), false));
                if (titleFloor) FloorMark(titleRect.xMax, titleRect, titleMark);
                if (titleRect.Contains(Event.current.mousePosition)) AskTip("row-title:" + row.TitleLink, FloorTip(explorer, row.TitleLink, PanelWords.GoTo(ShownName(explorer, row.TitleLink, row.TitleLink))));
                if (GUI.Button(titleRect, GUIContent.none, GUIStyle.none)) Go(explorer, row.TitleLink);
            }
            else
            {
                GUI.Label(new Rect(0f, y, width, U(20f)), title, Skin.DimLabel);
            }
            return y + U(24f);
        }

        /// <summary>
        /// One item's chip in the row's flow, with its amount: a search to run, or the item with its
        /// icon, a mark (only here, what a trader pays) in a pill at its end and the floor's mark,
        /// going to it on a click. True where its amount is a count not yet taken.
        /// </summary>
        private static bool ItemChip(Explorer explorer, Facts.Row row, Facts.Ingredient item, float width, ref ChipFlow flow)
        {
            var amount = explorer.StrictAmount(row.Tally, row.TallyOf, item.Prefab) ?? item.Amount;
            var hidden = amount == CompendiumWords.Hidden;
            var text = DetailWords.Amounted(amount, item.Name);
            // A mark (only here, what a trader pays) in a pill of its own at the chip's end.
            var markW = item.Mark != null ? Skin.Width(Skin.Badge, item.Mark) + U(16f) : 0f;
            var itemMark = !string.IsNullOrEmpty(item.Prefab) ? ChoiceMark(explorer, item.Prefab) : null;
            var floorW = itemMark != null ? FloorRoom : 0f;
            markW += floorW;
            var w = Mathf.Min(width, Skin.Width(Skin.Chip, text) + U(30f) + markW);
            var at = flow.Place(w);
            var chip = new Rect(at.X, at.Y, w, flow.RowHeight);
            if (OutOfSight(chip)) return hidden;
            var hover = chip.Contains(Event.current.mousePosition);
            // A chip that runs a search, such as for what to bring against a creature.
            if (EntryKeys.SearchOf(item.Prefab) is string query)
            {
                Skin.PillBox(chip, hover ? Skin.RaisedHover : Skin.Raised);
                Skin.LabelIn(new Rect(chip.x + U(10f), chip.y, chip.width - U(14f), chip.height), text, Skin.Small, Skin.Accent);
                if (hover) AskTip("search:" + query, PanelWords.TryInSearch(query));
                if (GUI.Button(chip, GUIContent.none, GUIStyle.none)) SearchFor(explorer, query);
                return hidden;
            }
            var goes = !string.IsNullOrEmpty(item.Prefab) && Goes(explorer, item.Prefab);
            var kind = goes ? KindOf(explorer, item.Prefab) : null;
            Skin.PillBox(chip, goes ? LinkFill(kind, hover) : Skin.Raised);
            if (item.Icon != null && !Unseen(explorer, item.Prefab)) DrawSprite(item.Icon, new Rect(chip.x + U(6f), chip.y + U(4f), U(22f), U(22f)));
            Skin.LabelIn(new Rect(chip.x + U(32f), chip.y, chip.width - U(36f) - markW, chip.height), text, Skin.Small, goes ? LinkText(kind, hover) : Skin.Text);
            if (item.Mark != null)
            {
                // A solid accent pill, its words dark on it.
                var mark = new Rect(chip.xMax - markW - U(4f), chip.y + U(5f), markW - floorW, chip.height - U(10f));
                Skin.PillBox(mark, Skin.Accent);
                GUI.Label(mark, item.Mark, Skin.Badge);
            }
            // What only the floor shows keeps the floor's mark at the chip's end.
            if (floorW > 0f) PlateFloorMark(chip, itemMark);

            // One with no page to go to, as an upgrade a trader sells, says on hover what it does.
            if (string.IsNullOrEmpty(item.Prefab) && hover && item.MarkTip != null) AskTip("chip:" + item.Name, item.MarkTip);

            // Clicking an ingredient or a drop goes to it.
            if (!string.IsNullOrEmpty(item.Prefab))
            {
                if (hover && goes) AskTip("goto:" + item.Prefab + item.Mark, FloorTip(explorer, item.Prefab, item.MarkTip != null ? Naming.Lines(item.MarkTip, PanelWords.GoTo(item.Name)) : PanelWords.GoTo(item.Name)));
                if (GUI.Button(chip, GUIContent.none, GUIStyle.none) && explorer.Jump(item.Prefab))
                {
                    AfterGoing();
                }
            }
            return hidden;
        }

        /// <summary>The things to bring the row being drawn shows, the rest kept back (<see cref="Facts.Ingredient.ShownBy"/>).</summary>
        private static readonly List<Facts.Ingredient> Suggested = new List<Facts.Ingredient>();

        /// <summary>Whether a row's chips are things to bring, shown by what was found out.</summary>
        private static bool Suggests(IReadOnlyList<Facts.Ingredient> items)
        {
            for (var i = 0; i < items.Count; i++) if (items[i].ShownBy != null) return true;
            return false;
        }

        /// <summary>Each chain as last drawn, by its title: its steps' keys in turn, and whether each read ??? as not met; for the self-test.</summary>
        [Diagnostic]
        public static Dictionary<string, List<(string Key, bool Unmet)>> ChainsDrawnForTest { get; } = new Dictionary<string, List<(string, bool)>>();

        /// <summary>
        /// A chain's steps, an arrow between each, the page's own lit and each other one going to its
        /// page; with the compendium on, a step not met reads ???, going nowhere, so the chain stands
        /// whole whatever the character met of it.
        /// </summary>
        private static float ChainRow(Explorer explorer, Facts.Row row, float width, float y)
        {
            if (!ChainsDrawnForTest.TryGetValue(row.Title, out var drawn)) ChainsDrawnForTest[row.Title] = drawn = new List<(string, bool)>();
            drawn.Clear();
            y += U(6f);
            GUI.Label(new Rect(0f, y, width, U(20f)), Masked(explorer, row.Title), Skin.DimLabel);
            y += U(24f);
            var shown = explorer.Selected as Entry;
            var flow = new ChipFlow(0f, width, y, U(26f), U(4f), U(5f));
            for (var s = 0; s < row.Chain.Steps.Count; s++)
            {
                if (s > 0)
                {
                    var arrowW = Skin.Width(Skin.Small, ChainWords.Arrow) + U(4f);
                    var arrow = flow.Place(arrowW);
                    GUI.Label(new Rect(arrow.X, arrow.Y, arrowW, flow.RowHeight), ChainWords.Arrow, Skin.Small);
                }
                foreach (var key in row.Chain.Steps[s])
                {
                    var here = shown != null && (shown.Name == key || shown.Key == key);
                    var unmet = !here && explorer.NamesOnlyMet && explorer.LeavesOut(key);
                    drawn.Add((key, unmet));
                    if (unmet)
                    {
                        var hiddenW = Mathf.Min(width, LinkChipWidth(CompendiumWords.Hidden, false, false));
                        var hiddenAt = flow.Place(hiddenW);
                        var hidden = new Rect(hiddenAt.X, hiddenAt.Y, hiddenW, flow.RowHeight);
                        if (!OutOfSight(hidden)) LinkChip(hidden, CompendiumWords.Hidden, null, false, false);
                        continue;
                    }
                    var name = ShownName(explorer, key, key);
                    var mark = !here ? ChoiceMark(explorer, key) : null;
                    var w = Mathf.Min(width, LinkChipWidth(name, true, mark != null));
                    var at = flow.Place(w);
                    var chip = new Rect(at.X, at.Y, w, flow.RowHeight);
                    if (OutOfSight(chip)) continue;
                    if (here)
                    {
                        GUI.Label(chip, name, Skin.Fitted(Skin.ChipOn, chip));
                        continue;
                    }
                    var goes = Goes(explorer, key);
                    if (LinkChip(chip, name, goes ? KindOf(explorer, key) : null, false, goes, goes ? mark : null) && goes) Go(explorer, key);
                    if (goes && chip.Contains(Event.current.mousePosition)) AskTip("chain:" + key, FloorTip(explorer, key, PanelWords.GoTo(name)));
                }
            }
            return flow.RowBottom + U(6f);
        }
    }
}
