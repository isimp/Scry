using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What anyone on this server walked inside the dungeons each source of the world holds
    /// (<see cref="ServerWalks"/>), for what a source shows with insides: a ground's, the dungeons
    /// lying in it as the server answered (<see cref="DungeonsInGround"/>), its walks on them in;
    /// a source of single places, the server's records of its kinds of dungeon
    /// (<see cref="GroupMapsLink"/>); the character's own walks added to each, all of them to its
    /// own ground, as it walked them there. Gathered again only as any of it changes.
    /// </summary>
    internal static class SourceWalks
    {
        static SourceWalks() => WorldCaches.Register(nameof(SourceWalks), Forget);

        private static ServerWalks _walks = ServerWalks.None;
        private static (int Ground, int Server, int Yours, int Sources, int Regroups, string World) _stamp;

        /// <summary>Counts up each time the walks gathered show something other than before, so a walk in a room walked already counts nothing afresh.</summary>
        public static int Version { get; private set; }

        /// <summary>Whether the walks were gathered since the world or character last changed.</summary>
        private static bool _gathered;

        /// <summary>The walks as they stand, gathered again where anything they come of changed.</summary>
        public static ServerWalks Of(CompendiumBook book)
        {
            var world = CompendiumMap.World;
            var catalog = WorldCatalog.Current;
            var stamp = (DungeonsInGround.Stamp, GroupMapsLink.Group.Stamp, DungeonWalks.Maps.Stamp, book?.SourcesVersion ?? -1, catalog?.Regroups ?? -1, world);
            if (_gathered && stamp == _stamp) return _walks;
            _gathered = true;
            _stamp = stamp;
            var walks = book == null || world == null ? ServerWalks.None : ServerWalks.Of(source => Held(book, world, source), RoomsByHash(catalog).TryGet, name => name.GetStableHashCode());
            if (walks.SameAs(_walks)) return _walks;
            _walks = walks;
            Version++;
            return _walks;
        }

        /// <summary>The dungeons a source holds, as far as anyone walked them, the character's own walks taken in.</summary>
        private static IEnumerable<DungeonMap> Held(CompendiumBook book, string world, WorldSource source)
        {
            var held = new DungeonMaps();
            var yours = DungeonWalks.Maps;
            // The census names kinds alone and shows no insides.
            if (source == WorldSource.WholeWorld) yield break;
            if (WorldSources.HasGround(source))
            {
                foreach (var (id, map) in DungeonsInGround.Of(source))
                {
                    held.Merge(One(id, map));
                    var own = yours.Of(id);
                    if (own != null) held.Merge(One(id, own));
                }
                if (source == WorldSource.Explored) foreach (var (id, map) in yours.OfWorld(world)) held.Merge(One(id, map));
            }
            else
            {
                foreach (var kind in book.Found(world, source))
                {
                    if (!DungeonKinds.Is(kind)) continue;
                    foreach (var (id, map) in GroupMapsLink.Group.OfLocation(world, kind)) held.Merge(One(id, map));
                    foreach (var (id, map) in yours.OfLocation(world, kind)) held.Merge(One(id, map));
                }
            }
            foreach (var (_, map) in held.All) yield return map;
        }

        private static DungeonMaps One(string id, DungeonMap map)
        {
            var one = new DungeonMaps();
            one.Keep(id, map.Location, map.Rooms, map.Entrance, map.Generator, map.Turn);
            foreach (var room in map.Walked) one.Walk(id, room);
            return one;
        }

        /// <summary>The shapes of the kinds of room read, by the hash the game saves a room under.</summary>
        private static RoomShapes RoomsByHash(EntryCatalog catalog)
        {
            var shapes = new RoomShapes();
            if (catalog == null) return shapes;
            foreach (var entry in catalog.All)
            {
                if (entry.Source is PlaceSource place && place.IsRoom && place.Contents?.Room != null) shapes.ByHash[place.Prefab.GetStableHashCode()] = place.Contents.Room;
            }
            return shapes;
        }

        private sealed class RoomShapes
        {
            public readonly Dictionary<int, RoomShape> ByHash = new Dictionary<int, RoomShape>();

            public RoomShape TryGet(int hash) => ByHash.TryGetValue(hash, out var shape) ? shape : null;
        }

        private static void Forget()
        {
            _walks = ServerWalks.None;
            _stamp = default;
            _gathered = false;
            Version++;
        }
    }
}
