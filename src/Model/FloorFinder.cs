using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Where a ray cast straight down landed on something flat: the patch of rays it belongs to
    /// (each room of an example is probed on its own grid), its place on that grid, the height,
    /// the ground it stands for, and whether it is open, with nothing of the place above it.
    /// </summary>
    public struct FloorHit
    {
        public int Patch, I, J;
        public float Height, Area;
        public bool Open;
    }

    /// <summary>
    /// What one patch of rays found, height band by height band: the room of its spots with flat
    /// ground all round, the heights that landed there, and whether a ray landed in the band
    /// itself. A place's floors are found from its patches added up, so an example's rooms are
    /// each read once, as they come in, however many there are.
    /// </summary>
    public sealed class FloorPatch
    {
        internal struct Band
        {
            public float Room;
            public double Sum;
            public int Count;
            public bool Landed;
        }

        internal readonly Dictionary<int, Band> Bands = new Dictionary<int, Band>();
    }

    /// <summary>
    /// A place's floors found in the place itself: rays cast straight down over it on a grid land
    /// on what is flat, floors, landings and platforms, but also tables, beds and beams. Only
    /// ground one can stand on makes a floor: a spot counts where the spots beside it, all four
    /// ways, landed at the same height too, so a beam, a table or a bed adds nothing; those spots
    /// must hold <see cref="MinRoom"/> and a share of the place's ground. What is open, with
    /// nothing of the place above it (a flat roof's top, a cave's rock over its hollow, a tower's
    /// open deck), is seen with the roof on and is no floor to cut to. Heights closer than
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
        public static List<float> Floors(IReadOnlyList<FloorHit> hits, float footprint) =>
            Floors(hits.GroupBy(h => h.Patch).Select(Patch).ToList(), footprint);

        /// <summary>
        /// What one patch's rays found, band by band (<see cref="FloorPatch"/>). A spot counts in a
        /// band, and within a band either way, where the spots beside it all four ways landed
        /// there too, so a floor lying across two bands counts whole; each spot's ground is
        /// counted once, however many of its rays lie in the bands.
        /// </summary>
        public static FloorPatch Patch(IEnumerable<FloorHit> hits)
        {
            var byBand = new Dictionary<int, List<FloorHit>>();
            var spots = new Dictionary<int, HashSet<(int, int)>>();
            foreach (var hit in hits)
            {
                if (hit.Open) continue;
                var band = (int)Math.Round(hit.Height / Band);
                if (!byBand.TryGetValue(band, out var held))
                {
                    byBand[band] = held = new List<FloorHit>();
                    spots[band] = new HashSet<(int, int)>();
                }
                held.Add(hit);
                spots[band].Add((hit.I, hit.J));
            }

            var bands = new HashSet<int>();
            foreach (var band in byBand.Keys) for (var b = band - 1; b <= band + 1; b++) bands.Add(b);

            var patch = new FloorPatch();
            var counted = new HashSet<(int, int)>();
            foreach (var band in bands)
            {
                bool Near(int i, int j)
                {
                    for (var b = band - 1; b <= band + 1; b++) if (spots.TryGetValue(b, out var at) && at.Contains((i, j))) return true;
                    return false;
                }

                var found = new FloorPatch.Band { Landed = byBand.ContainsKey(band) };
                counted.Clear();
                for (var b = band - 1; b <= band + 1; b++)
                {
                    if (!byBand.TryGetValue(b, out var held)) continue;
                    foreach (var hit in held)
                    {
                        if (!Near(hit.I - 1, hit.J) || !Near(hit.I + 1, hit.J) || !Near(hit.I, hit.J - 1) || !Near(hit.I, hit.J + 1)) continue;
                        found.Sum += hit.Height;
                        found.Count++;
                        if (counted.Add((hit.I, hit.J))) found.Room += hit.Area;
                    }
                }
                if (found.Count > 0) patch.Bands[band] = found;
            }
            return patch;
        }

        /// <summary>How near a floor a room's own ground must be for the room to stand on it, in metres.</summary>
        public const float Holding = 1f;

        /// <summary>
        /// Whether a patch's rays found ground one can stand on within <see cref="Holding"/> of a
        /// floor: a room of an example stands on a floor its own rays found ground on, whatever
        /// box the game sizes the room by.
        /// </summary>
        public static bool Holds(FloorPatch patch, float floor)
        {
            // A patch keeps only bands its rays found ground in.
            foreach (var band in patch.Bands.Values)
            {
                if (Math.Abs(band.Sum / band.Count - floor) <= Holding) return true;
            }
            return false;
        }

        /// <summary>The floors from the top down from what each patch of rays found, and the ground they were cast over.</summary>
        public static List<float> Floors(IEnumerable<FloorPatch> patches, float footprint)
        {
            var least = Math.Max(MinRoom, footprint * MinShare);
            var total = new Dictionary<int, FloorPatch.Band>();
            foreach (var patch in patches)
            {
                foreach (var pair in patch.Bands)
                {
                    total.TryGetValue(pair.Key, out var sum);
                    sum.Room += pair.Value.Room;
                    sum.Sum += pair.Value.Sum;
                    sum.Count += pair.Value.Count;
                    sum.Landed |= pair.Value.Landed;
                    total[pair.Key] = sum;
                }
            }

            // A floor is in a band some ray landed in.
            var candidates = new List<(int Band, float Room, float Height)>();
            foreach (var pair in total)
            {
                var band = pair.Value;
                if (!band.Landed || band.Count == 0 || band.Room < least) continue;
                candidates.Add((pair.Key, band.Room, (float)(band.Sum / band.Count)));
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
