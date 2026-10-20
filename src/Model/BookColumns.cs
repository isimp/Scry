using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The compendium box's columns after its captions: one for each level a world's source can
    /// show (Off, Seen, Known, With insides), so a level stands at one place on every row, a row
    /// without one leaving its cell empty. Counted, the whole world's alone, spans Seen and Known;
    /// the rules (Off, Loose, Strict) take the first three, and the floor's arrows the first and
    /// the last, its biome the two between. A column is as wide as the widest that stands in it,
    /// each measured as it is drawn lit; one spanning is made room for at the last column it spans.
    /// </summary>
    internal static class BookColumns
    {
        /// <summary>How many columns there are.</summary>
        public const int Count = 4;

        /// <summary>The floor's arrows and its biome between them, as columns.</summary>
        public static readonly (int First, int Last) FloorLower = (0, 0), FloorValue = (1, 2), FloorRaise = (3, 3);

        /// <summary>The columns a source's level stands in.</summary>
        public static (int First, int Last) Of(SourceLevel level)
        {
            switch (level)
            {
                case SourceLevel.Off: return (0, 0);
                case SourceLevel.Counted: return (1, 2);
                case SourceLevel.Seen: return (1, 1);
                case SourceLevel.Known: return (2, 2);
                default: return (3, 3);
            }
        }

        /// <summary>The column a rule stands in.</summary>
        public static (int First, int Last) Of(CompendiumRule rule) => ((int)rule, (int)rule);

        /// <summary>The columns' widths, worked out afresh: each the widest of what stands in it alone, then the last of a span widened where what spans it is wider than its columns and their gaps.</summary>
        public static void Widths(float[] into, IReadOnlyList<(int First, int Last, float Width)> cells, float gap)
        {
            for (var c = 0; c < Count; c++) into[c] = 0f;
            foreach (var (first, last, width) in cells)
            {
                if (first == last && width > into[first]) into[first] = width;
            }
            foreach (var (first, last, width) in cells)
            {
                if (first == last) continue;
                var held = gap * (last - first);
                for (var c = first; c <= last; c++) held += into[c];
                if (width > held) into[last] += width - held;
            }
        }

        /// <summary>Where a cell of columns starts, from where the columns start, a gap apart, and how wide it is with the gaps it spans.</summary>
        public static (float X, float Width) Cell(float[] widths, float left, float gap, int first, int last)
        {
            var x = left;
            for (var c = 0; c < first; c++) x += widths[c] + gap;
            var width = gap * (last - first);
            for (var c = first; c <= last; c++) width += widths[c];
            return (x, width);
        }

        /// <summary>How wide the columns are together, with the gaps between them.</summary>
        public static float Width(float[] widths, float gap) => Cell(widths, 0f, gap, 0, Count - 1).Width;
    }
}
