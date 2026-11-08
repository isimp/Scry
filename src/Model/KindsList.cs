using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The kinds list on Valheim's map: a small window of the kinds kept, dragged by its title,
    /// folded to it and sized by its corner, anywhere on the screen, kept with the panel's place
    /// (<see cref="PanelPlace"/>) as where it stands from the map's top left, how wide and how many
    /// rows tall; never off the screen, never narrower than a chip stands nor wider than the screen,
    /// never shorter than a row nor taller than the screen below it. Its chips flow into rows as wide
    /// as it, and past the rows it is tall enough for it scrolls.
    /// </summary>
    internal static class KindsList
    {
        /// <summary>The title row's height, a chip's, the gap between chips and the room round them, in the screen's points before the panel's scale.</summary>
        public const float TitleHeight = 26f, ChipHeight = 26f, Gap = 4f, Pad = 6f;

        /// <summary>How narrow the list may be made, in points before the panel's scale.</summary>
        public const float LeastWidth = 160f;

        /// <summary>How wide a list never moved stands, of the map's width.</summary>
        public const float StartShare = 0.38f;

        /// <summary>How many rows of chips a list never sized stands before it scrolls.</summary>
        public const int StartRows = 6;

        /// <summary>
        /// Where the list stands with a map at a point and width on a screen: where it was left,
        /// from the map's top left and as wide, beside the map too, or at the map's top left at a
        /// share of its width; brought inside the screen, its title row at least, and between its
        /// least width and the screen's.
        /// </summary>
        public static (float X, float Y, float Width) Place(float mapX, float mapY, float mapWidth, float screenWidth, float screenHeight, (float X, float Y, float Width)? kept, float scale)
        {
            var least = LeastWidth * scale;
            var most = Math.Max(least, screenWidth - 24f * scale);
            var width = Math.Min(most, Math.Max(least, kept?.Width ?? mapWidth * StartShare));
            var x = mapX + (kept?.X ?? 12f * scale);
            var y = mapY + (kept?.Y ?? 12f * scale);
            x = Math.Max(0f, Math.Min(x, screenWidth - width));
            y = Math.Max(0f, Math.Min(y, screenHeight - TitleHeight * scale));
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

        /// <summary>
        /// Categories laid out: each its heading's row, then under it, unless folded, its chips flowed
        /// in the rows left beside an indent. Each heading's row, each category's chips, how many rows in all.
        /// </summary>
        public static (int[] Headings, (float X, int Row)[][] Chips, int Rows) Laid(IReadOnlyList<(bool Folded, IReadOnlyList<float> Widths)> categories, float inner, float gap, float indent)
        {
            var headings = new int[categories.Count];
            var chips = new (float X, int Row)[categories.Count][];
            var row = 0;
            for (var i = 0; i < categories.Count; i++)
            {
                headings[i] = row++;
                var (folded, widths) = categories[i];
                if (folded)
                {
                    chips[i] = Array.Empty<(float X, int Row)>();
                    continue;
                }
                var flow = Flow(widths, inner - indent, gap);
                chips[i] = new (float X, int Row)[flow.Length];
                for (var c = 0; c < flow.Length; c++) chips[i][c] = (flow[c].X + indent, flow[c].Row + row);
                row += Rows(flow);
            }
            return (headings, chips, row);
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

        /// <summary>How many rows stand at once of so many, the rest scrolled to; no more than there are, so the list is no taller than it holds.</summary>
        public static int RowsShown(int rows, int allowed) => Math.Min(rows, allowed);

        /// <summary>How many rows a list with its top at a height may stand: as it was sized, or so many never sized; at least one, and no more than fit on the screen below it.</summary>
        public static int RowsAllowed(int? kept, float top, float screenHeight, float scale)
        {
            var fit = (int)Math.Floor((screenHeight - top - (TitleHeight + Pad * 2f - Gap) * scale) / ((ChipHeight + Gap) * scale));
            return Math.Max(1, Math.Min(kept ?? StartRows, fit));
        }

        /// <summary>The rows a list with its top at a height stands with its foot at another, the nearest; at least one.</summary>
        public static int RowsAt(float foot, float top, float scale) =>
            Math.Max(1, (int)Math.Round((foot - top - (TitleHeight + Pad * 2f - Gap) * scale) / ((ChipHeight + Gap) * scale)));
    }
}
