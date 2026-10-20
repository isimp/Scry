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

        /// <summary>
        /// The rooms what was left inside uncovers (<see cref="DungeonTraces.Uncovered"/>), by their
        /// place in its rooms: in a server's records apart from the rooms walked; in its answers those
        /// that came of traces alone, the rest counted among the rooms walked.
        /// </summary>
        public readonly SortedSet<int> Traced = new SortedSet<int>();

        /// <summary>What was left inside, for the words; none where nothing was read.</summary>
        public TraceTally Tally;

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
            foreach (var room in Traced) local.Traced.Add(room);
            local.Tally = Tally;
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

        /// <summary>
        /// An id of the older form, the world's then its generator's network id (the user who made
        /// it and its number); none outside a world. The game numbers its objects afresh each time it
        /// loads the world, so such an id names another object after a restart: maps kept under one
        /// are re-keyed by place where they come in (<see cref="ByPlace"/>); the self-test's made-up
        /// maps are kept under them.
        /// </summary>
        public static string IdOf(string world, long user, uint id) => world != null ? world + ":" + Stored.Count(user) + ":" + Stored.Count(id) : null;

        /// <summary>The id a dungeon of the world is kept by: the world's, then where its location stands, which the world's making fixed for good (<see cref="EntranceIdOf"/>); none outside a world.</summary>
        public static string PlaceIdOf(string world, Vec3 entrance) => EntranceIdOf(world, entrance.X, entrance.Z);

        /// <summary>Whether an id is of the older form, by a generator's network id, and of which world.</summary>
        private static bool ByNumber(string id, out string world)
        {
            world = null;
            var parts = id.Split(':');
            if (parts.Length != 3 || !Stored.TryLong(parts[1], out _) || !Stored.TryLong(parts[2], out var number) || number < 0) return false;
            world = parts[0];
            return true;
        }

        /// <summary>
        /// The maps kept under ids of the older form re-keyed by where each dungeon's location stands
        /// (<see cref="PlaceIdOf"/>), those of one dungeon kept under several numbers across restarts
        /// merged into one, its rooms walked, earliest first day and latest last day; these maps
        /// themselves where none is of the older form, else new ones to be written again.
        /// </summary>
        public DungeonMaps ByPlace()
        {
            if (!_order.Any(id => ByNumber(id, out _))) return this;
            var keyed = new DungeonMaps();
            foreach (var (id, map) in All)
            {
                var at = ByNumber(id, out var world) ? PlaceIdOf(world, map.Entrance) : id;
                var one = new DungeonMaps();
                var kept = one.Keep(at, map.Location, map.Rooms, map.Entrance, map.Generator, map.Turn);
                foreach (var room in map.Walked) one.Walk(at, room);
                kept.FirstDay = map.FirstDay;
                kept.LastDay = map.LastDay;
                keyed.Merge(one);
                // Traces are kept by place already: they stay with the map.
                if (map.Tally != null) keyed.SetTraced(at, map.Traced, map.Tally);
            }
            foreach (var pair in _forgotten) keyed._forgotten[pair.Key] = pair.Value;
            return keyed;
        }

        /// <summary>The game's day each world's records were last forgotten on, by an admin (<see cref="NoteForgotten"/>).</summary>
        private readonly Dictionary<string, int> _forgotten = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>The game's day a world's records were last forgotten on; -1 where they never were.</summary>
        public int ForgottenDay(string world) => world != null && _forgotten.TryGetValue(world, out var day) ? day : -1;

        /// <summary>Notes the game's day a world's records were forgotten on, so walks last walked before it are not taken back; a later forget moves it on, never back.</summary>
        public void NoteForgotten(string world, int day)
        {
            if (world == null || day <= ForgottenDay(world)) return;
            _forgotten[world] = day;
            Stamp++;
        }

        /// <summary>The id a dungeon known only by its entrance is kept by, its area never loaded: the world's, then where it lies, to the metre; none outside a world.</summary>
        public static string EntranceIdOf(string world, float x, float z) =>
            world != null ? world + ":at:" + Stored.Count((int)Math.Round(x)) + ":" + Stored.Count((int)Math.Round(z)) : null;

        public DungeonMap Of(string id) => id != null && _maps.TryGetValue(id, out var map) ? map : null;

        /// <summary>How many of a map's rooms were walked, of how many, end caps and dividers left out; a room of a kind not read counted as one, a walk past its rooms none.</summary>
        public static (int Walked, int Rooms) Walked(DungeonMap map, Func<int, RoomShape> roomOf)
        {
            bool IsRoom(int at)
            {
                if (at < 0 || at >= map.Rooms.Count) return false;
                var shape = roomOf(map.Rooms[at].Hash);
                return shape == null || !shape.EndCap && !shape.Divider;
            }
            var rooms = 0;
            for (var i = 0; i < map.Rooms.Count; i++) if (IsRoom(i)) rooms++;
            return (map.Walked.Count(IsRoom), rooms);
        }

        /// <summary>The kinds of room walked in every world's maps, by the hash the game saves a room under; a walk past a map's rooms passed over.</summary>
        public HashSet<int> RoomsWalked()
        {
            var walked = new HashSet<int>();
            foreach (var map in _maps.Values)
            {
                if (map.Rooms == null) continue;
                foreach (var at in map.Walked) if (at >= 0 && at < map.Rooms.Count) walked.Add(map.Rooms[at].Hash);
            }
            return walked;
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
                // Known by its entrance alone before, its rooms come with the map that has them.
                if ((kept.Rooms == null || kept.Rooms.Count == 0) && map.Rooms != null && map.Rooms.Count > 0)
                {
                    kept.Rooms = map.Rooms;
                    kept.Generator = map.Generator;
                    kept.Turn = map.Turn;
                    Stamp++;
                }
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

        /// <summary>Sets the rooms what was left inside a kept dungeon uncovers, and what was left; true where that changed it, false for a dungeon not kept.</summary>
        public bool SetTraced(string id, IEnumerable<int> rooms, TraceTally tally)
        {
            var map = Of(id);
            if (map == null) return false;
            var now = new SortedSet<int>(rooms.Where(r => r >= 0 && r < map.Rooms.Count));
            if (now.SetEquals(map.Traced) && (tally == null ? map.Tally == null : tally.Same(map.Tally))) return false;
            map.Traced.Clear();
            map.Traced.UnionWith(now);
            map.Tally = tally;
            Stamp++;
            return true;
        }

        /// <summary>Takes in a server's answer: each dungeon it holds as it holds it, in place of the copy held before, as a guess round a loop it took back goes with it; the others as they were.</summary>
        public void Replace(DungeonMaps answer)
        {
            foreach (var (id, map) in answer.All.ToList())
            {
                var kept = Of(id);
                if (kept == null)
                {
                    kept = Keep(id, map.Location, map.Rooms, map.Entrance, map.Generator, map.Turn);
                }
                else
                {
                    kept.Location = map.Location;
                    kept.Rooms = map.Rooms;
                    kept.Entrance = map.Entrance;
                    kept.Generator = map.Generator;
                    kept.Turn = map.Turn;
                    kept.Walked.Clear();
                }
                kept.Walked.UnionWith(map.Walked);
                kept.Traced.Clear();
                kept.Traced.UnionWith(map.Traced);
                kept.Tally = map.Tally;
                kept.FirstDay = map.FirstDay;
                kept.LastDay = map.LastDay;
                Stamp++;
            }
        }

        /// <summary>
        /// The maps of a world's dungeons of the locations named, as they stand from their generators
        /// (<see cref="DungeonMap.FromGenerator"/>), as a server answers them: the rooms traces
        /// uncover counted as walked, those that came of traces alone told apart; none outside a world.
        /// </summary>
        public DungeonMaps Only(string world, IEnumerable<string> locations)
        {
            var named = new HashSet<string>(locations, StringComparer.Ordinal);
            var only = new DungeonMaps();
            foreach (var (id, map) in OfWorld(world))
            {
                if (!named.Contains(map.Location)) continue;
                only.TakeAnswered(id, map.FromGenerator());
            }
            return only;
        }

        /// <summary>Keeps a record as a server answers it: its rooms walked and those its traces uncover walked, those of traces alone told apart.</summary>
        public void TakeAnswered(string id, DungeonMap record)
        {
            var kept = Keep(id, record.Location, record.Rooms, record.Entrance, record.Generator, record.Turn);
            foreach (var room in record.Walked) Walk(id, room);
            foreach (var room in record.Traced) Walk(id, room);
            if (record.Tally != null) SetTraced(id, record.Traced.Where(r => !record.Walked.Contains(r)), record.Tally);
            kept.FirstDay = record.FirstDay;
            kept.LastDay = record.LastDay;
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
        /// go on a line of their own after its own, so an older Scry reads its line as before; one
        /// known only by its entrance, no rooms, goes on a line of its own kind, which an older
        /// Scry passes over.
        /// </summary>
        public Func<string> ToSave()
        {
            var held = All.Select(m => (m.Id, m.Map.Location, m.Map.Entrance, m.Map.Generator, m.Map.Turn, Walked: m.Map.Walked.ToArray(), m.Map.Rooms, Days: (m.Map.FirstDay, m.Map.LastDay), Traced: m.Map.Traced.ToArray(), m.Map.Tally)).ToList();
            var forgotten = _forgotten.OrderBy(f => f.Key, StringComparer.Ordinal).ToList();
            _savedStamp = Stamp;
            return () =>
            {
                var text = new StringBuilder();
                text.Append(FirstLine).Append('\n');
                foreach (var pair in forgotten) text.Append(ForgottenLine).Append('\t').Append(Stored.Escaped(pair.Key)).Append('\t').Append(Stored.Count(pair.Value)).Append('\n');
                foreach (var (id, location, entrance, generator, turn, walked, rooms, days, traced, tally) in held)
                {
                    if (rooms == null || rooms.Count == 0)
                    {
                        text.Append(EntranceLine).Append('\t').Append(Stored.Escaped(id)).Append('\t').Append(Stored.Escaped(location ?? "")).Append('\t').Append(Point(entrance)).Append('\n');
                        continue;
                    }
                    text.Append(Stored.Escaped(id)).Append('\t')
                        .Append(Stored.Escaped(location ?? "")).Append('\t')
                        .Append(Point(entrance)).Append('\t')
                        .Append(Point(generator)).Append('\t')
                        .Append(Stored.Exact(turn.X)).Append(' ').Append(Stored.Exact(turn.Y)).Append(' ').Append(Stored.Exact(turn.Z)).Append(' ').Append(Stored.Exact(turn.W)).Append('\t')
                        .Append(string.Join(",", walked.Select(i => Stored.Count(i)))).Append('\t')
                        .Append(Convert.ToBase64String(SavedRooms.Write(rooms))).Append('\n');
                    if (days.FirstDay >= 0 || days.LastDay >= 0)
                        text.Append(DaysLine).Append('\t').Append(Stored.Escaped(id)).Append('\t').Append(Stored.Count(days.FirstDay)).Append('\t').Append(Stored.Count(days.LastDay)).Append('\n');
                    if (tally != null)
                        text.Append(TracedLine).Append('\t').Append(Stored.Escaped(id)).Append('\t').Append(string.Join(",", traced.Select(i => Stored.Count(i)))).Append('\t').Append(tally.ToField()).Append('\n');
                }
                return text.ToString();
            };
        }

        /// <summary>How a line of a dungeon's days begins.</summary>
        private const string DaysLine = "days";

        /// <summary>How a line of a dungeon known only by its entrance begins.</summary>
        private const string EntranceLine = "entrance";

        /// <summary>How a line of the day a world's records were forgotten on begins.</summary>
        private const string ForgottenLine = "forgotten";

        /// <summary>How a line of the rooms a dungeon's traces uncover begins, after its own, four fields that an older Scry passes over.</summary>
        private const string TracedLine = "traced";

        private static string Point(Vec3 p) => Stored.Exact(p.X) + " " + Stored.Exact(p.Y) + " " + Stored.Exact(p.Z);

        /// <summary>The maps a file held, with their days; a line that cannot be read is passed over, as are days of a dungeon not held, and none for no text.</summary>
        public static DungeonMaps Load(string text)
        {
            var maps = new DungeonMaps();
            if (string.IsNullOrEmpty(text)) return maps;
            var days = new List<(string Id, int First, int Last)>();
            var traces = new List<(string Id, List<int> Rooms, TraceTally Tally)>();
            foreach (var line in text.Split('\n'))
            {
                var fields = line.TrimEnd('\r').Split('\t');
                if (fields.Length == 4 && fields[0] == TracedLine)
                {
                    if (Stored.TryUnescaped(fields[1], out var of) && TryCounts(fields[2], out var uncovered) && TraceTally.TryField(fields[3], out var tally)) traces.Add((of, uncovered, tally));
                    continue;
                }
                if (fields.Length == 4 && fields[0] == DaysLine)
                {
                    if (Stored.TryUnescaped(fields[1], out var of) && Stored.TryCount(fields[2], out var first) && Stored.TryCount(fields[3], out var last)) days.Add((of, first, last));
                    continue;
                }
                if (fields.Length == 3 && fields[0] == ForgottenLine)
                {
                    if (Stored.TryUnescaped(fields[1], out var gone) && Stored.TryCount(fields[2], out var day)) maps.NoteForgotten(gone, day);
                    continue;
                }
                if (fields.Length == 4 && fields[0] == EntranceLine)
                {
                    if (Stored.TryUnescaped(fields[1], out var at) && at.Length > 0 && !maps._maps.ContainsKey(at) && Stored.TryUnescaped(fields[2], out var place) && TryPoint(fields[3], out var door))
                        maps.Keep(at, place, new List<(int, Vec3, Vec3)>(), door, default, Quat.Identity);
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
            foreach (var (id, uncovered, tally) in traces) maps.SetTraced(id, uncovered, tally);
            maps._savedStamp = maps.Stamp;
            return maps;
        }

        /// <summary>Counts by commas, none for an empty field; false where one cannot be read.</summary>
        private static bool TryCounts(string field, out List<int> counts)
        {
            counts = new List<int>();
            foreach (var part in field.Split(Comma, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Stored.TryCount(part, out var count)) return false;
                counts.Add(count);
            }
            return true;
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

        /// <summary>The line of the server's record of a dungeon, anyone's walks on it: numbered where the location has more than one, how far, how many of those rooms came of traces alone, its days, and the tier that opens it.</summary>
        public static string Server(int index, int count, int walked, int rooms, string floor, int first, int last, int fromTraces) =>
            "Walked on this server" + Numbered(index, count) + ": " + RoomsOf(walked, rooms) + FromTraces(walked, fromTraces, " \u00b7 ") + WithDays(first, last) + " \u00b7 opened up to the " + floor + " tier";

        /// <summary>How many rooms walked came of traces alone: "from traces" where all did; none where none did.</summary>
        private static string FromTraces(int walked, int fromTraces, string before) =>
            fromTraces <= 0 ? "" : before + (fromTraces >= walked ? "from traces" : Numbers.Count(fromTraces) + " from traces");

        /// <summary>
        /// What was left inside a dungeon in words, on its line's hover: each kind and how many,
        /// in how many rooms, the way to them uncovered too, and that who walked it is not known;
        /// the latest day known; none where nothing was read.
        /// </summary>
        public static string TraceTip(TraceTally tally)
        {
            if (tally == null) return null;
            var left = new List<string>();
            void Add(int count, string one, string many, string done)
            {
                if (count > 0) left.Add(Naming.Count(count, one, many) + " " + done);
            }
            Add(tally.Chests, "chest", "chests", "emptied");
            Add(tally.Broken, "thing", "things", "broken");
            Add(tally.Pickups, "pickup", "pickups", "taken");
            Add(tally.Mined, "rock", "rocks", "mined");
            Add(tally.Doors, "door", "doors", "opened");
            Add(tally.Spawners, "spawn point", "spawn points", "woken");
            if (left.Count == 0) return null;
            var said = "Read from what was left inside: " + string.Join(", ", left);
            if (tally.Rooms > 0) said += ", in " + Naming.Count(tally.Rooms, "room", "rooms") + ", and the rooms on the way to them.";
            else if (tally.Doors > 0) said += ", and the rooms on the way to them.";
            else said += ", so someone was inside.";
            said += " Who walked it is not known.";
            if (tally.LastDay >= 0) said += " The latest pickup was taken on day " + Numbers.Count(tally.LastDay) + ".";
            return said;
        }

        /// <summary>The words on the hover of a server's line: what was left inside, then its days; none where neither is known.</summary>
        public static string ServerTip(TraceTally tally, int first, int last)
        {
            var traces = TraceTip(tally);
            var days = DaysTip(false, first, last);
            return traces == null ? days : days == null ? traces : traces + " " + days;
        }

        /// <summary>The answer to reading the traces in a world's dungeons: in how many, how many rooms they uncover, and how many of those only as the way to a trace.</summary>
        public static string TracesRead(int dungeons, int rooms, int onTheWay) => dungeons == 0
            ? "the server found no trace of a visit in the dungeons of this world."
            : "the server read the traces in " + Naming.Count(dungeons, "dungeon", "dungeons") + " of this world: " + Naming.Count(rooms, "room", "rooms") + " walked, "
              + (onTheWay == 0 ? "none" : Numbers.Count(onTheWay) + " of them") + " on the way to a trace.";

        public const string TracesRefused = "only an admin of the server can have it read traces.";

        /// <summary>The answer to reading them where this game is the server, which says the rest once done.</summary>
        public const string TracesReading = "the traces in the dungeons of this world are being read; how many follows once done.";

        /// <summary>The answer to reading them from a game that is not the server, which answers again once done.</summary>
        public const string TracesAsked = "the server is asked to read the traces in the dungeons of this world.";

        /// <summary>A line's number among the location's, "(2 of 3)"; none where it is the only one.</summary>
        private static string Numbered(int index, int count) => count > 1 ? " (" + Numbers.Count(index + 1) + " of " + Numbers.Count(count) + ")" : "";

        private static string RoomsOf(int walked, int rooms) => Numbers.Count(walked) + " of " + Naming.Count(rooms, "room", "rooms");

        /// <summary>How far a mark's dungeon was walked: "4 of 9 rooms", "all 9 rooms" once every one is.</summary>
        private static string MarkRooms(int walked, int rooms) => walked == rooms && rooms > 1 ? "all " + Naming.Count(rooms, "room", "rooms") : RoomsOf(walked, rooms);

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
        /// The words under the mark of a dungeon walked by you on Valheim's map, as the game names a
        /// pin under it: its name, and under it how far you walked it, smaller and in a colour of
        /// its own, in the rich text the game's pin names write.
        /// </summary>
        public static string MarkLabel(string name, int walked, int rooms, string walkedColour, int yourLast, int serverLast) =>
            $"{name}\n<size=80%><color=#{walkedColour}>Walked by you: {MarkRooms(walked, rooms)}</color></size>" + LastDays(yourLast, serverLast);

        /// <summary>The words under the mark of a dungeon walked on this server, shown with insides: its name, how far anyone walked it, how many of those rooms came of traces alone, and the last day anyone did.</summary>
        public static string ServerLabel(string name, int walked, int rooms, string colour, int serverLast, int fromTraces) =>
            $"{name}\n<size=80%><color=#{colour}>Walked on this server: {MarkRooms(walked, rooms)}{FromTraces(walked, fromTraces, ", ")}</color></size>" + LastDays(-1, serverLast);

        /// <summary>The words under the mark of a dungeon's entrance alone: whether anyone on this server walked it, With insides then showing what was walked.</summary>
        public static string EntranceLabel(string name, bool walkedOnServer, string colour) =>
            $"{name}\n<size=80%><color=#{colour}>{(walkedOnServer ? "Walked on this server; With insides shows what was walked" : "Nobody on this server walked it")}</color></size>";

        /// <summary>A mark's line of the last days walked, yours apart from anyone's on this server, smaller; none where neither is known.</summary>
        private static string LastDays(int yourLast, int serverLast)
        {
            var said = new List<string>();
            if (yourLast >= 0) said.Add("you last day " + Numbers.Count(yourLast));
            if (serverLast >= 0) said.Add("server last day " + Numbers.Count(serverLast));
            return said.Count == 0 ? "" : "\n<size=75%>" + string.Join(" \u00b7 ", said) + "</size>";
        }

        /// <summary>The answer to forgetting the maps of the world played.</summary>
        public static string WorldForgotten(int maps) => maps == 0
            ? "there is no map of a dungeon of this world to forget."
            : maps == 1 ? "the map of 1 dungeon of this world is forgotten." : "the maps of " + Numbers.Count(maps) + " dungeons of this world are forgotten.";
    }
}
