using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The panel: search and kind chips across the top, the list on the left, the preview and
    /// everything that can be done with the selection on the right.
    ///
    /// Drawn with IMGUI in plain rectangles rather than automatic layout, which keeps a list of
    /// thousands of rows cheap: only the rows in view are drawn. Sizes are in design units scaled
    /// by the screen height and the PanelScale setting.
    /// </summary>
    internal static class ScryPanel
    {
        private const string SearchControl = "scry-search";
        private const string ParamControl = "scry-param-filter";

        private static Rect _win;
        private static bool _placed;
        private static float _s = 1f;

        private static Vector2 _listScroll;
        private static Vector2 _sideScroll;
        private static float _sideHeight;
        private static float _listHeight;
        private static int _rowsInView = 10;

        private static bool _focusSearch;
        private static bool _reveal;
        private static bool _details;
        private static string _paramFilter = "";

        private enum Drag { None, Move, Resize, Orbit }
        private static Drag _drag;
        private static bool _failed;

        /// <summary>Whether the search box has the keyboard, so a letter key does not close the panel.</summary>
        public static bool SearchFocused { get; private set; }

        public static void Opened()
        {
            Skin.LookForFontsAgain();
            _focusSearch = true;
            _reveal = true;
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

                Place();
                var explorer = Session.Explorer;
                Keys(explorer);
                if (!Session.IsOpen) return;

                Draw(explorer);
                Drags();

                // The panel is solid: clicks and the wheel over it stop here.
                var e = Event.current;
                if (_win.Contains(e.mousePosition) && (e.isMouse || e.type == EventType.ScrollWheel)) e.Use();

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
                if (!LoadRect())
                {
                    var w = Mathf.Min(U(1180f), Screen.width - U(40f));
                    var h = Mathf.Min(U(740f), Screen.height - U(40f));
                    _win = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
                }
            }

            var minW = Mathf.Min(U(820f), Screen.width);
            var minH = Mathf.Min(U(520f), Screen.height);
            _win.width = Mathf.Clamp(_win.width, minW, Screen.width);
            _win.height = Mathf.Clamp(_win.height, minH, Screen.height);
            _win.x = Mathf.Clamp(_win.x, 0f, Screen.width - _win.width);
            _win.y = Mathf.Clamp(_win.y, 0f, Screen.height - _win.height);
        }

        private static void Drags()
        {
            var e = Event.current;
            if (_drag == Drag.None) return;

            if (e.rawType == EventType.MouseUp)
            {
                if (_drag == Drag.Move || _drag == Drag.Resize) SaveRect();
                _drag = Drag.None;
                Stage.Dragging = false;
                return;
            }

            if (e.type != EventType.MouseDrag) return;

            switch (_drag)
            {
                case Drag.Move:
                    _win.position += e.delta;
                    break;
                case Drag.Resize:
                    _win.width += e.delta.x;
                    _win.height += e.delta.y;
                    break;
                case Drag.Orbit:
                    Stage.Orbit(e.delta);
                    break;
            }
            e.Use();
        }

        private static string RectFile => Path.Combine(Plugin.DataFolder, "panel.txt");

        private static bool LoadRect()
        {
            try
            {
                if (!File.Exists(RectFile)) return false;
                var parts = File.ReadAllText(RectFile).Split(' ');
                if (parts.Length != 4) return false;
                var v = parts.Select(p => float.Parse(p, CultureInfo.InvariantCulture)).ToArray();
                _win = new Rect(v[0], v[1], v[2], v[3]);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SaveRect()
        {
            try
            {
                Directory.CreateDirectory(Plugin.DataFolder);
                File.WriteAllText(RectFile, string.Join(" ", new[] { _win.x, _win.y, _win.width, _win.height }
                    .Select(v => Mathf.Round(v).ToString(CultureInfo.InvariantCulture))));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Scry could not remember the panel's place: {ex.Message}");
            }
        }

        // ----- Drawing -----

        private static void Draw(Explorer explorer)
        {
            Skin.Box(_win, Skin.Backdrop, Skin.Outline);

            GUI.BeginGroup(_win);
            var w = _win.width;
            var h = _win.height;
            var pad = U(16f);
            var e = Event.current;

            // Header: the name, the catalog size, and a close button. The header drags the panel.
            var header = new Rect(0f, 0f, w - U(56f), U(52f));
            GUI.Label(new Rect(pad, U(10f), U(90f), U(34f)), "Scry", Skin.Title);
            GUI.Label(new Rect(pad + U(86f), U(16f), U(300f), U(26f)), Session.CatalogSummary, Skin.Subtitle);
            if (GUI.Button(new Rect(w - pad - U(32f), U(12f), U(32f), U(32f)), "×", Skin.Close)) Session.Hide();
            if (e.type == EventType.MouseDown && e.button == 0 && header.Contains(e.mousePosition))
            {
                _drag = Drag.Move;
                e.Use();
            }

            var leftW = Mathf.Round((w - pad * 3f) * 0.40f);
            var rightX = pad * 2f + leftW;
            var rightW = w - rightX - pad;

            // Search.
            var y = U(56f);
            var search = new Rect(pad, y, leftW, U(36f));
            Search(explorer, search);

            // Chips: every kind with its count, then favourites and origin.
            var chipsBottom = Chips(explorer, new Rect(rightX, y, rightW, U(36f)));

            var bodyTop = Mathf.Max(search.yMax, chipsBottom) + U(12f);
            var footerH = U(28f);
            var bodyBottom = h - pad - footerH;
            List(explorer, new Rect(pad, bodyTop, leftW, bodyBottom - bodyTop));
            Side(explorer, new Rect(rightX, bodyTop, rightW, bodyBottom - bodyTop));

            Footer(new Rect(pad, h - pad - footerH + U(6f), w - pad * 2f, footerH));

            // Resize grip in the corner.
            var grip = new Rect(w - U(22f), h - U(22f), U(20f), U(20f));
            for (var i = 0; i < 3; i++)
            {
                var d = U(4f) * i;
                Skin.Box(new Rect(grip.xMax - U(6f) - d, grip.yMax - U(6f), U(3f), U(3f)), Skin.Faint);
                Skin.Box(new Rect(grip.xMax - U(6f), grip.yMax - U(6f) - d, U(3f), U(3f)), Skin.Faint);
            }
            if (e.type == EventType.MouseDown && e.button == 0 && grip.Contains(e.mousePosition))
            {
                _drag = Drag.Resize;
                e.Use();
            }

            GUI.EndGroup();
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

        private static float Chips(Explorer explorer, Rect rect)
        {
            var x = rect.x;
            var y = rect.y + U(3f);
            var chipH = U(30f);
            var gap = U(6f);

            void Chip(string text, bool on, Action act)
            {
                var width = Skin.Chip.CalcSize(new GUIContent(text)).x + U(8f);
                if (x + width > rect.xMax && x > rect.x)
                {
                    x = rect.x;
                    y += chipH + gap;
                }
                if (GUI.Button(new Rect(x, y, width, chipH), text, on ? Skin.ChipOn : Skin.Chip)) act();
                x += width + gap;
            }

            Chip($"All  {explorer.CountAll:N0}", explorer.KindFilter == null, () => Filter(explorer, null));
            foreach (Kind kind in Enum.GetValues(typeof(Kind)))
            {
                var count = explorer.CountOf(kind);
                if (count == 0 && explorer.KindFilter != kind) continue;
                var k = kind;
                Chip($"{Kinds.Label(kind)}  {count:N0}", explorer.KindFilter == kind, () => Filter(explorer, k));
            }

            Chip(explorer.FavouritesOnly ? "Favourites only" : "Favourites", explorer.FavouritesOnly,
                () => { explorer.FavouritesOnly = !explorer.FavouritesOnly; _listScroll = Vector2.zero; });

            var origin = explorer.Origin == OriginFilter.Vanilla ? "Game only" : explorer.Origin == OriginFilter.Mods ? "Mods only" : "Game and mods";
            Chip(origin, explorer.Origin != OriginFilter.All, () =>
            {
                explorer.Origin = (OriginFilter)(((int)explorer.Origin + 1) % 3);
                _listScroll = Vector2.zero;
            });

            return y + chipH;
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
            var text = note ?? "Arrows move through the list, Enter plays or shows, Ctrl+F searches, Esc closes.";
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
            _listHeight = inner.height;

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
            for (var i = first; i <= last; i++)
            {
                Row(explorer, results[i], new Rect(0f, i * rowH, view.width, rowH), i == explorer.SelectedIndex);
            }

            GUI.EndScrollView();
        }

        private static void Row(Explorer explorer, Entry entry, Rect rect, bool selected)
        {
            var e = Event.current;
            var hover = rect.Contains(e.mousePosition) && _drag == Drag.None;
            var inner = new Rect(rect.x + U(2f), rect.y + U(1f), rect.width - U(4f), rect.height - U(2f));

            if (selected) Skin.Box(inner, Skin.AccentSoft);
            else if (hover) Skin.Box(inner, Skin.Hover);
            if (selected) Skin.Box(new Rect(inner.x, inner.y + U(8f), U(3f), inner.height - U(16f)), Skin.Accent);

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
            var nameW = Mathf.Min(textW, nameStyle.CalcSize(new GUIContent(primary)).x);
            GUI.Label(new Rect(textX, inner.y, nameW, inner.height), primary, nameStyle);
            nameStyle.normal.textColor = was;

            if (secondary.Length > 0 && textW - nameW > U(40f))
            {
                GUI.Label(new Rect(textX + nameW + U(8f), inner.y + U(1f), textW - nameW - U(8f), inner.height), secondary, Skin.RowSub);
            }

            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
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
            Skin.Box(rect, new Color(color.r, color.g, color.b, 0.18f));
            var style = Skin.Glyph;
            var was = style.normal.textColor;
            style.normal.textColor = color;
            GUI.Label(rect, Skin.KindMark(entry.Kind), style);
            style.normal.textColor = was;
        }

        // ----- The selection -----

        private static void Side(Explorer explorer, Rect rect)
        {
            var entry = explorer.Selected;
            if (entry == null)
            {
                Skin.Box(rect, Skin.Panel);
                var middle = new Rect(rect.x + U(40f), rect.y + rect.height / 2f - U(40f), rect.width - U(80f), U(80f));
                GUI.Label(new Rect(middle.x, middle.y, middle.width, U(30f)), "Pick something from the list", Skin.Center);
                GUI.Label(new Rect(middle.x, middle.y + U(32f), middle.width, U(44f)),
                    "Type to search, use the arrow keys to move, and Enter to play or show it.", Skin.CenterDim);
                return;
            }

            var stageH = Mathf.Round(Mathf.Min(rect.width * 0.60f, rect.height * 0.52f));
            StageArea(entry, new Rect(rect.x, rect.y, rect.width, stageH));

            var below = new Rect(rect.x, rect.y + stageH + U(12f), rect.width, rect.height - stageH - U(12f));
            var content = new Rect(0f, 0f, below.width - U(14f), Mathf.Max(_sideHeight, below.height));
            _sideScroll = GUI.BeginScrollView(below, _sideScroll, content, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);

            var cw = content.width;
            var y = 0f;
            y = Title(explorer, entry, cw, y);
            y = Actions(entry, cw, y);
            y = Adjust(explorer, entry, cw, y);
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

            // The kind, in its colour, in the corner.
            var color = Skin.KindColor(entry.Kind);
            var label = entry.Kind == Kind.StatusEffect ? "Status effect" : Kinds.Label(entry.Kind).TrimEnd('s');
            var width = Skin.Glyph.CalcSize(new GUIContent(label)).x + U(20f);
            var badge = new Rect(rect.x + U(10f), rect.y + U(10f), width, U(22f));
            Skin.PillBox(badge, new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.85f));
            var style = Skin.Glyph;
            var was = style.normal.textColor;
            style.normal.textColor = color;
            GUI.Label(badge, label, style);
            style.normal.textColor = was;
        }

        private static readonly Dictionary<Entry, string> SoundFacts = new Dictionary<Entry, string>();

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

            if (!SoundFacts.TryGetValue(entry, out var facts))
            {
                facts = DescribeSound(entry.Source as GameObject);
                SoundFacts[entry] = facts;
            }

            GUI.Label(new Rect(rect.x + U(20f), mid + U(64f), rect.width - U(40f), U(26f)), facts, Skin.CenterDim);
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

        private static void StatusCard(Entry entry, Rect rect)
        {
            var effect = entry.Source as StatusEffect;
            var iconSize = U(84f);
            var icon = new Rect(rect.x + (rect.width - iconSize) / 2f, rect.y + U(34f), iconSize, iconSize);
            if (effect != null && effect.m_icon != null) DrawIcon(entry, icon);
            else DrawIcon(new Entry { Kind = Kind.StatusEffect }, icon);

            var y = icon.yMax + U(12f);
            if (effect == null) return;

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(effect.m_category)) lines.Add("Category: " + effect.m_category);
            lines.Add(effect.m_ttl > 0f ? "Lasts " + Duration(effect.m_ttl) : "No time limit of its own");
            GUI.Label(new Rect(rect.x + U(20f), y, rect.width - U(40f), U(22f)), string.Join("    ", lines), Skin.CenterDim);

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
            GUI.Label(new Rect(0f, y, width - U(150f), U(30f)), primary, Skin.Big);

            var favourite = explorer.Favourites.Contains(entry);
            var starRect = new Rect(width - U(28f), y + U(4f), U(22f), U(22f));
            Skin.Icon(starRect, favourite ? Skin.Star : Skin.StarHollow, favourite ? Skin.Accent : Skin.Dim);
            if (GUI.Button(starRect, GUIContent.none, GUIStyle.none)) explorer.ToggleFavourite(entry);

            if (GUI.Button(new Rect(width - U(142f), y + U(2f), U(104f), U(26f)), "Copy name", Skin.Button))
            {
                GUIUtility.systemCopyBuffer = entry.Name;
                Session.Say($"Copied \"{entry.Name}\".");
            }
            y += U(32f);

            var origin = entry.Origin == Origin.Vanilla ? "from the game" : entry.Origin == Origin.Mod ? "added by a mod" : "";
            var sub = entry.Name == primary ? origin : entry.Name + (origin.Length > 0 ? "   ·   " + origin : "");
            if (sub.Length > 0)
            {
                GUI.Label(new Rect(0f, y, width, U(20f)), sub, Skin.DimLabel);
                y += U(24f);
            }

            return y + U(8f);
        }

        // ----- Actions -----

        private static float Actions(Entry entry, float width, float y)
        {
            var x = 0f;
            var rowH = U(32f);

            bool Button(string text, GUIStyle style)
            {
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

            switch (entry.Kind)
            {
                case Kind.Sound:
                    if (Button("Play", Skin.Primary)) Previews.PlaySound(entry);
                    if (Previews.SoundPlaying && Button("Stop", Skin.Button)) Previews.StopSound();
                    if (Button(Previews.Loop ? "Repeat on" : "Repeat off", Previews.Loop ? Skin.On : Skin.Button)) Previews.Loop = !Previews.Loop;
                    break;

                case Kind.Effect:
                    if (Button("Play where you look", Skin.Primary)) Previews.PlayEffect(entry, onYou: false);
                    if (Button("Play on you", Skin.Button)) Previews.PlayEffect(entry, onYou: true);
                    if (Button("Replay", Skin.Button)) Previews.Replay();
                    if (Button(Previews.Loop ? "Repeat on" : "Repeat off", Previews.Loop ? Skin.On : Skin.Button)) Previews.Loop = !Previews.Loop;
                    break;

                case Kind.StatusEffect:
                    if (Previews.StatusShowing)
                    {
                        if (Button("Take it off you", Skin.Primary)) Previews.StopStatus(true);
                    }
                    else if (Button("Show it on you", Skin.Primary))
                    {
                        Previews.ShowStatus(entry);
                        if (!Previews.StatusShowing) Session.Say("This status effect has no visuals of its own.");
                    }
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

            y += rowH;

            string note = null;
            if (entry.Kind == Kind.StatusEffect) note = "Only the look. The effect itself is never applied to you.";
            else if (Previews.InWorld) note = "Only you can see it, and it is gone when you leave the world. Close the panel to walk around it.";
            if (note != null)
            {
                GUI.Label(new Rect(0f, y + U(6f), width, U(20f)), note, Skin.FaintLabel);
                y += U(26f);
            }

            return y + U(14f);
        }

        // ----- Modifiers -----

        private static float Adjust(Explorer explorer, Entry entry, float width, float y)
        {
            var modifiers = explorer.Modifiers;
            var staged = Stage.IsStaged(entry);
            var projectile = entry.Kind == Kind.Projectile;
            var animator = staged ? Previews.StageAnimator() : null;

            if (!staged && !projectile) return y;

            y = SectionHeading("ADJUST", width, y, () => modifiers.Reset());
            var labelW = U(120f);

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
                var speed = SliderRow("Speed", $"{Mathf.RoundToInt(Previews.ProjectileSpeed)} m/s", Previews.ProjectileSpeed, 5f, 120f, width, labelW, ref y);
                Previews.ProjectileSpeed = speed;
            }

            if (animator != null)
            {
                var speed = SliderRow("Animation speed", $"×{modifiers.AnimationSpeed.ToString("0.0", CultureInfo.InvariantCulture)}", modifiers.AnimationSpeed, 0f, Modifiers.MaxAnimationSpeed, width, labelW, ref y);
                if (!Mathf.Approximately(speed, modifiers.AnimationSpeed)) modifiers.AnimationSpeed = speed;
                y = Parameters(animator, width, y);
            }

            return y + U(10f);
        }

        private static float SectionHeading(string text, float width, float y, Action reset)
        {
            Skin.Box(new Rect(0f, y + U(10f), width, U(1f)), Skin.Outline);
            var textW = Skin.Heading.CalcSize(new GUIContent(text)).x;
            GUI.Label(new Rect(0f, y, textW + U(10f), U(20f)), text, Skin.Heading);
            if (reset != null && GUI.Button(new Rect(width - U(64f), y - U(2f), U(64f), U(24f)), "Reset", Skin.Chip)) reset();
            return y + U(28f);
        }

        private static float SliderRow(string label, string value, float current, float min, float max, float width, float labelW, ref float y)
        {
            var rowH = U(26f);
            GUI.Label(new Rect(0f, y, labelW, rowH), label, Skin.DimLabel);
            var valueW = U(72f);
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
                if (GUI.Button(new Rect(x, y, w, rowH), names[i], style)) chosen = i;
                x += w + U(4f);
            }
            y += rowH + U(8f);
            return chosen;
        }

        private static Animator _paramsFor;
        private static AnimatorControllerParameter[] _params = new AnimatorControllerParameter[0];

        /// <summary>
        /// What the creature's animator can be told: triggers play once (attacks, hits, emotes),
        /// switches hold a state (blocking, sleeping), and numbers blend (walking and running speed).
        /// </summary>
        private static float Parameters(Animator animator, float width, float y)
        {
            if (animator != _paramsFor)
            {
                _paramsFor = animator;
                try
                {
                    _params = animator.parameters;
                }
                catch
                {
                    _params = new AnimatorControllerParameter[0];
                }
            }

            if (_params.Length == 0) return y;

            y = SectionHeading("ANIMATIONS", width, y, null);

            if (_params.Length > 12)
            {
                var field = new Rect(0f, y, Mathf.Min(width, U(280f)), U(28f));
                GUI.SetNextControlName(ParamControl);
                _paramFilter = GUI.TextField(field, _paramFilter, 40, Skin.Field);
                if (string.IsNullOrEmpty(_paramFilter)) GUI.Label(field, "Filter animations", Skin.Placeholder);
                y += U(36f);
            }

            var shown = _params.Where(p => _paramFilter.Length == 0 || p.name.IndexOf(_paramFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            var x = 0f;
            var rowH = U(26f);
            foreach (var p in shown.Where(p => p.type == AnimatorControllerParameterType.Trigger))
            {
                var w = Skin.Chip.CalcSize(new GUIContent(p.name)).x + U(8f);
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                if (GUI.Button(new Rect(x, y, w, rowH), p.name, Skin.Chip)) Previews.Trigger(p.name);
                x += w + U(5f);
            }
            if (x > 0f) y += rowH + U(10f);

            x = 0f;
            foreach (var p in shown.Where(p => p.type == AnimatorControllerParameterType.Bool))
            {
                var on = animator.GetBool(p.name);
                var w = Skin.Chip.CalcSize(new GUIContent(p.name)).x + U(8f);
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                if (GUI.Button(new Rect(x, y, w, rowH), p.name, on ? Skin.ChipOn : Skin.Chip)) Previews.SetBool(p.name, !on);
                x += w + U(5f);
            }
            if (x > 0f) y += rowH + U(10f);

            var labelW = U(160f);
            foreach (var p in shown.Where(p => p.type == AnimatorControllerParameterType.Float))
            {
                var current = animator.GetFloat(p.name);
                var value = SliderRow(p.name, current.ToString("0.0", CultureInfo.InvariantCulture), current, 0f, 10f, width, labelW, ref y);
                if (!Mathf.Approximately(value, current)) Previews.SetFloat(p.name, value);
            }

            return y;
        }

        // ----- Details -----

        private static readonly Dictionary<Entry, string> ComponentLists = new Dictionary<Entry, string>();

        private static float Details(Entry entry, float width, float y)
        {
            Skin.Box(new Rect(0f, y + U(10f), width, U(1f)), Skin.Outline);
            if (GUI.Button(new Rect(0f, y, U(130f), U(22f)), _details ? "HIDE DETAILS" : "SHOW DETAILS", Skin.Heading)) _details = !_details;
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
    }
}
