using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Scry's work over a stretch of frames, by part (<see cref="FrameTimes"/>): the time a frame
    /// on average, the slowest frame, and each part's share, so a steady cost can be told apart
    /// from a single slow frame and named by the part that takes it.
    /// </summary>
    internal sealed class PartWindow
    {
        /// <summary>How many of the parts inside others are told, slowest first.</summary>
        private const int InnerTold = 8;

        private readonly Dictionary<string, double> _ms = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly List<string> _order = new List<string>();
        private int _frames;
        private double _total;
        private double _most;

        // Made once: adding runs every frame inside the work it measures, and would otherwise allocate.
        private readonly Action<string, double> _add;

        public PartWindow()
        {
            _add = (part, ms) =>
            {
                if (!_ms.TryGetValue(part, out var sum)) _order.Add(part);
                _ms[part] = sum + ms;
            };
        }

        /// <summary>Adds one frame's parts.</summary>
        public void Add(FrameTimes frame)
        {
            var total = frame.Total;
            _frames++;
            _total += total;
            if (total > _most) _most = total;
            frame.ForEachPart(_add);
        }

        /// <summary>Starts the next stretch empty.</summary>
        public void Reset()
        {
            _ms.Clear();
            _order.Clear();
            _frames = 0;
            _total = 0;
            _most = 0;
        }

        public string Line(double seconds)
        {
            var head = $"In the last {Numbers.Amount(seconds, 0)} s";
            if (_frames == 0) return head + " Scry did no work it times.";

            // A part named with one word is outer: only those add up to the frame (FrameTimes).
            var shown = _order
                .Select(part => (Part: part, Ms: _ms[part] / _frames))
                .Where(p => Math.Round(p.Ms, 2) > 0)
                .OrderByDescending(p => p.Ms)
                .ToList();
            string Told((string Part, double Ms) p) => $"{p.Part} {Numbers.Fixed(p.Ms, 2)}";
            var outer = shown.Where(p => p.Part.IndexOf(' ') < 0).Select(Told);
            var inner = shown.Where(p => p.Part.IndexOf(' ') >= 0).Take(InnerTold).Select(Told).ToList();

            var line = $"{head} Scry's work took {Numbers.Fixed(_total / _frames, 2)} ms a frame over {Naming.Count(_frames, "frame", "frames")}, {Numbers.Fixed(_most, 2)} ms at most: {string.Join(", ", outer)}";
            if (inner.Count > 0) line += "; inside them " + string.Join(", ", inner);
            return line + ".";
        }
    }
}
