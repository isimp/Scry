using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Scry's work in one frame, by part: how long each took, about how much memory it
    /// allocated, and whether the runtime's memory cleanup ran inside it. A part named with a
    /// space ("panel list", "side facts") runs inside one named with one word ("panel"), so only
    /// the one-word parts add up to the frame. A cleanup pauses whatever is running, so it is told on
    /// the part it ran in, where it would otherwise look like slow code.
    /// </summary>
    public sealed class FrameTimes
    {
        private sealed class Part
        {
            public string Name;
            public double Ms;
            public long Bytes;
            public int Cleanups;
            public bool Outer => Name.IndexOf(' ') < 0;
        }

        private readonly List<Part> _parts = new List<Part>();
        private readonly Dictionary<string, Part> _byName = new Dictionary<string, Part>(StringComparer.Ordinal);

        /// <summary>The time of the outer parts, in milliseconds.</summary>
        public double Total => _parts.Where(p => p.Outer).Sum(p => p.Ms);

        /// <summary>What the outer parts allocated, in bytes.</summary>
        public long Bytes => _parts.Where(p => p.Outer).Sum(p => p.Bytes);

        /// <summary>The cleanups that ran inside the outer parts.</summary>
        public int Cleanups => _parts.Where(p => p.Outer).Sum(p => p.Cleanups);

        /// <summary>The time of the inner parts so far, in milliseconds.</summary>
        public double InnerMs => _parts.Where(p => !p.Outer).Sum(p => p.Ms);

        /// <summary>The time of one part, 0 when it has none.</summary>
        public double MsOf(string part) => part != null && _byName.TryGetValue(part, out var known) ? known.Ms : 0;

        /// <summary>The frame's time less one inner part's, which runs inside an outer part; an outer part is not taken out.</summary>
        public double TotalWithout(string inner) => Total - (inner != null && _byName.TryGetValue(inner, out var part) && !part.Outer ? part.Ms : 0);

        /// <summary>The part that took longest: of the inner parts, which say more about what was slow, else of the outer ones.</summary>
        public (string Name, double Ms) Slowest => SlowestWithout(null);

        /// <summary>The part that took longest, as <see cref="Slowest"/>, leaving one part out.</summary>
        public (string Name, double Ms) SlowestWithout(string left)
        {
            Part inner = null, outer = null;
            foreach (var part in _parts)
            {
                if (part.Ms <= 0 || part.Name == left) continue;
                if (part.Outer) { if (outer == null || part.Ms > outer.Ms) outer = part; }
                else if (inner == null || part.Ms > inner.Ms) inner = part;
            }
            var slowest = inner ?? outer;
            return slowest == null ? ("", 0.0) : (slowest.Name, slowest.Ms);
        }

        /// <summary>Each part with time this frame, as its name and milliseconds, for the resource monitor; nothing is made for it.</summary>
        public void ForEachPart(Action<string, double> each)
        {
            foreach (var part in _parts) if (part.Ms > 0) each(part.Name, part.Ms);
        }

        /// <summary>Adds to a part: its time, what it allocated, and how many cleanups ran inside it.</summary>
        public void Add(string part, double ms, long bytes, int cleanups)
        {
            if (!_byName.TryGetValue(part, out var known))
            {
                known = new Part { Name = part };
                _byName[part] = known;
                _parts.Add(known);
            }
            known.Ms += ms;
            known.Bytes += Math.Max(0, bytes);
            known.Cleanups += Math.Max(0, cleanups);
        }

        /// <summary>
        /// Empties the frame for the next. The parts are kept and zeroed rather than made again,
        /// since the same few dozen come back every frame and measuring should add no garbage.
        /// </summary>
        public void Clear()
        {
            foreach (var part in _parts)
            {
                part.Ms = 0;
                part.Bytes = 0;
                part.Cleanups = 0;
            }
            if (_parts.Count > 500)
            {
                _parts.Clear();
                _byName.Clear();
            }
        }

        /// <summary>
        /// The frame as one log line: its outer parts' time, any cleanups and allocation, then the
        /// parts slowest first. A part is left out when it took no time, allocated little and ran
        /// no cleanup.
        /// </summary>
        public string Line(double frameMs)
        {
            var cleanups = Cleanups;
            var kb = Kilobytes(Bytes);

            var head = $"Scry took {Numbers.Amount(Total, 0)} ms of a {Numbers.Amount(frameMs, 0)} ms frame";
            if (cleanups == 1) head += ", a memory cleanup ran inside it";
            else if (cleanups > 1) head += $", {Numbers.Count(cleanups)} memory cleanups ran inside it";
            if (kb > 0) head += $", allocating about {Numbers.Count(kb)} KB";

            var told = _parts
                .Where(p => Math.Round(p.Ms) >= 1 || Kilobytes(p.Bytes) > 0 || p.Cleanups > 0)
                .OrderByDescending(p => p.Ms)
                .Select(Told);
            return head + ": " + string.Join(", ", told) + ".";
        }

        private static string Told(Part part)
        {
            var text = $"{part.Name} {Numbers.Amount(part.Ms, 0)}";
            if (part.Cleanups == 1) return text + " [memory cleanup]";
            if (part.Cleanups > 1) return text + $" [{Numbers.Count(part.Cleanups)} memory cleanups]";
            var kb = Kilobytes(part.Bytes);
            return kb > 0 ? text + $" [{Numbers.Count(kb)} KB]" : text;
        }

        private static long Kilobytes(long bytes) => (long)Math.Round(bytes / 1024.0);
    }
}
