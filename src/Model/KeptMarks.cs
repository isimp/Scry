using System.Collections.Generic;

namespace Scry
{
    /// <summary>The shapes a kind's mark stands in on Valheim's map, its colour told apart from the others' first (<see cref="KeptMarks"/>).</summary>
    internal enum MarkShape
    {
        Circle,
        Diamond,
        Square,
        Triangle,
    }

    /// <summary>
    /// The mark each kind stands in on Valheim's map, so a dot or a square tells which kind it is
    /// at a glance: a number, its colour one of eight, then past eight kinds the same colours in
    /// the next shape, 32 told apart before they begin again. Every kind of dungeon keeps its
    /// amber discs (<see cref="Dungeons"/>).
    /// </summary>
    internal static class KeptMarks
    {
        /// <summary>How many colours the marks are told apart by, how many shapes, and how many marks so.</summary>
        public const int Colours = 8, Shapes = 4, Most = Colours * Shapes;

        /// <summary>The mark of every kind of dungeon, kept as one: the accent's amber, as their discs stand.</summary>
        public const int Dungeons = -1;

        /// <summary>A mark's colour, one of <see cref="Colours"/>; none for the dungeons', which stand in the accent.</summary>
        public static int ColourOf(int mark) => mark < 0 ? -1 : mark % Colours;

        /// <summary>A mark's shape: past every colour, the next.</summary>
        public static MarkShape ShapeOf(int mark) => mark < 0 ? MarkShape.Circle : (MarkShape)(mark / Colours % Shapes);

        /// <summary>The lowest mark none of those taken has.</summary>
        public static int Lowest(IEnumerable<int> taken)
        {
            var held = new HashSet<int>(taken);
            var mark = 0;
            while (held.Contains(mark)) mark++;
            return mark;
        }

        /// <summary>The mark a square stands in: the one kind's it holds, none where it holds several.</summary>
        public static int? SquareMark(IReadOnlyList<(int Mark, int Left)> kinds)
        {
            if (kinds.Count == 0) return null;
            var mark = kinds[0].Mark;
            for (var i = 1; i < kinds.Count; i++) if (kinds[i].Mark != mark) return null;
            return mark;
        }

        /// <summary>How many places a square holds, of every kind in it.</summary>
        public static int SquareCount(IReadOnlyList<(int Mark, int Left)> kinds)
        {
            var count = 0;
            foreach (var (_, left) in kinds) count += left;
            return count;
        }
    }
}
