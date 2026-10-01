using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Where a ray cast straight down landed on something flat: the patch of rays it belongs to
    /// (each room of an example is probed on its own grid), its place on that grid, the height,
    /// and the ground it stands for.
    /// </summary>
    public struct FloorHit
    {
        public int Patch, I, J;
        public float Height, Area;
    }

    /// <summary>
    /// A place's floors found in the place itself: rays cast straight down over it on a grid land
    /// on what is flat, floors, landings and platforms, but also tables, beds and beams. Only
    /// ground one can stand on makes a floor: a spot counts where the spots beside it, all four
    /// ways, landed at the same height too, so a beam, a table or a bed adds nothing; those spots
    /// must hold <see cref="MinRoom"/> and a share of the place's ground. Heights closer than
    /// <see cref="PlaceView.SameFloor"/> are one floor (a step, a dais), the one with the most
    /// room standing for them.
    /// </summary>
    public static class FloorFinder
    {
        /// <summary>The height band rays are counted in, in metres; a spot's neighbours count within a band either way.</summary>
        public const float Band = 0.25f;

        /// <summary>The room a floor needs at the least, in square metres of spots with flat ground all round, and its share of the place's ground.</summary>
        public const float MinRoom = 2f;
        public const float MinShare = 0.015f;

        /// <summary>Rays cast no closer than this, and at most this many along a side.</summary>
        public const float Spacing = 0.5f;
        public const int MostPerSide = 40;

        /// <summary>The floors from the top down, each where its rays landed on average, from where they landed and the ground they were cast over.</summary>
        public static List<float> Floors(IReadOnlyList<FloorHit> hits, float footprint)
        {
            var least = Math.Max(MinRoom, footprint * MinShare);
            var byBand = new Dictionary<int, List<FloorHit>>();
            foreach (var hit in hits)
            {
                var band = (int)Math.Round(hit.Height / Band);
                if (!byBand.TryGetValue(band, out var held)) byBand[band] = held = new List<FloorHit>();
                held.Add(hit);
            }

            var candidates = new List<(int Band, float Room, float Height)>();
            foreach (var band in byBand.Keys)
            {
                // The spots within a band either way, so a floor lying across two bands counts whole.
                var near = new List<FloorHit>();
                for (var b = band - 1; b <= band + 1; b++) if (byBand.TryGetValue(b, out var held)) near.AddRange(held);
                var spots = new HashSet<(int, int, int)>(near.Select(h => (h.Patch, h.I, h.J)));
                bool Inner(FloorHit h) => spots.Contains((h.Patch, h.I - 1, h.J)) && spots.Contains((h.Patch, h.I + 1, h.J))
                                          && spots.Contains((h.Patch, h.I, h.J - 1)) && spots.Contains((h.Patch, h.I, h.J + 1));
                var inner = near.Where(Inner).ToList();
                if (inner.Count == 0) continue;

                // Each spot's ground once, however many of its hits lie in the bands.
                var room = inner.GroupBy(h => (h.Patch, h.I, h.J)).Sum(g => g.First().Area);
                if (room < least) continue;
                candidates.Add((band, room, (float)inner.Average(h => (double)h.Height)));
            }

            // The floors with the most room first, each unless one taken is too near it.
            var taken = new List<float>();
            foreach (var candidate in candidates.OrderByDescending(c => c.Room).ThenByDescending(c => c.Band))
            {
                if (taken.Any(t => Math.Abs(t - candidate.Height) < PlaceView.SameFloor - 1e-4f)) continue;
                taken.Add(candidate.Height);
            }
            return taken.OrderByDescending(h => h).ToList();
        }

        /// <summary>Where to cast the rays over a footprint, with each one's place on the grid: every half metre, spread out to at most forty along its longer side, its corners included.</summary>
        public static List<(int I, int J, float X, float Z)> Grid(float minX, float maxX, float minZ, float maxZ)
        {
            var spanX = Math.Max(0.0, maxX - minX);
            var spanZ = Math.Max(0.0, maxZ - minZ);
            var longer = Math.Max(spanX, spanZ);
            var alongLonger = Math.Min(MostPerSide, (int)Math.Ceiling(longer / Spacing - 1e-6) + 1);
            var step = alongLonger > 1 ? longer / (alongLonger - 1) : Spacing;
            int Count(double span) => step <= 0 ? 1 : (int)Math.Ceiling(span / step - 1e-6) + 1;
            var nx = Count(spanX);
            var nz = Count(spanZ);

            var points = new List<(int, int, float, float)>(nx * nz);
            for (var i = 0; i < nx; i++)
            {
                var x = nx > 1 ? minX + spanX * i / (nx - 1) : (minX + maxX) / 2.0;
                for (var j = 0; j < nz; j++)
                {
                    var z = nz > 1 ? minZ + spanZ * j / (nz - 1) : (minZ + maxZ) / 2.0;
                    points.Add((i, j, (float)x, (float)z));
                }
            }
            return points;
        }

        /// <summary>The ground each ray of a grid stands for: the footprint shared out among them.</summary>
        public static float CellArea(float minX, float maxX, float minZ, float maxZ, int rays)
        {
            if (rays <= 0) return 0f;
            var area = Math.Max(0f, maxX - minX) * Math.Max(0f, maxZ - minZ);
            return area > 0f ? area / rays : Spacing * Spacing;
        }
    }
}
