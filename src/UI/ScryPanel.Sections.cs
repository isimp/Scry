using System;
using System.Collections.Generic;
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

        /// <summary>Folds a section shut or opens it, remembered with where the panel is.</summary>
        private static void SetFolded(string key, bool folded)
        {
            if (folded == IsFolded(key)) return;
            if (folded) Folded.Add(key);
            else Folded.Remove(key);
            SaveRects();
        }

        /// <summary>Folds these sections shut and opens every other, as remembered.</summary>
        private static void FoldOnly(IEnumerable<string> keys)
        {
            Folded.Clear();
            foreach (var key in keys) Folded.Add(key);
        }

        /// <summary>Every section that folds, by key.</summary>
        private static readonly string[] Foldable = { "kept", "variants", "adjust", "animations", "effects", "playsin", "links", "facts", "runes", "command", "details" };

        private static void FoldAll(bool fold)
        {
            if (fold) foreach (var key in Foldable) Folded.Add(key);
            else Folded.Clear();
            SaveRects();
        }

        /// <summary>
        /// A section's heading and rule, with how many the section holds where
        /// <paramref name="count"/> is 0 or more. With a key, the heading folds the section shut or
        /// opens it when clicked, and the choice is remembered; the caller skips its body while
        /// folded; and the line under the title names it (<see cref="MarkSection"/>).
        /// </summary>
        private static float SectionHeading(string text, float width, float y, Action reset, string key = null, int count = -1)
        {
            var folded = IsFolded(key);
            var shown = key == null ? text : PanelWords.SectionTitle(MarkSection(key, text, count, y), folded);
            var textW = Skin.Width(Skin.Heading, shown);
            var head = new Rect(0f, y, textW + U(4f), U(20f));
            if (key == null)
            {
                GUI.Label(head, shown, Skin.Heading);
            }
            else
            {
                LinkLabel(head, shown, Skin.Heading, Skin.Heading.normal.textColor);
                if (head.Contains(Event.current.mousePosition)) AskTip("fold:" + key, PanelWords.SectionTip(folded));
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
            var resetChip = new Rect(width - U(64f), y - U(2f), U(64f), U(24f));
            if (reset != null && GUI.Button(resetChip, "Reset", Skin.Fitted(Skin.Chip, resetChip))) reset();
            return y + U(folded ? 36f : 30f);
        }

        /// <summary>A labelled row's label, in the column every labelled row (sliders, segments, toggles) shares.</summary>
        private static void RowLabel(string label, float labelW, float y, float rowH) => FitLabel(new Rect(0f, y, labelW - U(6f), rowH), label, Skin.DimLabel, 10f);

        /// <summary>A slider's bar, as thin as every bar in the panel, across a row and in its middle.</summary>
        private static Rect BarIn(float x, float y, float width, float rowH) => new Rect(x, y + (rowH - U(14f)) / 2f, width, U(14f));

        /// <summary>
        /// A timeline's bar, which a clip's time can be dragged along to any point, as a sound's and
        /// an animation's have. Returns the time dragged to, or null where it was not dragged.
        /// </summary>
        private static float? TimeBar(float x, float y, float width, float rowH, float time, float length)
        {
            var changed = GUI.changed;
            GUI.changed = false;
            var picked = GUI.HorizontalSlider(BarIn(x, y, width, rowH), time, 0f, Mathf.Max(length, 0.01f));
            var dragged = GUI.changed;
            GUI.changed = changed || dragged;
            return dragged ? picked : (float?)null;
        }

        private static float SliderRow(string label, string value, float current, float min, float max, float width, float labelW, ref float y)
        {
            var rowH = U(26f);
            RowLabel(label, labelW, y, rowH);
            var valueW = U(64f);
            var result = GUI.HorizontalSlider(BarIn(labelW, y, width - labelW - valueW - U(10f), rowH), current, min, max);
            GUI.Label(new Rect(width - valueW, y, valueW, rowH), value, Skin.Label);
            y += rowH + U(8f);
            return result;
        }

        /// <summary>A labelled row of segments, one of them on; returns the one clicked, or -1.</summary>
        private static int Segments(string label, List<string> names, int selected, float width, float labelW, ref float y) => SegmentRow(label, names, selected, null, width, labelW, ref y);

        /// <summary>Like <see cref="Segments"/>, but any number can be on; returns the one clicked, or -1.</summary>
        private static int Toggles(string label, List<string> names, Func<int, bool> on, float width, float labelW, ref float y) => SegmentRow(label, names, -1, on, width, labelW, ref y);

        /// <summary>
        /// The segments after their label, wrapping back to the label's edge; lit where
        /// <paramref name="on"/> says, or the one <paramref name="selected"/> without it.
        /// </summary>
        private static int SegmentRow(string label, List<string> names, int selected, Func<int, bool> on, float width, float labelW, ref float y)
        {
            var flow = new ChipFlow(labelW, width, y, U(28f), U(4f), U(4f));
            RowLabel(label, labelW, y, flow.RowHeight);
            var clicked = -1;
            for (var i = 0; i < names.Count; i++)
            {
                var lit = on != null ? on(i) : i == selected;
                var style = lit ? Skin.SegmentOn : Skin.Segment;
                var w = Skin.Width(style, names[i]) + U(10f);
                var at = flow.Place(w);
                if (GUI.Button(new Rect(at.X, at.Y, w, flow.RowHeight), names[i], style)) clicked = i;
            }
            y = flow.RowBottom + U(8f);
            return clicked;
        }

        // ----- Tooltips -----

        /// <summary>The panel's foot line for each view and setting, once made.</summary>
        private static readonly string[] FootHints = new string[8];

        /// <summary>The foot's hint for the view and settings now (<see cref="PanelWords.FootHint"/>), made once for each.</summary>
        private static string FootHint()
        {
            var walk = Settings.WalkWhileOpen;
            var look = Settings.LookWithRightMouse;
            var index = (_compact ? 4 : 0) + (walk ? 2 : 0) + (look ? 1 : 0);
            return FootHints[index] ?? (FootHints[index] = PanelWords.FootHint(!_compact, walk, look));
        }

        // ----- What is off -----

        private static int _offCount = -1;
        private static List<Feature> _offFeatures = new List<Feature>();
        private static string _offLine;

        /// <summary>How much was off when the notice was put away; it comes back when more goes off.</summary>
        private static int _offPutAway;
        private static Vector2 _offScroll;

        /// <summary>What is off, read again whenever how much is off changes, not every frame.</summary>
        private static void ReadOff()
        {
            var count = Compatibility.OffCount;
            if (count == _offCount) return;
            _offCount = count;
            _offFeatures = count > 0 ? Compatibility.FeaturesOff() : new List<Feature>();
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
            switch (NoticeStrip(rect, Skin.Warn, _offLine, Skin.WarnLabel, true, "Details", _card == ListCard.Off, true, crossTip: "Put this away until more of Scry goes off"))
            {
                case StripClick.Chip:
                    ToggleCard(ListCard.Off);
                    break;
                case StripClick.Cross:
                    _offPutAway = _offFeatures.Count;
                    CloseCard(ListCard.Off);
                    break;
            }
        }

        private enum StripClick
        {
            None,
            Chip,
            Cross,
        }

        /// <summary>
        /// A notice under the header (the self-test's, what is off): tinted in its tone with a bar
        /// at its left, a mark where it warns, its line sliding where it is longer than the strip,
        /// a chip, and a cross that puts it away where it can be. Returns what was clicked.
        /// </summary>
        private static StripClick NoticeStrip(Rect rect, Color tone, string line, GUIStyle lineStyle, bool mark, string chip, bool chipOn, bool cross, string chipTip = null, string crossTip = null)
        {
            Skin.Fill(rect, Skin.Alpha(tone, 0.13f));
            Skin.Fill(new Rect(rect.x, rect.y, U(3f), rect.height), tone);
            var left = rect.x + U(14f);
            if (mark)
            {
                var size = U(18f);
                var markRect = new Rect(rect.x + U(12f), rect.y + (rect.height - size) / 2f, size, size);
                Skin.Icon(markRect, Skin.Circle, tone);
                GUI.Label(markRect, "!", Skin.Mark);
                left = markRect.xMax + U(10f);
            }

            var crossW = U(24f);
            var away = new Rect(rect.xMax - crossW - U(4f), rect.y + (rect.height - crossW) / 2f, crossW, crossW);
            var chipW = Skin.Width(Skin.Chip, chip) + U(12f);
            var chipH = U(24f);
            var button = new Rect((cross ? away.x - U(6f) : rect.xMax - U(4f)) - chipW, rect.y + (rect.height - chipH) / 2f, chipW, chipH);
            Ticker(new Rect(left, rect.y, button.x - left - U(8f), rect.height), line, lineStyle);

            var clicked = StripClick.None;
            if (GUI.Button(button, chip, Skin.Fitted(chipOn ? Skin.ChipOn : Skin.Chip, button))) clicked = StripClick.Chip;
            if (chipTip != null && button.Contains(Event.current.mousePosition)) AskTip("strip:" + chip, chipTip);
            if (!cross) return clicked;
            if (GUI.Button(away, "\u00D7", Skin.Close)) clicked = StripClick.Cross;
            if (crossTip != null && away.Contains(Event.current.mousePosition)) AskTip("strip-away:" + chip, crossTip);
            return clicked;
        }

        /// <summary>The notice's details in the list's place: why, what to do, and each part that is off.</summary>
        private static void OffCard(Rect rect)
        {
            var width = CardWidth(rect);
            var body = OffWords.Details();
            var bodyH = Skin.Height(Skin.DimWrap, body, width);
            var height = U(44f) + bodyH + U(12f) + _offFeatures.Count * U(26f) + U(12f);
            var card = BeginCard(rect, ref _offScroll, height, "What is off");
            var x = card.X;
            var y = card.Y;
            GUI.Label(new Rect(x, y, width, bodyH), body, Skin.DimWrap);
            y += bodyH + U(12f);
            foreach (var feature in _offFeatures)
            {
                Skin.Fill(new Rect(x, y + U(8f), U(6f), U(6f)), Skin.Warn);
                GUI.Label(new Rect(x + U(16f), y, width - U(16f), U(22f)), Naming.Capital(feature.Name), Skin.Label);
                y += U(26f);
            }
            if (EndCard(rect, out var close)) CloseCard(ListCard.Off);

            // For reporting: what is off, with the versions, ready to paste.
            if (CardButton(rect, close, "Copy for a report", out _))
            {
                if (!Guard.Run(Feature.Panel, "telling the game's version", GameVersion, out var game)) game = "of an unknown version";
                GUIUtility.systemCopyBuffer = OffWords.Report(About.Version, game, _offFeatures);
                Say("Copied what is off, with Scry's and the game's versions.");
            }
        }

        /// <summary>The game's version, in a method of its own so that an update renaming it fails only here.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string GameVersion() => global::Version.GetVersionString();
    }
}
