using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// An item's tooltip as the game shows it, every mod's hooks in, under its details where mods
    /// add to it (<see cref="Tooltips"/>): at the quality picked, as a card headed by the item's
    /// name and laid out as <see cref="TooltipLayout"/> tells, each run of lines a mod added in a
    /// block tinted with the mods' colour, and under the card what the tint means and the mods
    /// hooking the tooltip, each going to its page.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>The quality the tooltip is shown at, and the entry it is for; the first quality again for another entry.</summary>
        private static int _tooltipLevel = 1;
        private static Entry _tooltipOf;

        /// <summary>The qualities offered, for the item's most quality they were made for.</summary>
        private static List<string> _tooltipQualities;
        private static int _tooltipQualitiesFor;

        /// <summary>Where each of the card's parts starts below the card's top, and how tall it is, worked out before the card is drawn.</summary>
        private static readonly List<float> TooltipTops = new List<float>();
        private static readonly List<float> TooltipHeights = new List<float>();

        /// <summary>Whether tooltips are folded away; setting it is remembered, as clicking its heading is.</summary>
        public static bool TooltipFolded
        {
            get => IsFolded("tooltip");
            set => SetFolded("tooltip", value);
        }

        private static float TooltipSection(Explorer explorer, Entry entry, float width, float y)
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
                if (_tooltipQualities == null || _tooltipQualitiesFor != most)
                {
                    _tooltipQualities = TooltipWords.Qualities(most);
                    _tooltipQualitiesFor = most;
                }
                var chosen = Segments(TooltipWords.Quality, _tooltipQualities, _tooltipLevel - 1, width, U(80f), ref y);
                if (chosen >= 0) _tooltipLevel = chosen + 1;
            }

            var shown = Tooltips.Of(prefab, Mathf.Clamp(_tooltipLevel, 1, most)) ?? first;
            y = TooltipCard(shown, width, y);
            return TooltipMods(explorer, shown.Mods, width, y) + U(10f);
        }

        /// <summary>
        /// The card: a topic's box, the item's name in its top left, then each part, every run of
        /// a mod's lines on a tint with a bar of the mods' colour down its left. Heights are worked
        /// out first, so the box and the tints are drawn behind what they hold the first time.
        /// </summary>
        private static float TooltipCard(Tooltips.Shown shown, float width, float y)
        {
            var pad = U(12f);
            var inner = width - pad * 2f;
            var labelW = Mathf.Min(U(130f), inner * 0.36f);
            var parts = shown.Parts;

            TooltipTops.Clear();
            TooltipHeights.Clear();
            var at = U(34f);
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part.FromMods && (i == 0 || !parts[i - 1].FromMods)) at += U(4f);
                var height = TooltipPartHeight(part, inner, labelW);
                TooltipTops.Add(at);
                TooltipHeights.Add(height);
                at += height;
                var last = i + 1 == parts.Count;
                if (part.FromMods && (last || !parts[i + 1].FromMods)) at += U(4f);
                if (part.Kind == TooltipPartKind.Description && (last || parts[i + 1].Kind != TooltipPartKind.Description)) at += U(6f);
            }

            var top = y;
            var cardH = at + U(8f);
            Skin.Box(new Rect(0f, top, width, cardH), Skin.TopicFill);
            var modColour = Skin.KindColor(Kind.Mod);
            foreach (var (firstPart, lastPart) in shown.Runs)
            {
                var runTop = TooltipTops[firstPart] - U(4f);
                var block = new Rect(U(4f), top + runTop, width - U(8f), TooltipTops[lastPart] + TooltipHeights[lastPart] + U(4f) - runTop);
                Skin.Fill(block, Skin.Alpha(modColour, 0.10f));
                Skin.Fill(new Rect(block.x, block.y, U(3f), block.height), modColour);
            }
            GUI.Label(new Rect(pad, top + U(8f), inner, U(20f)), shown.Name, Skin.TopicName);
            for (var i = 0; i < parts.Count; i++)
            {
                TooltipPartDrawn(parts[i], new Rect(pad, top + TooltipTops[i], inner, TooltipHeights[i]), labelW, modColour);
            }
            return top + cardH + U(8f);
        }

        private static float TooltipPartHeight(TooltipPart part, float width, float labelW)
        {
            switch (part.Kind)
            {
                case TooltipPartKind.Divider:
                    return U(13f);
                case TooltipPartKind.Description:
                    return Skin.Height(Skin.DimWrap, part.Value, width) + U(2f);
                case TooltipPartKind.Heading:
                    return Skin.Height(Skin.SmallWrap, part.Value, width) + U(4f);
                case TooltipPartKind.Row:
                    var valueW = width - labelW - U(10f);
                    return Mathf.Max(U(20f), Mathf.Max(Skin.Height(Skin.DimWrap, part.Label, labelW), TooltipValueHeight(part, valueW))) + U(2f);
                default:
                    return Skin.Height(Skin.Wrap, part.Value, width) + U(2f);
            }
        }

        /// <summary>Whether a row's bracketed aside fits on the line after its value.</summary>
        private static bool AsideBeside(TooltipPart part, float valueW) =>
            Skin.Width(Skin.Wrap, part.Value) + U(6f) + Skin.Width(Skin.Wrap, part.Extra) <= valueW;

        private static float TooltipValueHeight(TooltipPart part, float valueW)
        {
            var height = Skin.Height(Skin.Wrap, part.Value, valueW);
            if (part.Extra.Length == 0 || AsideBeside(part, valueW)) return height;
            return height + Skin.Height(Skin.Wrap, part.Extra, valueW);
        }

        /// <summary>
        /// One part: the description soft, a heading small and faint, a row's label soft beside its
        /// value with the value's aside fainter after it (under it where it does not fit), any
        /// other line whole, a divider a hairline, tinted within a mod's run.
        /// </summary>
        private static void TooltipPartDrawn(TooltipPart part, Rect at, float labelW, Color modColour)
        {
            if (OutOfSight(at)) return;
            switch (part.Kind)
            {
                case TooltipPartKind.Divider:
                    Skin.Fill(new Rect(at.x, at.y + U(6f), at.width, U(1f)), part.FromMods ? Skin.Alpha(modColour, 0.25f) : Skin.Outline);
                    break;
                case TooltipPartKind.Description:
                    GUI.Label(at, part.Value, Skin.DimWrap);
                    break;
                case TooltipPartKind.Heading:
                    Skin.LabelIn(new Rect(at.x, at.y + U(2f), at.width, at.height - U(2f)), part.Value, Skin.SmallWrap, Skin.Faint);
                    break;
                case TooltipPartKind.Row:
                    var valueW = at.width - labelW - U(10f);
                    var valueX = at.x + labelW + U(10f);
                    GUI.Label(new Rect(at.x, at.y, labelW, at.height), part.Label, Skin.DimWrap);
                    if (part.Extra.Length == 0)
                    {
                        GUI.Label(new Rect(valueX, at.y, valueW, at.height), part.Value, Skin.Wrap);
                    }
                    else if (AsideBeside(part, valueW))
                    {
                        var shownW = Skin.Width(Skin.Wrap, part.Value);
                        GUI.Label(new Rect(valueX, at.y, shownW + U(2f), at.height), part.Value, Skin.Wrap);
                        Skin.LabelIn(new Rect(valueX + shownW + U(6f), at.y, valueW - shownW - U(6f), at.height), part.Extra, Skin.Wrap, Skin.Faint);
                    }
                    else
                    {
                        var valueH = Skin.Height(Skin.Wrap, part.Value, valueW);
                        GUI.Label(new Rect(valueX, at.y, valueW, valueH), part.Value, Skin.Wrap);
                        Skin.LabelIn(new Rect(valueX, at.y + valueH, valueW, at.height - valueH), part.Extra, Skin.Wrap, Skin.Faint);
                    }
                    break;
                default:
                    GUI.Label(at, part.Value, Skin.Wrap);
                    break;
            }
        }

        /// <summary>Under the card: a swatch of the tint with what it means, then a chip for each mod hooking the tooltip, going to its page.</summary>
        private static float TooltipMods(Explorer explorer, IReadOnlyList<string> mods, float width, float y)
        {
            var modColour = Skin.KindColor(Kind.Mod);
            Skin.Fill(new Rect(0f, y + U(4f), U(14f), U(12f)), Skin.Alpha(modColour, 0.10f));
            Skin.Fill(new Rect(0f, y + U(4f), U(3f), U(12f)), modColour);
            var legend = TooltipWords.Legend(mods.Count > 0);
            var legendW = width - U(22f);
            var legendH = Skin.Height(Skin.DimWrap, legend, legendW);
            GUI.Label(new Rect(U(22f), y, legendW, legendH), legend, Skin.DimWrap);
            y += Mathf.Max(U(20f), legendH) + U(4f);
            if (mods.Count == 0) return y;

            var flow = new ChipFlow(0f, width, y, U(26f), U(5f), U(5f));
            foreach (var mod in mods)
            {
                var page = EntryKeys.For(Kind.Mod, mod);
                var go = InCatalog(explorer, page);
                var w = Mathf.Min(width, LinkChipWidth(mod, go));
                var place = flow.Place(w);
                var chip = new Rect(place.X, place.Y, w, flow.RowHeight);
                if (!OutOfSight(chip) && LinkChip(chip, mod, Kind.Mod, false, go)) Go(explorer, page);
            }
            return flow.RowBottom;
        }

        /// <summary>Lets go of the entry the tooltip was last shown for, of the world left.</summary>
        private static void ForgetTooltip()
        {
            _tooltipOf = null;
            _tooltipLevel = 1;
        }
    }
}
