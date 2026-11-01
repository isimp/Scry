using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>Chips that go to other entries, coloured by their kind, and the Linked section.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>Goes to the entry a key names: a prefab by its name, or a status effect or raid in its namespace (<see cref="EntryKeys"/>).</summary>
        private static void Go(Explorer explorer, string target)
        {
            if (!explorer.Jump(target)) return;
            AfterGoing();
        }

        /// <summary>Puts a search in the box, with every other filter cleared so it shows all it finds.</summary>
        private static void SearchFor(Explorer explorer, string text)
        {
            explorer.SearchEverything(text);
            ListFiltered();
            RevealSelected();
        }

        /// <summary>Text drawn as a link: accent coloured, brighter under the mouse.</summary>
        private static void LinkLabel(Rect rect, string text, GUIStyle style) => LinkLabel(rect, text, style, Skin.Accent);

        /// <summary>Text that can be clicked: in its colour, brighter under the mouse.</summary>
        private static void LinkLabel(Rect rect, string text, GUIStyle style, Color colour)
        {
            Skin.LabelIn(rect, text, style, rect.Contains(Event.current.mousePosition) ? Skin.Lift(colour, 0.35f) : colour);
        }

        /// <summary>
        /// A chip that goes to an entry rather than doing something: tinted in the colour of the
        /// kind of entry it goes to, with an arrow. Brighter while what it names is playing. One
        /// that goes nowhere (the entry itself, or something not in the catalog) is plain.
        /// </summary>
        private static bool LinkChip(Rect rect, string text, Kind? kind, bool lit, bool go, Texture2D mark = null)
        {
            var floor = mark != null;
            var hover = go && rect.Contains(Event.current.mousePosition);
            Skin.PillBox(rect, go || lit ? LinkFill(kind, hover, lit) : Skin.Unlinked);

            // One going to what only the floor shows keeps the floor's mark at its end.
            var label = floor ? new Rect(rect.x, rect.y, rect.width - FloorRoom, rect.height) : rect;
            Skin.LabelIn(label, go ? GoText(text) : text, Skin.SmallCenter, go || lit ? LinkText(kind, hover || lit) : Skin.Dim);
            if (floor) PlateFloorMark(rect, mark);

            return go && GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        /// <summary>The room the floor's mark takes at the end of a chip or after a link (<see cref="FloorMark"/>).</summary>
        private static float FloorRoom => U(18f);

        /// <summary>The mark of what shows only by the player's choice: the floor's stairs, or a folded map for what a source of the world shows; none for anything else.</summary>
        private static Texture2D ChoiceMark(Explorer explorer, Entry entry) =>
            entry == null ? null : explorer.BookByFloor(entry) ? Skin.Steps : explorer.BookByWorld(entry) != null ? Skin.FoldedMap : null;

        /// <summary>The mark of what a key goes to, as <see cref="ChoiceMark(Explorer, Entry)"/>.</summary>
        private static Texture2D ChoiceMark(Explorer explorer, string key) => ChoiceMark(explorer, explorer.Find(key));

        /// <summary>Counts a mark drawn: the floor's and a source's alike as marks of the player's choice, a source's also as its own.</summary>
        private static void CountMark(Texture2D mark, PanelPart part)
        {
            CountDrawn(part);
            if (mark == Skin.FoldedMap) CountDrawn(PanelPart.SourceMark);
        }

        /// <summary>
        /// The mark of the player's choice, faint, as on a list row and under a page's title, after
        /// a chip or link to what shows as known only because of it (<see cref="ChoiceMark(Explorer, Entry)"/>).
        /// </summary>
        private static void FloorMark(float x, Rect line, Texture2D mark)
        {
            CountMark(mark, PanelPart.FloorLink);
            var size = U(14f);
            Skin.Icon(new Rect(x, line.y + (Mathf.Min(line.height, U(26f)) - size) / 2f, size, size), mark, Skin.Faint);
        }

        /// <summary>
        /// The floor's mark at a plate's end (<see cref="FloorMark"/>): in the plate's middle, as its
        /// icon and words are, however many lines it has, and as far in from its end as the icon
        /// is from its start.
        /// </summary>
        private static void PlateFloorMark(Rect plate, Texture2D mark)
        {
            CountMark(mark, PanelPart.FloorLink);
            var size = U(14f);
            Skin.Icon(new Rect(plate.xMax - U(6f) - size, plate.y + (plate.height - size) / 2f, size, size), mark, Skin.Faint);
        }

        /// <summary>A chip's or link's tip, the floor's or the source's line above it where what it goes to shows only by the player's choice (<see cref="Explorer.ChoiceTip"/>).</summary>
        private static string FloorTip(Explorer explorer, string key, string tip) => explorer.ChoiceTip(key, tip);

        /// <summary>The fill of anything that goes to an entry: a dark shade of its kind's colour.</summary>
        private static Color LinkFill(Kind? kind, bool hover, bool lit = false)
        {
            var colour = kind.HasValue ? Skin.KindColor(kind.Value) : Skin.Neutral;
            var fill = lit ? 0.55f : hover ? 0.36f : 0.22f;
            return Skin.Alpha(Skin.Shade(colour, fill), 0.95f);
        }

        /// <summary>The text of anything that goes to an entry: its kind's colour, lighter.</summary>
        private static Color LinkText(Kind? kind, bool hover)
        {
            var colour = kind.HasValue ? Skin.KindColor(kind.Value) : Skin.Neutral;
            return Skin.Lift(colour, hover ? 0.6f : 0.35f);
        }

        /// <summary>
        /// A chip's width. A name not measured before is measured within the frame's share, and
        /// guessed from its length past it, so opening a long list measures its names over a few
        /// frames rather than all in one.
        /// </summary>
        private static float LinkChipWidth(string text, bool go, bool floor = false) => Skin.WidthSoon(Skin.Small, go ? GoText(text) : text) + U(16f) + (floor ? FloorRoom : 0f);

        private static readonly Dictionary<string, string> GoTexts = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>A chip's text with the arrow of one that goes somewhere, made once for each name.</summary>
        private static string GoText(string text)
        {
            if (GoTexts.TryGetValue(text, out var shown)) return shown;
            if (GoTexts.Count > 20000) GoTexts.Clear();
            return GoTexts[text] = PanelWords.GoArrow(text);
        }

        /// <summary>The kind of the entry a key goes to (a prefab's name, or a key in a namespace), when it is in the catalog.</summary>
        private static Kind? KindOf(Explorer explorer, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_kindsFor != explorer)
            {
                _kindsFor = explorer;
                KindByKey.Clear();
                foreach (var e in explorer.Catalog) if (!KindByKey.ContainsKey(e.Key)) KindByKey[e.Key] = e.Kind;
            }
            return KindByKey.TryGetValue(key, out var kind) ? kind : (Kind?)null;
        }

        private static readonly Dictionary<string, Kind> KindByKey = new Dictionary<string, Kind>();
        private static Explorer _kindsFor;

        /// <summary>
        /// A small heading and a wrapping row of link chips, each with its own text, tip and doing;
        /// without the heading where the topic it stands in names it already. Where its names show
        /// once met (<paramref name="onceMet"/>), only those not met are left out under the strict rules.
        /// </summary>
        private static float LinkItems(Explorer explorer, string title, IEnumerable<(string Key, string Text, string Tip, Action Click)> items, float width, float y, bool titled = true, bool onceMet = false)
        {
            var given = items as IReadOnlyList<(string Key, string Text, string Tip, Action Click)> ?? items.ToList();
            var all = ShownStrictly(explorer, given, i => i.Key, onceMet);
            // A row left with nothing draws nothing, its title neither, so the next one takes its place.
            if (all.Count == 0)
            {
                if (given.Count > 0) CountDrawn(PanelPart.EmptyRowLeftOut);
                return y;
            }
            if (titled) GUI.Label(new Rect(0f, y, width, U(20f)), title, Skin.DimLabel);
            var flow = new ChipFlow(0f, width, titled ? y + U(24f) : y, U(26f), U(5f), U(5f));
            if (all.Count < given.Count) title = CompendiumWords.Uncounted(title);
            var key = "links:" + title;
            var count = ShownOf(key, all.Count);
            for (var i = 0; i < count; i++)
            {
                var item = all[i];
                var go = !Unseen(explorer, item.Key);
                var mark = go ? ChoiceMark(explorer, item.Key) : null;
                var w = Mathf.Min(width, LinkChipWidth(item.Text, go, mark != null));
                var at = flow.Place(w);
                var chip = new Rect(at.X, at.Y, w, flow.RowHeight);
                if (OutOfSight(chip)) continue;
                if (LinkChip(chip, item.Text, go ? KindOf(explorer, item.Key) : null, false, go, mark)) item.Click();
                if (go && chip.Contains(Event.current.mousePosition)) AskTip("link:" + title + item.Key + item.Text, FloorTip(explorer, item.Key, item.Tip ?? PanelWords.GoTo(item.Text)));
            }
            MoreChip(key, all.Count, FirstChips, width, ref flow);
            return flow.RowBottom + U(10f);
        }

        /// <summary>
        /// Everything else the entry is linked to, a row per heading: what it leaves behind or is
        /// left by, what it carries, its footsteps, the sounds its animations name, what it
        /// spawns, its set, its ammo, its status effects. A sound an animation names goes to the
        /// creature and plays that animation there.
        /// </summary>
        private static float LinksSection(Explorer explorer, Entry entry, float width, float y)
        {
            MakeLinkRowsFor(explorer, entry);
            // It holds the page's own parts and wiring, nothing of the world to find: under either
            // rules it names what the character met, counts none of it among the page's finds and
            // says nothing of what it leaves out, and with the compendium on it stands folded until opened.
            if (LinkRows.Count == 0) return y;
            var linked = LinkedCount(explorer);
            if (linked == 0) return y;

            var key = explorer.CompendiumOn ? FoldMarks.LinkedInBook : "links";
            y = SectionHeading("LINKED", width, y, null, key, linked);
            if (IsFolded(key)) return y;

            StrictSectionStarts();
            var was = PassingStarts(true);
            foreach (var (title, items) in LinkRows) y = LinkItems(explorer, title, items, width, y, onceMet: true);
            PassingEnds(was);
            return y + U(4f);
        }

        /// <summary>How many links the section shows, those the rules leave out not counted; worked out again only as what the character knows changes.</summary>
        private static int LinkedCount(Explorer explorer)
        {
            if (_linkedStamp == explorer.KnowStamp && _linkedCountFor == _linksFor) return _linkedCount;
            _linkedStamp = explorer.KnowStamp;
            _linkedCountFor = _linksFor;
            _linkedCount = 0;
            foreach (var (_, items) in LinkRows) foreach (var item in items) if (!explorer.LeavesOut(item.Key, true)) _linkedCount++;
            return _linkedCount;
        }

        private static int _linkedStamp = -1;
        private static Entry _linkedCountFor;

        /// <summary>
        /// The link rows of the entry shown: those for LINKED, and those a topic of In the game
        /// shows instead (<see cref="FactLayout.Places"/>). Made once for the entry shown, not for
        /// every event the panel draws.
        /// </summary>
        private static void MakeLinkRowsFor(Explorer explorer, Entry entry)
        {
            if (ReferenceEquals(_linksFor, entry) && ReferenceEquals(_linksIn, explorer)) return;
            _linksFor = entry;
            _linksIn = explorer;
            LinkRows.Clear();
            PlacedLinkRows.Clear();
            MakeLinkRows(explorer, entry, LinkRows, PlacedLinkRows);
            _linkedStamp = -1;
        }

        /// <summary>
        /// A group of links a topic of In the game shows, its heading left out where it is all the
        /// topic holds. With the compendium on, what a creature carries that its attack table names
        /// already, its own attacks, is passed over rather than counted as left to know, as the
        /// table tells them.
        /// </summary>
        private static float PlacedLinks(Explorer explorer, Entry entry, Facts facts, string group, bool titled, float width, float y)
        {
            MakeLinkRowsFor(explorer, entry);
            if (!PlacedLinkRows.TryGetValue(group, out var items)) return y;
            CountDrawn(PanelPart.TopicLinks);
            if (group == LinkWords.Carries && explorer.NamesOnlyMet) items = CarriedBesidesAttacks(facts, items);
            return LinkItems(explorer, FactLayout.LinkTitle(entry.Kind, group), items, width, y, titled, FactLayout.ShownOnceMet(entry.Kind, group));
        }

        /// <summary>What a creature carries besides the attacks its table names, made once for its facts.</summary>
        private static List<(string Key, string Text, string Tip, Action Click)> CarriedBesidesAttacks(Facts facts, List<(string Key, string Text, string Tip, Action Click)> items)
        {
            if (ReferenceEquals(_carriedOf, facts) && ReferenceEquals(_carriedFrom, items)) return _carried;
            var attacks = EntryKeys.ItemsAttacking(facts.Rows.Where(r => r.AttackCells != null).SelectMany(r => r.Lines.Select(l => l.Link)));
            _carriedOf = facts;
            _carriedFrom = items;
            _carried = items.Where(i => !attacks.Contains(i.Key)).ToList();
            return _carried;
        }

        private static Facts _carriedOf;
        private static List<(string Key, string Text, string Tip, Action Click)> _carriedFrom, _carried;

        /// <summary>Whether LINKED shows a group of the entry shown, for the self-test.</summary>
        [ForTest]
        public static bool LinkedShows(string group) => LinkRows.Exists(r => r.Title == group);

        /// <summary>The rows LINKED holds for an entry as its page makes them, each its group and what each chip goes to, before any rules leave some out, for the self-test's tally.</summary>
        [ForTest]
        public static List<(string Group, List<string> Targets)> LinkedRowsForTest(Explorer explorer, Entry entry)
        {
            var rows = new LinkRowList();
            MakeLinkRows(explorer, entry, rows, new PlacedRows());
            return rows.ConvertAll(r => (r.Title, r.Items.ConvertAll(i => i.Key)));
        }

        /// <summary>
        /// Adds a row of links: to In the game's where a topic shows its group, else to LINKED
        /// without what In the game links already, and not at all with nothing left.
        /// </summary>
        private static void AddLinkRow(Entry entry, string title, List<(string Key, string Text, string Tip, Action Click)> items, ICollection<string> shown, LinkRowList rows, PlacedRows placed)
        {
            if (FactLayout.Places(entry.Kind, title))
            {
                var kept = FactLayout.LeftInTopic(entry.Kind, title, items.Select(i => i.Key).ToList(), shown);
                if (kept.Count > 0) placed[title] = kept.Count == items.Count ? items : kept.Select(i => items[i]).ToList();
                return;
            }
            var left = FactLayout.LeftInLinked(entry.Kind, title, items.Select(i => i.Key).ToList(), shown);
            if (left.Count > 0) rows.Add((title, left.Select(i => items[i]).ToList()));
        }

        /// <summary>Rows of links, each its title and chips: those of LINKED.</summary>
        private sealed class LinkRowList : List<(string Title, List<(string Key, string Text, string Tip, Action Click)> Items)>
        {
        }

        /// <summary>Rows of links a topic of In the game shows, by their group.</summary>
        private sealed class PlacedRows : Dictionary<string, List<(string Key, string Text, string Tip, Action Click)>>
        {
            public PlacedRows() : base(StringComparer.Ordinal)
            {
            }
        }

        /// <summary>The link rows a topic of In the game shows, by their group.</summary>
        private static readonly PlacedRows PlacedLinkRows = new PlacedRows();

        private static Entry _linksFor;
        private static Explorer _linksIn;

        /// <summary>How many things the section links, counted with its rows.</summary>
        private static int _linkedCount;

        private static readonly LinkRowList LinkRows = new LinkRowList();

        /// <summary>The rows of the LINKED section, each chip with its text, tip and what it does.</summary>
        private static void MakeLinkRows(Explorer explorer, Entry entry, LinkRowList rows, PlacedRows placed)
        {
            // What In the game links already is not linked again.
            var shown = Facts.For(entry).LinkedTargets();
            void Plain(string title, List<string> names)
            {
                var told = Naming.TellApart(names.Select(n => (ShownName(explorer, n, n), n)).ToList());
                AddLinkRow(entry, title, names.Select((n, i) => (n, told[i], (string)null, (Action)(() => Go(explorer, n)))).ToList(), shown, rows, placed);
            }
            if (entry.LeftBy.Count > 0) Plain("Left behind by", entry.LeftBy);
            if (entry.LeavesBehind.Count > 0) Plain("Leaves behind", entry.LeavesBehind);

            foreach (var group in entry.LinkGroups())
            {
                var links = group.Value;
                var title = group.Key;

                if (group.Key == Relations.PlayedByAnimation)
                {
                    // One chip per animation, which goes to the creature and plays it.
                    var items = links.SelectMany(l => l.Notes.Count > 0 ? l.Notes.Select(n => (l.Target, n)) : new[] { (l.Target, "") })
                        .Select(p =>
                        {
                            var shown = ShownName(explorer, p.Target, p.Target);
                            var text = LinkWords.Animation(shown, p.Item2);
                            var tip = LinkWords.AnimationTip(shown, p.Item2);
                            return (p.Target, text, tip, (Action)(() =>
                            {
                                if (p.Item2.Length > 0) Previews.PlayClipOnShow(p.Item2);
                                Go(explorer, p.Target);
                            }));
                        });
                    // A chip here plays an animation too, which nothing in In the game does.
                    AddLinkRow(entry, title, items.ToList(), Array.Empty<string>(), rows, placed);
                    continue;
                }

                // How an item gives an effect is told in its facts' words; the notes keep the field's for grouping.
                var giver = group.Key == Relations.GivenBy;
                var told = Naming.TellApart(links.Select(l => (ShownName(explorer, l.Target, l.Target), l.Target)).ToList());
                AddLinkRow(entry, title, links.Select((l, i) =>
                {
                    var name = told[i];
                    var notes = giver ? l.Notes.Select(Groups.GiverWords).ToList() : l.Notes;
                    return (l.Target, LinkWords.Chip(name, notes), LinkWords.Tip(name, notes), (Action)(() => Go(explorer, l.Target)));
                }).ToList(), shown, rows, placed);
            }
        }

        /// <summary>A small heading and a wrapping row of chips, each doing its own thing when clicked; one with nothing to do is plain.</summary>
        private static float ChipRow(string title, IEnumerable<KeyValuePair<string, Action>> chips, float width, float y)
        {
            var all = chips.ToList();
            // A row left with nothing, as biomes the strict rules leave out, draws nothing.
            if (all.Count == 0)
            {
                CountDrawn(PanelPart.EmptyRowLeftOut);
                return y;
            }
            GUI.Label(new Rect(0f, y, width, U(20f)), title, Skin.DimLabel);
            var flow = new ChipFlow(0f, width, y + U(24f), U(26f), U(5f), U(5f));
            var key = "chips:" + title;
            foreach (var chip in all.Take(ShownOf(key, all.Count)))
            {
                var w = Mathf.Min(width, Skin.Width(Skin.Chip, chip.Key) + U(8f));
                var at = flow.Place(w);
                var rect = new Rect(at.X, at.Y, w, flow.RowHeight);
                if (OutOfSight(rect)) continue;
                if (chip.Value != null && rect.Contains(Event.current.mousePosition)) AskTip("chips:" + title + chip.Key, PanelWords.GoTo(chip.Key));
                if (chip.Value == null) LinkChip(rect, chip.Key, null, false, false);
                else if (GUI.Button(rect, chip.Key, Skin.Fitted(Skin.Chip, rect))) chip.Value();
            }
            MoreChip(key, all.Count, FirstChips, width, ref flow);
            return flow.RowBottom + U(10f);
        }

        private static readonly Dictionary<string, Sprite> PrefabIcons = new Dictionary<string, Sprite>();
        private static HashSet<string> _catalogNames;
        private static Explorer _namesFor;

        /// <summary>
        /// Builds the lookups the side pane uses over the whole catalog (names in it, their kinds,
        /// the status effects, the chains) one a frame after the catalog is read, so the first entry
        /// selected does not build them all at once.
        /// </summary>
        public static void Prepare(Explorer explorer)
        {
            if (explorer == null) return;

            // Reading the locations adds values to search by (in:): the terms are made again on a
            // worker thread started here, and taken up here once done, rather than on a keystroke.
            if (ReferenceEquals(explorer, _prepared))
            {
                if (!Assist.HasTermsFor(explorer) && Locations.Now != Locations.State.Reading) Assist.TermsFor(explorer);
                return;
            }
            if (!ReferenceEquals(explorer, _preparing))
            {
                _preparing = explorer;
                _prepareStep = 0;
            }
            switch (_prepareStep++)
            {
                case 0: InCatalog(explorer, ""); break;
                case 1: KindOf(explorer, "-"); break;
                case 2: Assist.TermsFor(explorer); break;
                case 3: Chains.Of(""); break;
                default: _prepared = explorer; break;
            }
        }

        private static Explorer _preparing;
        private static Explorer _prepared;
        private static int _prepareStep;

        /// <summary>Whether the entry a key names (a prefab's name, or a key in a namespace) is in the catalog to go to.</summary>
        private static bool InCatalog(Explorer explorer, string key)
        {
            if (_namesFor != explorer || _catalogNames == null)
            {
                _namesFor = explorer;
                _catalogNames = new HashSet<string>(explorer.Catalog.Select(e => e.Key));
                PrefabIcons.Clear();
            }
            return key != null && _catalogNames.Contains(key);
        }

        /// <summary>
        /// Whether a chip naming the entry a key names goes there: it is in the catalog and the
        /// character has seen it (<see cref="Explorer.Reaches"/>). Else the name is plain text.
        /// </summary>
        private static bool Goes(Explorer explorer, string key) => InCatalog(explorer, key) && !Unseen(explorer, key);

        /// <summary>Whether a key names an entry of the catalog the character has not seen: told by its name only, without an icon, going nowhere.</summary>
        private static bool Unseen(Explorer explorer, string key)
        {
            if (!LeftOut(explorer, key)) return false;
            // A mod is always known: one the compendium lists nowhere is named plainly, no name kept back.
            if (KindOf(explorer, key) != Kind.Mod) CountDrawn(PanelPart.UnseenName);
            return true;
        }

        /// <summary>Whether a list of what there is leaves out the entry a key names: one in the catalog the character has not seen.</summary>
        private static bool LeftOut(Explorer explorer, string key) => InCatalog(explorer, key) && !explorer.Reaches(key) && !explorer.NamedAsHeld(explorer.Find(key));

        /// <summary>A text as the page shown would draw it (<see cref="Masked"/>), for the self-test.</summary>
        [ForTest]
        public static string AsDrawn(Explorer explorer, string text) => Masked(explorer, text);

        private static NameMask _mask;
        private static Explorer _maskIn;
        private static int _maskStamp = -1;
        private static int _maskSelection = -1;
        private static readonly Dictionary<string, string> MaskedTexts = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// A known page's text with the compendium on, the names of what the character has not
        /// met kept back (<see cref="NameMask"/>); as it is otherwise. The mask is made again when
        /// what the character knows changes, each text masked once.
        /// </summary>
        private static string Masked(Explorer explorer, string text, bool always = false)
        {
            // The page's text masks on a known page alone; a list's row masks whatever page is shown (always).
            if (string.IsNullOrEmpty(text) || !(always || explorer.NamesOnlyMet)) return text;
            // The page's too: what a room or place shown whole names as held is met for its page alone (Explorer.NamedAsHeld).
            if (!ReferenceEquals(_maskIn, explorer) || _maskStamp != explorer.KnowStamp || _maskSelection != explorer.SelectionVersion)
            {
                _maskIn = explorer;
                _maskStamp = explorer.KnowStamp;
                _maskSelection = explorer.SelectionVersion;
                MaskedTexts.Clear();
                var unseen = new List<string>();
                var seen = new List<string>();
                foreach (var entry in explorer.Catalog)
                {
                    if (!Maskable(entry)) continue;
                    // Named too as one of all a room or place shown whole holds (Explorer.NamedAsHeld), as the chips name it.
                    var met = explorer.LevelOf(entry) >= KnownLevel.Seen || explorer.NamedAsHeld(entry);
                    // A skill's name is mostly a word of what one does (run, swim, sneak), so text keeps it; facts naming one not learned are left out instead.
                    if (!met && entry.Kind == Kind.Skill) continue;
                    (met ? seen : unseen).Add(entry.ShownName);
                }
                _mask = new NameMask(unseen, seen);
            }
            if (MaskedTexts.TryGetValue(text, out var masked)) return masked;
            if (MaskedTexts.Count > 4000) MaskedTexts.Clear();
            return MaskedTexts[text] = _mask.Apply(text);
        }

        /// <summary>
        /// Whether a page's text keeps back an entry's name while it is not met: only the names of
        /// the world a player meets. An effect, a sound or a projectile is never met, and its name
        /// ("Fire") is often a plain word of the text ("fires"); a name made of a prefab's words
        /// ("Rock heath", the Swamp's "Well") is no name a text holds.
        /// </summary>
        public static bool Maskable(Entry entry) => NameMask.Keeps(entry, entry.Source is PlaceSource place && Places.NamedByWords(place.Contents, place.IsRoom));

        /// <summary>How many things the rules left out of the section being drawn as never met, and as met but not seen together, for its last lines (<see cref="StrictLeftLine"/>).</summary>
        private static int _strictUnmet, _strictApart;

        /// <summary>The kinds of the things counted as never met, and as met but not seen together, where known, for the lines' hovers to count them by kind.</summary>
        private static readonly List<Kind> StrictUnmetKinds = new List<Kind>(), StrictApartKinds = new List<Kind>();

        /// <summary>A section begins: what the strict rules leave out is counted afresh.</summary>
        private static void StrictSectionStarts()
        {
            _strictUnmet = 0;
            _strictApart = 0;
            StrictUnmetKinds.Clear();
            StrictApartKinds.Clear();
            _recipesWait = 0;
            _strictNamed = 0;
        }

        /// <summary>How many things the section being drawn named, for its count line to say "N not yet met" alone where it named none.</summary>
        private static int _strictNamed;

        /// <summary>How many recipe rows the section being drawn left out, waiting for the game to list them.</summary>
        private static int _recipesWait;

        /// <summary>Counts a recipe row the section being drawn leaves out, waiting for the game to list it, for its last line.</summary>
        private static void RecipeWaiting() => _recipesWait++;

        /// <summary>
        /// Counts things the rules left out of the section being drawn, those never met apart from
        /// those met but not seen together, and of the page unless they show only while a list
        /// plays (<see cref="_passing"/>).
        /// </summary>
        private static void StrictLeft(int unmet, int apart)
        {
            if (unmet <= 0 && apart <= 0) return;
            _strictUnmet += Math.Max(0, unmet);
            _strictApart += Math.Max(0, apart);
            if (!_passing) _drawLeft += Math.Max(0, unmet) + Math.Max(0, apart);
        }

        /// <summary>Counts names the rules left out of the section being drawn, each as never met or as met but not seen together (<see cref="Explorer.NotMet"/>).</summary>
        private static void StrictLeft(Explorer explorer, IEnumerable<string> keys)
        {
            var unmet = 0;
            var apart = 0;
            foreach (var key in keys)
            {
                var notMet = explorer.NotMet(key);
                if (notMet) unmet++;
                else apart++;
                StrictKind(explorer, key, notMet);
            }
            StrictLeft(unmet, apart);
        }

        /// <summary>Counts one name the rules left out of the section being drawn (<see cref="StrictLeft(Explorer, IEnumerable{string})"/>).</summary>
        private static void StrictLeft(Explorer explorer, string key) => StrictLeft(explorer, new[] { key });

        /// <summary>Keeps the kind of a name left out of the section being drawn, never met or met, where it is known, for its line's hover.</summary>
        private static void StrictKind(Explorer explorer, string key, bool notMet)
        {
            if (explorer.Find(key) is Entry entry) (notMet ? StrictUnmetKinds : StrictApartKinds).Add(entry.Kind);
        }

        /// <summary>Whether what is drawn shows only while a list plays, the parts of the effect list or clip played last: no part of the page's own count.</summary>
        private static bool _passing;

        /// <summary>The names the details' draw going on left out under the strict rules, of the page's own.</summary>
        private static int _drawLeft;

        /// <summary>
        /// The names the last whole draw of an entry's details left out under the strict rules, the
        /// sections' last lines together, and whether a section that names things was folded, so not
        /// counted; for the self-test to hold against the page's count (<see cref="PageNames"/>).
        /// </summary>
        [Diagnostic]
        public static (Entry Of, int Left, bool Folded) NamesDrawnForTest { get; private set; }

        /// <summary>How many names the page last gathered leaves out where a section's last line counts them, for the self-test.</summary>
        [Diagnostic]
        public static int NamesLinedForTest => _finds.Lined;

        /// <summary>A draw of an entry's details begins: nothing left out yet.</summary>
        private static void StartNameCount() => _drawLeft = 0;

        /// <summary>What is drawn from here shows only while a list plays, or not; what it was before, to put back (<see cref="PassingEnds"/>).</summary>
        private static bool PassingStarts(bool passing)
        {
            var was = _passing;
            _passing = passing;
            return was;
        }

        /// <summary>Puts back whether what is drawn shows only while a list plays, as it was before (<see cref="PassingStarts"/>).</summary>
        private static void PassingEnds(bool was) => _passing = was;

        /// <summary>A whole draw of an entry's details ended: what it left out is the page's.</summary>
        private static void EndNameCount(Entry entry) => NamesDrawnForTest = (entry, _drawLeft, NamingSectionFolded());

        /// <summary>The sections a page's names stand in, which the strict rules leave out of.</summary>
        private static readonly string[] NamingSections = { "facts", "playsin" };

        /// <summary>Whether a section of the details that names things is folded, so what it would leave out goes uncounted in a draw.</summary>
        private static bool NamingSectionFolded()
        {
            foreach (var section in NamingSections) if (IsFolded(section)) return true;
            return false;
        }

        /// <summary>
        /// What the section just drawn left out, said faintly at its end by what the section is
        /// about, what was never met apart from what was met but not seen together, each line's
        /// hover saying the way to it (<see cref="Explorer.LeftOutLines"/>); nothing where nothing
        /// was. Starts the count afresh for the next section.
        /// </summary>
        private static float StrictLeftLine(Explorer explorer, string topic, float width, float y, string name = null)
        {
            var (unmet, apart) = (_strictUnmet, _strictApart);
            var waiting = _recipesWait;
            _strictUnmet = 0;
            _strictApart = 0;
            _recipesWait = 0;
            var lines = explorer.NamesOnlyMet ? LeftOutLinesOf(explorer, topic, unmet, apart, name) : null;
            StrictUnmetKinds.Clear();
            StrictApartKinds.Clear();
            if (lines == null) return y;
            // Names left out alone are said here, as they are gone from the page; a weak spot not
            // found, as every number held back, is in the You box's list. A recipe row waiting for
            // the game to list it is said the same way, so the page does not go silent on it.
            for (var i = 0; i < lines.Count; i++) y = HintLine(lines[i].Line, "strict-left:", lines[i].Line, PanelPart.StrictLeftOut, width, y, lines[i].Tip);
            return HintLine(waiting > 0 ? CompendiumWords.RecipeRowsWait(waiting) : null, "recipe-wait:", topic, PanelPart.RecipeWaits, width, y);
        }

        /// <summary>
        /// A section's left-out lines (<see cref="Explorer.LeftOutLines"/>), worked out once for what
        /// they say and kept, as the section is drawn again each repaint with the same counts: by the
        /// section, its counts, whether it names anything, the page's name, the rules, and the kinds
        /// counted (<see cref="KindsSum"/>).
        /// </summary>
        private static List<(string Line, string Tip)> LeftOutLinesOf(Explorer explorer, string topic, int unmet, int apart, string name)
        {
            var key = (topic, unmet, apart, _strictNamed == 0, name, explorer.LeavingOut, KindsSum(StrictUnmetKinds), KindsSum(StrictApartKinds));
            if (LeftOutLineCache.TryGetValue(key, out var lines)) return lines;
            if (LeftOutLineCache.Count > 256) LeftOutLineCache.Clear();
            lines = explorer.LeftOutLines(topic, unmet, apart, alone: key.Item4, name: name, unmetKinds: StrictUnmetKinds.ToArray(), apartKinds: StrictApartKinds.ToArray());
            LeftOutLineCache[key] = lines;
            return lines;
        }

        private static readonly Dictionary<(string, int, int, bool, string, bool, long, long), List<(string Line, string Tip)>> LeftOutLineCache =
            new Dictionary<(string, int, int, bool, string, bool, long, long), List<(string Line, string Tip)>>();

        /// <summary>
        /// The kinds a line counts as one number, the same whatever their order, as the line's hover
        /// counts them by kind and not by order: each kind adds its own scrambled number, so two
        /// different sets of counts practically never add up alike.
        /// </summary>
        private static long KindsSum(List<Kind> kinds)
        {
            var sum = 0UL;
            foreach (var kind in kinds) sum = unchecked(sum + Scrambled((ulong)kind + 1UL));
            return unchecked((long)sum);
        }

        /// <summary>A kind's number in <see cref="KindsSum"/>, scrambled so that sums of different counts do not line up (the finishing step of SplitMix64).</summary>
        private static ulong Scrambled(ulong x)
        {
            unchecked
            {
                x *= 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }

        /// <summary>
        /// A line telling what is still to find with the compendium on (what was not met or seen
        /// together, a number kept back, what is still to unlock), set apart from a page's footnotes,
        /// which stay faint: an accent bar and a compass at its start, its words a step brighter; its
        /// whole text on hover where it does not fit, the tip told apart by its kind and what it is
        /// of; nothing for none.
        /// </summary>
        private static float HintLine(string said, string tipKind, string tipOf, PanelPart part, float width, float y, string tip = null)
        {
            if (said == null) return y;
            CountDrawn(part);
            CountDrawn(PanelPart.HintLine);
            Skin.Fill(new Rect(U(6f), y + U(4f), U(2f), U(16f)), Skin.Accent);
            Skin.Icon(new Rect(U(12f), y + U(5f), U(14f), U(14f)), Skin.Compass, Skin.Accent);
            var line = new Rect(U(30f), y + U(2f), width - U(30f), U(20f));
            // On hover the whole line where it was cut, and what frees what it counts, where that is told.
            var fit = FitLabel(line, said, Skin.HintLabel, 10f);
            if ((!fit || tip != null) && line.Contains(Event.current.mousePosition)) AskTip(tipKind + tipOf, Naming.Lines(fit ? null : said, tip));
            return y + U(24f);
        }

        private static readonly Dictionary<object, object> StrictLists = new Dictionary<object, object>();
        private static Explorer _strictIn;
        private static int _strictStamp;
        private static int _strictSelection;

        /// <summary>
        /// What a list on the page shown keeps with the compendium on (<see cref="Explorer.LeavesOut(string)"/>):
        /// what the character has met, under the strict rules only what was seen together with the
        /// page or is part of it; all of it otherwise. Worked out
        /// once for each list while what the character knows and the page stay the same. What it
        /// leaves out is counted for the section's last line (<see cref="StrictLeft(int, int)"/>).
        /// </summary>
        private static IReadOnlyList<T> ShownStrictly<T>(Explorer explorer, IReadOnlyList<T> items, Func<T, string> key, bool gameShown = false)
        {
            return ShownStrictly(explorer, items, key, item => explorer.LeavesOut(key(item), gameShown));
        }

        /// <summary>
        /// What a list keeps under the strict rules, each item left out as it says
        /// (<see cref="ShownStrictly{T}(Explorer, IReadOnlyList{T}, Func{T, string}, bool)"/>), what
        /// it leaves out counted by its key as never met or as met but not seen together.
        /// </summary>
        private static IReadOnlyList<T> ShownStrictly<T>(Explorer explorer, IReadOnlyList<T> items, Func<T, string> key, Func<T, bool> leftOut)
        {
            if (!explorer.NamesOnlyMet) return items;
            if (!ReferenceEquals(_strictIn, explorer) || _strictStamp != explorer.KnowStamp || _strictSelection != explorer.SelectionVersion || StrictLists.Count > 256)
            {
                _strictIn = explorer;
                _strictStamp = explorer.KnowStamp;
                _strictSelection = explorer.SelectionVersion;
                StrictLists.Clear();
            }
            if (StrictLists.TryGetValue(items, out var had))
            {
                var (kept, unmet, unmetKinds, apartKinds) = ((IReadOnlyList<T>, int, Kind[], Kind[]))had;
                StrictLeft(unmet, items.Count - kept.Count - unmet);
                StrictUnmetKinds.AddRange(unmetKinds);
                StrictApartKinds.AddRange(apartKinds);
                _strictNamed += kept.Count;
                return kept;
            }
            var keeping = new List<T>();
            var neverMet = 0;
            var unmetCount = StrictUnmetKinds.Count;
            var apartCount = StrictApartKinds.Count;
            foreach (var item in items)
            {
                if (!leftOut(item))
                {
                    keeping.Add(item);
                    continue;
                }
                var notMet = explorer.NotMet(key(item));
                if (notMet) neverMet++;
                StrictKind(explorer, key(item), notMet);
            }
            IReadOnlyList<T> shown = keeping.Count == items.Count ? items : keeping;
            _strictNamed += shown.Count;
            StrictLists[items] = (shown, neverMet, StrictUnmetKinds.Skip(unmetCount).ToArray(), StrictApartKinds.Skip(apartCount).ToArray());
            StrictLeft(neverMet, items.Count - shown.Count - neverMet);
            return shown;
        }

        private static readonly Dictionary<object, object> ListedLists = new Dictionary<object, object>();
        private static Explorer _listedIn;
        private static int _listedStamp;

        /// <summary>
        /// What a list of what there is shows: all of it, or with the compendium on only what the
        /// character has seen, worked out once for each list while what it knows stays the same.
        /// </summary>
        private static List<T> Listed<T>(Explorer explorer, List<T> items, Func<T, string> key)
        {
            if (!explorer.CompendiumOn) return items;
            if (!ReferenceEquals(_listedIn, explorer) || _listedStamp != explorer.KnowStamp || ListedLists.Count > 256)
            {
                _listedIn = explorer;
                _listedStamp = explorer.KnowStamp;
                ListedLists.Clear();
            }
            if (ListedLists.TryGetValue(items, out var had)) return (List<T>)had;
            var kept = items.Where(i => !LeftOut(explorer, key(i))).ToList();
            ListedLists[items] = kept;
            return kept;
        }

        /// <summary>The icon of an item or piece prefab by name, or null.</summary>
        private static Sprite PrefabIcon(string prefab)
        {
            if (PrefabIcons.TryGetValue(prefab, out var known)) return known;
            Sprite icon = null;
            var go = GamePrefabs.Named(prefab);
            if (go != null)
            {
                var icons = go.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared?.m_icons;
                if (icons != null && icons.Length > 0) icon = icons[0];
                else icon = go.GetComponent<Piece>().OrNull()?.m_icon;
            }
            PrefabIcons[prefab] = icon;
            return icon;
        }

        private static void DrawSprite(Sprite sprite, Rect rect)
        {
            if (sprite == null || sprite.texture == null || Event.current.type != EventType.Repaint) return;
            if (!SpriteUv(sprite, out var uv, out _)) return;
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
        }

        /// <summary>
        /// Where a sprite sits on its texture, and its rectangle there; false for one packed in a
        /// way that has no simple rectangle, which is then drawn without its icon.
        /// </summary>
        private static bool SpriteUv(Sprite sprite, out Rect uv, out Rect rect)
        {
            uv = rect = default;
            // Unity has no rectangle for one packed tightly (Sprite.textureRect throws for it).
            var t = sprite.texture;
            if (t == null || (sprite.packed && sprite.packingMode == SpritePackingMode.Tight)) return false;
            rect = sprite.textureRect;
            uv = new Rect(rect.x / t.width, rect.y / t.height, rect.width / t.width, rect.height / t.height);
            return true;
        }

        /// <summary>Lets go of the linked rows, icons and names made of the world left.</summary>
        private static void ForgetLinks()
        {
            PrefabIcons.Clear();
            KindByKey.Clear();
            _catalogNames = null;
            _preparing = null;
            _prepared = null;
            _linksFor = null;
            _linkedCountFor = null;
            _linkedStamp = -1;
            _linksIn = null;
            LinkRows.Clear();
            _kindsFor = null;
            _namesFor = null;
            ListedLists.Clear();
            _listedIn = null;
            StrictLists.Clear();
            _strictIn = null;
            MaskedTexts.Clear();
            _mask = null;
            _maskIn = null;
        }
    }
}
