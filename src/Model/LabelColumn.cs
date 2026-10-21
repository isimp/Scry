using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The column a card's labels stand in, beside what they name: as wide as its widest label and
    /// a gap, so the choices keep the rest of the row, and no more than a share of the card.
    /// </summary>
    internal static class LabelColumn
    {
        /// <summary>The most of a card's width the labels take.</summary>
        public const float MostShare = 0.42f;

        /// <summary>The column's width for labels of these widths, with a gap after them, in a card so wide.</summary>
        public static float Width(IReadOnlyList<float> labels, float gap, float card)
        {
            var widest = 0f;
            for (var i = 0; i < labels.Count; i++) widest = Math.Max(widest, labels[i]);
            return Math.Min(widest + gap, card * MostShare);
        }
    }
}
