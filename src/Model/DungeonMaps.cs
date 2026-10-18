using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>A dungeon a character has been in: its rooms as the world saved them, where its entrance is, and the rooms stood in by their place in that list.</summary>
    internal sealed class DungeonMap
    {
        public string Location;
        public List<(int Hash, Vec3 Position, Vec3 Angles)> Rooms;
        public Vec3 Entrance;
        public readonly SortedSet<int> Walked = new SortedSet<int>();
    }

    /// <summary>
    /// The dungeons a character has been in (<see cref="DungeonMap"/>), each by the world and its
    /// generator's network id: kept as first read on being in or at it, and walked room by room.
    /// </summary>
    internal sealed class DungeonMaps
    {
        private readonly Dictionary<string, DungeonMap> _maps = new Dictionary<string, DungeonMap>(StringComparer.Ordinal);

        /// <summary>Counts up each time a map is kept, walked further or forgotten.</summary>
        public int Stamp { get; private set; }

        /// <summary>Every map kept, by its id, in the order they were kept.</summary>
        public IEnumerable<(string Id, DungeonMap Map)> All => _maps.Select(m => (m.Key, m.Value));

        /// <summary>The id a map is kept by: the world's, then its generator's network id (the user who made it and its number); none outside a world.</summary>
        public static string IdOf(string world, long user, uint id) => world != null ? world + ":" + Stored.Count(user) + ":" + Stored.Count(id) : null;

        public DungeonMap Of(string id) => id != null && _maps.TryGetValue(id, out var map) ? map : null;

        /// <summary>Keeps a dungeon been in, its rooms and entrance as first read; one kept already stays as it was.</summary>
        public DungeonMap Keep(string id, string location, List<(int Hash, Vec3 Position, Vec3 Angles)> rooms, Vec3 entrance)
        {
            if (_maps.TryGetValue(id, out var kept)) return kept;
            var map = new DungeonMap { Location = location, Rooms = rooms, Entrance = entrance };
            _maps[id] = map;
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
            Stamp++;
            return true;
        }

        public void ForgetAll()
        {
            if (_maps.Count == 0) return;
            _maps.Clear();
            Stamp++;
        }
    }
}
