namespace Scry
{
    /// <summary>What a sound's page says: its clips, its play button, each variant and the playing time.</summary>
    internal static class SoundWords
    {
        /// <summary>The play button, which plays one at random of several.</summary>
        public static string Play(int variants) => variants > 1 ? "Play a random one" : "Play";

        /// <summary>A sound's clips: how many, the longest's length, and whether it loops.</summary>
        public static string Clips(int count, float longest, bool loops)
        {
            if (count == 0) return loops ? "Loops" : "No clips found";
            var what = count == 1 ? "1 clip" : $"{Numbers.Count(count)} clips, one picked at random";
            return $"{what}, {Numbers.Fixed(longest, 1)} s{(loops ? ", loops" : "")}";
        }

        /// <summary>A variant's chip: its number and name.</summary>
        public static string Variant(int index, string name) => $"{Numbers.Count(index + 1)}   {name}";

        /// <summary>A variant's tip: its name and length.</summary>
        public static string VariantTip(string name, float length) => $"{name}\n{Numbers.Fixed(length, 2)} s";

        /// <summary>A playing time: seconds to a tenth under ten, minutes and seconds above.</summary>
        public static string Clock(float seconds) => seconds < 10f ? Numbers.Fixed(seconds, 1) + " s" : Numbers.Clock(seconds);
    }
}
