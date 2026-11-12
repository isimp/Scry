using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The kinds list on Valheim's map: a small window of the kinds kept, dragged by its title,
    /// folded to it and sized by its corner, anywhere on the screen, kept with the panel's place
    /// (<see cref="PanelPlace"/>) as where it stands from the map's top left, how wide and how many
    /// lines tall; never off the screen, never narrower than a row stands nor wider than the screen,
    /// never shorter than a line nor taller than the screen below it. Each kind is a row under its
    /// category's heading, the rows flowing into as many columns as its width holds, and past the
    /// lines it is tall enough for it scrolls.
    /// </summary>
    internal static class KindsList
    {
        /// <summary>The title row's height, a line's, the gap between lines and the room round them, in the screen's points before the panel's scale.</summary>
        public const float TitleHeight = 26f, LineHeight = 24f, Gap = 2f, Pad = 6f;

        /// <summary>How wide a column of rows is at the least, before the panel's scale: a kind's mark, its name, its counts and its eye.</summary>
        public const float ColumnLeast = 190f;

        /// <summary>How narrow the list may be made, in points before the panel's scale.</summary>
        public const float LeastWidth = 160f;

        /// <summary>How wide a list never moved stands, of the map's width.</summary>
        public const float StartShare = 0.38f;

        /// <summary>How many lines a list never sized stands before it scrolls.</summary>
        public const int StartRows = 10;

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

        /// <summary>Pieces of these widths flowed into rows of a width, as the foot's switches: each one's place along its row and its row; one wider than the row alone in its own.</summary>
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

        /// <summary>How many columns of rows a width holds, each at least so wide with gaps between; at least one.</summary>
        public static int Columns(float inner, float gap, float least) => Math.Max(1, (int)Math.Floor((inner + gap) / (least + gap)));

        /// <summary>
        /// Categories laid out as lines: each its heading's line, then under it, unless folded, its
        /// kinds' rows across so many columns, left to right then down, each category on lines of
        /// its own. Each heading's line, each category's rows by column and line, how many lines in all.
        /// </summary>
        public static (int[] Headings, (int Column, int Line)[][] Rows, int Lines) LaidRows(IReadOnlyList<(bool Folded, int Kinds)> categories, int columns)
        {
            columns = Math.Max(1, columns);
            var headings = new int[categories.Count];
            var rows = new (int Column, int Line)[categories.Count][];
            var line = 0;
            for (var i = 0; i < categories.Count; i++)
            {
                headings[i] = line++;
                var (folded, kinds) = categories[i];
                if (folded || kinds <= 0)
                {
                    rows[i] = Array.Empty<(int Column, int Line)>();
                    continue;
                }
                rows[i] = new (int Column, int Line)[kinds];
                for (var k = 0; k < kinds; k++) rows[i][k] = (k % columns, line + k / columns);
                line += (kinds + columns - 1) / columns;
            }
            return (headings, rows, line);
        }

        /// <summary>
        /// What the list says folded to its title: the kinds picked to stand out by name, or that
        /// every kind stands alike; and, where anything is hidden, what: the kinds by name, the spots
        /// crossed off and the places the server told of, or every mark. Each line its label and words.
        /// </summary>
        public static List<(string Label, string Words)> FoldedLines(IReadOnlyList<string> picked, IReadOnlyList<string> hidden, bool all, bool crossedHidden, bool toldHidden)
        {
            var lines = new List<(string Label, string Words)>
            {
                picked.Count > 0 ? (SpotWords.FoldedPicked, string.Join(", ", picked)) : ((string)null, SpotWords.FoldedAlike),
            };
            var gone = new List<string>();
            if (all) gone.Add(SpotWords.FoldedEveryMark);
            else gone.AddRange(hidden);
            if (crossedHidden) gone.Add(SpotWords.FoldedCrossed);
            if (toldHidden) gone.Add(SpotWords.FoldedTold);
            if (gone.Count > 0) lines.Add((SpotWords.FoldedHidden, string.Join(", ", gone)));
            return lines;
        }

        /// <summary>How many rows flowed pieces take; none for none.</summary>
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

        /// <summary>Whether the list stands on the map: turned on, and two kinds or more, or one while anything is hidden, so what is hidden can always be shown again.</summary>
        public static bool Stands(bool listOn, int chips, bool anyHidden) => listOn && (chips >= 2 || chips >= 1 && anyHidden);

        /// <summary>Whether a kind hidden is left off the map: only while the list stands turned on, where it is shown again.</summary>
        public static bool Hides(bool listOn, bool hidden) => listOn && hidden;

        /// <summary>How much the list's title says is hidden: each chip whose kinds are all hidden, every chip while all are, and each of the crossed spots and the told places hidden.</summary>
        public static int HiddenCount(IReadOnlyList<string[]> chips, Func<string, bool> hidden, bool all, bool crossedHidden, bool toldHidden)
        {
            var count = all ? chips.Count : chips.Count(kinds => kinds.Length > 0 && kinds.All(hidden));
            return count + (crossedHidden ? 1 : 0) + (toldHidden ? 1 : 0);
        }

        /// <summary>How many rows a list with its top at a height may stand: as it was sized, or so many never sized; at least one, and no more than fit on the screen below it.</summary>
        public static int RowsAllowed(int? kept, float top, float screenHeight, float scale)
        {
            var fit = (int)Math.Floor((screenHeight - top - (TitleHeight + Pad * 2f - Gap) * scale) / ((LineHeight + Gap) * scale));
            return Math.Max(1, Math.Min(kept ?? StartRows, fit));
        }

        /// <summary>The rows a list with its top at a height stands with its foot at another, the nearest; at least one.</summary>
        public static int RowsAt(float foot, float top, float scale) =>
            Math.Max(1, (int)Math.Round((foot - top - (TitleHeight + Pad * 2f - Gap) * scale) / ((LineHeight + Gap) * scale)));
    }
}
