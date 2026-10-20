using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>One dungeon's line on its location's page: where it lies, and how far you and anyone on this server walked it.</summary>
    internal sealed class WalkLine
    {
        public string Id;

        /// <summary>Where its location stands, from your walk of it or the ground you know holding it, which the line tells from where you stand.</summary>
        public Vec3 Entrance;

        /// <summary>How far you walked it; none where you did not.</summary>
        public (int Walked, int Rooms)? Yours;
        public int YourFirst = -1, YourLast = -1;

        /// <summary>How far anyone on this server walked it, and how many of those rooms came of traces alone; none where the server keeps no walk of it shown.</summary>
        public (int Walked, int Rooms, int FromTraces)? Server;
        public int ServerFirst = -1, ServerLast = -1;

        /// <summary>What was left inside it, read on the server; none where nothing was.</summary>
        public TraceTally Tally;

        /// <summary>Whether you know where it lies: you walked it, or it lies in ground you know; else its place stays hidden.</summary>
        public bool Found;

        /// <summary>How far it lies from where you stand, across, in metres, and which way, from 0 for north round by east to 7.</summary>
        public float Distance;
        public int Direction;
    }

    /// <summary>
    /// The lines of a dungeon location's page for its dungeons walked in this world: one each,
    /// yours and the server's record of one dungeon on one line, those with no room walked left
    /// out. The server's records carry no place of the world (<see cref="DungeonMap.FromGenerator"/>):
    /// where one lies is known from your walk of it, or from the ground you know holding it, else
    /// it is not found. Those found stand nearest first, as you move, the others after in the order kept.
    /// </summary>
    internal static class WalkLines
    {
        /// <summary>How many lines show before the rest fold behind one chip.</summary>
        public const int FirstShown = 4;

        public static List<WalkLine> Of(IEnumerable<(string Id, DungeonMap Map, int Walked, int Rooms)> yours, IEnumerable<(string Id, DungeonMap Map, int Walked, int Rooms)> server,
            Func<string, Vec3?> knownAt, Vec3 player)
        {
            var lines = new List<WalkLine>();
            var byId = new Dictionary<string, WalkLine>(StringComparer.Ordinal);
            WalkLine LineOf(string id, DungeonMap map)
            {
                if (byId.TryGetValue(id, out var line)) return line;
                line = new WalkLine { Id = id, Entrance = map.Entrance };
                byId[id] = line;
                lines.Add(line);
                return line;
            }
            foreach (var (id, map, walked, rooms) in yours)
            {
                if (walked <= 0) continue;
                var line = LineOf(id, map);
                line.Yours = (walked, rooms);
                line.YourFirst = map.FirstDay;
                line.YourLast = map.LastDay;
                line.Found = true;
            }
            foreach (var (id, map, walked, rooms) in server)
            {
                if (walked <= 0) continue;
                var line = LineOf(id, map);
                line.Server = (walked, rooms, map.Traced.Count);
                line.ServerFirst = map.FirstDay;
                line.ServerLast = map.LastDay;
                line.Tally = map.Tally;
                if (line.Yours.HasValue || !(knownAt(id) is Vec3 at)) continue;
                line.Entrance = at;
                line.Found = true;
            }
            foreach (var line in lines)
            {
                line.Distance = DistanceOf(player, line.Entrance);
                line.Direction = DirectionOf(player, line.Entrance);
            }
            // The sort keeps the order of lines alike, so those not found stay as kept, telling nothing of where they lie.
            return lines.OrderBy(line => line.Found ? 0 : 1).ThenBy(line => line.Found ? line.Distance : 0f).ToList();
        }

        /// <summary>How far one point lies from another across the world, its height left out, as a dungeon's rooms stand far above its entrance.</summary>
        public static float DistanceOf(Vec3 from, Vec3 to)
        {
            var dx = to.X - from.X;
            var dz = to.Z - from.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Which way one point lies from another: 0 for north, the world's +z, round by east to 7, each a span of 45 degrees.</summary>
        public static int DirectionOf(Vec3 from, Vec3 to)
        {
            var degrees = Math.Atan2(to.X - from.X, to.Z - from.Z) * 180.0 / Math.PI;
            if (degrees < 0.0) degrees += 360.0;
            return (int)Math.Floor((degrees + 22.5) / 45.0) % 8;
        }
    }
}
