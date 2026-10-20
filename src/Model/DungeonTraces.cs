using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What was left inside a dungeon that tells someone was there.</summary>
    internal enum TraceKind
    {
        /// <summary>A pickup taken: picked, or gone where the game takes it away on picking.</summary>
        Pickup,

        /// <summary>A chest emptied, or gone where it takes itself away once empty.</summary>
        Chest,

        /// <summary>A thing broken: gone from where its room made it.</summary>
        Broken,

        /// <summary>A rock mined away.</summary>
        Mined,

        /// <summary>A door opened, standing in the doorway two rooms share.</summary>
        Door,

        /// <summary>A spawn point woken, which it does for anyone within its reach.</summary>
        Spawner,
    }

    /// <summary>One thing left inside a dungeon, where it lies, and the game's day it was left on; -1 where that is not known.</summary>
    internal readonly struct Trace
    {
        public Trace(TraceKind kind, Vec3 at, int day = -1)
        {
            Kind = kind;
            At = at;
            Day = day;
        }

        public TraceKind Kind { get; }
        public Vec3 At { get; }
        public int Day { get; }
    }

    /// <summary>How much of each kind was left inside a dungeon, in how many rooms, and the latest day known of it; for the words.</summary>
    internal sealed class TraceTally
    {
        public int Pickups, Chests, Broken, Mined, Doors, Spawners;

        /// <summary>How many rooms hold a trace of their own, doors and spawn points left out.</summary>
        public int Rooms;

        /// <summary>The latest game's day known of a trace; -1 where none is.</summary>
        public int LastDay = -1;

        public bool Same(TraceTally other) => other != null && ToField() == other.ToField();

        /// <summary>The tally as one field of the maps file, its counts and day by commas.</summary>
        public string ToField() => string.Join(",", new[] { Pickups, Chests, Broken, Mined, Doors, Spawners, Rooms, LastDay }.Select(n => Stored.Count(n)));

        /// <summary>A tally read back from <see cref="ToField"/>; false for one that cannot be read.</summary>
        public static bool TryField(string field, out TraceTally tally)
        {
            tally = null;
            var parts = field.Split(',');
            if (parts.Length != 8) return false;
            var counts = new int[8];
            for (var i = 0; i < 8; i++) if (!Stored.TryCount(parts[i], out counts[i])) return false;
            tally = new TraceTally { Pickups = counts[0], Chests = counts[1], Broken = counts[2], Mined = counts[3], Doors = counts[4], Spawners = counts[5], Rooms = counts[6], LastDay = counts[7] };
            return true;
        }
    }

    /// <summary>
    /// What a dungeon's traces tell of who was in it, though not who (the game keeps no maker of
    /// what is left): a pickup taken, a chest emptied, a thing broken or a rock mined, the room it
    /// lies in; a door opened, the room on its entrance side, whoever opened it having come from
    /// there; a spawn point woken, only that someone was inside, as it wakes for anyone within 60
    /// metres. Every visit starts at the entrance, so the way to each such room was walked too
    /// (<see cref="Uncovered"/>).
    /// </summary>
    internal sealed class DungeonTraces
    {
        private readonly DungeonWays _ways;
        private readonly SortedSet<int> _inRooms = new SortedSet<int>();
        private readonly SortedSet<int> _upTo = new SortedSet<int>();

        private DungeonTraces(DungeonWays ways) => _ways = ways;

        /// <summary>The rooms holding a trace of their own, by their place in the saved rooms.</summary>
        public IReadOnlyCollection<int> InRooms => _inRooms;

        /// <summary>The rooms a door opened leads on from, by their place in the saved rooms.</summary>
        public IReadOnlyCollection<int> UpTo => _upTo;

        /// <summary>Whether any trace tells that someone was inside.</summary>
        public bool Inside { get; private set; }

        public TraceTally Tally { get; } = new TraceTally();

        /// <summary>How near a door stands to the doorway it closes, in metres: the game makes it there.</summary>
        private const float AtDoorway = 0.5f;

        /// <summary>
        /// The traces of a dungeon by the rooms they tell; after the world's records were forgotten
        /// on a day, only those of that day or later, a trace of no known day perhaps being from
        /// before it, so a forget sticks; one in no room tells nothing.
        /// </summary>
        public static DungeonTraces Of(DungeonWays ways, IEnumerable<Trace> traces, int forgottenDay)
        {
            var found = new DungeonTraces(ways);
            foreach (var trace in traces)
            {
                if (forgottenDay >= 0 && trace.Day < forgottenDay) continue;
                if (trace.Kind == TraceKind.Spawner)
                {
                    if (Holding(ways, trace.At) == null) continue;
                    found.Tally.Spawners++;
                }
                else if (trace.Kind == TraceKind.Door)
                {
                    var side = EntranceSide(ways, trace.At);
                    if (side == null) continue;
                    found._upTo.Add(side.SavedAt);
                    found.Tally.Doors++;
                }
                else
                {
                    var room = Holding(ways, trace.At);
                    if (room == null) continue;
                    found._inRooms.Add(room.SavedAt);
                    found.Count(trace.Kind);
                }
                found.Inside = true;
                if (trace.Day > found.Tally.LastDay) found.Tally.LastDay = trace.Day;
            }
            found.Tally.Rooms = found._inRooms.Count;
            return found;
        }

        /// <summary>
        /// Takes in what was read of the dungeon before, kept in its record: the rooms that held
        /// traces and those doors opened lead on from, and of each kind as much as was ever read at
        /// once. A mod that resets dungeons makes all in one again, chests filled and pots whole, so
        /// what was read before stays though its traces are gone; without one, nothing goes.
        /// </summary>
        public void Keep(DungeonMap record)
        {
            if (record?.Tally == null) return;
            _inRooms.UnionWith(record.TraceRooms);
            _upTo.UnionWith(record.TraceDoors);
            Inside = true;
            var kept = record.Tally;
            Tally.Pickups = Math.Max(Tally.Pickups, kept.Pickups);
            Tally.Chests = Math.Max(Tally.Chests, kept.Chests);
            Tally.Broken = Math.Max(Tally.Broken, kept.Broken);
            Tally.Mined = Math.Max(Tally.Mined, kept.Mined);
            Tally.Doors = Math.Max(Tally.Doors, kept.Doors);
            Tally.Spawners = Math.Max(Tally.Spawners, kept.Spawners);
            Tally.LastDay = Math.Max(Tally.LastDay, kept.LastDay);
            Tally.Rooms = _inRooms.Count;
        }

        private void Count(TraceKind kind)
        {
            if (kind == TraceKind.Pickup) Tally.Pickups++;
            else if (kind == TraceKind.Chest) Tally.Chests++;
            else if (kind == TraceKind.Broken) Tally.Broken++;
            else if (kind == TraceKind.Mined) Tally.Mined++;
        }

        /// <summary>The room whose box holds a point; of boxes overlapping, the one whose foot is nearest under it, as <see cref="SavedRooms.Holding"/>; none in none.</summary>
        private static PlacedRoom Holding(DungeonWays ways, Vec3 point)
        {
            PlacedRoom found = null;
            var highest = float.NegativeInfinity;
            foreach (var room in ways.Rooms)
            {
                var local = room.Rotation.Inverse() * (point - room.Position);
                var half = room.Room.Size * 0.5f;
                if (Math.Abs(local.X) > half.X || Math.Abs(local.Y) > half.Y || Math.Abs(local.Z) > half.Z) continue;
                var foot = room.Position.Y - half.Y;
                if (foot <= highest) continue;
                highest = foot;
                found = room;
            }
            return found;
        }

        /// <summary>Of the rooms whose doorway a door stands in, the one fewer rooms in from the entrance; where it stands in none's, the room holding it.</summary>
        private static PlacedRoom EntranceSide(DungeonWays ways, Vec3 door)
        {
            PlacedRoom side = null;
            foreach (var room in ways.Rooms)
            {
                var steps = ways.StepsIn(room);
                if (steps < 0) continue;
                for (var i = 0; i < room.Room.Doorways.Count; i++)
                {
                    if (Vec3.Distance(room.DoorwayAt(i), door) > AtDoorway) continue;
                    if (side == null || steps < ways.StepsIn(side)) side = room;
                    break;
                }
            }
            return side ?? Holding(ways, door);
        }

        /// <summary>
        /// The rooms the traces uncover, by their place in the saved rooms: each room holding a trace
        /// and each a door opened leads on from, and the way to each from the entrance, the
        /// entrance alone where only a spawn point woke; the way needing the fewest rooms nobody is
        /// known to have walked, the rooms walked and those holding traces known, then the fewest
        /// rooms; where ways as good part, the one going on through the room placed first; each way
        /// known once taken for those after. A walk round the other side of a loop so takes a guess
        /// on this side back.
        /// </summary>
        public SortedSet<int> Uncovered(ICollection<int> walked)
        {
            var uncovered = new SortedSet<int>();
            var entrance = _ways.Entrance;
            if (!Inside || entrance == null) return uncovered;
            uncovered.Add(entrance.SavedAt);
            var known = new HashSet<int>(walked);
            known.UnionWith(_inRooms);
            var targets = _ways.Rooms.Where(r => _inRooms.Contains(r.SavedAt) || _upTo.Contains(r.SavedAt)).ToList();
            foreach (var target in targets)
            {
                foreach (var room in WayTo(target, known))
                {
                    uncovered.Add(room.SavedAt);
                    known.Add(room.SavedAt);
                }
            }
            return uncovered;
        }

        /// <summary>
        /// The rooms from the entrance to a room: the way needing the fewest rooms not known, then the
        /// fewest rooms; where ways as good part, the one going on through the room placed first; the
        /// room alone where no way reaches it.
        /// </summary>
        private List<PlacedRoom> WayTo(PlacedRoom target, HashSet<int> known)
        {
            int Cost(PlacedRoom room) => known.Contains(room.SavedAt) ? 0 : 1;
            // What each room's best way on to the room needs, worked back from it: rooms not known, then rooms.
            var left = new Dictionary<PlacedRoom, (int Unknown, int Rooms)> { [target] = (Cost(target), 1) };
            var open = new List<PlacedRoom> { target };
            while (open.Count > 0)
            {
                var at = open.OrderBy(r => left[r]).First();
                open.Remove(at);
                foreach (var other in _ways.WaysOf(at))
                {
                    var through = (left[at].Unknown + Cost(other), left[at].Rooms + 1);
                    if (left.TryGetValue(other, out var had) && had.CompareTo(through) <= 0) continue;
                    left[other] = through;
                    if (!open.Contains(other)) open.Add(other);
                }
            }
            var entrance = _ways.Entrance;
            if (!left.ContainsKey(entrance)) return new List<PlacedRoom> { target };
            // From the entrance on, at each room the first way placed that keeps to the best.
            var way = new List<PlacedRoom> { entrance };
            for (var here = entrance; here != target;)
            {
                var on = (left[here].Unknown - Cost(here), left[here].Rooms - 1);
                here = _ways.WaysOf(here).First(w => left.TryGetValue(w, out var next) && next == on);
                way.Add(here);
            }
            return way;
        }

        /// <summary>The seed a room's random parts are rolled from (<c>DungeonGenerator.PlaceRoom</c>): its place, from the generator where the location sets its own interior, summed as the game sums it, wrapping round, the base seed added where the generator adds it.</summary>
        public static int RoomSeed(Vec3 room, Vec3 generator, bool customInterior, int baseSeed)
        {
            var at = customInterior ? room - generator : room;
            return unchecked((int)at.X * 4271 + (int)at.Y * 9187 + (int)at.Z * 2134 + baseSeed);
        }

        /// <summary>How near a part saved where its room made it stands to that place, in metres: what the traces look for never moves.</summary>
        private const float StillThere = 0.3f;

        /// <summary>The parts rooms made, each of a prefab where it was made and the trace it leaves gone, that stand there no longer: no object of its prefab within <see cref="StillThere"/>.</summary>
        public static List<Trace> Gone(IEnumerable<(int Prefab, Vec3 At, TraceKind Kind)> made, IEnumerable<(int Prefab, Vec3 At)> standing)
        {
            var byPrefab = new Dictionary<int, List<Vec3>>();
            foreach (var (prefab, at) in standing)
            {
                if (!byPrefab.TryGetValue(prefab, out var list)) byPrefab[prefab] = list = new List<Vec3>();
                list.Add(at);
            }
            var gone = new List<Trace>();
            foreach (var (prefab, at, kind) in made)
            {
                if (byPrefab.TryGetValue(prefab, out var list) && list.Any(p => Vec3.Distance(p, at) <= StillThere)) continue;
                gone.Add(new Trace(kind, at));
            }
            return gone;
        }
    }
}
