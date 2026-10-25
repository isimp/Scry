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
                    foreach (var part in parts)
                    {
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
                    foreach (var part in parts)
                    {
                        if (!part.Holds(gx, gy)) continue;
                        if (part.Cut) cut = true;
                        else if (part.Inlay) inlaid = true;
                        else body = true;
                    }
                    solid[y * side + x] = body && !cut;
                    inlay[y * side + x] = inlaid && body && !cut;
                }
            }
            // The edge: a solid pixel with a pixel not solid within the stroke's reach.
            var reach = Stroke * side / 64f;
            var r = (int)Math.Ceiling(reach);
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
                    bits[at] = NearNotSolid(solid, side, x, y, r, reach * reach);
                }
            }
            return bits;
        }

        private static bool NearNotSolid(bool[] solid, int side, int x, int y, int r, float reach2)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                var yy = y + dy;
                if (yy < 0 || yy >= side) return true;
                for (var dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > reach2) continue;
                    var xx = x + dx;
                    if (xx < 0 || xx >= side || !solid[yy * side + xx]) return true;
                }
            }
            return false;
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
