using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The kinds list on Valheim's map: a small window of the kinds kept, dragged by its title,
    /// folded to it and made wider or narrower by its corner, kept with the panel's place
    /// (<see cref="PanelPlace"/>) as where it stands from the map's top left and how wide; never
    /// off the map, never narrower than a chip stands nor wider than the map. Its chips flow into
    /// rows as wide as it, and past so many rows it scrolls.
    /// </summary>
    internal static class KindsList
    {
        /// <summary>The title row's height, a chip's, the gap between chips and the room round them, in the screen's points before the panel's scale.</summary>
        public const float TitleHeight = 26f, ChipHeight = 26f, Gap = 4f, Pad = 6f;

        /// <summary>How narrow the list may be made, in points before the panel's scale.</summary>
        public const float LeastWidth = 160f;

        /// <summary>How wide a list never moved stands, of the map's width.</summary>
        public const float StartShare = 0.38f;

        /// <summary>How many rows of chips stand before the list scrolls.</summary>
        public const int MostRows = 6;

        /// <summary>
        /// Where the list stands over a map at a point and size: where it was left, from the map's
        /// top left and as wide, or at the top left at a share of the map's width; brought inside
        /// the map, its title row at least, and between its least width and the map's.
        /// </summary>
        public static (float X, float Y, float Width) Place(float mapX, float mapY, float mapWidth, float mapHeight, (float X, float Y, float Width)? kept, float scale)
        {
            var least = LeastWidth * scale;
            var most = Math.Max(least, mapWidth - 24f * scale);
            var width = Math.Min(most, Math.Max(least, kept?.Width ?? mapWidth * StartShare));
            var x = mapX + (kept?.X ?? 12f * scale);
            var y = mapY + (kept?.Y ?? 12f * scale);
            x = Math.Max(mapX, Math.Min(x, mapX + mapWidth - width));
            y = Math.Max(mapY, Math.Min(y, mapY + mapHeight - TitleHeight * scale));
            return (x, y, width);
        }

        /// <summary>Chips of these widths flowed into rows of a width: each one's place along its row and its row; a chip wider than the row alone in its own.</summary>
        public static (float X, int Row)[] Flow(IReadOnlyList<float> widths, float inner, float gap)
        {
            var placed = new (float X, int Row)[widths.Count];
            float x = 0f;
            var row = 0;
            for (var i = 0; i < widths.Count; i++)
            {
                if (x > 0f && x + widths[i] > inner)
                {
                    row++;
                    x = 0f;
                }
                placed[i] = (x, row);
                x += widths[i] + gap;
            }
            return placed;
        }

        /// <summary>How many rows flowed chips take; none for none.</summary>
        public static int Rows(IReadOnlyList<(float X, int Row)> flow) => flow.Count == 0 ? 0 : flow[flow.Count - 1].Row + 1;

        /// <summary>
        /// The kinds standing gathered into chips by what each groups under, a family's kinds, as
        /// a place's numbered ones, under one, a kind of no family alone: each chip's key and its
        /// kinds' places in the list, in the order first met.
        /// </summary>
        public static List<(string Key, List<int> Members)> Group(IReadOnlyList<string> groupsUnder)
        {
            var groups = new List<(string Key, List<int> Members)>();
            var at = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < groupsUnder.Count; i++)
            {
                var key = groupsUnder[i] ?? "";
                if (!at.TryGetValue(key, out var index))
                {
                    at[key] = index = groups.Count;
                    groups.Add((key, new List<int>()));
                }
                groups[index].Members.Add(i);
            }
            return groups;
        }

        /// <summary>How many rows stand at once, the rest scrolled to.</summary>
        public static int RowsShown(int rows) => Math.Min(rows, MostRows);
    }
}
