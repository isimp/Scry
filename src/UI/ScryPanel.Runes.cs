using UnityEngine;

namespace Scry
{
    /// <summary>A location's runestones under its details: every text each stone can give, with its title.</summary>
    internal static partial class ScryPanel
    {

        /// <summary>Whether the runestones' texts are folded away; setting it is remembered, as clicking its heading is.</summary>
        public static bool RunesFolded
        {
            get => IsFolded("runes");
            set
            {
                if (value == IsFolded("runes")) return;
                if (value) Folded.Add("runes");
                else Folded.Remove("runes");
                SaveRects();
            }
        }

        private static float RunesSection(Entry entry, float width, float y)
        {
            if (!(entry.Source is PlaceSource place) || place.Contents == null || place.Contents.Runestones.Count == 0) return y;

            y = SectionHeading("RUNESTONE TEXTS", width, y, null, "runes");
            if (IsFolded("runes")) return y;
            CountDrawn(PanelPart.Runes);

            foreach (var stone in place.Contents.Runestones)
            {
                var caption = RuneWords.Caption(stone);
                if (caption != null)
                {
                    var captionH = Skin.Height(Skin.DimWrap, caption, width);
                    GUI.Label(new Rect(0f, y, width, captionH), caption, Skin.DimWrap);
                    y += captionH + U(8f);
                }
                foreach (var text in stone.Texts)
                {
                    var title = RuneWords.Title(text, stone.Name);
                    var titleH = Skin.Height(Skin.WrapBold, title, width);
                    GUI.Label(new Rect(0f, y, width, titleH), title, Skin.WrapBold);
                    y += titleH + U(2f);
                    var bodyH = Skin.Height(Skin.Wrap, text.Text, width);
                    GUI.Label(new Rect(0f, y, width, bodyH), text.Text, Skin.Wrap);
                    y += bodyH + U(10f);
                }
            }
            return y + U(4f);
        }
    }
}
