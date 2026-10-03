using System;

namespace Scry
{
    /// <summary>
    /// The header's two small sliders, each opened from its icon, as a share of their track from
    /// 0 to 1: the selection's volume, from silent to twice the game's own
    /// (<see cref="Modifiers.MaxVolume"/>) in steps of 5%, and its size on a curve from a tenth to
    /// ten times, so both ends are as easy to reach, caught at its own size near the middle.
    /// </summary>
    internal static class HeaderSliders
    {
        /// <summary>How near the middle of its track the size is caught at its own, as a share of the track.</summary>
        public const float Catch = 0.01f;

        public static float VolumeAt(float share)
        {
            var volume = Clamp01(share) * Modifiers.MaxVolume;
            return (float)Math.Round(volume * 20f, MidpointRounding.AwayFromZero) / 20f;
        }

        public static float VolumeShare(float volume) => Clamp01(volume / Modifiers.MaxVolume);

        public static string VolumeLabel(float volume) =>
            Numbers.Percent(volume);

        public static float SizeAt(float share)
        {
            var at = Clamp01(share);
            if (Math.Abs(at - 0.5f) <= Catch) return 1f;
            return (float)Math.Pow(10.0, at * 2.0 - 1.0);
        }

        public static float SizeShare(float scale) => scale <= 0f ? 0f : Clamp01(((float)Math.Log10(scale) + 1f) / 2f);

        public static string SizeLabel(float scale) => "\u00d7" + Numbers.Fixed(scale, 2);

        private static float Clamp01(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
    }
}
