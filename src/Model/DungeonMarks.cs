using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>How a dungeon's mark on Valheim's big map looks, by who walked it as far as it shows.</summary>
    internal enum MarkLook
    {
        /// <summary>Walked by you: a filled doorway.</summary>
        Yours,

        /// <summary>Every room of it walked on this server, you among those who walked it, or you alone: a tick in place of the doorway.</summary>
        Whole,

        /// <summary>Walked on this server, shown with insides: a hollow doorway, ringed.</summary>
        Server,

        /// <summary>Its entrance alone: nobody on this server walked it, or its row shows no insides.</summary>
        Entrance,

        /// <summary>Walked whole on this server by others, shown with insides: ringed, with a tick in outline.</summary>
        ServerWhole,

        /// <summary>A place whose rooms stand in the world, as the Sealed Tower, that you stood in: filled as yours, no rooms counted.</summary>
        Visited,
    }

    /// <summary>A dungeon marked on Valheim's big map: walked by you, or its entrance seen.</summary>
    internal sealed class DungeonMark
    {
        /// <summary>The dungeon, as the world names it (<see cref="DungeonMaps.IdOf"/>, <see cref="DungeonMaps.EntranceIdOf"/>).</summary>
        public string Id;

        /// <summary>Its rooms, as far as known, and where it stands.</summary>
        public DungeonMap Map;

        /// <summary>The server's record of one walked by you, as last sent, its rooms walked by others counted with yours; none.</summary>
        public DungeonMap Server;

        /// <summary>How its mark looks.</summary>
        public MarkLook Look;
    }

    /// <summary>
    /// The dungeons marked on Valheim's big map, where their places stand as Show on map shows
    /// them: every one walked by you in the world played, ticked where every room of it was
    /// walked, by you or by you and others on this server together, whatever the compendium shows
    /// of the server's rooms; then every one whose entrance your character saw, where the world
    /// remembers what is seen, each once:
    /// hollow where anyone on this server walked a room of it and Ground you explored, the ground
    /// it was seen in, shows With insides, ticked where every room was, else by its entrance
    /// alone. One lying in ground never come near gets no mark: its page's Show on map shades
    /// its square.
    /// </summary>
    internal static class DungeonMarks
    {
        /// <summary>
        /// The marks, from the dungeons walked by you and the entrances seen (<see cref="Seen"/>);
        /// walked whole told by the kinds of room <paramref name="roomOf"/> reads, none where it is
        /// not given; the server's record of each (<paramref name="serverOf"/>), counted with
        /// yours for one you walked, telling others' walks for one seen.
        /// </summary>
        public static List<DungeonMark> Of(CompendiumBook book, IEnumerable<(string Id, DungeonMap Map)> walked, IEnumerable<(string Id, string Location, int X, int Z)> seen,
            Func<int, RoomShape> roomOf = null, Func<string, DungeonMap> serverOf = null)
        {
            var marks = new List<DungeonMark>();
            var marked = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, map) in walked)
            {
                if (id == null || map == null || !marked.Add(id)) continue;
                var server = serverOf?.Invoke(id);
                // Kept without rooms: a place in the world you stood in.
                var look = map.Rooms == null || map.Rooms.Count == 0 ? MarkLook.Visited : roomOf != null && WalkedWhole(map, server, roomOf) ? MarkLook.Whole : MarkLook.Yours;
                marks.Add(new DungeonMark { Id = id, Map = map, Server = server, Look = look });
            }
            var insides = book != null && book.Shows(WorldSource.Explored) == SourceLevel.WithInsides;
            foreach (var (id, location, x, z) in seen)
            {
                if (id == null || location == null || !marked.Add(id)) continue;
                // Its walks kept at every level, so its words say others walked it; its look hollow only with insides.
                var map = SeenAt(location, x, z, serverOf?.Invoke(id), insides);
                var look = !insides || map.Walked.Count == 0 ? MarkLook.Entrance : roomOf != null && WalkedWhole(map, null, roomOf) ? MarkLook.ServerWhole : MarkLook.Server;
                marks.Add(new DungeonMark { Id = id, Map = map, Look = look });
            }
            return marks;
        }

        /// <summary>
        /// The dungeons whose entrances a character's sight kept in a world, each by its id, its
        /// kind and where it stands: the spots of each kind of dungeon (<paramref name="spotsOf"/>,
        /// by the place's entry key), so an entrance seen is the same dungeon as one walked there;
        /// none where the world keeps no map or its server remembers nothing seen
        /// (<see cref="SeenOnMap.Remembers"/>), as no other kind's spots stand then.
        /// </summary>
        public static IEnumerable<(string Id, string Location, int X, int Z)> Seen(string world, IEnumerable<string> dungeonKinds, Func<string, IReadOnlyList<(int X, int Z)>> spotsOf, bool remembered)
        {
            if (!remembered) yield break;
            foreach (var kind in dungeonKinds)
                foreach (var (x, z) in spotsOf(EntryKeys.For(Kind.Location, kind)))
                    yield return (DungeonMaps.EntranceIdOf(world, x, z), kind, x, z);
        }

        /// <summary>
        /// A dungeon seen by its entrance, standing there, with the rooms others on this server
        /// walked where its record is given; its rooms laid out as the record lays them only with
        /// insides, so below them a click stands an example in, as for an entrance alone.
        /// </summary>
        private static DungeonMap SeenAt(string location, int x, int z, DungeonMap record, bool insides)
        {
            var map = new DungeonMap { Location = location, Entrance = new Vec3(x, 0f, z), Rooms = new List<(int, Vec3, Vec3)>() };
            if (record == null) return map;
            if (insides)
            {
                map.Rooms = record.Rooms;
                map.Generator = record.Generator;
                map.Turn = record.Turn;
            }
            map.Walked.UnionWith(record.Walked);
            map.Traced.UnionWith(record.Traced);
            map.FirstDay = record.FirstDay;
            map.LastDay = record.LastDay;
            return map;
        }

        /// <summary>Whether every room of a dungeon was walked, end caps and dividers left out, by you and anyone on this server together where its record is given.</summary>
        private static bool WalkedWhole(DungeonMap map, DungeonMap server, Func<int, RoomShape> roomOf)
        {
            var (walked, rooms, _) = DungeonMaps.WalkedTogether(map, server, roomOf);
            return rooms > 0 && walked >= rooms;
        }
    }
}
