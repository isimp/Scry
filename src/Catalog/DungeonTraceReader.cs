using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SoftReferenceableAssets;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Reads what was left inside the world's dungeons, where this game runs the world, which holds
    /// every object of it (a client holds only the zones near it): once its records are read, and
    /// again as an admin asks. Each dungeon whose rooms the world keeps is read in a frame of its
    /// own, a few milliseconds a frame: its rooms' parts as the game made them, rolled again from
    /// each room's own place as the game rolls them (<c>DungeonGenerator.PlaceRoom</c>), against the
    /// objects standing there now (<see cref="DungeonTraces.Gone"/>), and what stands told by its
    /// state; then the rooms that tells, and the way to them, go into the server's record of it
    /// (<see cref="DungeonMaps.SetTraced(string, IEnumerable{int}, TraceTally, IEnumerable{int}, IEnumerable{int})"/>), apart from the rooms walked, and are worked out again
    /// as a walk comes in. Only what never moves and leaves no trace by itself counts: a loose item
    /// on the floor vanishes by itself after an hour with nobody near, as the game has it, a part
    /// with a lifetime goes by itself, and a creature walks off.
    /// </summary>
    internal static class DungeonTraceReader
    {
        static DungeonTraceReader() => WorldCaches.Register(nameof(DungeonTraceReader), Forget);

        /// <summary>A few milliseconds a frame for reading dungeons, and one dungeon at least.</summary>
        private const double BudgetMs = 4.0;

        /// <summary>How far a dungeon's rooms reach from its generator across, in metres: a zone round it, as the game keeps them inside one.</summary>
        private const float Reach = 96f;

        private static readonly SimulationDistance ZonesRound = new SimulationDistance(1, 0, classic: true);

        private static Queue<ZoneSystem.LocationInstance> _queue;
        private static string _world;
        private static readonly List<Action<int, int, int>> Waiting = new List<Action<int, int, int>>();
        private static int _dungeons, _rooms, _onTheWay;

        /// <summary>How many dungeons the reading passed over as made again of other rooms than those kept.</summary>
        private static int _otherRooms;

        /// <summary>The chests the rooms read were made with, and of those standing how many were filled and how many were taken from, for the server's log.</summary>
        private static int _chestsMade, _chestsStanding, _chestsFilled, _chestsTaken, _chestsGone;

        /// <summary>What each dungeon's traces told, as last read, for working out the way again as a walk comes in.</summary>
        private static readonly Dictionary<string, DungeonTraces> Read = new Dictionary<string, DungeonTraces>(StringComparer.Ordinal);

        /// <summary>The prefabs held while reading, let go of once done.</summary>
        private static readonly Dictionary<int, SoftReference<GameObject>> RoomsHeld = new Dictionary<int, SoftReference<GameObject>>();
        private static readonly Dictionary<string, (SoftReference<GameObject> Prefab, DungeonGenerator Generator)> KindsHeld = new Dictionary<string, (SoftReference<GameObject>, DungeonGenerator)>(StringComparer.Ordinal);
        private static readonly Dictionary<int, RoomShape> Shapes = new Dictionary<int, RoomShape>();

        /// <summary>What a prefab's object tells standing, and what a part of a room's leaves gone, by kind.</summary>
        private static readonly Dictionary<int, Standing> StandingOf = new Dictionary<int, Standing>();

        private static readonly List<ZDO> Near = new List<ZDO>();

        /// <summary>Whether a reading is under way.</summary>
        public static bool Reading => _queue != null;

        /// <summary>How many dungeons the last reading done found traces in, for the self-test; -1 before one.</summary>
        [Diagnostic]
        public static int LastFound { get; private set; } = -1;

        private enum Standing
        {
            Nothing,
            Pickable,
            Chest,
            Door,
            Spawner,
        }

        /// <summary>Starts reading the world's dungeons, or is told with the reading under way; told once done how many dungeons held traces, how many rooms they uncover and how many of those only as the way to a trace.</summary>
        public static void Start(string world, Action<int, int, int> done)
        {
            if (done != null) Waiting.Add(done);
            if (_queue != null && _world == world) return;
            var zones = ZoneSystem.instance;
            if (zones == null || world == null) return;
            _world = world;
            _queue = new Queue<ZoneSystem.LocationInstance>();
            foreach (var instance in zones.m_locationInstances.Values) if (instance.m_placed && instance.m_location != null) _queue.Enqueue(instance);
            _dungeons = 0;
            _rooms = 0;
            _onTheWay = 0;
            _otherRooms = 0;
            _chestsMade = _chestsStanding = _chestsFilled = _chestsTaken = _chestsGone = 0;
        }

        /// <summary>Reads on a few milliseconds' worth, into the server's records; called each frame where this game runs the world.</summary>
        public static void Step(DungeonMaps records, string world)
        {
            if (_queue == null || records == null) return;
            if (world != _world)
            {
                Finish();
                return;
            }
            var watch = Stopwatch.StartNew();
            while (_queue.Count > 0 && (watch.Elapsed.TotalMilliseconds < BudgetMs))
            {
                var instance = _queue.Dequeue();
                var dungeon = WorldDungeons.Placed(world, instance);
                if (dungeon == null) continue;
                if (!Guard.Run(Feature.GroupDungeonMaps, "reading the traces in a dungeon", () => ReadOne(records, world, instance, dungeon))) continue;
                // A dungeon read is a frame's worth.
                break;
            }
            if (_queue.Count == 0) Finish();
        }

        /// <summary>Works out again the rooms a dungeon's traces uncover, as a walk of it came in.</summary>
        public static void Again(DungeonMaps records, string id)
        {
            if (records == null || id == null || !Read.TryGetValue(id, out var traces)) return;
            var record = records.Of(id);
            if (record != null) records.SetTraced(id, traces.Uncovered(record.Walked), traces.Tally, traces.InRooms, traces.UpTo);
        }

        private static void ReadOne(DungeonMaps records, string world, ZoneSystem.LocationInstance instance, WorldDungeon dungeon)
        {
            var kind = Kind(instance.m_location);
            if (kind == null) return;
            var made = new List<(int, Vec3, TraceKind)>();
            var chests = new List<(int, Vec3, TraceKind)>();
            foreach (var room in dungeon.Saved) MadeIn(room, dungeon, kind.Value, made, chests);
            var standing = new List<(int, Vec3)>();
            var traces = new List<Trace>();
            Near.Clear();
            ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(new Vector3(dungeon.Generator.X, 0f, dungeon.Generator.Z)), ZonesRound, Near);
            foreach (var zdo in Near)
            {
                var at = zdo.GetPosition();
                if (!Character.InInterior(at) || Mathf.Abs(at.x - dungeon.Generator.X) > Reach || Mathf.Abs(at.z - dungeon.Generator.Z) > Reach) continue;
                var point = new Vec3(at.x, at.y, at.z);
                standing.Add((zdo.GetPrefab(), point));
                var told = Told(zdo, point);
                if (told.HasValue) traces.Add(told.Value);
            }
            traces.AddRange(DungeonTraces.Gone(made, standing));
            _chestsMade += chests.Count;
            _chestsGone += DungeonTraces.Gone(chests, standing).Count;

            var record = records.Of(dungeon.Id);
            // Made again of other rooms, as a mod raising their number may when it resets a dungeon, it is not the dungeon kept.
            if (record != null && record.Rooms.Count > 0 && !record.SameRooms(dungeon.Saved))
            {
                _otherRooms++;
                return;
            }
            var placed = RealDungeon.Of(dungeon.Saved, ShapeOf, dungeon.Generator, dungeon.Turn, out _);
            var found = DungeonTraces.Of(DungeonWays.Of(placed.Rooms), traces, records.ForgottenDay(world));
            // What was read before stays, though a mod resetting dungeons made all in it again since.
            found.Keep(record);
            if (!found.Inside) return;
            Read[dungeon.Id] = found;
            record = record ?? records.Keep(dungeon.Id, dungeon.Location, dungeon.Saved, dungeon.Entrance, dungeon.Generator, dungeon.Turn);
            var uncovered = found.Uncovered(record.Walked);
            records.SetTraced(dungeon.Id, uncovered, found.Tally, found.InRooms, found.UpTo);
            _dungeons++;
            _rooms += uncovered.Count;
            foreach (var room in uncovered) if (!found.InRooms.Contains(room) && !found.UpTo.Contains(room)) _onTheWay++;
        }

        /// <summary>The trace an object standing in a dungeon tells by its state: a pickup picked, a chest taken from, a door opened, a spawn point woken; none for anything else.</summary>
        private static Trace? Told(ZDO zdo, Vec3 at)
        {
            var prefab = GamePrefabs.Hashed(zdo.GetPrefab());
            if (prefab == null) return null;
            switch (StandingKind(zdo.GetPrefab(), prefab))
            {
                case Standing.Pickable:
                    var pickable = prefab.GetComponent<Pickable>();
                    var time = zdo.GetLong(ZDOVars.s_pickedTime, 0L);
                    // A pickup that grows back keeps when it was picked; the game's day from it.
                    var day = time > 1 && EnvMan.instance != null ? EnvMan.instance.GetDay(time / (double)TimeSpan.TicksPerSecond) : -1;
                    if (zdo.GetBool(ZDOVars.s_picked, pickable.m_defaultPicked) || day >= 0) return new Trace(TraceKind.Pickup, at, day);
                    return null;
                case Standing.Chest:
                    _chestsStanding++;
                    if (zdo.GetBool(ZDOVars.s_addedDefaultItems)) _chestsFilled++;
                    if (!TakenFrom(zdo, prefab.GetComponent<Container>())) return null;
                    _chestsTaken++;
                    return new Trace(TraceKind.Chest, at);
                case Standing.Door:
                    return zdo.GetInt(ZDOVars.s_state) != 0 ? new Trace(TraceKind.Door, at) : (Trace?)null;
                case Standing.Spawner:
                    var spawned = zdo.GetConnection()?.m_type == ZDOExtraData.ConnectionType.Spawned || zdo.GetLong(ZDOVars.s_aliveTime, 0L) != 0L;
                    return spawned ? new Trace(TraceKind.Spawner, at) : (Trace?)null;
                default:
                    return null;
            }
        }

        /// <summary>What a prefab's object tells standing: a pickup that is not picked at first, a chest whose filling always holds something, a door, a spawn point.</summary>
        private static Standing StandingKind(int hash, GameObject prefab)
        {
            if (StandingOf.TryGetValue(hash, out var kind)) return kind;
            kind = Standing.Nothing;
            var pickable = prefab.GetComponent<Pickable>();
            var chest = prefab.GetComponent<Container>();
            if (pickable != null) kind = pickable.m_defaultPicked ? Standing.Nothing : Standing.Pickable;
            else if (chest != null) kind = !chest.m_autoDestroyEmpty && AlwaysFilled(chest) ? Standing.Chest : Standing.Nothing;
            else if (prefab.GetComponent<Door>() != null) kind = Standing.Door;
            else if (prefab.GetComponent<CreatureSpawner>() != null) kind = Standing.Spawner;
            StandingOf[hash] = kind;
            return kind;
        }

        /// <summary>Whether a chest's filling always holds something (<c>Container.AddDefaultItems</c>), so one found with less than it gives was taken from.</summary>
        private static bool AlwaysFilled(Container chest)
        {
            var table = chest.m_defaultItems;
            return table != null && table.m_drops.Count > 0 && table.m_dropMin > 0 && table.m_dropChance >= 1f;
        }

        /// <summary>Whether a chest was filled and holds fewer items now than its filling ever gives, as its saved inventory has it (<see cref="DungeonTraces.TakenFrom"/>).</summary>
        private static bool TakenFrom(ZDO zdo, Container chest)
        {
            if (!zdo.GetBool(ZDOVars.s_addedDefaultItems)) return false;
            // Kept as bytes, as the game reads a chest's items back.
            var items = zdo.GetByteArray(ZDOVars.s_items);
            if (items == null || items.Length == 0) return false;
            var inventory = new Inventory("", null, chest.m_width, chest.m_height);
            inventory.Load(new ZPackage(items));
            return DungeonTraces.TakenFrom(inventory.NrOfItems(), chest.m_defaultItems.m_dropMin, chest.m_defaultItems.m_oneOfEach);
        }

        /// <summary>The trace a part a room made leaves gone: a pickup the game takes away on picking, a chest that goes once empty and is always filled, a rock mined, a thing broken; none for a part that does not keep itself, has a lifetime or walks.</summary>
        private static TraceKind? GoneAs(ZNetView view)
        {
            if (view == null || !view.m_persistent) return null;
            var part = view.gameObject;
            if (part.GetComponent<Character>() != null || part.GetComponent<ItemDrop>() != null) return null;
            var pickable = part.GetComponent<Pickable>();
            if (pickable != null) return pickable.m_respawnTimeMinutes <= 0f && pickable.m_hideWhenPicked == null && !pickable.m_defaultPicked ? TraceKind.Pickup : (TraceKind?)null;
            var chest = part.GetComponent<Container>();
            if (chest != null) return chest.m_autoDestroyEmpty && AlwaysFilled(chest) ? TraceKind.Chest : (TraceKind?)null;
            if (part.GetComponent<MineRock>() != null || part.GetComponent<MineRock5>() != null) return TraceKind.Mined;
            var thing = part.GetComponent<Destructible>();
            if (thing != null) return thing.m_ttl <= 0f ? TraceKind.Broken : (TraceKind?)null;
            return null;
        }

        /// <summary>
        /// The parts a saved room was made with that leave a trace gone, where it made them, as the
        /// game makes them in a world first built (<c>DungeonGenerator.PlaceRoom</c>): its random
        /// parts rolled from the room's own place, then each network object switched on made; the
        /// room's prefab set back as it was, as the game sets it back. A generator that adds its own
        /// seed to the roll is not rolled again: only the parts no roll decides count there.
        /// </summary>
        private static void MadeIn((int Hash, Vec3 Position, Vec3 Angles) saved, WorldDungeon dungeon, (DungeonGenerator Generator, bool Rolls) kind, List<(int, Vec3, TraceKind)> made, List<(int, Vec3, TraceKind)> chests)
        {
            var prefab = RoomPrefab(saved.Hash);
            if (prefab == null) return;
            var room = prefab.GetComponent<Room>();
            if (room == null || room.m_endCap || room.m_divider) return;
            var pos = new Vector3(saved.Position.X, saved.Position.Y, saved.Position.Z);
            var rot = Quaternion.Euler(saved.Angles.X, saved.Angles.Y, saved.Angles.Z);
            var views = Utils.GetEnabledComponentsInChildren<ZNetView>(prefab);
            var spawns = Utils.GetEnabledComponentsInChildren<RandomSpawn>(prefab);
            var objects = Utils.GetEnabledComponentsInChildren<RandomObject>(prefab);
            var root = prefab.transform.position;
            var back = Quaternion.Inverse(prefab.transform.rotation);
            Vector3 Placed(Transform part) => pos + rot * (back * (part.position - root));
            var state = UnityEngine.Random.state;
            try
            {
                if (kind.Rolls)
                {
                    foreach (var spawn in spawns) spawn.Prepare();
                    UnityEngine.Random.InitState(DungeonTraces.RoomSeed(saved.Position, dungeon.Generator, kind.Generator.m_useCustomInteriorTransform, 0));
                    foreach (var spawn in spawns) spawn.Randomize(Placed(spawn.transform), null, kind.Generator);
                    foreach (var one in objects) one.Randomize(Placed(one.transform), null, kind.Generator);
                }
                foreach (var view in views)
                {
                    if (!view.gameObject.activeSelf) continue;
                    // Without the roll, a part a roll decides may never have been made.
                    if (!kind.Rolls && RolledOver(view.transform, prefab.transform)) continue;
                    var at = Placed(view.transform);
                    var part = (Utils.GetPrefabName(view.gameObject).GetStableHashCode(), new Vec3(at.x, at.y, at.z), TraceKind.Chest);
                    if (view.m_persistent && view.GetComponent<Container>() != null) chests.Add(part);
                    var gone = GoneAs(view);
                    if (!gone.HasValue) continue;
                    made.Add((part.Item1, part.Item2, gone.Value));
                }
            }
            finally
            {
                UnityEngine.Random.state = state;
                if (kind.Rolls)
                {
                    foreach (var spawn in spawns) spawn.Reset();
                    foreach (var one in objects) one.Reset();
                }
                foreach (var view in views) view.gameObject.SetActive(true);
            }
        }

        /// <summary>Whether a part stands under a random part or object of its room, which a roll decides.</summary>
        private static bool RolledOver(Transform part, Transform room)
        {
            for (var at = part; at != null && at != room; at = at.parent)
            {
                if (at.GetComponent<RandomSpawn>() != null || at.GetComponent<RandomObject>() != null) return true;
            }
            return false;
        }

        /// <summary>A kind of dungeon's generator as its location's prefab holds it, and whether its rooms' parts can be rolled again; held until the reading is done.</summary>
        private static (DungeonGenerator Generator, bool Rolls)? Kind(ZoneSystem.ZoneLocation location)
        {
            var name = CompendiumMap.NameOf(location);
            if (!KindsHeld.TryGetValue(name, out var held))
            {
                var prefab = location.m_prefab;
                if (!prefab.IsValid) return null;
                prefab.Load();
                held = (prefab, prefab.Asset != null ? prefab.Asset.GetComponentInChildren<DungeonGenerator>(true) : null);
                KindsHeld[name] = held;
            }
            if (held.Generator == null) return null;
            return (held.Generator, !held.Generator.m_addBaseSeedToRandomSpawn);
        }

        /// <summary>A kind of room's prefab, held until the reading is done; none for a kind the game does not know.</summary>
        private static GameObject RoomPrefab(int hash)
        {
            if (RoomsHeld.TryGetValue(hash, out var held)) return held.Asset;
            var data = DungeonDB.instance != null ? DungeonDB.instance.GetRoom(hash) : null;
            if (data == null || !data.m_prefab.IsValid) return null;
            data.m_prefab.Load();
            RoomsHeld[hash] = data.m_prefab;
            return data.m_prefab.Asset;
        }

        /// <summary>A kind of room's shape, read once from its prefab (<see cref="PlaceReader.Shape"/>).</summary>
        private static RoomShape ShapeOf(int hash)
        {
            if (Shapes.TryGetValue(hash, out var shape)) return shape;
            var prefab = RoomPrefab(hash);
            shape = prefab != null ? PlaceReader.Shape(prefab) : null;
            Shapes[hash] = shape;
            return shape;
        }

        /// <summary>Ends the reading: tells everyone waiting, and lets go of the prefabs held.</summary>
        private static void Finish()
        {
            _queue = null;
            LastFound = _dungeons;
            // What a dedicated server read is in its log, as nobody there reads the chat.
            if (ZNet.instance != null && ZNet.instance.IsDedicated())
            {
                Log.Report(DungeonMapWords.TracesRead(_dungeons, _rooms, _onTheWay) + DungeonMapWords.OtherRooms(_otherRooms));
                Log.Report(DungeonMapWords.ChestsRead(_chestsMade, _chestsStanding, _chestsFilled, _chestsTaken, _chestsGone));
            }
            foreach (var done in Waiting) done(_dungeons, _rooms, _onTheWay);
            Waiting.Clear();
            LetGo();
        }

        private static void LetGo()
        {
            foreach (var held in RoomsHeld.Values) held.Release();
            RoomsHeld.Clear();
            foreach (var held in KindsHeld.Values) held.Prefab.Release();
            KindsHeld.Clear();
        }

        private static void Forget()
        {
            _queue = null;
            _world = null;
            Waiting.Clear();
            Read.Clear();
            LetGo();
        }
    }
}
