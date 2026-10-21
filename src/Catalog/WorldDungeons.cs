using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>A dungeon of the world as its generator keeps it: its rooms as saved, and where the generator stands and how it is turned.</summary>
    internal sealed class WorldDungeon
    {
        public List<(int Hash, Vec3 Position, Vec3 Angles)> Saved;
        public Vec3 Generator;
        public Quat Turn;

        /// <summary>The id its map is kept by (<see cref="DungeonMaps.IdOf"/>).</summary>
        public string Id;

        /// <summary>The location it is the dungeon of, and where that stands in the world.</summary>
        public string Location;
        public Vec3 Entrance;

        /// <summary>Each kind of room's size by its hash, read from the rooms standing in the world, end caps and dividers left out; empty until they stand.</summary>
        public readonly Dictionary<int, Vec3> Sizes = new Dictionary<int, Vec3>();

        /// <summary>A room's size by its hash, none for one not standing, an end cap or a divider.</summary>
        public Vec3? SizeOf(int hash) => Sizes.TryGetValue(hash, out var size) ? size : (Vec3?)null;
    }

    /// <summary>
    /// The dungeon of the location the player stands in or at (<c>Location.GetLocation</c>, as the
    /// compendium has it), read from its generator's saved rooms once it is loaded, which is only
    /// while its zone is. The generator stands in the location's zone, made from its own network
    /// object, its rooms standing under it; the saved rooms are read once, as they never change
    /// once laid out, and the sizes of its kinds of room once they stand.
    /// </summary>
    internal static class WorldDungeons
    {
        /// <summary>Leaving a world lets go of what is held of it (<see cref="WorldCaches"/>).</summary>
        static WorldDungeons() => WorldCaches.Register(nameof(WorldDungeons), Forget);

        private static DungeonGenerator _generator;
        private static WorldDungeon _read;

        public static void Forget()
        {
            _generator = null;
            _read = null;
            InZone.Clear();
        }

        /// <summary>The dungeon of the location named where the player stands, the same each time while its generator stands; null elsewhere or before it has loaded.</summary>
        public static WorldDungeon Here(string location)
        {
            var place = PlayersPlace();
            return place != null && Utils.GetPrefabName(place.gameObject) == location ? Of(place) : null;
        }

        /// <summary>The dungeon of the location the player stands in or at, whichever it is; null where there is none loaded.</summary>
        public static WorldDungeon AtPlayer()
        {
            var place = PlayersPlace();
            return place != null ? Of(place) : null;
        }

        private static Location PlayersPlace()
        {
            var player = Player.m_localPlayer;
            return player != null ? Location.GetLocation(player.transform.position) : null;
        }

        private static WorldDungeon Of(Location place)
        {
            var zone = ZoneSystem.GetZone(place.transform.position);
            if (_generator == null || ZoneSystem.GetZone(_generator.transform.position) != zone)
            {
                _read = null;
                _generator = null;
                foreach (var generator in Object.FindObjectsByType<DungeonGenerator>(FindObjectsSortMode.None))
                {
                    if (ZoneSystem.GetZone(generator.transform.position) != zone) continue;
                    _generator = generator;
                    break;
                }
                if (_generator == null) return null;
            }
            if (_read == null) _read = Read(_generator, place);
            if (_read != null && _read.Sizes.Count == 0) Measure(_generator, _read);
            return _read;
        }

        /// <summary>The dungeon of a generator standing in the world, as its network object keeps it; null before it has any rooms.</summary>
        private static WorldDungeon Read(DungeonGenerator generator, Location place)
        {
            var view = generator.GetComponent<ZNetView>();
            var zdo = view != null ? view.GetZDO() : null;
            return zdo != null ? FromZdo(CompendiumMap.World, zdo, Utils.GetPrefabName(place.gameObject), place.transform.position) : null;
        }

        /// <summary>
        /// A dungeon as its generator's network object keeps it, of the location named whose entrance
        /// is given: the rooms it saved (<c>DungeonGenerator.Load</c> reads them back the same way,
        /// the older keys too), where it stands and how it is turned; null where it has no rooms saved.
        /// </summary>
        private static WorldDungeon FromZdo(string world, ZDO zdo, string location, Vector3 entrance)
        {
            List<(int, Vec3, Vec3)> saved;
            if (zdo.GetByteArray(ZDOVars.s_roomData, out var data)) saved = SavedRooms.Read(data);
            else
            {
                saved = new List<(int, Vec3, Vec3)>();
                var count = zdo.GetInt(ZDOVars.s_rooms);
                for (var i = 0; i < count; i++)
                {
                    var keys = SavedRooms.OldKeys(i);
                    var position = zdo.GetVec3(keys.Position, Vector3.zero);
                    var angles = zdo.GetQuaternion(keys.Turn, Quaternion.identity).eulerAngles;
                    saved.Add((zdo.GetInt(keys.Hash), new Vec3(position.x, position.y, position.z), new Vec3(angles.x, angles.y, angles.z)));
                }
            }
            if (saved.Count == 0) return null;
            var at = zdo.GetPosition();
            var turn = zdo.GetRotation();
            return new WorldDungeon
            {
                Saved = saved,
                Generator = new Vec3(at.x, at.y, at.z),
                Turn = new Quat(turn.x, turn.y, turn.z, turn.w),
                // By where its location stands: the game numbers its objects afresh each time it loads the world.
                Id = DungeonMaps.PlaceIdOf(world, new Vec3(entrance.x, entrance.y, entrance.z)),
                Location = location,
                Entrance = new Vec3(entrance.x, entrance.y, entrance.z),
            };
        }

        /// <summary>No zone around a place's own, for looking through the objects of one zone.</summary>
        private static readonly SimulationDistance OneZone = new SimulationDistance(0, 0);

        private static readonly List<ZDO> InZone = new List<ZDO>();

        /// <summary>
        /// The dungeons lying in explored ground whose rooms the world keeps, as the game running the
        /// world has them: each location placed in the ground, its zone's objects looked through for
        /// a generator standing where the game counts a place as inside (<c>Character.InInterior</c>)
        /// with rooms saved, the one nearest its entrance; a location whose zone never loaded has none
        /// yet. Those of the kinds of dungeon named that hold no rooms yet, placed or not, are told by
        /// their entrance alone, as the game knows where every location lies.
        /// </summary>
        public static List<WorldDungeon> InGround(string world, ExploredGrid grid, ICollection<string> kinds, List<(string Location, Vector3 At)> entrances)
        {
            var found = new List<WorldDungeon>();
            var zones = ZoneSystem.instance;
            var zdos = ZDOMan.instance;
            if (zones == null || zdos == null || grid == null || world == null) return found;
            foreach (var instance in zones.m_locationInstances.Values)
            {
                if (instance.m_location == null) continue;
                var at = instance.m_position;
                if (!grid.Explored(at.x, at.z)) continue;
                var name = CompendiumMap.NameOf(instance.m_location);
                if (!instance.m_placed)
                {
                    if (kinds != null && kinds.Contains(name)) entrances.Add((name, at));
                    continue;
                }
                var dungeon = Placed(world, instance);
                if (dungeon != null) found.Add(dungeon);
                else if (kinds != null && kinds.Contains(name)) entrances.Add((name, at));
            }
            return found;
        }

        /// <summary>
        /// The dungeon of a location placed in the world, as the game running the world has it: its
        /// zone's objects looked through for a generator standing where the game counts a place as
        /// inside (<c>Character.InInterior</c>) with rooms saved, the one nearest its entrance; none
        /// for a location with no dungeon, or one whose zone never loaded.
        /// </summary>
        public static WorldDungeon Placed(string world, ZoneSystem.LocationInstance instance)
        {
            var zdos = ZDOMan.instance;
            if (zdos == null || world == null || instance.m_location == null || !instance.m_placed) return null;
            var at = instance.m_position;
            InZone.Clear();
            zdos.FindSectorObjects(ZoneSystem.GetZone(at), OneZone, InZone);
            ZDO generator = null;
            var nearest = float.MaxValue;
            foreach (var zdo in InZone)
            {
                var p = zdo.GetPosition();
                if (!Character.InInterior(p) || !HasRooms(zdo)) continue;
                var apart = (p.x - at.x) * (p.x - at.x) + (p.z - at.z) * (p.z - at.z);
                if (apart >= nearest) continue;
                nearest = apart;
                generator = zdo;
            }
            return generator != null ? FromZdo(world, generator, CompendiumMap.NameOf(instance.m_location), at) : null;
        }

        /// <summary>Whether a network object holds rooms a generator saved, in either of the game's ways of keeping them.</summary>
        private static bool HasRooms(ZDO zdo) => zdo.GetByteArray(ZDOVars.s_roomData, out _) || zdo.GetInt(ZDOVars.s_rooms) > 0;

        /// <summary>The size of each kind of room standing under the generator, as the game places it (<c>DungeonGenerator.PlaceRoom</c> makes each room under it).</summary>
        private static void Measure(DungeonGenerator generator, WorldDungeon dungeon)
        {
            foreach (var room in generator.GetComponentsInChildren<Room>())
            {
                if (room.m_endCap || room.m_divider) continue;
                dungeon.Sizes[room.GetHash()] = new Vec3(room.m_size.x, room.m_size.y, room.m_size.z);
            }
        }
    }
}
