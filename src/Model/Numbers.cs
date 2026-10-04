using System;
using System.Globalization;

namespace Scry
{
    /// <summary>
    /// How every number Scry shows is written, in the panel, its tips and its log alike: English
    /// whatever language the PC is set to, the thousands by commas, a fraction's point a point,
    /// rounded half away from zero to the decimals asked (as .NET and Mono both write a number's
    /// digits, so 2.675 reads 2.68), and never "-0". No
    /// number becomes shown text anywhere else; what Scry writes into its own files to read back
    /// goes through <see cref="Stored"/>.
    /// </summary>
    internal static class Numbers
    {
        private static readonly CultureInfo English = CultureInfo.InvariantCulture;

        /// <summary>A whole count: "1,234", or with its sign, "+10".</summary>
        public static string Count(long value, bool signed = false) => (signed && value > 0 ? "+" : "") + value.ToString("#,0", English);

        /// <summary>An amount with up to so many decimals, none for a whole one: "1,234.5", "12".</summary>
        public static string Amount(double value, int decimals = 2, bool signed = false) =>
            Signed(value, Pattern(decimals, fixedDecimals: false), signed);

        /// <summary>A readout that always shows its decimals, keeping its width as it changes: "1.50".</summary>
        public static string Fixed(double value, int decimals) =>
            Signed(value, Pattern(decimals, fixedDecimals: true), signed: false);

        /// <summary>A share (1 is all) as a percentage: "25%", "12.5%", "+5%".</summary>
        public static string Percent(double share, int decimals = 0, bool signed = false) =>
            Amount(share * 100.0, decimals, signed) + "%";

        /// <summary>A multiplier, with up to so many decimals: "×1.5", "×2".</summary>
        public static string Times(double value, int decimals = 2) => "×" + Amount(value, decimals);

        /// <summary>A multiplier readout that always shows its decimals, keeping its width as it changes: "×1.50".</summary>
        public static string TimesFixed(double value, int decimals) => "×" + Fixed(value, decimals);

        /// <summary>A length in metres: "1,500 m".</summary>
        public static string Metres(double value, int decimals = 2) => Amount(value, decimals) + " m";

        /// <summary>From one count to another, one number when they meet: "3", "1–4".</summary>
        public static string CountRange(long least, long most) => most <= least ? Count(least) : Count(least) + "–" + Count(most);

        /// <summary>A length of time in the largest unit that reads well, to a tenth of it: "40 s", "25 min", "2.5 h".</summary>
        public static string Duration(double seconds)
        {
            if (seconds < 120.0) return Amount(seconds, 1) + " s";
            if (seconds < 7200.0) return Amount(seconds / 60.0, 1) + " min";
            return Amount(seconds / 3600.0, 1) + " h";
        }

        /// <summary>From one time to another, the unit said once where both share it: "50–60 min", "90 s to 3 min".</summary>
        public static string DurationRange(double least, double most)
        {
            var low = Duration(least);
            var high = Duration(most);
            if (low == high) return low;
            var lowCut = low.LastIndexOf(' ');
            var highCut = high.LastIndexOf(' ');
            return string.Equals(low.Substring(lowCut), high.Substring(highCut), StringComparison.Ordinal)
                ? low.Substring(0, lowCut) + "–" + high
                : low + " to " + high;
        }

        /// <summary>A playing time as minutes and seconds, the seconds that have passed: "1:05".</summary>
        public static string Clock(double seconds)
        {
            var whole = (long)Math.Floor(Math.Max(0.0, seconds));
            return Count(whole / 60) + ":" + (whole % 60).ToString("00", English);
        }

        /// <summary>A point on a picture or a plane by its figures, each to so many decimals: "(0.50, 1.25)".</summary>
        public static string Point(double x, double y, int decimals) => "(" + Fixed(x, decimals) + ", " + Fixed(y, decimals) + ")";

        /// <summary>A point in space by its figures, each to so many decimals: "(1.00, 2.50, -3.00)".</summary>
        public static string Point(double x, double y, double z, int decimals) =>
            "(" + Fixed(x, decimals) + ", " + Fixed(y, decimals) + ", " + Fixed(z, decimals) + ")";

        /// <summary>A code in hexadecimal digits, as a colour is written: "0A".</summary>
        public static string Hex(long value, int digits) => value.ToString("X" + Math.Max(1, digits).ToString(English), English);

        private static string Pattern(int decimals, bool fixedDecimals) =>
            decimals <= 0 ? "#,0" : "#,0." + new string(fixedDecimals ? '0' : '#', decimals);

        private static string Signed(double value, string pattern, bool signed)
        {
            var text = value.ToString(pattern, English);
            // A rounded-away fraction below zero reads "-0"; nothing is less than nothing.
            if (text == "-0") text = "0";
            return signed && value > 0 && text != "0" ? "+" + text : text;
        }
    }
}
