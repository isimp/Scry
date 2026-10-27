using System;

namespace Scry
{
    /// <summary>
    /// Draws a mark's glyph (<see cref="MarkGlyphs"/>) as coverage over a square of pixels: filled,
    /// its body with its cuts and inlays as holes, for a mark walked by you; and as an outline, the
    /// body's edge a stroke wide drawn inward with its inlays solid, for a mark walked on this server
    /// or an entrance alone. Worked out at twice the size and averaged, so the edges are soft.
    /// </summary>
    internal static class MarkRaster
    {
        /// <summary>The disc's inner radius in the mark's 64-unit space, which no glyph reaches past.</summary>
        public const float InnerDisc = 27f;

        /// <summary>How wide the outline's stroke is, in the mark's units.</summary>
        public const float Stroke = 4f;

        /// <summary>The glyph filled: each pixel's coverage, 0 to 255, over a square of <paramref name="side"/> pixels.</summary>
        public static byte[] Filled(MarkGlyph glyph, int side) => Averaged(FilledBits(glyph, side * 2), side);

        /// <summary>The glyph as an outline: each pixel's coverage, 0 to 255, over a square of <paramref name="side"/> pixels.</summary>
        public static byte[] Outline(MarkGlyph glyph, int side) => Averaged(OutlineBits(glyph, side * 2), side);

        /// <summary>The glyph filled, as a bit per pixel over a square of <paramref name="side"/> pixels: the body with its cuts and inlays as holes.</summary>
        public static bool[] FilledBits(MarkGlyph glyph, int side)
        {
            var parts = MarkGlyphs.PartsOf(glyph);
            var bits = new bool[side * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var (gx, gy) = Point(x, y, side);
                    var body = false;
                    var hole = false;
                    // By index: a look at the parts through their list would make an enumerator for each pixel.
                    for (var i = 0; i < parts.Count; i++)
                    {
                        var part = parts[i];
                        if (!part.Holds(gx, gy)) continue;
                        if (part.Cut || part.Inlay) hole = true;
                        else body = true;
                    }
                    bits[y * side + x] = body && !hole;
                }
            }
            return bits;
        }

        /// <summary>The glyph as an outline, as a bit per pixel: the edge of the body with its cuts, a stroke wide inward, and its inlays solid.</summary>
        public static bool[] OutlineBits(MarkGlyph glyph, int side)
        {
            var (solid, inlay) = SolidAndInlay(glyph, side);
            return Edge(solid, inlay, side);
        }

        /// <summary>The outline of a body and its inlays, each a bit a pixel over a square of <paramref name="side"/> pixels.</summary>
        internal static bool[] Edge(bool[] solid, bool[] inlay, int side)
        {
            // The edge: a solid pixel with a pixel not solid within the stroke's reach, or the square's
            // edge. Told row by row from a count of the pixels not solid up to each, the same
            // pixels as looking at every pixel within reach, with one look a row rather than one a pixel.
            var reach = Stroke * side / 64f;
            var r = (int)Math.Ceiling(reach);
            var reach2 = reach * reach;
            var widths = new int[r + 1];
            for (var dy = 0; dy <= r; dy++)
            {
                var w = -1;
                while ((w + 1) * (w + 1) + dy * dy <= reach2) w++;
                widths[dy] = w;
            }
            var open = new int[side * (side + 1)];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++) open[y * (side + 1) + x + 1] = open[y * (side + 1) + x] + (solid[y * side + x] ? 0 : 1);
            }
            var bits = new bool[side * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var at = y * side + x;
                    if (inlay[at])
                    {
                        bits[at] = true;
                        continue;
                    }
                    if (!solid[at]) continue;
                    bits[at] = y < r || y >= side - r || NearNotSolid(open, side, x, y, r, widths);
                }
            }
            return bits;
        }

        /// <summary>Whether a pixel not solid, or the square's side, lies within reach of one in a row near enough.</summary>
        private static bool NearNotSolid(int[] open, int side, int x, int y, int r, int[] widths)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                var w = widths[dy < 0 ? -dy : dy];
                if (w < 0) continue;
                if (x - w < 0 || x + w >= side) return true;
                var row = (y + dy) * (side + 1);
                if (open[row + x + w + 1] - open[row + x - w] > 0) return true;
            }
            return false;
        }

        /// <summary>The glyph's body less its cuts, and its inlays within it, as a bit a pixel over a square of <paramref name="side"/> pixels.</summary>
        internal static (bool[] Solid, bool[] Inlay) SolidAndInlay(MarkGlyph glyph, int side)
        {
            var parts = MarkGlyphs.PartsOf(glyph);
            var solid = new bool[side * side];
            var inlay = new bool[side * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var (gx, gy) = Point(x, y, side);
                    var body = false;
                    var cut = false;
                    var inlaid = false;
                    for (var i = 0; i < parts.Count; i++)
                    {
                        var part = parts[i];
                        if (!part.Holds(gx, gy)) continue;
                        if (part.Cut) cut = true;
                        else if (part.Inlay) inlaid = true;
                        else body = true;
                    }
                    solid[y * side + x] = body && !cut;
                    inlay[y * side + x] = inlaid && body && !cut;
                }
            }
            return (solid, inlay);
        }

        /// <summary>A pixel's middle in the mark's space, y up from the disc's middle.</summary>
        private static (float X, float Y) Point(int x, int y, int side)
        {
            var scale = 64f / side;
            return ((x + 0.5f) * scale - 32f, 32f - (y + 0.5f) * scale);
        }

        /// <summary>Bits at twice the size averaged two by two into coverage.</summary>
        private static byte[] Averaged(bool[] bits, int side)
        {
            var twice = side * 2;
            var coverage = new byte[side * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var count = 0;
                    for (var sy = 0; sy < 2; sy++) for (var sx = 0; sx < 2; sx++) if (bits[(y * 2 + sy) * twice + x * 2 + sx]) count++;
                    coverage[y * side + x] = (byte)(count * 255 / 4);
                }
            }
            return coverage;
        }
    }
}
