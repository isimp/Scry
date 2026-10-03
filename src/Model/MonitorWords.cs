using System;

namespace Scry
{
    /// <summary>The resource monitor's figures in words: milliseconds and memory.</summary>
    internal static class MonitorWords
    {
        /// <summary>Milliseconds: to two places under ten, to one above.</summary>
        public static string Ms(double ms) => Numbers.Fixed(ms, ms < 10 ? 2 : 1);

        /// <summary>Memory in bytes, kilobytes, megabytes or gigabytes, whichever reads best.</summary>
        public static string Bytes(long bytes)
        {
            bytes = Math.Max(0, bytes);
            if (bytes < 1024) return Numbers.Count(bytes) + " B";
            if (bytes < 1024 * 1024) return Numbers.Count(bytes / 1024) + " KB";
            if (bytes < 1024L * 1024 * 1024) return Numbers.Fixed(bytes / (1024.0 * 1024), 1) + " MB";
            return Numbers.Fixed(bytes / (1024.0 * 1024 * 1024), 1) + " GB";
        }
    }
}
