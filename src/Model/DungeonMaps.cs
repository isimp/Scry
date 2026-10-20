using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>
    /// A dungeon a character has been in: its rooms as the world saved them, where its location
    /// stands and where its generator stands and how it is turned, the rooms stood in by their
    /// place in that list, and the game's days it was first and last walked in.
    /// </summary>
    internal sealed class DungeonMap
    {
        public string Location;
        public List<(int Hash, Vec3 Position, Vec3 Angles)> Rooms;
        public Vec3 Entrance;
        public Vec3 Generator;
        public Quat Turn = Quat.Identity;
        public readonly SortedSet<int> Walked = new SortedSet<int>();

        /// <summary>The game's day it was first walked in; -1 where that is not known, as for a walk kept before days were.</summary>
        public int FirstDay = -1;

        /// <summary>The game's day it was last walked in; -1 where that is not known.</summary>
        public int LastDay = -1;

        /// <summary>The same map as it stands from its generator: its rooms placed and turned from there, the generator at the origin and unturned, no place of the world in it.</summary>
        public DungeonMap FromGenerator()
        {
            var back = Turn.Inverse();
            var local = new DungeonMap
            {
                Location = Location,
                Rooms = Rooms.Select(r => (r.Hash, back * (r.Position - Generator), (back * Quat.Euler(r.Angles.X, r.Angles.Y, r.Angles.Z)).ToEuler())).ToList(),
            };
            foreach (var room in Walked) local.Walked.Add(room);
            local.FirstDay = FirstDay;
            local.LastDay = LastDay;
            return local;
        }
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

        /// <summary>The name of the file a server keeps a world's records of its dungeons in.</summary>
        public static string ServerFile(string world) => "maps-" + world + ".dat";

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
            OfWorld(world).Where(m => m.Map.Location == location);

        /// <summary>
        /// A location's maps worth a line on its page, in the order kept, each with how far it was
        /// walked: one with no room walked holds nothing to show and is left out, still kept until
        /// forgotten.
        /// </summary>
        public IEnumerable<(string Id, DungeonMap Map, int Walked, int Rooms)> WalkedOf(string world, string location, Func<DungeonMap, (int Walked, int Rooms)> count)
        {
            foreach (var (id, map) in OfLocation(world, location))
            {
                var (walked, rooms) = count(map);
                if (walked > 0) yield return (id, map, walked, rooms);
            }
        }

        /// <summary>The maps of a world's dungeons, in the order kept; none outside a world.</summary>
        public IEnumerable<(string Id, DungeonMap Map)> OfWorld(string world) =>
            world == null ? Enumerable.Empty<(string, DungeonMap)>() : All.Where(m => InWorld(m.Id, world));

        private static bool InWorld(string id, string world) => id.StartsWith(world + ":", StringComparison.Ordinal);

        /// <summary>Keeps a dungeon been in, as first read, on the game's day given as its first and last walked (-1 for none); one kept already stays as it was.</summary>
        public DungeonMap Keep(string id, string location, List<(int Hash, Vec3 Position, Vec3 Angles)> rooms, Vec3 entrance, Vec3 generator, Quat turn, int day = -1)
        {
            if (_maps.TryGetValue(id, out var kept)) return kept;
            var map = new DungeonMap { Location = location, Rooms = rooms, Entrance = entrance, Generator = generator, Turn = turn, FirstDay = day, LastDay = day };
            _maps[id] = map;
            _order.Add(id);
            Stamp++;
            return map;
        }

        /// <summary>A kept dungeon stood in on a game's day: its last day moved on to it, never back, its first left as it is; true when it moved.</summary>
        public bool Visit(string id, int day)
        {
            var map = Of(id);
            if (map == null || day < 0 || day <= map.LastDay) return false;
            map.LastDay = day;
            Stamp++;
            return true;
        }

        /// <summary>A room of a kept dungeon stood in, by its place in its rooms; true when it was not walked before.</summary>
        public bool Walk(string id, int room)
        {
            var map = Of(id);
            if (map == null || room < 0 || room >= map.Rooms.Count || !map.Walked.Add(room)) return false;
            Stamp++;
            return true;
        }

        /// <summary>Takes in other maps: each kept as first read, its rooms walked added, the earliest first day and the latest last day kept; how many rooms were walked anew.</summary>
        public int Merge(DungeonMaps other) => Merge(other.All);

        /// <summary>Takes in the other maps of one world alone, as a server does those its players send; none outside a world.</summary>
        public int Merge(DungeonMaps other, string world) => Merge(other.OfWorld(world));

        private int Merge(IEnumerable<(string Id, DungeonMap Map)> maps)
        {
            var walked = 0;
            foreach (var (id, map) in maps.ToList())
            {
                var kept = Keep(id, map.Location, map.Rooms, map.Entrance, map.Generator, map.Turn);
                foreach (var room in map.Walked) if (Walk(id, room)) walked++;
                if (map.FirstDay >= 0 && (kept.FirstDay < 0 || map.FirstDay < kept.FirstDay))
                {
                    kept.FirstDay = map.FirstDay;
                    Stamp++;
                }
                if (map.LastDay > kept.LastDay)
                {
                    kept.LastDay = map.LastDay;
                    Stamp++;
                }
            }
            return walked;
        }

        /// <summary>The maps of a world's dungeons of the locations named, as they stand from their generators (<see cref="DungeonMap.FromGenerator"/>); none outside a world.</summary>
        public DungeonMaps Only(string world, IEnumerable<string> locations)
        {
            var named = new HashSet<string>(locations, StringComparer.Ordinal);
            var only = new DungeonMaps();
            foreach (var (id, map) in OfWorld(world))
            {
                if (!named.Contains(map.Location)) continue;
                var local = map.FromGenerator();
                var kept = only.Keep(id, local.Location, local.Rooms, local.Entrance, local.Generator, local.Turn);
                foreach (var room in local.Walked) only.Walk(id, room);
                kept.FirstDay = local.FirstDay;
                kept.LastDay = local.LastDay;
            }
            return only;
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
        /// there from what they hold now, not after. They count as saved from here. A dungeon's days
        /// go on a line of their own after its own, so an older Scry reads its line as before.
        /// </summary>
        public Func<string> ToSave()
        {
            var held = All.Select(m => (m.Id, m.Map.Location, m.Map.Entrance, m.Map.Generator, m.Map.Turn, Walked: m.Map.Walked.ToArray(), m.Map.Rooms, Days: (m.Map.FirstDay, m.Map.LastDay))).ToList();
            _savedStamp = Stamp;
            return () =>
            {
                var text = new StringBuilder();
                text.Append(FirstLine).Append('\n');
                foreach (var (id, location, entrance, generator, turn, walked, rooms, days) in held)
                {
                    text.Append(Stored.Escaped(id)).Append('\t')
                        .Append(Stored.Escaped(location ?? "")).Append('\t')
                        .Append(Point(entrance)).Append('\t')
                        .Append(Point(generator)).Append('\t')
                        .Append(Stored.Exact(turn.X)).Append(' ').Append(Stored.Exact(turn.Y)).Append(' ').Append(Stored.Exact(turn.Z)).Append(' ').Append(Stored.Exact(turn.W)).Append('\t')
                        .Append(string.Join(",", walked.Select(i => Stored.Count(i)))).Append('\t')
                        .Append(Convert.ToBase64String(SavedRooms.Write(rooms))).Append('\n');
                    if (days.FirstDay < 0 && days.LastDay < 0) continue;
                    text.Append(DaysLine).Append('\t').Append(Stored.Escaped(id)).Append('\t').Append(Stored.Count(days.FirstDay)).Append('\t').Append(Stored.Count(days.LastDay)).Append('\n');
                }
                return text.ToString();
            };
        }

        /// <summary>How a line of a dungeon's days begins.</summary>
        private const string DaysLine = "days";

        private static string Point(Vec3 p) => Stored.Exact(p.X) + " " + Stored.Exact(p.Y) + " " + Stored.Exact(p.Z);

        /// <summary>The maps a file held, with their days; a line that cannot be read is passed over, as are days of a dungeon not held, and none for no text.</summary>
        public static DungeonMaps Load(string text)
        {
            var maps = new DungeonMaps();
            if (string.IsNullOrEmpty(text)) return maps;
            var days = new List<(string Id, int First, int Last)>();
            foreach (var line in text.Split('\n'))
            {
                var fields = line.TrimEnd('\r').Split('\t');
                if (fields.Length == 4 && fields[0] == DaysLine)
                {
                    if (Stored.TryUnescaped(fields[1], out var of) && Stored.TryCount(fields[2], out var first) && Stored.TryCount(fields[3], out var last)) days.Add((of, first, last));
                    continue;
                }
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
            foreach (var (id, first, last) in days)
            {
                var map = maps.Of(id);
                if (map == null) continue;
                map.FirstDay = first;
                map.LastDay = last;
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
        /// <summary>A walk of yours as the page lines it: numbered where the location has more than one, how far, and its days.</summary>
        public static string Yours(int index, int count, int walked, int rooms, int first, int last) =>
            "Walked by you" + Numbered(index, count) + ": " + RoomsOf(walked, rooms) + WithDays(first, last);

        /// <summary>The line of the server's record of a dungeon, anyone's walks on it: numbered where the location has more than one, how far, its days, and the tier that opens it.</summary>
        public static string Server(int index, int count, int walked, int rooms, string floor, int first, int last) =>
            "Walked on this server" + Numbered(index, count) + ": " + RoomsOf(walked, rooms) + WithDays(first, last) + " \u00b7 opened up to the " + floor + " tier";

        /// <summary>A line's number among the location's, "(2 of 3)"; none where it is the only one.</summary>
        private static string Numbered(int index, int count) => count > 1 ? " (" + Numbers.Count(index + 1) + " of " + Numbers.Count(count) + ")" : "";

        private static string RoomsOf(int walked, int rooms) => Numbers.Count(walked) + " of " + Naming.Count(rooms, "room", "rooms");

        private static string WithDays(int first, int last)
        {
            var days = Days(first, last);
            return days == null ? "" : " \u00b7 " + days;
        }

        /// <summary>The game's days a walk spans: "days 103–241", "day 103" for one day, "last day 241" where its first is not known; none where its last is not.</summary>
        public static string Days(int first, int last)
        {
            if (last < 0) return null;
            if (first < 0) return "last day " + Numbers.Count(last);
            return first == last ? "day " + Numbers.Count(first) : "days " + Numbers.Count(first) + "\u2013" + Numbers.Count(last);
        }

        /// <summary>The days a walk spans in words, on the line's hover: yours, or anyone's on this server; none where its last is not known.</summary>
        public static string DaysTip(bool byYou, int first, int last)
        {
            if (last < 0) return null;
            var who = byYou ? "You" : "Anyone on this server";
            if (first < 0) return who + " last walked it on day " + Numbers.Count(last);
            return first == last ? who + " walked it on day " + Numbers.Count(first) : who + " first walked it on day " + Numbers.Count(first) + ", last on day " + Numbers.Count(last);
        }

        public const string Open = "Show it";
        public const string OpenTip = "Show this dungeon on the stage as far as you walked it, wherever you are";
        public const string Forget = "Forget";
        public const string ForgetTip = "Forget this walk: its rooms show again only as you walk them";



        public const string GroupOpenTip = "Show this dungeon on the stage as far as anyone on this server walked it";

        /// <summary>The answer to forgetting the server's records of the world played.</summary>
        public static string GroupForgotten(int records) => records == 0
            ? "the server has no record of a dungeon of this world to forget."
            : records == 1 ? "the server's record of 1 dungeon of this world is forgotten." : "the server's records of " + Numbers.Count(records) + " dungeons of this world are forgotten.";

        public const string GroupForgetRefused = "only an admin of the server can forget its records.";

        /// <summary>The answer to forgetting them from a game that is not the server, which answers again once done.</summary>
        public const string GroupAsked = "the server is asked to forget its records of this world.";

        /// <summary>
        /// The words under a dungeon's mark on Valheim's map, as the game names a pin under it: its
        /// name, and under it how far it was walked, smaller and in a colour of its own, in the rich
        /// text the game's pin names write.
        /// </summary>
        public static string MarkLabel(string name, int walked, int rooms, string walkedColour, int yourLast, int serverLast) =>
            $"{name}\n<size=80%><color=#{walkedColour}>{PlanBoxWords.Walked(walked, rooms)}</color></size>" + LastDays(yourLast, serverLast);

        /// <summary>A mark's line of the last days walked, yours apart from anyone's on this server, smaller; none where neither is known.</summary>
        private static string LastDays(int yourLast, int serverLast)
        {
            var said = new List<string>();
            if (yourLast >= 0) said.Add("you last day " + Numbers.Count(yourLast));
            if (serverLast >= 0) said.Add("server last day " + Numbers.Count(serverLast));
            return said.Count == 0 ? "" : "\n<size=75%>" + string.Join(" \u00b7 ", said) + "</size>";
        }

        /// <summary>A mark's words for a dungeon shown with its insides and not walked: its name, and the ground it shows from, smaller and in the colour given.</summary>
        public static string InsidesLabel(string name, WorldSource source, string colour, int serverLast) =>
            $"{name}\n<size=80%><color=#{colour}>not walked \u00b7 with insides from {CompendiumWords.SourceName(source)}</color></size>" + LastDays(-1, serverLast);

        /// <summary>The answer to forgetting the maps of the world played.</summary>
        public static string WorldForgotten(int maps) => maps == 0
            ? "there is no map of a dungeon of this world to forget."
            : maps == 1 ? "the map of 1 dungeon of this world is forgotten." : "the maps of " + Numbers.Count(maps) + " dungeons of this world are forgotten.";
    }
}
