using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The panel's header: the search with its filters and buttons, back and forward, and the kind tabs.</summary>
    internal static partial class ScryPanel
    {
        private static readonly string[] OriginNames = { "All", "Game", "Mods" };

        /// <summary>Whether the search box takes the keyboard at its next repaint.</summary>
        private static bool _focusSearch;

        /// <summary>Gives the search box the keyboard at its next repaint, or not.</summary>
        private static void FocusSearch(bool focus) => _focusSearch = focus;

        /// <summary>Whether the panel lets go of the keyboard at its next draw (<see cref="LeaveBoxes"/>).</summary>
        private static bool _leaveBoxes;

        /// <summary>
        /// Lets go of the keyboard at the panel's next draw, as Esc in a text box does: the
        /// keyboard can only be handed over while the panel draws. For the self-test's pictures,
        /// so the search's help does not cover the list.
        /// </summary>
        internal static void LeaveBoxes()
        {
            _leaveBoxes = true;
            _focusSearch = false;
        }

        /// <summary>Lets go of the keyboard while the panel draws, where <see cref="LeaveBoxes"/> asked for it.</summary>
        private static void LeaveBoxesIfAsked()
        {
            if (!_leaveBoxes) return;
            GUIUtility.keyboardControl = 0;
            _leaveBoxes = false;
        }

        /// <summary>
        /// Puts new text in the search, however it came (typed, cleared, completed): the list is
        /// filtered anew from its top, the search's help and the mod report give way to it, and
        /// the selection is shown in it.
        /// </summary>
        private static void Searched(Explorer explorer, string text)
        {
            explorer.Text = text;
            ListFiltered();
            RevealSelected();
        }

        /// <summary>
        /// The search box, then which list is shown (help, favourites, recent), then apart from
        /// those the origin switch, labelled so its "All" is not taken for the kind tabs' own.
        /// </summary>
        private static float Controls(Explorer explorer, Rect rect)
        {
            var names = OriginNames;
            var widths = names.Select(n => Skin.Width(Skin.Segment, n) + U(6f)).ToArray();
            const string fromText = "From";
            var fromW = Skin.Width(Skin.FaintLabel, fromText) + U(8f);
            var originW = fromW + widths.Sum() + U(4f) * (names.Length - 1);
            var starW = rect.height;
            var gap = U(8f);
            var recentW = starW;

            // In the compact view the search has the whole first row and the buttons go below it.
            var row = rect.y;
            var navW = starW * 3f + U(4f) * 2f + gap;
            Rect search;
            if (_compact)
            {
                search = new Rect(rect.x + navW, rect.y, rect.width - navW, rect.height);
                row = rect.yMax + U(8f);
            }
            else
            {
                search = new Rect(rect.x + navW, rect.y, rect.width - navW - originW - starW * 2f - recentW - gap * 5f, rect.height);
            }
            ListButton(new Rect(rect.x, rect.y, starW, rect.height));
            var navX = rect.x + starW + U(4f);
            BackAndForward(explorer, new Rect(navX, rect.y, starW, rect.height), new Rect(navX + starW + U(4f), rect.y, starW, rect.height));
            Search(explorer, search);
            var startX = _compact ? rect.x - gap : search.xMax;

            // Help for the search terms.
            var help = new Rect(startX + gap, row, starW, rect.height);
            if (GUI.Button(help, "?", _card == ListCard.Help ? Skin.On : Skin.IconButton)) ToggleCard(ListCard.Help);
            if (help.Contains(Event.current.mousePosition)) AskTip("help", "How to search");

            var star = new Rect(help.xMax + gap, row, starW, rect.height);
            if (GUI.Button(star, GUIContent.none, explorer.FavouritesOnly ? Skin.On : Skin.IconButton))
            {
                explorer.FavouritesOnly = !explorer.FavouritesOnly;
                ListFiltered();
            }
            var icon = new Rect(star.x + star.width * 0.22f, star.y + star.height * 0.22f, star.width * 0.56f, star.height * 0.56f);
            Skin.Icon(icon, explorer.FavouritesOnly ? Skin.Star : Skin.StarHollow, explorer.FavouritesOnly ? Skin.Accent : Skin.Dim);
            if (star.Contains(Event.current.mousePosition)) AskTip("fav", ListWords.FavouritesTip(explorer.FavouritesOnly));

            var recent = new Rect(star.xMax + gap, row, recentW, rect.height);
            if (GUI.Button(recent, GUIContent.none, explorer.RecentOnly ? Skin.On : Skin.IconButton))
            {
                explorer.RecentOnly = !explorer.RecentOnly;
                ListFiltered();
            }
            var clock = new Rect(recent.x + recent.width * 0.22f, recent.y + recent.height * 0.22f, recent.width * 0.56f, recent.height * 0.56f);
            Skin.Icon(clock, Skin.Clock, explorer.RecentOnly ? Skin.Accent : Skin.Dim);
            if (recent.Contains(Event.current.mousePosition)) AskTip("recent", ListWords.RecentTip(explorer.RecentOnly));

            var (originX, nextRow) = HeaderLayout.OriginSwitch(_compact, recent.xMax + gap * 2f, originW, rect.x, rect.xMax);
            var originRow = nextRow ? row + rect.height + U(6f) : row;

            GUI.Label(new Rect(originX, originRow, fromW, rect.height), fromText, Skin.FaintLabel);
            var x = originX + fromW;
            for (var i = 0; i < names.Length; i++)
            {
                var on = (int)explorer.Origin == i;
                if (GUI.Button(new Rect(x, originRow, widths[i], rect.height), names[i], on ? Skin.SegmentOn : Skin.Segment))
                {
                    explorer.Origin = (OriginFilter)i;
                    ListFiltered();
                }
                x += widths[i] + U(4f);
            }
            var originRect = new Rect(originX, originRow, originW, rect.height);
            if (originRect.Contains(Event.current.mousePosition)) AskTip("origin", "Everything, only the game's own, or only what mods added");

            return originRow + rect.height;
        }

        /// <summary>Folds the list away or brings it back; lit while it is folded, so the way back is plain.</summary>
        private static void ListButton(Rect button)
        {
            if (GUI.Button(button, GUIContent.none, ListHidden ? Skin.On : Skin.IconButton)) ToggleList();
            var icon = new Rect(button.x + button.width * 0.24f, button.y + button.height * 0.24f, button.width * 0.52f, button.height * 0.52f);
            Skin.Icon(icon, Skin.ListMark, ListHidden ? Skin.Accent : Skin.Dim);
            if (button.Contains(Event.current.mousePosition)) AskTip("list-button", ListWords.ListButtonTip(ListHidden));
        }

        private static int _steppedFrame = -1;

        /// <summary>Back to the entry shown before, and forward again, as in a browser.</summary>
        private static void BackAndForward(Explorer explorer, Rect back, Rect forward)
        {
            var was = GUI.enabled;
            GUI.enabled = was && (explorer.CanGoBack || Stage.CanStepBack);
            if (GUI.Button(back, "\u2039", Skin.IconButton)) Step(explorer, true);
            GUI.enabled = was && (explorer.CanGoForward || Stage.CanStepForward);
            if (GUI.Button(forward, "\u203A", Skin.IconButton)) Step(explorer, false);
            GUI.enabled = was;
            if (back.Contains(Event.current.mousePosition)) AskTip("back", "Back out of the room you went into, or to the page shown before (Alt+← or the mouse's back button)");
            if (forward.Contains(Event.current.mousePosition)) AskTip("forward", "Forward again, into a room come out of or to an entry (Alt+→ or the mouse's forward button)");
        }

        /// <summary>Goes back or forward, and shows where it lands.</summary>
        public static void Step(Explorer explorer, bool back)
        {
            // The same press can arrive both as a key and as a panel event; it counts once.
            if (explorer == null || _steppedFrame == Time.frameCount) return;
            _steppedFrame = Time.frameCount;
            // Inside a dungeon's example, out of and between the rooms gone into first.
            if (back ? Stage.StepBack() : Stage.StepForward()) return;
            if (!(back ? explorer.Back() : explorer.Forward())) return;
            AfterGoing();
        }

        /// <summary>
        /// A small filter box with its placeholder and, while it holds anything, a button to clear
        /// it, as the search has. Returns what it holds now.
        /// </summary>
        private static string FilterField(string control, string value, Rect rect)
        {
            var e = Event.current;
            var hasText = !string.IsNullOrEmpty(value);
            var clear = new Rect(rect.xMax - U(28f), rect.y + (rect.height - U(22f)) / 2f, U(22f), U(22f));

            // The text field takes every click inside it, so the clear button is handled before it.
            if (hasText && e.type == EventType.MouseDown && e.button == 0 && clear.Contains(e.mousePosition))
            {
                GUIUtility.keyboardControl = 0;
                e.Use();
                return "";
            }

            GUI.SetNextControlName(control);
            value = GUI.TextField(rect, value, 40, Skin.Field);
            if (string.IsNullOrEmpty(value))
            {
                GUI.Label(rect, "Filter", Skin.Placeholder);
                return value;
            }

            ClearCross(clear, "clear-filter:" + control, "Clear the filter");
            return value;
        }

        /// <summary>The cross that clears a box while it holds anything, lit under the mouse.</summary>
        private static void ClearCross(Rect clear, string tipKey, string tip)
        {
            var hover = clear.Contains(Event.current.mousePosition);
            if (hover) Skin.Icon(clear, Skin.Circle, Skin.Light(0.12f));
            Skin.LabelIn(new Rect(clear.x, clear.y - U(1f), clear.width, clear.height), "×", Skin.Cross, hover ? Skin.Text : Skin.Dim);
            if (hover) AskTip(tipKey, tip);
        }

        private static void Search(Explorer explorer, Rect rect)
        {
            Assist.Picks(explorer);
            var e = Event.current;
            var hasText = !string.IsNullOrEmpty(explorer.Text);
            var clear = new Rect(rect.xMax - U(32f), rect.y + (rect.height - U(24f)) / 2f, U(24f), U(24f));

            // The text field takes every click inside it, so the clear button is handled before it.
            if (hasText && e.type == EventType.MouseDown && e.button == 0 && clear.Contains(e.mousePosition))
            {
                Searched(explorer, "");

                // A focused field keeps showing its own copy of the text until it lets go of the keyboard.
                GUIUtility.keyboardControl = 0;
                _focusSearch = true;
                hasText = false;
                e.Use();
            }

            GUI.SetNextControlName(SearchControl);
            var text = GUI.TextField(rect, explorer.Text, 80, Skin.Field);
            if (text != explorer.Text)
            {
                Searched(explorer, text);
            }

            // After the typed text is taken: what Tab puts in must not be undone by it.
            Assist.Tab(explorer);
            hasText = !string.IsNullOrEmpty(explorer.Text);

            if (!hasText)
            {
                GUI.Label(rect, "Search by name, kind:, in:, has:…", Skin.Placeholder);
            }
            else
            {
                ClearCross(clear, "clear-search", "Clear the search");
            }

            Assist.Suggestions(explorer, rect);

            if (_focusSearch && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(SearchControl);
                _focusSearch = false;
            }
        }

        private static readonly Kind[] EveryKind = (Kind[])Enum.GetValues(typeof(Kind));

        /// <summary>
        /// Each tab's text by its name, count, the total it is counted of (none below zero) and
        /// whether it is picked, so drawing the tabs, which happens several times a frame, makes no
        /// new text while the counts stay the same.
        /// </summary>
        private static readonly Dictionary<(string Label, int Count, int Total, bool On), string> TabTexts =
            new Dictionary<(string, int, int, bool), string>();

        private static string TabText(string label, int count, int total, bool on)
        {
            if (TabTexts.TryGetValue((label, count, total, on), out var text)) return text;
            if (TabTexts.Count > 2000) TabTexts.Clear();
            var colour = on ? Skin.TabCountOn : Skin.TabCountOff;
            text = total >= 0 ? CompendiumWords.Tab(label, count, total, colour) : ListWords.Tab(label, count, colour);
            TabTexts[(label, count, total, on)] = text;
            return text;
        }

        /// <summary>Where each kind tab was last drawn, for the self-test.</summary>
        [Diagnostic]
        public static List<Rect> TabsDrawnForTest { get; } = new List<Rect>();

        /// <summary>
        /// One tab per kind with what the search holds of it, wrapping when the row is full; each as
        /// wide as it is picked, its words bold, whether picked or not, so picking one leaves every
        /// tab where it stood.
        /// </summary>
        private static float Tabs(Explorer explorer, Rect rect)
        {
            var flow = new ChipFlow(rect.x, rect.xMax, rect.y, rect.height, U(4f), U(4f));
            var repaint = Event.current.type == EventType.Repaint;
            if (repaint) TabsDrawnForTest.Clear();

            bool Tab(string label, int count, int total, Color dot, bool on, string tipKey = null, string tip = null)
            {
                var style = on ? Skin.TabOn : Skin.Tab;
                var text = TabText(label, count, total, on);
                var width = Skin.Width(Skin.TabOn, TabText(label, count, total, true)) + U(2f);
                var at = flow.Place(width);
                var tab = new Rect(at.X, at.Y, width, flow.RowHeight);
                if (repaint) TabsDrawnForTest.Add(tab);
                var clicked = GUI.Button(tab, text, Skin.Fitted(style, tab));
                var size = U(8f);
                Skin.Icon(new Rect(tab.x + U(10f), tab.y + (tab.height - size) / 2f, size, size), Skin.Circle, on ? Skin.OnAccent : dot);
                if (tip != null && tab.Contains(Event.current.mousePosition)) AskTip(tipKey, tip);
                return clicked;
            }

            // The learned list is a tab of its own after All, with the compendium on and while it
            // holds anything: picking it, or All or a kind, leaves the others.
            var learned = explorer.LearnedOnly;
            if (Tab("All", explorer.CountAll, -1, Skin.Text, explorer.KindFilter == null && !learned)) Filter(explorer, null);
            if (explorer.CompendiumSet && (explorer.LearnedCount > 0 || learned))
            {
                CountDrawn(PanelPart.LearnedTab);
                if (Tab(CompendiumWords.LearnedTab, explorer.LearnedCount, -1, Skin.Accent, learned, "learned", CompendiumWords.LearnedTip(learned)) && !learned)
                {
                    explorer.KindFilter = null;
                    ShowLearned(explorer, true);
                }
            }
            // With the compendium set, peeking or not, and nothing narrowing the list, a kind counted so tells what
            // was found of all there is.
            var collecting = explorer.CompendiumSet && string.IsNullOrEmpty(explorer.Text) && !explorer.FavouritesOnly && !explorer.RecentOnly && !learned && explorer.Origin == OriginFilter.All;
            foreach (var kind in EveryKind)
            {
                var collected = collecting && CompendiumView.Counted(kind);
                var count = collected ? explorer.FoundOf(kind) : explorer.CountOf(kind);
                var total = collected ? explorer.TotalOf(kind) : -1;
                if (count == 0 && explorer.KindFilter != kind) continue;
                if (Tab(Kinds.Label(kind), count, total, Skin.KindColor(kind), explorer.KindFilter == kind && !learned)) Filter(explorer, kind);
            }

            return flow.RowBottom;
        }

        private static float _frontScale = -1f, _floorValueW;

        /// <summary>Whether the compendium's box is open: while the mouse is on its button or on the box, or while the self-test holds it.</summary>
        private static bool _bookOpen;
        private static bool _bookHeld;

        /// <summary>Where the box and its controls stand, worked out with the title row each event, for its clicks taken early and its drawing last.</summary>
        private static Rect _bookBox, _bookLower, _bookValue, _bookRaise;
        private static readonly Rect[] BookSegments = new Rect[CompendiumWords.Rules.Count];
        private static bool _bookFloorShown;
        private static float _bookCaptionW;

        /// <summary>Whether the box shows the world's sources (<see cref="WorldSource"/>): while the compendium is on.</summary>
        private static bool _bookSourcesShown;

        /// <summary>Where each source's levels stand in the box, by source and level, and its caption and hint.</summary>
        private static readonly Rect[][] SourceSegments = Array.ConvertAll(WorldSources.All, _ => new Rect[Enum.GetValues(typeof(SourceLevel)).Length]);
        private static readonly Rect[] SourceNames = new Rect[WorldSources.All.Length];

        /// <summary>Where each ground's pin stands at its row's end (<see cref="CompendiumBook.MarksDungeons"/>); none for a source without ground.</summary>
        private static readonly Rect[] SourcePins = new Rect[WorldSources.All.Length];
        private static Rect _sourcesCaption, _sourcesHint;

        /// <summary>Holds the compendium's box open, or lets it close again; for the self-test.</summary>
        internal static void HoldCompendiumBox(bool open) => _bookHeld = open;

        /// <summary>Whether the box stays open though the mouse is elsewhere, as the notice's chip asked, until the mouse has been on it and left; and whether it has been.</summary>
        private static bool _bookPinned, _bookPinnedVisited;

        /// <summary>Keeps the box open until the mouse has been on it and left it, or lets it close.</summary>
        private static void PinCompendiumBox(bool pin)
        {
            _bookPinned = pin;
            _bookPinnedVisited = false;
        }

        /// <summary>Whether the box shows the players played with: while the compendium is on and another player is in the world; and where its row and the line under it stand.</summary>
        private static bool _bookWithShown;

        private static Rect _withCaption, _withValue, _withHint;

        /// <summary>The compendium's button as last drawn: its width and its label, for the self-test to see the label fits.</summary>
        private static float _bookButtonW;
        private static string _bookLabel;

        /// <summary>How wide the compendium's button was last drawn, and how wide its label is plain and open (bold); for the self-test.</summary>
        internal static (float Width, float Plain, float Open) CompendiumButtonRoom() =>
            _bookLabel == null ? (0f, 0f, 0f) : (_bookButtonW, Skin.Width(Skin.Button, _bookLabel), Skin.Width(Skin.On, _bookLabel));

        /// <summary>
        /// The compendium in the title row, once the character's book is read and only in the full
        /// view: a button naming the rules chosen, the box under it opening while the mouse is on
        /// either, as Clear world's list does, with the rules as segments and, while it is on, the
        /// floor between arrows; and while it is on, the eye peeking at everything for now beside
        /// the button. Left out where it would reach past <paramref name="leftmost"/>. Where the
        /// drawn controls begin, else <paramref name="right"/>.
        /// </summary>
        private static float CompendiumFront(Explorer explorer, float right, float y, float height, float leftmost, Rect room)
        {
            var book = CompendiumKeeper.Book;
            if (book == null || _compact)
            {
                _bookOpen = false;
                return right;
            }
            if (_frontScale != U(1f))
            {
                // The widest biome the floor can say, measured once for each size of the panel.
                _frontScale = U(1f);
                _floorValueW = Enumerable.Range(0, Tiers.Last + 1).Max(f => Skin.Width(Skin.Button, CompendiumWords.FloorName(f))) + U(16f);
            }
            var on = explorer.CompendiumSet;
            var peek = new Rect(right - U(12f) - height, y, height, height);
            var label = CompendiumWords.Button(book.Rule);
            // Drawn bold while its box is open: as wide as the bold label, so it is never clipped.
            var buttonW = ButtonSize.Fitting(Skin.Width(Skin.Button, label), Skin.Width(Skin.On, label), U(10f));
            var button = new Rect((on ? peek.x - U(4f) : right - U(12f)) - buttonW, y, buttonW, height);
            if (button.x < leftmost)
            {
                _bookOpen = false;
                return right;
            }
            CountDrawn(PanelPart.CompendiumSwitch);
            _bookButtonW = buttonW;
            _bookLabel = label;
            var e = Event.current;
            GUI.Button(button, label, _bookOpen ? Skin.On : Skin.Button);
            if (!_bookOpen && button.Contains(e.mousePosition)) AskTip("mode", CompendiumWords.ModeTip);
            if (on) PeekButton(explorer, peek);
            BookLayout(book, button, height, room, e);
            return button.x;
        }

        /// <summary>The eye beside the compendium's button while it is on, peeking at everything for now, lit while it does.</summary>
        private static void PeekButton(Explorer explorer, Rect rect)
        {
            CountDrawn(PanelPart.PeekButton);
            if (GUI.Button(rect, GUIContent.none, explorer.Peeking ? Skin.On : Skin.IconButton)) Peek(explorer, !explorer.Peeking);
            var icon = new Rect(rect.x + rect.width * 0.2f, rect.y + rect.height * 0.2f, rect.width * 0.6f, rect.height * 0.6f);
            Skin.Icon(icon, Skin.Eye, explorer.Peeking ? Skin.Accent : Skin.Dim);
            if (rect.Contains(Event.current.mousePosition)) AskTip("peek", CompendiumWords.PeekTip(explorer.Peeking));
        }

        /// <summary>
        /// Where the box and its controls stand under the button, kept inside the panel: its
        /// caption, the rules' segments and, while the compendium is on and a floor can be set,
        /// the floor between its arrows; while it is on, what the world shows as known, a row of
        /// levels for each source and the faint line under them, then the players played with and
        /// the line under them; and whether it is open, while the mouse is on the button or the
        /// box, with a little room between them, or while the notice's chip keeps it open.
        /// </summary>
        private static void BookLayout(CompendiumBook book, Rect button, float height, Rect room, Event e)
        {
            var pad = U(10f);
            var gap = U(4f);
            _bookFloorShown = book.Rule != CompendiumRule.Off && (CompendiumKeeper.Ceiling > 0 || book.Floor > 0);
            _bookSourcesShown = book.Rule != CompendiumRule.Off;
            ReadPlayingWith();
            _bookWithShown = _bookSourcesShown && _withRow != null;
            _bookCaptionW = Mathf.Max(Skin.Width(Skin.FaintLabel, CompendiumWords.RulesCaption), Skin.Width(Skin.FaintLabel, CompendiumWords.FloorCaption) + U(18f));
            if (_bookWithShown) _bookCaptionW = Mathf.Max(_bookCaptionW, Skin.Width(Skin.FaintLabel, PlayingWithWords.RowCaption));
            var sourcesW = 0f;
            if (_bookSourcesShown)
            {
                foreach (var source in WorldSources.All)
                {
                    _bookCaptionW = Mathf.Max(_bookCaptionW, Skin.Width(Skin.FaintLabel, CompendiumWords.SourceName(source)));
                    var levelsW = 0f;
                    foreach (var level in WorldSources.Offered(source)) levelsW += Skin.Width(Skin.Segment, CompendiumWords.LevelName(level)) + U(6f) + (levelsW > 0f ? gap : 0f);
                    if (WorldSources.HasGround(source)) levelsW += gap + height;
                    sourcesW = Mathf.Max(sourcesW, levelsW);
                }
            }
            _bookCaptionW += U(10f);
            var segmentsW = 0f;
            for (var i = 0; i < CompendiumWords.Rules.Count; i++) segmentsW += Skin.Width(Skin.Segment, CompendiumWords.RuleName(CompendiumWords.Rules[i])) + U(6f) + (i > 0 ? gap : 0f);
            var floorW = _bookFloorShown ? height * 2f + _floorValueW + gap * 2f : 0f;
            var captionsW = Mathf.Max(Skin.Width(Skin.FaintLabel, CompendiumWords.BoxCaption), _bookSourcesShown ? Skin.Width(Skin.FaintLabel, CompendiumWords.SourcesCaption) : 0f);
            var width = Mathf.Min(room.width, Mathf.Max(captionsW, _bookCaptionW + Mathf.Max(segmentsW, Mathf.Max(floorW, sourcesW))) + pad * 2f);
            var rows = _bookFloorShown ? 2 : 1;
            var x = Mathf.Clamp(button.xMax - width, room.x, room.xMax - width);
            var sourcesH = 0f;
            var hintH = 0f;
            if (_bookSourcesShown)
            {
                hintH = Skin.DimWrap.CalcHeight(new GUIContent(CompendiumWords.SourcesHint), width - pad * 2f - U(30f));
                sourcesH = U(14f) + U(22f) + WorldSources.All.Length * (height + U(6f)) + hintH;
            }
            var withH = 0f;
            var rowH = 0f;
            var withHintH = 0f;
            if (_bookWithShown)
            {
                rowH = Mathf.Max(height, Skin.Wrap.CalcHeight(new GUIContent(_withRow), width - pad * 2f - _bookCaptionW));
                withHintH = Skin.DimWrap.CalcHeight(new GUIContent(PlayingWithWords.RowHint), width - pad * 2f - U(30f));
                withH = U(14f) + rowH + U(6f) + withHintH;
            }
            _bookBox = new Rect(x, button.yMax + gap, width, pad + U(26f) + rows * height + (rows - 1) * U(6f) + sourcesH + withH + pad);

            var rowY = _bookBox.y + pad + U(26f);
            var sx = _bookBox.x + pad + _bookCaptionW;
            for (var i = 0; i < CompendiumWords.Rules.Count; i++)
            {
                var w = Skin.Width(Skin.Segment, CompendiumWords.RuleName(CompendiumWords.Rules[i])) + U(6f);
                BookSegments[i] = new Rect(sx, rowY, w, height);
                sx += w + gap;
            }
            var floorY = rowY + height + U(6f);
            _bookLower = new Rect(_bookBox.x + pad + _bookCaptionW, floorY, height, height);
            _bookValue = new Rect(_bookLower.xMax + gap, floorY, _floorValueW, height);
            _bookRaise = new Rect(_bookValue.xMax + gap, floorY, height, height);
            if (_bookSourcesShown)
            {
                var sourceY = rowY + rows * height + (rows - 1) * U(6f) + U(14f);
                _sourcesCaption = new Rect(_bookBox.x + pad, sourceY, _bookBox.width - pad * 2f, U(20f));
                sourceY += U(22f);
                for (var s = 0; s < WorldSources.All.Length; s++)
                {
                    SourceNames[s] = new Rect(_bookBox.x + pad, sourceY, _bookCaptionW, height);
                    var lx = _bookBox.x + pad + _bookCaptionW;
                    for (var l = 0; l < SourceSegments[s].Length; l++) SourceSegments[s][l] = Rect.zero;
                    foreach (var level in WorldSources.Offered(WorldSources.All[s]))
                    {
                        var w = Skin.Width(Skin.Segment, CompendiumWords.LevelName(level)) + U(6f);
                        SourceSegments[s][(int)level] = new Rect(lx, sourceY, w, height);
                        lx += w + gap;
                    }
                    SourcePins[s] = WorldSources.HasGround(WorldSources.All[s]) ? new Rect(lx, sourceY, height, height) : Rect.zero;
                    sourceY += height + U(6f);
                }
                _sourcesHint = new Rect(_bookBox.x + pad, sourceY, _bookBox.width - pad * 2f, hintH);
            }
            if (_bookWithShown)
            {
                var withY = _bookBox.yMax - pad - withH + U(14f);
                _withCaption = new Rect(_bookBox.x + pad, withY, _bookCaptionW, height);
                _withValue = new Rect(_bookBox.x + pad + _bookCaptionW, withY, _bookBox.width - pad * 2f - _bookCaptionW, rowH);
                _withHint = new Rect(_bookBox.x + pad, withY + rowH + U(6f), _bookBox.width - pad * 2f, withHintH);
            }

            var zone = button;
            if (_bookOpen)
            {
                var left = Mathf.Min(button.x, _bookBox.x) - U(6f);
                var right = Mathf.Max(button.xMax, _bookBox.xMax) + U(6f);
                zone = new Rect(left, button.y - U(6f), right - left, _bookBox.yMax - button.y + U(12f));
            }
            var on = zone.Contains(e.mousePosition);
            if (_bookPinned && on) _bookPinnedVisited = true;
            else if (_bookPinned && _bookPinnedVisited) PinCompendiumBox(false);
            _bookOpen = _bookHeld || _bookPinned || on;
        }

        /// <summary>Whether the floor can be raised from where it is, as far as the bosses that fell in a row reach (<see cref="CompendiumKeeper.Ceiling"/>).</summary>
        private static bool CanRaise(int floor) => floor < CompendiumKeeper.Ceiling && floor < Tiers.Last;

        /// <summary>Before anything under the box is drawn: a click on one of its rules, or on the floor's arrows.</summary>
        private static void CompendiumBoxClicks(Event e)
        {
            var book = CompendiumKeeper.Book;
            if (!_bookOpen || book == null || e.type != EventType.MouseDown || !_bookBox.Contains(e.mousePosition)) return;
            if (e.button == 0)
            {
                for (var i = 0; i < CompendiumWords.Rules.Count; i++)
                {
                    var rule = CompendiumWords.Rules[i];
                    if (BookSegments[i].Contains(e.mousePosition) && book.Rule != rule) Choose(rule);
                }
                if (_bookFloorShown && _bookLower.Contains(e.mousePosition) && book.Floor > 0) SetFloor(book, book.Floor - 1);
                else if (_bookFloorShown && _bookRaise.Contains(e.mousePosition) && CanRaise(book.Floor)) SetFloor(book, book.Floor + 1);
                for (var s = 0; _bookSourcesShown && s < WorldSources.All.Length; s++)
                {
                    foreach (var level in WorldSources.Offered(WorldSources.All[s]))
                    {
                        if (level == SourceLevel.WithInsides && !OffersInsides(book, WorldSources.All[s])) continue;
                        if (SourceSegments[s][(int)level].Contains(e.mousePosition) && book.Shows(WorldSources.All[s]) != level) ShowSource(WorldSources.All[s], level);
                    }
                    if (SourcePins[s].Contains(e.mousePosition)) MarkDungeons(WorldSources.All[s], !book.MarksDungeons(WorldSources.All[s]));
                }
            }
            e.Use();
        }

        /// <summary>
        /// Drawn last, over what lies below the button: the box's caption, the rules with the one
        /// chosen lit, and the floor's biome between arrows lowering and raising it, tier by tier;
        /// each with its tip.
        /// </summary>
        private static void DrawCompendiumBox(Explorer explorer)
        {
            var book = CompendiumKeeper.Book;
            if (!_bookOpen || book == null || _compact) return;
            var mouse = Event.current.mousePosition;
            var pad = U(10f);
            var repaint = Event.current.type == EventType.Repaint;
            if (repaint)
            {
                CountDrawn(PanelPart.CompendiumBox);
                Skin.Box(_bookBox, Skin.Backdrop, Skin.Outline);
                GUI.Label(new Rect(_bookBox.x + pad, _bookBox.y + pad, _bookBox.width - pad * 2f, U(20f)), CompendiumWords.BoxCaption, Skin.FaintLabel);
                GUI.Label(new Rect(_bookBox.x + pad, BookSegments[0].y, _bookCaptionW, BookSegments[0].height), CompendiumWords.RulesCaption, Skin.FaintLabel);
            }
            for (var i = 0; i < CompendiumWords.Rules.Count; i++)
            {
                var rule = CompendiumWords.Rules[i];
                if (repaint) GUI.Button(BookSegments[i], CompendiumWords.RuleName(rule), book.Rule == rule ? Skin.SegmentOn : Skin.Segment);
                if (BookSegments[i].Contains(mouse)) AskTip("mode-" + CompendiumWords.RuleName(rule), CompendiumWords.StepTip(rule));
            }
            DrawSources(book, mouse, repaint);
            DrawPlayingWith(repaint);
            if (!_bookFloorShown) return;
            CountDrawn(PanelPart.FloorControl);
            var floor = book.Floor;
            var canRaise = CanRaise(floor);
            if (repaint)
            {
                GUI.Label(new Rect(_bookBox.x + pad, _bookLower.y, _bookCaptionW, _bookLower.height), CompendiumWords.FloorCaption, Skin.FaintLabel);
                // The floor's own mark after its caption, as what it shows is marked.
                var stairs = U(13f);
                Skin.Icon(new Rect(_bookBox.x + pad + Skin.Width(Skin.FaintLabel, CompendiumWords.FloorCaption) + U(5f), _bookLower.y + (_bookLower.height - stairs) / 2f, stairs, stairs), Skin.Steps, Skin.Faint);
                var was = GUI.enabled;
                GUI.enabled = was && floor > 0;
                GUI.Button(_bookLower, "\u2039", Skin.IconButton);
                GUI.enabled = was && canRaise;
                GUI.Button(_bookRaise, "\u203A", Skin.IconButton);
                GUI.enabled = was;
                GUI.Label(_bookValue, CompendiumWords.FloorName(floor), Skin.Button);
            }
            if (floor > 0 && _bookLower.Contains(mouse)) AskTip("floor-down", CompendiumWords.LowerFloor(floor - 1));
            if (floor < Tiers.Last && (_bookRaise.Contains(mouse) || _bookValue.Contains(mouse)))
            {
                var boss = explorer.Find(Tiers.BossOf(floor + 1))?.ShownName ?? Naming.Readable(Tiers.BossOf(floor + 1));
                AskTip("floor-up", canRaise ? CompendiumWords.RaiseFloor(floor + 1, boss) : CompendiumWords.FloorWaits(boss));
            }
        }

        /// <summary>
        /// Whether a row offers With insides: some place it found in this world has insides
        /// (<see cref="DungeonKinds"/>), or no place is read yet to tell; dimmed otherwise.
        /// </summary>
        private static bool OffersInsides(CompendiumBook book, WorldSource source) =>
            DungeonKinds.All.Count == 0 || WorldSources.HasInsides(book.Found(CompendiumMap.World, source), DungeonKinds.Is);

        /// <summary>The box's rows for the world's sources: the caption, each source's name with its levels, the one chosen lit, With insides dimmed where nothing found has insides, and the faint line saying what turning one down does; each with its tip.</summary>
        private static void DrawSources(CompendiumBook book, Vector2 mouse, bool repaint)
        {
            if (!_bookSourcesShown) return;
            CountDrawn(PanelPart.SourcesControl);
            if (repaint)
            {
                GUI.Label(_sourcesCaption, CompendiumWords.SourcesCaption, Skin.FaintLabel);
                BoxHint(_sourcesHint, CompendiumWords.SourcesHint, Skin.FoldedMap);
            }
            for (var s = 0; s < WorldSources.All.Length; s++)
            {
                var source = WorldSources.All[s];
                if (repaint) GUI.Label(SourceNames[s], CompendiumWords.SourceName(source), Skin.FaintLabel);
                if (SourceNames[s].Contains(mouse)) AskTip("source-" + source, CompendiumWords.SourceTip(source));
                var shows = book.Shows(source);
                foreach (var level in WorldSources.Offered(source))
                {
                    var at = SourceSegments[s][(int)level];
                    var dimmed = level == SourceLevel.WithInsides && shows != level && !OffersInsides(book, source);
                    if (dimmed) CountDrawn(PanelPart.InsidesDimmed);
                    if (repaint)
                    {
                        var was = GUI.enabled;
                        GUI.enabled = was && !dimmed;
                        GUI.Button(at, CompendiumWords.LevelName(level), shows == level ? Skin.SegmentOn : Skin.Segment);
                        GUI.enabled = was;
                    }
                    if (at.Contains(mouse)) AskTip("source-level-" + level, dimmed ? CompendiumWords.NoInsides(source) : CompendiumWords.LevelTip(level));
                }
                DrawPin(book, source, SourcePins[s], mouse, repaint);
            }
        }

        /// <summary>A ground's pin: the big map's dungeon mark on a segment, in the accent while on, faint while off, saying on hover what it does and whether it is on.</summary>
        private static void DrawPin(CompendiumBook book, WorldSource source, Rect pin, Vector2 mouse, bool repaint)
        {
            if (pin.width <= 0f) return;
            CountDrawn(PanelPart.DungeonPins);
            var on = book.MarksDungeons(source);
            if (repaint)
            {
                GUI.Button(pin, GUIContent.none, Skin.Segment);
                var inset = U(4f);
                Skin.Icon(new Rect(pin.x + inset, pin.y + inset, pin.width - inset * 2f, pin.height - inset * 2f), Skin.Doorway, on ? Skin.Accent : Skin.Alpha(Skin.Faint, 0.45f));
            }
            if (pin.Contains(mouse)) AskTip("source-pin-" + source, Naming.Lines(CompendiumWords.PinName, CompendiumWords.PinTip(on)));
        }

        /// <summary>Turns a ground's pin on or off for the character played and keeps it at once, asking about the ground as it goes on: a choice of the player's. The pin, and the self-test.</summary>
        public static void MarkDungeons(WorldSource source, bool on)
        {
            var book = CompendiumKeeper.Book;
            if (book == null) return;
            book.MarkDungeons(source, on);
            CompendiumKeeper.Save(now: true);
            if (on) DungeonsInGround.AskNow(source);
        }

        /// <summary>A line in the box as a page's hint line: the accent bar and its mark, the compass or the mark it speaks of, its words a step brighter.</summary>
        private static void BoxHint(Rect rect, string said, Texture2D mark)
        {
            Skin.Fill(new Rect(rect.x + U(6f), rect.y + U(2f), U(2f), rect.height - U(4f)), Skin.Accent);
            Skin.Icon(new Rect(rect.x + U(12f), rect.y + U(3f), U(14f), U(14f)), mark, Skin.Accent);
            GUI.Label(new Rect(rect.x + U(30f), rect.y, rect.width - U(30f), rect.height), said, Skin.DimWrap);
        }

        /// <summary>The box's row of the players played with, each with whether their game has Scry, and the line saying what one without it means.</summary>
        private static void DrawPlayingWith(bool repaint)
        {
            if (!_bookWithShown) return;
            CountDrawn(PanelPart.PlayingWithRow);
            if (!repaint) return;
            GUI.Label(_withCaption, PlayingWithWords.RowCaption, Skin.FaintLabel);
            GUI.Label(_withValue, _withRow, Skin.Wrap);
            BoxHint(_withHint, PlayingWithWords.RowHint, Skin.Compass);
        }

        /// <summary>Sets how a source of the world shows for the character played and keeps it at once: a choice of the player's. The box, and the self-test.</summary>
        public static void ShowSource(WorldSource source, SourceLevel level)
        {
            var book = CompendiumKeeper.Book;
            if (book == null) return;
            book.Show(source, level);
            CompendiumKeeper.Save(now: true);
            ListFiltered();
        }

        /// <summary>Chooses the compendium's rules for the character played and keeps them at once: a choice of the player's. The mode's arrows, and the self-test.</summary>
        public static void Choose(CompendiumRule rule)
        {
            var book = CompendiumKeeper.Book;
            if (book == null) return;
            book.Rule = rule;
            CompendiumKeeper.Save(now: true);
            ListFiltered();
        }

        /// <summary>Sets the character's floor and keeps it at once: a choice of the player's.</summary>
        private static void SetFloor(CompendiumBook book, int floor)
        {
            book.Floor = floor;
            CompendiumKeeper.Save(now: true);
            ListFiltered();
        }

        private static void Filter(Explorer explorer, Kind? kind)
        {
            // The learned list is a tab of its own, so All or a kind leaves it.
            explorer.LearnedOnly = false;
            explorer.KindFilter = kind;
            ListFiltered();
            RevealSelected();
        }
    }
}
