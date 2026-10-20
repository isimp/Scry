using System.Globalization;
using System.Text;

namespace Scry
{
    /// <summary>
    /// How Scry writes numbers where it reads them back, in its own files and its own keys, and
    /// how it reads them: plain digits, no thousands, a point for a fraction, whatever language
    /// the PC is set to, so a file written on one PC reads on any. Numbers shown to the player or
    /// in the log go through <see cref="Numbers"/>.
    /// </summary>
    internal static class Stored
    {
        private static readonly CultureInfo Plain = CultureInfo.InvariantCulture;

        /// <summary>A whole number: "12345".</summary>
        public static string Count(long value) => value.ToString(Plain);

        /// <summary>A number with up to so many decimals: "0.85".</summary>
        public static string Number(double value, int decimals = 2) =>
            value.ToString(decimals <= 0 ? "0" : "0." + new string('#', decimals), Plain);

        /// <summary>Reads a whole number written by <see cref="Count"/>; false for anything else.</summary>
        public static bool TryCount(string text, out int value) => int.TryParse(text, NumberStyles.AllowLeadingSign, Plain, out value);

        /// <summary>Reads a long whole number written by <see cref="Count"/>, as a network id's user; false for anything else.</summary>
        public static bool TryLong(string text, out long value) => long.TryParse(text, NumberStyles.AllowLeadingSign, Plain, out value);

        /// <summary>A yes or no: "1" or "0".</summary>
        public static string Flag(bool value) => value ? "1" : "0";

        /// <summary>Reads a yes or no written by <see cref="Flag"/>; false for anything else.</summary>
        public static bool TryFlag(string text, out bool value)
        {
            value = text == "1";
            return text == "1" || text == "0";
        }

        /// <summary>A 64-bit value as sixteen hexadecimal digits: "00000000000000e9".</summary>
        public static string Hex(ulong value) => value.ToString("x16", Plain);

        /// <summary>A hash of a text that reads the same on every machine and run (64-bit FNV-1a over its characters), as <see cref="Hex"/> writes it.</summary>
        public static string Hash(string text)
        {
            var hash = 14695981039346656037UL;
            foreach (var c in text)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            return Hex(hash);
        }

        /// <summary>Reads hexadecimal digits, as a file's escaped character gives its code ("00e9"); false for anything else.</summary>
        public static bool TryHex(string text, out int value) => int.TryParse(text, NumberStyles.AllowHexSpecifier, Plain, out value);

        /// <summary>Reads a number written by <see cref="Number"/>; false for anything else, a value that is no number among it.</summary>
        public static bool TryNumber(string text, out float value) =>
            float.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Plain, out value) && !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>A number with every digit it needs to read back as the very same number: "0.333333343".</summary>
        public static string Exact(float value) => value.ToString("G9", Plain);

        /// <summary>Reads a number written by <see cref="Exact"/>; false for anything else, a value that is no number among it.</summary>
        public static bool TryExact(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, Plain, out value) && !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>A text as one field of a line split at tabs: its backslashes, tabs and line breaks written as \\, \t, \n and \r.</summary>
        public static string Escaped(string text)
        {
            var written = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                switch (c)
                {
                    case '\\': written.Append("\\\\"); break;
                    case '\t': written.Append("\\t"); break;
                    case '\n': written.Append("\\n"); break;
                    case '\r': written.Append("\\r"); break;
                    default: written.Append(c); break;
                }
            }
            return written.ToString();
        }

        /// <summary>Reads a field written by <see cref="Escaped"/>; false for a backslash that starts nothing it writes.</summary>
        public static bool TryUnescaped(string field, out string text)
        {
            var read = new StringBuilder(field.Length);
            for (var i = 0; i < field.Length; i++)
            {
                var c = field[i];
                if (c != '\\')
                {
                    read.Append(c);
                    continue;
                }
                if (++i >= field.Length)
                {
                    text = "";
                    return false;
                }
                switch (field[i])
                {
                    case '\\': read.Append('\\'); break;
                    case 't': read.Append('\t'); break;
                    case 'n': read.Append('\n'); break;
                    case 'r': read.Append('\r'); break;
                    default:
                        text = "";
                        return false;
                }
            }
            text = read.ToString();
            return true;
        }
    }
}
