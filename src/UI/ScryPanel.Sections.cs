using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The side's sections, headings and rows, and the tips shown while the mouse rests on something.</summary>
    internal static partial class ScryPanel
    {
        // ----- Sections and rows -----

        /// <summary>The sections folded shut, by key. Details start shut; the rest start open.</summary>
        private static readonly HashSet<string> Folded = new HashSet<string> { "details" };

        private static bool IsFolded(string key) => key != null && Folded.Contains(key);

        /// <summary>Every section that folds, by key.</summary>
        private static readonly string[] Foldable = { "kept", "variants", "adjust", "animations", "effects", "playsin", "links", "facts", "layout", "command", "details" };

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
            var textW = Skin.Width(Skin.Heading, shown);
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
                var w = Skin.Width(style, names[i]) + U(10f);
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

        // ----- Tooltips -----

        /// <summary>The panel's foot line for each view and setting, once made.</summary>
        private static readonly string[] FootHints = new string[8];

        /// <summary>
        /// The keys that are not obvious, short, and only those the settings allow; the rest is
        /// under "?". Made once for each view and setting.
        /// </summary>
        private static string FootHint()
        {
            var walk = Plugin.WalkWhileOpen;
            var look = Plugin.LookWithRightMouse;
            var index = (_compact ? 4 : 0) + (walk ? 2 : 0) + (look ? 1 : 0);
            if (FootHints[index] != null) return FootHints[index];

            var parts = new List<string>();
            if (!_compact) parts.AddRange(new[] { "Enter plays", "Ctrl+F searches", "Esc leaves a box" });
            if (walk) parts.Add("keys walk when not typing");
            if (look) parts.Add("right-drag outside to look");
            if (parts.Count == 0) parts.Add("Enter plays");
            parts[0] = char.ToUpperInvariant(parts[0][0]) + parts[0].Substring(1);
            return FootHints[index] = string.Join("  ·  ", parts);
        }

        private static int _offCount = -1;
        private static string _offTip = "";

        /// <summary>
        /// When a game update has turned features off, a chip beside the title says so, and its
        /// tip names them: nothing fails quietly. It stays until Scry is updated for the game.
        /// </summary>
        private static void GameChangedChip(float x, float limit, Event e)
        {
            var count = Compatibility.OffCount;
            if (count == 0) return;
            if (count != _offCount)
            {
                _offCount = count;
                var off = Compatibility.FeaturesOff();
                _offTip = "This version of Scry does not fully know this version of the game, so these are off until Scry is updated:\n"
                          + string.Join("\n", off.Select(f => "  " + f)) + "\nEverything else works. The log has the details.";
            }
            const string text = "Game changed";
            var w = Skin.Width(Skin.Chip, text) + U(12f);
            if (x + w > limit) return;
            var rect = new Rect(x, U(18f), w, U(24f));
            GUI.Label(rect, text, Skin.Chip);
            if (rect.Contains(e.mousePosition)) AskTip("game-changed", _offTip);
        }

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
            var width = Mathf.Min(maxW, Skin.Width(Skin.Tip, _askedTipText) + U(2f));
            var height = Skin.Height(Skin.Tip, _askedTipText, width);
            var x = Mathf.Min(_askedTipAt.x + U(16f), Screen.width - width - U(4f));
            var y = _askedTipAt.y + U(20f);
            if (y + height > Screen.height - U(4f)) y = _askedTipAt.y - height - U(8f);
            Skin.Tip.Draw(new Rect(x, y, width, height), content, false, false, false, false);
        }
    }
}
