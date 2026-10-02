using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// How the stage is seen, set and mostly left: whether the model turns, the backdrop, the
    /// lighting and a person beside it for size, in a box opened from the stage's View chip. A
    /// row is clicked to switch it, a backdrop or lighting row going on to the next. The box takes
    /// the mouse before anything under it and is drawn over everything; Escape, a click elsewhere
    /// or another selection closes it.
    /// </summary>
    internal static partial class ScryPanel
    {
        private static bool _viewOpen;
        private static Entry _viewFor;

        /// <summary>Where the View chip and its box are, on the screen.</summary>
        private static Rect _viewChip, _viewBox;

        /// <summary>Whether the View box is open, for the self-test.</summary>
        public static bool ViewMenuOpen => _viewOpen;

        /// <summary>The rows of the box: what each says, its value, whether it is on, what it does.</summary>
        private static List<(string Name, string Value, bool On, string Tip, System.Action Act)> ViewRows(Entry entry)
        {
            var rows = new List<(string, string, bool, string, System.Action)>
            {
                ("Spin", Stage.Spin ? "On" : "Off", Stage.Spin, Stage.Spin ? "Turning; click to hold it still" : "Held still; click to turn it", () => Stage.Spin = !Stage.Spin),
                ("Backdrop", Stage.BackdropNames[Stage.BackdropIndex], false, "Click for the next backdrop", () => Stage.BackdropIndex = (Stage.BackdropIndex + 1) % Stage.BackdropNames.Length),
                ("Light", Stage.LightingNames[Stage.LightingIndex], false, "Click for the next lighting", () => Stage.LightingIndex = (Stage.LightingIndex + 1) % Stage.LightingNames.Length),
            };
            if (!Looks.IsWorn(entry)) rows.Add(("Person", Stage.ShowPerson ? "On" : "Off", Stage.ShowPerson, "A person beside it, to judge its size", () => Stage.ShowPerson = !Stage.ShowPerson));
            return rows;
        }

        private static float ViewRowHeight => U(28f);

        /// <summary>Opens or closes the box under the View chip, its right edge on the chip's.</summary>
        private static void ViewToggle(Entry entry, Rect chipScreen)
        {
            if (_viewOpen && _viewFor == entry)
            {
                _viewOpen = false;
                return;
            }
            _viewOpen = true;
            _viewFor = entry;
            ViewPlace(entry, chipScreen);
        }

        private static void ViewPlace(Entry entry, Rect chipScreen)
        {
            _viewChip = chipScreen;
            var w = ViewNameWidth() + ViewValueWidth() + U(8f) + U(8f) + U(16f) + U(6f);
            var h = ViewRows(entry).Count * ViewRowHeight + U(8f);
            _viewBox = new Rect(chipScreen.xMax - w, chipScreen.yMax + U(4f), w, h);
        }

        /// <summary>
        /// How wide the box's names and values are at the most: every name, and every value a row
        /// can take, so the box neither changes width as a row is switched nor lets a long value
        /// such as "Sky and ground" run over its name.
        /// </summary>
        private static float ViewNameWidth()
        {
            var most = 0f;
            foreach (var name in new[] { "Spin", "Backdrop", "Light", "Person" }) most = Mathf.Max(most, Skin.Width(Skin.Label, name));
            return most;
        }

        private static float ViewValueWidth()
        {
            var most = Mathf.Max(Skin.Width(Skin.Chip, "On"), Skin.Width(Skin.ChipOn, "Off"));
            foreach (var value in Stage.BackdropNames) most = Mathf.Max(most, Skin.Width(Skin.Chip, value));
            foreach (var value in Stage.LightingNames) most = Mathf.Max(most, Skin.Width(Skin.Chip, value));
            return most + U(4f);
        }

        /// <summary>
        /// Before anything draws: the open box takes a press over it, switching the row under
        /// it; a press elsewhere, but on its chip, closes it, as do Escape and another selection.
        /// True when it took the event.
        /// </summary>
        private static bool ViewInput(Explorer explorer)
        {
            if (!_viewOpen) return false;
            if (explorer.Selected != _viewFor)
            {
                _viewOpen = false;
                return false;
            }
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _viewOpen = false;
                e.Use();
                return true;
            }
            var mouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
            var over = _viewBox.Contains(mouse);
            if (e.type == EventType.MouseDown)
            {
                if (!over)
                {
                    if (!_viewChip.Contains(mouse)) _viewOpen = false;
                    return false;
                }
                var rows = ViewRows(_viewFor);
                var index = Mathf.FloorToInt((mouse.y - _viewBox.y - U(4f)) / ViewRowHeight);
                if (index >= 0 && index < rows.Count)
                {
                    rows[index].Act();
                    SaveRects();
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

        /// <summary>Draws the open box over everything else.</summary>
        private static void ViewDraw()
        {
            if (!_viewOpen || _viewFor == null) return;
            var e = Event.current;
            Rect Here(Rect screen) => new Rect(GUIUtility.ScreenToGUIPoint(screen.position), screen.size);
            var box = Here(_viewBox);
            var rows = ViewRows(_viewFor);
            if (e.type == EventType.Repaint) Skin.Box(box, Skin.Panel, Skin.Outline);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = new Rect(box.x + U(4f), box.y + U(4f) + i * ViewRowHeight, box.width - U(8f), ViewRowHeight);
                var hover = row.Contains(e.mousePosition);
                if (hover) AskTip("view:" + rows[i].Name, rows[i].Tip);
                if (e.type != EventType.Repaint) continue;
                if (hover) Skin.Fill(row, new Color(Skin.Text.r, Skin.Text.g, Skin.Text.b, 0.08f));
                GUI.Label(new Rect(row.x + U(8f), row.y, ViewNameWidth() + U(4f), row.height), rows[i].Name, Skin.Label);
                var value = rows[i].Value;
                var style = rows[i].On ? Skin.ChipOn : Skin.Chip;
                var w = Skin.Width(style, value) + U(4f);
                GUI.Label(new Rect(row.xMax - U(6f) - w, row.y + (row.height - U(22f)) / 2f, w, U(22f)), value, style);
            }
        }
    }
}
