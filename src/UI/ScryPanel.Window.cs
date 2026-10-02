using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The panel's window: its place and size in each view, dragging them, and keeping them between games.</summary>
    internal static partial class ScryPanel
    {
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

        /// <summary>Folds the list away or brings it back, in the view in use.</summary>
        private static void ToggleList()
        {
            ListHidden = !ListHidden;
            _reveal = true;
            SaveRects();
        }

        /// <summary>
        /// The gap between the list and the details: a grip to drag it (past the smallest the list
        /// folds away), a double-click to fold it. While folded, the gap is a button along its
        /// whole length that brings the list back. Across the full view it runs top to bottom;
        /// in the compact view, where the list is above the details, left to right.
        /// </summary>
        private static void ListDivider(Rect gap, bool upright)
        {
            var e = Event.current;
            var inside = gap.Contains(e.mousePosition);

            if (ListHidden)
            {
                if (GUI.Button(gap, upright ? "\u203A" : "Show the list  \u25BE", Skin.Button)) ToggleList();
                if (inside) AskTip("list-gap", "Bring the list back");
                return;
            }

            var hover = inside || _drag == Drag.ListSize;
            var grip = upright
                ? new Rect(gap.center.x - U(2f), gap.center.y - U(24f), U(4f), U(48f))
                : new Rect(gap.center.x - U(24f), gap.center.y - U(2f), U(48f), U(4f));
            Skin.Fill(grip, hover ? Skin.Dim : Skin.Outline);
            if (inside) AskTip("list-gap", "Drag to make the list bigger or smaller, past the smallest to fold it away; double-click to fold it");

            if (e.type != EventType.MouseDown || e.button != 0 || !inside) return;
            if (e.clickCount == 2)
            {
                ToggleList();
            }
            else
            {
                _listDragged = ListShare;
                _drag = Drag.ListSize;
            }
            e.Use();
        }

        private static void Drags()
        {
            var e = Event.current;
            if (_drag == Drag.None) return;

            if (e.rawType == EventType.MouseUp)
            {
                if (_drag == Drag.Move || _drag == Drag.Resize || _drag == Drag.StageSize || _drag == Drag.ListSize) SaveRects();
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
                    _orbitMoved += e.delta.magnitude;
                    Stage.Orbit(e.delta);
                    break;
                case Drag.Pan:
                    // What was under the pointer goes with it.
                    var now = GUIUtility.GUIToScreenPoint(e.mousePosition);
                    Stage.Pan(OnPicture(now - e.delta), OnPicture(now));
                    break;
                case Drag.Ruler:
                    RulerAt(GUIUtility.GUIToScreenPoint(e.mousePosition).y);
                    break;
                case Drag.StageSize:
                    _stageScale = Mathf.Clamp(_stageScale + e.delta.y / Mathf.Max(1f, _stageBaseH), 0.4f, 2.4f);
                    break;
                case Drag.ListSize:
                    // Past its narrowest the list folds away; dragged back out, it opens again.
                    _listDragged += (_compact ? e.delta.y : e.delta.x) / Mathf.Max(1f, _listSpan);
                    ListHidden = _listDragged < ListFolds;
                    if (!ListHidden) ListShare = _listDragged;
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
                    if (parts.Length == 2 && parts[0] == "creatures") Stage.CreaturesShown = parts[1] == "1";
                    if (parts.Length == 2 && parts[0] == "stage" && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var stage)) _stageScale = Mathf.Clamp(stage, 0.4f, 2.4f);
                    if (parts.Length == 2 && parts[0] == "list" && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var share)) _listShareFull = Mathf.Clamp(share, ListNarrowest, ListWidest);
                    if (parts.Length == 2 && parts[0] == "listhidden") _listHiddenFull = parts[1] == "1";
                    if (parts.Length == 2 && parts[0] == "clist" && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var cshare)) _listShareCompact = Mathf.Clamp(cshare, ListNarrowest, ListWidest);
                    if (parts.Length == 2 && parts[0] == "clisthidden") _listHiddenCompact = parts[1] == "1";
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
                    "spin " + (Stage.Spin ? "1" : "0"), "creatures " + (Stage.CreaturesShown ? "1" : "0"), "folded " + string.Join(" ", Folded),
                    "stage " + _stageScale.ToString("0.00", CultureInfo.InvariantCulture),
                    "list " + _listShareFull.ToString("0.00", CultureInfo.InvariantCulture), "listhidden " + (_listHiddenFull ? "1" : "0"),
                    "clist " + _listShareCompact.ToString("0.00", CultureInfo.InvariantCulture), "clisthidden " + (_listHiddenCompact ? "1" : "0") });
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Scry could not remember the panel's place: {ex.Message}");
            }
        }
    }
}
