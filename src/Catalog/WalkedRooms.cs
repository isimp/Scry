using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The kinds of dungeon room the character has stood in, in the world played, from the rooms
    /// its kept maps mark walked (<see cref="DungeonWalks"/>), each by the hash the game saves a
    /// room under (<c>DungeonGenerator.Save</c>: its prefab name's stable hash), for the compendium's
    /// strict rules (<see cref="CompendiumView.RoomsWalked"/>). Gathered again only as the maps change.
    /// </summary>
    internal static class WalkedRooms
    {
        static WalkedRooms() => WorldCaches.Register(nameof(WalkedRooms), Forget);

        private static readonly HashSet<int> Hashes = new HashSet<int>();
        private static int _stamp = -1;
        private static string _world;
        private static int _version;

        /// <summary>Counts every change of which kinds of room were walked, gathered first where the maps changed.</summary>
        public static int Version
        {
            get
            {
                Gather();
                return _version;
            }
        }

        /// <summary>Whether the character has stood in a room of this kind, by its prefab's name.</summary>
        public static readonly Func<string, bool> Walked = name => !string.IsNullOrEmpty(name) && Hashes.Contains(name.GetStableHashCode());

        private static void Gather()
        {
            var maps = DungeonWalks.Maps;
            var world = CompendiumMap.World;
            if (maps == null || (maps.Stamp == _stamp && world == _world)) return;
            _stamp = maps.Stamp;
            _world = world;
            var before = Hashes.Count;
            var changed = false;
            foreach (var (_, map) in maps.OfWorld(world))
            {
                if (map?.Rooms == null) continue;
                foreach (var at in map.Walked)
                {
                    if (at >= 0 && at < map.Rooms.Count && Hashes.Add(map.Rooms[at].Hash)) changed = true;
                }
            }
            if (changed || Hashes.Count != before) _version++;
        }

        /// <summary>Forgets what was gathered, for another world or character.</summary>
        public static void Forget()
        {
            Hashes.Clear();
            _stamp = -1;
            _world = null;
            _version++;
        }
    }
}
