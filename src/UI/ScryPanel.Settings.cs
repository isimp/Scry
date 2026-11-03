using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The cards a player sets Scry and the compendium from: the settings, opened by the gear in
    /// the title row over the panel's whole body (<see cref="SettingsCard"/>); in the list's place,
    /// the compendium's guide, opened by the ? in its box; and the first start's setup over the
    /// body (<see cref="SetupCard"/>).
    /// </summary>
    internal static partial class ScryPanel
    {
        private static Vector2 _guideScroll;
        private static float _guideHeight;

        /// <summary>Whether the compendium's guide is shown, and showing or putting it away; the box's ? and the self-test.</summary>
        public static bool GuideShown => _card == ListCard.Guide;

        public static void ShowGuide(bool shown)
        {
            if (shown) ShowCard(ListCard.Guide);
            else CloseCard(ListCard.Guide);
        }

        /// <summary>The gear in the title row, lit while the settings card is shown.</summary>
        private static void SettingsGear(Rect rect)
        {
            var shown = SettingsShown;
            if (GUI.Button(rect, GUIContent.none, shown ? Skin.On : Skin.IconButton)) ShowSettings(!shown);
            var inset = U(6f);
            Skin.Icon(new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f), Skin.Gear, shown ? Skin.Accent : Skin.Dim);
            if (rect.Contains(Event.current.mousePosition)) AskTip("settings", SettingsWords.GearTip);
            CountDrawn(PanelPart.SettingsGear);
        }

        /// <summary>
        /// The compendium's guide (<see cref="GuideWords"/>): each heading its caption, the line
        /// saying what it is for, its words dimmer under it, and where it has them a table and
        /// examples, each example on a faint plate with an accent bar, as a page's lines.
        /// </summary>
        private static void GuideCard(Rect rect)
        {
            CountDrawn(PanelPart.GuideCard);
            var body = BeginCard(rect, ref _guideScroll, _guideHeight, SettingsWords.GuideTitle);
            var y = body.Y;
            foreach (var section in GuideWords.Sections)
            {
                GUI.Label(new Rect(body.X, y, body.Width, U(22f)), section.Caption, Skin.WrapBold);
                y += U(24f);
                y = Wrapped(section.Lead, Skin.Wrap, body.X, y, body.Width) + U(4f);
                y = Wrapped(section.Words, Skin.DimWrap, body.X, y, body.Width) + U(6f);
                if (section.Table != null) y = GuideTableAt(section.Table, body.X, y, body.Width) + U(6f);
                foreach (var example in section.Examples) y = GuideExampleAt(example, body.X, y, body.Width) + U(6f);
                y += U(10f);
            }
            if (Event.current.type == EventType.Repaint) _guideHeight = y;
            if (EndCard(rect, out _)) ShowGuide(false);
        }

        /// <summary>Words wrapped in a style at the width given; where they end.</summary>
        private static float Wrapped(string words, GUIStyle style, float x, float y, float width)
        {
            if (string.IsNullOrEmpty(words)) return y;
            var h = style.CalcHeight(new GUIContent(words), width);
            GUI.Label(new Rect(x, y, width, h), words, style);
            return y + h;
        }

        /// <summary>A table of the guide: its heads faint over the columns, each row's name in the first and a cell under each head, a faint line between rows; where it ends.</summary>
        private static float GuideTableAt(GuideTable table, float x, float y, float width)
        {
            var first = width * 0.34f;
            var cell = (width - first) / table.Columns.Length;
            for (var c = 0; c < table.Columns.Length; c++) GUI.Label(new Rect(x + first + c * cell, y, cell, U(18f)), table.Columns[c], Skin.FaintLabel);
            y += U(20f);
            foreach (var (row, cells) in table.Rows)
            {
                Skin.Fill(new Rect(x, y, width, 1f), Skin.Alpha(Skin.Text, 0.06f));
                var h = Skin.SmallWrap.CalcHeight(new GUIContent(row), first - U(6f));
                for (var c = 0; c < cells.Length; c++) h = Mathf.Max(h, Skin.SmallWrap.CalcHeight(new GUIContent(cells[c]), cell - U(4f)));
                GUI.Label(new Rect(x, y + U(3f), first - U(6f), h), row, Skin.SmallWrap);
                for (var c = 0; c < cells.Length; c++) GUI.Label(new Rect(x + first + c * cell, y + U(3f), cell - U(4f), h), cells[c], Skin.SmallWrap);
                y += h + U(6f);
            }
            return y;
        }

        /// <summary>An example of the guide on a faint plate with an accent bar: its title faint, each line's label dim beside what it says, and its line under them; where it ends.</summary>
        private static float GuideExampleAt(GuideExample example, float x, float y, float width)
        {
            var inner = width - U(20f);
            var label = inner * 0.36f;
            // Measured first, for the plate to stand under all of it.
            var height = U(22f);
            foreach (var (name, said) in example.Lines) height += LineHeight(name, said, inner, label) + U(2f);
            if (!string.IsNullOrEmpty(example.Foot)) height += Skin.DimWrap.CalcHeight(new GUIContent(example.Foot), inner) + U(2f);
            height += U(6f);
            var plate = new Rect(x, y, width, height);
            Skin.Fill(plate, Skin.Alpha(Skin.Text, 0.035f));
            Skin.Fill(new Rect(plate.x, plate.y, U(2f), plate.height), Skin.Accent);
            var left = x + U(12f);
            GUI.Label(new Rect(left, y + U(4f), inner, U(18f)), example.Title, Skin.FaintLabel);
            var at = y + U(22f);
            foreach (var (name, said) in example.Lines)
            {
                var h = LineHeight(name, said, inner, label);
                if (string.IsNullOrEmpty(name)) GUI.Label(new Rect(left, at, inner, h), said, Skin.SmallWrap);
                else
                {
                    GUI.Label(new Rect(left, at, label - U(6f), h), name, Skin.DimLabel);
                    GUI.Label(new Rect(left + label, at, inner - label, h), said, Skin.SmallWrap);
                }
                at += h + U(2f);
            }
            if (!string.IsNullOrEmpty(example.Foot)) Wrapped(example.Foot, Skin.DimWrap, left, at, inner);
            return y + height;
        }

        /// <summary>How tall an example's line stands: what it says wrapped beside its label, or across the plate with none.</summary>
        private static float LineHeight(string name, string said, float inner, float label) =>
            Mathf.Max(U(18f), Skin.SmallWrap.CalcHeight(new GUIContent(said), string.IsNullOrEmpty(name) ? inner : inner - label));
    }
}
