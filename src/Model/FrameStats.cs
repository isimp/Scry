using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Scry's own work in each of many frames, as the self-test measures it: how long each frame's
    /// work took, which part of it took longest (<see cref="FrameTimes.Slowest"/>), the outer part
    /// it ran in and how many memory cleanups ran in the frame, told as the average, the 95th
    /// percentile, the most, and the slowest frames.
    /// </summary>
    internal sealed class FrameStats
    {
        private readonly List<(double Ms, string Part, double PartMs, string Outer, double OuterMs, int Cleanups)> _frames =
            new List<(double, string, double, string, double, int)>();

        /// <summary>A frame: its time, its slowest part, its slowest outer part (<see cref="FrameTimes.SlowestOuter"/>) and the memory cleanups that ran in it.</summary>
        public void Add(double ms, string slowestPart, double slowestMs, string outer = null, double outerMs = 0, int cleanups = 0) =>
            _frames.Add((ms, slowestPart ?? "", slowestMs, outer ?? "", outerMs, cleanups));

        /// <summary>
        /// A slow frame in words: its time, its slowest part, the outer part it ran in, and the
        /// memory cleanups in it, which can make a frame long with no part of Scry's slow.
        /// </summary>
        public static string Told((double Ms, string Part, double PartMs, string Outer, double OuterMs, int Cleanups) frame)
        {
            var told = $"a slow frame: {Numbers.Amount(frame.Ms, 1)} ms, the slowest part {frame.Part} {Numbers.Amount(frame.PartMs, 1)} ms";
            if (frame.Outer.Length > 0) told += $", in {frame.Outer} {Numbers.Amount(frame.OuterMs, 1)} ms";
            if (frame.Cleanups == 1) told += ", a memory cleanup ran in it";
            else if (frame.Cleanups > 1) told += $", {Numbers.Count(frame.Cleanups)} memory cleanups ran in it";
            return told;
        }

        /// <summary>The most the self-test's own checks took in a frame, left out of the frames above.</summary>
        public double TestMax { get; private set; }

        public void AddTest(double ms) => TestMax = Math.Max(TestMax, ms);

        public int Frames => _frames.Count;

        public double Mean => _frames.Count == 0 ? 0 : _frames.Average(f => f.Ms);

        public double Max => _frames.Count == 0 ? 0 : _frames.Max(f => f.Ms);

        /// <summary>The time no more than the given share of frames stay under (nearest rank), 0 with none.</summary>
        public double Percentile(double share)
        {
            if (_frames.Count == 0) return 0;
            var sorted = _frames.Select(f => f.Ms).OrderBy(ms => ms).ToList();
            var rank = (int)Math.Ceiling(share * sorted.Count);
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, rank - 1))];
        }

        /// <summary>How many frames took longer than so many milliseconds.</summary>
        public int Over(double ms) => _frames.Count(f => f.Ms > ms);

        /// <summary>The slowest frames, slowest first, each with its slowest part.</summary>
        public List<(double Ms, string Part, double PartMs, string Outer, double OuterMs, int Cleanups)> Slowest(int count) =>
            _frames.OrderByDescending(f => f.Ms).Take(count).ToList();

        public string Line(double budgetMs)
        {
            if (_frames.Count == 0) return "no frames measured";
            return $"{Numbers.Count(_frames.Count)} frames: Scry's own work {Numbers.Amount(Mean, 1)} ms on average, {Numbers.Amount(Percentile(0.95), 1)} ms at the 95th percentile, " +
                   $"{Numbers.Amount(Max, 1)} ms at the most; {Numbers.Count(Over(budgetMs))} frames over {Numbers.Amount(budgetMs, 1)} ms";
        }

        public void Clear()
        {
            _frames.Clear();
            TestMax = 0;
        }

        /// <summary>The frames from the given one on, told on their own.</summary>
        public FrameStats Since(int first)
        {
            var part = new FrameStats();
            for (var i = Math.Max(0, first); i < _frames.Count; i++) part._frames.Add(_frames[i]);
            return part;
        }
    }
}
