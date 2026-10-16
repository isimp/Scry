using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// How a dungeon example's rooms are joined: the game joins each room at an open doorway of
    /// those placed before it, so a dungeon branches, and at the end leaves two open doorways
    /// meeting at one spot open, a second way round, or closes them with a divider. Two rooms are
    /// joined where doorways of theirs lie within <see cref="Meeting"/> of each other, unless a
    /// divider stands there; end caps and dividers are no rooms. From the entrance, the room the
    /// example starts with, each room is so many rooms in, by the way through the fewest rooms.
    /// </summary>
    internal sealed class DungeonWays
    {
        /// <summary>How near two doorways must be to meet, in metres.</summary>
        public const float Meeting = 0.1f;

        private readonly List<PlacedRoom> _rooms = new List<PlacedRoom>();
        private readonly Dictionary<PlacedRoom, List<PlacedRoom>> _ways = new Dictionary<PlacedRoom, List<PlacedRoom>>();
        private readonly Dictionary<PlacedRoom, PlacedRoom> _cameFrom = new Dictionary<PlacedRoom, PlacedRoom>();
        private readonly Dictionary<PlacedRoom, int> _steps = new Dictionary<PlacedRoom, int>();
        private readonly Dictionary<PlacedRoom, int> _placed = new Dictionary<PlacedRoom, int>();

        private DungeonWays()
        {
        }

        /// <summary>The rooms, end caps and dividers left out, as placed.</summary>
        public IReadOnlyList<PlacedRoom> Rooms => _rooms;

        /// <summary>The room the example starts with (its place order 0), else its first room; none without rooms.</summary>
        public PlacedRoom Entrance { get; private set; }

        /// <summary>The lowest room reached from the entrance, by the foot of its box; of two as low, the nearer to the entrance, then the one placed first (the sort keeps the rooms' order).</summary>
        public PlacedRoom Deepest { get; private set; }

        /// <summary>How many second ways round there are: ways beyond those joining every room once.</summary>
        public int Loops { get; private set; }

        /// <summary>The ways of an example's rooms.</summary>
        public static DungeonWays Of(IReadOnlyList<PlacedRoom> placed)
        {
            var ways = new DungeonWays();
            ways._rooms.AddRange(placed.Where(r => !r.Room.EndCap && !r.Room.Divider));
            for (var i = 0; i < ways._rooms.Count; i++)
            {
                ways._ways[ways._rooms[i]] = new List<PlacedRoom>();
                ways._placed[ways._rooms[i]] = i;
            }
            ways.Join(placed);
            ways.Entrance = ways._rooms.FirstOrDefault(r => r.PlaceOrder == 0) ?? ways._rooms.FirstOrDefault();
            ways.Walk();
            ways.Deepest = ways._rooms.Where(ways._steps.ContainsKey)
                .OrderBy(Foot).ThenBy(r => ways._steps[r])
                .FirstOrDefault();
            return ways;
        }

        /// <summary>The rooms a room is joined to, in the order they were placed.</summary>
        public IReadOnlyList<PlacedRoom> WaysOf(PlacedRoom room) => _ways.TryGetValue(room, out var ways) ? ways : new List<PlacedRoom>();

        /// <summary>How much higher a room's foot must be than another's to stand over it, in metres.</summary>
        public const float Over = 2f;

        /// <summary>
        /// Whether the example is mostly one stack of rooms, as Mörkhalla is: no crossroads, and
        /// more than half its rooms under one whose foot is at least <see cref="Over"/> higher.
        /// Such an example is seen best cut from the side (<see cref="SideCut"/>).
        /// </summary>
        public bool IsStack => !_rooms.Any(IsCrossroads) && _rooms.Count(Hidden) * 2 > _rooms.Count;

        /// <summary>Whether a room stands under another, its middle seen from above inside the other's floor.</summary>
        private bool Hidden(PlacedRoom room) => _rooms.Any(other => other != room && Foot(other) >= Foot(room) + Over && other.Covers(room.Position.X, room.Position.Z));

        /// <summary>Whether a room has three ways or more.</summary>
        public bool IsCrossroads(PlacedRoom room) => WaysOf(room).Count >= 3;

        /// <summary>Whether a room other than the entrance has one way only.</summary>
        public bool IsDeadEnd(PlacedRoom room) => room != Entrance && WaysOf(room).Count == 1;

        /// <summary>How many rooms in from the entrance a room is, by the way through the fewest; -1 where no way reaches it.</summary>
        public int StepsIn(PlacedRoom room) => _steps.TryGetValue(room, out var steps) ? steps : -1;

        /// <summary>The rooms from the entrance to a room, both included, through the fewest; none where no way reaches it.</summary>
        public List<PlacedRoom> Route(PlacedRoom room)
        {
            var route = new List<PlacedRoom>();
            if (!_steps.ContainsKey(room)) return route;
            for (var at = room; at != null; at = _cameFrom.TryGetValue(at, out var from) ? from : null) route.Add(at);
            route.Reverse();
            return route;
        }

        private static float Foot(PlacedRoom room) => room.Position.Y - room.Room.Size.Y / 2f;

        /// <summary>Joins each two rooms with doorways meeting where no divider stands, and counts the ways round.</summary>
        private void Join(IReadOnlyList<PlacedRoom> placed)
        {
            var dividers = new List<Vec3>();
            var doorways = new List<(Vec3 At, PlacedRoom Room)>();
            foreach (var room in placed)
            {
                for (var i = 0; i < room.Room.Doorways.Count; i++)
                {
                    var at = room.DoorwayAt(i);
                    if (room.Room.Divider) dividers.Add(at);
                    else if (!room.Room.EndCap) doorways.Add((at, room));
                }
            }

            var joins = 0;
            for (var a = 0; a < doorways.Count; a++)
            {
                for (var b = a + 1; b < doorways.Count; b++)
                {
                    var (at, room) = doorways[a];
                    var other = doorways[b].Room;
                    if (room == other || Vec3.Distance(at, doorways[b].At) >= Meeting) continue;
                    if (dividers.Any(d => Vec3.Distance(d, at) < Meeting)) continue;
                    if (_ways[room].Contains(other)) continue;
                    _ways[room].Add(other);
                    _ways[other].Add(room);
                    joins++;
                }
            }
            foreach (var list in _ways.Values) list.Sort((x, y) => _placed[x].CompareTo(_placed[y]));

            var parts = 0;
            var seen = new HashSet<PlacedRoom>();
            foreach (var room in _rooms)
            {
                if (!seen.Add(room)) continue;
                parts++;
                var stack = new Stack<PlacedRoom>();
                stack.Push(room);
                while (stack.Count > 0)
                {
                    foreach (var next in _ways[stack.Pop()]) if (seen.Add(next)) stack.Push(next);
                }
            }
            Loops = joins - (_rooms.Count - parts);
        }

        /// <summary>Walks out from the entrance, a room's ways in the order they were placed, so each room is reached by the way through the fewest rooms.</summary>
        private void Walk()
        {
            if (Entrance == null) return;
            var queue = new Queue<PlacedRoom>();
            _steps[Entrance] = 0;
            queue.Enqueue(Entrance);
            while (queue.Count > 0)
            {
                var room = queue.Dequeue();
                foreach (var next in _ways[room])
                {
                    if (_steps.ContainsKey(next)) continue;
                    _steps[next] = _steps[room] + 1;
                    _cameFrom[next] = room;
                    queue.Enqueue(next);
                }
            }
        }
    }
}
