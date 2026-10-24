using System;
using System.Collections.Generic;

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
    }

    /// <summary>A dungeon marked on Valheim's big map: walked by you where it names no ground, else lying in the ground named.</summary>
    internal sealed class DungeonMark
    {
        /// <summary>The dungeon, as the world names it (<see cref="DungeonMaps.IdOf"/>, <see cref="DungeonMaps.EntranceIdOf"/>).</summary>
        public string Id;

        /// <summary>Its rooms, as far as known, and where it stands.</summary>
        public DungeonMap Map;

        /// <summary>The ground it lies in; none for one walked by you.</summary>
        public WorldSource? From;

        /// <summary>The server's record of one walked by you, as last sent, its rooms walked by others counted with yours; none.</summary>
        public DungeonMap Server;

        /// <summary>How its mark looks.</summary>
        public MarkLook Look;
    }

    /// <summary>
    /// The dungeons marked on Valheim's big map: every one walked by you in the world played,
    /// ticked where every room of it was walked, by you or by you and others on this server
    /// together, whatever the compendium shows of the server's rooms, then those lying in a ground
    /// whose pin is on while its row shows anything (<see cref="CompendiumBook.MarksDungeonsNow"/>),
    /// in the box's order, each once: hollow where the row shows With insides and anyone on this
    /// server walked a room of it, ticked where every room was, else by its entrance alone.
    /// </summary>
    internal static class DungeonMarks
    {
        /// <summary>
        /// The marks, from the dungeons walked by you and those each ground holds; walked whole told
        /// by the kinds of room <paramref name="roomOf"/> reads, none where it is not given, the
        /// server's record of one walked by you (<paramref name="serverOf"/>) counted with yours.
        /// </summary>
        public static List<DungeonMark> Of(CompendiumBook book, IEnumerable<(string Id, DungeonMap Map)> walked, Func<WorldSource, IEnumerable<(string Id, DungeonMap Map)>> inGround,
            Func<int, RoomShape> roomOf = null, Func<string, DungeonMap> serverOf = null)
        {
            var marks = new List<DungeonMark>();
            var marked = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, map) in walked)
            {
                if (id == null || map == null || !marked.Add(id)) continue;
                var server = serverOf?.Invoke(id);
                marks.Add(new DungeonMark { Id = id, Map = map, Server = server, Look = roomOf != null && WalkedWhole(map, server, roomOf) ? MarkLook.Whole : MarkLook.Yours });
            }
            if (book == null) return marks;
            foreach (var source in WorldSources.All)
            {
                if (!book.MarksDungeonsNow(source)) continue;
                var insides = book.Shows(source) == SourceLevel.WithInsides;
                foreach (var (id, map) in inGround(source))
                {
                    if (id == null || map == null || !marked.Add(id)) continue;
                    var look = !insides || map.Walked.Count == 0 ? MarkLook.Entrance : roomOf != null && WalkedWhole(map, null, roomOf) ? MarkLook.ServerWhole : MarkLook.Server;
                    marks.Add(new DungeonMark { Id = id, Map = map, From = source, Look = look });
                }
            }
            return marks;
        }

        /// <summary>Whether every room of a dungeon was walked, end caps and dividers left out, by you and anyone on this server together where its record is given.</summary>
        private static bool WalkedWhole(DungeonMap map, DungeonMap server, Func<int, RoomShape> roomOf)
        {
            var (walked, rooms, _) = DungeonMaps.WalkedTogether(map, server, roomOf);
            return rooms > 0 && walked >= rooms;
        }
    }
}
