using System;
using System.Diagnostics;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// How long Scry's own work takes each frame, by part, with about how much memory each part
    /// allocated and whether the runtime's memory cleanup ran inside it (<see cref="FrameTimes"/>).
    /// With <see cref="Settings.LogPreviews"/> on, a frame where it took long is said in the log with
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

        /// <summary>How often the counts of memory cleanups and allocation are told, in seconds.</summary>
        private const float WindowSeconds = 30f;

        private static readonly FrameTimes Frame = new FrameTimes();
        private static readonly AllocationWindow Window = new AllocationWindow();
        private static int _frame = -1;
        private static float _toldAt = -10f;
        private static float _windowFrom = -1f;

        /// <summary>
        /// Where each frame's total goes while the self-test measures (<see cref="SelfTestHost"/>),
        /// with its slowest part; null otherwise. Slow frames are logged only with
        /// <see cref="Settings.LogPreviews"/> on.
        /// </summary>
        public static FrameStats Measuring;

        /// <summary>About how much Scry's own work has allocated in all, as measured, for the self-test to tell what it allocates idling.</summary>
        public static long ScryBytes { get; private set; }

        /// <summary>The self-test's own checks, taken out of the frame it measures (<see cref="Own"/>).</summary>
        public const string SelfTestPart = "update self-test";

        /// <summary>The time of the inner parts so far this frame, for <see cref="Own"/>.</summary>
        public static double InnerMs()
        {
            if (!On) return 0;
            Roll();
            return Frame.InnerMs;
        }

        /// <summary>
        /// Adds a part's own time: the time since it started less the parts timed inside it, so
        /// Scry's work that it called (a copy made, details told) stays where it is counted.
        /// </summary>
        public static void Own(string part, Mark started, double innerBefore)
        {
            if (!On || started.Ticks == 0) return;
            var ms = (Stopwatch.GetTimestamp() - started.Ticks) * 1000.0 / Stopwatch.Frequency;
            // All it allocated, inner parts and all, so what it asked of Scry is left out with it.
            var bytes = GC.CollectionCount(0) > started.Cleanups ? 0 : GC.GetTotalMemory(false) - started.Bytes;
            Roll();
            Frame.Add(part, Math.Max(0, ms - (Frame.InnerMs - innerBefore)), Math.Max(0, bytes), 0);
        }

        private static bool On => Settings.LogPreviews || Measuring != null || Settings.ShowMonitor;

        /// <summary>Told each frame of Scry's work as the next begins, with the frame's whole time, while the resource monitor shows.</summary>
        public static event Action<FrameTimes, double> FrameDone;

        public static Mark Start()
        {
            if (!On) return default;
            return new Mark { Ticks = Stopwatch.GetTimestamp(), Bytes = GC.GetTotalMemory(false), Cleanups = GC.CollectionCount(0) };
        }

        /// <summary>
        /// Adds the time since <paramref name="started"/> to a part of this frame, and what it
        /// allocated, which the memory in use tells only roughly and not at all across a cleanup.
        /// </summary>
        public static void Add(string part, Mark started)
        {
            if (!On || started.Ticks == 0) return;
            var ms = (Stopwatch.GetTimestamp() - started.Ticks) * 1000.0 / Stopwatch.Frequency;
            var cleanups = GC.CollectionCount(0) - started.Cleanups;
            var bytes = cleanups > 0 ? 0 : GC.GetTotalMemory(false) - started.Bytes;
            Roll();
            Frame.Add(part, ms, bytes, cleanups);
        }

        /// <summary>
        /// A new frame: the last one is told if Scry took long in it, and every half minute how
        /// many memory cleanups ran and how much of what the game allocated was Scry's.
        /// </summary>
        private static void Roll()
        {
            if (Time.frameCount == _frame) return;
            // What Scry allocated, the self-test's own checks left out as their time is.
            ScryBytes += Frame.BytesWithout(SelfTestPart);
            if (Settings.ShowMonitor && _frame >= 0) FrameDone?.Invoke(Frame, Time.unscaledDeltaTime * 1000.0);
            if (Measuring != null && _frame >= 0)
            {
                // Scry's own work as a player's frame has it, the self-test's own checks left out.
                var slowest = Frame.SlowestWithout(SelfTestPart);
                var outer = Frame.SlowestOuter;
                Measuring.Add(Frame.TotalWithout(SelfTestPart), slowest.Name, slowest.Ms, outer.Name, outer.Ms, Frame.Cleanups);
                Measuring.AddTest(Frame.MsOf(SelfTestPart));
            }
            if (!Settings.LogPreviews)
            {
                Frame.Clear();
                _frame = Time.frameCount;
                return;
            }
            if (Frame.Total >= SlowMs && Time.unscaledTime - _toldAt >= 0.5f)
            {
                _toldAt = Time.unscaledTime;
                Log.Note(Frame.Line(Time.unscaledDeltaTime * 1000f));
            }

            Window.AddScry(Frame.Bytes, Frame.Cleanups);
            Window.Sample(GC.GetTotalMemory(false), GC.CollectionCount(0));
            var now = Time.unscaledTime;
            if (_windowFrom < 0f || now < _windowFrom)
            {
                Window.Reset();
                _windowFrom = now;
            }
            else if (now - _windowFrom >= WindowSeconds)
            {
                Log.Note(Window.Line(now - _windowFrom));
                Window.Reset();
                _windowFrom = now;
            }

            Frame.Clear();
            _frame = Time.frameCount;
        }
    }
}
