using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    internal static partial class ScryPanel
    {
        /// <summary>
        /// What the card of the room gone into shows, worked out again as the room, what is read of
        /// it or what the character knows changes; its rows laid out again as its width or the
        /// lines that fit change.
        /// </summary>
        private sealed class RoomCardShown
        {
            public PlacedRoom Room;
            public int Read, Rooms, Know;
            public bool Strict, Trims;

            public string Name, Key, Where;
            public bool Goes;
            public List<(string Line, string Page)> Creatures = new List<(string, string)>();
            public List<(string Name, string Odds, string Page)> Loot = new List<(string, string, string)>();
            public int CreaturesLeft, LootLeft;

            /// <summary>The faint lines saying how many creatures and how many chests and pickups were left out, in the words of the rules; none for none.</summary>
            public string CreaturesLeftLine, LootLeftLine;

            /// <summary>How many creatures and how many chests and pickups this copy rolled in the room, those left out with them, as its folded lists say.</summary>
            public int CreatureCount, LootCount;

            /// <summary>A room of the world's own dungeon, shown by its layout alone: no lists of creatures or of chests and pickups.</summary>
            public bool LayoutOnly;
            public List<WayOn> Ways = new List<WayOn>();
            public List<float> Levels = new List<float>();
            public List<string> LevelWords = new List<string>();
            public float Ground;

            /// <summary>The rows as laid out for so many lines and a width: each list fitted, the last line saying how many more.</summary>
            public int LaidMost = -1;
            public float LaidWidth = -1f;
            public List<(string Name, string Odds, bool More, string Page)> CreatureRows, LootRows;
            public float WaysTall, LevelsTall;
        }

        private static RoomCardShown _roomCard;

        /// <summary>The room a way chip of the card is under the mouse for, lit on the stage and the plan; none.</summary>
        private static PlacedRoom _cardLit;

        /// <summary>How far the card is scrolled, and the room it is scrolled for, so another room's card opens at its top.</summary>
        private static Vector2 _cardScroll;
        private static PlacedRoom _cardScrollFor;

        /// <summary>How wide the card's scrollbar is with its margin.</summary>
        private static float CardScrollbar => U(12f);

        /// <summary>Whether the card scrolled when last drawn, wanting more than it had over the plan box, for the self-test.</summary>
        [Diagnostic]
        public static bool RoomCardScrolls { get; private set; }

        /// <summary>The room the self-test lights as hovering a way chip does, kept lit until it puts it out.</summary>
        private static PlacedRoom _cardLitForTest;

        /// <summary>What the card says, line by line as drawn, for the self-test; empty while none is drawn.</summary>
        private static readonly List<string> CardLines = new List<string>();

        /// <summary>The ways on the card shows, in its order, for the self-test to go through one as a click does.</summary>
        [Diagnostic, ForTest]
        public static IReadOnlyList<WayOn> RoomCardWays => _roomCard != null && Stage.RoomGoneInto == _roomCard.Room ? _roomCard.Ways : new List<WayOn>();

        /// <summary>What the card says, line by line, for the self-test.</summary>
        [Diagnostic, ForTest]
        public static IReadOnlyList<string> RoomCardLines => CardLines;

        /// <summary>Clicks a way chip of the card as the mouse does, for the self-test.</summary>
        [Diagnostic]
        public static void ClickCardWayForTest(WayOn way) => Stage.GoInto(way.To);

        /// <summary>Lights a room as hovering a way chip does, for the self-test; none puts it out.</summary>
        [Diagnostic]
        public static void LightCardWayForTest(PlacedRoom room) => _cardLitForTest = room;

        /// <summary>The card's contents for the room gone into, made again only when something it shows has changed.</summary>
        private static RoomCardShown CardOf(Explorer explorer, PlacedRoom room)
        {
            var card = _roomCard;
            if (card != null && card.Room == room && card.Read == Stage.ExampleRoomsRead && card.Rooms == Stage.ExampleRoomsShown
                && card.Know == explorer.KnowStamp && card.Strict == explorer.LeavingOut && card.Trims == explorer.NamesOnlyMet) return card;

            var ways = Stage.ExampleWays;
            card = new RoomCardShown { Room = room, Read = Stage.ExampleRoomsRead, Rooms = Stage.ExampleRoomsShown, Know = explorer.KnowStamp, Strict = explorer.LeavingOut, Trims = explorer.NamesOnlyMet, LayoutOnly = ExampleLayouts.ShowsWorlds };
            string RoomName(PlacedRoom r) => BoxRoomName(explorer, r);
            card.Key = EntryKeys.For(Kind.Location, room.Room.Name);
            card.Name = RoomName(room);
            card.Goes = Goes(explorer, card.Key);
            card.Ground = Stage.ExampleRoomGround(room);
            var entrance = ways?.Entrance;
            var rise = entrance != null ? card.Ground - Stage.ExampleRoomGround(entrance) : 0f;
            card.Where = RoomCardWords.Where(rise, ways != null ? ways.StepsIn(room) : -1, ways != null ? ways.WaysOf(room).Count : 0);

            // What the character has not met is left out and counted, under the strict rules also what was not seen together with the page.
            var creatures = Stage.RoomCreatures(room);
            var kept = creatures.Where(c => !explorer.LeavesOut(c)).ToList();
            card.CreaturesLeft = creatures.Count - kept.Count;
            card.CreaturesLeftLine = explorer.LeftOutLine("room", card.CreaturesLeft, alone: kept.Count == 0);
            card.CreatureCount = kept.Count;
            card.Creatures = RoomCardWords.CountedPages(kept.Select(c => (ShownName(explorer, c, CreatureName(c)), c)));

            var loot = Stage.RoomLoot(room);
            var shown = loot.Where(l => !explorer.LeavesOut(l.Prefab)).ToList();
            card.LootLeft = loot.Count - shown.Count;
            card.LootLeftLine = explorer.LeftOutLine("room", card.LootLeft, alone: shown.Count == 0);
            card.LootCount = shown.Count;
            card.Loot = RoomCardWords.OddsPages(shown.Select(l => (ShownName(explorer, l.Prefab, PartName(l.Prefab)), l.Prefab, l.Chance)));

            if (ways != null) card.Ways = RoomCard.WaysOn(ways, room, Stage.ExampleRoomGround, RoomName);
            card.Levels = Stage.ExampleRoomLevels(room);
            card.LevelWords = card.Levels.Select(l => RoomCardWords.Floor(l, card.Ground)).ToList();
            _roomCard = card;
            return card;
        }

        /// <summary>The peek last worked out, for the room it is of, as the mouse rests on a room.</summary>
        private static PlacedRoom _peekFor;
        private static string _peekText;
        private static int _peekRead = -1;
        private static bool _peekInside;

        /// <summary>The peek on a room of a dungeon's example under the mouse (<see cref="StageWords.RoomPeek"/>), worked out again only for another room, more of the example read, or the room gone into or come out of.</summary>
        private static string Peek(Explorer explorer, PlacedRoom room)
        {
            var inside = room == Stage.RoomGoneInto;
            if (room == _peekFor && _peekRead == Stage.ExampleRoomsRead && _peekInside == inside && _peekText != null) return _peekText;
            _peekFor = room;
            _peekRead = Stage.ExampleRoomsRead;
            _peekInside = inside;
            var ways = Stage.ExampleWays;
            var rise = ways?.Entrance != null ? Stage.ExampleRoomGround(room) - Stage.ExampleRoomGround(ways.Entrance) : 0f;
            var where = RoomCardWords.Where(rise, ways != null ? ways.StepsIn(room) : -1, ways != null ? ways.WaysOf(room).Count : 0);
            _peekText = StageWords.RoomPeek(BoxRoomName(explorer, room), where, Stage.RoomCreatures(room).Count, Stage.RoomLoot(room).Count, inside, ExampleLayouts.ShowsWorlds);
            return _peekText;
        }

        /// <summary>Whether a list of the card stands open, its creatures or its chests and pickups: folded to its count until opened, remembered with the panel's folds by a key standing while it is open.</summary>
        private static bool CardListOpen(string list) => IsFolded("card" + list + "open");

        private static void OpenCardList(string list, bool open) => SetFolded("card" + list + "open", open);

        /// <summary>Opens or folds the card's lists, for the self-test; returns whether each stood open.</summary>
        [Diagnostic]
        public static (bool Creatures, bool Loot) OpenCardListsForTest(bool creatures, bool loot)
        {
            var was = (CardListOpen("creatures"), CardListOpen("loot"));
            OpenCardList("creatures", creatures);
            OpenCardList("loot", loot);
            return was;
        }

        /// <summary>Lays the card's rows out for so many lines of each list and a width, kept until either changes.</summary>
        private static void LayCard(RoomCardShown card, int most, float width, float chipH)
        {
            if (card.LaidMost == most && Mathf.Approximately(card.LaidWidth, width)) return;
            card.LaidMost = most;
            card.LaidWidth = width;
            card.CreatureRows = RoomCardWords.Fit(card.Creatures.Select(c => (c.Line, (string)null, false, c.Page)).ToList(), most, more => (more, (string)null, true, (string)null));
            card.LootRows = RoomCardWords.Fit(card.Loot.Select(l => (l.Name, l.Odds, false, l.Page)).ToList(), most, more => (more, (string)null, true, (string)null));
            card.WaysTall = ChipRowsTall(card.Ways.Select(w => Skin.Fit(Skin.Chip, w.Name, width - U(6f))), Skin.Chip, width, chipH);
            card.LevelsTall = ChipRowsTall(card.LevelWords, Skin.Segment, width, chipH);
        }

        private static string CreatureName(string prefab)
        {
            var go = GamePrefabs.Named(prefab);
            return go != null ? ShownNameOf(go) : Naming.Readable(prefab);
        }

        /// <summary>A chest's or pickup's name as the game shows it, from its prefab; its prefab's words where it has none.</summary>
        private static string PartName(string prefab)
        {
            var go = GamePrefabs.Item(prefab);
            if (go == null) return Naming.Readable(prefab);
            var token = go.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared?.m_name
                        ?? go.GetComponent<Piece>().OrNull()?.m_name
                        ?? go.GetComponent<Container>().OrNull()?.m_name;
            var shown = CatalogBuilder.Localize(token);
            return shown.Length > 0 ? shown : Naming.Readable(prefab);
        }

        /// <summary>
        /// The card of a dungeon's room gone into, in the stage's top left under the breadcrumb
        /// and above the plan: its name with its page a link beside it and where it is, the
        /// creatures and the chests and pickups this copy rolled in it, each list folded to its
        /// count until opened, its ways on as chips that go into their rooms and light them while
        /// hovered, and its levels as segments that open them.
        /// It takes the mouse where it stands. Returns where it stands; none outside a room.
        /// </summary>
        private static Rect RoomCardBox(Explorer explorer, Rect inner, Rect plan)
        {
            var e = Event.current;
            var repaint = e.type == EventType.Repaint;
            // The room picked, where the view is, else the room gone into.
            var room = Stage.PickedRoom ?? Stage.RoomGoneInto;
            var picked = Stage.PickedRoom != null;
            if (repaint)
            {
                CardLines.Clear();
                _cardLit = _cardLitForTest;
            }
            if (room == null || !Stage.HasInside || !Stage.ShowsInside) return Rect.zero;
            var card = CardOf(explorer, room);

            var pad = U(8f);
            var width = Mathf.Min(U(260f), inner.width * 0.45f);
            var textW = width - pad * 2f;
            var top = inner.y + U(CornerFit.Top);
            var boxTop = plan.width > 0f ? plan.y : inner.yMax - U(CornerFit.Bottom);
            var nameH = U(20f);
            var whereH = U(18f);
            var lineH = U(17f);
            var headH = U(18f);
            var chipH = U(22f);
            // Its levels open only in the room gone into.
            var levels = card.Levels.Count > 1 && !picked;

            // Its lines of creatures and of chests and pickups, at most six of each.
            float Tall(int most)
            {
                LayCard(card, most, textW, chipH);
                var h = pad + nameH + whereH;
                if (!card.LayoutOnly)
                {
                    h += headH + (CardListOpen("creatures") ? lineH * RoomCardWords.ListLines(card.CreatureRows.Select(r => r.Name), card.CreaturesLeftLine).Count : 0f);
                    h += headH + (CardListOpen("loot") ? lineH * RoomCardWords.ListLines(card.LootRows.Select(r => r.Name), card.LootLeftLine).Count : 0f);
                }
                h += headH + card.WaysTall;
                if (levels) h += headH + card.LevelsTall;
                return h + pad;
            }
            // As tall as it wants, else as much as is left over the plan box, giving way to it and
            // scrolling the rest (CornerFit), its text narrower by the scrollbar then.
            if (!ReferenceEquals(room, _cardScrollFor))
            {
                _cardScrollFor = room;
                _cardScroll = Vector2.zero;
            }
            var wanted = Tall(6);
            var tall = CornerFit.Card(wanted, inner.y, boxTop, _s);
            var scrolls = CornerFit.Scrolls(wanted, tall);
            if (scrolls)
            {
                textW -= CardScrollbar;
                wanted = Tall(6);
            }
            if (repaint) RoomCardScrolls = scrolls;
            var area = new Rect(inner.x + U(8f), top, width, tall);

            CountDrawn(PanelPart.RoomCard);
            Skin.Box(area, Skin.Alpha(Skin.Stage, 0.88f), Skin.Outline);
            if (scrolls) _cardScroll = GUI.BeginScrollView(area, _cardScroll, new Rect(area.x, area.y, area.width - CardScrollbar, wanted), false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);
            var pen = new CardPen { Explorer = explorer, X = area.x + pad, Y = area.y + pad, TextW = textW, LineH = lineH, HeadH = headH, Repaint = repaint };
            CardName(ref pen, card, picked, nameH, out var openPage, out var goIn);
            pen.Line(whereH, card.Where, Skin.FaintLabel);
            // The world's own dungeon is shown by its layout alone: what lives and lies in it is the world's, not read.
            if (!card.LayoutOnly)
            {
                pen.ListHeading("creatures", RoomCardWords.Creatures, card.CreatureCount, Skin.KindColor(Kind.Creature), RoomCardWords.CreaturesTip);
                if (CardListOpen("creatures")) pen.Rows(card.CreatureRows, card.CreaturesLeftLine);
                pen.ListHeading("loot", RoomCardWords.Loot, card.LootCount, Skin.KindColor(Kind.Item), RoomCardWords.LootTip);
                if (CardListOpen("loot")) pen.Rows(card.LootRows, card.LootLeftLine);
            }
            var goTo = CardWays(ref pen, card, chipH);
            if (levels) CardLevels(ref pen, card, chipH);
            if (scrolls) GUI.EndScrollView();

            if (goTo != null) Stage.GoInto(goTo);
            else if (goIn) Stage.GoInto(room);
            else if (openPage) Go(explorer, card.Key);
            else if (pen.GoPage != null) Go(explorer, pen.GoPage);

            // The card is not the stage: a click on it neither turns nor zooms it, and the wheel on it
            // steps the room's cut a mark a notch, as Shift and the wheel do on the stage.
            if (area.Contains(e.mousePosition) && e.type == EventType.ScrollWheel && Stage.HasFloors) WheelStep(StageCamera.WheelTurn(e.delta.x, e.delta.y));
            if (area.Contains(e.mousePosition) && (e.type == EventType.MouseDown || e.type == EventType.ScrollWheel)) e.Use();
            return area;
        }

        /// <summary>
        /// Where the room card's next line goes and what it is drawn with: its left edge, how far
        /// down, its text's width, its lines' and headings' heights, whether this is a repaint; and
        /// the page a line clicked goes to.
        /// </summary>
        private struct CardPen
        {
            public Explorer Explorer;
            public float X, Y, TextW, LineH, HeadH;
            public bool Repaint;
            public string GoPage;

            /// <summary>A line of the card, its odds faint at its end; a creature's or a chest's goes to its page on a click, where it has one.</summary>
            public void Line(float h, string text, GUIStyle style, string odds = null, string page = null)
            {
                var oddsW = odds != null ? Skin.Width(Skin.FaintLabel, odds) + U(4f) : 0f;
                if (Repaint) CardLines.Add(RoomCardWords.Row(text, odds));
                var rect = new Rect(X, Y, TextW - oddsW, h);
                if (page != null && Goes(Explorer, page))
                {
                    var link = new Rect(rect.x, rect.y, Mathf.Min(rect.width, Skin.Width(style, text)), rect.height);
                    CountDrawn(PanelPart.RoomCardLine);
                    if (link.Contains(Event.current.mousePosition)) AskTip("card-line:" + page, RoomCardWords.LineTip);
                    if (GUI.Button(link, GUIContent.none, GUIStyle.none)) GoPage = page;
                    LinkLabel(rect, text, style, Skin.Text);
                }
                else FitLabel(rect, text, style, 10f);
                if (odds != null) GUI.Label(new Rect(X + TextW - oddsW + U(4f), Y, oddsW, h), odds, Skin.FaintLabel);
                Y += h;
            }

            public void Heading(string text, Color colour, string tip)
            {
                var rect = new Rect(X, Y, TextW, HeadH);
                if (Repaint) CardLines.Add(text);
                Skin.LabelIn(rect, text, Skin.Heading, colour);
                if (tip != null && rect.Contains(Event.current.mousePosition)) AskTip("card:" + text, tip);
                Y += HeadH;
            }

            /// <summary>A list's rows, the line saying how many more faint and without odds; none, said so.</summary>
            public void Rows(List<(string Name, string Odds, bool More, string Page)> rows, string left)
            {
                if (rows.Count == 0 && left == null) Line(LineH, RoomCardWords.None, Skin.FaintLabel);
                foreach (var (name, odds, more, page) in rows)
                {
                    if (more) Line(LineH, name, Skin.FaintLabel);
                    else Line(LineH, name, Skin.Small, odds, page);
                }
                if (left != null) Line(LineH, left, Skin.FaintLabel);
            }

            /// <summary>A list's heading saying how many it holds; a click on it opens it or folds it away.</summary>
            public void ListHeading(string list, string text, int count, Color colour, string tip)
            {
                var open = CardListOpen(list);
                var shown = RoomCardWords.ListHeading(text, count, open);
                if (Repaint) CardLines.Add(text);
                Skin.LabelIn(new Rect(X, Y, TextW, HeadH), shown, Skin.Heading, colour);
                var head = new Rect(X, Y, Mathf.Min(TextW, Skin.Width(Skin.Heading, shown) + U(4f)), HeadH);
                if (head.Contains(Event.current.mousePosition)) AskTip("card:" + text, RoomCardWords.ListTip(tip, open));
                if (GUI.Button(head, GUIContent.none, GUIStyle.none)) OpenCardList(list, !open);
                Y += HeadH;
            }
        }

        /// <summary>The card's name, its page a link at the line's end where it has one, a room picked a link going into it before; which was clicked.</summary>
        private static void CardName(ref CardPen pen, RoomCardShown card, bool picked, float nameH, out bool openPage, out bool goIn)
        {
            var e = Event.current;
            openPage = false;
            goIn = false;
            var x = pen.X;
            var y = pen.Y;
            var textW = pen.TextW;
            var pageW = card.Goes ? Skin.Width(Skin.Small, RoomCardWords.PageLink) + U(8f) : 0f;
            var inW = picked ? Skin.Width(Skin.Small, StageWords.GoInLink) + U(8f) : 0f;
            if (pen.Repaint) CardLines.Add(card.Name);
            FitLabel(new Rect(x, y, textW - pageW - inW, nameH), card.Name, Skin.TopicName, 10f);
            if (picked)
            {
                var link = new Rect(x + textW - pageW - inW + U(8f), y + U(2f), inW - U(8f), nameH - U(2f));
                if (link.Contains(e.mousePosition)) AskTip("card:in", StageWords.GoInTip);
                if (GUI.Button(link, GUIContent.none, GUIStyle.none)) goIn = true;
                LinkLabel(link, StageWords.GoInLink, Skin.Small, Skin.Accent);
            }
            if (card.Goes)
            {
                var link = new Rect(x + textW - pageW + U(8f), y + U(2f), pageW - U(8f), nameH - U(2f));
                if (link.Contains(e.mousePosition)) AskTip("card:page", RoomCardWords.OpenPageTip);
                if (GUI.Button(link, GUIContent.none, GUIStyle.none)) openPage = true;
                LinkLabel(link, RoomCardWords.PageLink, Skin.Small, Skin.Accent);
            }
            pen.Y += nameH;
        }

        /// <summary>The card's ways on as chips, each its room's name cut at its end where too long, how far and whether a dead end in its tip: each goes into its room, and lights it on the stage and the plan while hovered. The room clicked.</summary>
        private static PlacedRoom CardWays(ref CardPen pen, RoomCardShown card, float chipH)
        {
            var e = Event.current;
            var repaint = pen.Repaint;
            var textW = pen.TextW;
            pen.Heading(RoomCardWords.Ways, Skin.Faint, null);
            var flow = new ChipFlow(pen.X, pen.X + textW, pen.Y, chipH, U(4f), U(4f));
            PlacedRoom goTo = null;
            foreach (var way in card.Ways)
            {
                var label = Skin.Fit(Skin.Chip, way.Name, textW - U(6f));
                var w = Mathf.Min(Skin.Width(Skin.Chip, label) + U(6f), textW);
                var (cx, cy) = flow.Place(w);
                var chip = new Rect(cx, cy, w, chipH);
                CountDrawn(PanelPart.RoomCardWay);
                if (repaint) CardLines.Add(label);
                if (chip.Contains(e.mousePosition)) AskTip("card-way:" + way.Words, StageWords.GoIntoTip(way.Words));
                if (repaint && chip.Contains(OwnMouse(e))) _cardLit = way.To;
                if (GUI.Button(chip, label, Skin.Fitted(Skin.Chip, chip))) goTo = way.To;
            }
            pen.Y += card.WaysTall;
            return goTo;
        }

        /// <summary>The room's own levels as segments, the one open lit; opening one cuts at head height over it.</summary>
        private static void CardLevels(ref CardPen pen, RoomCardShown card, float chipH)
        {
            var e = Event.current;
            var repaint = pen.Repaint;
            var textW = pen.TextW;
            pen.Heading(RoomCardWords.Floors, Skin.Faint, null);
            var open = Stage.RoomLevelOpen;
            var steps = new ChipFlow(pen.X, pen.X + textW, pen.Y, chipH, U(4f), U(4f));
            for (var i = 0; i < card.Levels.Count; i++)
            {
                var words = card.LevelWords[i];
                var on = open is float at && Mathf.Abs(at - card.Levels[i]) < 0.01f;
                var style = on ? Skin.SegmentOn : Skin.Segment;
                var w = Mathf.Min(Skin.Width(Skin.Segment, words) + U(6f), textW);
                var (cx, cy) = steps.Place(w);
                var segment = new Rect(cx, cy, w, chipH);
                CountDrawn(PanelPart.RoomCardLevel);
                if (repaint) CardLines.Add(words);
                if (segment.Contains(e.mousePosition)) AskTip("card-level:" + words, RoomCardWords.FloorTip);
                if (GUI.Button(segment, words, Skin.Fitted(style, segment)) && !on) Stage.OpenRoomLevel(card.Levels[i]);
            }
            pen.Y += card.LevelsTall;
        }

        /// <summary>How tall rows of chips of these texts stand, wrapped to a width, with the gap under them.</summary>
        private static float ChipRowsTall(IEnumerable<string> texts, GUIStyle style, float width, float chipH)
        {
            var flow = new ChipFlow(0f, width, 0f, chipH, U(4f), U(4f));
            foreach (var text in texts) flow.Place(Mathf.Min(Skin.Width(style, text) + U(6f), width));
            return flow.Below + U(4f);
        }

        /// <summary>Lets go of the card, as the stage goes or another world is shown.</summary>
        private static void ForgetRoomCard()
        {
            _roomCard = null;
            _cardScrollFor = null;
            _cardScroll = Vector2.zero;
            _cardLit = null;
            _cardLitForTest = null;
            CardLines.Clear();
        }
    }
}
