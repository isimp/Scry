using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The walkways of a dungeon's plan from above (<see cref="RoomWalkway"/>): each room's hall,
    /// and for each way between two rooms the doorway they share joined to each one's hall, so the
    /// plan's ways run along the rooms' walls. Worked out once for an example's ways; the plan
    /// only turns and scales them.
    /// </summary>
    internal sealed class PlanWalkways
    {
        /// <summary>A line of the walkways: the room it runs in, and the way it belongs to, or none for the room's hall; a way closing a loop marked so.</summary>
        internal struct Line
        {
            public Vec3 From, To;
            public PlacedRoom Room;
            public (PlacedRoom, PlacedRoom) Way;
            public bool Loop;
        }

        private readonly DungeonWays _ways;
        private readonly List<Line> _lines = new List<Line>();

        /// <summary>Every line of the walkways, each room's hall among them.</summary>
        public IReadOnlyList<Line> Lines => _lines;

        private PlanWalkways(DungeonWays ways) => _ways = ways;

        /// <summary>The walkways of an example's rooms and the ways between them.</summary>
        public static PlanWalkways Of(DungeonWays ways)
        {
            var walkways = new PlanWalkways(ways);
            var doors = new List<Vec3>();
            foreach (var room in ways.Rooms)
            {
                // The hall reaches as far as the ways on the way from the entrance; a way closing
                // a loop past its end goes on to its doorway's foot as part of that way.
                doors.Clear();
                foreach (var other in ways.WaysOf(room))
                {
                    if (!(ways.DoorBetween(room, other) is Vec3 door)) continue;
                    if (!ways.IsLoop(room, other)) doors.Add(door);
                    walkways.Add(room, other, RoomWalkway.Stub(room, door), ways.IsLoop(room, other));
                }
                var hall = RoomWalkway.HallOf(room, doors);
                if (Vec3.Distance(hall.From, hall.To) > 0.001f) walkways._lines.Add(new Line { From = hall.From, To = hall.To, Room = room });
                foreach (var other in ways.WaysOf(room))
                {
                    if (!ways.IsLoop(room, other) || !(ways.DoorBetween(room, other) is Vec3 door)) continue;
                    if (RoomWalkway.Beyond(room, hall, door) is (Vec3, Vec3) past) walkways.Add(room, other, new List<Vec3> { past.Item1, past.Item2 }, true);
                }
            }
            return walkways;
        }

        /// <summary>Adds the lines between points, one after the other, as part of a way.</summary>
        private void Add(PlacedRoom room, PlacedRoom other, List<Vec3> points, bool loop)
        {
            for (var i = 1; i < points.Count; i++)
                _lines.Add(new Line { From = points[i - 1], To = points[i], Room = room, Way = (room, other), Loop = loop });
        }

        /// <summary>The walkway along a way through rooms: from the first room's middle, through the doorway each two share, to the last room's middle.</summary>
        public List<Vec3> Route(IReadOnlyList<PlacedRoom> rooms)
        {
            var points = new List<Vec3>();
            foreach (var (point, _) in RouteIn(rooms)) points.Add(point);
            return points;
        }

        /// <summary>The same walkway, each point with the room it runs in: a doorway two rooms share with the first, where the way leaves it.</summary>
        public List<(Vec3 Point, PlacedRoom Room)> RouteIn(IReadOnlyList<PlacedRoom> rooms)
        {
            var points = new List<(Vec3, PlacedRoom)>();
            for (var i = 0; i < rooms.Count; i++)
            {
                var entry = i > 0 ? _ways.DoorBetween(rooms[i - 1], rooms[i]) : null;
                var exit = i < rooms.Count - 1 ? _ways.DoorBetween(rooms[i], rooms[i + 1]) : null;
                foreach (var point in RoomWalkway.Through(rooms[i], entry, exit))
                {
                    if (points.Count == 0 || Vec3.Distance(points[points.Count - 1].Item1, point) > 0.001f) points.Add((point, rooms[i]));
                }
            }
            return points;
        }
    }
}
