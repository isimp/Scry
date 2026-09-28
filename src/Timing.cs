using System;
using System.Diagnostics;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// How long Scry's own work takes each frame, by part, with about how much memory each part
    /// allocated and whether the runtime's memory cleanup ran inside it (<see cref="FrameTimes"/>).
    /// With <see cref="Plugin.LogPreviews"/> on, a frame where it took long is said in the log with
    /// its parts, at most twice a second; with it off, nothing is measured.
    /// </summary>
    internal static class Timing
    {
        /// <summary>Scry's share of a frame, in milliseconds, above which the frame is told.</summary>
        private const double SlowMs = 20;

        /// <summary>Where a part started: the clock, the memory in use and how many cleanups had run.</summary>
        internal struct Mark
        {
            public long Ticks;
            public long Bytes;
            public int Cleanups;
        }

        private static readonly FrameTimes Frame = new FrameTimes();
        private static int _frame = -1;
        private static float _toldAt = -10f;

        public static Mark Start()
        {
            if (!Plugin.LogPreviews) return default;
            return new Mark { Ticks = Stopwatch.GetTimestamp(), Bytes = GC.GetTotalMemory(false), Cleanups = GC.CollectionCount(0) };
        }

        /// <summary>
        /// Adds the time since <paramref name="started"/> to a part of this frame, and what it
        /// allocated, which the memory in use tells only roughly and not at all across a cleanup.
        /// </summary>
        public static void Add(string part, Mark started)
        {
            if (!Plugin.LogPreviews || started.Ticks == 0) return;
            var ms = (Stopwatch.GetTimestamp() - started.Ticks) * 1000.0 / Stopwatch.Frequency;
            var cleanups = GC.CollectionCount(0) - started.Cleanups;
            var bytes = cleanups > 0 ? 0 : GC.GetTotalMemory(false) - started.Bytes;
            Roll();
            Frame.Add(part, ms, bytes, cleanups);
        }

        /// <summary>A new frame: the last one is told if Scry took long in it.</summary>
        private static void Roll()
        {
            if (Time.frameCount == _frame) return;
            if (Frame.Total >= SlowMs && Time.unscaledTime - _toldAt >= 0.5f)
            {
                _toldAt = Time.unscaledTime;
                Plugin.Log.LogInfo(Frame.Line(Time.unscaledDeltaTime * 1000f));
            }
            Frame.Clear();
            _frame = Time.frameCount;
        }
    }
}
