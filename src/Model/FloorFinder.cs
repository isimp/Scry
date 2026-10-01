using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A place's floors found in the place itself: rays cast straight down over it land on what
    /// faces up, floors, landings and platforms, but also tables and crates. Where many land at
    /// one height there is a floor; a few on a table are none. Heights closer than
    /// <see cref="PlaceView.SameFloor"/> are one floor, a step or a slope, the height most rays
    /// landed on standing for them.
    /// </summary>
    public static class FloorFinder
    {
        /// <summary>The height band rays are counted in, in metres.</summary>
        public const float Band = 0.25f;

        /// <summary>The share of the rays a floor needs at the least, and how many at the least.</summary>
        public const float MinShare = 0.015f;
        public const int MinHits = 6;

        /// <summary>Rays cast no closer than this, and at most this many along a side.</summary>
        public const float Spacing = 0.5f;
        public const int MostPerSide = 40;

        /// <summary>
        /// The floors from the top down, each where its rays landed on average, from the heights
        /// rays landed at on what faces up (a ray landing at several heights adds each).
        /// </summary>
        public static List<float> Floors(IReadOnlyList<float> hits, int rays)
        {
            var bands = new Dictionary<int, (int Count, double Sum)>();
            foreach (var hit in hits)
            {
                var band = (int)Math.Round(hit / Band);
                bands.TryGetValue(band, out var held);
                bands[band] = (held.Count + 1, held.Sum + hit);
            }
            var least = Math.Max(MinHits, (int)Math.Ceiling(rays * MinShare));

            // Each band with the bands either side, so a floor lying across two bands counts whole.
            (int Count, double Sum) Around(int band)
            {
                var count = 0;
                var sum = 0.0;
                for (var b = band - 1; b <= band + 1; b++)
                {
                    if (!bands.TryGetValue(b, out var held)) continue;
                    count += held.Count;
                    sum += held.Sum;
                }
                return (count, sum);
            }

            // The bands most rays landed around first, each a floor unless one taken is too near it.
            var taken = new List<float>();
            foreach (var band in bands.Keys.Select(b => (Band: b, Around: Around(b))).Where(b => b.Around.Count >= least).OrderByDescending(b => b.Around.Count).ThenByDescending(b => b.Band))
            {
                var height = (float)(band.Around.Sum / band.Around.Count);
                if (taken.Any(t => Math.Abs(t - height) < PlaceView.SameFloor - 1e-4f)) continue;
                taken.Add(height);
            }
            return taken.OrderByDescending(h => h).ToList();
        }

        /// <summary>Where to cast the rays over a footprint: every half metre, spread out to at most forty along its longer side, its corners included.</summary>
        public static List<(float X, float Z)> Grid(float minX, float maxX, float minZ, float maxZ)
        {
            var spanX = Math.Max(0.0, maxX - minX);
            var spanZ = Math.Max(0.0, maxZ - minZ);
            var longer = Math.Max(spanX, spanZ);
            var alongLonger = Math.Min(MostPerSide, (int)Math.Ceiling(longer / Spacing - 1e-6) + 1);
            var step = alongLonger > 1 ? longer / (alongLonger - 1) : Spacing;
            int Count(double span) => step <= 0 ? 1 : (int)Math.Ceiling(span / step - 1e-6) + 1;
            var nx = Count(spanX);
            var nz = Count(spanZ);

            var points = new List<(float, float)>(nx * nz);
            for (var i = 0; i < nx; i++)
            {
                var x = nx > 1 ? minX + spanX * i / (nx - 1) : (minX + maxX) / 2.0;
                for (var j = 0; j < nz; j++)
                {
                    var z = nz > 1 ? minZ + spanZ * j / (nz - 1) : (minZ + maxZ) / 2.0;
                    points.Add(((float)x, (float)z));
                }
            }
            return points;
        }
    }
}
