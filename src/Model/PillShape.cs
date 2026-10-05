using System;

namespace Scry
{
    /// <summary>
    /// How a pill (a box with fully round ends: a chip, a tab, a loot mark) is drawn at a
    /// height: from a picture of a pill that high, its round ends kept as they are and the
    /// straight between them stretched to the length drawn. The IMGUI keeps a sliced picture's
    /// ends at their own size, so one picture for all heights would draw its ends overlapping,
    /// with stray lines beside them, on a pill lower than they are tall; each height drawn has
    /// its own picture.
    /// </summary>
    internal static class PillShape
    {
        /// <summary>The height a pill is drawn at, in whole pixels, two at the least.</summary>
        public static int Height(float drawn) => Math.Max(2, (int)Math.Round(drawn));

        /// <summary>The width of a pill's picture: its two round ends, with a little straight between them to stretch.</summary>
        public static int Width(int height) => height + 2;

        /// <summary>How far in from each side a pill's round end reaches, kept as it is while the straight between stretches.</summary>
        public static int End(int height) => (height + 1) / 2;

        /// <summary>Whether a pill drawn this long has room for both its ends; a shorter one is drawn as its whole picture squeezed.</summary>
        public static bool Stretches(float length, int height) => length >= Width(height);
    }
}
