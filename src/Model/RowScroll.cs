using System;

namespace Scry
{
    /// <summary>
    /// A list showing some of its rows, scrolled a row at a time: which row stands first as the
    /// wheel turns or its thumb is dragged, and where the thumb stands on its track.
    /// </summary>
    internal static class RowScroll
    {
        /// <summary>How many rows a turn of the wheel moves the list.</summary>
        public const int WheelRows = 2;

        /// <summary>The first row kept inside the list, so it never stands scrolled past its end.</summary>
        public static int Clamp(int first, int rows, int shown) => Math.Max(0, Math.Min(first, rows - shown));

        /// <summary>The first row after a turn of the wheel, down where it is more than none.</summary>
        public static int Wheeled(int first, float wheel, int rows, int shown) =>
            Clamp(wheel > 0f ? first + WheelRows : wheel < 0f ? first - WheelRows : first, rows, shown);

        /// <summary>Where the thumb stands on a track: its share of it as the rows shown are of all, never shorter than <paramref name="least"/>, from one end of the track to the other.</summary>
        public static (float Y, float Height) Thumb(float top, float track, int first, int rows, int shown, float least)
        {
            var height = ThumbHeight(track, rows, shown, least);
            var most = rows - shown;
            return (top + (most > 0 ? (track - height) * first / most : 0f), height);
        }

        /// <summary>The first row where the thumb's top is dragged to a height, staying inside the list.</summary>
        public static int FirstAt(float thumbTop, float top, float track, int rows, int shown, float least)
        {
            var travel = track - ThumbHeight(track, rows, shown, least);
            if (travel <= 0f) return 0;
            return Clamp((int)Math.Round((thumbTop - top) / travel * (rows - shown)), rows, shown);
        }

        private static float ThumbHeight(float track, int rows, int shown, float least) =>
            rows <= shown ? track : Math.Min(track, Math.Max(least, track * shown / rows));
    }
}
