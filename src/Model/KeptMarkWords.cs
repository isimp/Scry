using System.Collections.Generic;

namespace Scry
{
    /// <summary>The words of the picker a kept kind's mark is chosen in, opened from its line in the On the map list (<see cref="KeptMarks"/>).</summary>
    internal static class KeptMarkWords
    {
        /// <summary>The colours by name, in the marks' order.</summary>
        public static readonly string[] Colours = { "Lime", "Sky", "Rose", "Violet", "Teal", "Rust", "Magenta", "Red" };

        /// <summary>The shapes by name, in the marks' order.</summary>
        public static readonly string[] Shapes = { "Circle", "Diamond", "Square", "Triangle" };

        /// <summary>The game's map pictures by what they show, in the marks' order.</summary>
        public static readonly string[] Pictures = { "Fire", "House", "Hammer", "Dot", "Portal", "Cauldron", "Bag", "Cart", "Stones", "Rune stone" };

        public const string ColourRow = "Colour";
        public const string ShapeRow = "Shape";
        public const string PictureRow = "Picture";
        public const string OwnRow = "Its own picture";

        /// <summary>Where a kind has no picture of its own: a place, or a deposit dropping nothing with an icon.</summary>
        public const string NoOwn = "None: it has no picture of its own";

        /// <summary>The foot of the list while the pointer rests on a mark that can be picked.</summary>
        public const string MarkHint = "A click picks this mark's colour, shape or picture";

        /// <summary>The picker's heading.</summary>
        public static string Heading(string name) => $"Mark of {name}";

        /// <summary>What a choice in the picker is, as the pointer rests on it: its colour and shape, its picture, or the kind's own picture by what it shows.</summary>
        public static string Choice(int mark, string ownName)
        {
            var colour = Colours[KeptMarks.ColourOf(mark)];
            if (KeptMarks.IsOwn(mark)) return ownName != null ? $"{ownName}, on {colour.ToLowerInvariant()}" : colour;
            if (KeptMarks.PictureOf(mark) is int picture) return $"{Pictures[picture]}, in {colour.ToLowerInvariant()}";
            return $"{colour} {Shapes[(int)KeptMarks.ShapeOf(mark)].ToLowerInvariant()}";
        }

        /// <summary>The line under the picker's choices: what the choice pointed at is, and the kinds kept that stand in it already.</summary>
        public static string Said(int mark, string ownName, IReadOnlyList<string> holders) =>
            holders.Count == 0 ? Choice(mark, ownName) : $"{Choice(mark, ownName)}, also the mark of {Naming.Joined(holders)}";
    }
}
