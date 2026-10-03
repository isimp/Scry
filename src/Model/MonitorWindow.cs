using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// What the resource monitor tells of Scry's last frames: its own time and each frame's,
    /// what its work allocated and the memory cleanups that ran in it, for its figures over the
    /// last seconds and a little graph of them; and each part of its work smoothed over the last
    /// couple of seconds, the largest first.
    /// </summary>
    internal sealed class MonitorWindow
    {
        private readonly double[] _scry;
        private readonly double[] _frame;
        private readonly long[] _bytes;
        private readonly int[] _cleanups;
        private int _next;
        private readonly double _smoothing;
        private readonly Dictionary<string, double> _parts = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _thisFrame = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly List<string> _names = new List<string>();

        /// <param name="frames">How many frames it keeps.</param>
        /// <param name="smoothing">How much of each frame a part's figure takes in.</param>
        public MonitorWindow(int frames, double smoothing = 0.02)
        {
            _scry = new double[Math.Max(1, frames)];
            _frame = new double[_scry.Length];
            _bytes = new long[_scry.Length];
            _cleanups = new int[_scry.Length];
            _smoothing = smoothing;
        }

        /// <summary>How many frames it holds.</summary>
        public int Count { get; private set; }

        /// <summary>A frame: Scry's own time in it and the frame's, in milliseconds, what Scry allocated and the cleanups that ran in its work.</summary>
        public void Add(double scryMs, double frameMs, long bytes, int cleanups)
        {
            _scry[_next] = scryMs;
            _frame[_next] = frameMs;
            _bytes[_next] = Math.Max(0, bytes);
            _cleanups[_next] = Math.Max(0, cleanups);
            _next = (_next + 1) % _scry.Length;
            Count = Math.Min(Count + 1, _scry.Length);
        }

        /// <summary>The frames held, oldest first.</summary>
        private IEnumerable<int> Held()
        {
            var first = (_next - Count + _scry.Length) % _scry.Length;
            for (var i = 0; i < Count; i++) yield return (first + i) % _scry.Length;
        }

        public double Last => Count == 0 ? 0 : _scry[(_next - 1 + _scry.Length) % _scry.Length];

        public double Mean => Count == 0 ? 0 : Held().Average(i => _scry[i]);

        public double Max => Count == 0 ? 0 : Held().Max(i => _scry[i]);

        /// <summary>Scry's time at a share of its frames, sorted from least to most.</summary>
        public double Percentile(double share)
        {
            if (Count == 0) return 0;
            var sorted = Held().Select(i => _scry[i]).OrderBy(ms => ms).ToList();
            var at = (int)Math.Ceiling(share * sorted.Count) - 1;
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, at))];
        }

        /// <summary>Scry's share of the frames' time.</summary>
        public double Share
        {
            get
            {
                var frames = Held().Sum(i => _frame[i]);
                return frames <= 0 ? 0 : Held().Sum(i => _scry[i]) / frames;
            }
        }

        /// <summary>How long the frames held lasted, in seconds.</summary>
        public double Seconds => Held().Sum(i => _frame[i]) / 1000.0;

        public double BytesPerSecond => Seconds <= 0 ? 0 : Held().Sum(i => (double)_bytes[i]) / Seconds;

        public int Cleanups => Held().Sum(i => _cleanups[i]);

        /// <summary>The frames held in so many stretches, oldest first, the most of each; the youngest at the right where it holds fewer frames than stretches.</summary>
        public double[] Columns(int columns)
        {
            var result = new double[Math.Max(1, columns)];
            var held = Held().ToList();
            if (held.Count == 0) return result;
            if (held.Count < result.Length)
            {
                for (var i = 0; i < held.Count; i++) result[result.Length - held.Count + i] = _scry[held[i]];
                return result;
            }
            for (var c = 0; c < result.Length; c++)
            {
                var from = c * held.Count / result.Length;
                var to = (c + 1) * held.Count / result.Length;
                for (var i = from; i < to; i++) result[c] = Math.Max(result[c], _scry[held[i]]);
            }
            return result;
        }

        /// <summary>A part's time in this frame, before <see cref="EndParts"/>.</summary>
        public void AddPart(string name, double ms)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!_parts.ContainsKey(name))
            {
                _parts[name] = 0;
                _names.Add(name);
            }
            _thisFrame.TryGetValue(name, out var so);
            _thisFrame[name] = so + ms;
        }

        /// <summary>The frame's parts are in: each part's figure keeps all but its smoothing's share and takes that share of this frame's time, nothing for a part it did not have.</summary>
        public void EndParts()
        {
            foreach (var name in _names)
            {
                _thisFrame.TryGetValue(name, out var ms);
                _parts[name] = _parts[name] * (1 - _smoothing) + _smoothing * ms;
            }
            _thisFrame.Clear();
        }

        /// <summary>The parts, their smoothed time each, the largest first, so many at the most.</summary>
        public List<(string Name, double Ms)> Parts(int most) =>
            _names.Select(n => (Name: n, Ms: _parts[n])).Where(p => p.Ms > 1e-9).OrderByDescending(p => p.Ms).Take(most).ToList();
    }
}
