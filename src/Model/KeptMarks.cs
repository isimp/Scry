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
    /// at a glance: a number, its colour one of eight in the shape of its category
    /// (<see cref="KeptCategories"/>), eight told apart in a category before the colours begin again.
    /// The player may pick a kept kind's mark instead: any colour in any shape, then past those 32
    /// one of the game's map pictures in each colour
    /// (<see cref="Pictured"/>), then the kind's own picture on each colour (<see cref="Own"/>).
    /// Each number's colour is its remainder by the colours, so an older Scry reading a picked
    /// mark still stands it in its colour. Every kind of dungeon keeps its amber discs
    /// (<see cref="Dungeons"/>).
    /// </summary>
    internal static class KeptMarks
    {
        /// <summary>How many colours the marks are told apart by, how many shapes, and how many marks so.</summary>
        public const int Colours = 8, Shapes = 4, Most = Colours * Shapes;

        /// <summary>How many of the game's map pictures a mark may stand in.</summary>
        public const int Pictures = 10;

        /// <summary>How many marks there are to pick from: the colours in each shape, in each picture, and on the kind's own picture.</summary>
        public const int Looks = Most + Pictures * Colours + Colours;

        /// <summary>The mark of every kind of dungeon, kept as one: the accent's amber, as their discs stand.</summary>
        public const int Dungeons = -1;

        /// <summary>A colour in a shape.</summary>
        public static int Plain(int colour, MarkShape shape) => (int)shape * Colours + colour;

        /// <summary>One of the game's map pictures in a colour.</summary>
        public static int Pictured(int colour, int picture) => Most + picture * Colours + colour;

        /// <summary>The kind's own picture on a colour (<see cref="OwnPictureOf"/>).</summary>
        public static int Own(int colour) => Most + Pictures * Colours + colour;

        /// <summary>Whether a mark is one this Scry offers, the dungeons' aside.</summary>
        public static bool Offered(int mark) => mark >= 0 && mark < Looks;

        /// <summary>A mark's colour, one of <see cref="Colours"/>; none for the dungeons', which stand in the accent.</summary>
        public static int ColourOf(int mark) => mark < 0 ? -1 : mark % Colours;

        /// <summary>A mark's shape: past every colour, the next; a picture stands in the round.</summary>
        public static MarkShape ShapeOf(int mark) => mark < 0 || mark >= Most ? MarkShape.Circle : (MarkShape)(mark / Colours);

        /// <summary>Which of the game's map pictures a mark stands in; none for a shape or its own picture.</summary>
        public static int? PictureOf(int mark) => mark >= Most && mark < Own(0) ? (mark - Most) / Colours : (int?)null;

        /// <summary>Whether a mark stands in the kind's own picture.</summary>
        public static bool IsOwn(int mark) => mark >= Own(0) && mark < Looks;

        /// <summary>
        /// The mark dealt in a shape: the lowest colour none of those taken has in it; past all of
        /// them, the one fewest stand in, the lowest of those. A picture is only ever picked, never dealt.
        /// </summary>
        public static int Dealt(MarkShape shape, IEnumerable<int> taken)
        {
            var held = new int[Colours];
            foreach (var mark in taken) if (mark >= 0 && mark < Most && ShapeOf(mark) == shape) held[ColourOf(mark)]++;
            var lowest = 0;
            for (var colour = 1; colour < Colours; colour++) if (held[colour] < held[lowest]) lowest = colour;
            return Plain(lowest, shape);
        }

        /// <summary>
        /// The entry whose picture a kind's own mark shows: its own icon where it has one (an item,
        /// a piece); for a creature its trophy, never another drop; for a deposit, bush or tree the
        /// first thing it drops that has an icon; none for anything else, a place among them.
        /// </summary>
        public static string OwnPictureOf(string name, Kind kind, bool hasIcon, IEnumerable<(string Name, bool HasIcon)> drops)
        {
            if (hasIcon) return name;
            foreach (var (drop, icon) in drops)
            {
                if (!icon) continue;
                if (kind == Kind.Creature && drop.StartsWith("Trophy", System.StringComparison.Ordinal)) return drop;
                if (kind == Kind.Resource) return drop;
            }
            return null;
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
