using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>For the self-test: how many pixels of a film are of the accent colour, each channel within a tolerance, as the room gone into's outline is drawn.</summary>
    internal static class AccentPixels
    {
        /// <summary>The pixels (each 0xRRGGBB) within <paramref name="tolerance"/> of <paramref name="accent"/> on every channel.</summary>
        public static int Count(IReadOnlyList<int> film, int accent, int tolerance)
        {
            var count = 0;
            foreach (var pixel in film)
            {
                if (Math.Abs(((pixel >> 16) & 0xFF) - ((accent >> 16) & 0xFF)) <= tolerance
                    && Math.Abs(((pixel >> 8) & 0xFF) - ((accent >> 8) & 0xFF)) <= tolerance
                    && Math.Abs((pixel & 0xFF) - (accent & 0xFF)) <= tolerance) count++;
            }
            return count;
        }

        /// <summary>
        /// How many pixels a film has changed from the same film without one thing in it, by more
        /// than <paramref name="threshold"/> on any channel, and their average colour (0xRRGGBB);
        /// none and -1 where none changed or the films differ in size.
        /// </summary>
        public static (int Changed, int Average) Changed(IReadOnlyList<int> with, IReadOnlyList<int> without, int threshold)
        {
            if (with.Count != without.Count) return (0, -1);
            long r = 0, g = 0, b = 0;
            var changed = 0;
            for (var i = 0; i < with.Count; i++)
            {
                int now = with[i], was = without[i];
                if (Math.Abs(((now >> 16) & 0xFF) - ((was >> 16) & 0xFF)) <= threshold
                    && Math.Abs(((now >> 8) & 0xFF) - ((was >> 8) & 0xFF)) <= threshold
                    && Math.Abs((now & 0xFF) - (was & 0xFF)) <= threshold) continue;
                changed++;
                r += (now >> 16) & 0xFF;
                g += (now >> 8) & 0xFF;
                b += now & 0xFF;
            }
            if (changed == 0) return (0, -1);
            return (changed, (int)(r / changed) << 16 | (int)(g / changed) << 8 | (int)(b / changed));
        }
    }
}
