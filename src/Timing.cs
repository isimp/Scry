using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// How long Scry's own work takes each frame, by part. With <see cref="Plugin.LogPreviews"/>
    /// on, a frame where it took long is said in the log with its parts, at most twice a second.
    /// </summary>
    internal static class Timing
    {
        /// <summary>Scry's share of a frame, in milliseconds, above which the frame is told.</summary>
        private const double SlowMs = 20;

        private static readonly Dictionary<string, double> Parts = new Dictionary<string, double>();
        private static int _frame = -1;
        private static float _toldAt = -10f;

        public static long Start() => Stopwatch.GetTimestamp();

        /// <summary>Adds the time since <paramref name="started"/> to a part of this frame.</summary>
        public static void Add(string part, long started)
        {
            if (!Plugin.LogPreviews) return;
            var ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            Roll();
            Parts.TryGetValue(part, out var sum);
            Parts[part] = sum + ms;
        }

        /// <summary>A new frame: the last one is told if Scry took long in it.</summary>
        private static void Roll()
        {
            if (Time.frameCount == _frame) return;
            var total = Parts.Where(p => !p.Key.Contains(" ")).Sum(p => p.Value);
            if (total >= SlowMs && Time.unscaledTime - _toldAt >= 0.5f)
            {
                _toldAt = Time.unscaledTime;
                var parts = string.Join(", ", Parts.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value:0}"));
                Plugin.Log.LogInfo($"Scry took {total:0} ms of a {Time.unscaledDeltaTime * 1000f:0} ms frame: {parts}.");
            }
            Parts.Clear();
            _frame = Time.frameCount;
        }
    }
}
