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
        private static bool _details;
        private static string _clipFilter = "";

        private enum Drag { None, Move, Resize, Orbit }
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
            _focusSearch = true;
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
                Keys(explorer);
                if (!Session.IsOpen) return;

                Draw(explorer);
                Drags();
                Tooltip();

                // The panel is solid: clicks and the wheel over it stop here.
                var e = Event.current;
                if (Win.Contains(e.mousePosition) && (e.isMouse || e.type == EventType.ScrollWheel)) e.Use();

                SearchFocused = GUI.GetNameOfFocusedControl() == SearchControl;
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
                var cw = Mathf.Min(U(430f), Screen.width - U(40f));
                _compactRect = new Rect(Screen.width - cw - U(20f), U(40f), cw, Screen.height - U(80f));
                LoadRects();
            }

            var win = Win;
            var minW = Mathf.Min(U(_compact ? 360f : 820f), Screen.width);
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
                if (_drag == Drag.Move || _drag == Drag.Resize) SaveRects();
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
                File.WriteAllLines(RectFile, new[] { Line("full", _full), Line("compact", _compactRect), "view " + (_compact ? "compact" : "full") });
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
            var header = new Rect(0f, 0f, w - U(56f) - viewW, U(52f));
            GUI.Label(new Rect(pad, U(10f), U(90f), U(34f)), "Scry", Skin.Title);
            if (!_compact) GUI.Label(new Rect(pad + U(86f), U(16f), U(300f), U(26f)), Session.CatalogSummary, Skin.Subtitle);
            if (GUI.Button(new Rect(w - pad - U(40f) - viewW, U(15f), viewW, U(28f)), viewText, Skin.Button)) ToggleCompact();
            if (GUI.Button(new Rect(w - pad - U(32f), U(12f), U(32f), U(32f)), "×", Skin.Close)) Session.Hide();
            if (e.type == EventType.MouseDown && e.button == 0 && header.Contains(e.mousePosition))
            {
                _drag = Drag.Move;
                e.Use();
            }

            // Search, favourites and origin, then the kind tabs, across the whole width.
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

            var search = new Rect(rect.x, rect.y, rect.width - originW - starW - gap * 2f, rect.height);
            Search(explorer, search);

            var star = new Rect(search.xMax + gap, rect.y, starW, rect.height);
            if (GUI.Button(star, GUIContent.none, explorer.FavouritesOnly ? Skin.On : Skin.IconButton))
            {
                explorer.FavouritesOnly = !explorer.FavouritesOnly;
                _listScroll = Vector2.zero;
            }
            var icon = new Rect(star.x + star.width * 0.22f, star.y + star.height * 0.22f, star.width * 0.56f, star.height * 0.56f);
            Skin.Icon(icon, explorer.FavouritesOnly ? Skin.Star : Skin.StarHollow, explorer.FavouritesOnly ? Skin.Accent : Skin.Dim);
            if (star.Contains(Event.current.mousePosition)) AskTip("fav", explorer.FavouritesOnly ? "Showing only favourites" : "Show only favourites");

            var x = star.xMax + gap;
            for (var i = 0; i < names.Length; i++)
            {
                var on = (int)explorer.Origin == i;
                if (GUI.Button(new Rect(x, rect.y, widths[i], rect.height), names[i], on ? Skin.SegmentOn : Skin.Segment))
                {
                    explorer.Origin = (OriginFilter)i;
                    _listScroll = Vector2.zero;
                }
                x += widths[i] + U(4f);
            }
            var originRect = new Rect(star.xMax + gap, rect.y, originW, rect.height);
            if (originRect.Contains(Event.current.mousePosition)) AskTip("origin", "Everything, only the game's own, or only what mods added");

            return rect.yMax;
        }

        private static void Search(Explorer explorer, Rect rect)
        {
            GUI.SetNextControlName(SearchControl);
            var text = GUI.TextField(rect, explorer.Text, 80, Skin.Field);
            if (text != explorer.Text)
            {
                explorer.Text = text;
                _listScroll = Vector2.zero;
                _reveal = true;
            }

            if (string.IsNullOrEmpty(explorer.Text))
            {
                GUI.Label(rect, "Search by name", Skin.Placeholder);
            }
            else if (GUI.Button(new Rect(rect.xMax - U(30f), rect.y + U(5f), U(26f), rect.height - U(10f)), "×", Skin.Close))
            {
                explorer.Text = "";
                _focusSearch = true;
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
            _reveal = true;
        }

        private static void Footer(Rect rect)
        {
            var note = Session.Note;
            var text = note ?? (_compact
                ? "Hold right mouse outside the panel to look around. Esc closes."
                : "Arrows move, Enter plays or shows, Ctrl+F searches. Hold right mouse outside the panel to look around. Esc closes.");
            GUI.Label(new Rect(rect.x, rect.y, rect.width - U(30f), rect.height), text, note != null ? Skin.DimLabel : Skin.FaintLabel);
        }

        // ----- The list -----

        private static void List(Explorer explorer, Rect rect)
        {
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
                var stageH = Mathf.Round(Mathf.Min(rect.width * 0.60f, rect.height * 0.50f));
                StageArea(entry, new Rect(rect.x, rect.y, rect.width, stageH));
                top += stageH + U(12f);
            }

            var below = new Rect(rect.x, top, rect.width, rect.yMax - top);
            var content = new Rect(0f, 0f, below.width - U(14f), Mathf.Max(_sideHeight, below.height));
            _sideScroll = GUI.BeginScrollView(below, _sideScroll, content, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);

            var cw = content.width;
            var y = 0f;
            y = Title(explorer, entry, cw, y);
            if (!withStage && (entry.Kind == Kind.Sound || entry.Kind == Kind.StatusEffect)) y = CompactCard(entry, cw, y);
            y = Actions(entry, cw, y);
            if (entry.Kind == Kind.Sound) y = Variants(entry, cw, y);
            y = Adjust(explorer, entry, cw, y, withStage);
            y = Details(entry, cw, y);
            if (Event.current.type == EventType.Repaint) _sideHeight = y + U(8f);

            GUI.EndScrollView();
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

                if (rect.Contains(e.mousePosition) || _drag == Drag.Orbit)
                {
                    GUI.Label(new Rect(inner.x + U(12f), inner.yMax - U(28f), inner.width - U(24f), U(22f)),
                        "Drag to turn, scroll to zoom, double-click to reset", Skin.FaintLabel);
                }

                if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                {
                    if (e.clickCount == 2) Stage.ResetView();
                    _drag = Drag.Orbit;
                    Stage.Dragging = true;
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
            var playing = Previews.SoundPlaying;
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
            var origin = entry.Origin == Origin.Vanilla ? "from the game" : entry.Origin == Origin.Mod ? "added by a mod" : "";
            var sub = entry.Name == primary ? origin : entry.Name + (origin.Length > 0 ? "   ·   " + origin : "");
            if (sub.Length > 0 || _compact)
            {
                var subRect = new Rect(x, y, width - x, U(22f));
                if (!FitLabel(subRect, sub, Skin.DimLabel, 10f) && subRect.Contains(Event.current.mousePosition)) AskTip("sub", sub);
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
                    if (Previews.SoundPlaying && Button("Stop", Skin.Button)) Previews.StopSound();
                    if (Button(Previews.LoopSounds ? "Repeat on" : "Repeat off", Previews.LoopSounds ? Skin.On : Skin.Button)) Previews.LoopSounds = !Previews.LoopSounds;
                    break;

                case Kind.Effect:
                    if (Button("Play where you look", Skin.Primary)) Previews.PlayEffect(entry, onYou: false);
                    if (Button("Play on you", Skin.Button)) Previews.PlayEffect(entry, onYou: true);
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
                    if (Previews.IsModel(entry))
                    {
                        if (Button(Previews.InWorld ? "Showing in the world" : "Show in the world", Previews.InWorld ? Skin.On : (entry.Kind == Kind.Projectile ? Skin.Button : Skin.Primary))) Previews.ToggleWorld();
                        if (Previews.InWorld)
                        {
                            if (Button("Move to where you look", Skin.Button)) Previews.PlaceHere();
                            if (Button("Pin", Skin.Button))
                            {
                                Previews.Pin();
                                Session.Say("Pinned. It stays where it is until you clear the world.");
                            }
                        }
                    }
                    break;
            }

            if (Previews.AnythingInWorld && Button(Previews.PinnedCount > 0 ? $"Clear the world ({Previews.PinnedCount} pinned)" : "Clear the world", Skin.Button))
            {
                Previews.ClearWorld();
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
                if (button("Play " + list.Key.ToLowerInvariant(), hasStart ? Skin.Button : Skin.Primary)) Previews.PlayOnYou(list.Value);
            }

            return "Only the look. The effect itself is never applied to you.";
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

            y = SectionHeading($"VARIANTS  {clips.Count}", width, y, null);
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

            y = SectionHeading("ADJUST", width, y, () => modifiers.Reset());
            var labelW = U(_compact ? 100f : 120f);

            if (staged)
            {
                // Size on a curve, so the range from a tenth to ten times is usable end to end.
                var logScale = Mathf.Log10(modifiers.Scale);
                var picked = SliderRow("Size", $"×{modifiers.Scale.ToString("0.00", CultureInfo.InvariantCulture)}", logScale, -1f, 1f, width, labelW, ref y);
                if (!Mathf.Approximately(picked, logScale)) modifiers.Scale = Mathf.Pow(10f, picked);
            }

            if (entry.Kind == Kind.Creature && modifiers.MaxLevel > 1)
            {
                var names = new List<string>();
                for (var level = 1; level <= modifiers.MaxLevel; level++) names.Add(level == 1 ? "No stars" : level == 2 ? "1 star" : $"{level - 1} stars");
                var chosen = Segments("Level", names, modifiers.Level - 1, width, labelW, ref y);
                if (chosen >= 0) modifiers.Level = chosen + 1;
            }

            if (modifiers.WearAvailable)
            {
                var chosen = Segments("Wear", new List<string> { "New", "Worn", "Broken" }, (int)modifiers.Wear, width, labelW, ref y);
                if (chosen >= 0) modifiers.Wear = (Wear)chosen;
            }

            if (projectile)
            {
                Previews.ProjectileSpeed = SliderRow("Speed", $"{Mathf.RoundToInt(Previews.ProjectileSpeed)} m/s", Previews.ProjectileSpeed, 5f, 120f, width, labelW, ref y);
            }

            if (clips.Count > 0)
            {
                var speed = SliderRow("Animation speed", $"×{modifiers.AnimationSpeed.ToString("0.0", CultureInfo.InvariantCulture)}", modifiers.AnimationSpeed, 0f, Modifiers.MaxAnimationSpeed, width, labelW, ref y);
                if (!Mathf.Approximately(speed, modifiers.AnimationSpeed)) modifiers.AnimationSpeed = speed;
                y = Clips(clips, width, y);
            }

            return y + U(10f);
        }

        private static float SectionHeading(string text, float width, float y, Action reset)
        {
            var textW = Skin.Heading.CalcSize(new GUIContent(text)).x;
            GUI.Label(new Rect(0f, y, textW + U(4f), U(20f)), text, Skin.Heading);
            var lineEnd = reset != null ? width - U(74f) : width;
            Skin.Fill(new Rect(textW + U(12f), y + U(10f), Mathf.Max(0f, lineEnd - textW - U(12f)), U(1f)), Skin.Outline);
            if (reset != null && GUI.Button(new Rect(width - U(64f), y - U(2f), U(64f), U(24f)), "Reset", Skin.Chip)) reset();
            return y + U(30f);
        }

        private static float SliderRow(string label, string value, float current, float min, float max, float width, float labelW, ref float y)
        {
            var rowH = U(26f);
            GUI.Label(new Rect(0f, y, labelW, rowH), label, Skin.DimLabel);
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
            GUI.Label(new Rect(0f, y, labelW, rowH), label, Skin.DimLabel);
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
        /// Every animation clip the creature has, each played directly on the copy. The one playing
        /// is lit; Stop hands the copy back to its own animations.
        /// </summary>
        private static float Clips(List<AnimationClip> clips, float width, float y)
        {
            var playing = Previews.PlayingClip();
            y = SectionHeading("ANIMATIONS", width, y, null);

            var x = 0f;
            var rowH = U(26f);

            if (GUI.Button(new Rect(x, y, U(84f), rowH), Previews.LoopClips ? "Repeat on" : "Repeat off", Previews.LoopClips ? Skin.ChipOn : Skin.Chip)) Previews.ToggleLoopClips();
            x += U(90f);
            if (playing != null && GUI.Button(new Rect(x, y, U(60f), rowH), "Stop", Skin.Chip)) Previews.StopClip();
            x += U(66f);

            if (clips.Count > 12)
            {
                var field = new Rect(x, y - U(1f), Mathf.Max(U(120f), Mathf.Min(width - x, U(260f))), U(28f));
                GUI.SetNextControlName(ClipControl);
                _clipFilter = GUI.TextField(field, _clipFilter, 40, Skin.Field);
                if (string.IsNullOrEmpty(_clipFilter)) GUI.Label(field, "Filter", Skin.Placeholder);
            }
            y += rowH + U(10f);

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
                if (GUI.Button(chip, clip.name, style)) Previews.PlayClip(clip);
                if (chip.Contains(Event.current.mousePosition))
                {
                    AskTip("clip:" + clip.name, $"{clip.name}\n{clip.length.ToString("0.0", CultureInfo.InvariantCulture)} s{(clip.isLooping ? ", loops" : "")}");
                }
                x += w + U(5f);
            }
            if (x > 0f) y += rowH;

            return y + U(10f);
        }

        // ----- Details -----

        private static readonly Dictionary<Entry, string> ComponentLists = new Dictionary<Entry, string>();

        private static float Details(Entry entry, float width, float y)
        {
            var label = _details ? "HIDE DETAILS" : "SHOW DETAILS";
            var labelW = Skin.Heading.CalcSize(new GUIContent(label)).x;
            if (GUI.Button(new Rect(0f, y, labelW + U(4f), U(20f)), label, Skin.Heading)) _details = !_details;
            Skin.Fill(new Rect(labelW + U(12f), y + U(10f), Mathf.Max(0f, width - labelW - U(12f)), U(1f)), Skin.Outline);
            y += U(28f);
            if (!_details) return y;

            var lines = new List<string> { "Prefab name: " + entry.Name };
            lines.Add("Origin: " + (entry.Origin == Origin.Vanilla ? "the game" : entry.Origin == Origin.Mod ? "added by a mod" : "unknown"));
            if (entry.ExtraLevels > 0) lines.Add($"Star looks: {entry.ExtraLevels}");

            if (entry.UsedBy.Count > 0)
            {
                var users = entry.UsedBy.Take(8).ToList();
                var more = entry.UsedBy.Count > users.Count ? $" and {entry.UsedBy.Count - users.Count} more" : "";
                lines.Add("Used by: " + string.Join(", ", users) + more);
            }

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
