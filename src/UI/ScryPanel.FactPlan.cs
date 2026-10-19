using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>In the game laid out by topic (<see cref="FactLayout"/>): the plan for the page shown, its topics' headings, tiles and notes.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>The facts the plan was made for and the blocks they had; made again only when either changes.</summary>
        private static Facts _planFacts;

        private static int _planBlocks = -1;
        private static List<FactTopicPlan> _plan;

        private static readonly FactBlock[] EveryBlock = (FactBlock[])Enum.GetValues(typeof(FactBlock));

        /// <summary>How tall each topic's box was when last drawn, so the box is drawn behind what it holds; 0 where it is not yet known.</summary>
        private static readonly List<float> TopicHeights = new List<float>();
        private static readonly List<(string, string)> PlanPairs = new List<(string, string)>();
        private static readonly List<(string, string)> PlanRows = new List<(string, string)>();
        private static readonly List<FactBlock> PlanBlocks = new List<FactBlock>();
        private static readonly List<string> PlanLinks = new List<string>();

        /// <summary>The page's topics for an entry, or null for a kind not laid out by topic yet.</summary>
        private static List<FactTopicPlan> PlanOf(Explorer explorer, Entry entry, Facts facts, bool places)
        {
            if (!FactLayout.LaysOut(entry.Kind)) return null;
            PlanBlocks.Clear();
            if (facts.Where.Count > 0) PlanBlocks.Add(FactBlock.Where);
            if (places) PlanBlocks.Add(FactBlock.FoundIn);
            if (entry.Biomes.Length > 0 && entry.Kind != Kind.Biome) PlanBlocks.Add(FactBlock.Biomes);
            if (Users(explorer, entry).Count > 0) PlanBlocks.Add(FactBlock.Users);
            if (facts.UseRows.Count > 0) PlanBlocks.Add(FactBlock.Uses);
            if (ModHookWords.Line(facts.Hooks) != null) PlanBlocks.Add(FactBlock.Hooks);
            var blocks = 0;
            foreach (var block in PlanBlocks) blocks |= 1 << (int)block;
            if (ReferenceEquals(facts, _planFacts) && blocks == _planBlocks) return _plan;

            PlanPairs.Clear();
            for (var i = 0; i < facts.Pairs.Count; i++) PlanPairs.Add((facts.Pairs[i].Key, facts.PairParts[i]));
            PlanRows.Clear();
            foreach (var row in facts.Rows) PlanRows.Add((row.Title, row.Part ?? ""));
            PlanLinks.Clear();
            foreach (var group in entry.LinkGroups()) PlanLinks.Add(group.Key);
            _plan = FactLayout.Plan(entry.Kind, PlanPairs, PlanRows, PlanBlocks, PlanLinks);
            _planFacts = facts;
            _planBlocks = blocks;
            TopicHeights.Clear();
            return _plan;
        }

        /// <summary>
        /// The page's topics: the headline tiles as they are, and each named topic in a box of
        /// its own, its name bright in the box's top left and what it holds inset under it, so a
        /// topic is told apart from the page's sections at a glance. A box is drawn behind what it
        /// holds at the height that held last time; the first time it shows, it is drawn the
        /// time after.
        /// </summary>
        private static float Topics(Explorer explorer, Entry entry, Facts facts, List<FactTopicPlan> plan, bool places, float width, float y)
        {
            while (TopicHeights.Count < plan.Count) TopicHeights.Add(0f);
            var pad = U(10f);
            var inner = width - pad * 2f;
            var labelW = Mathf.Min(U(130f), inner * 0.36f);
            for (var i = 0; i < plan.Count; i++)
            {
                var topic = plan[i];
                StrictSectionStarts();
                if (topic.Heading == null)
                {
                    NoteTopic(topic.Id, y);
                    y = TopicBody(explorer, entry, facts, topic, places, width, Mathf.Min(U(130f), width * 0.36f), y);
                    y = StrictLeftLine(explorer, topic.Id, width, y);
                    continue;
                }

                var top = y + U(6f);
                NoteTopic(topic.Id, top);
                if (TopicHeights[i] > 0f) Skin.Box(new Rect(0f, top, width, TopicHeights[i]), Skin.TopicFill);
                GUI.Label(new Rect(pad, top + U(6f), inner, U(20f)), topic.Heading, Skin.TopicName);
                y = top + U(30f);
                GUI.BeginGroup(new Rect(pad, 0f, inner, y + U(100000f)));
                try
                {
                    y = TopicBody(explorer, entry, facts, topic, places, inner, labelW, y);
                    y = StrictLeftLine(explorer, topic.Id, inner, y);
                }
                finally
                {
                    GUI.EndGroup();
                }
                y += U(4f);
                if (Event.current.type == EventType.Repaint) TopicHeights[i] = y - top;
                y += U(4f);
            }
            return y;
        }

        /// <summary>What a topic holds: its tiles, then its facts, rows, blocks and links, each with the notes under it.</summary>
        private static float TopicBody(Explorer explorer, Entry entry, Facts facts, FactTopicPlan topic, bool places, float width, float labelW, float y)
        {
            // Each kind of bit is timed apart, so the first page of a kind tells what its drawing costs.
            var step = Timing.Start();
            if (topic.Tiles.Count > 0)
            {
                // A tile naming what the rules leave out is left out and counted, as a fact is.
                ShownTiles.Clear();
                foreach (var tile in topic.Tiles)
                {
                    if (facts.Links.TryGetValue(facts.Pairs[tile].Key, out var linked) && explorer.LeavesOut(linked)) StrictLeft(1);
                    else ShownTiles.Add(tile);
                }
                if (ShownTiles.Count > 0) y = Tiles(explorer, facts, ShownTiles, width, y);
                Timing.Add("facts drawn tiles", step);
            }
            foreach (var bit in topic.Bits)
            {
                step = Timing.Start();
                if (bit.Pair >= 0)
                {
                    y = FactPair(explorer, entry, facts, facts.Pairs[bit.Pair], width, labelW, y);
                    Timing.Add("facts drawn pairs", step);
                }
                else if (bit.Row >= 0)
                {
                    y = FactRow(explorer, facts.Rows[bit.Row], width, y, facts.Rows, bit.Row);
                    Timing.Add("facts drawn rows", step);
                }
                else if (bit.Block is FactBlock block)
                {
                    y = FactBlockOf(explorer, entry, facts, block, places, false, width, y);
                    Timing.Add("facts drawn blocks", step);
                }
                else if (bit.LinkGroup != null)
                {
                    y = PlacedLinks(explorer, entry, bit.LinkGroup, topic.Bits.Count > 1, width, y);
                    Timing.Add("facts drawn links", step);
                }
                if (bit.Notes.Count == 0) continue;
                step = Timing.Start();
                foreach (var note in bit.Notes) y = FactNote(explorer, facts, facts.Pairs[note], width, y);
                Timing.Add("facts drawn notes", step);
            }
            return y;
        }

        /// <summary>Lets go of the plan and the facts it was made for, for another world.</summary>
        private static void ForgetFactPlan()
        {
            _planFacts = null;
            _plan = null;
            _planBlocks = -1;
            ShownCounts.Clear();
        }

        /// <summary>The tiles of the topic being drawn that the rules keep, kept so no frame makes a new list.</summary>
        private static readonly List<int> ShownTiles = new List<int>();

        /// <summary>
        /// A topic's headline numbers as tiles, a small label over each value, up to four in a
        /// row and fewer where the side is narrow, each row as tall as its tallest value. Under
        /// the strict rules a number kept back reads ??? and says on hover what shows it, as a
        /// table's cell does, and names never met read ???.
        /// </summary>
        private static float Tiles(Explorer explorer, Facts facts, List<int> tiles, float width, float y)
        {
            y += U(2f);
            var gap = U(6f);
            var columns = Mathf.Clamp(Mathf.FloorToInt((width + gap) / (U(110f) + gap)), 1, Mathf.Min(4, tiles.Count));
            var tileW = (width - gap * (columns - 1)) / columns;
            var valueW = tileW - U(16f);
            for (var first = 0; first < tiles.Count; first += columns)
            {
                var last = Mathf.Min(tiles.Count, first + columns);
                var valueH = U(20f);
                var yoursH = 0f;
                for (var i = first; i < last; i++)
                {
                    valueH = Mathf.Max(valueH, Skin.Height(Skin.Wrap, TileValue(explorer, facts, facts.Pairs[tiles[i]], out var keptHere), valueW));
                    if (keptHere == null && YoursShown(explorer, facts, facts.Pairs[tiles[i]].Key, out var yoursHere)) yoursH = Mathf.Max(yoursH, Skin.Height(Skin.SmallWrap, yoursHere, valueW));
                }
                var tileH = U(26f) + valueH + yoursH + U(6f);
                for (var i = first; i < last; i++)
                {
                    var pair = facts.Pairs[tiles[i]];
                    var at = new Rect((i - first) * (tileW + gap), y, tileW, tileH);
                    if (OutOfSight(at)) continue;
                    // A tile Scry is not sure of is marked, softer, and says why on hover.
                    var unsure = facts.Unsure.TryGetValue(pair.Key, out var why);
                    Skin.Box(at, Skin.Raised);
                    GUI.Label(new Rect(at.x + U(8f), at.y + U(4f), valueW, U(20f)), unsure ? UnsureWords.Marked(pair.Key) : pair.Key, Skin.DimLabel);
                    var value = TileValue(explorer, facts, pair, out var kept);
                    GUI.Label(new Rect(at.x + U(8f), at.y + U(24f), valueW, valueH), value, unsure ? Skin.DimWrap : Skin.Wrap);
                    if (unsure && at.Contains(Event.current.mousePosition)) AskTip("unsure:" + pair.Key, why);
                    // What it comes to at the character's own skill, faint under it.
                    if (kept == null && YoursShown(explorer, facts, pair.Key, out var yours))
                    {
                        var yoursRect = new Rect(at.x + U(8f), at.y + U(24f) + valueH, valueW, yoursH);
                        Skin.LabelIn(yoursRect, yours, Skin.SmallWrap, Skin.Faint);
                        if (facts.YoursTips.TryGetValue(pair.Key, out var tip) && yoursRect.Contains(Event.current.mousePosition)) AskTip("yours:" + pair.Key, tip);
                        CountDrawn(PanelPart.YourSkillLine);
                    }
                    if (kept == null) continue;
                    CountDrawn(PanelPart.FactKeptBack);
                    if (at.Contains(Event.current.mousePosition)) AskTip("tile-kept:" + pair.Key, KeptTip(explorer, pair.Key, kept));
                }
                y += tileH + gap;
            }
            CountDrawn(PanelPart.FactTiles);
            return y + U(2f);
        }

        /// <summary>What a tile shows: ??? for a number kept back, with what shows it (<paramref name="kept"/>), else its value with names never met kept back.</summary>
        private static string TileValue(Explorer explorer, Facts facts, KeyValuePair<string, string> pair, out string kept)
        {
            kept = facts.Proofs.TryGetValue(pair.Key, out var proof) && explorer.KeepsBack(proof) ? proof : null;
            if (kept != null) return CompendiumWords.Hidden;
            // A tile counting a row says how many of it the page names, not all there are.
            if (explorer.NamesOnlyMet && facts.TileCounts.TryGetValue(pair.Key, out var row)) return ShownCount(explorer, row);
            return Masked(explorer, pair.Value);
        }

        /// <summary>How many of a row's chips the page names, in a tile's words; worked out again only as what the character knows changes.</summary>
        private static string ShownCount(Explorer explorer, Facts.Row row)
        {
            if (ShownCounts.TryGetValue(row, out var told) && told.Stamp == explorer.KnowStamp) return told.Said;
            var count = 0;
            foreach (var item in row.Items) if (!explorer.LeavesOut(item.Prefab)) count++;
            var said = CompendiumWords.MetCount(count);
            ShownCounts[row] = (explorer.KnowStamp, said);
            return said;
        }

        private static readonly Dictionary<Facts.Row, (int Stamp, string Said)> ShownCounts = new Dictionary<Facts.Row, (int, string)>();

        /// <summary>The widths of a table's columns as it is drawn, kept for every table so none makes a new list each frame.</summary>
        private static readonly List<float> ColumnWidths = new List<float>();

        /// <summary>How wide each heading of the table being drawn is, which its column keeps where it can.</summary>
        private static readonly List<float> HeadingWidths = new List<float>();

        /// <summary>Whether each table's widths are kept as drawn (<see cref="TablesDrawnForTest"/>), for the self-test.</summary>
        [Diagnostic]
        public static bool KeepTablesForTest { get; set; }

        /// <summary>Each table's column widths and its headings' as last drawn, by its title, while kept for the self-test.</summary>
        [Diagnostic]
        public static Dictionary<string, (float[] Columns, float[] Headings)> TablesDrawnForTest { get; } = new Dictionary<string, (float[], float[])>();

        /// <summary>
        /// A table: its title, a faint line of column headings, then a line for each of its
        /// lines, the first cell going to what it names. Columns are as wide as what they show
        /// (??? where the compendium keeps a number back, nothing of a line left out), narrowed
        /// together where the side is narrower, a cell wrapping where it must.
        /// </summary>
        private static float TableRow(Explorer explorer, Facts.Row row, float width, float y)
        {
            // A table the compendium's rules leave every line out of draws nothing, its title and headings neither.
            if (row.Lines.Count > 0 && explorer.NamesOnlyMet && row.Lines.TrueForAll(l => !string.IsNullOrEmpty(l.Link) && explorer.LeavesOut(l.Link)))
            {
                StrictLeft(row.Lines.Count);
                CountDrawn(PanelPart.EmptyRowLeftOut);
                return y;
            }
            // A table every line of which waits to be found out (a creature's stars before one was
            // killed) is one faint line saying what shows it, not a table of ???.
            var whole = explorer.KeepsBackWhole(row.LineProofs, row.Lines.Count);
            if (whole != null)
            {
                return HintLine(explorer.ShowsOnce(row.Title, whole), explorer.MayRunElsewhere(whole) ? ElsewhereWords.Tip : null, "table-kept:", row.Title, PanelPart.TableKeptBack, width, y + U(6f));
            }
            y += U(6f);
            GUI.Label(new Rect(0f, y, width, U(20f)), Masked(explorer, row.Title), Skin.DimLabel);
            y += U(24f);

            var gap = U(12f);
            ColumnWidths.Clear();
            HeadingWidths.Clear();
            for (var c = 0; c < row.Columns.Length; c++)
            {
                var w = Skin.Width(Skin.DimLabel, row.Columns[c]);
                HeadingWidths.Add(w + U(2f));
                for (var l = 0; l < row.Lines.Count; l++)
                {
                    // Measured as drawn: a line left out takes no room, a number kept back that of ???.
                    if (LineLeftOut(explorer, row.Lines[l].Link)) continue;
                    var drawn = CellDrawn(explorer, row, l, c);
                    if (drawn != null) w = Mathf.Max(w, Skin.Width(Skin.Small, drawn) + (drawn != CompendiumWords.Hidden && explorer.ShownByFloor(CellLink(row, l, c)) ? FloorRoom : 0f));
                    if (!row.Yours.TryGetValue(l, out var yours) || LineKept(explorer, row, l, out _) || AttackKept(explorer, row, l)) continue;
                    foreach (var under in yours) if (c < under.Cells.Length && !explorer.NotMet(under.Skill)) w = Mathf.Max(w, Skin.Width(Skin.Small, under.Cells[c]) + (c == 0 ? YoursIndent : 0f));
                }
                ColumnWidths.Add(w + U(2f));
            }
            // Too wide, each column gives up what it wants beyond its heading, so the headings stay whole.
            TableFit.Fit(ColumnWidths, HeadingWidths, width - gap * (row.Columns.Length - 1));
            if (KeepTablesForTest) TablesDrawnForTest[row.Title ?? ""] = (ColumnWidths.ToArray(), HeadingWidths.ToArray());

            var x = 0f;
            for (var c = 0; c < row.Columns.Length; c++)
            {
                GUI.Label(new Rect(x, y, ColumnWidths[c], U(18f)), Skin.Fit(Skin.DimLabel, row.Columns[c], ColumnWidths[c]), Skin.DimLabel);
                x += ColumnWidths[c] + gap;
            }
            y += U(22f);

            var attacksKept = 0;
            var notCounted = false;
            KeptLines.Clear();
            for (var l = 0; l < row.Lines.Count; l++)
            {
                var (cells, link) = row.Lines[l];
                // A line of what was not seen together with the page (an attack never seen) is left out.
                if (LineLeftOut(explorer, link))
                {
                    StrictLeft(1);
                    continue;
                }
                // Under the strict rules an attack that never hit the character keeps its damage
                // back, and a line only something seen done tells (health by stars) all its numbers.
                var attackKept = AttackKept(explorer, row, l);
                if (attackKept) attacksKept++;
                var lineKept = LineKept(explorer, row, l, out var lineProof);
                if (lineKept && cells.Length > 0) KeptLines.Add((explorer.ShowsOnce(cells[0], lineProof), explorer.MayRunElsewhere(lineProof)));
                var height = U(20f);
                for (var c = 0; c < cells.Length && c < ColumnWidths.Count; c++) height = Mathf.Max(height, Skin.Height(Skin.SmallWrap, CellDrawn(explorer, row, l, c), ColumnWidths[c]));
                // Faint lines under it: the same at the character's skill, left out where the line
                // keeps its numbers back, and each where the compendium shows no such skill.
                var under = !lineKept && !attackKept && row.Yours.TryGetValue(l, out var yoursLines) ? yoursLines : null;
                var underHeight = 0f;
                if (under != null) foreach (var faint in under) if (!explorer.NotMet(faint.Skill)) underHeight += YoursHeight(faint.Cells);
                var line = new Rect(0f, y, width, height + underHeight);
                if (!OutOfSight(line))
                {
                    x = 0f;
                    for (var c = 0; c < cells.Length && c < ColumnWidths.Count; c++)
                    {
                        var cell = new Rect(x, y, ColumnWidths[c], height);
                        // An attack's name plays its animation on the stage and stops it again, lit
                        // while it plays, as its clip's chip does: a creature's, or a weapon's on the
                        // person wearing it, put on the person first where it is shown on its own.
                        if (c == 0 && EntryKeys.PlaysAttack(link, out var item, out var second, out var step))
                        {
                            var clip = Previews.ClipOfAttackItem(item, second, step);
                            var playing = clip != null && Previews.PlayingClip() == clip;
                            var wearFirst = clip == null && explorer.Selected is Entry weapon && weapon.Kind == Kind.Item && weapon.Name == item && !Looks.IsWorn(weapon);
                            // Its clips worked out and none of them its own: it plays nothing, so its name is no link.
                            if (clip == null && !wearFirst && Previews.StageClipsKnown())
                            {
                                GUI.Label(cell, Masked(explorer, cells[c]), Skin.SmallWrap);
                                if (cell.Contains(Event.current.mousePosition)) AskTip("table-attack-none:" + link, ClipWords.NoAttackClip);
                                x += ColumnWidths[c] + gap;
                                continue;
                            }
                            LinkLabel(cell, cells[c], Skin.SmallWrap, playing ? Skin.KindColor(Kind.Sound) : Skin.Accent);
                            if (cell.Contains(Event.current.mousePosition)) AskTip("table-attack:" + link + (clip != null) + playing + wearFirst, ClipWords.PlayAttackTip(clip != null, playing, wearFirst));
                            if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                            {
                                if (playing) Previews.StopClip();
                                else if (clip != null)
                                {
                                    Previews.PlayClip(clip);
                                    Previews.LastClip = clip;
                                }
                                else if (wearFirst)
                                {
                                    Looks.OnPerson = true;
                                    Previews.Rebuild();
                                    Previews.PlayAttackWhenKnown(item, second, step);
                                    SaveRects();
                                }
                            }
                        }
                        // A line naming music plays it, as a fact naming music does.
                        else if (c == 0 && !string.IsNullOrEmpty(link) && EntryKeys.PlaysMusic(link, out var named) && explorer.Selected is Entry shown)
                        {
                            var playing = MusicPreview.PlayingFor == shown && (named == null || MusicPreview.Playing == named);
                            LinkLabel(cell, cells[c], Skin.SmallWrap, playing ? Skin.KindColor(Kind.Sound) : Skin.Accent);
                            if (cell.Contains(Event.current.mousePosition)) AskTip("table-music:" + named, MusicWords.Tip(playing));
                            if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                            {
                                var said = named == null ? Previews.PlacesMusic(shown) : Previews.NamedMusic(shown, named);
                                if (said != null) Say(said);
                            }
                        }
                        else if (c == 0 && !string.IsNullOrEmpty(link) && Goes(explorer, link))
                        {
                            LinkLabel(cell, cells[c], Skin.SmallWrap, LinkText(KindOf(explorer, link), false));
                            if (explorer.ShownByFloor(link)) FloorMark(FloorMarkX(cell, cells[c]), cell);
                            if (cell.Contains(Event.current.mousePosition)) AskTip("table:" + link, FloorTip(explorer, link, PanelWords.GoTo(cells[c])));
                            if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) Go(explorer, link);
                        }
                        // Counted under the strict rules (a biome's weathers): how often it was seen, or ???.
                        else if (c > 0 && CellShown(explorer, row, l, c, cells[c]) is string counted && !ReferenceEquals(counted, cells[c]))
                        {
                            GUI.Label(cell, counted, Skin.SmallWrap);
                            if (counted == CompendiumWords.Hidden) notCounted = true;
                        }
                        // A number kept back reads ??? and says on hover what shows it.
                        else if (c > 0 && (lineKept || (attackKept && Array.IndexOf(row.AttackCells, c) >= 0)))
                        {
                            GUI.Label(cell, CompendiumWords.Hidden, Skin.SmallWrap);
                            if (cell.Contains(Event.current.mousePosition)) AskTip("kept:" + row.Title + cells[0] + row.Columns[c], lineKept ? KeptTip(explorer, cells[0], lineProof) : CompendiumWords.NotYetTakenTip);
                        }
                        // A cell naming what was not seen together with the page keeps the name back.
                        else if (c > 0 && row.CellLinks.TryGetValue((l, c), out var hidden) && explorer.LeavesOut(hidden))
                        {
                            GUI.Label(cell, CompendiumWords.Hidden, Skin.SmallWrap);
                        }
                        // A later cell naming something in the catalog goes there: an attack's projectile, what it puts on you.
                        else if (c > 0 && row.CellLinks.TryGetValue((l, c), out var to) && Goes(explorer, to))
                        {
                            LinkLabel(cell, cells[c], Skin.SmallWrap, LinkText(KindOf(explorer, to), false));
                            if (explorer.ShownByFloor(to)) FloorMark(FloorMarkX(cell, cells[c]), cell);
                            if (cell.Contains(Event.current.mousePosition)) AskTip("table:" + to, FloorTip(explorer, to, PanelWords.GoTo(cells[c])));
                            if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) Go(explorer, to);
                        }
                        else GUI.Label(cell, Masked(explorer, cells[c]), Skin.SmallWrap);
                        x += ColumnWidths[c] + gap;
                    }
                    if (under != null)
                    {
                        var at = y + height;
                        foreach (var faint in under) if (!explorer.NotMet(faint.Skill)) at = YoursLine(row, faint.Cells, faint.Tip, gap, width, at);
                    }
                    Skin.Fill(new Rect(0f, y + height + underHeight + U(2f), width, U(1f)), Skin.Outline);
                }
                y += height + underHeight + U(6f);
            }
            CountDrawn(PanelPart.FactTable);
            y += U(4f);
            y = HintLine(CompendiumWords.NotYetTaken(attacksKept), "attacks-kept:", row.Title, PanelPart.TableKeptBack, width, y);
            if (notCounted) y = HintLine(explorer.NotYetCounted(row.Tally), explorer.MayRunElsewhere(row.Tally) ? ElsewhereWords.Tip : null, "not-counted:", row.Title, PanelPart.TallyCounted, width, y);
            foreach (var said in explorer.ElsewhereLines(row.Tally, row.TallyOf)) y = FaintLine(said, ElsewhereWords.Tip, "elsewhere:", said, PanelPart.RanElsewhere, width, y);
            foreach (var (said, elsewhere) in KeptLines) y = HintLine(said, elsewhere ? ElsewhereWords.Tip : null, "line-kept:", said, PanelPart.TableKeptBack, width, y);
            return y;
        }

        /// <summary>How far a faint line at the character's skill sits in under its table line.</summary>
        private static float YoursIndent => U(10f);

        /// <summary>How tall a faint line at the character's skill is, its cells wrapping in the table's columns.</summary>
        private static float YoursHeight(string[] cells)
        {
            var height = U(18f);
            for (var c = 0; c < cells.Length && c < ColumnWidths.Count; c++) height = Mathf.Max(height, Skin.Height(Skin.SmallWrap, cells[c], ColumnWidths[c] - (c == 0 ? YoursIndent : 0f)));
            return height;
        }

        /// <summary>A pair's faint line at the character's skill, as the page shows it: none where the compendium shows no such skill, one not learned.</summary>
        private static bool YoursShown(Explorer explorer, Facts facts, string label, out string yours) =>
            facts.Yours.TryGetValue(label, out yours) && !(facts.YoursSkills.TryGetValue(label, out var skill) && explorer.NotMet(skill));

        /// <summary>
        /// A faint line under a table's line: the same figures at the character's own skill
        /// (<see cref="Facts.Row.Yours"/>), in the table's columns, its first cell set in; what it
        /// leaves out on hover.
        /// </summary>
        private static float YoursLine(Facts.Row row, string[] cells, string tip, float gap, float width, float y)
        {
            var height = YoursHeight(cells);
            var x = 0f;
            for (var c = 0; c < cells.Length && c < ColumnWidths.Count; c++)
            {
                var inset = c == 0 ? YoursIndent : 0f;
                Skin.LabelIn(new Rect(x + inset, y, ColumnWidths[c] - inset, height), cells[c], Skin.SmallWrap, Skin.Faint);
                x += ColumnWidths[c] + gap;
            }
            if (tip != null && new Rect(0f, y, width, height).Contains(Event.current.mousePosition)) AskTip("yours:" + row.Title + cells[0], tip);
            CountDrawn(PanelPart.YourSkillLine);
            return y + height;
        }

        /// <summary>A table's cell as the page shows it: under the strict rules, a counted one's count in place of the game's share (<see cref="Facts.Row.Tally"/>); else the cell itself.</summary>
        private static string CellShown(Explorer explorer, Facts.Row row, int line, int cell, string text) =>
            row.Tally != Tally.None && cell == row.TallyColumn && line < row.LineItems.Count
                ? explorer.StrictAmount(row.Tally, row.TallyOf, row.LineItems[line]) ?? text
                : text;

        /// <summary>Whether a table's line is left out, what it names not seen together with the page (an attack never seen).</summary>
        private static bool LineLeftOut(Explorer explorer, string link) => !string.IsNullOrEmpty(link) && explorer.LeavesOut(link);

        /// <summary>Whether a table's line keeps all its numbers back, as one only something seen done tells (health by stars).</summary>
        private static bool LineKept(Explorer explorer, Facts.Row row, int line, out string proof) => row.LineProofs.TryGetValue(line, out proof) && explorer.KeepsBack(proof);

        /// <summary>Whether a table's line is an attack that never hit the character, its damage kept back.</summary>
        private static bool AttackKept(Explorer explorer, Facts.Row row, int line) => row.AttackCells != null && explorer.HidesAttack(row.Lines[line].Link);

        /// <summary>
        /// What a cell of a line drawn shows, in the order the table tries them: its name, the
        /// strict count, ??? for a number or name kept back, a link's name, else its text with
        /// names not met kept back; none past the line's cells.
        /// </summary>
        private static string CellDrawn(Explorer explorer, Facts.Row row, int line, int cell)
        {
            var cells = row.Lines[line].Cells;
            if (cell >= cells.Length) return null;
            var text = cells[cell];
            if (cell == 0) return string.IsNullOrEmpty(row.Lines[line].Link) ? Masked(explorer, text) : text;
            var counted = CellShown(explorer, row, line, cell, text);
            if (!ReferenceEquals(counted, text)) return counted;
            if (LineKept(explorer, row, line, out _) || (AttackKept(explorer, row, line) && Array.IndexOf(row.AttackCells, cell) >= 0)) return CompendiumWords.Hidden;
            if (row.CellLinks.TryGetValue((line, cell), out var to)) return explorer.LeavesOut(to) ? CompendiumWords.Hidden : Goes(explorer, to) ? text : Masked(explorer, text);
            return Masked(explorer, text);
        }

        /// <summary>What a table's cell goes to: its line's link for the first, its own for a later one; none for none.</summary>
        private static string CellLink(Facts.Row row, int line, int cell) =>
            cell == 0 ? row.Lines[line].Link : row.CellLinks.TryGetValue((line, cell), out var to) ? to : null;

        /// <summary>Where the floor's mark goes after a cell's link: right after its text, within the cell.</summary>
        private static float FloorMarkX(Rect cell, string text) => cell.x + Mathf.Min(Skin.Width(Skin.Small, text) + U(4f), cell.width - FloorRoom);

        /// <summary>What shows each line of the table being drawn whose numbers are kept back, for its faint lines.</summary>
        private static readonly List<(string Said, bool Elsewhere)> KeptLines = new List<(string, bool)>();

        /// <summary>The tip of a number kept back in a table's line: what shows it, and why where another player's game may run that.</summary>
        private static string KeptTip(Explorer explorer, string what, string proof)
        {
            var shows = explorer.ShowsOnce(what, proof);
            return explorer.MayRunElsewhere(proof) ? Naming.Lines(shows, ElsewhereWords.Tip) : shows;
        }

        /// <summary>A fact that qualifies what stands above it, told under it in a soft line.</summary>
        private static float FactNote(Explorer explorer, Facts facts, KeyValuePair<string, string> pair, float width, float y)
        {
            // A note goes through what a fact goes through: counted over every character's play, naming
            // what the rules leave out, or telling what is still to find out, it is left out or hinted at.
            if (explorer.LeavingOut && facts.AllPlay.Contains(pair.Key)) return y;
            if (facts.Links.TryGetValue(pair.Key, out var linked) && explorer.LeavesOut(linked))
            {
                StrictLeft(1);
                return y;
            }
            if (facts.Proofs.TryGetValue(pair.Key, out var proof) && explorer.KeepsBack(proof))
            {
                return HintLine(explorer.ShowsOnce(pair.Key, proof), explorer.MayRunElsewhere(proof) ? ElsewhereWords.Tip : null, "note-kept:", pair.Key, PanelPart.FactKeptBack, width, y);
            }
            var unsure = facts.Unsure.TryGetValue(pair.Key, out var why);
            var text = Masked(explorer, FactWords.Note(unsure ? UnsureWords.Marked(pair.Key) : pair.Key, pair.Value));
            var height = Skin.Height(Skin.DimWrap, text, width);
            var line = new Rect(0f, y, width, height);
            GUI.Label(line, text, Skin.DimWrap);
            if (unsure && line.Contains(Event.current.mousePosition)) AskTip("unsure:" + pair.Key, why);
            return y + height + U(6f);
        }
    }
}
