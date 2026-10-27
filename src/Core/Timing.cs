using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            public int Rebuilds;

            /// <summary>Its place among the parts open (<see cref="Nesting"/>).</summary>
            public int Id;
        }

        /// <summary>Which parts are open inside which, so a part's own allocation is told apart from its inner parts' (<see cref="PartNesting"/>).</summary>
        private static readonly PartNesting Nesting = new PartNesting();

        /// <summary>
        /// How often a font's texture has been rebuilt this session. A font out of room in its
        /// texture draws all its letters again, and the text drawn after it in the frame pays
        /// too; counted as it happens, a part it happened in is told so (<see cref="FrameTimes"/>).
        /// </summary>
        private static int _fontRebuilds;

        static Timing() => Font.textureRebuilt += _ => _fontRebuilds++;

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

        /// <summary>What each part allocated while the self-test measures, whole and its own, to tell which part allocates.</summary>
        private static readonly Dictionary<string, long> BytesByPart = new Dictionary<string, long>(StringComparer.Ordinal);
        private static readonly Dictionary<string, long> OwnBytesByPart = new Dictionary<string, long>(StringComparer.Ordinal);

        /// <summary>The time each part took of its own while the self-test measures, beside what it allocated, to take off what other threads allocated meanwhile.</summary>
        private static readonly Dictionary<string, double> OwnMsByPart = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>Forgets what each part allocated, as the self-test starts counting afresh.</summary>
        [ForTest]
        public static void ForgetBytesByPart()
        {
            BytesByPart.Clear();
            OwnBytesByPart.Clear();
            OwnMsByPart.Clear();
        }

        /// <summary>The parts that allocated most since the self-test last forgot them, each with its kilobytes: its own, the inner parts' taken off, or the whole; the self-test's own left out.</summary>
        [Diagnostic, ForTest]
        public static string BytesByPartTold(int most, bool own = false)
        {
            var told = (own ? OwnBytesByPart : BytesByPart).Where(p => p.Key != SelfTestPart).OrderByDescending(p => p.Value).Take(most).Select(p => $"{p.Key} {Numbers.Fixed(p.Value / 1024f, 1)} KB").ToList();
            return told.Count > 0 ? string.Join(", ", told) : "none";
        }

        /// <summary>What every part allocated of its own since the self-test last forgot them, the self-test's own left out: Scry's whole allocation, told by part.</summary>
        [Diagnostic, ForTest]
        public static long OwnBytesSum()
        {
            var sum = 0L;
            foreach (var part in OwnBytesByPart) if (part.Key != SelfTestPart) sum += part.Value;
            return sum;
        }

        /// <summary>What every part allocated of its own since the self-test last forgot them, by part, with the time it took of its own, the self-test's own left out.</summary>
        [Diagnostic, ForTest]
        public static List<(string Part, long Bytes, double Ms)> OwnBytesByPartCopy()
        {
            var parts = new List<(string, long, double)>();
            foreach (var part in OwnBytesByPart) if (part.Key != SelfTestPart) parts.Add((part.Key, part.Value, OwnMsByPart.TryGetValue(part.Key, out var ms) ? ms : 0));
            return parts;
        }

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
            var bytes = GC.CollectionCount(0) > started.Cleanups && !CountsPerThread ? 0 : Allocated() - started.Bytes;
            var (own, _) = Nesting.Close(started.Id, Math.Max(0, bytes), ms);
            Roll();
            Frame.Add(part, Math.Max(0, ms - (Frame.InnerMs - innerBefore)), Math.Max(0, bytes), 0, 0, own);
        }

        private static bool On => Settings.LogPreviews || Measuring != null || Settings.ShowMonitor;

        /// <summary>
        /// How much Scry's thread has allocated, read before and after each part: the runtime's
        /// count of the thread where it keeps one, so other mods' threads allocating meanwhile are
        /// not counted as Scry's, else the memory in use (<see cref="AllocationMeter"/>).
        /// </summary>
        private static readonly Func<long> Allocated = AllocationMeter.Pick(PerThread, () => GC.GetTotalMemory(false));

        /// <summary>The runtime's count of what the thread asking allocated, where it has one.</summary>
        private static Func<long> PerThread => _perThread ?? (_perThread = ThreadCount());

        private static Func<long> _perThread;

        /// <summary>Whether what each part allocates is counted for Scry's thread alone; for the monitor and the self-test.</summary>
        public static bool CountsPerThread => _perThread != null && ReferenceEquals(Allocated, _perThread);

        /// <summary>The runtime's count of what the thread asking allocated, found by name as the game's runtime has it and its reference assemblies do not; none without it.</summary>
        private static Func<long> ThreadCount()
        {
            var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, Type.EmptyTypes, null);
            return method != null ? (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method) : null;
        }

        /// <summary>Told each frame of Scry's work as the next begins, with the frame's whole time, while the resource monitor shows.</summary>
        public static event Action<FrameTimes, double> FrameDone;

        public static Mark Start()
        {
            if (!On) return default;
            return new Mark { Ticks = Stopwatch.GetTimestamp(), Bytes = Allocated(), Cleanups = GC.CollectionCount(0), Rebuilds = _fontRebuilds, Id = Nesting.Open() };
        }

        /// <summary>
        /// Adds the time since <paramref name="started"/> to a part of this frame, and what it
        /// allocated: counted for Scry's thread where the runtime keeps that count, else told only
        /// roughly by the memory in use, and not at all across a cleanup.
        /// </summary>
        public static void Add(string part, Mark started)
        {
            if (!On || started.Ticks == 0) return;
            var ms = (Stopwatch.GetTimestamp() - started.Ticks) * 1000.0 / Stopwatch.Frequency;
            var cleanups = GC.CollectionCount(0) - started.Cleanups;
            var bytes = cleanups > 0 && !CountsPerThread ? 0 : Allocated() - started.Bytes;
            var (own, _) = Nesting.Close(started.Id, Math.Max(0, bytes), ms);
            Roll();
            Frame.Add(part, ms, bytes, cleanups, _fontRebuilds - started.Rebuilds, own);
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
                // A slow frame keeps all its parts, as the log tells them, to say where its time went.
                var slowest = Frame.SlowestWithout(SelfTestPart);
                var outer = Frame.SlowestOuter;
                var total = Frame.TotalWithout(SelfTestPart);
                Measuring.Add(total, slowest.Name, slowest.Ms, outer.Name, outer.Ms, Frame.Cleanups, total >= SlowMs ? Frame.Line(Time.unscaledDeltaTime * 1000f) : null);
                Measuring.AddTest(Frame.MsOf(SelfTestPart));
                Frame.AddBytesTo(BytesByPart);
                Frame.AddOwnBytesTo(OwnBytesByPart);
                Frame.AddOwnMsTo(OwnMsByPart);
            }
            // A part open across the frame's end is told whole when it closes.
            Nesting.Clear();
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
