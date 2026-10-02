using System;
using System.Globalization;

namespace Scry
{
    /// <summary>The resource monitor's figures in words: milliseconds and memory.</summary>
    public static class MonitorWords
    {
        /// <summary>Milliseconds: to two places under ten, to one above.</summary>
        public static string Ms(double ms) => ms.ToString(ms < 10 ? "0.00" : "0.0", CultureInfo.InvariantCulture);

        /// <summary>Memory in bytes, kilobytes, megabytes or gigabytes, whichever reads best.</summary>
        public static string Bytes(long bytes)
        {
            bytes = Math.Max(0, bytes);
            if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024).ToString(CultureInfo.InvariantCulture) + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            return (bytes / (1024.0 * 1024 * 1024)).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
        }
    }
}
