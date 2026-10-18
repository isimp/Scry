using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>A table's columns made to fit the width it is drawn in.</summary>
    internal static class TableFit
    {
        /// <summary>
        /// Each column's width, worked out in place of what it wants: as wide as it wants where all
        /// fit; else what each wants beyond its heading given up alike, so every heading stays
        /// whole; else, where not even the headings fit, every column alike by its share.
        /// </summary>
        public static void Fit(IList<float> widths, IList<float> heading, float room)
        {
            var sum = 0f;
            var least = 0f;
            for (var c = 0; c < widths.Count; c++)
            {
                sum += widths[c];
                least += Math.Min(widths[c], c < heading.Count ? heading[c] : 0f);
            }
            if (sum <= room || sum <= 0f) return;
            if (least < room)
            {
                // What each column wants beyond its heading gives way, alike, until all fit.
                var spare = sum - least;
                var cut = sum - room;
                for (var c = 0; c < widths.Count; c++)
                {
                    var keep = Math.Min(widths[c], c < heading.Count ? heading[c] : 0f);
                    widths[c] -= (widths[c] - keep) * cut / spare;
                }
                return;
            }
            for (var c = 0; c < widths.Count; c++) widths[c] *= room / sum;
        }
    }
}
