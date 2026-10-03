using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>How long each part of reading the catalog took, summed over the frames it was read in.</summary>
    internal static class CatalogTiming
    {
        private static readonly Dictionary<string, double> Parts = new Dictionary<string, double>();

        public static long Start() => System.Diagnostics.Stopwatch.GetTimestamp();

        public static void Add(string part, long started)
        {
            var ms = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Parts.TryGetValue(part, out var sum);
            Parts[part] = sum + ms;
        }

        public static void Reset() => Parts.Clear();

        /// <summary>The parts, longest first.</summary>
        public static string Report() => string.Join(", ", Parts.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {Numbers.Amount(p.Value, 0)}"));
    }
}
