using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// For the self-test: how much darker a dungeon example's rooms look on the stage dimmed than
    /// whole, from three pictures of the same view, each pixel's brightness: the rooms put away,
    /// whole, and dimmed. Only the rooms' pixels count, those differing from the picture with the
    /// rooms put away by more than <see cref="Backdrop"/>, and of those only the ones brighter
    /// than <see cref="Darkest"/> when whole. The reading is the middle of each pixel's share of
    /// its own brightness left, so a few lights left on do not pull it up.
    /// </summary>
    internal static class DimLook
    {
        /// <summary>How much a pixel must differ from the picture without the rooms to be of the rooms.</summary>
        public const float Backdrop = 0.02f;

        /// <summary>How much brighter a room's pixel must be whole than on its floor (or than black, with none) to tell how much darker dimming makes it.</summary>
        public const float Darkest = 0.02f;

        /// <summary>The share of their brightness dimmed rooms keep at the most, to look dimmed.</summary>
        public const float Wanted = 0.6f;

        /// <summary>A pixel's brightness from its colour, 0 to 1, as the eye weighs red, green and blue.</summary>
        public static float Brightness(byte r, byte g, byte b) => (0.2126f * r + 0.7152f * g + 0.0722f * b) / 255f;

        /// <summary>
        /// How many of the rooms' pixels were read, and the middle share of their brightness the
        /// dimmed picture keeps; none where no pixel counts or the pictures differ in size. With a
        /// <paramref name="floor"/>, the rooms filmed with every colour black, only the brightness
        /// above it counts, what their colours give them: fog and highlights stay however dark the
        /// colours, so dark stone is read by what dimming can take away. A pixel no brighter whole
        /// than on the floor has nothing to take and is left out; one dimmed below it reads as none kept.
        /// </summary>
        public static (int Pixels, float? Share) Read(float[] empty, float[] whole, float[] dimmed, float[] floor = null)
        {
            if (empty == null || whole == null || dimmed == null || empty.Length != whole.Length || whole.Length != dimmed.Length) return (0, null);
            if (floor != null && floor.Length != whole.Length) return (0, null);
            var shares = new List<float>();
            for (var i = 0; i < whole.Length; i++)
            {
                var under = floor?[i] ?? 0f;
                if (System.Math.Abs(whole[i] - empty[i]) <= Backdrop || whole[i] - under <= Darkest) continue;
                shares.Add(System.Math.Max(0f, (dimmed[i] - under) / (whole[i] - under)));
            }
            if (shares.Count == 0) return (0, null);
            shares.Sort();
            var middle = shares.Count / 2;
            var share = shares.Count % 2 == 1 ? shares[middle] : (shares[middle - 1] + shares[middle]) / 2f;
            return (shares.Count, share);
        }

        /// <summary>Whether a reading says the rooms look dimmed: they keep at most <see cref="Wanted"/> of their brightness.</summary>
        public static bool DarkEnough(float? share) => share is float kept && kept <= Wanted;
    }
}
