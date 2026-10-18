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

        /// <summary>The rooms a generator saved (<c>DungeonGenerator.Load</c> reads them back the same way, the older keys too); null before its network object has any.</summary>
        private static WorldDungeon Read(DungeonGenerator generator, Location place)
        {
            var view = generator.GetComponent<ZNetView>();
            var zdo = view != null ? view.GetZDO() : null;
            if (zdo == null) return null;
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
            var at = generator.transform.position;
            var turn = generator.transform.rotation;
            var entrance = place.transform.position;
            return new WorldDungeon
            {
                Saved = saved,
                Generator = new Vec3(at.x, at.y, at.z),
                Turn = new Quat(turn.x, turn.y, turn.z, turn.w),
                Id = DungeonMaps.IdOf(CompendiumMap.World, zdo.m_uid.UserID, zdo.m_uid.ID),
                Location = Utils.GetPrefabName(place.gameObject),
                Entrance = new Vec3(entrance.x, entrance.y, entrance.z),
            };
        }

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
