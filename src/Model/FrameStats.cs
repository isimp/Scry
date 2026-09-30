using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Scry's own work in each of many frames, as the self-test measures it: how long each frame's
    /// work took and which part of it took longest (<see cref="FrameTimes.Slowest"/>), told as the
    /// average, the 95th percentile, the most, and the slowest frames.
    /// </summary>
    public sealed class FrameStats
    {
        private readonly List<(double Ms, string Part, double PartMs)> _frames = new List<(double, string, double)>();

        public void Add(double ms, string slowestPart, double slowestMs) => _frames.Add((ms, slowestPart ?? "", slowestMs));

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
        public List<(double Ms, string Part, double PartMs)> Slowest(int count) => _frames.OrderByDescending(f => f.Ms).Take(count).ToList();

        public string Line(double budgetMs)
        {
            if (_frames.Count == 0) return "no frames measured";
            return $"{_frames.Count} frames: Scry's own work {Number(Mean)} ms on average, {Number(Percentile(0.95))} ms at the 95th percentile, " +
                   $"{Number(Max)} ms at the most; {Over(budgetMs)} frames over {Number(budgetMs)} ms";
        }

        public void Clear() => _frames.Clear();

        /// <summary>The frames from the given one on, told on their own.</summary>
        public FrameStats Since(int first)
        {
            var part = new FrameStats();
            for (var i = Math.Max(0, first); i < _frames.Count; i++) part._frames.Add(_frames[i]);
            return part;
        }

        private static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
