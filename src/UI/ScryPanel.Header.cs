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
            if (GUI.Button(help, "?", _help ? Skin.On : Skin.IconButton))
            {
                _help = !_help;
                _modReport = false;
                _offDetails = false;
            }
            if (help.Contains(Event.current.mousePosition)) AskTip("help", "How to search");

            var star = new Rect(help.xMax + gap, row, starW, rect.height);
            if (GUI.Button(star, GUIContent.none, explorer.FavouritesOnly ? Skin.On : Skin.IconButton))
            {
                explorer.FavouritesOnly = !explorer.FavouritesOnly;
                _listScroll = Vector2.zero;
                _help = false;
                _modReport = false;
            }
            var icon = new Rect(star.x + star.width * 0.22f, star.y + star.height * 0.22f, star.width * 0.56f, star.height * 0.56f);
            Skin.Icon(icon, explorer.FavouritesOnly ? Skin.Star : Skin.StarHollow, explorer.FavouritesOnly ? Skin.Accent : Skin.Dim);
            if (star.Contains(Event.current.mousePosition)) AskTip("fav", explorer.FavouritesOnly ? "Showing only favourites" : "Show only favourites");

            var recent = new Rect(star.xMax + gap, row, recentW, rect.height);
            if (GUI.Button(recent, GUIContent.none, explorer.RecentOnly ? Skin.On : Skin.IconButton))
            {
                explorer.RecentOnly = !explorer.RecentOnly;
                _listScroll = Vector2.zero;
                _help = false;
                _modReport = false;
            }
            var clock = new Rect(recent.x + recent.width * 0.22f, recent.y + recent.height * 0.22f, recent.width * 0.56f, recent.height * 0.56f);
            Skin.Icon(clock, Skin.Clock, explorer.RecentOnly ? Skin.Accent : Skin.Dim);
            if (recent.Contains(Event.current.mousePosition)) AskTip("recent", explorer.RecentOnly ? "Showing what you looked at last, newest first" : "Show what you looked at last, newest first");

            var originX = recent.xMax + gap * 2f;
            var originRow = row;
            if (originX + originW > rect.xMax)
            {
                originX = rect.x;
                originRow = row + rect.height + U(6f);
            }

            GUI.Label(new Rect(originX, originRow, fromW, rect.height), fromText, Skin.FaintLabel);
            var x = originX + fromW;
            for (var i = 0; i < names.Length; i++)
            {
                var on = (int)explorer.Origin == i;
                if (GUI.Button(new Rect(x, originRow, widths[i], rect.height), names[i], on ? Skin.SegmentOn : Skin.Segment))
                {
                    explorer.Origin = (OriginFilter)i;
                    _listScroll = Vector2.zero;
                    _help = false;
                    _modReport = false;
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
            if (button.Contains(Event.current.mousePosition)) AskTip("list-button", ListHidden ? "Bring the list back" : "Fold the list away, leaving the room to the details");
        }

        private static int _steppedFrame = -1;

        /// <summary>Back to the entry shown before, and forward again, as in a browser.</summary>
        private static void BackAndForward(Explorer explorer, Rect back, Rect forward)
        {
            var was = GUI.enabled;
            GUI.enabled = was && explorer.CanGoBack;
            if (GUI.Button(back, "\u2039", Skin.IconButton)) Step(explorer, true);
            GUI.enabled = was && explorer.CanGoForward;
            if (GUI.Button(forward, "\u203A", Skin.IconButton)) Step(explorer, false);
            GUI.enabled = was;
            if (back.Contains(Event.current.mousePosition)) AskTip("back", "Back to the entry shown before (mouse back button)");
            if (forward.Contains(Event.current.mousePosition)) AskTip("forward", "Forward again (mouse forward button)");
        }

        /// <summary>Goes back or forward, and shows where it lands.</summary>
        public static void Step(Explorer explorer, bool back)
        {
            // The same press can arrive both as a key and as a panel event; it counts once.
            if (explorer == null || _steppedFrame == Time.frameCount) return;
            _steppedFrame = Time.frameCount;
            if (!(back ? explorer.Back() : explorer.Forward())) return;
            _reveal = true;
            _sideScroll = Vector2.zero;
            _help = false;
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

            var hover = clear.Contains(e.mousePosition);
            if (hover) Skin.Icon(clear, Skin.Circle, new Color(1f, 1f, 1f, 0.12f));
            var style = Skin.Cross;
            var was = style.normal.textColor;
            style.normal.textColor = hover ? Skin.Text : Skin.Dim;
            GUI.Label(new Rect(clear.x, clear.y - U(1f), clear.width, clear.height), "×", style);
            style.normal.textColor = was;
            if (hover) AskTip("clear-filter:" + control, "Clear the filter");
            return value;
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
                explorer.Text = "";
                _listScroll = Vector2.zero;
                _reveal = true;

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
                explorer.Text = text;
                _listScroll = Vector2.zero;
                _reveal = true;
                _help = false;
                _modReport = false;
            }

            // After the typed text is taken: what Tab puts in must not be undone by it.
            Assist.Tab(explorer);
            hasText = !string.IsNullOrEmpty(explorer.Text);

            if (!hasText)
            {
                GUI.Label(rect, "Search by name", Skin.Placeholder);
            }
            else
            {
                var hover = clear.Contains(e.mousePosition);
                if (hover) Skin.Icon(clear, Skin.Circle, new Color(1f, 1f, 1f, 0.12f));
                var style = Skin.Cross;
                var was = style.normal.textColor;
                style.normal.textColor = hover ? Skin.Text : Skin.Dim;
                GUI.Label(new Rect(clear.x, clear.y - U(1f), clear.width, clear.height), "×", style);
                style.normal.textColor = was;
                if (hover) AskTip("clear-search", "Clear the search");
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
        /// Each tab's text by its name, count and whether it is picked, so drawing the tabs, which
        /// happens several times a frame, makes no new text while the counts stay the same.
        /// </summary>
        private static readonly Dictionary<(string Label, int Count, bool On), string> TabTexts =
            new Dictionary<(string, int, bool), string>();

        private static string TabText(string label, int count, bool on)
        {
            if (TabTexts.TryGetValue((label, count, on), out var text)) return text;
            if (TabTexts.Count > 2000) TabTexts.Clear();
            text = $"{label}  <color=#{(on ? "5a4526" : "8f929c")}>{Naming.Count(count)}</color>";
            TabTexts[(label, count, on)] = text;
            return text;
        }

        /// <summary>One tab per kind with what the search holds of it, wrapping when the row is full.</summary>
        private static float Tabs(Explorer explorer, Rect rect)
        {
            var x = rect.x;
            var y = rect.y;
            var tabH = rect.height;
            var gap = U(4f);

            bool Tab(string label, int count, Color dot, bool on)
            {
                var style = on ? Skin.TabOn : Skin.Tab;
                var text = TabText(label, count, on);
                var width = Skin.Width(style, text) + U(2f);
                if (x + width > rect.xMax && x > rect.x)
                {
                    x = rect.x;
                    y += tabH + gap;
                }
                var tab = new Rect(x, y, width, tabH);
                var clicked = GUI.Button(tab, text, style);
                var size = U(8f);
                Skin.Icon(new Rect(tab.x + U(10f), tab.y + (tabH - size) / 2f, size, size), Skin.Circle, on ? Skin.OnAccent : dot);
                x += width + gap;
                return clicked;
            }

            if (Tab("All", explorer.CountAll, Skin.Text, explorer.KindFilter == null)) Filter(explorer, null);
            foreach (var kind in EveryKind)
            {
                var count = explorer.CountOf(kind);
                if (count == 0 && explorer.KindFilter != kind) continue;
                if (Tab(Kinds.Label(kind), count, Skin.KindColor(kind), explorer.KindFilter == kind)) Filter(explorer, kind);
            }

            return y + tabH;
        }

        private static void Filter(Explorer explorer, Kind? kind)
        {
            explorer.KindFilter = kind;
            _listScroll = Vector2.zero;
            _help = false;
            _modReport = false;
            _reveal = true;
        }
    }
}
