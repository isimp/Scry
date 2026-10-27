using System;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class MarkRasterTests
    {
        // A mark's picture is drawn the first time its kind and look show on the map: drawn by
        // looking at every pixel within the stroke's reach of every pixel, it held the frame for
        // about 70 ms. Drawn row by row instead, it is the same picture.

        /// <summary>The size the map's marks are drawn at, twice their side, as the pictures are averaged two by two.</summary>
        private const int Drawn = 256;

        private static readonly MarkGlyph[] All = (MarkGlyph[])Enum.GetValues(typeof(MarkGlyph));

        /// <summary>The edge as first drawn: a solid pixel with one not solid, or the square's edge, within reach.</summary>
        private static bool[] EdgeByEveryPixel(bool[] solid, bool[] inlay, int side)
        {
            var reach = MarkRaster.Stroke * side / 64f;
            var r = (int)Math.Ceiling(reach);
            var reach2 = reach * reach;
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
                    var near = false;
                    for (var dy = -r; dy <= r && !near; dy++)
                    {
                        var yy = y + dy;
                        if (yy < 0 || yy >= side)
                        {
                            near = true;
                            break;
                        }
                        for (var dx = -r; dx <= r; dx++)
                        {
                            if (dx * dx + dy * dy > reach2) continue;
                            var xx = x + dx;
                            if (xx < 0 || xx >= side || !solid[yy * side + xx])
                            {
                                near = true;
                                break;
                            }
                        }
                    }
                    bits[at] = near;
                }
            }
            return bits;
        }

        [Fact]
        public void EveryGlyphsOutlineIsThePictureFirstDrawnPixelForPixel()
        {
            foreach (var glyph in All)
            {
                var (solid, inlay) = MarkRaster.SolidAndInlay(glyph, Drawn);
                Assert.True(EdgeByEveryPixel(solid, inlay, Drawn).SequenceEqual(MarkRaster.OutlineBits(glyph, Drawn)), glyph.ToString());
            }
        }

        [Fact]
        public void ABodyReachingTheSquaresSidesHasItsEdgeThereAsFirstDrawn()
        {
            // No glyph comes near the square's sides, kept within the disc: a body that does is
            // edged where the square ends, as it was, and nothing is looked for outside it.
            const int side = 64;
            var random = new Random(7);
            for (var round = 0; round < 20; round++)
            {
                var solid = new bool[side * side];
                var inlay = new bool[side * side];
                for (var i = 0; i < solid.Length; i++)
                {
                    solid[i] = round == 0 || random.NextDouble() < 0.97;
                    inlay[i] = solid[i] && random.NextDouble() < 0.01;
                }
                Assert.True(EdgeByEveryPixel(solid, inlay, side).SequenceEqual(MarkRaster.Edge(solid, inlay, side)), $"round {round}");
            }
        }

        [Fact]
        public void DrawingAnOutlineAllocatesLittleBeyondItsOwnPixels()
        {
            // Its three masks of a bit a pixel and a count a pixel are its own; what each pixel's
            // look at the glyph's parts made besides was megabytes, which brought the game's memory cleanups.
            MarkRaster.OutlineBits(MarkGlyph.Doorway, Drawn);
            var before = GC.GetAllocatedBytesForCurrentThread();
            MarkRaster.OutlineBits(MarkGlyph.Doorway, Drawn);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.InRange(allocated, 0, 3 * Drawn * Drawn + 4 * Drawn * (Drawn + 1) + 64 * 1024);
        }

        [Fact]
        public void DrawingAFilledGlyphAllocatesLittleBeyondItsOwnPixels()
        {
            MarkRaster.FilledBits(MarkGlyph.Doorway, Drawn);
            var before = GC.GetAllocatedBytesForCurrentThread();
            MarkRaster.FilledBits(MarkGlyph.Doorway, Drawn);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.InRange(allocated, 0, Drawn * Drawn + 64 * 1024);
        }
    }
}
