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
                var description = Masked(explorer, facts.Description);
                var height = Skin.Height(Skin.Wrap, description, width);
                GUI.Label(new Rect(0f, y, width, height), description, Skin.Wrap);
                y += height + U(10f);
            }

            // What the character itself did with it opens the facts, in a box of its own.
            var you = Timing.Start();
            y = YouBox(explorer, entry, facts, width, y);
            Timing.Add("facts drawn you", you);

            // A two-column table: what it is on the left, its value on the right.
            var labelW = Mathf.Min(U(130f), width * 0.36f);

            // A kind laid out by topic (FactLayout) shows its facts topic by topic, each under a
            // small heading, its headline numbers as tiles and what qualifies a row under it;
            // the others keep their facts, rows and blocks in the order they always had.
            var step = Timing.Start();
            var plan = PlanOf(explorer, entry, facts, places);
            Timing.Add("side facts plan", step);
            step = Timing.Start();
            if (plan == null)
            {
                // One section, ending as a topic does with what the compendium left out of it.
                StrictSectionStarts();
                var kind = Timing.Start();
                foreach (var pair in facts.Pairs) y = FactPair(explorer, entry, facts, pair, width, labelW, y);
                Timing.Add("facts drawn pairs", kind);
                kind = Timing.Start();
                foreach (var row in facts.Rows) y = FactRow(explorer, row, width, y);
                Timing.Add("facts drawn rows", kind);
                kind = Timing.Start();
                foreach (var block in EveryBlock) y = FactBlockOf(explorer, entry, facts, block, places, true, width, y);
                Timing.Add("facts drawn blocks", kind);
                y = StrictLeftLine(explorer, null, width, y);
                Timing.Add("side facts drawn", step);
                return y + U(14f);
            }

            y = Topics(explorer, entry, facts, plan, places, width, y);
            Timing.Add("side facts drawn", step);
            return y + U(14f);
        }

        private static Entry _youOf;
        private static float _youAt = -1f;
        private static float _youHeight;
        private static List<(string Label, string Value)> _you = new List<(string, string)>();

        /// <summary>The You box's lines as last read, for the self-test.</summary>
        [Diagnostic]
        public static List<(string Label, string Value)> YouLinesForTest => new List<(string, string)>(_you);
        private static List<(string Label, string Value)> _still = new List<(string, string)>();

        /// <summary>
        /// The box a page opens with, titled "You": what the game counted of the thing for the
        /// character (<see cref="CharacterRecords.Counts"/>) and, with the compendium on, how
        /// often it killed the character, when it was first seen and how it came to know it, each
        /// with its moment (<see cref="JournalWords"/>, <see cref="CompendiumWords.CameLine"/>);
        /// under the strict rules, what the page still keeps back, a row for each thing to do
        /// (<see cref="CompendiumWords.StillToFindOut"/>). Read again once a second and on another
        /// page; drawn behind what it holds at the height that held last time.
        /// </summary>
        private static float YouBox(Explorer explorer, Entry entry, Facts facts, float width, float y)
        {
            if (entry != _youOf || Time.unscaledTime - _youAt > 1f)
            {
                _youOf = entry;
                _youAt = Time.unscaledTime;
                string came = null, seen = null, killedYou = null;
                // The book the compendium shows: the character's, or one shown in its place.
                var book = explorer.CompendiumOn ? explorer.Compendium?.Book : null;
                if (book != null)
                {
                    var record = book.Came(entry.Key);
                    var at = record?.At ?? Moment.None;
                    came = CompendiumWords.CameLine(record?.Way, at.Day, BiomeShown(explorer, at.Biome), explorer.Compendium.Because(entry)?.ShownName, explorer.ByFloor(entry),
                        explorer.LevelOf(entry) == KnownLevel.Known, at.Time, at.Weather, InHandShown(explorer, at.InHand), explorer.ByWorld(entry));
                    if (book.FirstSeen(entry.Key) is var first && first != null)
                    {
                        var sight = first.Value.At;
                        seen = JournalWords.FirstSeen(sight.Day, sight.Time, sight.Weather, BiomeShown(explorer, sight.Biome), first.Value.Stars);
                    }
                    if (book.DeathsBy(entry.Key) is var deaths && deaths != null) killedYou = JournalWords.KilledYou(deaths.Value.Count, deaths.Value.LastDay);
                }
                _you = CompendiumWords.You(CharacterRecords.Counts(entry), came, seen, killedYou);
                // A skill's page opens with the character's level and the way to the next.
                if (entry.Source is SkillSource skill) _you.AddRange(YourSkills.Lines(skill));
                _still = StillToFindOut(explorer, facts);
            }
            if (_you.Count == 0 && _still.Count == 0) return y;

            var top = y;
            if (_youHeight > 0f) Skin.Box(new Rect(0f, top, width, _youHeight), Skin.TopicFill);
            var pad = U(10f);
            var inner = width - pad * 2f;
            GUI.Label(new Rect(pad, top + U(6f), inner, U(20f)), CompendiumWords.YouTitle, Skin.TopicName);
            y = top + U(30f);
            var labelW = Mathf.Min(U(130f), inner * 0.36f);
            var valueW = inner - labelW - U(10f);
            foreach (var (label, value) in _you)
            {
                var height = Mathf.Max(U(20f), Skin.Height(Skin.Wrap, value, valueW));
                GUI.Label(new Rect(pad, y, labelW, height), label, Skin.DimWrap);
                var valueRect = new Rect(pad + labelW + U(10f), y, valueW, height);
                GUI.Label(valueRect, value, Skin.Wrap);
                y += height + U(6f);
            }
            // What is still to find out: what to do on the left, what it shows faint on the right.
            foreach (var (label, value) in _still)
            {
                var height = Mathf.Max(U(20f), Skin.Height(Skin.Wrap, value, valueW));
                GUI.Label(new Rect(pad, y, labelW, height), label, Skin.DimWrap);
                var valueRect = new Rect(pad + labelW + U(10f), y, valueW, height);
                Skin.LabelIn(valueRect, value, Skin.Wrap, Skin.Faint);
                y += height + U(6f);
            }
            if (_still.Count > 0) CountDrawn(PanelPart.StillToFindOut);
            y += U(4f);
            if (Event.current.type == EventType.Repaint) _youHeight = y - top;
            CountDrawn(PanelPart.YouBox);
            return y + U(10f);
        }

        /// <summary>A biome kept in a moment by its name, as its page names it; none for none.</summary>
        private static string BiomeShown(Explorer explorer, string biome) =>
            biome == null ? null : explorer.Find(EntryKeys.For(Kind.Biome, biome))?.ShownName ?? BiomeWords.Shown(biome);

        /// <summary>What was in hand in a moment, by its prefab, as its page names it; none bare-handed.</summary>
        private static string InHandShown(Explorer explorer, string prefab) =>
            prefab == null ? null : explorer.Find(prefab)?.ShownName ?? Naming.Readable(prefab);

        /// <summary>
        /// What a page under the strict rules still keeps back, gathered from what it draws: the
        /// facts whose numbers wait for a deed, the weak spots not found, the damage types not told,
        /// the attacks not taken, and the drops or yields not counted; none otherwise.
        /// </summary>
        private static List<(string Label, string Value)> StillToFindOut(Explorer explorer, Facts facts)
        {
            if (!explorer.LeavingOut || facts == null) return NothingStill;
            var deeds = new List<string>();
            var spots = 0;
            foreach (var pair in facts.Pairs)
            {
                if (!facts.Proofs.TryGetValue(pair.Key, out var proof) || !explorer.KeepsBack(proof)) continue;
                if (EntryKeys.IsWeakSpot(proof)) spots++;
                else deeds.Add(proof);
            }
            var types = new List<string>();
            var attacks = 0;
            var uncounted = new List<Tally>();
            foreach (var row in facts.Rows)
            {
                if (row.HitReveals && row.Cells != null)
                {
                    foreach (var cell in row.Cells) if (cell.Tone != Tone.Quiet && explorer.HidesDamage(cell.Type)) types.Add(cell.Type);
                }
                foreach (var proof in row.LineProofs.Values) if (explorer.KeepsBack(proof)) deeds.Add(proof);
                if (row.AttackCells != null) foreach (var (_, link) in row.Lines) if (explorer.HidesAttack(link)) attacks++;
                if (row.Tally != Tally.None && (row.Items.Any(i => !explorer.LeavesOut(i.Prefab) && explorer.StrictAmount(row.Tally, row.TallyOf, i.Prefab) == CompendiumWords.Hidden)
                    || row.LineItems.Any(i => explorer.StrictAmount(row.Tally, row.TallyOf, i) == CompendiumWords.Hidden))) uncounted.Add(row.Tally);
            }
            return CompendiumWords.StillToFindOut(deeds, types, attacks, spots, uncounted);
        }

        private static readonly List<(string Label, string Value)> NothingStill = new List<(string, string)>();

        /// <summary>What showed the fact drawn just above, where it was kept back: the next one kept back by the same says it only on hover.</summary>
        private static string _heldAbove;

        /// <summary>A section begins: no fact kept back stands above.</summary>
        private static void HeldAboveForgotten() => _heldAbove = null;

        /// <summary>Lets go of the page the You box was read for, for another world.</summary>
        private static void ForgetYou()
        {
            _youOf = null;
            _you.Clear();
            _still = NothingStill;
            _youHeight = 0f;
        }

        /// <summary>One labelled fact: its label in the left column, its value at its right, a link, music or a website where it is one.</summary>
        private static float FactPair(Explorer explorer, Entry entry, Facts facts, KeyValuePair<string, string> pair, float width, float labelW, float y)
        {
            // Counted over every character's play: under the strict rules the page counts this character's own.
            if (explorer.LeavingOut && facts.AllPlay.Contains(pair.Key)) return y;
            // A line only naming what the page lists waits until the character has seen it; under
            // the strict rules a line naming what was not seen together with the page is left out.
            if (facts.Links.TryGetValue(pair.Key, out var linked))
            {
                if (facts.Listing.Contains(pair.Key) && LeftOut(explorer, linked)) return y;
                if (explorer.LeavesOut(linked))
                {
                    StrictLeft(1);
                    return y;
                }
            }
            // Under the strict rules the names of what was not seen are kept back in its text, and
            // a number the game never shows until what shows it was seen: a weak spot no hit found
            // is left out and counted, anything else reads ??? and says what shows it.
            var value = Masked(explorer, pair.Value);
            string keptBack = null;
            if (facts.Proofs.TryGetValue(pair.Key, out var proof) && explorer.KeepsBack(proof))
            {
                if (EntryKeys.IsWeakSpot(proof))
                {
                    SpotLeft();
                    return y;
                }
                keptBack = proof;
                value = explorer.KeptBack(proof);
                CountDrawn(PanelPart.FactKeptBack);
            }
            var valueW = width - labelW - U(10f);
            // A line Scry is not sure of is marked, softer, and says why on hover.
            var unsure = facts.Unsure.TryGetValue(pair.Key, out var why);
            var label = unsure ? UnsureWords.Marked(pair.Key) : pair.Key;
            var labelH = Skin.Height(Skin.DimWrap, label, labelW);
            var height = Mathf.Max(U(20f), Mathf.Max(labelH, Skin.Height(Skin.Wrap, value, valueW)));
            GUI.Label(new Rect(0f, y, labelW, labelH), label, Skin.DimWrap);
            var valueRect = new Rect(labelW + U(10f), y, valueW, height);
            if (unsure && new Rect(0f, y, width, height).Contains(Event.current.mousePosition)) AskTip("unsure:" + pair.Key, why);
            // Music plays where it is named: the entry's own, or one by its name (a biome has one for each time of day).
            if (facts.Links.TryGetValue(pair.Key, out var music) && EntryKeys.PlaysMusic(music, out var named))
            {
                var playing = MusicPreview.PlayingFor == entry && (named == null || MusicPreview.Playing == named);
                var musicW = Mathf.Min(valueW, Skin.Width(Skin.Wrap, value) + U(4f));
                var musicRect = new Rect(valueRect.x, valueRect.y, musicW, height);
                LinkLabel(musicRect, value, Skin.Wrap, playing ? Skin.KindColor(Kind.Sound) : Skin.Accent);
                if (musicRect.Contains(Event.current.mousePosition)) AskTip("music:" + entry.Key + ":" + named, MusicWords.Tip(playing));
                if (GUI.Button(musicRect, GUIContent.none, GUIStyle.none))
                {
                    var said = named == null ? Previews.PlacesMusic(entry) : Previews.NamedMusic(entry, named);
                    if (said != null) Say(said);
                }
            }
            // A mod's website is copied for the player to open: Scry opens nothing outside the game.
            else if (facts.Links.TryGetValue(pair.Key, out var web) && EntryKeys.WebsiteOf(web) is string url)
            {
                var webW = Mathf.Min(valueW, Skin.Width(Skin.Wrap, value) + U(4f));
                var webRect = new Rect(valueRect.x, valueRect.y, webW, height);
                LinkLabel(webRect, value, Skin.Wrap, Skin.Accent);
                if (webRect.Contains(Event.current.mousePosition)) AskTip("web:" + entry.Key, "Copy its address");
                if (GUI.Button(webRect, GUIContent.none, GUIStyle.none))
                {
                    GUIUtility.systemCopyBuffer = url;
                    Say(PanelWords.Copied(url));
                }
            }
            // A link only to what is in the catalog: a creature's own attack items are not.
            else if (facts.Links.TryGetValue(pair.Key, out var link) && Goes(explorer, link))
            {
                var mark = ChoiceMark(explorer, link);
                var floor = mark != null;
                var linkW = Mathf.Min(valueW - (floor ? FloorRoom : 0f), Skin.Width(Skin.Wrap, value) + U(4f));
                var linkRect = new Rect(valueRect.x, valueRect.y, linkW, height);
                LinkLabel(linkRect, value, Skin.Wrap, LinkText(KindOf(explorer, link), false));
                if (floor) FloorMark(linkRect.xMax, linkRect, mark);
                if (linkRect.Contains(Event.current.mousePosition)) AskTip("link:" + link, FloorTip(explorer, link, PanelWords.GoTo(ShownName(explorer, link, value))));
                if (GUI.Button(linkRect, GUIContent.none, GUIStyle.none)) Go(explorer, link);
            }
            // Kept back: ??? as any value, what shows it faint, as every other hint of the strict rules;
            // said once for lines one after another that one deed shows, the rest saying it on hover.
            else if (keptBack != null)
            {
                var hidden = Skin.Width(Skin.Wrap, CompendiumWords.Hidden);
                GUI.Label(new Rect(valueRect.x, valueRect.y, hidden + U(2f), height), CompendiumWords.Hidden, Skin.Wrap);
                var hint = explorer.KeptBackHint(keptBack);
                if (keptBack != _heldAbove) Skin.LabelIn(new Rect(valueRect.x + hidden, valueRect.y, valueRect.width - hidden, height), hint, Skin.Wrap, Skin.Faint);
                else if (valueRect.Contains(Event.current.mousePosition)) AskTip("held:" + pair.Key, Naming.Capital(CompendiumWords.ShowsOnce(keptBack)));
            }
            else
            {
                GUI.Label(valueRect, value, unsure ? Skin.DimWrap : Skin.Wrap);
            }
            _heldAbove = keptBack;
            // What it comes to at the character's own skill, faint under the game's figure; none where that is kept back.
            if (keptBack == null && YoursShown(explorer, facts, pair.Key, out var yours))
            {
                var yoursH = Skin.Height(Skin.SmallWrap, yours, valueW);
                var yoursRect = new Rect(valueRect.x, y + height, valueW, yoursH);
                Skin.LabelIn(yoursRect, yours, Skin.SmallWrap, Skin.Faint);
                if (facts.YoursTips.TryGetValue(pair.Key, out var tip) && yoursRect.Contains(Event.current.mousePosition)) AskTip("yours:" + pair.Key, tip);
                CountDrawn(PanelPart.YourSkillLine);
                height += yoursH;
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
            // A line of where it spawns or grows shows only where it was met in one of its biomes;
            // one counted over every character's play is left out quietly under the strict rules, the
            // page counting this character's own, and under the loose rules where its giver was not met.
            var where = ShownStrictly(explorer, facts.Where, s => s.AllPlay
                ? !explorer.LeavingOut && explorer.LeavesOut(s.Prefab)
                : explorer.LeavesOut(s.Prefab) || explorer.LeavesOutAll(s.MetIn));
            if (explorer.LeavingOut && OwnPlayOnly(where)) where = OwnPlay;
            if (where.Count == 0) return y;
            var (lines, rows) = SplitWhere(explorer, where);
            if (titled)
            {
                y += U(6f);
                GUI.Label(new Rect(0f, y, width, U(20f)), facts.WhereTitle, Skin.DimLabel);
                y += U(24f);
            }
            const int firstLines = 8;
            var shownLines = ShownOf("where", lines.Count, firstLines);
            HeldHints.Clear();
            foreach (var source in lines.Take(shownLines))
            {
                // A line naming a prefab in the catalog is a chip that goes there; the rest is text.
                // A line Scry is not sure of is marked and says why on hover. Under the strict rules
                // a way with a chance in it tells the character's count in its place, or ???.
                var counted = explorer.StrictSourceLine(source.Record);
                var held = counted != null && counted.EndsWith(CompendiumWords.Hidden, StringComparison.Ordinal) ? SourceWords.HeldTip(source.Record) : null;
                if (held != null && held != TallyWords.NeverCounted && !HeldHints.Contains(held)) HeldHints.Add(held);
                var text = Masked(explorer, counted ?? (source.Unsure != null ? UnsureWords.Marked(source.Text) : source.Text));
                if (string.IsNullOrEmpty(source.Prefab) || !Goes(explorer, source.Prefab))
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
                var mark = ChoiceMark(explorer, source.Prefab);
                var floor = mark != null;
                var textX = icon != null ? U(34f) : U(12f);
                var textW = width - textX - U(10f) - (floor ? FloorRoom : 0f);
                var wrapped = Skin.SmallWrap;
                var chipH = Mathf.Max(U(30f), Skin.Height(wrapped, text, textW) + U(10f));
                var chip = new Rect(0f, y, width, chipH);
                var hover = chip.Contains(Event.current.mousePosition);
                var kind = KindOf(explorer, source.Prefab);
                Skin.Box(chip, LinkFill(kind, hover));
                if (icon != null) DrawSprite(icon, new Rect(U(6f), y + (chipH - U(22f)) / 2f, U(22f), U(22f)));
                Skin.LabelIn(new Rect(textX, y, textW, chipH), text, wrapped, LinkText(kind, hover));
                if (floor) PlateFloorMark(chip, mark);
                if (hover) AskTip("src:" + source.Prefab + source.Unsure + text, FloorTip(explorer, source.Prefab, Naming.Lines(held, source.Unsure, PanelWords.GoTo(ShownName(explorer, source.Prefab, source.Prefab)))));
                if (GUI.Button(chip, GUIContent.none, GUIStyle.none) && explorer.Jump(source.Prefab))
                {
                    AfterGoing();
                }
                y += chipH + U(5f);
            }
            var more = new ChipFlow(0f, width, y, U(26f), U(5f), U(5f));
            MoreChip("where", lines.Count, firstLines, width, ref more);
            if (more.InRow) y = more.RowBottom + U(5f);
            foreach (var row in rows) y = FactRow(explorer, row, width, y);
            // Under the strict rules a giver's chip may hold its count back as a line does.
            if (explorer.LeavingOut && rows.Count > 0)
            {
                foreach (var source in where)
                {
                    if (source.AllPlay || !SourceGroups.Groups(source.Record) || explorer.StrictSourceAmount(source.Record) != CompendiumWords.Hidden) continue;
                    var held = SourceWords.HeldTip(source.Record);
                    if (held != null && held != TallyWords.NeverCounted && !HeldHints.Contains(held)) HeldHints.Add(held);
                }
            }
            // What shows a count not yet seen, faintly under the lines, once for each way of counting.
            // What shows each amount held back, faintly under the lines, once each; what nothing counts says so on hover only.
            foreach (var hint in HeldHints) y = HintLine(hint, "not-counted:", hint, PanelPart.TallyCounted, width, y);
            return y;
        }

        private static readonly Dictionary<object, (IReadOnlyList<Source> Lines, IReadOnlyList<Facts.Row> Rows)> WhereSplits = new Dictionary<object, (IReadOnlyList<Source>, IReadOnlyList<Facts.Row>)>();
        private static Explorer _splitIn;
        private static int _splitStamp = -1;
        private static int _splitSelection = -1;

        /// <summary>The rows of chips an entry's page tells where it comes from in, under the rules as they are, for the self-test.</summary>
        [Diagnostic]
        public static IReadOnlyList<Facts.Row> WhereRowsForTest(Explorer explorer, Entry entry) => SplitWhere(explorer, Facts.For(entry).Where).Rows;

        /// <summary>
        /// Where a thing comes from, split into the lines told as they are and the rows of chips
        /// the ways giving an amount or nothing go into, a row for each way, givers alike but for
        /// their biome one chip saying how many (<see cref="SourceGroups"/>), each giver's own line
        /// and prefab on its hover. Under the strict rules every giver keeps a chip of its own, its
        /// count the character's. Worked out once for each list while the page and what the
        /// character knows stay the same.
        /// </summary>
        private static (IReadOnlyList<Source> Lines, IReadOnlyList<Facts.Row> Rows) SplitWhere(Explorer explorer, IReadOnlyList<Source> where)
        {
            if (!ReferenceEquals(_splitIn, explorer) || _splitStamp != explorer.KnowStamp || _splitSelection != explorer.SelectionVersion || WhereSplits.Count > 64)
            {
                _splitIn = explorer;
                _splitStamp = explorer.KnowStamp;
                _splitSelection = explorer.SelectionVersion;
                WhereSplits.Clear();
            }
            if (WhereSplits.TryGetValue(where, out var split)) return split;
            var lines = new List<Source>();
            var grouped = new List<SourceFacts>();
            foreach (var source in where)
            {
                if (!source.AllPlay && SourceGroups.Groups(source.Record)) grouped.Add(source.Record);
                else lines.Add(source);
            }
            var rows = new List<Facts.Row>();
            Facts.Row RowOf(string lead)
            {
                var row = rows.Count > 0 && rows[rows.Count - 1].Title == lead ? rows[rows.Count - 1] : null;
                if (row != null) return row;
                row = new Facts.Row { Title = lead };
                rows.Add(row);
                return row;
            }
            if (explorer.LeavingOut)
            {
                // Each giver its own chip, its amount the character's count or ???, the line and how
                // the count comes to show on its hover.
                foreach (var record in grouped)
                {
                    var own = SourceGroups.Of(new[] { record })[0];
                    var amount = explorer.StrictSourceAmount(record);
                    var held = amount == CompendiumWords.Hidden ? SourceWords.HeldTip(record) : null;
                    RowOf(own.Lead).Items.Add(new Facts.Ingredient
                    {
                        Icon = PrefabIcon(record.Giver), Name = own.Chip, Amount = amount ?? own.Amount ?? "", Prefab = record.Giver,
                        MarkTip = Naming.Lines(explorer.StrictSourceLine(record) ?? SourceWords.Line(record, Knowledge.BossOf), held),
                    });
                }
            }
            else
            {
                foreach (var group in SourceGroups.Of(grouped))
                {
                    // The chip goes to the first of its givers the character can go to.
                    var first = group.Givers.FirstOrDefault(g => explorer.Reaches(g.Giver))?.Giver ?? group.Givers[0].Giver;
                    RowOf(group.Lead).Items.Add(new Facts.Ingredient
                    {
                        Icon = PrefabIcon(first), Name = group.Chip, Amount = group.Amount ?? "", Prefab = first,
                        MarkTip = group.Tip(s => SourceWords.Line(s, Knowledge.BossOf)),
                    });
                }
            }
            split =(lines.Count == where.Count ? where : lines, rows);
            WhereSplits[where] = split;
            return split;
        }

        /// <summary>Its biomes as chips.</summary>
        private static float BiomesBlock(Explorer explorer, Entry entry, float width, float y)
        {
            // Its biomes, each going to its page (which can search for everything there), else
            // searching; a biome's own page names none.
            if (entry.Biomes.Length > 0 && entry.Kind != Kind.Biome)
            {
                StrictLeft(entry.Biomes.Count(b => explorer.LeavesOut(EntryKeys.For(Kind.Biome, b))));
                y = ChipRow("Biomes", entry.Biomes.Where(b => !explorer.LeavesOut(EntryKeys.For(Kind.Biome, b))).Select(b =>
                {
                    var page = EntryKeys.For(Kind.Biome, b);
                    Action go = Unseen(explorer, page) ? null
                        : Goes(explorer, page) ? () => Go(explorer, page)
                        : (Action)(() => SearchFor(explorer, SearchHelp.Term("biome", b)));
                    return new KeyValuePair<string, Action>(Knowledge.BiomeName(b), go);
                }), width, y);
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

        /// <summary>What shows the amounts held back in the block of where a thing comes from being drawn, each once.</summary>
        private static readonly List<string> HeldHints = new List<string>();

        /// <summary>The lines of where a thing comes from the block being drawn shows under the strict rules, those counted over every character's play left out.</summary>
        private static readonly List<Source> OwnPlay = new List<Source>();

        /// <summary>Whether lines hold any counted over every character's play; if so, the rest are put in <see cref="OwnPlay"/>.</summary>
        private static bool OwnPlayOnly(IReadOnlyList<Source> lines)
        {
            var any = false;
            for (var i = 0; i < lines.Count && !any; i++) any = lines[i].AllPlay;
            if (!any) return false;
            OwnPlay.Clear();
            for (var i = 0; i < lines.Count; i++) if (!lines[i].AllPlay) OwnPlay.Add(lines[i]);
            return true;
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
            CountDrawn(PanelPart.Command);
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
                var dump = new Rect(0f, y, Mathf.Min(width, w), U(26f));
                if (GUI.Button(dump, text, Skin.Fitted(Skin.Chip, dump))) Say(Stage.Dump());
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
                    var stopChip = new Rect(width - stopW, y, stopW, U(24f));
                    if (GUI.Button(stopChip, stop, Skin.Fitted(Skin.Chip, stopChip))) StopReadingLocations();
                    return y + U(30f);

                default:
                    var w = Skin.Width(Skin.Chip, LocationsButtonText) + U(8f);
                    var readChip = new Rect(0f, y, Mathf.Min(width, w), U(26f));
                    if (GUI.Button(readChip, LocationsButtonText, Skin.Fitted(Skin.Chip, readChip))) StartReadingLocations();
                    y += U(30f);
                    const string note = "Read each of this world's locations and dungeon rooms once, in the background, over a few minutes, keeping what they hold for the next time.";
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
            var strict = explorer.LeavingOut;
            if (ReferenceEquals(_foundInFor, entry) && ReferenceEquals(_foundInOf, entry.FoundIn) && ReferenceEquals(_foundInIn, explorer) && strict == _foundInStrict)
            {
                StrictLeft(_foundInLeft);
                return FoundInItems;
            }
            _foundInFor = entry;
            _foundInOf = entry.FoundIn;
            _foundInIn = explorer;
            _foundInStrict = strict;
            _foundInLeft = 0;
            FoundInItems.Clear();
            foreach (var place in entry.FoundIn)
            {
                var name = Places.NameOf(place);
                var location = Places.LocationNamed(explorer.Catalog, place);
                // A place with no entry of its own can never be seen together with the page: the strict rules leave it out.
                if (location == null && strict)
                {
                    _foundInLeft++;
                    continue;
                }
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
        private static bool _foundInStrict;
        private static int _foundInLeft;
        private static Explorer _foundInIn;

        private const string LocationsButtonText = "Find where things are";
        private const string LocationsButtonTip = "Read every location and dungeon room over a few minutes, so things tell where they are found";

        private const string ReadAgainText = "Look again";
        private const string ReadAgainTip = "Forget what was read of every location and room and read them afresh, for when one seems wrong";

        /// <summary>
        /// Above the Locations tab's list: until the places are known, Find where things are (Stop
        /// reading while it reads), and Every world beside it; once known, that they are kept, or
        /// how far reading them again has got, Every world, and Look again.
        /// </summary>
        private static Rect PlacesBar(Rect inner)
        {
            CountDrawn(PanelPart.PlacesBar);
            var barH = U(30f);
            var mouse = Event.current.mousePosition;
            var everyW = Skin.Width(Skin.SegmentOn, LocationWords.EveryWorld) + U(12f);
            Rect every;
            if (Locations.Now == Locations.State.Read)
            {
                var w = Skin.Width(Skin.Chip, ReadAgainText) + U(12f);
                var button = new Rect(inner.xMax - w - U(6f), inner.y, w, U(26f));
                every = new Rect(button.x - U(8f) - everyW, inner.y, everyW, U(26f));
                GUI.Label(new Rect(inner.x + U(10f), inner.y + U(2f), every.x - inner.x - U(18f), U(24f)),
                    LocationWords.PlacesBar(Locations.Reading, Locations.Done, Locations.Total), Skin.DimLabel);
                if (GUI.Button(button, ReadAgainText, Skin.Fitted(Skin.Chip, button))) ReadPlacesAgain();
                if (button.Contains(mouse)) AskTip("read-again", ReadAgainTip);
            }
            else
            {
                var reading = Locations.Now == Locations.State.Reading;
                var label = reading ? "Stop reading" : LocationsButtonText;
                var button = new Rect(inner.x + U(6f), inner.y, Skin.Width(Skin.Button, label) + U(10f), U(26f));
                if (GUI.Button(button, label, Skin.Button))
                {
                    if (reading) StopReadingLocations();
                    else StartReadingLocations();
                }
                if (button.Contains(mouse)) AskTip("locations-bar", reading ? "Stop reading the locations and dungeons; nothing read so far is kept" : LocationsButtonTip);
                every = new Rect(button.xMax + U(8f), inner.y, everyW, U(26f));
            }
            var auto = Settings.ReadLocationsAutomatically;
            if (GUI.Button(every, LocationWords.EveryWorld, Skin.Choice(0, 1, auto))) EveryWorld(!auto);
            if (every.Contains(mouse)) AskTip("read-every-world", LocationWords.EveryWorldTip(auto));
            return new Rect(inner.x, inner.y + barH, inner.width, inner.height - barH);
        }

        /// <summary>Reads where things are by itself in every world, or only when asked, kept in the config file; turned on, it starts here at once. The switch, and the self-test.</summary>
        internal static void EveryWorld(bool on)
        {
            Settings.ReadLocationsAutomatically = on;
            if (on && Locations.Now == Locations.State.NotRead) StartReadingLocations();
        }

        /// <summary>Above the Locations tab's list with the compendium on: that the explored map counts, and reading it again.</summary>
        private static Rect MapBar(Rect inner)
        {
            CountDrawn(PanelPart.MapBar);
            var barH = U(30f);
            var w = Skin.Width(Skin.Chip, CompendiumWords.ReadMap) + U(12f);
            GUI.Label(new Rect(inner.x + U(10f), inner.y + U(2f), inner.width - w - U(24f), U(24f)), CompendiumWords.MapBar, Skin.DimLabel);
            var button = new Rect(inner.xMax - w - U(6f), inner.y, w, U(26f));
            if (GUI.Button(button, CompendiumWords.ReadMap, Skin.Fitted(Skin.Chip, button))) Say(CompendiumMap.Again(CompendiumKeeper.Book));
            if (button.Contains(Event.current.mousePosition)) AskTip("readmap", CompendiumWords.ReadMapTip);
            return new Rect(inner.x, inner.y + barH, inner.width, inner.height - barH);
        }

        /// <summary>Forgets what was kept of the places and reads them afresh, as Look again does, and says so.</summary>
        internal static void ReadPlacesAgain()
        {
            var said = Locations.ForgetKept();
            Say(Naming.Capital(said));
        }

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
            WhereSplits.Clear();
            _splitIn = null;
        }
    }
}
