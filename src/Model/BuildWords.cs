using System.Collections.Generic;
using System.Globalization;

namespace Scry
{
    /// <summary>
    /// What holds a piece up and what wears it down, in words. Support follows
    /// <c>WearNTear.UpdateSupport</c>: a piece on the ground has its material's full support, and
    /// one resting on another has that one's support less a share for each metre between their
    /// centres, a bigger share sideways than up, and falls below its material's least. Weather
    /// follows <c>WearNTear.UpdateWear</c>.
    /// </summary>
    public static class BuildWords
    {
        private static string Whole(float value) => value.ToString("#,0.##", CultureInfo.InvariantCulture);

        private static string Share(float value) => (value * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";

        /// <summary>The support it has on the ground and the least it stands with; null for a material without figures.</summary>
        public static string Support(float max, float min, bool needsSupport)
        {
            if (max <= 0f) return null;
            return $"{Whole(max)} on the ground, " + (needsSupport ? $"falls below {Whole(min)}" : "stands without it");
        }

        /// <summary>The share of support lost for each metre sideways and up; null when none is lost.</summary>
        public static string SupportLoss(float sideways, float up)
        {
            if (sideways <= 0f && up <= 0f) return null;
            if (Share(sideways) == Share(up)) return $"{Share(sideways)} a metre either way";
            var parts = new List<string>();
            if (sideways > 0f) parts.Add($"{Share(sideways)} a metre sideways");
            if (up > 0f) parts.Add($"{Share(up)} a metre up");
            return string.Join(", ", parts);
        }

        /// <summary>Whether rain wears it: every minute it is wet without a roof, 5% of its health, while more than half is left.</summary>
        public static string Rain(bool wears)
        {
            return wears ? "5% of its health a minute while wet and unroofed, until half is left" : "does not wear it";
        }

        /// <summary>
        /// How it stands up to the Ashlands, told only when it does better than most pieces: one
        /// immune takes no harm from ash or lava; one that resists stops wearing from ash at a
        /// tenth of its health and takes a third of the lava's harm.
        /// </summary>
        public static string Ash(bool immune, bool resists)
        {
            if (immune) return "unharmed by ash and lava";
            return resists ? "ash wears it only down to a tenth, lava a third as hard" : null;
        }

        /// <summary>Heavy snow in the Deep North, told only for a piece it does not harm.</summary>
        public static string Snow(bool immune) => immune ? "does not harm it" : null;
    }
}
