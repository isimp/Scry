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
    internal struct FloorHit
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
    internal sealed class FloorPatch
    {
        internal struct Band
        {
            public float Room;
            public double Sum;
            public int Count;
            public bool Landed;
        }

        internal readonly Dictionary<int, Band> Bands = new Dictionary<int, Band>();

        /// <summary>The ground its rays were cast over, in square metres; 0 where it is not known.</summary>
        public float Ground;

        /// <summary>
        /// The highest doorway of the dungeon room its rays were cast over: ground more than
        /// <see cref="FloorFinder.AboveDoors"/> over it is out of the room's reach. None for a location.
        /// </summary>
        public float Door = float.PositiveInfinity;
    }

    /// <summary>The rules a dungeon room's own ground is read by: the room a level of its own needs, and how far over its highest doorway its ground is out of reach.</summary>
    internal readonly struct FloorRules
    {
        public FloorRules(float ownRoom, float aboveDoors)
        {
            OwnRoom = ownRoom;
            AboveDoors = aboveDoors;
        }

        public float OwnRoom { get; }
        public float AboveDoors { get; }

        /// <summary>The rules now (<see cref="FloorFinder.OwnLevelRoom"/>, <see cref="FloorFinder.AboveDoors"/>).</summary>
        public static readonly FloorRules Now = new FloorRules(FloorFinder.OwnLevelRoom, FloorFinder.AboveDoors);

        /// <summary>The rules before 2026-10-05: a level of a room's own 2 square metres at the least, its ground at any height; the self-test tells what they found, with the room's open ground left out as it was, beside those now.</summary>
        public static readonly FloorRules Before = new FloorRules(FloorFinder.MinRoom, float.PositiveInfinity);
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
    internal static class FloorFinder
    {
        /// <summary>The height band rays are counted in, in metres; a spot's neighbours count within a band either way.</summary>
        public const float Band = 0.25f;

        /// <summary>The room a floor needs at the least, in square metres of spots with flat ground all round, and its share of the place's ground.</summary>
        public const float MinRoom = 2f;
        public const float MinShare = 0.015f;

        /// <summary>
        /// The room a level of a dungeon room's own needs at the least, in square metres, besides
        /// its share of the room's ground: a ledge, a tomb's top or a step in a shaft is less.
        /// </summary>
        public const float OwnLevelRoom = 6f;

        /// <summary>
        /// How far over a dungeon room's highest doorway its ground is its own, in metres: a sealed
        /// tower's storey stands by its doorway up, while a frost cave's rock top, a sunken crypt's
        /// ledge or the ground round M&#xF6;rkhalla's gate is far over every one.
        /// </summary>
        public const float AboveDoors = 2f;

        /// <summary>How far under an entrance's doorway out open ground is still the ground outside, in metres.</summary>
        public const float Outdoors = 1f;

        /// <summary>Whether a band of a patch is ground of its room: no more than <see cref="FloorRules.AboveDoors"/> over its highest doorway.</summary>
        private static bool Stands(FloorPatch patch, FloorPatch.Band band, FloorRules rules) =>
            band.Count > 0 && band.Sum / band.Count <= patch.Door + rules.AboveDoors;

        /// <summary>
        /// A dungeon room's hits with what is open, nothing of the room over it, taken as its
        /// ground where it is inside: read alone, a room whose ceiling is the room above (each of
        /// M&#xF6;rkhalla's) has nothing of its own over its floor. Open ground stays open high over
        /// every doorway (<see cref="AboveDoors"/>, the top of its rock) and, in the entrance, near
        /// or over its doorway out (<see cref="Outdoors"/>, the ground outside); none for a room
        /// with no doorway out (<paramref name="outerDoor"/> infinite).
        /// </summary>
        public static List<FloorHit> Inside(IEnumerable<FloorHit> hits, float door, float outerDoor)
        {
            var inside = new List<FloorHit>();
            foreach (var hit in hits)
            {
                var each = hit;
                if (each.Open && each.Height <= door + AboveDoors && each.Height < outerDoor - Outdoors) each.Open = false;
                inside.Add(each);
            }
            return inside;
        }

        /// <summary>Whether a band of a patch is a level of its room's own: ground of it, with its share of the room's ground and room to stand.</summary>
        private static bool OwnLevel(FloorPatch patch, FloorPatch.Band band, FloorRules rules) =>
            band.Room >= Math.Max(rules.OwnRoom, patch.Ground * MinShare) && Stands(patch, band, rules);

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

        /// <summary>
        /// Where a patch has the most room to stand, on average, with at least <see cref="MinRoom"/>
        /// of it: a room's own main floor. Null where it has nowhere so big.
        /// </summary>
        public static float? MainFloor(FloorPatch patch)
        {
            if (!(MainBand(patch, FloorRules.Now) is int main)) return null;
            var band = patch.Bands[main];
            return (float)(band.Sum / band.Count);
        }

        private static int? MainBand(FloorPatch patch, FloorRules rules)
        {
            int? best = null;
            var most = 0f;
            foreach (var pair in patch.Bands)
            {
                var band = pair.Value;
                if (!band.Landed || !Stands(patch, band, rules) || band.Room < MinRoom || band.Room <= most) continue;
                best = pair.Key;
                most = band.Room;
            }
            return best;
        }

        /// <summary>How near a floor a room's own ground must be for the room to stand on it, in metres.</summary>
        public const float Holding = 1f;

        /// <summary>
        /// Whether a patch's rays found ground one can stand on within <see cref="Holding"/> of a
        /// floor: a room of an example stands on a floor its own rays found ground on, whatever
        /// box the game sizes the room by, but not on the top of its rock.
        /// </summary>
        public static bool Holds(FloorPatch patch, float floor)
        {
            // A patch keeps only bands its rays found ground in.
            foreach (var band in patch.Bands.Values)
            {
                if (Stands(patch, band, FloorRules.Now) && Math.Abs(band.Sum / band.Count - floor) <= Holding) return true;
            }
            return false;
        }

        /// <summary>
        /// The floors from the top down from what each patch of rays found, and the ground they
        /// were cast over. With <paramref name="storey"/>, ground less than that below the next,
        /// step after step, is one floor, at the ground with the most room (an example's rooms,
        /// <see cref="PlaceView.Storey"/>), as far down as <see cref="PlaceView.LevelSpan"/> from
        /// its top: a cave's long slope is a floor that much apart, so none of its ground stands
        /// above its floor's cut. A patch's own floors are floors however little of all
        /// the ground they are, as they are when its room is shown alone: its main ground
        /// (<see cref="MainFloor"/>), and with its own ground known, each level with its share of
        /// that, as big as <see cref="OwnLevelRoom"/>. In a big example, a deep chamber's floor or
        /// a hall's gallery is a sliver of the ground of all its rooms, but a level of its own
        /// room. A dungeon room's ground high over its doorways is none of it but in its entrance
        /// (<see cref="FloorPatch.Door"/>).
        /// </summary>
        public static List<float> Floors(IEnumerable<FloorPatch> patches, float footprint, float storey = 0f) =>
            Floors(patches, footprint, storey, FloorRules.Now);

        /// <summary>The floors as <see cref="Floors(IEnumerable{FloorPatch}, float, float)"/> finds them by other rules, for the self-test to tell what the rules before found.</summary>
        public static List<float> Floors(IEnumerable<FloorPatch> patches, float footprint, float storey, FloorRules rules)
        {
            var least = Math.Max(MinRoom, footprint * MinShare);
            var total = new Dictionary<int, FloorPatch.Band>();
            var mains = new HashSet<int>();
            foreach (var patch in patches)
            {
                if (MainBand(patch, rules) is int main) mains.Add(main);
                if (patch.Ground > 0f)
                {
                    foreach (var pair in patch.Bands) if (OwnLevel(patch, pair.Value, rules)) mains.Add(pair.Key);
                }
                foreach (var pair in patch.Bands)
                {
                    if (!Stands(patch, pair.Value, rules)) continue;
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
                if (!band.Landed || band.Count == 0 || band.Room < least && !mains.Contains(pair.Key)) continue;
                candidates.Add((pair.Key, band.Room, (float)(band.Sum / band.Count)));
            }

            // Ground stepping down less than a storey at a time, within a level's span of its top:
            // the one with the most room stands for it.
            if (candidates.Count > 1)
            {
                var steps = candidates.OrderByDescending(c => c.Height).ToList();
                var kept = new List<(int Band, float Room, float Height)>();
                var best = steps[0];
                var top = steps[0].Height;
                for (var i = 1; i < steps.Count; i++)
                {
                    if (steps[i - 1].Height - steps[i].Height < storey && top - steps[i].Height < PlaceView.LevelSpan)
                    {
                        if (steps[i].Room > best.Room) best = steps[i];
                        continue;
                    }
                    kept.Add(best);
                    best = steps[i];
                    top = steps[i].Height;
                }
                kept.Add(best);
                candidates = kept;
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
