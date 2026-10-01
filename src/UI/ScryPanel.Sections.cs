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
        private static readonly string[] Foldable = { "kept", "variants", "adjust", "animations", "effects", "playsin", "links", "facts", "runes", "command", "details" };

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

        // ----- What is off -----

        private static int _offCount = -1;
        private static List<string> _offFeatures = new List<string>();
        private static string _offLine;

        /// <summary>How much was off when the notice was put away; it comes back when more goes off.</summary>
        private static int _offPutAway;

        /// <summary>Whether the notice's details stand in the list's place.</summary>
        private static bool _offDetails;
        private static Vector2 _offScroll;
        private static GUIStyle _offStyle;
        private static GUIStyle _offStyleFrom;

        /// <summary>What is off, read again whenever how much is off changes, not every frame.</summary>
        private static void ReadOff()
        {
            var count = Compatibility.OffCount;
            if (count == _offCount) return;
            _offCount = count;
            _offFeatures = count > 0 ? Compatibility.FeaturesOff() : new List<string>();
            _offLine = OffWords.Line(_offFeatures);
        }

        /// <summary>How tall the notice under the header is: none while nothing is off or it was put away.</summary>
        private static float OffNoticeHeight()
        {
            ReadOff();
            return _offFeatures.Count > _offPutAway ? U(32f) : 0f;
        }

        /// <summary>
        /// A strip under the header while part of Scry is off, so nothing fails quietly: what is
        /// off in a line, sliding when it is longer than the strip, details in the list's place,
        /// and a cross that puts it away until more goes off.
        /// </summary>
        private static void OffNotice(Rect rect)
        {
            if (_offLine == null) return;
            if (_offStyle == null || !ReferenceEquals(_offStyleFrom, Skin.Label))
            {
                _offStyleFrom = Skin.Label;
                _offStyle = new GUIStyle(Skin.Label) { normal = { textColor = Skin.Warn } };
            }

            Skin.Fill(rect, Skin.WarnSoft);
            Skin.Fill(new Rect(rect.x, rect.y, U(3f), rect.height), Skin.Warn);
            var mark = U(18f);
            var markRect = new Rect(rect.x + U(12f), rect.y + (rect.height - mark) / 2f, mark, mark);
            Skin.Icon(markRect, Skin.Circle, Skin.Warn);
            GUI.Label(markRect, "!", new GUIStyle(Skin.Label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Skin.OnAccent }, fontStyle = FontStyle.Bold });

            const string details = "Details";
            var closeW = U(24f);
            var detailsW = Skin.Width(Skin.Chip, details) + U(12f);
            var close = new Rect(rect.xMax - closeW - U(4f), rect.y + (rect.height - closeW) / 2f, closeW, closeW);
            var more = new Rect(close.x - U(6f) - detailsW, rect.y + (rect.height - U(24f)) / 2f, detailsW, U(24f));
            var text = new Rect(markRect.xMax + U(10f), rect.y, more.x - markRect.xMax - U(18f), rect.height);
            Ticker(text, _offLine, _offStyle);

            if (GUI.Button(more, details, _offDetails ? Skin.ChipOn : Skin.Chip))
            {
                _offDetails = !_offDetails;
                _help = false;
                _modReport = false;
            }
            if (GUI.Button(close, "\u00D7", Skin.Close))
            {
                _offPutAway = _offFeatures.Count;
                _offDetails = false;
            }
            if (close.Contains(Event.current.mousePosition)) AskTip("off-away", "Put this away until more of Scry goes off");
        }

        /// <summary>The notice's details in the list's place: why, what to do, and each part that is off.</summary>
        private static void OffCard(Rect rect)
        {
            Skin.Box(rect, Skin.Panel);
            var closeH = U(30f);
            var area = new Rect(rect.x + U(4f), rect.y + U(6f), rect.width - U(8f), rect.height - closeH - U(20f));
            var width = area.width - U(38f);
            var body = OffWords.Details();
            var bodyH = Skin.Height(Skin.DimWrap, body, width);
            var height = U(44f) + bodyH + U(12f) + _offFeatures.Count * U(26f) + U(12f);
            var view = new Rect(0f, 0f, area.width - U(14f), Mathf.Max(height, area.height));
            _offScroll = GUI.BeginScrollView(area, _offScroll, view, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);
            var x = U(14f);
            var y = U(10f);
            GUI.Label(new Rect(x, y, width, U(26f)), "What is off", Skin.Big);
            y += U(34f);
            GUI.Label(new Rect(x, y, width, bodyH), body, Skin.DimWrap);
            y += bodyH + U(12f);
            foreach (var feature in _offFeatures)
            {
                Skin.Fill(new Rect(x, y + U(8f), U(6f), U(6f)), Skin.Warn);
                GUI.Label(new Rect(x + U(16f), y, width - U(16f), U(22f)), char.ToUpperInvariant(feature[0]) + feature.Substring(1), Skin.Label);
                y += U(26f);
            }
            GUI.EndScrollView();

            var closeRect = new Rect(rect.xMax - U(96f), rect.yMax - closeH - U(10f), U(80f), closeH);
            if (GUI.Button(closeRect, "Close", Skin.Button)) _offDetails = false;

            // For reporting: what is off, with the versions, ready to paste.
            const string copy = "Copy for a report";
            var copyW = Skin.Width(Skin.Button, copy) + U(10f);
            var copyRect = new Rect(closeRect.x - U(8f) - copyW, closeRect.y, copyW, closeH);
            if (copyRect.x > rect.x + U(8f) && GUI.Button(copyRect, copy, Skin.Button))
            {
                string game;
                try { game = GameVersion(); }
                catch (System.Exception) { game = "of an unknown version"; }
                GUIUtility.systemCopyBuffer = $"Scry {Plugin.Version}, Valheim {game}: off: {string.Join(", ", _offFeatures)}";
                Session.Say("Copied what is off, with Scry's and the game's versions.");
            }
        }

        /// <summary>The game's version, in a method of its own so that an update renaming it fails only here.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string GameVersion() => global::Version.GetVersionString();

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
