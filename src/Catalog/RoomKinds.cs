using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The kinds of room the places read hold, by the hash the game saves a room under, so a
    /// dungeon's walked rooms are counted out of the rooms one can walk, its end caps and dividers
    /// left out (<see cref="DungeonMaps.Walked"/>), on a page, in the plan box and under a mark on
    /// Valheim's map alike. Known once the places are read or were kept from a reading before;
    /// gathered again as the catalog regroups.
    /// </summary>
    internal static class RoomKinds
    {
        static RoomKinds() => WorldCaches.Register(nameof(RoomKinds), Forget);

        private static readonly Dictionary<int, RoomShape> ByHash = new Dictionary<int, RoomShape>();
        private static EntryCatalog _of;
        private static int _regroups = -1;

        /// <summary>Lets go of the kinds read and the catalog they were read from, with the world.</summary>
        private static void Forget()
        {
            ByHash.Clear();
            _of = null;
            _regroups = -1;
        }

        /// <summary>The kind of room the game saves under a hash; none for one not read.</summary>
        public static readonly Func<int, RoomShape> Of = hash => Read().TryGetValue(hash, out var shape) ? shape : null;

        /// <summary>How many of a kept map's rooms were walked, of the rooms one can walk.</summary>
        public static (int Walked, int Rooms) Walked(DungeonMap map) => DungeonMaps.Walked(map, Of);

        private static Dictionary<int, RoomShape> Read()
        {
            var catalog = WorldCatalog.Current;
            if (ReferenceEquals(catalog, _of) && (catalog == null || catalog.Regroups == _regroups)) return ByHash;
            _of = catalog;
            _regroups = catalog?.Regroups ?? -1;
            ByHash.Clear();
            if (catalog == null) return ByHash;
            foreach (var entry in catalog.All)
            {
                if (entry.Source is PlaceSource place && place.IsRoom && place.Contents?.Room != null) ByHash[place.Prefab.GetStableHashCode()] = place.Contents.Room;
            }
            return ByHash;
        }
    }
}
