using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Where a person can walk in a place, from where it is come into, on what its rays met
    /// (<see cref="FloorRecord"/>): ground one stands on with <see cref="Headroom"/> over it,
    /// reached from the next ray's ground by climbing no more than <see cref="Step"/> (or the
    /// rise of a stair over the rays' spacing), or by dropping any height onto the first ground
    /// under one. A room is come into by its doorways, onto the first ground under each; a
    /// location from its ground at its root and from anything under the open sky. Only ground so
    /// reached makes floors, and what lies just under it (ice on a cave's rock): the tops of
    /// pillars, walls, altars and rock, and a deck with no way up, are none.
    /// </summary>
    internal static class FloorWalk
    {
        /// <summary>The most one climbs from one ray's ground to the next's, in metres, where the rays lie closer than that.</summary>
        public const float Step = 0.6f;

        /// <summary>The room over ground one needs to stand on it, in metres.</summary>
        public const float Headroom = 1.7f;

        /// <summary>How far under ground walked on what is flat still belongs to it, in metres.</summary>
        public const float Under = 0.75f;

        /// <summary>How far beside a doorway one comes in, in metres, and in rays at the least.</summary>
        public const float DoorReach = 1.5f;
        public const int DoorRays = 2;

        /// <summary>How far over a doorway the ground by it may be, in metres; under it, any height.</summary>
        public const float DoorRise = 1f;

        /// <summary>How near a location's root its ground is, in metres, where it is walked onto from the world's.</summary>
        public const float RootReach = 1f;

        /// <summary>Where a place is come into: a room's doorways, in its own frame; none for a location, come into from its ground and the open sky.</summary>
        internal sealed class Start
        {
            public IReadOnlyList<Vec3> Doorways { get; private set; }

            /// <summary>A location's: its ground at its root, and all it has under the open sky.</summary>
            public static Start Outdoors => new Start();

            /// <summary>A room's: by each of its doorways.</summary>
            public static Start At(IEnumerable<Vec3> doorways) => new Start { Doorways = doorways.ToList() };
        }

        /// <summary>
        /// The landings of a roll one can walk to from where the place is come into: every flat
        /// surface no more than <see cref="Under"/> under ground reached in its ray's column.
        /// </summary>
        public static List<FloorLanding> Landings(FloorRecord record, ISet<int> present, Start start)
        {
            var landings = new List<FloorLanding>();
            foreach (var _ in Walking(record, present, start, landings)) { }
            return landings;
        }

        /// <summary>How many rays, or steps of the walk, are taken at a time between waits.</summary>
        private const int AtOnce = 512;

        /// <summary>
        /// The walk of <see cref="Landings"/> in steps, the landings found added to
        /// <paramref name="into"/> at its end: so many rays, or steps from one surface to the
        /// next, at a time, nothing between them, so a fortress's thousands of rays are walked
        /// over frames. The landings are those of the walk done at once.
        /// </summary>
        public static IEnumerable<object> Walking(FloorRecord record, ISet<int> present, Start start, List<FloorLanding> into)
        {
            var grid = record.Grid;
            var columns = new List<RaySurface>[grid.Count];
            var at = new Dictionary<(int, int), int>(grid.Count);
            for (var ray = 0; ray < grid.Count; ray++)
            {
                if (ray > 0 && ray % AtOnce == 0) yield return null;
                columns[ray] = record.On(ray, present).ToList();
                at[(grid[ray].I, grid[ray].J)] = ray;
            }
            var spacing = (float)Math.Sqrt(record.Cell);
            var seeds = Seeds(columns, grid, start, spacing);
            yield return null;
            var reached = new Dictionary<int, List<float>>();
            foreach (var _ in Reaching(columns, at, grid, seeds, Math.Max(Step, spacing), reached)) yield return null;

            var looked = 0;
            foreach (var landing in record.Landings(present))
            {
                if (++looked % AtOnce == 0) yield return null;
                if (!at.TryGetValue((landing.I, landing.J), out var ray) || !reached.TryGetValue(ray, out var heights)) continue;
                if (heights.Any(h => landing.Height <= h + 0.05f && h - landing.Height <= Under)) into.Add(landing);
            }
        }

        /// <summary>Whether one stands on a surface of a column: nothing of the place within <see cref="Headroom"/> over it.</summary>
        private static bool Stands(List<RaySurface> column, int k) => k == 0 || column[k - 1].Height - column[k].Height >= Headroom;

        /// <summary>
        /// Where the walk starts. By each doorway, on every ray within reach of it, the first
        /// ground one stands on at most <see cref="DoorRise"/> over it, however far under; for a
        /// location, ground one stands on within <see cref="RootReach"/> of its root, and any
        /// under the open sky.
        /// </summary>
        private static List<(int Ray, int K)> Seeds(List<RaySurface>[] columns, IReadOnlyList<(int I, int J, float X, float Z)> grid, Start start, float spacing)
        {
            var seeds = new List<(int, int)>();
            if (start.Doorways == null)
            {
                for (var ray = 0; ray < columns.Length; ray++)
                {
                    var column = columns[ray];
                    for (var k = 0; k < column.Count; k++)
                    {
                        var open = FloorRecord.IsOpen(column[0].Height, column[k].Height);
                        if (Stands(column, k) && (open || Math.Abs(column[k].Height) <= RootReach)) seeds.Add((ray, k));
                    }
                }
                return seeds;
            }
            var reach = Math.Max(DoorReach, DoorRays * spacing);
            for (var ray = 0; ray < columns.Length; ray++)
            {
                foreach (var door in start.Doorways)
                {
                    var dx = grid[ray].X - door.X;
                    var dz = grid[ray].Z - door.Z;
                    if (dx * dx + dz * dz > reach * reach) continue;
                    var column = columns[ray];
                    for (var k = 0; k < column.Count; k++)
                    {
                        if (column[k].Height > door.Y + DoorRise || !Stands(column, k)) continue;
                        seeds.Add((ray, k));
                        break;
                    }
                }
            }
            return seeds;
        }

        /// <summary>
        /// The ground reached from the seeds, by ray, into <paramref name="reached"/> at its end:
        /// from ground one stands on to the next ray's, onto the first surface there no more than
        /// <paramref name="up"/> over it, where one stands; anything higher is in the way, anything
        /// lower is dropped onto. So many steps at a time, nothing between them.
        /// </summary>
        private static IEnumerable<object> Reaching(List<RaySurface>[] columns, Dictionary<(int, int), int> at, IReadOnlyList<(int I, int J, float X, float Z)> grid, List<(int Ray, int K)> seeds, float up,
            Dictionary<int, List<float>> reached)
        {
            var seen = new HashSet<(int, int)>();
            var next = new Queue<(int, int)>();
            foreach (var seed in seeds) if (seen.Add(seed)) next.Enqueue(seed);
            void StepTo(int i, int j, float height)
            {
                if (!at.TryGetValue((i, j), out var ray)) return;
                var column = columns[ray];
                for (var k = 0; k < column.Count; k++)
                {
                    if (column[k].Height > height + up) continue;
                    if (Stands(column, k) && seen.Add((ray, k))) next.Enqueue((ray, k));
                    return;
                }
            }
            var stepped = 0;
            while (next.Count > 0)
            {
                if (++stepped % AtOnce == 0) yield return null;
                var (ray, k) = next.Dequeue();
                var height = columns[ray][k].Height;
                var (i, j) = (grid[ray].I, grid[ray].J);
                StepTo(i - 1, j, height);
                StepTo(i + 1, j, height);
                StepTo(i, j - 1, height);
                StepTo(i, j + 1, height);
            }
            foreach (var (ray, k) in seen)
            {
                if (!reached.TryGetValue(ray, out var heights)) reached[ray] = heights = new List<float>();
                heights.Add(columns[ray][k].Height);
            }
        }
    }
}
