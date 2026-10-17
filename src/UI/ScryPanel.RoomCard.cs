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
            public bool Strict;

            public string Name, Key, Where;
            public bool Goes;
            public List<(string Line, string Page)> Creatures = new List<(string, string)>();
            public List<(string Name, string Odds, string Page)> Loot = new List<(string, string, string)>();
            public int CreaturesLeft, LootLeft;
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
        [Diagnostic]
        public static IReadOnlyList<WayOn> RoomCardWays => _roomCard != null && Stage.RoomGoneInto == _roomCard.Room ? _roomCard.Ways : new List<WayOn>();

        /// <summary>What the card says, line by line, for the self-test.</summary>
        [Diagnostic]
        public static IReadOnlyList<string> RoomCardLines => CardLines;

        /// <summary>Clicks a way chip of the card as the mouse does, for the self-test.</summary>
        [Diagnostic]
        public static void ClickCardWayForTest(WayOn way) => Stage.GoInto(way.To);

        /// <summary>Clicks the card's Whole dungeon button as the mouse does, for the self-test.</summary>
        [Diagnostic]
        public static void ClickCardWholeForTest() => Stage.ComeOut();

        /// <summary>Lights a room as hovering a way chip does, for the self-test; none puts it out.</summary>
        [Diagnostic]
        public static void LightCardWayForTest(PlacedRoom room) => _cardLitForTest = room;

        /// <summary>The card's contents for the room gone into, made again only when something it shows has changed.</summary>
        private static RoomCardShown CardOf(Explorer explorer, PlacedRoom room)
        {
            var card = _roomCard;
            if (card != null && card.Room == room && card.Read == Stage.ExampleRoomsRead && card.Rooms == Stage.ExampleRoomsShown
                && card.Know == explorer.KnowStamp && card.Strict == explorer.LeavingOut) return card;

            var ways = Stage.ExampleWays;
            card = new RoomCardShown { Room = room, Read = Stage.ExampleRoomsRead, Rooms = Stage.ExampleRoomsShown, Know = explorer.KnowStamp, Strict = explorer.LeavingOut };
            string RoomName(PlacedRoom r) => ShownName(explorer, EntryKeys.For(Kind.Location, r.Room.Name), LocationWords.RoomName(r.Room.Name));
            card.Key = EntryKeys.For(Kind.Location, room.Room.Name);
            card.Name = RoomName(room);
            card.Goes = Goes(explorer, card.Key);
            card.Ground = Stage.ExampleRoomGround(room);
            var entrance = ways?.Entrance;
            var rise = entrance != null ? card.Ground - Stage.ExampleRoomGround(entrance) : 0f;
            card.Where = RoomCardWords.Where(rise, ways != null ? ways.StepsIn(room) : -1, ways != null ? ways.WaysOf(room).Count : 0);

            // Under the strict rules, what was not seen together with the page is left out and counted.
            var creatures = Stage.RoomCreatures(room);
            var kept = creatures.Where(c => !explorer.LeavesOut(c)).ToList();
            card.CreaturesLeft = creatures.Count - kept.Count;
            card.Creatures = RoomCardWords.CountedPages(kept.Select(c => (ShownName(explorer, c, CreatureName(c)), c)));

            var loot = Stage.RoomLoot(room);
            var shown = loot.Where(l => !explorer.LeavesOut(l.Prefab)).ToList();
            card.LootLeft = loot.Count - shown.Count;
            card.Loot = RoomCardWords.OddsPages(shown.Select(l => (ShownName(explorer, l.Prefab, PartName(l.Prefab)), l.Prefab, l.Chance)));

            if (ways != null) card.Ways = RoomCard.WaysOn(ways, room, Stage.ExampleRoomGround, RoomName);
            card.Levels = Stage.ExampleRoomLevels(room);
            card.LevelWords = card.Levels.Select(l => RoomCardWords.Level(l, card.Ground)).ToList();
            _roomCard = card;
            return card;
        }

        /// <summary>Lays the card's rows out for so many lines of each list and a width, kept until either changes.</summary>
        private static void LayCard(RoomCardShown card, int most, float width, float chipH)
        {
            if (card.LaidMost == most && Mathf.Approximately(card.LaidWidth, width)) return;
            card.LaidMost = most;
            card.LaidWidth = width;
            card.CreatureRows = RoomCardWords.Fit(card.Creatures.Select(c => (c.Line, (string)null, false, c.Page)).ToList(), most, more => (more, (string)null, true, (string)null));
            card.LootRows = RoomCardWords.Fit(card.Loot.Select(l => (l.Name, l.Odds, false, l.Page)).ToList(), most, more => (more, (string)null, true, (string)null));
            card.WaysTall = ChipRowsTall(card.Ways.Select(w => w.Words), Skin.Chip, width, chipH);
            card.LevelsTall = ChipRowsTall(card.LevelWords, Skin.Segment, width, chipH);
        }

        private static string CreatureName(string prefab)
        {
            var go = GamePrefabs.Named(prefab);
            return go != null ? ShownNameOf(go) : prefab;
        }

        /// <summary>A chest's or pickup's name as the game shows it, from its prefab; its prefab's name where it has none.</summary>
        private static string PartName(string prefab)
        {
            var go = GamePrefabs.Item(prefab);
            if (go == null) return prefab;
            var token = go.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared?.m_name
                        ?? go.GetComponent<Piece>().OrNull()?.m_name
                        ?? go.GetComponent<Container>().OrNull()?.m_name;
            var shown = CatalogBuilder.Localize(token);
            return shown.Length > 0 ? shown : prefab;
        }

        /// <summary>
        /// The card of a dungeon's room gone into, in the stage's top left under the kind badge
        /// and above the plan: its name and where it is, the creatures and the chests and pickups
        /// this copy rolled in it, its ways on as chips that go into their rooms and light them
        /// while hovered, its levels as segments that open them, its page and the way back out.
        /// It takes the mouse where it stands. Returns where it stands; none outside a room.
        /// </summary>
        private static Rect RoomCardBox(Explorer explorer, Rect inner, Rect plan)
        {
            var e = Event.current;
            var repaint = e.type == EventType.Repaint;
            var room = Stage.RoomGoneInto;
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
            var buttonH = U(24f);
            var levels = card.Levels.Count > 1;

            // Its lines of creatures and of chests and pickups, at most six of each.
            float Tall(int most)
            {
                LayCard(card, most, textW, chipH);
                var h = pad + nameH + whereH;
                h += headH + lineH * (Mathf.Max(1, card.CreatureRows.Count) + (card.CreaturesLeft > 0 ? 1 : 0));
                h += headH + lineH * (Mathf.Max(1, card.LootRows.Count) + (card.LootLeft > 0 ? 1 : 0));
                h += headH + card.WaysTall;
                if (levels) h += headH + card.LevelsTall;
                return h + U(2f) + buttonH + pad;
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
            var x = area.x + pad;
            var y = area.y + pad;

            // A creature's or a chest's line goes to its page on a click, where it has one.
            string goPage = null;
            void Line(float h, string text, GUIStyle style, string odds = null, string page = null)
            {
                var oddsW = odds != null ? Skin.Width(Skin.FaintLabel, odds) + U(4f) : 0f;
                if (repaint) CardLines.Add(RoomCardWords.Row(text, odds));
                var rect = new Rect(x, y, textW - oddsW, h);
                if (page != null && Goes(explorer, page))
                {
                    var link = new Rect(rect.x, rect.y, Mathf.Min(rect.width, Skin.Width(style, text)), rect.height);
                    CountDrawn(PanelPart.RoomCardLine);
                    if (link.Contains(e.mousePosition)) AskTip("card-line:" + page, RoomCardWords.LineTip);
                    if (GUI.Button(link, GUIContent.none, GUIStyle.none)) goPage = page;
                    LinkLabel(rect, text, style, Skin.Text);
                }
                else FitLabel(rect, text, style, 10f);
                if (odds != null) GUI.Label(new Rect(x + textW - oddsW + U(4f), y, oddsW, h), odds, Skin.FaintLabel);
                y += h;
            }

            void Heading(string text, Color colour, string tip)
            {
                var rect = new Rect(x, y, textW, headH);
                if (repaint) CardLines.Add(text);
                Skin.LabelIn(rect, text, Skin.Heading, colour);
                if (tip != null && rect.Contains(e.mousePosition)) AskTip("card:" + text, tip);
                y += headH;
            }

            void Rows(List<(string Name, string Odds, bool More, string Page)> rows, int left)
            {
                if (rows.Count == 0) Line(lineH, RoomCardWords.None, Skin.FaintLabel);
                // The line saying how many more stands faint, without odds.
                foreach (var (name, odds, more, page) in rows)
                {
                    if (more) Line(lineH, name, Skin.FaintLabel);
                    else Line(lineH, name, Skin.Small, odds, page);
                }
                if (left > 0) Line(lineH, CompendiumWords.LeftOutOf(null, left), Skin.FaintLabel);
            }

            Line(nameH, card.Name, Skin.TopicName);
            Line(whereH, card.Where, Skin.FaintLabel);
            Heading(RoomCardWords.Creatures, Skin.KindColor(Kind.Creature), RoomCardWords.CreaturesTip);
            Rows(card.CreatureRows, card.CreaturesLeft);
            Heading(RoomCardWords.Loot, Skin.KindColor(Kind.Item), RoomCardWords.LootTip);
            Rows(card.LootRows, card.LootLeft);

            // Its ways on: each goes into its room, and lights it on the stage and the plan while hovered.
            Heading(RoomCardWords.Ways, Skin.Faint, null);
            var flow = new ChipFlow(x, x + textW, y, chipH, U(4f), U(4f));
            PlacedRoom goTo = null;
            foreach (var way in card.Ways)
            {
                var w = Mathf.Min(Skin.Width(Skin.Chip, way.Words) + U(6f), textW);
                var (cx, cy) = flow.Place(w);
                var chip = new Rect(cx, cy, w, chipH);
                CountDrawn(PanelPart.RoomCardWay);
                if (repaint) CardLines.Add(way.Words);
                if (chip.Contains(e.mousePosition)) AskTip("card-way:" + way.Words, StageWords.GoIntoTip(way.Words, false));
                if (repaint && chip.Contains(OwnMouse(e))) _cardLit = way.To;
                if (GUI.Button(chip, way.Words, Skin.Fitted(Skin.Chip, chip))) goTo = way.To;
            }
            y += card.WaysTall;

            // Its own levels, the one open lit; opening one cuts at head height over it.
            if (levels)
            {
                Heading(RoomCardWords.Levels, Skin.Faint, null);
                var open = Stage.RoomLevelOpen;
                var steps = new ChipFlow(x, x + textW, y, chipH, U(4f), U(4f));
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
                    if (segment.Contains(e.mousePosition)) AskTip("card-level:" + words, RoomCardWords.LevelTip);
                    if (GUI.Button(segment, words, Skin.Fitted(style, segment)) && !on) Stage.OpenRoomLevel(card.Levels[i]);
                }
                y += card.LevelsTall;
            }

            // Its page, as a click on a room once went to, and the way back out.
            y += U(2f);
            var half = (textW - U(6f)) / 2f;
            var page = new Rect(x, y, half, buttonH);
            var whole = new Rect(x + half + U(6f), y, half, buttonH);
            if (page.Contains(e.mousePosition)) AskTip("card:page", RoomCardWords.OpenPageTip);
            if (whole.Contains(e.mousePosition)) AskTip("card:whole", RoomCardWords.WholeTip);
            var enabled = GUI.enabled;
            GUI.enabled = enabled && card.Goes;
            var openPage = GUI.Button(page, RoomCardWords.OpenPage, Skin.Fitted(Skin.Button, page)) && card.Goes;
            GUI.enabled = enabled;
            var backOut = GUI.Button(whole, RoomCardWords.Whole, Skin.Fitted(Skin.Button, whole));
            if (scrolls) GUI.EndScrollView();

            if (goTo != null) Stage.GoInto(goTo);
            else if (backOut) Stage.ComeOut();
            else if (openPage) Go(explorer, card.Key);
            else if (goPage != null) Go(explorer, goPage);

            // The card is not the stage: a click or the wheel on it neither turns nor zooms it.
            if (area.Contains(e.mousePosition) && (e.type == EventType.MouseDown || e.type == EventType.ScrollWheel)) e.Use();
            return area;
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
