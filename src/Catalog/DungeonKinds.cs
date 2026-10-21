using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The kinds of place whose dungeon lies inside, as read of their places (a plan of the game's
    /// dungeon kind, not a camp's): for the dungeons of a ground asked of the server, entrances
    /// among them, and With insides dimmed on a row none of whose places is one. Known once the
    /// places are read or were kept from a reading before.
    /// </summary>
    internal static class DungeonKinds
    {
        static DungeonKinds() => WorldCaches.Register(nameof(DungeonKinds), Forget);

        /// <summary>Lets go of the kinds read and the catalog they were read from, with the world.</summary>
        private static void Forget()
        {
            Kinds.Clear();
            _of = null;
            _regroups = -1;
        }

        /// <summary>The plan of a dungeon lying inside, as the game names its generator's way.</summary>
        private const string InsideAlgorithm = "Dungeon";

        private static readonly HashSet<string> Kinds = new HashSet<string>(StringComparer.Ordinal);
        private static EntryCatalog _of;
        private static int _regroups = -1;

        /// <summary>Whether a kind of place, by its prefab's name, is a dungeon lying inside.</summary>
        public static readonly Func<string, bool> Is = location => location != null && Read().Contains(location);

        /// <summary>Every such kind of place, as the catalog reads now.</summary>
        public static IReadOnlyCollection<string> All => Read();

        private static HashSet<string> Read()
        {
            var catalog = WorldCatalog.Current;
            if (ReferenceEquals(catalog, _of) && (catalog == null || catalog.Regroups == _regroups)) return Kinds;
            _of = catalog;
            _regroups = catalog?.Regroups ?? -1;
            Kinds.Clear();
            if (catalog == null) return Kinds;
            foreach (var entry in catalog.All)
            {
                if (entry.Kind == Kind.Location && entry.Source is PlaceSource place && !place.IsRoom && place.Contents?.Dungeon?.Algorithm == InsideAlgorithm) Kinds.Add(place.Prefab);
            }
            return Kinds;
        }
    }
}
