using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The kinds of dungeon room the character has stood in, in any world, as all else it met
    /// counts in every world, from the rooms its kept maps mark walked (<see cref="DungeonWalks"/>,
    /// <see cref="DungeonMaps.RoomsWalked"/>), each by the hash the game saves a
    /// room under (<c>DungeonGenerator.Save</c>: its prefab name's stable hash), for the compendium's
    /// strict rules (<see cref="CompendiumView.RoomsWalked"/>). Gathered again only as the maps change.
    /// </summary>
    internal static class WalkedRooms
    {
        static WalkedRooms() => WorldCaches.Register(nameof(WalkedRooms), Forget);

        private static HashSet<int> _hashes = new HashSet<int>();
        private static int _stamp = -1;
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
        public static readonly Func<string, bool> Walked = name => !string.IsNullOrEmpty(name) && _hashes.Contains(name.GetStableHashCode());

        private static void Gather()
        {
            var maps = DungeonWalks.Maps;
            if (maps == null || maps.Stamp == _stamp) return;
            _stamp = maps.Stamp;
            var walked = maps.RoomsWalked();
            if (walked.SetEquals(_hashes)) return;
            _hashes = walked;
            _version++;
        }

        /// <summary>Forgets what was gathered, for another world or character.</summary>
        public static void Forget()
        {
            _hashes = new HashSet<int>();
            _stamp = -1;
            _version++;
        }
    }
}
