using System;

namespace Scry
{
    /// <summary>
    /// A text made to fit a width it is drawn in, by a measure of how wide a text is: whole where
    /// it fits, else cut with an ellipsis, at the end of a word where that keeps most of it.
    /// </summary>
    internal static class TextFit
    {
        public const string Ellipsis = "…";

        /// <summary>The words between the parts of a list row's tag ("Burial Chambers dungeon · Crypt2").</summary>
        private const string Apart = " · ";

        /// <summary>
        /// The text as it fits in a width: whole, else as much of it as fits before an ellipsis,
        /// cut after a whole word where that keeps at least half of what fits; nothing where not
        /// even the ellipsis fits.
        /// </summary>
        public static string Cut(string text, float width, Func<string, float> measure)
        {
            if (string.IsNullOrEmpty(text) || measure(text) <= width) return text ?? "";
            if (measure(Ellipsis) > width) return "";
            // The longest start of it that fits with the ellipsis.
            int least = 0, most = text.Length - 1;
            while (least < most)
            {
                var middle = (least + most + 1) / 2;
                if (measure(Start(text, middle) + Ellipsis) <= width) least = middle;
                else most = middle - 1;
            }
            var kept = Start(text, least);
            var space = kept.LastIndexOf(' ');
            if (space > 0 && space >= kept.Length / 2 && least < text.Length && text[least] != ' ') kept = Start(text, space);
            return kept + Ellipsis;
        }

        /// <summary>The least share of a tag's letters its cut keeps to be worth showing: less tells nothing ("Aban…" of a prefab's name).</summary>
        private const double LeastKept = 0.6;

        /// <summary>
        /// A list row's tag as it fits: whole, else its last part, the one telling it from rows
        /// alike ("Crypt2"), else that cut (<see cref="Cut"/>); nothing where the cut would keep
        /// too little of it to tell anything, the whole showing on hover.
        /// </summary>
        public static string Tag(string tag, float width, Func<string, float> measure)
        {
            if (string.IsNullOrEmpty(tag) || measure(tag) <= width) return tag ?? "";
            var at = tag.LastIndexOf(Apart, StringComparison.Ordinal);
            var part = at >= 0 ? tag.Substring(at + Apart.Length) : tag;
            var cut = Cut(part, width, measure);
            return cut == part || cut.Length - Ellipsis.Length >= part.Length * LeastKept ? cut : "";
        }

        /// <summary>The first letters of a text, without the spaces and commas they end on.</summary>
        private static string Start(string text, int length) => text.Substring(0, length).TrimEnd(' ', ',', '·');
    }
}
