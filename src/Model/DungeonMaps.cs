using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>
    /// A dungeon a character has been in: its rooms as the world saved them, where its location
    /// stands and where its generator stands and how it is turned, and the rooms stood in by their
    /// place in that list.
    /// </summary>
    internal sealed class DungeonMap
    {
        public string Location;
        public List<(int Hash, Vec3 Position, Vec3 Angles)> Rooms;
        public Vec3 Entrance;
        public Vec3 Generator;
        public Quat Turn = Quat.Identity;
        public readonly SortedSet<int> Walked = new SortedSet<int>();
    }

    /// <summary>
    /// The dungeons a character has been in (<see cref="DungeonMap"/>), each by the world and its
    /// generator's network id: kept as first read on being in it, walked room by room, and kept
    /// in a file of the character's own, a line a dungeon, its rooms as the world saves them.
    /// </summary>
    internal sealed class DungeonMaps
    {
        private readonly Dictionary<string, DungeonMap> _maps = new Dictionary<string, DungeonMap>(StringComparer.Ordinal);

        /// <summary>The ids in the order the maps were kept.</summary>
        private readonly List<string> _order = new List<string>();

        private int _savedStamp;

        /// <summary>Counts up each time a map is kept, walked further or forgotten.</summary>
        public int Stamp { get; private set; }

        /// <summary>Whether the maps changed since last saved or read.</summary>
        public bool Changed => Stamp != _savedStamp;

        /// <summary>Every map kept, by its id, in the order they were kept.</summary>
        public IEnumerable<(string Id, DungeonMap Map)> All => _order.Select(id => (id, _maps[id]));

        /// <summary>The id a map is kept by: the world's, then its generator's network id (the user who made it and its number); none outside a world.</summary>
        public static string IdOf(string world, long user, uint id) => world != null ? world + ":" + Stored.Count(user) + ":" + Stored.Count(id) : null;

        public DungeonMap Of(string id) => id != null && _maps.TryGetValue(id, out var map) ? map : null;

        /// <summary>How many of a map's rooms were walked, of how many, end caps and dividers left out; a room of a kind not read counted as one.</summary>
        public static (int Walked, int Rooms) Walked(DungeonMap map, Func<int, RoomShape> roomOf)
        {
            bool IsRoom(int at)
            {
                var shape = roomOf(map.Rooms[at].Hash);
                return shape == null || !shape.EndCap && !shape.Divider;
            }
            var rooms = 0;
            for (var i = 0; i < map.Rooms.Count; i++) if (IsRoom(i)) rooms++;
            return (map.Walked.Count(IsRoom), rooms);
        }

        /// <summary>The maps of a location's dungeons in a world, in the order kept; none outside a world.</summary>
        public IEnumerable<(string Id, DungeonMap Map)> OfLocation(string world, string location) =>
            world == null ? Enumerable.Empty<(string, DungeonMap)>() : All.Where(m => InWorld(m.Id, world) && m.Map.Location == location);

        private static bool InWorld(string id, string world) => id.StartsWith(world + ":", StringComparison.Ordinal);

        /// <summary>Keeps a dungeon been in, as first read; one kept already stays as it was.</summary>
        public DungeonMap Keep(string id, string location, List<(int Hash, Vec3 Position, Vec3 Angles)> rooms, Vec3 entrance, Vec3 generator, Quat turn)
        {
            if (_maps.TryGetValue(id, out var kept)) return kept;
            var map = new DungeonMap { Location = location, Rooms = rooms, Entrance = entrance, Generator = generator, Turn = turn };
            _maps[id] = map;
            _order.Add(id);
            Stamp++;
            return map;
        }

        /// <summary>A room of a kept dungeon stood in, by its place in its rooms; true when it was not walked before.</summary>
        public bool Walk(string id, int room)
        {
            var map = Of(id);
            if (map == null || room < 0 || room >= map.Rooms.Count || !map.Walked.Add(room)) return false;
            Stamp++;
            return true;
        }

        /// <summary>Forgets one dungeon's map; true when there was one.</summary>
        public bool Forget(string id)
        {
            if (id == null || !_maps.Remove(id)) return false;
            _order.Remove(id);
            Stamp++;
            return true;
        }

        /// <summary>Forgets the maps of a world's dungeons, those of other worlds kept; how many were forgotten.</summary>
        public int ForgetWorld(string world)
        {
            var gone = _order.Where(id => InWorld(id, world)).ToList();
            foreach (var id in gone) Forget(id);
            return gone.Count;
        }

        public void ForgetAll()
        {
            if (_maps.Count == 0) return;
            _maps.Clear();
            _order.Clear();
            Stamp++;
        }

        // ----- The file -----

        private const string FirstLine = "scry-maps 1";

        private static readonly char[] Comma = { ',' };

        /// <summary>
        /// The maps as they are now, to be written out on a worker thread: the text is put together
        /// there from what they hold now, not after. They count as saved from here.
        /// </summary>
        public Func<string> ToSave()
        {
            var held = All.Select(m => (m.Id, m.Map.Location, m.Map.Entrance, m.Map.Generator, m.Map.Turn, Walked: m.Map.Walked.ToArray(), m.Map.Rooms)).ToList();
            _savedStamp = Stamp;
            return () =>
            {
                var text = new StringBuilder();
                text.Append(FirstLine).Append('\n');
                foreach (var (id, location, entrance, generator, turn, walked, rooms) in held)
                {
                    text.Append(Stored.Escaped(id)).Append('\t')
                        .Append(Stored.Escaped(location ?? "")).Append('\t')
                        .Append(Point(entrance)).Append('\t')
                        .Append(Point(generator)).Append('\t')
                        .Append(Stored.Exact(turn.X)).Append(' ').Append(Stored.Exact(turn.Y)).Append(' ').Append(Stored.Exact(turn.Z)).Append(' ').Append(Stored.Exact(turn.W)).Append('\t')
                        .Append(string.Join(",", walked.Select(i => Stored.Count(i)))).Append('\t')
                        .Append(Convert.ToBase64String(SavedRooms.Write(rooms))).Append('\n');
                }
                return text.ToString();
            };
        }

        private static string Point(Vec3 p) => Stored.Exact(p.X) + " " + Stored.Exact(p.Y) + " " + Stored.Exact(p.Z);

        /// <summary>The maps a file held; a line that cannot be read is passed over, and none for no text.</summary>
        public static DungeonMaps Load(string text)
        {
            var maps = new DungeonMaps();
            if (string.IsNullOrEmpty(text)) return maps;
            foreach (var line in text.Split('\n'))
            {
                var fields = line.TrimEnd('\r').Split('\t');
                if (fields.Length != 7 || !Stored.TryUnescaped(fields[0], out var id) || id.Length == 0 || !Stored.TryUnescaped(fields[1], out var location)) continue;
                if (!TryPoint(fields[2], out var entrance) || !TryPoint(fields[3], out var generator) || !TryTurn(fields[4], out var turn)) continue;
                var rooms = TryRooms(fields[6]);
                if (rooms == null || rooms.Count == 0) continue;
                var walked = new List<int>();
                var readable = true;
                foreach (var part in fields[5].Split(Comma, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Stored.TryCount(part, out var room)) walked.Add(room);
                    else readable = false;
                }
                if (!readable || maps._maps.ContainsKey(id)) continue;
                maps.Keep(id, location, rooms, entrance, generator, turn);
                foreach (var room in walked) maps.Walk(id, room);
            }
            maps._savedStamp = maps.Stamp;
            return maps;
        }

        private static bool TryPoint(string field, out Vec3 point)
        {
            var parts = field.Split(' ');
            point = default;
            if (parts.Length != 3 || !Stored.TryExact(parts[0], out var x) || !Stored.TryExact(parts[1], out var y) || !Stored.TryExact(parts[2], out var z)) return false;
            point = new Vec3(x, y, z);
            return true;
        }

        private static bool TryTurn(string field, out Quat turn)
        {
            var parts = field.Split(' ');
            turn = Quat.Identity;
            if (parts.Length != 4 || !Stored.TryExact(parts[0], out var x) || !Stored.TryExact(parts[1], out var y) || !Stored.TryExact(parts[2], out var z) || !Stored.TryExact(parts[3], out var w)) return false;
            turn = new Quat(x, y, z, w);
            return true;
        }

        /// <summary>The rooms of a field written as base64; none for one that is not.</summary>
        private static List<(int Hash, Vec3 Position, Vec3 Angles)> TryRooms(string field) => IsBase64(field) ? SavedRooms.Read(Convert.FromBase64String(field)) : null;

        /// <summary>Whether a text is base64 as written: in fours, of its letters, digits, + and /, padded only at its end.</summary>
        private static bool IsBase64(string text)
        {
            if (text.Length == 0 || text.Length % 4 != 0) return false;
            var padded = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '=')
                {
                    if (i < text.Length - 2) return false;
                    padded++;
                    continue;
                }
                if (padded > 0) return false;
                if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '+' || c == '/')) return false;
            }
            return true;
        }
    }

    /// <summary>What the page of a dungeon been in says of its maps, and the reset's answer.</summary>
    internal static class DungeonMapWords
    {
        /// <summary>A map's line: numbered where the location has more than one.</summary>
        public static string Yours(int index, int count, int walked, int rooms) =>
            (count > 1 ? "Your map " + Numbers.Count(index + 1) : "Your map") + ": " + PlanBoxWords.Walked(walked, rooms);

        public const string Open = "Open map";
        public const string OpenTip = "Shows this dungeon on the stage as far as you walked it, wherever you are";
        public const string Forget = "Forget";
        public const string ForgetTip = "Forgets this map: its rooms show again only as you walk them";

        /// <summary>The answer to forgetting the maps of the world played.</summary>
        public static string WorldForgotten(int maps) => maps == 0
            ? "there is no map of a dungeon of this world to forget."
            : maps == 1 ? "the map of 1 dungeon of this world is forgotten." : "the maps of " + Numbers.Count(maps) + " dungeons of this world are forgotten.";
    }
}
