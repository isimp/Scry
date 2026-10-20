using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>How a dungeon's mark on Valheim's big map looks, by who walked it as far as it shows.</summary>
    internal enum MarkLook
    {
        /// <summary>Walked by you: a filled doorway.</summary>
        Yours,

        /// <summary>Walked on this server, shown with insides: a hollow doorway, ringed.</summary>
        Server,

        /// <summary>Its entrance alone: nobody on this server walked it, or its row shows no insides.</summary>
        Entrance,
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

        /// <summary>How its mark looks.</summary>
        public MarkLook Look;
    }

    /// <summary>
    /// The dungeons marked on Valheim's big map: every one walked by you in the world played, then
    /// those lying in a ground whose pin is on while its row shows anything
    /// (<see cref="CompendiumBook.MarksDungeonsNow"/>), in the box's order, each once: hollow where
    /// the row shows With insides and anyone on this server walked a room of it, else by its
    /// entrance alone.
    /// </summary>
    internal static class DungeonMarks
    {
        /// <summary>The marks, from the dungeons walked by you and those each ground holds.</summary>
        public static List<DungeonMark> Of(CompendiumBook book, IEnumerable<(string Id, DungeonMap Map)> walked, Func<WorldSource, IEnumerable<(string Id, DungeonMap Map)>> inGround)
        {
            var marks = new List<DungeonMark>();
            var marked = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, map) in walked)
            {
                if (id != null && map != null && marked.Add(id)) marks.Add(new DungeonMark { Id = id, Map = map, Look = MarkLook.Yours });
            }
            if (book == null) return marks;
            foreach (var source in WorldSources.All)
            {
                if (!book.MarksDungeonsNow(source)) continue;
                var insides = book.Shows(source) == SourceLevel.WithInsides;
                foreach (var (id, map) in inGround(source))
                {
                    if (id == null || map == null || !marked.Add(id)) continue;
                    marks.Add(new DungeonMark { Id = id, Map = map, From = source, Look = insides && map.Walked.Count > 0 ? MarkLook.Server : MarkLook.Entrance });
                }
            }
            return marks;
        }
    }
}
