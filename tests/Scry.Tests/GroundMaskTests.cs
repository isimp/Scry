using System;
using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class GroundMaskTests
    {
        // A camp paints the stage's ground with hundreds of paints. Its mask, up to 256 points
        // across, is painted with only the paints near each point, so laying it costs a fraction
        // of weighing every paint at every point, and comes out as the same mask.

        private static readonly (float R, float G, float B, float A) Bare = (0f, 0f, 0f, 0.5f);

        /// <summary>The mask as every point weighs every paint that reaches it, in the game's order.</summary>
        private static (float, float, float, float)[] PointByPoint(int size, float middleX, float middleZ, float across, List<GroundPaint> paints)
        {
            var pixels = new (float, float, float, float)[size * size];
            var near = new List<GroundPaint>();
            for (var j = 0; j < size; j++)
            {
                var z = middleZ + ((j + 0.5f) / size - 0.5f) * across;
                for (var i = 0; i < size; i++)
                {
                    var x = middleX + ((i + 0.5f) / size - 0.5f) * across;
                    near.Clear();
                    foreach (var paint in paints) if (Math.Abs(x - paint.X) <= paint.Radius && Math.Abs(z - paint.Z) <= paint.Radius) near.Add(paint);
                    pixels[j * size + i] = near.Count == 0 ? Bare : StageGround.Painted(Bare, x, z, near);
                }
            }
            return pixels;
        }

        private static List<GroundPaint> Paints(int count, int seed, float spread, float middleX, float middleZ)
        {
            var random = new Random(seed);
            var paints = new List<GroundPaint>();
            for (var n = 0; n < count; n++)
            {
                paints.Add(new GroundPaint
                {
                    X = middleX + (float)(random.NextDouble() - 0.5) * spread,
                    Z = middleZ + (float)(random.NextDouble() - 0.5) * spread,
                    Radius = (float)random.NextDouble() * 8f,
                    Strength = (float)random.NextDouble(),
                    Color = ((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()),
                    ClearsVegetation = random.Next(2) == 0,
                });
            }
            return paints;
        }

        [Theory]
        [InlineData(32, 24f, 5, 1)]
        [InlineData(100, 60f, 80, 2)]
        [InlineData(256, 160f, 300, 3)]
        [InlineData(256, 30f, 300, 4)]
        [InlineData(77, 45.5f, 150, 5)]
        public void AMaskPaintedFromThePaintsNearEachPointIsTheSameMask(int size, float across, int count, int seed)
        {
            var paints = Paints(count, seed, across * 1.3f, 12.5f, -40f);
            var mask = new (float, float, float, float)[size * size];

            StageGround.PaintMask(size, 12.5f, -40f, across, paints, Bare, mask);

            Assert.Equal(PointByPoint(size, 12.5f, -40f, across, paints), mask);
        }

        [Fact]
        public void PaintsOffTheMaskLeaveItBare()
        {
            var paints = new List<GroundPaint>
            {
                new GroundPaint { X = 500f, Z = 0f, Radius = 5f, Strength = 1f, Color = (1f, 1f, 1f, 1f), ClearsVegetation = true },
                new GroundPaint { X = 0f, Z = -500f, Radius = 5f, Strength = 1f, Color = (1f, 1f, 1f, 1f) },
                new GroundPaint { X = 1e12f, Z = 0f, Radius = 5f, Strength = 1f, Color = (1f, 1f, 1f, 1f) },
                // One whose reach is no number reaches nothing.
                new GroundPaint { X = 0f, Z = 0f, Radius = float.NaN, Strength = 1f, Color = (1f, 1f, 1f, 1f) },
            };
            var mask = new (float, float, float, float)[32 * 32];

            StageGround.PaintMask(32, 0f, 0f, 24f, paints, Bare, mask);

            Assert.All(mask, point => Assert.Equal(Bare, point));
        }

        [Fact]
        public void APaintReachingTheEdgeIsWeighedThereToo()
        {
            // Its middle off the mask, its reach over the edge.
            var paints = new List<GroundPaint> { new GroundPaint { X = 13f, Z = 0f, Radius = 3f, Strength = 1f, Color = (1f, 0f, 0f, 1f) } };
            var mask = new (float, float, float, float)[32 * 32];

            StageGround.PaintMask(32, 0f, 0f, 24f, paints, Bare, mask);

            Assert.Equal(PointByPoint(32, 0f, 0f, 24f, paints), mask);
            Assert.NotEqual(Bare, mask[16 * 32 + 31]);
        }
    }
}
