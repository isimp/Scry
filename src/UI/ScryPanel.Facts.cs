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
                var kind = Timing.Start();
                foreach (var pair in facts.Pairs) y = FactPair(explorer, entry, facts, pair, width, labelW, y);
                Timing.Add("facts drawn pairs", kind);
                kind = Timing.Start();
                foreach (var row in facts.Rows) y = FactRow(explorer, row, width, y);
                Timing.Add("facts drawn rows", kind);
                kind = Timing.Start();
                foreach (var block in EveryBlock) y = FactBlockOf(explorer, entry, facts, block, places, true, width, y);
                Timing.Add("facts drawn blocks", kind);
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
        private static List<(string Label, string Value)> _still = new List<(string, string)>();
        private static string _killedElsewhere;

        /// <summary>
        /// The box a page opens with, titled "You": what the game counted of the thing for the
        /// character (<see cref="CharacterRecords.Counts"/>) and, with the compendium on, how it
        /// came to know it (<see cref="CompendiumWords.CameLine"/>); under the strict rules, what
        /// the page still keeps back, a row for each thing to do (<see cref="CompendiumWords.StillToFindOut"/>).
        /// Read again once a second and on another page; drawn behind what it holds at the height
        /// that held last time.
        /// </summary>
        private static float YouBox(Explorer explorer, Entry entry, Facts facts, float width, float y)
        {
            if (entry != _youOf || Time.unscaledTime - _youAt > 1f)
            {
                _youOf = entry;
                _youAt = Time.unscaledTime;
                string came = null;
                if (explorer.CompendiumOn)
                {
                    var record = CompendiumKeeper.Book?.Came(entry.Key);
                    var biome = record?.Biome != null ? explorer.Find(EntryKeys.For(Kind.Biome, record.Value.Biome))?.ShownName ?? record.Value.Biome : null;
                    came = CompendiumWords.CameLine(record?.Way, record?.Day ?? -1, biome, explorer.Compendium.Because(entry)?.ShownName, explorer.ByFloor(entry), explorer.LevelOf(entry) == KnownLevel.Known);
                }
                _you = CompendiumWords.You(CharacterRecords.Counts(entry), came);
                // A skill's page opens with the character's level and the way to the next.
                if (entry.Source is SkillSource skill) _you.AddRange(YourSkills.Lines(skill));
                _killedElsewhere = entry.Kind == Kind.Creature ? explorer.KilledElsewhere(entry.Key) : null;
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
                // Kills another player's game ran follow the count, faint, saying why on hover.
                if (label == CompendiumWords.KilledLabel && _killedElsewhere != null)
                {
                    var said = Skin.Width(Skin.Wrap, value);
                    GUI.Label(new Rect(valueRect.x, y, said + U(2f), height), value, Skin.Wrap);
                    Skin.LabelIn(new Rect(valueRect.x + said, y, valueRect.width - said, height), _killedElsewhere, Skin.Wrap, Skin.Faint);
                    if (valueRect.Contains(Event.current.mousePosition)) AskTip("killed-elsewhere", ElsewhereWords.Tip);
                    CountDrawn(PanelPart.RanElsewhere);
                }
                else GUI.Label(valueRect, value, Skin.Wrap);
                y += height + U(6f);
            }
            // What is still to find out: what to do on the left, what it shows faint on the right,
            // why on hover where it shows only where the character's game runs it.
            foreach (var (label, value) in _still)
            {
                var height = Mathf.Max(U(20f), Skin.Height(Skin.Wrap, value, valueW));
                GUI.Label(new Rect(pad, y, labelW, height), label, Skin.DimWrap);
                var valueRect = new Rect(pad + labelW + U(10f), y, valueW, height);
                Skin.LabelIn(valueRect, value, Skin.Wrap, Skin.Faint);
                if (value.EndsWith(ElsewhereWords.Suffix, System.StringComparison.Ordinal) && new Rect(pad, y, inner, height).Contains(Event.current.mousePosition)) AskTip("still:" + label, ElsewhereWords.Tip);
                y += height + U(6f);
            }
            if (_still.Count > 0) CountDrawn(PanelPart.StillToFindOut);
            y += U(4f);
            if (Event.current.type == EventType.Repaint) _youHeight = y - top;
            CountDrawn(PanelPart.YouBox);
            return y + U(10f);
        }

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
            return CompendiumWords.StillToFindOut(deeds, types, attacks, spots, uncounted, explorer.WithOthers);
        }

        private static readonly List<(string Label, string Value)> NothingStill = new List<(string, string)>();

        /// <summary>Lets go of the page the You box was read for, for another world.</summary>
        private static void ForgetYou()
        {
            _youOf = null;
            _you.Clear();
            _still = NothingStill;
            _killedElsewhere = null;
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
                var floor = explorer.ShownByFloor(link);
                var linkW = Mathf.Min(valueW - (floor ? FloorRoom : 0f), Skin.Width(Skin.Wrap, value) + U(4f));
                var linkRect = new Rect(valueRect.x, valueRect.y, linkW, height);
                LinkLabel(linkRect, value, Skin.Wrap, LinkText(KindOf(explorer, link), false));
                if (floor) FloorMark(linkRect.xMax, linkRect);
                if (linkRect.Contains(Event.current.mousePosition)) AskTip("link:" + link, FloorTip(explorer, link, PanelWords.GoTo(ShownName(explorer, link, value))));
                if (GUI.Button(linkRect, GUIContent.none, GUIStyle.none)) Go(explorer, link);
            }
            // Kept back: ??? as any value, what shows it faint, as every other hint of the strict rules.
            else if (keptBack != null)
            {
                var hidden = Skin.Width(Skin.Wrap, CompendiumWords.Hidden);
                GUI.Label(new Rect(valueRect.x, valueRect.y, hidden + U(2f), height), CompendiumWords.Hidden, Skin.Wrap);
                Skin.LabelIn(new Rect(valueRect.x + hidden, valueRect.y, valueRect.width - hidden, height), explorer.KeptBackHint(keptBack), Skin.Wrap, Skin.Faint);
                if (explorer.MayRunElsewhere(keptBack) && valueRect.Contains(Event.current.mousePosition)) AskTip("elsewhere:" + pair.Key, ElsewhereWords.Tip);
            }
            else
            {
                GUI.Label(valueRect, value, unsure ? Skin.DimWrap : Skin.Wrap);
            }
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
            // one counted over every character's play is left out quietly, the page counting this character's own.
            var where = ShownStrictly(explorer, facts.Where, s => !s.AllPlay && (explorer.LeavesOut(s.Prefab) || explorer.LeavesOutAll(s.MetIn)));
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
                var floor = explorer.ShownByFloor(source.Prefab);
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
                if (floor) PlateFloorMark(chip);
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
        /// and prefab on its hover. Under the strict rules every giver keeps its own line, its
        /// count the character's. Worked out once for each list while the page and what the
        /// character knows stay the same.
        /// </summary>
        private static (IReadOnlyList<Source> Lines, IReadOnlyList<Facts.Row> Rows) SplitWhere(Explorer explorer, IReadOnlyList<Source> where)
        {
            if (explorer.LeavingOut) return (where, Array.Empty<Facts.Row>());
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
            foreach (var group in SourceGroups.Of(grouped))
            {
                var row = rows.Count > 0 && rows[rows.Count - 1].Title == group.Lead ? rows[rows.Count - 1] : null;
                if (row == null)
                {
                    row = new Facts.Row { Title = group.Lead };
                    rows.Add(row);
                }
                // The chip goes to the first of its givers the character can go to.
                var first = group.Givers.FirstOrDefault(g => explorer.Reaches(g.Giver))?.Giver ?? group.Givers[0].Giver;
                row.Items.Add(new Facts.Ingredient
                {
                    Icon = PrefabIcon(first), Name = group.Chip, Amount = group.Amount ?? "", Prefab = first,
                    MarkTip = group.Tip(s => SourceWords.Line(s, Knowledge.BossOf)),
                });
            }
            split = (lines.Count == where.Count ? where : lines, rows);
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
            GUI.Label(titleRect, row.Unsure != null ? UnsureWords.Marked(row.Title) : row.Title, Skin.DimLabel);
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
                if (OutOfSight(at)) continue;
                // A quiet cell (tools' damage a creature takes none of) keeps its place but stands back.
                Skin.Box(at, cell.Tone == Tone.Quiet ? Skin.Panel : Skin.Raised);
                GUI.Label(new Rect(at.x + U(2f), at.y + U(3f), at.width - U(4f), U(16f)), cell.Type, Skin.CellType);
                Skin.LabelIn(new Rect(at.x + U(2f), at.y, at.width - U(4f), at.height - U(4f)), hidden ? CompendiumWords.Hidden : cell.Value, Skin.CellValue, ToneColor(hidden ? Tone.Plain : cell.Tone));
                if (at.Contains(Event.current.mousePosition)) AskTip("cell:" + row.Title + cell.Type + hidden, hidden ? CompendiumWords.NotYetHitWithTip(cell.Type) : cell.Tip);
            }
            CountDrawn(PanelPart.ResistanceGrid);
            var lines = (row.Cells.Count + columns - 1) / columns;
            y += lines * (cellH + gap) + U(4f);
            return HintLine(CompendiumWords.NotYetHitWith(keptBack), "damage-kept-back", "", PanelPart.DamageKeptBack, width, y);
        }

        /// <summary>Whether the world played has a key set, a boss's fall; none without a world.</summary>
        private static readonly Func<string, bool> WorldKeySet = key => ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key);

        /// <summary>
        /// A titled row of chips, each an item with its amount that goes to it when clicked; the
        /// title goes to the station it names. A long row shows its first chips and one for the rest.
        /// </summary>
        private static float FactRow(Explorer explorer, Facts.Row row, float width, float y)
        {
            if (row.AllPlay && explorer.LeavingOut) return y;
            // Wares waiting for a boss not yet fallen are not named with the compendium on: under the
            // loose rules the row keeps its title and how many, under the strict rules they are counted at the section's end.
            if (row.WaitsFor != null)
            {
                var unlock = UnlockRows.Of(row.WaitsFor, explorer.NamesOnlyMet, explorer.LeavingOut, WorldKeySet);
                if (unlock == UnlockShown.Folded)
                {
                    ToUnlock(row.Items.Count);
                    return y;
                }
                if (unlock == UnlockShown.Counted)
                {
                    y += U(6f);
                    GUI.Label(new Rect(0f, y, width, U(20f)), Masked(explorer, row.Title), Skin.DimLabel);
                    return HintLine(CompendiumWords.ToUnlock(row.Items.Count), "to-unlock:", row.Title, PanelPart.ToUnlock, width, y + U(20f));
                }
            }
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
            var items = ShownStrictly(explorer, listed, i => i.Prefab);
            // What a creature eats, some not yet seen, is seen only where its game runs it.
            var eatenElsewhere = row.SeenWhereRun && explorer.WithOthers && items.Count < listed.Count;
            // A thing to bring shows once what suggests it was found out; left out quietly, as a suggestion.
            if (explorer.LeavingOut && Suggests(items))
            {
                Suggested.Clear();
                foreach (var item in items) if (!explorer.KeepsBackAll(item.ShownBy)) Suggested.Add(item);
                items = Suggested;
            }
            if (items.Count == 0) return eatenElsewhere ? FaintLine(ElsewhereWords.Eating, ElsewhereWords.Tip, "eating:", row.Title, PanelPart.RanElsewhere, width, y) : y;
            var title = listing ? CompendiumWords.Found(row.Title, items.Count) : items.Count < listed.Count ? CompendiumWords.Uncounted(row.Title) : row.Title;
            // Under the strict rules drops and yields are told as the character saw them, not by the game's chances.
            var counted = explorer.StrictTitle(row.Tally, row.TallyOf, row.TallyLead);
            if (counted != null) title = counted;
            // A name in the title the character has not met is kept back, as in any text on the page.
            title = Masked(explorer, title);
            var notCounted = false;
            var key = "facts:" + row.Title;
            var count = ShownOf(key, items.Count);
            y += U(6f);
            // A row Scry is not sure of is marked and says why on hover.
            if (row.Unsure != null)
            {
                var unsureRect = new Rect(0f, y, width, U(20f));
                GUI.Label(unsureRect, UnsureWords.Marked(title), Skin.DimLabel);
                if (unsureRect.Contains(Event.current.mousePosition)) AskTip("unsure:" + row.Title, row.Unsure);
            }
            else if (!string.IsNullOrEmpty(row.TitleLink) && Goes(explorer, row.TitleLink))
            {
                var titleFloor = explorer.ShownByFloor(row.TitleLink);
                var titleW = Mathf.Min(width - (titleFloor ? FloorRoom : 0f), Skin.Width(Skin.DimLabel, title) + U(4f));
                var titleRect = new Rect(0f, y, titleW, U(20f));
                LinkLabel(titleRect, title, Skin.DimLabel, LinkText(KindOf(explorer, row.TitleLink), false));
                if (titleFloor) FloorMark(titleRect.xMax, titleRect);
                if (titleRect.Contains(Event.current.mousePosition)) AskTip("row-title:" + row.TitleLink, FloorTip(explorer, row.TitleLink, PanelWords.GoTo(ShownName(explorer, row.TitleLink, row.TitleLink))));
                if (GUI.Button(titleRect, GUIContent.none, GUIStyle.none)) Go(explorer, row.TitleLink);
            }
            else
            {
                GUI.Label(new Rect(0f, y, width, U(20f)), title, Skin.DimLabel);
            }
            y += U(24f);

            var flow = new ChipFlow(0f, width, y, U(30f), U(6f), U(5f));
            for (var i = 0; i < count; i++)
            {
                var item = items[i];
                var amount = explorer.StrictAmount(row.Tally, row.TallyOf, item.Prefab) ?? item.Amount;
                if (amount == CompendiumWords.Hidden) notCounted = true;
                var text = DetailWords.Amounted(amount, item.Name);
                // A mark (only here, what a trader pays) in a pill of its own at the chip's end.
                var markW = item.Mark != null ? Skin.Width(Skin.Badge, item.Mark) + U(16f) : 0f;
                var floorW = !string.IsNullOrEmpty(item.Prefab) && explorer.ShownByFloor(item.Prefab) ? FloorRoom : 0f;
                markW += floorW;
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
                if (floorW > 0f) PlateFloorMark(chip);

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
            }
            MoreChip(key, items.Count, FirstChips, width, ref flow);
            y = flow.RowBottom + U(6f);
            if (notCounted) y = HintLine(explorer.NotYetCounted(row.Tally), explorer.MayRunElsewhere(row.Tally) ? ElsewhereWords.Tip : null, "not-counted:", row.Title, PanelPart.TallyCounted, width, y - U(2f));
            foreach (var said in explorer.ElsewhereLines(row.Tally, row.TallyOf)) y = FaintLine(said, ElsewhereWords.Tip, "elsewhere:", said, PanelPart.RanElsewhere, width, y);
            if (eatenElsewhere) y = FaintLine(ElsewhereWords.Eating, ElsewhereWords.Tip, "eating:", row.Title, PanelPart.RanElsewhere, width, y);
            return y;
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

        /// <summary>The things to bring the row being drawn shows, the rest kept back (<see cref="Facts.Ingredient.ShownBy"/>).</summary>
        private static readonly List<Facts.Ingredient> Suggested = new List<Facts.Ingredient>();

        /// <summary>Whether a row's chips are things to bring, shown by what was found out.</summary>
        private static bool Suggests(IReadOnlyList<Facts.Ingredient> items)
        {
            for (var i = 0; i < items.Count; i++) if (items[i].ShownBy != null) return true;
            return false;
        }

        /// <summary>A chain's steps, an arrow between each, the page's own lit and each other one going to its page.</summary>
        private static float ChainRow(Explorer explorer, Facts.Row row, float width, float y)
        {
            if (explorer.NamesOnlyMet && row.Chain.Steps.Any(step => step.Any(explorer.LeavesOut)))
            {
                StrictLeft(1);
                return y;
            }
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
                    var floor = !here && explorer.ShownByFloor(key);
                    var w = Mathf.Min(width, LinkChipWidth(name, true, floor));
                    var at = flow.Place(w);
                    var chip = new Rect(at.X, at.Y, w, flow.RowHeight);
                    if (OutOfSight(chip)) continue;
                    if (here)
                    {
                        GUI.Label(chip, name, Skin.Fitted(Skin.ChipOn, chip));
                        continue;
                    }
                    var goes = Goes(explorer, key);
                    if (LinkChip(chip, name, goes ? KindOf(explorer, key) : null, false, goes, floor && goes) && goes) Go(explorer, key);
                    if (goes && chip.Contains(Event.current.mousePosition)) AskTip("chain:" + key, FloorTip(explorer, key, PanelWords.GoTo(name)));
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
                    const string note = "Reads each of this world's locations and dungeon rooms once, in the background, over a few minutes, and keeps what they hold for the next time.";
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

        private const string LocationsButtonText = "Read all locations";
        private const string LocationsButtonTip = "Reads what every location and dungeon room in this world holds, in the background over a few minutes, so things tell where they are found and in: finds them. What is read is kept, and known at once the next time. A location selected is read by itself.";

        private const string ReadAgainText = "Read again";
        private const string ReadAgainTip = "Forgets what Scry kept of every location and dungeon room, what each holds and where its floors are, and reads them all afresh in the background, for when what is shown seems wrong or out of date. Until then they show as never read.";

        /// <summary>Above the Locations tab's list once the places are known: that they are kept, or how far reading them again has got, and Read again.</summary>
        private static Rect PlacesBar(Rect inner)
        {
            CountDrawn(PanelPart.PlacesBar);
            var barH = U(30f);
            var w = Skin.Width(Skin.Chip, ReadAgainText) + U(12f);
            GUI.Label(new Rect(inner.x + U(10f), inner.y + U(2f), inner.width - w - U(24f), U(24f)),
                LocationWords.PlacesBar(Locations.Reading, Locations.Done, Locations.Total), Skin.DimLabel);
            var button = new Rect(inner.xMax - w - U(6f), inner.y, w, U(26f));
            if (GUI.Button(button, ReadAgainText, Skin.Fitted(Skin.Chip, button))) ReadPlacesAgain();
            if (button.Contains(Event.current.mousePosition)) AskTip("read-again", ReadAgainTip);
            return new Rect(inner.x, inner.y + barH, inner.width, inner.height - barH);
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

        /// <summary>Forgets what was kept of the places and reads them afresh, as Read again does, and says so.</summary>
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
