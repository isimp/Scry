using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>What the game knows of the entry, the command to spawn it, and its details.</summary>
    internal static partial class ScryPanel
    {
        private static float FactsSection(Explorer explorer, Entry entry, float width, float y)
        {
            var built = Timing.Start();
            var facts = Facts.For(entry);
            Timing.Add("facts built", built);
            var places = !EntryKeys.HasOwnNamespace(entry.Kind) && (Locations.Now != Locations.State.Read || entry.FoundIn.Length > 0);
            if (facts.IsEmpty && !places && entry.Biomes.Length == 0 && Users(explorer, entry).Count == 0) return y;

            y = SectionHeading("IN THE GAME", width, y, null, "facts");
            if (IsFolded("facts")) return y;

            if (facts.Description.Length > 0)
            {
                var height = Skin.Height(Skin.Wrap, facts.Description, width);
                GUI.Label(new Rect(0f, y, width, height), facts.Description, Skin.Wrap);
                y += height + U(10f);
            }

            // A two-column table: what it is on the left, its value on the right.
            var labelW = Mathf.Min(U(130f), width * 0.36f);

            // A kind laid out by topic (FactLayout) shows its facts topic by topic, each under a
            // small heading, its headline numbers as tiles and what qualifies a row under it;
            // the others keep their facts, rows and blocks in the order they always had.
            var plan = PlanOf(explorer, entry, facts, places);
            if (plan == null)
            {
                foreach (var pair in facts.Pairs) y = FactPair(explorer, entry, facts, pair, width, labelW, y);
                foreach (var row in facts.Rows) y = FactRow(explorer, row, width, y);
                foreach (var block in EveryBlock) y = FactBlockOf(explorer, entry, facts, block, places, true, width, y);
                return y + U(14f);
            }

            return Topics(explorer, entry, facts, plan, places, width, y) + U(14f);
        }

        /// <summary>One labelled fact: its label in the left column, its value at its right, a link, music or a website where it is one.</summary>
        private static float FactPair(Explorer explorer, Entry entry, Facts facts, KeyValuePair<string, string> pair, float width, float labelW, float y)
        {
            var valueW = width - labelW - U(10f);
            // A line Scry is not sure of is marked, softer, and says why on hover.
            var unsure = facts.Unsure.TryGetValue(pair.Key, out var why);
            var label = unsure ? UnsureWords.Marked(pair.Key) : pair.Key;
            var labelH = Skin.Height(Skin.DimWrap, label, labelW);
            var height = Mathf.Max(U(20f), Mathf.Max(labelH, Skin.Height(Skin.Wrap, pair.Value, valueW)));
            GUI.Label(new Rect(0f, y, labelW, labelH), label, Skin.DimWrap);
            var valueRect = new Rect(labelW + U(10f), y, valueW, height);
            if (unsure && new Rect(0f, y, width, height).Contains(Event.current.mousePosition)) AskTip("unsure:" + pair.Key, why);
            // Music plays where it is named: the entry's own, or one by its name (a biome has one for each time of day).
            if (facts.Links.TryGetValue(pair.Key, out var music) && EntryKeys.PlaysMusic(music, out var named))
            {
                var playing = MusicPreview.PlayingFor == entry && (named == null || MusicPreview.Playing == named);
                var musicW = Mathf.Min(valueW, Skin.Width(Skin.Wrap, pair.Value) + U(4f));
                var musicRect = new Rect(valueRect.x, valueRect.y, musicW, height);
                LinkLabel(musicRect, pair.Value, Skin.Wrap, playing ? Skin.KindColor(Kind.Sound) : Skin.Accent);
                if (musicRect.Contains(Event.current.mousePosition)) AskTip("music:" + entry.Key + ":" + named, MusicWords.Tip(playing));
                if (GUI.Button(musicRect, GUIContent.none, GUIStyle.none))
                {
                    var said = named == null ? Previews.PlacesMusic(entry) : Previews.NamedMusic(entry, named);
                    if (said != null) Say(said);
                }
            }
            // A mod's website opens in the browser.
            else if (facts.Links.TryGetValue(pair.Key, out var web) && EntryKeys.WebsiteOf(web) is string url)
            {
                var webW = Mathf.Min(valueW, Skin.Width(Skin.Wrap, pair.Value) + U(4f));
                var webRect = new Rect(valueRect.x, valueRect.y, webW, height);
                LinkLabel(webRect, pair.Value, Skin.Wrap, Skin.Accent);
                if (webRect.Contains(Event.current.mousePosition)) AskTip("web:" + entry.Key, "Open it in your browser");
                if (GUI.Button(webRect, GUIContent.none, GUIStyle.none)) Application.OpenURL(url);
            }
            // A link only to what is in the catalog: a creature's own attack items are not.
            else if (facts.Links.TryGetValue(pair.Key, out var link) && InCatalog(explorer, link))
            {
                var linkW = Mathf.Min(valueW, Skin.Width(Skin.Wrap, pair.Value) + U(4f));
                var linkRect = new Rect(valueRect.x, valueRect.y, linkW, height);
                LinkLabel(linkRect, pair.Value, Skin.Wrap, LinkText(KindOf(explorer, link), false));
                if (linkRect.Contains(Event.current.mousePosition)) AskTip("link:" + link, PanelWords.GoTo(ShownName(explorer, link, pair.Value)));
                if (GUI.Button(linkRect, GUIContent.none, GUIStyle.none)) Go(explorer, link);
            }
            else
            {
                GUI.Label(valueRect, pair.Value, unsure ? Skin.DimWrap : Skin.Wrap);
            }
            return y + height + U(6f);
        }

        /// <summary>What a page tells besides its labelled facts and rows, each where it has any; under a topic's heading, without a title of its own.</summary>
        private static float FactBlockOf(Explorer explorer, Entry entry, Facts facts, FactBlock block, bool places, bool titled, float width, float y)
        {
            switch (block)
            {
                case FactBlock.Where:
                    return facts.Where.Count > 0 ? WhereBlock(explorer, facts, titled, width, y) : y;
                case FactBlock.FoundIn:
                    return places ? FoundIn(explorer, entry, width, y + U(6f)) : y;
                case FactBlock.Biomes:
                    return BiomesBlock(explorer, entry, width, y);
                case FactBlock.Users:
                    var users = Users(explorer, entry);
                    return users.Count > 0 ? LinkItems(explorer, _usersTitle, users, width, y) : y;
                case FactBlock.Uses:
                    return UsesBlock(explorer, facts, titled, width, y);
                default:
                    return HooksBlock(entry, facts, width, y);
            }
        }

        /// <summary>Where it lives, comes from, or what gives it: a line naming a prefab in the catalog a chip that goes there.</summary>
        private static float WhereBlock(Explorer explorer, Facts facts, bool titled, float width, float y)
        {
            if (titled)
            {
                y += U(6f);
                GUI.Label(new Rect(0f, y, width, U(20f)), facts.WhereTitle, Skin.DimLabel);
                y += U(24f);
            }
            const int firstLines = 8;
            var lines = ShownOf("where", facts.Where.Count, firstLines);
            foreach (var source in facts.Where.Take(lines))
            {
                // A line naming a prefab in the catalog is a chip that goes there; the rest is text.
                // A line Scry is not sure of is marked and says why on hover.
                var text = source.Unsure != null ? UnsureWords.Marked(source.Text) : source.Text;
                if (string.IsNullOrEmpty(source.Prefab) || !InCatalog(explorer, source.Prefab))
                {
                    var style = source.Unsure != null ? Skin.DimWrap : Skin.Wrap;
                    var height = Skin.Height(style, text, width);
                    var line = new Rect(0f, y, width, height);
                    GUI.Label(line, text, style);
                    if (source.Unsure != null && line.Contains(Event.current.mousePosition)) AskTip("unsure:" + source.Text, source.Unsure);
                    y += height + U(4f);
                    continue;
                }

                var icon = PrefabIcon(source.Prefab);
                var textX = icon != null ? U(34f) : U(12f);
                var textW = width - textX - U(10f);
                var wrapped = Skin.SmallWrap;
                var chipH = Mathf.Max(U(30f), Skin.Height(wrapped, text, textW) + U(10f));
                var chip = new Rect(0f, y, width, chipH);
                var hover = chip.Contains(Event.current.mousePosition);
                var kind = KindOf(explorer, source.Prefab);
                Skin.Box(chip, LinkFill(kind, hover));
                if (icon != null) DrawSprite(icon, new Rect(U(6f), y + (chipH - U(22f)) / 2f, U(22f), U(22f)));
                Skin.LabelIn(new Rect(textX, y, textW, chipH), text, wrapped, LinkText(kind, hover));
                if (hover) AskTip("src:" + source.Prefab + source.Unsure, Naming.Lines(source.Unsure, PanelWords.GoTo(ShownName(explorer, source.Prefab, source.Prefab))));
                if (GUI.Button(chip, GUIContent.none, GUIStyle.none) && explorer.Jump(source.Prefab))
                {
                    AfterGoing();
                }
                y += chipH + U(5f);
            }
            var more = new ChipFlow(0f, width, y, U(26f), U(5f), U(5f));
            MoreChip("where", facts.Where.Count, firstLines, width, ref more);
            if (more.InRow) y = more.RowBottom + U(5f);
            return y;
        }

        /// <summary>Its biomes as chips.</summary>
        private static float BiomesBlock(Explorer explorer, Entry entry, float width, float y)
        {
            // Its biomes, each going to its page (which can search for everything there), else
            // searching; a biome's own page names none.
            if (entry.Biomes.Length > 0 && entry.Kind != Kind.Biome)
            {
                y = ChipRow("Biomes", entry.Biomes.Select(b => new KeyValuePair<string, Action>(Knowledge.BiomeName(b), () =>
                {
                    var page = EntryKeys.For(Kind.Biome, b);
                    if (InCatalog(explorer, page)) Go(explorer, page);
                    else SearchFor(explorer, SearchHelp.Term("biome", b));
                })), width, y);
            }
            return y;
        }

        /// <summary>What it is used for, a row for each kind of use and place.</summary>
        private static float UsesBlock(Explorer explorer, Facts facts, bool titled, float width, float y)
        {
            // What it is used for, under a heading of its own; a long row (wood builds a hundred
            // pieces) shows its first few until asked for the rest.
            if (facts.UseRows.Count > 0)
            {
                if (titled)
                {
                    y += U(6f);
                    GUI.Label(new Rect(0f, y, width, U(20f)), "What it is used for", Skin.DimLabel);
                    y += U(22f);
                }
                foreach (var row in facts.UseRows) y = FactRow(explorer, row, width, y);
            }
            return y;
        }

        /// <summary>The mods hooking into what the page tells, in one soft line.</summary>
        private static float HooksBlock(Entry entry, Facts facts, float width, float y)
        {
            // Mods hooking into what the page tells, in one soft line after the rest; who they
            // are and what each may change on hover.
            var hooks = ModHookWords.Line(facts.Hooks);
            if (hooks != null)
            {
                y += U(6f);
                var text = UnsureWords.Marked(hooks);
                var hooksRect = new Rect(0f, y, width, Skin.Height(Skin.DimWrap, text, width));
                GUI.Label(hooksRect, text, Skin.DimWrap);
                if (hooksRect.Contains(Event.current.mousePosition)) AskTip("hooks:" + entry.Key, Naming.Lines(ModHookWords.Tip(facts.Hooks), UnsureWords.Hooked));
                y = hooksRect.yMax;
            }

            return y;
        }

        /// <summary>The cells' colours: the plain share faint, resisting green, weak red, taking nothing blue, a quiet one faint.</summary>
        private static Color ToneColor(Tone tone)
        {
            switch (tone)
            {
                case Tone.Resists: return Skin.Resisting;
                case Tone.Weak: return Skin.Weak;
                case Tone.Immune: return Skin.Immune;
                default: return Skin.Faint;
            }
        }

        /// <summary>
        /// A grid of every damage type: up to five cells a row, fewer where the side is narrow so
        /// none is cut, each its type over the share taken, coloured by how it lands, with the
        /// game's word on hover.
        /// </summary>
        private static float GridRow(Facts.Row row, float width, float y)
        {
            y += U(6f);
            var titleRect = new Rect(0f, y, width, U(20f));
            GUI.Label(titleRect, row.Unsure != null ? UnsureWords.Marked(row.Title) : row.Title, Skin.DimLabel);
            if (row.Unsure != null && titleRect.Contains(Event.current.mousePosition)) AskTip("unsure:" + row.Title, row.Unsure);
            y += U(24f);

            var gap = U(4f);
            var columns = Mathf.Clamp(Mathf.FloorToInt((width + gap) / (U(62f) + gap)), 2, 5);
            var cellW = (width - gap * (columns - 1)) / columns;
            var cellH = U(38f);
            for (var i = 0; i < row.Cells.Count; i++)
            {
                var cell = row.Cells[i];
                var at = new Rect((i % columns) * (cellW + gap), y + (i / columns) * (cellH + gap), cellW, cellH);
                if (OutOfSight(at)) continue;
                // A quiet cell (tools' damage a creature takes none of) keeps its place but stands back.
                Skin.Box(at, cell.Tone == Tone.Quiet ? Skin.Panel : Skin.Raised);
                GUI.Label(new Rect(at.x + U(2f), at.y + U(3f), at.width - U(4f), U(16f)), cell.Type, Skin.CellType);
                Skin.LabelIn(new Rect(at.x + U(2f), at.y, at.width - U(4f), at.height - U(4f)), cell.Value, Skin.CellValue, ToneColor(cell.Tone));
                if (at.Contains(Event.current.mousePosition)) AskTip("cell:" + row.Title + cell.Type, cell.Tip);
            }
            CountDrawn(PanelPart.ResistanceGrid);
            var lines = (row.Cells.Count + columns - 1) / columns;
            return y + lines * (cellH + gap) + U(4f);
        }

        /// <summary>
        /// A titled row of chips, each an item with its amount that goes to it when clicked; the
        /// title goes to the station it names. A long row shows its first chips and one for the rest.
        /// </summary>
        private static float FactRow(Explorer explorer, Facts.Row row, float width, float y)
        {
            if (row.Cells != null) return GridRow(row, width, y);
            if (row.Columns != null) return TableRow(explorer, row, width, y);
            if (row.Chain != null) return ChainRow(explorer, row, width, y);
            var key = "facts:" + row.Title;
            var count = ShownOf(key, row.Items.Count);
            y += U(6f);
            // A row Scry is not sure of is marked and says why on hover.
            if (row.Unsure != null)
            {
                var unsureRect = new Rect(0f, y, width, U(20f));
                GUI.Label(unsureRect, UnsureWords.Marked(row.Title), Skin.DimLabel);
                if (unsureRect.Contains(Event.current.mousePosition)) AskTip("unsure:" + row.Title, row.Unsure);
            }
            else if (!string.IsNullOrEmpty(row.TitleLink) && InCatalog(explorer, row.TitleLink))
            {
                var titleW = Mathf.Min(width, Skin.Width(Skin.DimLabel, row.Title) + U(4f));
                var titleRect = new Rect(0f, y, titleW, U(20f));
                LinkLabel(titleRect, row.Title, Skin.DimLabel, LinkText(KindOf(explorer, row.TitleLink), false));
                if (titleRect.Contains(Event.current.mousePosition)) AskTip("row-title:" + row.TitleLink, PanelWords.GoTo(ShownName(explorer, row.TitleLink, row.TitleLink)));
                if (GUI.Button(titleRect, GUIContent.none, GUIStyle.none)) Go(explorer, row.TitleLink);
            }
            else
            {
                GUI.Label(new Rect(0f, y, width, U(20f)), row.Title, Skin.DimLabel);
            }
            y += U(24f);

            var flow = new ChipFlow(0f, width, y, U(30f), U(6f), U(5f));
            for (var i = 0; i < count; i++)
            {
                var item = row.Items[i];
                var text = DetailWords.Amounted(item.Amount, item.Name);
                // A mark (only here, what a trader pays) in a pill of its own at the chip's end.
                var markW = item.Mark != null ? Skin.Width(Skin.Badge, item.Mark) + U(16f) : 0f;
                var w = Mathf.Min(width, Skin.Width(Skin.Chip, text) + U(30f) + markW);
                var at = flow.Place(w);
                var chip = new Rect(at.X, at.Y, w, flow.RowHeight);
                if (OutOfSight(chip)) continue;
                var hover = chip.Contains(Event.current.mousePosition);
                // A chip that runs a search, such as for what to bring against a creature.
                if (EntryKeys.SearchOf(item.Prefab) is string query)
                {
                    Skin.PillBox(chip, hover ? Skin.RaisedHover : Skin.Raised);
                    Skin.LabelIn(new Rect(chip.x + U(10f), chip.y, chip.width - U(14f), chip.height), text, Skin.Small, Skin.Accent);
                    if (hover) AskTip("search:" + query, PanelWords.TryInSearch(query));
                    if (GUI.Button(chip, GUIContent.none, GUIStyle.none)) SearchFor(explorer, query);
                    continue;
                }
                var goes = !string.IsNullOrEmpty(item.Prefab) && InCatalog(explorer, item.Prefab);
                var kind = goes ? KindOf(explorer, item.Prefab) : null;
                Skin.PillBox(chip, goes ? LinkFill(kind, hover) : Skin.Raised);
                if (item.Icon != null) DrawSprite(item.Icon, new Rect(chip.x + U(6f), chip.y + U(4f), U(22f), U(22f)));
                Skin.LabelIn(new Rect(chip.x + U(32f), chip.y, chip.width - U(36f) - markW, chip.height), text, Skin.Small, goes ? LinkText(kind, hover) : Skin.Text);
                if (item.Mark != null)
                {
                    // A thin accent outline round it, the chip showing through, its words in the accent.
                    var mark = new Rect(chip.xMax - markW - U(4f), chip.y + U(5f), markW, chip.height - U(10f));
                    Skin.PillLine(mark, Skin.Accent);
                    Skin.LabelIn(mark, item.Mark, Skin.Badge, Skin.Accent);
                }

                // Clicking an ingredient or a drop goes to it.
                if (!string.IsNullOrEmpty(item.Prefab))
                {
                    if (hover && goes) AskTip("goto:" + item.Prefab + item.Mark, item.MarkTip != null ? Naming.Lines(item.MarkTip, PanelWords.GoTo(item.Name)) : PanelWords.GoTo(item.Name));
                    if (GUI.Button(chip, GUIContent.none, GUIStyle.none) && explorer.Jump(item.Prefab))
                    {
                        AfterGoing();
                    }
                }
            }
            MoreChip(key, row.Items.Count, FirstChips, width, ref flow);
            return flow.RowBottom + U(6f);
        }

        /// <summary>A chain's steps, an arrow between each, the page's own lit and each other one going to its page.</summary>
        private static float ChainRow(Explorer explorer, Facts.Row row, float width, float y)
        {
            y += U(6f);
            GUI.Label(new Rect(0f, y, width, U(20f)), row.Title, Skin.DimLabel);
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
                    var name = ShownName(explorer, key, key);
                    var here = shown != null && (shown.Name == key || shown.Key == key);
                    var w = Mathf.Min(width, LinkChipWidth(name, true));
                    var at = flow.Place(w);
                    var chip = new Rect(at.X, at.Y, w, flow.RowHeight);
                    if (OutOfSight(chip)) continue;
                    if (here)
                    {
                        GUI.Label(chip, name, Skin.ChipOn);
                        continue;
                    }
                    var goes = InCatalog(explorer, key);
                    if (LinkChip(chip, name, goes ? KindOf(explorer, key) : null, false, goes) && goes) Go(explorer, key);
                    if (goes && chip.Contains(Event.current.mousePosition)) AskTip("chain:" + key, PanelWords.GoTo(name));
                }
            }
            return flow.RowBottom + U(6f);
        }

        private static int _commandAmount = 1;
        private static int _commandQuality = 1;
        private static Entry _commandFor;

        /// <summary>
        /// The game's own spawn command for the selection, ready to paste into the console: a
        /// creature at the level set above, an item given straight into the inventory.
        /// </summary>
        private static float Command(Explorer explorer, Entry entry, float width, float y)
        {
            if (entry != _commandFor)
            {
                _commandFor = entry;
                _commandAmount = 1;
                _commandQuality = 1;
            }

            var isItem = entry.Kind == Kind.Item;
            var level = isItem ? _commandQuality : entry.Kind == Kind.Creature ? explorer.Modifiers.Level : 1;
            var command = SpawnCommand.For(entry, _commandAmount, level, give: isItem);
            if (command == null) return y;

            y = SectionHeading(DetailWords.CommandHeading(isItem), width, y, null, "command");
            if (IsFolded("command")) return y;
            var rowH = U(28f);

            if (isItem)
            {
                GUI.Label(new Rect(0f, y, U(80f), rowH), "Amount", Skin.DimLabel);
                var x = U(80f);
                foreach (var step in new[] { -10, -1, 1, 10 })
                {
                    var text = Numbers.Count(step, signed: true);
                    if (step == 1)
                    {
                        GUI.Label(new Rect(x, y, U(48f), rowH), Numbers.Count(_commandAmount), Skin.Center);
                        x += U(52f);
                    }
                    if (GUI.Button(new Rect(x, y, U(40f), rowH), text, Skin.Segment)) _commandAmount = Mathf.Clamp(_commandAmount + step, 1, 999);
                    x += U(44f);
                }
                y += rowH + U(8f);

                var drop = (entry.Source as GameObject).OrNull()?.GetComponent<ItemDrop>();
                var maxQuality = Mathf.Min(SpawnCommand.MaxItemQuality, drop.OrNull()?.m_itemData?.m_shared?.m_maxQuality ?? 1);
                if (maxQuality > 1)
                {
                    var names = Enumerable.Range(1, maxQuality).Select(q => Numbers.Count(q)).ToList();
                    var chosen = Segments("Quality", names, _commandQuality - 1, width, U(80f), ref y);
                    if (chosen >= 0) _commandQuality = chosen + 1;
                }
            }

            var copyW = U(70f);
            var box = new Rect(0f, y, width - copyW - U(8f), U(30f));
            Skin.Box(box, Skin.Sunken, Skin.Outline);
            var commandRect = new Rect(box.x + U(10f), box.y, box.width - U(14f), box.height);
            if (!FitLabel(commandRect, command, Skin.Label, 10f) && commandRect.Contains(Event.current.mousePosition)) AskTip("command", command);
            if (GUI.Button(new Rect(box.xMax + U(8f), y, copyW, U(30f)), "Copy", Skin.Button))
            {
                GUIUtility.systemCopyBuffer = command;
                Say(DetailWords.CopiedCommand(command));
            }
            y += U(36f);

            const string note = "Runs in the console with devcommands on, which the game only allows the host or a single-player world.";
            var height = Skin.Height(Skin.DimWrap, note, width);
            GUI.Label(new Rect(0f, y, width, height), note, Skin.DimWrap);
            return y + height + U(14f);
        }

        private static readonly Dictionary<Entry, string> ComponentLists = new Dictionary<Entry, string>();
        private static Entry _usersFor;
        private static List<(string Key, string Text, string Tip, Action Click)> _users = new List<(string, string, string, Action)>();
        private static string _usersTitle = "";

        /// <summary>
        /// What plays it, where nothing else tells (<see cref="EffectLinks"/>): every one, the
        /// first few until asked for the rest, as every long row shows; found once for the entry
        /// shown, not for every event drawn.
        /// </summary>
        private static List<(string Key, string Text, string Tip, Action Click)> Users(Explorer explorer, Entry entry)
        {
            if (ReferenceEquals(_usersFor, entry)) return _users;
            _usersFor = entry;
            _users = entry.UsedBy.Count == 0 || EffectLinks.For(entry.Name).Count > 0
                ? new List<(string, string, string, Action)>()
                : entry.UsedBy.Where(u => InCatalog(explorer, u)).Select(n => (n, ShownName(explorer, n, n), (string)null, (Action)(() => Go(explorer, n)))).ToList();
            _usersTitle = Naming.Counted("Played by", _users.Count);
            return _users;
        }

        private static float Details(Explorer explorer, Entry entry, float width, float y)
        {
            y = SectionHeading("DETAILS", width, y, null, "details");
            if (IsFolded("details")) return y;

            var lines = new List<string> { DetailWords.PrefabName(entry.Name), DetailWords.OriginLine(entry) };

            if (entry.ExtraLevels > 0) lines.Add(DetailWords.StarLooks(entry.ExtraLevels));

            if (entry.Source is GameObject prefab)
            {
                if (!ComponentLists.TryGetValue(entry, out var components))
                {
                    components = Components(prefab);
                    ComponentLists[entry] = components;
                }
                lines.Add(DetailWords.MadeOf(components));
            }

            foreach (var line in lines)
            {
                var height = Skin.Height(Skin.DimWrap, line, width);
                GUI.Label(new Rect(0f, y, width, height), line, Skin.DimWrap);
                y += height + U(4f);
            }

            // For finding out why part of a model does not show: every part the preview draws, in the
            // log. Offered only with the preview notes on, as it is there for looking into previews.
            if (Stage.Subject != null && Settings.LogPreviews)
            {
                y += U(4f);
                var text = "Write its parts to the log";
                var w = Skin.Width(Skin.Chip, text) + U(8f);
                if (GUI.Button(new Rect(0f, y, Mathf.Min(width, w), U(26f)), text, Skin.Chip)) Say(Stage.Dump());
                y += U(32f);
            }

            return y + U(6f);
        }

        /// <summary>
        /// Where it is found in the world's locations and dungeons: a button that reads them (they
        /// are read only when asked, since loading them takes a while), how far the reading has got,
        /// then the places, each going to its location.
        /// </summary>
        private static float FoundIn(Explorer explorer, Entry entry, float width, float y)
        {
            switch (Locations.Now)
            {
                case Locations.State.Read:
                    if (entry.FoundIn.Length == 0) return y;
                    return LinkItems(explorer, "Found in", FoundInLinks(explorer, entry), width, y);

                case Locations.State.Reading:
                    // Not measured: the text changes every frame, and each would be kept.
                    const string stop = "Stop";
                    var stopW = Skin.Width(Skin.Chip, stop) + U(12f);
                    GUI.Label(new Rect(0f, y, width - stopW - U(8f), U(24f)), LocationWords.ReadingProgress(Locations.Done, Locations.Total), Skin.DimLabel);
                    if (GUI.Button(new Rect(width - stopW, y, stopW, U(24f)), stop, Skin.Chip)) StopReadingLocations();
                    return y + U(30f);

                default:
                    var w = Skin.Width(Skin.Chip, LocationsButtonText) + U(8f);
                    if (GUI.Button(new Rect(0f, y, Mathf.Min(width, w), U(26f)), LocationsButtonText, Skin.Chip)) StartReadingLocations();
                    y += U(30f);
                    const string note = "Reads each of this world's locations and dungeon rooms once, in the background, over a few minutes.";
                    var height = Skin.Height(Skin.DimWrap, note, width);
                    GUI.Label(new Rect(0f, y, width, height), note, Skin.DimWrap);
                    return y + height + U(6f);
            }
        }

        /// <summary>
        /// The places an entry is found in, each going to the location of that name
        /// (<see cref="Places.LocationNamed"/>), or, where no location goes by it, searching for
        /// what is found there. Made once for the entry shown and the places it was read to be in.
        /// </summary>
        private static List<(string Key, string Text, string Tip, Action Click)> FoundInLinks(Explorer explorer, Entry entry)
        {
            if (ReferenceEquals(_foundInFor, entry) && ReferenceEquals(_foundInOf, entry.FoundIn) && ReferenceEquals(_foundInIn, explorer)) return FoundInItems;
            _foundInFor = entry;
            _foundInOf = entry.FoundIn;
            _foundInIn = explorer;
            FoundInItems.Clear();
            foreach (var place in entry.FoundIn)
            {
                var name = Places.NameOf(place);
                var location = Places.LocationNamed(explorer.Catalog, place);
                if (location != null)
                {
                    var key = location.Key;
                    FoundInItems.Add((key, place, PanelWords.GoTo(name), () => Go(explorer, key)));
                }
                else
                {
                    var search = SearchHelp.Term("in", name);
                    FoundInItems.Add((search, place, LocationWords.SearchFoundIn(name), () => SearchFor(explorer, search)));
                }
            }
            return FoundInItems;
        }

        private static readonly List<(string Key, string Text, string Tip, Action Click)> FoundInItems = new List<(string, string, string, Action)>();
        private static Entry _foundInFor;
        private static string[] _foundInOf;
        private static Explorer _foundInIn;

        private const string LocationsButtonText = "Read all locations";
        private const string LocationsButtonTip = "Reads what every location and dungeon room in this world holds, in the background over a few minutes, so things tell where they are found and in: finds them. A location selected is read by itself.";

        /// <summary>Stops reading the locations, from any of the buttons that offer it, and says so.</summary>
        private static void StopReadingLocations()
        {
            var said = Locations.Stop();
            Say(Naming.Capital(said));
        }

        /// <summary>Starts reading the locations, from any of the buttons that offer it, and says so.</summary>
        private static void StartReadingLocations()
        {
            var said = Locations.Start();
            Say(Naming.Capital(said));
        }

        private static string Components(GameObject prefab)
        {
            var counts = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform) continue;
                var name = component.GetType().Name;
                counts.TryGetValue(name, out var n);
                counts[name] = n + 1;
            }
            return Naming.Commas(counts.Select(p => Naming.Repeated(p.Key, p.Value)));
        }

        /// <summary>Lets go of the details' lists, and of what they were last drawn for.</summary>
        private static void ForgetFacts()
        {
            ComponentLists.Clear();
            _usersFor = null;
            _users = new List<(string, string, string, Action)>();
            _commandFor = null;
            _foundInFor = null;
            _foundInOf = null;
            _foundInIn = null;
            FoundInItems.Clear();
        }
    }
}
