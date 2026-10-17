using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The levels of a dungeon's example, as the plan box shows them: each a plan from above, the
    /// highest first. Going down from the highest room, a room joins the level above it while its
    /// ground lies within two storeys under that level's lowest and it covers none of the level's
    /// rooms from above; else it begins a level of its own under it. So rooms stepping down a
    /// little at a time share a level, a room far under the rest stands on its own, and no room of
    /// a level covers another, each showing whole.
    /// </summary>
    internal sealed class PlanLevels
    {
        /// <summary>How far under a level's lowest room a room's ground may lie and still join it: two storeys.</summary>
        public const float Apart = 5f;

        /// <summary>How far two rooms must reach into each other from above to cover each other, so rooms wall to wall do not.</summary>
        public const float Touch = 0.05f;

        /// <summary>A level: its rooms, highest first, and its highest and lowest ground.</summary>
        public sealed class Level
        {
            public readonly List<PlacedRoom> Rooms = new List<PlacedRoom>();
            public float Top { get; internal set; }
            public float Bottom { get; internal set; }
        }

        private readonly List<Level> _levels = new List<Level>();
        private readonly Dictionary<PlacedRoom, int> _levelOf = new Dictionary<PlacedRoom, int>();

        /// <summary>The levels, highest first.</summary>
        public IReadOnlyList<Level> Levels => _levels;

        /// <summary>A room's level, from 0 at the highest; -1 for a room not among them.</summary>
        public int LevelOf(PlacedRoom room) => _levelOf.TryGetValue(room, out var level) ? level : -1;

        /// <summary>Lays the rooms out on levels by their grounds, each room's ground at the same place in <paramref name="grounds"/>.</summary>
        public static PlanLevels Of(IReadOnlyList<PlacedRoom> rooms, IReadOnlyList<float> grounds)
        {
            var levels = new PlanLevels();
            var order = Enumerable.Range(0, rooms.Count).OrderByDescending(i => grounds[i]).ThenBy(i => i);
            foreach (var i in order)
            {
                var room = rooms[i];
                var ground = grounds[i];
                var last = levels._levels.Count > 0 ? levels._levels[levels._levels.Count - 1] : null;
                if (last == null || last.Bottom - ground > Apart || last.Rooms.Any(r => Covers(r, room)))
                {
                    last = new Level { Top = ground };
                    levels._levels.Add(last);
                }
                last.Rooms.Add(room);
                last.Bottom = ground;
                levels._levelOf[room] = levels._levels.Count - 1;
            }
            return levels;
        }

        /// <summary>Whether two rooms reach into each other seen from above, by the boxes their corners span, more than <see cref="Touch"/>.</summary>
        public static bool Covers(PlacedRoom a, PlacedRoom b)
        {
            var (aMinX, aMaxX, aMinZ, aMaxZ) = Spans(a);
            var (bMinX, bMaxX, bMinZ, bMaxZ) = Spans(b);
            return aMaxX - Touch > bMinX && bMaxX - Touch > aMinX && aMaxZ - Touch > bMinZ && bMaxZ - Touch > aMinZ;
        }

        private static (float MinX, float MaxX, float MinZ, float MaxZ) Spans(PlacedRoom room)
        {
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            foreach (var corner in room.Corners())
            {
                minX = Math.Min(minX, corner.X);
                maxX = Math.Max(maxX, corner.X);
                minZ = Math.Min(minZ, corner.Z);
                maxZ = Math.Max(maxZ, corner.Z);
            }
            return (minX, maxX, minZ, maxZ);
        }
    }
}
