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
    }

    /// <summary>
    /// The dungeon of the location the player stands in or at (<c>Location.GetLocation</c>, as the
    /// compendium has it), read from its generator's saved rooms once it is loaded, which is only
    /// while its zone is. The generator stands in the location's zone, made from its own network
    /// object; the rooms are read once, as they never change once laid out.
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
            var player = Player.m_localPlayer;
            if (player == null) return null;
            var place = Location.GetLocation(player.transform.position);
            if (place == null || Utils.GetPrefabName(place.gameObject) != location) return null;
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
            return _read ?? (_read = Read(_generator));
        }

        /// <summary>The rooms a generator saved (<c>DungeonGenerator.Load</c> reads them back the same way, the older keys too); null before its network object has any.</summary>
        private static WorldDungeon Read(DungeonGenerator generator)
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
            return new WorldDungeon { Saved = saved, Generator = new Vec3(at.x, at.y, at.z), Turn = new Quat(turn.x, turn.y, turn.z, turn.w) };
        }
    }
}
