using UnityEngine;

namespace Scry
{
    /// <summary>
    /// An item's tooltip as the game shows it, every mod's hooks in, under its details where mods
    /// add to it (<see cref="Tooltips"/>): at the quality picked by its chips, the lines a mod
    /// added with an accent bar beside them, and a note naming the mods hooking the tooltip.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>The quality the tooltip is shown at, and the entry it is for; the first quality again for another entry.</summary>
        private static int _tooltipLevel = 1;
        private static Entry _tooltipOf;

        /// <summary>Whether tooltips are folded away; setting it is remembered, as clicking its heading is.</summary>
        public static bool TooltipFolded
        {
            get => IsFolded("tooltip");
            set => SetFolded("tooltip", value);
        }

        private static float TooltipSection(Entry entry, float width, float y)
        {
            if (entry.Kind != Kind.Item || !(entry.Source is GameObject prefab)) return y;
            if (!ReferenceEquals(_tooltipOf, entry))
            {
                _tooltipOf = entry;
                _tooltipLevel = 1;
            }
            // Shown where mods add to the first quality's; the game's own lines are In the game already.
            var first = Tooltips.Of(prefab, 1);
            if (first == null || !first.FromMods) return y;

            y = SectionHeading(TooltipWords.Heading, width, y, null, "tooltip");
            if (IsFolded("tooltip")) return y;
            CountDrawn(PanelPart.Tooltip);

            var most = prefab.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared?.m_maxQuality ?? 1;
            if (most > 1)
            {
                var flow = new ChipFlow(0f, width, y, U(26f), U(5f), U(5f));
                for (var q = 1; q <= most; q++)
                {
                    var text = TooltipWords.Level(q);
                    var style = q == _tooltipLevel ? Skin.ChipOn : Skin.Chip;
                    var w = Mathf.Min(width, Skin.Width(style, text) + U(8f));
                    var at = flow.Place(w);
                    var chip = new Rect(at.X, at.Y, w, flow.RowHeight);
                    if (GUI.Button(chip, text, Skin.Fitted(style, chip))) _tooltipLevel = q;
                }
                y = flow.RowBottom + U(8f);
            }

            var shown = Tooltips.Of(prefab, Mathf.Clamp(_tooltipLevel, 1, most)) ?? first;
            var note = TooltipWords.FromMods(shown.Mods);
            var noteH = Skin.Height(Skin.DimWrap, note, width);
            GUI.Label(new Rect(0f, y, width, noteH), note, Skin.DimWrap);
            y += noteH + U(6f);

            var bar = U(3f);
            var gap = U(8f);
            foreach (var (line, fromMods) in shown.Lines)
            {
                if (line.Length == 0)
                {
                    y += U(8f);
                    continue;
                }
                var height = Skin.Height(Skin.Wrap, line, width - bar - gap);
                if (fromMods) Skin.Fill(new Rect(0f, y + U(2f), bar, height - U(4f)), Skin.Accent);
                GUI.Label(new Rect(bar + gap, y, width - bar - gap, height), line, Skin.Wrap);
                y += height;
            }
            return y + U(10f);
        }

        /// <summary>Lets go of the entry the tooltip was last shown for, of the world left.</summary>
        private static void ForgetTooltip()
        {
            _tooltipOf = null;
            _tooltipLevel = 1;
        }
    }
}
