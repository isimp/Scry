using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>A dungeon marked on Valheim's big map: walked where it names no ground, else shown with its insides from the ground named.</summary>
    internal sealed class DungeonMark
    {
        /// <summary>The dungeon, as the world names it (<see cref="DungeonMaps.IdOf"/>).</summary>
        public string Id;

        /// <summary>Its rooms and where it stands.</summary>
        public DungeonMap Map;

        /// <summary>The ground it shows from with its insides; none for one walked.</summary>
        public WorldSource? From;
    }

    /// <summary>
    /// The dungeons marked on Valheim's big map: every one walked in the world played, then those
    /// lying in a ground whose pin is on while its row shows With insides
    /// (<see cref="CompendiumBook.MarksDungeonsNow"/>), in the box's order, each once.
    /// </summary>
    internal static class DungeonMarks
    {
        /// <summary>The marks, from the dungeons walked and those each ground holds.</summary>
        public static List<DungeonMark> Of(CompendiumBook book, IEnumerable<(string Id, DungeonMap Map)> walked, Func<WorldSource, IEnumerable<(string Id, DungeonMap Map)>> inGround)
        {
            var marks = new List<DungeonMark>();
            var marked = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (id, map) in walked)
            {
                if (id != null && map != null && marked.Add(id)) marks.Add(new DungeonMark { Id = id, Map = map });
            }
            if (book == null) return marks;
            foreach (var source in WorldSources.All)
            {
                if (!book.MarksDungeonsNow(source)) continue;
                foreach (var (id, map) in inGround(source))
                {
                    if (id != null && map != null && marked.Add(id)) marks.Add(new DungeonMark { Id = id, Map = map, From = source });
                }
            }
            return marks;
        }
    }
}
