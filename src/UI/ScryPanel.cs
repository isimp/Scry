using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The panel. In full view: search, favourites and origin across the top, the kind tabs under
    /// them, the list on the left and the preview with everything that can be done with the
    /// selection on the right. In compact view it is a slim column at the side of the screen with
    /// no turntable, which leaves the middle free for watching a preview in the world.
    ///
    /// Drawn with IMGUI in plain rectangles rather than automatic layout, which keeps a list of
    /// thousands of rows cheap: only the rows in view are drawn. Sizes are in design units scaled
    /// by the screen height and the PanelScale setting.
    /// </summary>
    internal static class ScryPanel
    {
        private const string SearchControl = "scry-search";
        private const string ClipControl = "scry-clip-filter";
        private const string EffectControl = "scry-effect-filter";
        private const float TipDelay = 0.35f;

        private static Rect _full;
        private static Rect _compactRect;
        private static bool _compact;
        private static bool _placed;
        private static float _s = 1f;

        private static Vector2 _listScroll;
        private static Vector2 _sideScroll;
        private static float _sideHeight;
        private static int _rowsInView = 10;

        private static bool _focusSearch;
        private static bool _reveal;
        private static string _clipFilter = "";
        private static bool _help;
        private static float _badgeWidth;
        private static string _effectFilter = "";

        private enum Drag { None, Move, Resize, Orbit, StageSize, Pan }

        /// <summary>The stage's height against its usual one, set by dragging its bottom edge.</summary>
        private static float _stageScale = 1f;
        private static float _stageBaseH = 300f;
        private static Drag _drag;
        private static bool _failed;

        // A tooltip asked for during this repaint, and the one showing.
        private static string _askedTipKey;
        private static string _askedTipText;
        private static Vector2 _askedTipAt;
        private static string _tipKey;
        private static float _tipSince;

        /// <summary>Whether the search box has the keyboard, so a letter key does not close the panel.</summary>
        public static bool SearchFocused { get; private set; }

        /// <summary>Whether any of the panel's text boxes has the keyboard.</summary>
        public static bool Typing { get; private set; }

        /// <summary>Whether the panel is in its compact view.</summary>
        public static bool Compact => _compact;

        private static Rect Win
        {
            get => _compact ? _compactRect : _full;
            set
            {
                if (_compact) _compactRect = value;
                else _full = value;
            }
        }

        public static void Opened()
        {
            Skin.LookForFontsAgain();

            // In the compact view the keys walk until the search is clicked, so it is not focused on opening.
            _focusSearch = !_compact;
            _reveal = true;
        }

        /// <summary>Whether a mouse position (in Unity's bottom-up screen coordinates) is over the panel.</summary>
        public static bool Covers(Vector3 mouse)
        {
            return Session.IsOpen && Win.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
        }

        private static float U(float v) => Mathf.Round(v * _s);

        private static float Scale() => Mathf.Clamp(Screen.height / 1080f, 0.75f, 3f) * Plugin.UiScale;

        public static void OnGUI()
        {
            var scale = Scale();
            if (!Session.IsOpen || Session.Explorer == null)
            {
                SearchFocused = false;
                Typing = false;
                Skin.Warm(scale);
                return;
            }

            var skin = GUI.skin;
            try
            {
                Skin.Ensure(scale);
                _s = scale;
                GUI.skin = Skin.Gui;
                if (Event.current.type == EventType.Repaint) _askedTipKey = null;

                Place();
                var explorer = Session.Explorer;

                // A click anywhere lets go of the keyboard; a click on a text box takes it straight
                // back. So clicking the list or a button after typing hands the keys back to walking.
                if (Event.current.type == EventType.MouseDown) GUIUtility.keyboardControl = 0;

                Keys(explorer);
                if (!Session.IsOpen) return;

                Draw(explorer);
                Drags();
                Tooltip();

                // The panel is solid: clicks and the wheel over it stop here.
                var e = Event.current;
                if (Win.Contains(e.mousePosition) && (e.isMouse || e.type == EventType.ScrollWheel)) e.Use();

                var focused = GUI.GetNameOfFocusedControl();
                SearchFocused = focused == SearchControl;
                Typing = focused == SearchControl || focused == ClipControl || focused == EffectControl;
            }
            catch (Exception ex)
            {
                if (!_failed) Plugin.Log.LogError($"Scry panel: {ex}");
                _failed = true;
            }
            finally
            {
                GUI.skin = skin;
            }
        }

        // ----- Keyboard -----

        private static void Keys(Explorer explorer)
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;

            switch (e.keyCode)
            {
                case KeyCode.Escape:
                    Session.Hide();
                    e.Use();
                    break;
                case KeyCode.UpArrow:
                    Step(explorer, -1);
                    e.Use();
                    break;
                case KeyCode.DownArrow:
                    Step(explorer, 1);
                    e.Use();
                    break;
                case KeyCode.PageUp:
                    Step(explorer, -Math.Max(1, _rowsInView - 1));
                    e.Use();
                    break;
                case KeyCode.PageDown:
                    Step(explorer, Math.Max(1, _rowsInView - 1));
                    e.Use();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Primary(explorer.Selected);
                    e.Use();
                    break;
                case KeyCode.F:
                    if (e.control)
                    {
                        _focusSearch = true;
                        e.Use();
                    }
                    break;
            }
        }

        private static void Step(Explorer explorer, int rows)
        {
            explorer.Move(rows);
            _reveal = true;
        }

        /// <summary>What Enter and a double click do: the main thing for the kind.</summary>
        private static void Primary(Entry entry)
        {
            if (entry == null) return;

            switch (entry.Kind)
            {
                case Kind.Sound:
                    Previews.PlaySound(entry);
                    break;
                case Kind.Effect:
                    Previews.PlayEffect(entry, onYou: false);
                    break;
                case Kind.Projectile:
                    Previews.Fire(entry);
                    break;
                case Kind.StatusEffect:
                    if (Previews.StatusShowing) Previews.StopStatus(true);
                    else Previews.ShowStatus(entry);
                    break;
                default:
                    if (Previews.IsModel(entry)) Previews.ToggleWorld();
                    break;
            }
        }

        // ----- Window -----

        private static void Place()
        {
            if (!_placed)
            {
                _placed = true;
                var w = Mathf.Min(U(1180f), Screen.width - U(40f));
                var h = Mathf.Min(U(760f), Screen.height - U(40f));
                _full = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
                var cw = Mathf.Min(U(500f), Screen.width - U(40f));
                _compactRect = new Rect(Screen.width - cw - U(20f), U(40f), cw, Screen.height - U(80f));
                LoadRects();
            }

            var win = Win;
            var minW = Mathf.Min(U(_compact ? 420f : 820f), Screen.width);
            var minH = Mathf.Min(U(_compact ? 440f : 540f), Screen.height);
            win.width = Mathf.Clamp(win.width, minW, Screen.width);
            win.height = Mathf.Clamp(win.height, minH, Screen.height);
            win.x = Mathf.Clamp(win.x, 0f, Screen.width - win.width);
            win.y = Mathf.Clamp(win.y, 0f, Screen.height - win.height);
            Win = win;
        }

        private static void ToggleCompact()
        {
            _compact = !_compact;
            _sideScroll = Vector2.zero;
            _reveal = true;
            SaveRects();
        }

        private static void Drags()
        {
            var e = Event.current;
            if (_drag == Drag.None) return;

            if (e.rawType == EventType.MouseUp)
            {
                if (_drag == Drag.Move || _drag == Drag.Resize || _drag == Drag.StageSize) SaveRects();
                _drag = Drag.None;
                Stage.Dragging = false;
                return;
            }

            if (e.type != EventType.MouseDrag) return;

            var win = Win;
            switch (_drag)
            {
                case Drag.Move:
                    win.position += e.delta;
                    break;
                case Drag.Resize:
                    win.width += e.delta.x;
                    win.height += e.delta.y;
                    break;
                case Drag.Orbit:
                    Stage.Orbit(e.delta);
                    break;
                case Drag.Pan:
                    Stage.Pan(e.delta);
                    break;
                case Drag.StageSize:
                    _stageScale = Mathf.Clamp(_stageScale + e.delta.y / Mathf.Max(1f, _stageBaseH), 0.4f, 2.4f);
                    break;
            }
            Win = win;
            e.Use();
        }

        private static string RectFile => Path.Combine(Plugin.DataFolder, "panel.txt");

        /// <summary>
        /// Where the panel was, in both views, and which view was in use. One line each:
        /// "full x y w h", "compact x y w h" and "view full|compact".
        /// </summary>
        private static void LoadRects()
        {
            try
            {
                if (!File.Exists(RectFile)) return;
                foreach (var line in File.ReadAllLines(RectFile))
                {
                    var parts = line.Split(' ');
                    if (parts.Length == 2 && parts[0] == "view") _compact = parts[1] == "compact";
                    if (parts.Length == 2 && parts[0] == "light" && int.TryParse(parts[1], out var light)) Stage.LightingIndex = light;
                    if (parts.Length == 2 && parts[0] == "backdrop" && int.TryParse(parts[1], out var backdrop)) Stage.BackdropIndex = backdrop;
                    if (parts.Length == 2 && parts[0] == "person") Stage.ShowPerson = parts[1] == "1";
                    if (parts.Length == 2 && parts[0] == "worn") Looks.OnPerson = parts[1] == "1";
                    if (parts.Length == 2 && parts[0] == "spin") Stage.Spin = parts[1] == "1";
                    if (parts.Length == 2 && parts[0] == "stage" && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var stage)) _stageScale = Mathf.Clamp(stage, 0.4f, 2.4f);
                    if (parts.Length >= 1 && parts[0] == "folded")
                    {
                        Folded.Clear();
                        foreach (var key in parts.Skip(1)) if (key.Length > 0) Folded.Add(key);
                    }
                    if (parts.Length != 5) continue;

                    var v = parts.Skip(1).Select(p => float.Parse(p, CultureInfo.InvariantCulture)).ToArray();
                    var rect = new Rect(v[0], v[1], v[2], v[3]);
                    if (parts[0] == "full") _full = rect;
                    else if (parts[0] == "compact") _compactRect = rect;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Scry could not read where the panel was: {ex.Message}");
            }
        }

        private static void SaveRects()
        {
            try
            {
                string Line(string name, Rect r) => name + " " + string.Join(" ", new[] { r.x, r.y, r.width, r.height }
                    .Select(v => Mathf.Round(v).ToString(CultureInfo.InvariantCulture)));

                Directory.CreateDirectory(Plugin.DataFolder);
                File.WriteAllLines(RectFile, new[] { Line("full", _full), Line("compact", _compactRect), "view " + (_compact ? "compact" : "full"),
                    "light " + Stage.LightingIndex, "backdrop " + Stage.BackdropIndex, "person " + (Stage.ShowPerson ? "1" : "0"), "worn " + (Looks.OnPerson ? "1" : "0"),
                    "spin " + (Stage.Spin ? "1" : "0"), "folded " + string.Join(" ", Folded),
                    "stage " + _stageScale.ToString("0.00", CultureInfo.InvariantCulture) });
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Scry could not remember the panel's place: {ex.Message}");
            }
        }

        // ----- Drawing -----

        private static void Draw(Explorer explorer)
        {
            var win = Win;
            Skin.Box(win, Skin.Backdrop, Skin.Outline);

            GUI.BeginGroup(win);
            var w = win.width;
            var h = win.height;
            var pad = U(16f);
            var e = Event.current;

            // Header: the name, the catalog size, the view switch and a close button. It drags the panel.
            var viewText = _compact ? "Full view" : "Compact";
            var viewW = Skin.Button.CalcSize(new GUIContent(viewText)).x + U(10f);
            // Clear sits in the header so it is in the same place whatever is selected.
            var outCount = Previews.OutCount;
            var clearText = outCount > 0 ? $"Clear  {outCount}" : "Clear";
            var clearW = Skin.Button.CalcSize(new GUIContent(clearText)).x + U(10f);
            var viewRect = new Rect(w - pad - U(40f) - viewW, U(15f), viewW, U(28f));
            var clearRect = new Rect(viewRect.x - U(8f) - clearW, viewRect.y, clearW, viewRect.height);

            var header = new Rect(0f, 0f, clearRect.x - U(8f), U(52f));
            GUI.Label(new Rect(pad, U(10f), U(90f), U(34f)), "Scry", Skin.Title);

            var enabled = GUI.enabled;
            GUI.enabled = outCount > 0;
            if (GUI.Button(clearRect, clearText, outCount > 0 ? Skin.Primary : Skin.Button))
            {
                Previews.ClearWorld();
                Session.Say("Cleared. Nothing from Scry is left in the world.");
            }
            GUI.enabled = enabled;
            if (clearRect.Contains(e.mousePosition))
            {
                AskTip("clear", outCount > 0
                    ? "Removes every preview from the world, pinned ones included, and stops what is playing"
                    : "Nothing from Scry is in the world");
            }

            if (GUI.Button(viewRect, viewText, Skin.Button)) ToggleCompact();
            if (GUI.Button(new Rect(w - pad - U(32f), U(12f), U(32f), U(32f)), "×", Skin.Close)) Session.Hide();
            if (e.type == EventType.MouseDown && e.button == 0 && header.Contains(e.mousePosition))
            {
                _drag = Drag.Move;
                e.Use();
            }

            // Search, favourites and origin, then the kind tabs, across the whole width.
            // The mouse's side buttons, as the panel's own events see them.
            if (Event.current.type == EventType.MouseDown && (Event.current.button == 3 || Event.current.button == 4))
            {
                Step(explorer, Event.current.button == 3);
                Event.current.Use();
            }

            var y = Controls(explorer, new Rect(pad, U(56f), w - pad * 2f, U(36f)));
            y = Tabs(explorer, new Rect(pad, y + U(10f), w - pad * 2f, U(30f)));

            var bodyTop = y + U(12f);
            var footerH = U(28f);
            var bodyBottom = h - pad - footerH;
            var bodyH = bodyBottom - bodyTop;

            if (_compact)
            {
                var listH = Mathf.Round(bodyH * 0.46f);
                List(explorer, new Rect(pad, bodyTop, w - pad * 2f, listH));
                Side(explorer, new Rect(pad, bodyTop + listH + U(12f), w - pad * 2f, bodyH - listH - U(12f)), withStage: false);
            }
            else
            {
                var leftW = Mathf.Round((w - pad * 3f) * 0.40f);
                var rightX = pad * 2f + leftW;
                List(explorer, new Rect(pad, bodyTop, leftW, bodyH));
                Side(explorer, new Rect(rightX, bodyTop, w - rightX - pad, bodyH), withStage: true);
            }

            Footer(new Rect(pad, h - pad - footerH + U(6f), w - pad * 2f, footerH));

            // Resize grip in the corner.
            var grip = new Rect(w - U(22f), h - U(22f), U(20f), U(20f));
            for (var i = 0; i < 3; i++)
            {
                var d = U(4f) * i;
                Skin.Fill(new Rect(grip.xMax - U(6f) - d, grip.yMax - U(6f), U(2f), U(2f)), Skin.Faint);
                Skin.Fill(new Rect(grip.xMax - U(6f), grip.yMax - U(6f) - d, U(2f), U(2f)), Skin.Faint);
            }
            if (e.type == EventType.MouseDown && e.button == 0 && grip.Contains(e.mousePosition))
            {
                _drag = Drag.Resize;
                e.Use();
            }

            GUI.EndGroup();
        }

        /// <summary>The search box, the favourites star and the origin switch, on one row.</summary>
        private static float Controls(Explorer explorer, Rect rect)
        {
            var names = new[] { "All", "Game", "Mods" };
            var widths = names.Select(n => Skin.Segment.CalcSize(new GUIContent(n)).x + U(6f)).ToArray();
            var originW = widths.Sum() + U(4f) * (names.Length - 1);
            var starW = rect.height;
            var gap = U(8f);
            var recentText = "Recent";
            var recentW = Skin.Segment.CalcSize(new GUIContent(recentText)).x + U(6f);

            // In the compact view the search has the whole first row and the buttons go below it.
            var row = rect.y;
            var navW = starW * 2f + U(4f) + gap;
            Rect search;
            if (_compact)
            {
                search = new Rect(rect.x + navW, rect.y, rect.width - navW, rect.height);
                row = rect.yMax + U(8f);
            }
            else
            {
                search = new Rect(rect.x + navW, rect.y, rect.width - navW - originW - starW * 2f - recentW - gap * 4f, rect.height);
            }
            BackAndForward(explorer, new Rect(rect.x, rect.y, starW, rect.height), new Rect(rect.x + starW + U(4f), rect.y, starW, rect.height));
            Search(explorer, search);
            var startX = _compact ? rect.x - gap : search.xMax;

            // Help for the search terms.
            var help = new Rect(startX + gap, row, starW, rect.height);
            if (GUI.Button(help, "?", _help ? Skin.On : Skin.IconButton)) _help = !_help;
            if (help.Contains(Event.current.mousePosition)) AskTip("help", "How to search");

            var star = new Rect(help.xMax + gap, row, starW, rect.height);
            if (GUI.Button(star, GUIContent.none, explorer.FavouritesOnly ? Skin.On : Skin.IconButton))
            {
                explorer.FavouritesOnly = !explorer.FavouritesOnly;
                _listScroll = Vector2.zero;
                _help = false;
            }
            var icon = new Rect(star.x + star.width * 0.22f, star.y + star.height * 0.22f, star.width * 0.56f, star.height * 0.56f);
            Skin.Icon(icon, explorer.FavouritesOnly ? Skin.Star : Skin.StarHollow, explorer.FavouritesOnly ? Skin.Accent : Skin.Dim);
            if (star.Contains(Event.current.mousePosition)) AskTip("fav", explorer.FavouritesOnly ? "Showing only favourites" : "Show only favourites");

            var recent = new Rect(star.xMax + gap, row, recentW, rect.height);
            if (GUI.Button(recent, recentText, explorer.RecentOnly ? Skin.SegmentOn : Skin.Segment))
            {
                explorer.RecentOnly = !explorer.RecentOnly;
                _listScroll = Vector2.zero;
                _help = false;
            }
            if (recent.Contains(Event.current.mousePosition)) AskTip("recent", "What you looked at last, newest first");

            var originX = recent.xMax + gap;
            var originRow = row;
            if (originX + originW > rect.xMax)
            {
                originX = rect.x;
                originRow = row + rect.height + U(6f);
            }

            var x = originX;
            for (var i = 0; i < names.Length; i++)
            {
                var on = (int)explorer.Origin == i;
                if (GUI.Button(new Rect(x, originRow, widths[i], rect.height), names[i], on ? Skin.SegmentOn : Skin.Segment))
                {
                    explorer.Origin = (OriginFilter)i;
                    _listScroll = Vector2.zero;
                    _help = false;
                }
                x += widths[i] + U(4f);
            }
            var originRect = new Rect(originX, originRow, originW, rect.height);
            if (originRect.Contains(Event.current.mousePosition)) AskTip("origin", "Everything, only the game's own, or only what mods added");

            return originRow + rect.height;
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

        private static void Search(Explorer explorer, Rect rect)
        {
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
                hasText = !string.IsNullOrEmpty(text);
            }

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

            if (_focusSearch && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(SearchControl);
                _focusSearch = false;
            }
        }

        /// <summary>One tab per kind with what the search holds of it, wrapping when the row is full.</summary>
        private static float Tabs(Explorer explorer, Rect rect)
        {
            var x = rect.x;
            var y = rect.y;
            var tabH = rect.height;
            var gap = U(4f);

            void Tab(string label, int count, Color dot, bool on, Action act)
            {
                var style = on ? Skin.TabOn : Skin.Tab;
                var countColor = on ? "5a4526" : "8f929c";
                var text = $"{label}  <color=#{countColor}>{count:N0}</color>";
                var width = style.CalcSize(new GUIContent(text)).x + U(2f);
                if (x + width > rect.xMax && x > rect.x)
                {
                    x = rect.x;
                    y += tabH + gap;
                }
                var tab = new Rect(x, y, width, tabH);
                if (GUI.Button(tab, text, style)) act();
                var size = U(8f);
                Skin.Icon(new Rect(tab.x + U(10f), tab.y + (tabH - size) / 2f, size, size), Skin.Circle, on ? Skin.OnAccent : dot);
                x += width + gap;
            }

            Tab("All", explorer.CountAll, Skin.Text, explorer.KindFilter == null, () => Filter(explorer, null));
            foreach (Kind kind in Enum.GetValues(typeof(Kind)))
            {
                var count = explorer.CountOf(kind);
                if (count == 0 && explorer.KindFilter != kind) continue;
                var k = kind;
                Tab(Kinds.Label(kind), count, Skin.KindColor(kind), explorer.KindFilter == kind, () => Filter(explorer, k));
            }

            return y + tabH;
        }

        private static void Filter(Explorer explorer, Kind? kind)
        {
            explorer.KindFilter = kind;
            _listScroll = Vector2.zero;
            _help = false;
            _reveal = true;
        }

        private static void Footer(Rect rect)
        {
            var note = Session.Note;
            var text = note ?? (_compact
                ? "Walk with your keys when not typing. Hold right mouse outside the panel to look around."
                : "Arrows move, Enter plays or shows, Ctrl+F searches. Click away from the search to walk, hold right mouse outside the panel to look.");
            // The catalog's size sits at the far right, clear of the resize grip.
            var summary = Session.CatalogSummary;
            var summaryW = _compact ? 0f : Skin.FaintLabel.CalcSize(new GUIContent(summary)).x;
            if (summaryW > 0f)
            {
                GUI.Label(new Rect(rect.xMax - U(26f) - summaryW, rect.y, summaryW, rect.height), summary, Skin.FaintLabel);
            }
            Ticker(new Rect(rect.x, rect.y, rect.width - U(40f) - summaryW, rect.height), text, note != null ? Skin.DimLabel : Skin.FaintLabel);
        }

        /// <summary>
        /// One line of text that does not wrap. When it is longer than its room it slides slowly
        /// to its end and back, resting a moment at each end, so all of it can be read.
        /// </summary>
        private static void Ticker(Rect rect, string text, GUIStyle style)
        {
            var width = style.CalcSize(new GUIContent(text)).x;
            var overflow = width - rect.width;
            if (overflow <= 0f)
            {
                GUI.Label(rect, text, style);
                return;
            }

            const float rest = 1.6f;
            var slide = overflow / Mathf.Max(1f, U(40f));
            var cycle = 2f * (rest + slide);
            var phase = Time.unscaledTime % cycle;
            float along;
            if (phase < rest) along = 0f;
            else if (phase < rest + slide) along = (phase - rest) / slide;
            else if (phase < rest * 2f + slide) along = 1f;
            else along = 1f - (phase - rest * 2f - slide) / slide;

            GUI.BeginGroup(rect);
            GUI.Label(new Rect(-overflow * along, 0f, width + U(4f), rect.height), text, style);
            GUI.EndGroup();
        }

        // ----- The list -----

        private static readonly string[][] HelpLines =
        {
            new[] { "troll", "Names containing it, in the game's words or the prefab's. Best matches first." },
            new[] { "troll hat", "Every word has to match." },
            new[] { "-ragdoll", "A minus leaves out whatever matches." },
            new[] { "kind:creature", "Only one kind: creature, item, piece, projectile, effect, sound, se (status effect), other." },
            new[] { "has:aoe", "Prefabs with a part of that type, such as has:light, has:pickable, has:fireplace." },
            new[] { "biome:swamp", "What spawns or grows in that biome." },
            new[] { "mod:epic", "What a mod added, by the start or any part of its name." },
            new[] { "used:troll", "The sounds and effects a prefab plays." },
            new[] { "station:forge3", "What is made at that station, here what a forge at level 3 can make. station:forge for any level, station:hand for what needs none." },
            new[] { "-has:ragdoll kind:c", "Terms combine, can be left out with a minus, and can be shortened." },
        };

        /// <summary>How to search, shown in place of the list while the ? button is on.</summary>
        private static Vector2 _helpScroll;
        private static float _helpHeight;

        private static void HelpCard(Rect rect)
        {
            Skin.Box(rect, Skin.Panel);
            var closeH = U(30f);
            var area = new Rect(rect.x + U(4f), rect.y + U(6f), rect.width - U(8f), rect.height - closeH - U(20f));
            var view = new Rect(0f, 0f, area.width - U(14f), Mathf.Max(_helpHeight, area.height));
            _helpScroll = GUI.BeginScrollView(area, _helpScroll, view, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);

            var x = U(14f);
            var width = view.width - U(24f);
            var y = U(10f);

            GUI.Label(new Rect(x, y, width, U(26f)), "How to search", Skin.Big);
            y += U(34f);

            // Side by side when there is room, the example above its meaning when not.
            var stacked = width < U(420f);
            var keyW = stacked ? width : Mathf.Min(U(190f), width * 0.42f);
            foreach (var line in HelpLines)
            {
                var keyText = new GUIContent(line[0]);
                var boxW = Mathf.Min(keyW, Skin.Label.CalcSize(keyText).x + U(16f));
                Skin.Box(new Rect(x - U(4f), y - U(1f), boxW, U(24f)), Skin.Raised);
                GUI.Label(new Rect(x + U(4f), y, boxW - U(8f), U(22f)), line[0], Skin.Label);

                var textX = stacked ? x : x + keyW + U(10f);
                var textY = stacked ? y + U(28f) : y + U(2f);
                var textW = stacked ? width : width - keyW - U(10f);
                var height = Skin.DimWrap.CalcHeight(new GUIContent(line[1]), textW);
                GUI.Label(new Rect(textX, textY, textW, height), line[1], Skin.DimWrap);
                y = Mathf.Max(y + U(24f), textY + height) + U(12f);
            }

            const string more = "The star shows only favourites, Recent what you looked at last. The kind tabs, Game or Mods, and all of the above work together.";
            var moreH = Skin.DimWrap.CalcHeight(new GUIContent(more), width);
            GUI.Label(new Rect(x, y, width, moreH), more, Skin.DimWrap);
            if (Event.current.type == EventType.Repaint) _helpHeight = y + moreH + U(12f);

            GUI.EndScrollView();

            if (GUI.Button(new Rect(rect.xMax - U(96f), rect.yMax - closeH - U(10f), U(80f), closeH), "Close", Skin.Button)) _help = false;
        }

        private static void List(Explorer explorer, Rect rect)
        {
            if (_help)
            {
                HelpCard(rect);
                return;
            }

            Skin.Box(rect, Skin.Panel);
            var inner = new Rect(rect.x + U(4f), rect.y + U(6f), rect.width - U(8f), rect.height - U(12f));
            var results = explorer.Results;
            var rowH = U(34f);
            _rowsInView = Mathf.Max(1, Mathf.FloorToInt(inner.height / rowH));

            if (results.Count == 0)
            {
                var message = explorer.FavouritesOnly && explorer.Favourites.Keys.Count == 0
                    ? "No favourites yet. Star something to keep it here."
                    : "Nothing matches.";
                GUI.Label(inner, message, Skin.CenterDim);
                return;
            }

            if (_reveal && Event.current.type == EventType.Layout)
            {
                _reveal = false;
                var index = explorer.SelectedIndex;
                if (index >= 0)
                {
                    var top = index * rowH;
                    if (top < _listScroll.y) _listScroll.y = top;
                    else if (top + rowH > _listScroll.y + inner.height) _listScroll.y = top + rowH - inner.height;
                }
            }

            var view = new Rect(0f, 0f, inner.width - U(14f), results.Count * rowH);
            _listScroll = GUI.BeginScrollView(inner, _listScroll, view, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);

            var first = Mathf.Max(0, Mathf.FloorToInt(_listScroll.y / rowH));
            var last = Mathf.Min(results.Count - 1, first + _rowsInView + 1);
            var visible = new Rect(0f, _listScroll.y, view.width, inner.height);
            for (var i = first; i <= last; i++)
            {
                Row(explorer, results[i], new Rect(0f, i * rowH, view.width, rowH), i == explorer.SelectedIndex, visible);
            }

            GUI.EndScrollView();
        }

        private static void Row(Explorer explorer, Entry entry, Rect rect, bool selected, Rect visible)
        {
            var e = Event.current;
            var hover = rect.Contains(e.mousePosition) && visible.Contains(e.mousePosition) && _drag == Drag.None;
            var inner = new Rect(rect.x + U(2f), rect.y + U(1f), rect.width - U(4f), rect.height - U(2f));

            if (selected) Skin.Box(inner, Skin.AccentSoft);
            else if (hover) Skin.Box(inner, Skin.Hover);
            if (selected) Skin.Fill(new Rect(inner.x, inner.y + U(8f), U(3f), inner.height - U(16f)), Skin.Accent);

            var icon = new Rect(inner.x + U(10f), inner.y + (inner.height - U(24f)) / 2f, U(24f), U(24f));
            DrawIcon(entry, icon);

            var favourite = explorer.Favourites.Contains(entry);
            var star = new Rect(inner.xMax - U(28f), inner.y + (inner.height - U(18f)) / 2f, U(18f), U(18f));
            if (favourite) Skin.Icon(star, Skin.Star, Skin.Accent);
            else if (hover) Skin.Icon(star, Skin.StarHollow, star.Contains(e.mousePosition) ? Skin.Accent : Skin.Faint);

            var textX = icon.xMax + U(10f);
            var textW = star.x - U(8f) - textX;
            var primary = string.IsNullOrEmpty(entry.DisplayName) ? entry.Name : entry.DisplayName;
            var secondary = string.IsNullOrEmpty(entry.DisplayName) || entry.DisplayName == entry.Name ? "" : entry.Name;

            var nameStyle = Skin.RowName;
            var was = nameStyle.normal.textColor;
            if (entry.Empty) nameStyle.normal.textColor = Skin.Faint;
            var fullW = nameStyle.CalcSize(new GUIContent(primary)).x;
            var nameW = Mathf.Min(textW, fullW);
            GUI.Label(new Rect(textX, inner.y, nameW, inner.height), primary, nameStyle);
            nameStyle.normal.textColor = was;

            var cut = fullW > textW;
            if (secondary.Length > 0)
            {
                var room = textW - nameW - U(8f);
                if (room > U(40f))
                {
                    GUI.Label(new Rect(textX + nameW + U(8f), inner.y + U(1f), room, inner.height), secondary, Skin.RowSub);
                    cut |= Skin.RowSub.CalcSize(new GUIContent(secondary)).x > room;
                }
                else
                {
                    cut = true;
                }
            }

            if (hover && cut && !star.Contains(e.mousePosition))
            {
                AskTip(entry.Key, secondary.Length > 0 ? primary + "\n" + secondary : primary);
            }

            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition) && visible.Contains(e.mousePosition))
            {
                if (star.Contains(e.mousePosition))
                {
                    explorer.ToggleFavourite(entry);
                }
                else
                {
                    explorer.Select(entry);
                    if (e.clickCount == 2) Primary(entry);
                }
                e.Use();
            }
        }

        private static void DrawIcon(Entry entry, Rect rect)
        {
            if (entry.Icon is Sprite sprite && sprite != null && sprite.texture != null && Event.current.type == EventType.Repaint)
            {
                try
                {
                    var t = sprite.texture;
                    var r = sprite.textureRect;
                    var uv = new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height);
                    var aspect = r.width / Mathf.Max(1f, r.height);
                    var fit = aspect >= 1f
                        ? new Rect(rect.x, rect.y + (rect.height - rect.height / aspect) / 2f, rect.width, rect.height / aspect)
                        : new Rect(rect.x + (rect.width - rect.width * aspect) / 2f, rect.y, rect.width * aspect, rect.height);
                    GUI.DrawTextureWithTexCoords(fit, t, uv, true);
                    return;
                }
                catch
                {
                    // A sprite packed in a way that has no simple rectangle; the mark below stands in.
                }
            }

            var color = Skin.KindColor(entry.Kind);
            Skin.Box(rect, new Color(color.r, color.g, color.b, 0.20f));
            var style = Skin.Glyph;
            var was = style.normal.textColor;
            style.normal.textColor = color;
            GUI.Label(rect, Skin.KindMark(entry.Kind), style);
            style.normal.textColor = was;
        }

        // ----- The selection -----

        private static void Side(Explorer explorer, Rect rect, bool withStage)
        {
            var entry = explorer.Selected;
            if (entry == null)
            {
                Skin.Box(rect, Skin.Panel);
                var middle = new Rect(rect.x + U(30f), rect.y + rect.height / 2f - U(40f), rect.width - U(60f), U(80f));
                GUI.Label(new Rect(middle.x, middle.y, middle.width, U(30f)), "Pick something from the list", Skin.Center);
                GUI.Label(new Rect(middle.x, middle.y + U(32f), middle.width, U(44f)),
                    "Type to search, use the arrow keys to move, and Enter to play or show it.", Skin.CenterDim);
                return;
            }

            var top = rect.y;
            if (withStage)
            {
                _stageBaseH = Mathf.Round(Mathf.Min(rect.width * 0.60f, rect.height * 0.50f));
                var stageH = Mathf.Round(Mathf.Clamp(_stageBaseH * _stageScale, U(120f), rect.height * 0.85f));
                StageArea(entry, new Rect(rect.x, rect.y, rect.width, stageH));
                StageHandle(new Rect(rect.x, rect.y + stageH, rect.width, U(12f)));
                top += stageH + U(12f);
            }

            var below = new Rect(rect.x, top, rect.width, rect.yMax - top);
            var content = new Rect(0f, 0f, below.width - U(14f), Mathf.Max(_sideHeight, below.height));
            _sideScroll = GUI.BeginScrollView(below, _sideScroll, content, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);

            var cw = content.width;
            var y = 0f;
            _foldAllShown = false;
            y = Title(explorer, entry, cw, y);
            if (!withStage && (entry.Kind == Kind.Sound || entry.Kind == Kind.StatusEffect)) y = CompactCard(entry, cw, y);
            y = Actions(entry, cw, y);
            if (Looks.IsWorn(entry)) y = Wearing(explorer, cw, y);
            if (entry.Kind == Kind.Sound)
            {
                y = Timeline(cw, y);
                y = Variants(entry, cw, y);
            }
            y = Adjust(explorer, entry, cw, y, withStage);
            y = Effects(explorer, entry, cw, y, withStage);
            y = PlaysInSection(explorer, entry, cw, y);
            y = LinksSection(explorer, entry, cw, y);
            y = FactsSection(explorer, entry, cw, y);
            y = Command(explorer, entry, cw, y);
            y = Details(explorer, entry, cw, y);
            if (Event.current.type == EventType.Repaint) _sideHeight = y + U(8f);

            GUI.EndScrollView();
        }

        /// <summary>
        /// The strip under the stage: dragged, it makes the stage taller or shorter; double-clicked,
        /// it puts the usual height back.
        /// </summary>
        private static void StageHandle(Rect strip)
        {
            var e = Event.current;
            var hover = strip.Contains(e.mousePosition) || _drag == Drag.StageSize;
            var bar = new Rect(strip.center.x - U(24f), strip.y + U(4f), U(48f), U(4f));
            Skin.Fill(bar, hover ? Skin.Dim : Skin.Outline);
            if (strip.Contains(e.mousePosition)) AskTip("stage-size", "Drag to make the stage taller or shorter, double-click for its usual height");

            if (e.type == EventType.MouseDown && e.button == 0 && strip.Contains(e.mousePosition))
            {
                if (e.clickCount == 2)
                {
                    _stageScale = 1f;
                    SaveRects();
                }
                else
                {
                    _drag = Drag.StageSize;
                }
                e.Use();
            }
        }

        private static void StageArea(Entry entry, Rect rect)
        {
            var e = Event.current;
            Skin.Box(rect, Skin.Stage);

            if (Stage.IsStaged(entry))
            {
                var inner = new Rect(rect.x + U(3f), rect.y + U(3f), rect.width - U(6f), rect.height - U(6f));
                if (e.type == EventType.Repaint) Stage.Request((int)inner.width, (int)inner.height);

                if (Stage.Subject != null && Stage.Texture != null)
                {
                    if (e.type == EventType.Repaint) GUI.DrawTexture(inner, Stage.Texture, ScaleMode.StretchToFill, false);
                }
                else if (entry.Kind != Kind.Effect)
                {
                    GUI.Label(inner, "This one could not be previewed.", Skin.CenterDim);
                }

                var viewsW = ViewButtons(inner);
                var textW = inner.width - U(24f) - viewsW;
                if (rect.Contains(e.mousePosition) || _drag == Drag.Orbit)
                {
                    FitLabel(new Rect(inner.x + U(12f), inner.yMax - U(28f), textW, U(22f)),
                        "Drag to turn, right-drag to move, scroll to zoom, double-click to reset", Skin.FaintLabel, 9f);
                }
                else if (Stage.Subject != null && Stage.ShowsGrid)
                {
                    // On the grid, how big the model is, in the same metres as its squares.
                    var size = Stage.SubjectSize;
                    string M(float v) => v.ToString(v < 10f ? "0.0" : "0", CultureInfo.InvariantCulture);
                    FitLabel(new Rect(inner.x + U(12f), inner.yMax - U(28f), textW, U(22f)),
                        $"Squares of 1 m, lines every 5 m  \u00B7  {M(size.y)} m tall, {M(size.x)} × {M(size.z)} m", Skin.DimLabel, 9f);
                }

                // The stage's own buttons come before its dragging, which would otherwise take their clicks.
                StageButtons(entry, rect);

                if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                {
                    if (e.clickCount == 2) Stage.ResetView();
                    _drag = Drag.Orbit;
                    Stage.Dragging = true;
                    e.Use();
                }
                else if (e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition))
                {
                    _drag = Drag.Pan;
                    e.Use();
                }
                else if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
                {
                    Stage.ZoomBy(e.delta.y);
                    e.Use();
                }
            }
            else if (entry.Kind == Kind.Sound)
            {
                SoundCard(entry, rect);
            }
            else if (entry.Kind == Kind.StatusEffect)
            {
                StatusCard(entry, rect);
            }
            else
            {
                var middle = new Rect(rect.x + U(30f), rect.y + rect.height / 2f - U(34f), rect.width - U(60f), U(70f));
                GUI.Label(new Rect(middle.x, middle.y, middle.width, U(28f)), "Nothing to see or hear", Skin.Center);
                GUI.Label(new Rect(middle.x, middle.y + U(30f), middle.width, U(40f)),
                    "It has no model, particles, light or sound. Often a spawner or a controller.", Skin.CenterDim);
            }

            KindBadge(entry, new Vector2(rect.x + U(10f), rect.y + U(10f)));
        }

        /// <summary>The kind, in its colour, as a small pill.</summary>
        private static float KindBadge(Entry entry, Vector2 at)
        {
            var color = Skin.KindColor(entry.Kind);
            var label = entry.Kind == Kind.StatusEffect ? "Status effect" : Kinds.Label(entry.Kind).TrimEnd('s');
            var width = Skin.Glyph.CalcSize(new GUIContent(label)).x + U(20f);
            var badge = new Rect(at.x, at.y, width, U(22f));
            _badgeWidth = width;
            Skin.PillBox(badge, new Color(color.r * 0.28f, color.g * 0.28f, color.b * 0.28f, 0.95f));
            var style = Skin.Glyph;
            var was = style.normal.textColor;
            style.normal.textColor = Color.Lerp(color, Color.white, 0.25f);
            GUI.Label(badge, label, style);
            style.normal.textColor = was;
            return badge.xMax;
        }

        /// <summary>In compact view, the facts the stage card would show, as lines of text.</summary>
        private static float CompactCard(Entry entry, float width, float y)
        {
            string text;
            if (entry.Kind == Kind.Sound)
            {
                text = SoundFacts(entry);
            }
            else
            {
                var effect = entry.Source as StatusEffect;
                if (effect == null) return y;
                var tooltip = CatalogBuilder.Localize(effect.m_tooltip);
                text = StatusFacts(effect) + (tooltip.Length > 0 ? "\n" + tooltip : "");
            }

            var height = Skin.DimWrap.CalcHeight(new GUIContent(text), width);
            GUI.Label(new Rect(0f, y, width, height), text, Skin.DimWrap);
            return y + height + U(12f);
        }

        private static readonly Dictionary<Entry, string> SoundFactCache = new Dictionary<Entry, string>();

        private static string SoundFacts(Entry entry)
        {
            if (!SoundFactCache.TryGetValue(entry, out var facts))
            {
                facts = DescribeSound(entry.Source as GameObject);
                SoundFactCache[entry] = facts;
            }
            return facts;
        }

        private static void SoundCard(Entry entry, Rect rect)
        {
            // Bars that move while the sound plays.
            var bars = 24;
            var barW = U(6f);
            var gap = U(5f);
            var total = bars * barW + (bars - 1) * gap;
            var x = rect.x + (rect.width - total) / 2f;
            var mid = rect.y + rect.height * 0.42f;
            var playing = Previews.SoundPlaying && !Previews.SoundPaused;
            var accent = Skin.KindColor(Kind.Sound);

            for (var i = 0; i < bars; i++)
            {
                var shape = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(i * 0.9f + 0.6f)) * Mathf.Sin((i + 1f) / (bars + 1f) * Mathf.PI);
                var motion = playing ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 9f + i * 0.7f) : 0.35f;
                var height = U(90f) * shape * motion + U(6f);
                var bar = new Rect(x + i * (barW + gap), mid - height / 2f, barW, height);
                Skin.PillBox(bar, playing ? accent : new Color(accent.r, accent.g, accent.b, 0.35f));
            }

            GUI.Label(new Rect(rect.x + U(20f), mid + U(64f), rect.width - U(40f), U(26f)), SoundFacts(entry), Skin.CenterDim);
        }

        private static string DescribeSound(GameObject prefab)
        {
            if (prefab == null) return "";

            var clips = new List<AudioClip>();
            foreach (var sfx in prefab.GetComponentsInChildren<ZSFX>(true))
            {
                if (sfx.m_audioClips != null) clips.AddRange(sfx.m_audioClips.Where(c => c != null));
            }
            foreach (var source in prefab.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.clip != null && !clips.Contains(source.clip)) clips.Add(source.clip);
            }

            var loops = prefab.GetComponentsInChildren<AudioSource>(true).Any(s => s.loop);
            if (clips.Count == 0) return loops ? "Loops" : "No clips found";

            var longest = clips.Max(c => c.length);
            var what = clips.Count == 1 ? "1 clip" : $"{clips.Count} clips, one picked at random";
            return $"{what}, {longest.ToString("0.0", CultureInfo.InvariantCulture)} s{(loops ? ", loops" : "")}";
        }

        private static readonly Dictionary<Entry, List<KeyValuePair<string, EffectList>>> StatusListCache =
            new Dictionary<Entry, List<KeyValuePair<string, EffectList>>>();

        private static List<KeyValuePair<string, EffectList>> StatusLists(Entry entry)
        {
            if (!StatusListCache.TryGetValue(entry, out var lists))
            {
                lists = Previews.StatusLists(entry.Source as StatusEffect);
                StatusListCache[entry] = lists;
            }
            return lists;
        }

        private static string StatusFacts(StatusEffect effect)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(effect.m_category)) parts.Add("Category: " + effect.m_category);
            parts.Add(effect.m_ttl > 0f ? "Lasts " + Duration(effect.m_ttl) : "No time limit of its own");
            return string.Join("    ", parts);
        }

        private static void StatusCard(Entry entry, Rect rect)
        {
            var effect = entry.Source as StatusEffect;
            var iconSize = U(84f);
            var icon = new Rect(rect.x + (rect.width - iconSize) / 2f, rect.y + U(34f), iconSize, iconSize);
            if (effect != null && effect.m_icon != null) DrawIcon(entry, icon);
            else DrawIcon(new Entry { Kind = Kind.StatusEffect }, icon);

            if (effect == null) return;
            var y = icon.yMax + U(12f);
            GUI.Label(new Rect(rect.x + U(20f), y, rect.width - U(40f), U(22f)), StatusFacts(effect), Skin.CenterDim);

            var tooltip = CatalogBuilder.Localize(effect.m_tooltip);
            if (tooltip.Length > 0)
            {
                GUI.Label(new Rect(rect.x + U(30f), y + U(28f), rect.width - U(60f), rect.yMax - y - U(34f)), tooltip, Skin.CenterDim);
            }
        }

        private static string Duration(float seconds)
        {
            if (seconds >= 120f) return $"{Mathf.RoundToInt(seconds / 60f)} min";
            return $"{Mathf.RoundToInt(seconds)} s";
        }

        private static float Title(Explorer explorer, Entry entry, float width, float y)
        {
            var primary = string.IsNullOrEmpty(entry.DisplayName) ? entry.Name : entry.DisplayName;
            var copyW = U(104f);
            var nameRect = new Rect(0f, y, width - copyW - U(46f), U(30f));
            var fits = FitLabel(nameRect, primary, Skin.Big, 13f);
            if (!fits && nameRect.Contains(Event.current.mousePosition)) AskTip("title", primary);

            var favourite = explorer.Favourites.Contains(entry);
            var starRect = new Rect(width - U(28f), y + U(4f), U(22f), U(22f));
            Skin.Icon(starRect, favourite ? Skin.Star : Skin.StarHollow, favourite ? Skin.Accent : Skin.Dim);
            if (GUI.Button(starRect, GUIContent.none, GUIStyle.none)) explorer.ToggleFavourite(entry);
            if (starRect.Contains(Event.current.mousePosition)) AskTip("star", favourite ? "Remove from favourites" : "Add to favourites");

            if (GUI.Button(new Rect(width - copyW - U(38f), y + U(2f), copyW, U(26f)), "Copy name", Skin.Button))
            {
                GUIUtility.systemCopyBuffer = entry.Name;
                Session.Say($"Copied \"{entry.Name}\".");
            }
            y += U(34f);

            // Kind in colour, then the prefab name (when the game shows another) and where it comes from.
            var x = _compact ? KindBadge(entry, new Vector2(0f, y)) + U(10f) : 0f;
            var origin = OriginText(entry);
            var sub = entry.Name == primary ? origin : entry.Name + (origin.Length > 0 ? "   ·   " + origin : "");
            if (sub.Length > 0 || _compact)
            {
                var subRect = new Rect(x, y, width - x, U(22f));
                if (!FitLabel(subRect, sub, Skin.DimLabel, 10f) && subRect.Contains(Event.current.mousePosition)) AskTip("sub", sub);
                if (entry.Origin == Origin.Mod && entry.ModName.Length > 0)
                {
                    if (subRect.Contains(Event.current.mousePosition)) AskTip("mod", "Show everything " + entry.ModName + " added");
                    if (GUI.Button(subRect, GUIContent.none, GUIStyle.none)) SearchFor(explorer, "mod:" + entry.ModName.Split(' ')[0].ToLowerInvariant());
                }
                y += U(26f);
            }

            return y + U(8f);
        }

        /// <summary>
        /// Draws a label, shrinking its text step by step until it fits, down to a smallest size.
        /// Returns false when even that was too wide and the text is cut.
        /// </summary>
        private static bool FitLabel(Rect rect, string text, GUIStyle style, float smallest)
        {
            var original = style.fontSize;
            var content = new GUIContent(text);
            var floor = Mathf.Max(1, Mathf.RoundToInt(smallest * _s));
            var fits = style.CalcSize(content).x <= rect.width;
            while (!fits && style.fontSize > floor)
            {
                style.fontSize -= 1;
                fits = style.CalcSize(content).x <= rect.width;
            }
            GUI.Label(rect, text, style);
            style.fontSize = original;
            return fits;
        }

        // ----- Actions -----

        private static float Actions(Entry entry, float width, float y)
        {
            var x = 0f;
            var rowH = U(32f);
            var any = false;

            bool Button(string text, GUIStyle style)
            {
                any = true;
                var w = style.CalcSize(new GUIContent(text)).x + U(12f);
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(6f);
                }
                var clicked = GUI.Button(new Rect(x, y, w, rowH), text, style);
                x += w + U(8f);
                return clicked;
            }

            string note = null;
            switch (entry.Kind)
            {
                case Kind.Sound:
                    if (Button(Variants(entry).Count > 1 ? "Play a random one" : "Play", Skin.Primary)) Previews.PlaySound(entry);
                    if (Previews.SoundPlaying)
                    {
                        if (Button(Previews.SoundPaused ? "Resume" : "Pause", Skin.Button)) Previews.PauseSound(!Previews.SoundPaused);
                        if (Button("Stop", Skin.Button)) Previews.StopSound();
                    }
                    if (Button(Previews.LoopSounds ? "Repeat on" : "Repeat off", Previews.LoopSounds ? Skin.On : Skin.Button)) Previews.LoopSounds = !Previews.LoopSounds;
                    break;

                case Kind.Effect:
                    var there = Previews.Playing.IsPlaying("there:" + entry.Name);
                    if (Button("Play where you look", there ? Skin.On : Skin.Primary))
                    {
                        if (there) Previews.Stop("there:" + entry.Name);
                        else Previews.PlayEffect(entry, onYou: false);
                    }
                    var onYou = Previews.Playing.IsPlaying("on you:" + entry.Name);
                    if (Button("Play on you", onYou ? Skin.On : Skin.Button))
                    {
                        if (onYou) Previews.Stop("on you:" + entry.Name);
                        else Previews.PlayEffect(entry, onYou: true);
                    }
                    if (!_compact)
                    {
                        if (Button("Replay", Skin.Button)) Previews.Replay();
                        if (Button(Previews.LoopEffects ? "Repeat on" : "Repeat off", Previews.LoopEffects ? Skin.On : Skin.Button)) Previews.LoopEffects = !Previews.LoopEffects;
                    }
                    break;

                case Kind.StatusEffect:
                    note = StatusActions(entry, Button);
                    break;

                default:
                    if (entry.Kind == Kind.Projectile && Button("Fire where you look", Skin.Primary)) Previews.Fire(entry);
                    if (Looks.IsWorn(entry))
                    {
                        var kept = Looks.Outfit.Contains(entry.Name);
                        if (Button(kept ? "Kept on" : "Keep it on", kept ? Skin.On : Skin.Button))
                        {
                            if (kept) Looks.Outfit.TakeOff(entry.Name);
                            else Looks.Outfit.Keep(entry.Name, Gear.SlotOf((GameObject)entry.Source));
                            Previews.Rebuild();
                        }
                    }
                    if (_compact && entry.Kind == Kind.Item && entry.Source is GameObject wearable && Gear.IsWearable(wearable)
                        && Button(Looks.OnPerson ? "Worn by a person" : "Wear it", Looks.OnPerson ? Skin.On : Skin.Button))
                    {
                        Looks.OnPerson = !Looks.OnPerson;
                        Previews.Rebuild();
                        SaveRects();
                    }
                    var fallen = Previews.Playing.IsPlaying("ragdoll");
                    if (Previews.RagdollOf(entry) != null && Stage.Subject != null && Button("Ragdoll", fallen ? Skin.On : Skin.Button))
                    {
                        if (fallen) Previews.Stop("ragdoll");
                        else Previews.Ragdoll();
                    }
                    if (Previews.IsModel(entry))
                    {
                        if (Button(Previews.InWorld ? "Showing in the world" : "Show in the world", Previews.InWorld ? Skin.On : (entry.Kind == Kind.Projectile ? Skin.Button : Skin.Primary))) Previews.ToggleWorld();
                        if (Previews.InWorld)
                        {
                            if (Button("Move to where you look", Skin.Button)) Previews.PlaceHere();
                            if (Button("Pin", Skin.Button))
                            {
                                Previews.Pin();
                                Session.Say("Pinned. It stays where it is until you press Clear.");
                            }
                        }
                    }
                    break;
            }


            if (any) y += rowH;

            if (note == null && (Previews.InWorld || Previews.PinnedCount > 0))
            {
                note = "Only you see it, and it is gone when you leave the world. Hold the right mouse button outside the panel to look around, or close the panel with F7: previews stay until you clear them.";
            }
            if (note != null)
            {
                var height = Skin.DimWrap.CalcHeight(new GUIContent(note), width);
                GUI.Label(new Rect(0f, y + U(8f), width, height), note, Skin.DimWrap);
                y += height + U(8f);
            }

            return y + U(14f);
        }

        /// <summary>
        /// A status effect's buttons: show its start visuals on you and take them off again, and
        /// play any other list it has once. Returns the note to show under them.
        /// </summary>
        private static string StatusActions(Entry entry, Func<string, GUIStyle, bool> button)
        {
            var effect = entry.Source as StatusEffect;
            var lists = StatusLists(entry);
            if (effect == null || lists.Count == 0) return "It has no visuals or sounds of its own.";

            var hasStart = lists.Any(l => l.Value == effect.m_startEffects);
            if (hasStart)
            {
                if (Previews.StatusShowing)
                {
                    if (button("Take it off you", Skin.Primary)) Previews.StopStatus(true);
                }
                else if (button("Show it on you", Skin.Primary))
                {
                    Previews.ShowStatus(entry);
                }
            }

            foreach (var list in lists)
            {
                if (list.Value == effect.m_startEffects) continue;
                var style = Previews.Playing.IsPlaying(list.Value) ? Skin.On : hasStart ? Skin.Button : Skin.Primary;
                if (button("Play " + list.Key.ToLowerInvariant(), style))
                {
                    if (Previews.Playing.IsPlaying(list.Value)) Previews.Stop(list.Value);
                    else Previews.PlayOnYou(list.Value);
                }
            }

            return "Only the look. The effect itself is never applied to you.";
        }

        // ----- Outfit -----

        /// <summary>
        /// What the person keeps on while other items are tried on over it. Each piece goes to its
        /// item when clicked, and comes off with its cross.
        /// </summary>
        private static float Wearing(Explorer explorer, float width, float y)
        {
            var kept = Looks.Outfit.Keys;
            if (kept.Count == 0) return y;

            y = SectionHeading("KEPT ON", width, y, () =>
            {
                foreach (var key in kept.ToList()) Looks.Outfit.TakeOff(key);
                Previews.Rebuild();
            }, "kept");
            if (IsFolded("kept")) return y;

            var x = 0f;
            var chipH = U(30f);
            foreach (var key in kept.ToList())
            {
                var prefab = Looks.Prefab(key);
                var shared = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                var name = shared != null ? CatalogBuilder.Localize(shared.m_name) : "";
                if (name.Length == 0) name = key;

                var w = Mathf.Min(width, Skin.Chip.CalcSize(new GUIContent(name)).x + U(58f));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += chipH + U(5f);
                }
                var chip = new Rect(x, y, w, chipH);
                var cross = new Rect(chip.xMax - U(26f), chip.y, U(24f), chipH);
                var hover = chip.Contains(Event.current.mousePosition);
                Skin.PillBox(chip, LinkFill(Kind.Item, hover));
                var icon = PrefabIcon(key);
                if (icon != null) DrawSprite(icon, new Rect(chip.x + U(6f), chip.y + U(4f), U(22f), U(22f)));
                var small = Skin.Small;
                var smallWas = small.normal.textColor;
                small.normal.textColor = LinkText(Kind.Item, hover);
                GUI.Label(new Rect(chip.x + U(32f), chip.y, chip.width - U(62f), chip.height), name, small);
                small.normal.textColor = smallWas;
                GUI.Label(cross, "\u00d7", Skin.Cross);

                if (GUI.Button(cross, GUIContent.none, GUIStyle.none))
                {
                    Looks.Outfit.TakeOff(key);
                    Previews.Rebuild();
                }
                else if (GUI.Button(new Rect(chip.x, chip.y, chip.width - U(26f), chip.height), GUIContent.none, GUIStyle.none) && explorer.Jump(key))
                {
                    _reveal = true;
                    _sideScroll = Vector2.zero;
                }
                if (hover) AskTip("kept:" + key, cross.Contains(Event.current.mousePosition) ? "Take it off" : "Go to " + name);
                x += w + U(6f);
            }

            return y + chipH + U(16f);
        }

        // ----- Sound timeline -----

        /// <summary>
        /// Where the playing sound is in its clip, as a bar that can be dragged to any point. Shown
        /// while a sound plays or is paused, which is what makes long clips such as music usable.
        /// </summary>
        private static float Timeline(float width, float y)
        {
            if (!Previews.SoundPosition(out var time, out var length)) return y;

            var rowH = U(26f);
            var labelW = U(52f);
            GUI.Label(new Rect(0f, y, labelW, rowH), Clock(time), Skin.Label);
            var slider = new Rect(labelW, y + (rowH - U(14f)) / 2f, width - labelW * 2f - U(8f), U(14f));

            GUI.changed = false;
            var picked = GUI.HorizontalSlider(slider, time, 0f, length);
            if (GUI.changed) Previews.SeekSound(picked);

            var end = new Rect(width - labelW, y, labelW, rowH);
            var style = Skin.DimLabel;
            var anchor = style.alignment;
            style.alignment = TextAnchor.MiddleRight;
            GUI.Label(end, Clock(length), style);
            style.alignment = anchor;

            return y + rowH + U(14f);
        }

        private static string Clock(float seconds)
        {
            var whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            if (seconds < 10f) return seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
            return $"{whole / 60}:{whole % 60:00}";
        }

        // ----- Sound variants -----

        private static readonly Dictionary<Entry, List<AudioClip>> VariantCache = new Dictionary<Entry, List<AudioClip>>();

        private static List<AudioClip> Variants(Entry entry)
        {
            if (!VariantCache.TryGetValue(entry, out var clips))
            {
                clips = Previews.SoundVariants(entry.Source as GameObject);
                VariantCache[entry] = clips;
            }
            return clips;
        }

        /// <summary>
        /// Every clip the sound can play. The game picks one at random each time; here each can be
        /// played on its own, and the one heard last, chosen or picked, is lit.
        /// </summary>
        private static float Variants(Entry entry, float width, float y)
        {
            var clips = Variants(entry);
            if (clips.Count < 2) return y;

            y = SectionHeading($"VARIANTS  {clips.Count}", width, y, null, "variants");
            if (IsFolded("variants")) return y;
            var now = Previews.SoundClipNow();
            var x = 0f;
            var rowH = U(28f);

            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                var text = $"{i + 1}   {clip.name}";
                var style = clip == now ? Skin.ChipOn : Skin.Chip;
                var w = Mathf.Min(width, style.CalcSize(new GUIContent(text)).x + U(8f));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                var chip = new Rect(x, y, w, rowH);
                if (GUI.Button(chip, text, style)) Previews.PlaySound(entry, clip);
                if (chip.Contains(Event.current.mousePosition))
                {
                    AskTip("variant:" + clip.name, $"{clip.name}\n{clip.length.ToString("0.00", CultureInfo.InvariantCulture)} s");
                }
                x += w + U(5f);
            }

            return y + rowH + U(16f);
        }

        // ----- Modifiers -----

        private static float Adjust(Explorer explorer, Entry entry, float width, float y, bool withStage)
        {
            var modifiers = explorer.Modifiers;
            var staged = Stage.IsStaged(entry);
            var projectile = entry.Kind == Kind.Projectile;
            var modelInWorld = Previews.InWorld && Previews.IsModel(entry);
            var clips = staged ? Previews.Clips() : new List<AnimationClip>();

            // Without the stage there is nothing to adjust unless the copy is in the world.
            if (!withStage && !modelInWorld && !projectile) return y;
            if (!staged && !projectile) return y;

            y = SectionHeading("ADJUST", width, y, () =>
            {
                modifiers.Reset();
                if (entry.Source is GameObject reset) Gear.ResetLoadout(reset);
                Previews.Rebuild();
            }, "adjust");
            var labelW = U(_compact ? 100f : 120f);
            var open = !IsFolded("adjust");

            if (open && staged)
            {
                // Size on a curve, so the range from a tenth to ten times is usable end to end.
                var logScale = Mathf.Log10(modifiers.Scale);
                var picked = SliderRow("Size", $"×{modifiers.Scale.ToString("0.00", CultureInfo.InvariantCulture)}", logScale, -1f, 1f, width, labelW, ref y);
                if (!Mathf.Approximately(picked, logScale)) modifiers.Scale = Mathf.Pow(10f, picked);
            }

            if (open && entry.Kind == Kind.Creature && modifiers.MaxLevel > 1)
            {
                var names = new List<string>();
                for (var level = 1; level <= modifiers.MaxLevel; level++) names.Add(level == 1 ? "No stars" : level == 2 ? "1 star" : $"{level - 1} stars");
                var chosen = Segments("Level", names, modifiers.Level - 1, width, labelW, ref y);
                if (chosen >= 0) modifiers.Level = chosen + 1;
            }

            if (open && modifiers.WearAvailable)
            {
                var chosen = Segments("Wear", new List<string> { "New", "Worn", "Broken" }, (int)modifiers.Wear, width, labelW, ref y);
                if (chosen >= 0) modifiers.Wear = (Wear)chosen;
            }

            if (open && modifiers.LookAvailable)
            {
                var chosen = Segments("Look", new List<string>(modifiers.LookNames), modifiers.Look, width, labelW, ref y);
                if (chosen >= 0) modifiers.Look = chosen;
                if (modifiers.Look > 0 && entry.Source is GameObject creature && Scry.Variants.IsGear(creature)) LoadoutRows(creature, modifiers.Look, width, labelW, ref y);
            }

            if (open && projectile)
            {
                Previews.ProjectileSpeed = SliderRow("Speed", $"{Mathf.RoundToInt(Previews.ProjectileSpeed)} m/s", Previews.ProjectileSpeed, 5f, 120f, width, labelW, ref y);
            }

            if (clips.Count > 0)
            {
                y += U(6f);
                y = Clips(explorer, clips, modifiers, width, labelW, y);
            }

            return y + U(10f);
        }

        /// <summary>The sections folded shut, by key. Details start shut; the rest start open.</summary>
        private static readonly HashSet<string> Folded = new HashSet<string> { "details" };

        private static bool IsFolded(string key) => key != null && Folded.Contains(key);

        /// <summary>Whether this pass over the side has drawn the fold-all link yet.</summary>
        private static bool _foldAllShown;

        /// <summary>Every section that folds, by key.</summary>
        private static readonly string[] Foldable = { "kept", "variants", "adjust", "animations", "effects", "playsin", "links", "facts", "command", "details" };

        private static void FoldAll(bool fold)
        {
            if (fold) foreach (var key in Foldable) Folded.Add(key);
            else Folded.Clear();
            SaveRects();
        }

        /// <summary>
        /// A section's heading and rule. With a key, the heading folds the section shut or opens
        /// it when clicked, and the choice is remembered; the caller skips its body while folded.
        /// </summary>
        private static float SectionHeading(string text, float width, float y, Action reset, string key = null)
        {
            var folded = IsFolded(key);
            var shown = key == null ? text : text + (folded ? "  \u25B8" : "  \u25BE");
            var textW = Skin.Heading.CalcSize(new GUIContent(shown)).x;
            var head = new Rect(0f, y, textW + U(4f), U(20f));
            if (key == null)
            {
                GUI.Label(head, shown, Skin.Heading);
            }
            else
            {
                LinkLabel(head, shown, Skin.Heading, Skin.Heading.normal.textColor);
                if (head.Contains(Event.current.mousePosition)) AskTip("fold:" + key, (folded ? "Open this section" : "Fold this section away") + "\nShift-click: every section");
                if (GUI.Button(head, GUIContent.none, GUIStyle.none))
                {
                    if (Event.current.shift) FoldAll(!folded);
                    else if (folded) Folded.Remove(key);
                    else Folded.Add(key);
                    SaveRects();
                }
            }
            var lineEnd = reset != null ? width - U(74f) : width;

            // The first section that folds carries a link to fold or open them all, at the end of its rule.
            if (key != null && !_foldAllShown)
            {
                _foldAllShown = true;
                var anyOpen = Foldable.Any(k => !Folded.Contains(k));
                var foldText = anyOpen ? "fold all" : "open all";
                var linkW = Skin.FaintLabel.CalcSize(new GUIContent(foldText)).x + U(4f);
                var link = new Rect(lineEnd - linkW, y, linkW, U(20f));
                LinkLabel(link, foldText, Skin.FaintLabel, Skin.Faint);
                if (link.Contains(Event.current.mousePosition)) AskTip("fold-all", anyOpen ? "Fold every section away" : "Open every section");
                if (GUI.Button(link, GUIContent.none, GUIStyle.none)) FoldAll(anyOpen);
                lineEnd = link.x - U(8f);
            }

            Skin.Fill(new Rect(textW + U(12f), y + U(10f), Mathf.Max(0f, lineEnd - textW - U(12f)), U(1f)), Skin.Outline);
            if (reset != null && GUI.Button(new Rect(width - U(64f), y - U(2f), U(64f), U(24f)), "Reset", Skin.Chip)) reset();
            return y + U(folded ? 36f : 30f);
        }

        private static float SliderRow(string label, string value, float current, float min, float max, float width, float labelW, ref float y)
        {
            var rowH = U(26f);
            FitLabel(new Rect(0f, y, labelW - U(6f), rowH), label, Skin.DimLabel, 10f);
            var valueW = U(64f);
            var slider = new Rect(labelW, y + (rowH - U(14f)) / 2f, width - labelW - valueW - U(10f), U(14f));
            var result = GUI.HorizontalSlider(slider, current, min, max);
            GUI.Label(new Rect(width - valueW, y, valueW, rowH), value, Skin.Label);
            y += rowH + U(8f);
            return result;
        }

        private static int Segments(string label, List<string> names, int selected, float width, float labelW, ref float y)
        {
            var rowH = U(28f);
            FitLabel(new Rect(0f, y, labelW - U(6f), rowH), label, Skin.DimLabel, 10f);
            var x = labelW;
            var chosen = -1;
            for (var i = 0; i < names.Count; i++)
            {
                var style = i == selected ? Skin.SegmentOn : Skin.Segment;
                var w = style.CalcSize(new GUIContent(names[i])).x + U(10f);
                if (x + w > width && x > labelW)
                {
                    x = labelW;
                    y += rowH + U(4f);
                }
                if (GUI.Button(new Rect(x, y, w, rowH), names[i], style)) chosen = i;
                x += w + U(4f);
            }
            y += rowH + U(8f);
            return chosen;
        }

        /// <summary>
        /// The weapon, shield and armour a creature can roll, one row each, and the extras it may
        /// be given, each put on or taken off. Only rows with a choice are shown.
        /// </summary>
        private static void LoadoutRows(GameObject creature, int look, float width, float labelW, ref float y)
        {
            var loadout = Gear.LoadoutOf(creature);
            loadout.Carrying(Gear.SetWeapons(creature, look));
            var rows = new[] { Loadout.Row.Holding, Loadout.Row.Weapon, Loadout.Row.Shield, Loadout.Row.Armour };
            var labels = new[] { "Holding", "Weapon", "Shield", "Armour" };
            for (var i = 0; i < rows.Length; i++)
            {
                if (!loadout.Offered(rows[i])) continue;
                var names = loadout.Options(rows[i]).Select(ItemName).ToList();
                for (var n = 0; n < names.Count; n++)
                {
                    if (names.Count(other => other == names[n]) > 1) names[n] = names[n] + " (" + loadout.Options(rows[i])[n] + ")";
                }

                // A shield the weapon leaves no hand for is shown put away, and still chosen for later.
                var held = loadout.Held(rows[i]);
                var was = GUI.color;
                if (!held) GUI.color = new Color(was.r, was.g, was.b, was.a * 0.4f);
                var top = y;
                var chosen = Segments(labels[i], names, loadout.Chosen(rows[i]), width, labelW, ref y);
                GUI.color = was;
                if (!held && new Rect(0f, top, width, y - top).Contains(Event.current.mousePosition))
                {
                    AskTip("shield-away", "Not worn: the weapon takes both hands. Pick a one-handed weapon to wear it.");
                }
                if (chosen < 0 || chosen == loadout.Chosen(rows[i])) continue;
                loadout.Choose(rows[i], chosen);
                Previews.Rebuild();
            }

            if (loadout.Extras.Count == 0) return;
            var extras = loadout.Extras.Select(e => ItemName(e.Name)).ToList();
            var clicked = Toggles("Extras", extras, loadout.ExtraOn, width, labelW, ref y);
            if (clicked < 0) return;
            loadout.ToggleExtra(clicked);
            Previews.Rebuild();
        }

        /// <summary>An item's name as the game shows it, or "Nothing" for an empty choice.</summary>
        private static string ItemName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return "Nothing";
            var shared = Looks.Prefab(prefabName)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            var shown = shared != null ? CatalogBuilder.Localize(shared.m_name) : "";
            return shown.Length > 0 ? shown : prefabName;
        }

        /// <summary>Like <see cref="Segments"/>, but any number can be on; returns the one clicked.</summary>
        private static int Toggles(string label, List<string> names, Func<int, bool> on, float width, float labelW, ref float y)
        {
            var rowH = U(28f);
            FitLabel(new Rect(0f, y, labelW - U(6f), rowH), label, Skin.DimLabel, 10f);
            var x = labelW;
            var clicked = -1;
            for (var i = 0; i < names.Count; i++)
            {
                var style = on(i) ? Skin.SegmentOn : Skin.Segment;
                var w = style.CalcSize(new GUIContent(names[i])).x + U(10f);
                if (x + w > width && x > labelW)
                {
                    x = labelW;
                    y += rowH + U(4f);
                }
                if (GUI.Button(new Rect(x, y, w, rowH), names[i], style)) clicked = i;
                x += w + U(4f);
            }
            y += rowH + U(8f);
            return clicked;
        }

        /// <summary>
        /// Every animation clip the creature has, each played directly on the copy. The one playing
        /// is lit; Stop hands the copy back to its own animations.
        /// </summary>
        private static float Clips(Explorer explorer, List<AnimationClip> clips, Modifiers modifiers, float width, float labelW, float y)
        {
            _groundsFor = explorer.Selected;
            var playing = Previews.PlayingClip();
            y = SectionHeading($"ANIMATIONS  {clips.Count}", width, y, null, "animations");
            if (IsFolded("animations")) return y;

            var speed = SliderRow("Speed", $"×{modifiers.AnimationSpeed.ToString("0.0", CultureInfo.InvariantCulture)}", modifiers.AnimationSpeed, 0f, Modifiers.MaxAnimationSpeed, width, labelW, ref y);
            if (!Mathf.Approximately(speed, modifiers.AnimationSpeed)) modifiers.AnimationSpeed = speed;

            // The ground footsteps sound on, when the creature sounds different on some.
            var grounds = Previews.Grounds(_groundsFor?.Source as GameObject);
            if (grounds.Count > 1)
            {
                var names = grounds.Select(g => g == FootStep.GroundMaterial.Default ? "Plain" : g == FootStep.GroundMaterial.GenericGround ? "Ground" : Naming.FieldLabel(g.ToString())).ToList();
                var chosen = Segments("Ground", names, Mathf.Max(0, grounds.IndexOf(Previews.StepGround)), width, labelW, ref y);
                if (chosen >= 0) Previews.StepGround = grounds[chosen];
            }

            var x = 0f;
            var rowH = U(26f);

            if (GUI.Button(new Rect(x, y, U(84f), rowH), Previews.LoopClips ? "Repeat on" : "Repeat off", Previews.LoopClips ? Skin.ChipOn : Skin.Chip)) Previews.ToggleLoopClips();
            x += U(90f);
            if (playing != null && GUI.Button(new Rect(x, y, U(60f), rowH), "Stop", Skin.Chip)) Previews.StopClip();
            x += U(66f);

            if (clips.Count > 12)
            {
                if (width - x < U(150f))
                {
                    x = 0f;
                    y += rowH + U(6f);
                }
                var field = new Rect(x, y - U(1f), Mathf.Min(width - x, U(260f)), U(28f));
                GUI.SetNextControlName(ClipControl);
                _clipFilter = GUI.TextField(field, _clipFilter, 40, Skin.Field);
                if (string.IsNullOrEmpty(_clipFilter)) GUI.Label(field, "Filter", Skin.Placeholder);
            }
            y += rowH + U(10f);

            // The clip playing, to pause and scrub through, like a sound.
            if (playing != null && Previews.ClipPosition(out var time, out var length))
            {
                var pauseW = U(84f);
                if (GUI.Button(new Rect(0f, y, pauseW, rowH), Previews.ClipPaused ? "Resume" : "Pause", Previews.ClipPaused ? Skin.ChipOn : Skin.Chip)) Previews.PauseClip(!Previews.ClipPaused);
                var readout = $"{time.ToString("0.00", CultureInfo.InvariantCulture)} / {length.ToString("0.00", CultureInfo.InvariantCulture)} s";
                var readW = Skin.DimLabel.CalcSize(new GUIContent(readout)).x + U(6f);
                var slider = new Rect(pauseW + U(10f), y + (rowH - U(14f)) / 2f, Mathf.Max(U(40f), width - pauseW - readW - U(20f)), U(14f));
                var picked = GUI.HorizontalSlider(slider, time, 0f, length);
                if (!Mathf.Approximately(picked, time)) Previews.SeekClip(picked);
                GUI.Label(new Rect(width - readW, y, readW, rowH), readout, Skin.DimLabel);
                y += rowH + U(10f);
            }

            x = 0f;
            foreach (var clip in clips)
            {
                if (_clipFilter.Length > 0 && clip.name.IndexOf(_clipFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var on = playing == clip;
                var style = on ? Skin.ChipOn : Skin.Chip;
                var w = Mathf.Min(width, style.CalcSize(new GUIContent(clip.name)).x + U(8f));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                var chip = new Rect(x, y, w, rowH);
                if (GUI.Button(chip, clip.name, style))
                {
                    if (on) Previews.StopClip();
                    else
                    {
                        Previews.PlayClip(clip);
                        Previews.LastClip = clip;
                    }
                }
                if (chip.Contains(Event.current.mousePosition))
                {
                    AskTip("clip:" + clip.name, $"{clip.name}\n{clip.length.ToString("0.0", CultureInfo.InvariantCulture)} s{(clip.isLooping ? ", loops" : "")}");
                }
                x += w + U(5f);
            }
            if (x > 0f) y += rowH;

            var last = Previews.LastClip;
            if (last != null && clips.Contains(last))
            {
                var members = Previews.ClipMembers(last).ToArray();
                if (members.Length > 0)
                {
                    y += U(10f);
                    y = Members(explorer, "In " + last.name + ":", last, members, null, width, y);
                }
            }

            return y + U(10f);
        }

        private static string OriginText(Entry entry)
        {
            if (entry.Origin == Origin.Vanilla) return "from the game";
            if (entry.Origin != Origin.Mod) return "";
            return entry.ModName.Length > 0 ? "added by " + entry.ModName : "added by a mod";
        }

        // ----- Stage buttons -----

        /// <summary>
        /// Front, side and top views and a fit, in the stage's bottom right corner. Picking a view
        /// holds the model still, so it stays in that view. Returns the width they take.
        /// </summary>
        private static float ViewButtons(Rect inner)
        {
            var h = U(22f);
            var y = inner.yMax - U(8f) - h;
            var x = inner.xMax - U(8f);
            var views = new[] { ("Fit", "Frame it whole again"), ("Top", "Look down on it"), ("Side", "Look at it from the side"), ("Front", "Look at it from the front") };
            foreach (var (name, tip) in views)
            {
                var w = Skin.Chip.CalcSize(new GUIContent(name)).x + U(2f);
                x -= w;
                var chip = new Rect(x, y, w, h);
                x -= U(4f);
                if (chip.Contains(Event.current.mousePosition)) AskTip("view:" + name, tip);
                if (!GUI.Button(chip, name, Skin.Chip)) continue;

                Stage.View(name);
                if (name != "Fit" && Stage.Spin)
                {
                    Stage.Spin = false;
                    SaveRects();
                }
            }
            return inner.xMax - U(8f) - x;
        }

        /// <summary>The person for size, the lighting and the backdrop, in the stage's top right corner.</summary>
        private static void StageButtons(Entry entry, Rect rect)
        {
            var h = U(24f);
            var y = rect.y + U(10f);
            var x = rect.xMax - U(10f);

            // Everything the row will hold, measured first, so it can move clear of the kind badge.
            var wearable = entry.Kind == Kind.Item && entry.Source is GameObject wornItem && Gear.IsWearable(wornItem);
            var texts = new List<string> { Stage.BackdropNames[Stage.BackdropIndex], Stage.LightingNames[Stage.LightingIndex], "Spin" };
            if (wearable) texts.Add("Worn");
            if (!Looks.IsWorn(entry)) texts.Add("Person");
            var total = texts.Sum(t => Skin.Chip.CalcSize(new GUIContent(t)).x + U(10f));
            if (x - total < rect.x + U(10f) + _badgeWidth + U(10f)) y += h + U(8f);

            bool Chip(string text, bool on, string tip)
            {
                var style = on ? Skin.ChipOn : Skin.Chip;
                var w = style.CalcSize(new GUIContent(text)).x + U(4f);
                x -= w;
                var chip = new Rect(x, y, w, h);
                x -= U(6f);
                if (chip.Contains(Event.current.mousePosition)) AskTip("stage:" + tip, tip);
                return GUI.Button(chip, text, style);
            }

            if (Chip("Spin", Stage.Spin, Stage.Spin ? "Turning; click to hold it still" : "Held still; click to turn it"))
            {
                Stage.Spin = !Stage.Spin;
                SaveRects();
            }
            if (Chip(Stage.BackdropNames[Stage.BackdropIndex], false, "Backdrop: click for the next"))
            {
                Stage.BackdropIndex = (Stage.BackdropIndex + 1) % Stage.BackdropNames.Length;
                SaveRects();
            }
            if (Chip(Stage.LightingNames[Stage.LightingIndex], false, "Lighting: click for the next"))
            {
                Stage.LightingIndex = (Stage.LightingIndex + 1) % Stage.LightingNames.Length;
                SaveRects();
            }
            if (wearable && Chip("Worn", Looks.OnPerson, "Show it worn by a person"))
            {
                Looks.OnPerson = !Looks.OnPerson;
                Previews.Rebuild();
                SaveRects();
            }
            if (!Looks.IsWorn(entry) && Chip("Person", Stage.ShowPerson, "A person beside it, to judge its size"))
            {
                Stage.ShowPerson = !Stage.ShowPerson;
                SaveRects();
            }
        }

        // ----- Effects -----

        private static readonly Dictionary<string, List<KeyValuePair<string, EffectList>>> EffectCache =
            new Dictionary<string, List<KeyValuePair<string, EffectList>>>();

        /// <summary>
        /// Every effect list the prefab carries, played on the stage copy and on the copy in the
        /// world: a creature's hits and death, a piece's placing and breaking, an item's swings.
        /// </summary>
        private static float Effects(Explorer explorer, Entry entry, float width, float y, bool withStage)
        {
            if (!(entry.Source is GameObject prefab) || entry.Kind == Kind.Sound || entry.Kind == Kind.Effect) return y;
            if (!withStage && !(Previews.InWorld && Previews.IsModel(entry))) return y;

            // A creature's chips follow what it has on: the weapon in its hand, not the rest.
            var carried = Previews.CarriedNow(entry);
            var triggers = Previews.StageTriggers();
            var cacheKey = entry.Key + "|" + (carried == null ? "all" : string.Join(",", carried.Select(c => c.name))) + "|" + (triggers == null ? "" : triggers.Count.ToString());
            if (!EffectCache.TryGetValue(cacheKey, out var lists))
            {
                lists = Previews.PrefabLists(prefab, carried, triggers);
                EffectCache[cacheKey] = lists;
            }
            if (lists.Count == 0) return y;

            y = SectionHeading($"EFFECTS  {lists.Count}", width, y, null, "effects");
            if (IsFolded("effects")) return y;
            var rowH = U(26f);

            if (lists.Count > 12)
            {
                var field = new Rect(0f, y, Mathf.Min(width, U(260f)), U(28f));
                GUI.SetNextControlName(EffectControl);
                _effectFilter = GUI.TextField(field, _effectFilter, 40, Skin.Field);
                if (string.IsNullOrEmpty(_effectFilter)) GUI.Label(field, "Filter", Skin.Placeholder);
                y += U(36f);
            }

            var x = 0f;
            foreach (var pair in lists)
            {
                if (_effectFilter.Length > 0 && pair.Key.IndexOf(_effectFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var w = Mathf.Min(width, Skin.Chip.CalcSize(new GUIContent(pair.Key)).x + U(8f));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                var chip = new Rect(x, y, w, rowH);
                var playing = Previews.Playing.IsPlaying(pair.Value);
                if (GUI.Button(chip, pair.Key, playing ? Skin.ChipOn : Skin.Chip))
                {
                    if (playing) Previews.Stop(pair.Value);
                    else Previews.PlayEffectList(pair.Key, pair.Value);
                }
                if (playing && chip.Contains(Event.current.mousePosition)) AskTip("fx-stop:" + pair.Key, "Playing; click to stop it");
                if (chip.Contains(Event.current.mousePosition))
                {
                    var names = pair.Value.m_effectPrefabs.Where(d => d?.m_prefab != null).Select(d => d.m_prefab.name);
                    AskTip("fx:" + pair.Key, string.Join("\n", names));
                }
                x += w + U(5f);
            }
            if (x > 0f) y += rowH;

            // What the list played last is made of, each part lit while its copy plays.
            var last = lists.FirstOrDefault(l => ReferenceEquals(l.Value, Previews.Playing.Last));
            if (last.Value != null)
            {
                y += U(10f);
                y = Members(explorer, "In " + last.Key + ":", last.Value, Members(last.Value), null, width, y);
            }

            return y + U(14f);
        }

        /// <summary>The prefabs an effect list plays, each once.</summary>
        private static string[] Members(EffectList list)
        {
            return list.m_effectPrefabs.Where(d => d != null && d.m_enabled && d.m_prefab != null).Select(d => d.m_prefab.name).Distinct().ToArray();
        }

        /// <summary>
        /// The parts of a list as chips: each goes to its prefab, and is lit while the copy of it
        /// the list last started still plays. The selected prefab itself is shown but not a link.
        /// </summary>
        private static Entry _groundsFor;

        private static float Members(Explorer explorer, string title, object list, string[] members, string self, float width, float y)
        {
            if (title != null)
            {
                GUI.Label(new Rect(0f, y, width, U(20f)), title, Skin.DimLabel);
                y += U(24f);
            }
            var x = 0f;
            var rowH = U(26f);
            foreach (var member in members)
            {
                var lit = Previews.Playing.IsPlaying(list, member);
                var go = member != self && InCatalog(explorer, member);
                var w = Mathf.Min(width, LinkChipWidth(member, go));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                var chip = new Rect(x, y, w, rowH);
                if (LinkChip(chip, member, KindOf(explorer, member), lit, go)) Go(explorer, member);
                if (go && chip.Contains(Event.current.mousePosition)) AskTip("member:" + member, "Go to " + member + (lit ? "\n(playing now)" : ""));
                x += w + U(5f);
            }
            return y + rowH + U(6f);
        }

        // ----- Plays in -----

        private static bool _allPlaysIn;
        private static Entry _playsInFor;
        private static List<PlaysInRow> _playsInRows = new List<PlaysInRow>();

        /// <summary>
        /// The effect lists a sound or effect is part of, one row per list: what it is for, who
        /// plays it, and what else it plays, all of which go where they name. Play plays the whole
        /// list: an effect's around it on the stage, a sound's where you are looking.
        /// </summary>
        private static float PlaysInSection(Explorer explorer, Entry entry, float width, float y)
        {
            if (_playsInFor != entry)
            {
                _playsInFor = entry;
                _allPlaysIn = false;

                // A list the entry itself plays is its own, not one it plays in; and it is not
                // named among what plays along.
                _playsInRows = EffectLinks.For(entry.Name)
                    .Select(r => new PlaysInRow
                    {
                        Label = r.Label, List = r.List,
                        Owners = r.Owners.Where(o => o.Key != entry.Name).ToList(),
                        Members = r.Members.Where(m => m != entry.Name).ToArray(),
                    })
                    .Where(r => r.Owners.Count > 0)
                    .ToList();
            }
            var rows = _playsInRows;
            if (rows.Count == 0) return y;

            y = SectionHeading($"PLAYS IN  {rows.Count}", width, y, null, "playsin");
            if (IsFolded("playsin")) return y;
            const int Shown = 8;
            var rowH = U(26f);

            foreach (var row in _allPlaysIn ? rows : rows.Take(Shown))
            {
                var list = row.List as EffectList;
                var playing = list != null && Previews.Playing.IsPlaying(list);

                // Play, what it is for, and who plays it.
                var x = 0f;
                var playText = "\u25B6 Play";
                var playW = Skin.Chip.CalcSize(new GUIContent(playText)).x + U(12f);
                if (list != null && GUI.Button(new Rect(x, y, playW, rowH), playText, playing ? Skin.ChipOn : Skin.Chip))
                {
                    if (playing) Previews.Stop(list);
                    else Previews.PlayWhole(entry, list);
                }
                if (new Rect(x, y, playW, rowH).Contains(Event.current.mousePosition)) AskTip("playrow:" + row.Label + row.Owners[0].Shown, "Play the whole list together");
                x += playW + U(8f);
                var labelW = Mathf.Min(width - x, Skin.Label.CalcSize(new GUIContent(row.Label)).x + U(4f));
                GUI.Label(new Rect(x, y, labelW, rowH), row.Label, Skin.Label);
                x += labelW + U(8f);

                const int Owners = 4;
                foreach (var owner in row.Owners.Take(Owners))
                {
                    var shown = ShownName(explorer, owner.Key, owner.Shown);
                    var go = owner.Key != null && CanGo(explorer, owner.Key);
                    var w = Mathf.Min(width, LinkChipWidth(shown, go));
                    if (x + w > width && x > 0f)
                    {
                        x = 0f;
                        y += rowH + U(5f);
                    }
                    var chip = new Rect(x, y, w, rowH);
                    var kind = owner.Key != null && owner.Key.StartsWith("se:", StringComparison.Ordinal) ? Kind.StatusEffect : KindOf(explorer, owner.Key);
                    if (LinkChip(chip, shown, kind, false, go)) Go(explorer, owner.Key);
                    if (go && chip.Contains(Event.current.mousePosition)) AskTip("owner:" + owner.Key, "Go to " + shown);
                    x += w + U(5f);
                }
                if (row.Owners.Count > Owners)
                {
                    var more = $"and {row.Owners.Count - Owners} more";
                    var w = Skin.DimLabel.CalcSize(new GUIContent(more)).x + U(4f);
                    if (x + w > width && x > 0f)
                    {
                        x = 0f;
                        y += rowH + U(5f);
                    }
                    var rect = new Rect(x, y, w, rowH);
                    GUI.Label(rect, more, Skin.DimLabel);
                    if (rect.Contains(Event.current.mousePosition)) AskTip("owners:" + row.Label + row.Owners[0].Shown, string.Join("\n", row.Owners.Skip(Owners).Take(30).Select(o => o.Shown)));
                }
                y += rowH + U(5f);

                // What plays along.
                if (list != null) y = Members(explorer, null, list, row.Members, entry.Name, width, y);
                y += U(6f);
            }

            if (rows.Count > Shown)
            {
                var text = _allPlaysIn ? "Show fewer" : $"Show all {rows.Count}";
                var w = Skin.Chip.CalcSize(new GUIContent(text)).x + U(8f);
                if (GUI.Button(new Rect(0f, y, Mathf.Min(width, w), rowH), text, Skin.Chip)) _allPlaysIn = !_allPlaysIn;
                y += rowH;
            }
            return y + U(14f);
        }

        /// <summary>Whether a prefab or status effect ("se:" name) is in the catalog to go to.</summary>
        private static bool CanGo(Explorer explorer, string key)
        {
            if (!key.StartsWith("se:", StringComparison.Ordinal)) return InCatalog(explorer, key);
            if (_statusFor != explorer)
            {
                _statusFor = explorer;
                StatusNames.Clear();
                foreach (var e in explorer.Catalog) if (e.Kind == Kind.StatusEffect) StatusNames.Add(e.Name);
            }
            return StatusNames.Contains(key.Substring(3));
        }

        private static readonly HashSet<string> StatusNames = new HashSet<string>();
        private static Explorer _statusFor;

        private static readonly Dictionary<string, string> ShownNames = new Dictionary<string, string>();
        private static Explorer _shownFor;

        /// <summary>A prefab's name as the game shows it, or the name given when it has none.</summary>
        private static string ShownName(Explorer explorer, string key, string fallback)
        {
            if (key == null) return fallback;
            if (_shownFor != explorer)
            {
                _shownFor = explorer;
                ShownNames.Clear();
                foreach (var e in explorer.Catalog)
                {
                    if (!string.IsNullOrEmpty(e.DisplayName) && !ShownNames.ContainsKey(e.Key)) ShownNames[e.Key] = e.DisplayName;
                }
            }
            return ShownNames.TryGetValue(key, out var shown) ? shown : fallback;
        }

        // ----- Facts -----

        private static float FactsSection(Explorer explorer, Entry entry, float width, float y)
        {
            var facts = Facts.For(entry);
            if (facts.IsEmpty) return y;

            y = SectionHeading("IN THE GAME", width, y, null, "facts");
            if (IsFolded("facts")) return y;

            if (facts.Description.Length > 0)
            {
                var height = Skin.Wrap.CalcHeight(new GUIContent(facts.Description), width);
                GUI.Label(new Rect(0f, y, width, height), facts.Description, Skin.Wrap);
                y += height + U(10f);
            }

            // A two-column table: what it is on the left, its value on the right.
            var labelW = Mathf.Min(U(130f), width * 0.36f);
            foreach (var pair in facts.Pairs)
            {
                var valueW = width - labelW - U(10f);
                var labelH = Skin.DimWrap.CalcHeight(new GUIContent(pair.Key), labelW);
                var height = Mathf.Max(U(20f), Mathf.Max(labelH, Skin.Wrap.CalcHeight(new GUIContent(pair.Value), valueW)));
                GUI.Label(new Rect(0f, y, labelW, labelH), pair.Key, Skin.DimWrap);
                var valueRect = new Rect(labelW + U(10f), y, valueW, height);
                if (facts.Links.TryGetValue(pair.Key, out var link))
                {
                    var linkW = Mathf.Min(valueW, Skin.Wrap.CalcSize(new GUIContent(pair.Value)).x + U(4f));
                    var linkRect = new Rect(valueRect.x, valueRect.y, linkW, height);
                    LinkLabel(linkRect, pair.Value, Skin.Wrap, LinkText(KindOfKey(explorer, link), false));
                    if (linkRect.Contains(Event.current.mousePosition)) AskTip("link:" + link, "Go to " + pair.Value);
                    if (GUI.Button(linkRect, GUIContent.none, GUIStyle.none)) Go(explorer, link);
                }
                else
                {
                    GUI.Label(valueRect, pair.Value, Skin.Wrap);
                }
                y += height + U(6f);
            }

            foreach (var row in facts.Rows)
            {
                y += U(6f);
                if (!string.IsNullOrEmpty(row.TitleLink) && InCatalog(explorer, row.TitleLink))
                {
                    var titleW = Mathf.Min(width, Skin.DimLabel.CalcSize(new GUIContent(row.Title)).x + U(4f));
                    var titleRect = new Rect(0f, y, titleW, U(20f));
                    LinkLabel(titleRect, row.Title, Skin.DimLabel, LinkText(KindOfKey(explorer, row.TitleLink), false));
                    if (titleRect.Contains(Event.current.mousePosition)) AskTip("station:" + row.TitleLink, "Go to " + row.TitleLink);
                    if (GUI.Button(titleRect, GUIContent.none, GUIStyle.none)) Go(explorer, row.TitleLink);
                }
                else
                {
                    GUI.Label(new Rect(0f, y, width, U(20f)), row.Title, Skin.DimLabel);
                }
                y += U(24f);

                var x = 0f;
                var chipH = U(30f);
                foreach (var item in row.Items)
                {
                    var text = string.IsNullOrEmpty(item.Amount) ? item.Name : $"{item.Amount}  {item.Name}";
                    var w = Mathf.Min(width, Skin.Chip.CalcSize(new GUIContent(text)).x + U(30f));
                    if (x + w > width && x > 0f)
                    {
                        x = 0f;
                        y += chipH + U(5f);
                    }
                    var chip = new Rect(x, y, w, chipH);
                    var hover = chip.Contains(Event.current.mousePosition);
                    var goes = !string.IsNullOrEmpty(item.Prefab) && InCatalog(explorer, item.Prefab);
                    var kind = goes ? KindOf(explorer, item.Prefab) : null;
                    Skin.PillBox(chip, goes ? LinkFill(kind, hover) : Skin.Raised);
                    if (item.Icon != null) DrawSprite(item.Icon, new Rect(chip.x + U(6f), chip.y + U(4f), U(22f), U(22f)));
                    var small = Skin.Small;
                    var smallWas = small.normal.textColor;
                    if (goes) small.normal.textColor = LinkText(kind, hover);
                    GUI.Label(new Rect(chip.x + U(32f), chip.y, chip.width - U(36f), chip.height), text, small);
                    small.normal.textColor = smallWas;

                    // Clicking an ingredient or a drop goes to it.
                    if (!string.IsNullOrEmpty(item.Prefab))
                    {
                        if (hover) AskTip("goto:" + item.Prefab, $"Go to {item.Name}");
                        if (GUI.Button(chip, GUIContent.none, GUIStyle.none) && explorer.Jump(item.Prefab))
                        {
                            _reveal = true;
                            _sideScroll = Vector2.zero;
                            _help = false;
                        }
                    }
                    x += w + U(6f);
                }
                y += chipH + U(6f);
            }

            if (facts.Where.Count > 0)
            {
                y += U(6f);
                GUI.Label(new Rect(0f, y, width, U(20f)), facts.WhereTitle, Skin.DimLabel);
                y += U(24f);
                foreach (var source in facts.Where)
                {
                    // A line naming a prefab in the catalog is a chip that goes there; the rest is text.
                    if (string.IsNullOrEmpty(source.Prefab) || !InCatalog(explorer, source.Prefab))
                    {
                        var height = Skin.Wrap.CalcHeight(new GUIContent(source.Text), width);
                        GUI.Label(new Rect(0f, y, width, height), source.Text, Skin.Wrap);
                        y += height + U(4f);
                        continue;
                    }

                    var icon = PrefabIcon(source.Prefab);
                    var textX = icon != null ? U(34f) : U(12f);
                    var textW = width - textX - U(10f);
                    var chipH = Mathf.Max(U(30f), Skin.Small.CalcHeight(new GUIContent(source.Text), textW) + U(10f));
                    var chip = new Rect(0f, y, width, chipH);
                    var hover = chip.Contains(Event.current.mousePosition);
                    var kind = KindOf(explorer, source.Prefab);
                    Skin.Box(chip, LinkFill(kind, hover));
                    if (icon != null) DrawSprite(icon, new Rect(U(6f), y + (chipH - U(22f)) / 2f, U(22f), U(22f)));
                    var wrapped = new GUIStyle(Skin.Small) { wordWrap = true };
                    wrapped.normal.textColor = LinkText(kind, hover);
                    GUI.Label(new Rect(textX, y, textW, chipH), source.Text, wrapped);
                    if (hover) AskTip("src:" + source.Prefab, "Go to " + source.Prefab);
                    if (GUI.Button(chip, GUIContent.none, GUIStyle.none) && explorer.Jump(source.Prefab))
                    {
                        _reveal = true;
                        _sideScroll = Vector2.zero;
                        _help = false;
                    }
                    y += chipH + U(5f);
                }
            }

            return y + U(14f);
        }

        /// <summary>Goes to a prefab, or to a status effect when the target starts with "se:".</summary>
        private static void Go(Explorer explorer, string target)
        {
            var statusEffect = target.StartsWith("se:", StringComparison.Ordinal);
            if (!explorer.Jump(statusEffect ? target.Substring(3) : target, statusEffect)) return;
            _reveal = true;
            _sideScroll = Vector2.zero;
            _help = false;
        }

        /// <summary>Puts a search in the box, with every other filter cleared so it shows all it finds.</summary>
        private static void SearchFor(Explorer explorer, string text)
        {
            explorer.KindFilter = null;
            explorer.FavouritesOnly = false;
            explorer.RecentOnly = false;
            explorer.Origin = OriginFilter.All;
            explorer.Text = text;
            _listScroll = Vector2.zero;
            _reveal = true;
            _help = false;
        }

        /// <summary>Text drawn as a link: accent coloured, brighter under the mouse.</summary>
        private static void LinkLabel(Rect rect, string text, GUIStyle style) => LinkLabel(rect, text, style, Skin.Accent);

        /// <summary>Text that can be clicked: in its colour, brighter under the mouse.</summary>
        private static void LinkLabel(Rect rect, string text, GUIStyle style, Color colour)
        {
            var was = style.normal.textColor;
            style.normal.textColor = rect.Contains(Event.current.mousePosition) ? Color.Lerp(colour, Color.white, 0.35f) : colour;
            GUI.Label(rect, text, style);
            style.normal.textColor = was;
        }

        /// <summary>
        /// A chip that goes to an entry rather than doing something: tinted in the colour of the
        /// kind of entry it goes to, with an arrow. Brighter while what it names is playing. One
        /// that goes nowhere (the entry itself, or something not in the catalog) is plain.
        /// </summary>
        private static bool LinkChip(Rect rect, string text, Kind? kind, bool lit, bool go)
        {
            var hover = go && rect.Contains(Event.current.mousePosition);
            Skin.PillBox(rect, go || lit ? LinkFill(kind, hover, lit) : new Color(0.2f, 0.2f, 0.22f, 0.45f));

            var style = Skin.Small;
            var was = style.normal.textColor;
            var alignment = style.alignment;
            style.normal.textColor = go || lit ? LinkText(kind, hover || lit) : Skin.Dim;
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(rect, go ? text + "  \u203A" : text, style);
            style.normal.textColor = was;
            style.alignment = alignment;

            return go && GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        /// <summary>The fill of anything that goes to an entry: a dark shade of its kind's colour.</summary>
        private static Color LinkFill(Kind? kind, bool hover, bool lit = false)
        {
            var colour = kind.HasValue ? Skin.KindColor(kind.Value) : Skin.Neutral;
            var fill = lit ? 0.55f : hover ? 0.36f : 0.22f;
            return new Color(colour.r * fill, colour.g * fill, colour.b * fill, 0.95f);
        }

        /// <summary>The text of anything that goes to an entry: its kind's colour, lighter.</summary>
        private static Color LinkText(Kind? kind, bool hover)
        {
            var colour = kind.HasValue ? Skin.KindColor(kind.Value) : Skin.Neutral;
            return Color.Lerp(colour, Color.white, hover ? 0.6f : 0.35f);
        }

        private static float LinkChipWidth(string text, bool go) => Skin.Small.CalcSize(new GUIContent(go ? text + "  \u203A" : text)).x + U(16f);

        /// <summary>The kind of the entry a prefab name goes to, when it is in the catalog.</summary>
        private static Kind? KindOf(Explorer explorer, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_kindsFor != explorer)
            {
                _kindsFor = explorer;
                KindByName.Clear();
                foreach (var e in explorer.Catalog) if (e.Kind != Kind.StatusEffect && !KindByName.ContainsKey(e.Name)) KindByName[e.Name] = e.Kind;
            }
            return KindByName.TryGetValue(name, out var kind) ? kind : (Kind?)null;
        }

        private static readonly Dictionary<string, Kind> KindByName = new Dictionary<string, Kind>();
        private static Explorer _kindsFor;

        /// <summary>A small heading and a wrapping row of link chips, each going to what it names.</summary>
        private static float LinkRow(Explorer explorer, string title, IEnumerable<string> names, float width, float y)
        {
            return LinkItems(explorer, title, names.Select(n => (n, ShownName(explorer, n, n), "Go to " + ShownName(explorer, n, n), (Action)(() => Go(explorer, n)))), width, y);
        }

        /// <summary>A small heading and a wrapping row of link chips, each with its own text, tip and doing.</summary>
        private static float LinkItems(Explorer explorer, string title, IEnumerable<(string Key, string Text, string Tip, Action Click)> items, float width, float y)
        {
            GUI.Label(new Rect(0f, y, width, U(20f)), title, Skin.DimLabel);
            y += U(24f);
            var x = 0f;
            var rowH = U(26f);
            foreach (var item in items)
            {
                var w = Mathf.Min(width, LinkChipWidth(item.Text, true));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                var chip = new Rect(x, y, w, rowH);
                if (LinkChip(chip, item.Text, KindOfKey(explorer, item.Key), false, true)) item.Click();
                if (chip.Contains(Event.current.mousePosition)) AskTip("link:" + title + item.Key + item.Text, item.Tip);
                x += w + U(5f);
            }
            return y + rowH + U(10f);
        }

        private static Kind? KindOfKey(Explorer explorer, string key) =>
            key != null && key.StartsWith("se:", StringComparison.Ordinal) ? Kind.StatusEffect : KindOf(explorer, key);

        // ----- Linked -----

        /// <summary>
        /// Everything else the entry is linked to, a row per heading: what it leaves behind or is
        /// left by, what it carries, its footsteps, the sounds its animations name, what it
        /// spawns, its set, its ammo, its status effects. A sound an animation names goes to the
        /// creature and plays that animation there.
        /// </summary>
        private static float LinksSection(Explorer explorer, Entry entry, float width, float y)
        {
            var groups = entry.LinkGroups();
            if (groups.Count == 0 && entry.LeftBy.Count == 0 && entry.LeavesBehind.Count == 0) return y;

            y = SectionHeading("LINKED", width, y, null, "links");
            if (IsFolded("links")) return y;

            if (entry.LeftBy.Count > 0) y = LinkRow(explorer, "Left behind by", entry.LeftBy.Take(24), width, y);
            if (entry.LeavesBehind.Count > 0) y = LinkRow(explorer, "Leaves behind", entry.LeavesBehind, width, y);

            foreach (var group in groups)
            {
                var links = group.Value.Take(40).ToList();
                var more = group.Value.Count > links.Count ? $" (first {links.Count} of {group.Value.Count})" : "";
                var title = group.Key + more;

                if (group.Key == Relations.PlayedByAnimation)
                {
                    // One chip per animation, which goes to the creature and plays it.
                    var items = links.SelectMany(l => l.Notes.Count > 0 ? l.Notes.Select(n => (l.Target, n)) : new[] { (l.Target, "") })
                        .Select(p =>
                        {
                            var shown = ShownName(explorer, p.Target, p.Target);
                            var text = p.Item2.Length > 0 ? shown + " \u00b7 " + p.Item2 : shown;
                            var tip = p.Item2.Length > 0 ? $"Go to {shown} and play its {p.Item2} animation" : "Go to " + shown;
                            return (p.Target, text, tip, (Action)(() =>
                            {
                                if (p.Item2.Length > 0) Previews.PlayClipOnShow(p.Item2);
                                Go(explorer, p.Target);
                            }));
                        });
                    y = LinkItems(explorer, title, items, width, y);
                    continue;
                }

                y = LinkItems(explorer, title, links.Select(l =>
                {
                    var shown = ShownName(explorer, l.Target, l.Target);
                    var note = l.Notes.Count == 1 && l.Notes[0].Length <= 28 ? " \u00b7 " + l.Notes[0] : "";
                    var tip = "Go to " + shown + (l.Notes.Count > 0 ? "\n" + string.Join("\n", l.Notes.Take(12)) : "");
                    return (l.Target, shown + note, tip, (Action)(() => Go(explorer, l.Target)));
                }), width, y);
            }
            return y + U(4f);
        }

        /// <summary>A small heading and a wrapping row of chips, each doing its own thing when clicked.</summary>
        private static float ChipRow(string title, IEnumerable<KeyValuePair<string, Action>> chips, float width, float y)
        {
            GUI.Label(new Rect(0f, y, width, U(20f)), title, Skin.DimLabel);
            y += U(24f);
            var x = 0f;
            var rowH = U(26f);
            foreach (var chip in chips)
            {
                var w = Mathf.Min(width, Skin.Chip.CalcSize(new GUIContent(chip.Key)).x + U(8f));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                if (GUI.Button(new Rect(x, y, w, rowH), chip.Key, Skin.Chip)) chip.Value();
                x += w + U(5f);
            }
            return y + rowH + U(10f);
        }

        private static readonly Dictionary<string, Sprite> PrefabIcons = new Dictionary<string, Sprite>();
        private static HashSet<string> _catalogNames;
        private static Explorer _namesFor;

        private static bool InCatalog(Explorer explorer, string prefab)
        {
            if (_namesFor != explorer || _catalogNames == null)
            {
                _namesFor = explorer;
                _catalogNames = new HashSet<string>(explorer.Catalog.Where(e => e.Kind != Kind.StatusEffect).Select(e => e.Name));
                PrefabIcons.Clear();
            }
            return _catalogNames.Contains(prefab);
        }

        /// <summary>The icon of an item or piece prefab by name, or null.</summary>
        private static Sprite PrefabIcon(string prefab)
        {
            if (PrefabIcons.TryGetValue(prefab, out var known)) return known;
            Sprite icon = null;
            var go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            if (go != null)
            {
                var icons = go.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
                if (icons != null && icons.Length > 0) icon = icons[0];
                else icon = go.GetComponent<Piece>()?.m_icon;
            }
            PrefabIcons[prefab] = icon;
            return icon;
        }

        private static void DrawSprite(Sprite sprite, Rect rect)
        {
            if (sprite == null || sprite.texture == null || Event.current.type != EventType.Repaint) return;
            try
            {
                var t = sprite.texture;
                var r = sprite.textureRect;
                GUI.DrawTextureWithTexCoords(rect, t, new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height), true);
            }
            catch
            {
                // Some sprites have no simple rectangle; the chip shows without its icon.
            }
        }

        // ----- Command -----

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

            y = SectionHeading(isItem ? "GIVE COMMAND" : "SPAWN COMMAND", width, y, null, "command");
            if (IsFolded("command")) return y;
            var rowH = U(28f);

            if (isItem)
            {
                GUI.Label(new Rect(0f, y, U(80f), rowH), "Amount", Skin.DimLabel);
                var x = U(80f);
                foreach (var step in new[] { -10, -1, 1, 10 })
                {
                    var text = step > 0 ? "+" + step : step.ToString(CultureInfo.InvariantCulture);
                    if (step == 1)
                    {
                        GUI.Label(new Rect(x, y, U(48f), rowH), _commandAmount.ToString(CultureInfo.InvariantCulture), Skin.Center);
                        x += U(52f);
                    }
                    if (GUI.Button(new Rect(x, y, U(40f), rowH), text, Skin.Segment)) _commandAmount = Mathf.Clamp(_commandAmount + step, 1, 999);
                    x += U(44f);
                }
                y += rowH + U(8f);

                var drop = (entry.Source as GameObject)?.GetComponent<ItemDrop>();
                var maxQuality = Mathf.Min(SpawnCommand.MaxItemQuality, drop?.m_itemData?.m_shared?.m_maxQuality ?? 1);
                if (maxQuality > 1)
                {
                    var names = Enumerable.Range(1, maxQuality).Select(q => q.ToString(CultureInfo.InvariantCulture)).ToList();
                    var chosen = Segments("Quality", names, _commandQuality - 1, width, U(80f), ref y);
                    if (chosen >= 0) _commandQuality = chosen + 1;
                }
            }

            var copyW = U(70f);
            var box = new Rect(0f, y, width - copyW - U(8f), U(30f));
            Skin.Box(box, new Color(0.055f, 0.060f, 0.073f, 1f), Skin.Outline);
            var commandRect = new Rect(box.x + U(10f), box.y, box.width - U(14f), box.height);
            if (!FitLabel(commandRect, command, Skin.Label, 10f) && commandRect.Contains(Event.current.mousePosition)) AskTip("command", command);
            if (GUI.Button(new Rect(box.xMax + U(8f), y, copyW, U(30f)), "Copy", Skin.Button))
            {
                GUIUtility.systemCopyBuffer = command;
                Session.Say($"Copied \"{command}\". Paste it into the console (F5).");
            }
            y += U(36f);

            const string note = "Runs in the console with devcommands on, which the game only allows the host or a single-player world.";
            var height = Skin.DimWrap.CalcHeight(new GUIContent(note), width);
            GUI.Label(new Rect(0f, y, width, height), note, Skin.DimWrap);
            return y + height + U(14f);
        }

        // ----- Details -----

        private static readonly Dictionary<Entry, string> ComponentLists = new Dictionary<Entry, string>();

        private static float Details(Explorer explorer, Entry entry, float width, float y)
        {
            y = SectionHeading("DETAILS", width, y, null, "details");
            if (IsFolded("details")) return y;

            var lines = new List<string> { "Prefab name: " + entry.Name };
            lines.Add("Origin: " + (entry.Origin == Origin.Vanilla ? "the game" : entry.Origin == Origin.Mod ? (entry.ModName.Length > 0 ? entry.ModName : "a mod, not named") : "unknown"));

            if (entry.ExtraLevels > 0) lines.Add($"Star looks: {entry.ExtraLevels}");



            if (entry.Source is GameObject prefab)
            {
                if (!ComponentLists.TryGetValue(entry, out var components))
                {
                    components = Components(prefab);
                    ComponentLists[entry] = components;
                }
                lines.Add("Made of: " + components);
            }

            foreach (var line in lines)
            {
                var height = Skin.DimWrap.CalcHeight(new GUIContent(line), width);
                GUI.Label(new Rect(0f, y, width, height), line, Skin.DimWrap);
                y += height + U(4f);
            }

            if (entry.Biomes.Length > 0)
            {
                y = ChipRow("Biomes (search)", entry.Biomes.Select(b => new KeyValuePair<string, Action>(Naming.FieldLabel(b), () => SearchFor(explorer, "biome:" + b.ToLowerInvariant()))), width, y);
            }
            if (entry.UsedBy.Count > 0 && EffectLinks.For(entry.Name).Count == 0)
            {
                var users = entry.UsedBy.Where(u => InCatalog(explorer, u)).Take(24).ToList();
                if (users.Count > 0) y = LinkRow(explorer, $"Played by ({entry.UsedBy.Count})", users, width, y);
            }

            // For finding out why part of a model does not show: every part the preview draws, in the log.
            if (Stage.Subject != null)
            {
                y += U(4f);
                var text = "Write its parts to the log";
                var w = Skin.Chip.CalcSize(new GUIContent(text)).x + U(8f);
                if (GUI.Button(new Rect(0f, y, Mathf.Min(width, w), U(26f)), text, Skin.Chip)) Session.Say(Stage.Dump());
                y += U(32f);
            }

            return y + U(6f);
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
            return string.Join(", ", counts.Select(p => p.Value > 1 ? $"{p.Key} ×{p.Value}" : p.Key));
        }

        // ----- Tooltips -----

        /// <summary>Asks for a tooltip at the mouse, shown once the mouse has rested on the same thing for a moment.</summary>
        private static void AskTip(string key, string text)
        {
            if (Event.current.type != EventType.Repaint || _drag != Drag.None || string.IsNullOrEmpty(text)) return;
            _askedTipKey = key;
            _askedTipText = text;
            _askedTipAt = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
        }

        private static void Tooltip()
        {
            if (Event.current.type != EventType.Repaint) return;

            if (_askedTipKey == null)
            {
                _tipKey = null;
                return;
            }

            if (_askedTipKey != _tipKey)
            {
                _tipKey = _askedTipKey;
                _tipSince = Time.unscaledTime;
            }
            if (Time.unscaledTime - _tipSince < TipDelay) return;

            var content = new GUIContent(_askedTipText);
            var maxW = U(420f);
            var width = Mathf.Min(maxW, Skin.Tip.CalcSize(content).x + U(2f));
            var height = Skin.Tip.CalcHeight(content, width);
            var x = Mathf.Min(_askedTipAt.x + U(16f), Screen.width - width - U(4f));
            var y = _askedTipAt.y + U(20f);
            if (y + height > Screen.height - U(4f)) y = _askedTipAt.y - height - U(8f);
            Skin.Tip.Draw(new Rect(x, y, width, height), content, false, false, false, false);
        }
    }
}
