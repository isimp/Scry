using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The compact view's header, three rows however many kinds the list holds: the name with the
    /// list button and the way back and forward after it, where the title row has room; the search
    /// with its help, favourites and recent; and the kind picked as one pill opening the others in
    /// a box over the list, Learned after it while it holds news, and where the list is taken from
    /// as one chip opening its choice. Each box takes a press over it before anything under it does.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>Which of the compact header's boxes is open.</summary>
        internal enum HeaderMenu
        {
            None,
            Kinds,
            Origin,
        }

        private static HeaderMenu _headerMenu;

        /// <summary>Where the open box's chip and the box stand, and each choice in it, on the screen.</summary>
        private static Rect _headerMenuChip, _headerMenuBox;
        private static readonly List<(Rect At, int Choice)> HeaderMenuChoices = new List<(Rect, int)>();

        /// <summary>The choice in the kinds' box that shows every kind; the others are the kinds themselves.</summary>
        private const int EveryKindChoice = -1;

        /// <summary>Where the pill and the origin chip last stood on the screen, and the row they stand in, for the self-test to open their boxes.</summary>
        private static Rect _pillOnScreen, _originOnScreen, _kindRowOnScreen;

        /// <summary>Whether the list button and the way back stand in the title row, as the compact view puts them where it has room.</summary>
        private static bool _navInTitle;

        /// <summary>Where each part of the compact header was last drawn, in the panel's own space, for the self-test.</summary>
        [Diagnostic]
        public static List<(string Part, Rect At)> CompactHeaderForTest { get; } = new List<(string, Rect)>();

        /// <summary>Where the panel was last drawn on the screen, for the self-test.</summary>
        [Diagnostic]
        public static Rect PanelDrawnForTest => _drawn;

        /// <summary>Which of the compact header's boxes is open, for the self-test.</summary>
        [Diagnostic]
        public static HeaderMenu HeaderMenuOpen => _headerMenu;

        /// <summary>Where the open box stands on the screen and how many choices it holds, for the self-test.</summary>
        [Diagnostic]
        public static (Rect Box, int Choices) HeaderMenuForTest => (_headerMenuBox, HeaderMenuChoices.Count);

        /// <summary>Opens a box of the compact header as a click on its chip does, for the self-test.</summary>
        [Diagnostic]
        public static void OpenHeaderMenuForTest(HeaderMenu menu)
        {
            if (menu == HeaderMenu.None) _headerMenu = HeaderMenu.None;
            else if (_headerMenu != menu) ToggleHeaderMenu(menu, menu == HeaderMenu.Kinds ? _pillOnScreen : _originOnScreen);
        }

        /// <summary>Takes a choice of the open box as a click on it does: a kind (<see cref="EveryKindChoice"/> for all), or where the list is taken from; for the self-test.</summary>
        [Diagnostic]
        public static void ChooseHeaderMenuForTest(int choice) => ChooseInHeaderMenu(_explorer, choice);

        /// <summary>Notes where a part of the compact header was drawn, for the self-test.</summary>
        private static void NoteCompactPart(string part, Rect at)
        {
            if (Event.current.type != EventType.Repaint) return;
            for (var i = 0; i < CompactHeaderForTest.Count; i++)
            {
                if (CompactHeaderForTest[i].Part != part) continue;
                CompactHeaderForTest[i] = (part, at);
                return;
            }
            CompactHeaderForTest.Add((part, at));
        }

        /// <summary>
        /// The list button, then back and forward, from a point of a row, each a button as tall as
        /// the row: in the compact view joined as one group, as the stage's chips are, back or
        /// forward faint where there is nowhere to go; in the full view apart.
        /// </summary>
        private static void Nav(Explorer explorer, float x, float y, float size)
        {
            if (!_compact)
            {
                ListButton(new Rect(x, y, size, size));
                var navX = x + size + U(4f);
                BackAndForward(explorer, new Rect(navX, y, size, size), new Rect(navX + size + U(4f), y, size, size));
                return;
            }
            var seam = U(1f);
            var list = new Rect(x, y, size, size);
            var back = new Rect(list.xMax + seam, y, size, size);
            var forward = new Rect(back.xMax + seam, y, size, size);
            if (NavPiece(list, ListHidden, true, false, null, Skin.ListMark, true, "list-button", ListWords.ListButtonTip(ListHidden))) ToggleList();
            if (NavPiece(back, false, false, false, "\u2039", null, explorer.CanGoBack || Stage.CanStepBack, "back", BackTip)) Step(explorer, true);
            if (NavPiece(forward, false, false, true, "\u203A", null, explorer.CanGoForward || Stage.CanStepForward, "forward", ForwardTip)) Step(explorer, false);
            NoteCompactPart("nav", new Rect(x, y, HeaderLayout.JoinedWidth(3, size, seam), size));
        }

        /// <summary>A piece of the joined group: its icon or words dark on the accent while on, faint where it cannot be used; true when clicked.</summary>
        private static bool NavPiece(Rect part, bool on, bool first, bool last, string text, Texture2D icon, bool usable, string tipKey, string tip)
        {
            var e = Event.current;
            var hover = usable && part.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                Skin.PillPiece(part, on ? Skin.Accent : hover ? Skin.RaisedHover : Skin.Raised, first, last);
                var ink = on ? Skin.OnAccent : usable ? Skin.Dim : Skin.Alpha(Skin.Faint, 0.45f);
                if (icon != null) Skin.Icon(new Rect(part.x + part.width * 0.24f, part.y + part.height * 0.24f, part.width * 0.52f, part.height * 0.52f), icon, ink);
                else Skin.LabelIn(part, text, Skin.SmallCenter, ink);
            }
            if (part.Contains(e.mousePosition)) AskTip(tipKey, tip);
            return usable && GUI.Button(part, GUIContent.none, GUIStyle.none);
        }

        /// <summary>Whether the list button and the way back stand in the title row, after the name, at a size, ending before a point; only in the compact view (<see cref="HeaderLayout.NavInTitleRow"/>).</summary>
        private static bool PlaceNav(float afterName, float size, float before)
        {
            // The first of the header placed each time it is drawn: what the self-test is told starts afresh.
            if (Event.current.type == EventType.Repaint) CompactHeaderForTest.Clear();
            _navInTitle = _compact && HeaderLayout.NavInTitleRow(afterName, NavWidth(size), before);
            return _navInTitle;
        }

        /// <summary>How wide the list button and the way back are at a size: joined in the compact view, apart with their gaps in the full view.</summary>
        private static float NavWidth(float size) => _compact ? HeaderLayout.JoinedWidth(3, size, U(1f)) : size * 3f + U(4f) * 2f;

        /// <summary>The compact view's search row: the search, after the list button and the way back where the title row has no room for them, with its help, favourites and recent at the row's end.</summary>
        private static float CompactControls(Explorer explorer, Rect rect)
        {
            var size = rect.height;
            var gap = U(8f);
            var (searchX, searchW, buttonsX) = HeaderLayout.CompactSearchRow(rect.x, rect.xMax, _navInTitle ? 0f : NavWidth(size), size * 3f + gap * 2f, gap);
            if (!_navInTitle) Nav(explorer, rect.x, rect.y, size);
            var search = new Rect(searchX, rect.y, searchW, rect.height);
            Search(explorer, search);
            SearchButtons(explorer, buttonsX, rect.y, size, gap);
            NoteCompactPart("search", search);
            NoteCompactPart("buttons", new Rect(buttonsX, rect.y, rect.xMax - buttonsX, size));
            return rect.yMax;
        }

        /// <summary>
        /// The compact view's kind row: the kind picked as a pill whose click opens every kind in a
        /// box over the list, lit unless the learned list is shown; Learned after it while it holds
        /// news; and where the list is taken from as a chip at the row's end, whose click opens its choice.
        /// </summary>
        private static float CompactTabs(Explorer explorer, Rect rect)
        {
            var e = Event.current;
            var learned = explorer.LearnedOnly;
            var picked = explorer.KindFilter;
            var collecting = Collecting(explorer);
            string label;
            int count, total;
            Color dot;
            string census = null;
            if (picked is Kind kind)
            {
                var collected = collecting && CompendiumView.Counted(kind);
                label = Kinds.Label(kind);
                count = collected ? explorer.FoundOf(kind) : explorer.CountOf(kind);
                total = collected ? explorer.TotalOf(kind) : -1;
                dot = Skin.KindColor(kind);
                if (collected && total != explorer.AllOf(kind)) census = CensusWords.TabTip(count, total, explorer.AllOf(kind));
            }
            else
            {
                label = "All";
                count = explorer.CountAll;
                total = -1;
                dot = Skin.Text;
            }
            var pillOn = !learned;
            var pillW = Skin.Width(Skin.TabOn, ListWords.KindPill(TabText(label, count, total, true), false)) + U(2f);
            var showLearned = explorer.CompendiumSet && (explorer.LearnedCount > 0 || learned);
            var learnedW = showLearned ? TabWidth(CompendiumWords.LearnedTab, explorer.LearnedCount, -1) : 0f;
            var origin = OriginNames[(int)explorer.Origin];
            var originW = Skin.Width(Skin.On, ListWords.OriginChip(origin, false)) + U(14f);
            var (pillX, pillWidth, learnedX, originX) = HeaderLayout.CompactKindRow(rect.x, rect.xMax, pillW, learnedW, originW, U(4f));
            _kindRowOnScreen = OnScreen(rect);

            var pill = new Rect(pillX, rect.y, pillWidth, rect.height);
            _pillOnScreen = OnScreen(pill);
            var pillText = ListWords.KindPill(TabText(label, count, total, pillOn), _headerMenu == HeaderMenu.Kinds);
            if (GUI.Button(pill, pillText, Skin.Fitted(pillOn ? Skin.TabOn : Skin.Tab, pill))) ToggleHeaderMenu(HeaderMenu.Kinds, _pillOnScreen);
            var size = U(8f);
            Skin.Icon(new Rect(pill.x + U(10f), pill.y + (pill.height - size) / 2f, size, size), Skin.Circle, pillOn ? Skin.OnAccent : dot);
            if (pill.Contains(e.mousePosition)) AskTip(census != null ? "tab-census" : "kind-pill", census ?? ListWords.KindPillTip);
            NoteCompactPart("pill", pill);

            if (showLearned)
            {
                CountDrawn(PanelPart.LearnedTab);
                var tab = new Rect(learnedX, rect.y, learnedW, rect.height);
                if (DrawTab(tab, CompendiumWords.LearnedTab, explorer.LearnedCount, -1, Skin.Accent, learned, "learned", CompendiumWords.LearnedTip(learned)) && !learned)
                {
                    explorer.KindFilter = null;
                    ShowLearned(explorer, true);
                }
                NoteCompactPart("learned", tab);
            }

            var chip = new Rect(originX, rect.y, originW, rect.height);
            _originOnScreen = OnScreen(chip);
            var originOpen = _headerMenu == HeaderMenu.Origin;
            if (GUI.Button(chip, ListWords.OriginChip(origin, originOpen), originOpen || explorer.Origin != OriginFilter.All ? Skin.On : Skin.Button)) ToggleHeaderMenu(HeaderMenu.Origin, _originOnScreen);
            if (!originOpen && chip.Contains(e.mousePosition)) AskTip("origin", ListWords.OriginTip);
            NoteCompactPart("origin", chip);
            return rect.yMax;
        }

        /// <summary>Opens a box under its chip, or closes it where it is open: every kind across the kind row, where the list is taken from ending at its chip.</summary>
        private static void ToggleHeaderMenu(HeaderMenu menu, Rect chipOnScreen)
        {
            if (_headerMenu == menu)
            {
                _headerMenu = HeaderMenu.None;
                return;
            }
            _headerMenu = menu;
            _headerMenuChip = chipOnScreen;
            HeaderMenuChoices.Clear();
            var top = chipOnScreen.yMax + U(4f);
            // Laid out as it is drawn; its height is known once the kinds are placed.
            _headerMenuBox = menu == HeaderMenu.Kinds
                ? new Rect(_kindRowOnScreen.x, top, _kindRowOnScreen.width, U(40f))
                : new Rect(chipOnScreen.xMax - OriginSwitchWidth() - U(16f), top, OriginSwitchWidth() + U(16f), chipOnScreen.height + U(16f));
        }

        /// <summary>Takes a choice of the open box and closes it: a kind, or every kind, or where the list is taken from.</summary>
        private static void ChooseInHeaderMenu(Explorer explorer, int choice)
        {
            var menu = _headerMenu;
            _headerMenu = HeaderMenu.None;
            if (explorer == null) return;
            if (menu == HeaderMenu.Kinds) Filter(explorer, choice == EveryKindChoice ? (Kind?)null : (Kind)choice);
            else if (menu == HeaderMenu.Origin)
            {
                explorer.Origin = (OriginFilter)choice;
                ListFiltered();
            }
        }

        /// <summary>Closes the compact header's box.</summary>
        private static void ForgetHeaderMenu()
        {
            _headerMenu = HeaderMenu.None;
            HeaderMenuChoices.Clear();
        }

        /// <summary>
        /// Before anything draws: the open box takes a press over it, taking the choice under it; a
        /// press elsewhere, but on its chip, closes it, as do Escape and the full view. True when it
        /// took the event.
        /// </summary>
        private static bool HeaderMenuInput(Explorer explorer)
        {
            if (_headerMenu == HeaderMenu.None) return false;
            if (!_compact)
            {
                ForgetHeaderMenu();
                return false;
            }
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                ForgetHeaderMenu();
                e.Use();
                return true;
            }
            var mouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
            var over = _headerMenuBox.Contains(mouse);
            if (e.type == EventType.MouseDown)
            {
                if (!over)
                {
                    if (!_headerMenuChip.Contains(mouse)) ForgetHeaderMenu();
                    return false;
                }
                foreach (var (at, choice) in HeaderMenuChoices)
                {
                    if (!at.Contains(mouse)) continue;
                    ChooseInHeaderMenu(explorer, choice);
                    break;
                }
                e.Use();
                return true;
            }
            if (over && (e.type == EventType.MouseUp || e.type == EventType.ScrollWheel || e.type == EventType.MouseDrag))
            {
                e.Use();
                return true;
            }
            return false;
        }

        /// <summary>Draws the open box over everything else: every kind with something of it as the full view's tabs, or where the list is taken from as its switch.</summary>
        private static void HeaderMenuDraw(Explorer explorer)
        {
            if (_headerMenu == HeaderMenu.None || !_compact) return;
            var e = Event.current;
            var box = Here(_headerMenuBox);
            HeaderMenuChoices.Clear();
            var inset = U(8f);
            if (_headerMenu == HeaderMenu.Kinds)
            {
                // Placed once to know how tall the box is, then drawn where they were placed.
                var rows = KindTabRows(explorer);
                var measure = new ChipFlow(box.x + inset, box.xMax - inset, box.y + inset, U(30f), U(4f), U(4f));
                foreach (var row in rows) measure.Place(TabWidth(row.Label, row.Count, row.Total));
                box.height = measure.Below - box.y + inset;
                _headerMenuBox = OnScreen(box);
                if (e.type == EventType.Repaint) Skin.Box(box, Skin.Panel, Skin.Outline);
                var flow = new ChipFlow(box.x + inset, box.xMax - inset, box.y + inset, U(30f), U(4f), U(4f));
                foreach (var row in rows)
                {
                    var width = TabWidth(row.Label, row.Count, row.Total);
                    var at = flow.Place(width);
                    var tab = new Rect(at.X, at.Y, width, flow.RowHeight);
                    if (e.type == EventType.Repaint) TabsDrawnForTest.Add(tab);
                    DrawTab(tab, row.Label, row.Count, row.Total, row.Dot, row.On, row.Census != null ? "tab-census" : null, row.Census);
                    HeaderMenuChoices.Add((OnScreen(tab), row.Kind.HasValue ? (int)row.Kind.Value : EveryKindChoice));
                }
                CountDrawn(PanelPart.KindMenu);
                return;
            }
            if (e.type == EventType.Repaint) Skin.Box(box, Skin.Panel, Skin.Outline);
            foreach (var (at, choice) in OriginSegments(explorer, box.x + inset, box.y + inset, box.height - inset * 2f, choose: false)) HeaderMenuChoices.Add((OnScreen(at), choice));
            if (box.Contains(e.mousePosition)) AskTip("origin", ListWords.OriginTip);
            CountDrawn(PanelPart.OriginMenu);
        }
    }
}
