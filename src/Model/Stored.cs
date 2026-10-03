using System.Globalization;

namespace Scry
{
    /// <summary>
    /// How Scry writes numbers where it reads them back, in its own files and its own keys, and
    /// how it reads them: plain digits, no thousands, a point for a fraction, whatever language
    /// the PC is set to, so a file written on one PC reads on any. Numbers shown to the player or
    /// in the log go through <see cref="Numbers"/>.
    /// </summary>
    public static class Stored
    {
        private static readonly CultureInfo Plain = CultureInfo.InvariantCulture;

        /// <summary>A whole number: "12345".</summary>
        public static string Count(long value) => value.ToString(Plain);

        /// <summary>A number with up to so many decimals: "0.85".</summary>
        public static string Number(double value, int decimals = 2) =>
            value.ToString(decimals <= 0 ? "0" : "0." + new string('#', decimals), Plain);

        /// <summary>Reads a whole number written by <see cref="Count"/>; false for anything else.</summary>
        public static bool TryCount(string text, out int value) => int.TryParse(text, NumberStyles.AllowLeadingSign, Plain, out value);

        /// <summary>Reads hexadecimal digits, as a file's escaped character gives its code ("00e9"); false for anything else.</summary>
        public static bool TryHex(string text, out int value) => int.TryParse(text, NumberStyles.AllowHexSpecifier, Plain, out value);

        /// <summary>Reads a number written by <see cref="Number"/>; false for anything else, a value that is no number among it.</summary>
        public static bool TryNumber(string text, out float value) =>
            float.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Plain, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
