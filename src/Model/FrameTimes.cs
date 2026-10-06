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
    internal sealed class FrameTimes
    {
        private sealed class Part
        {
            public string Name;
            public double Ms;
            public long Bytes;
            public int Cleanups;
            public int Rebuilds;
            public bool Outer => Name.IndexOf(' ') < 0;
        }

        private readonly List<Part> _parts = new List<Part>();
        private readonly Dictionary<string, Part> _byName = new Dictionary<string, Part>(StringComparer.Ordinal);

        // The sums below are read several times a frame inside the update they measure, so they
        // walk the parts by hand: what they allocated would be counted as Scry's own.

        /// <summary>The time of the outer parts, in milliseconds.</summary>
        public double Total
        {
            get
            {
                var sum = 0.0;
                foreach (var part in _parts) if (part.Outer) sum += part.Ms;
                return sum;
            }
        }

        /// <summary>What the outer parts allocated, in bytes.</summary>
        public long Bytes
        {
            get
            {
                var sum = 0L;
                foreach (var part in _parts) if (part.Outer) sum += part.Bytes;
                return sum;
            }
        }

        /// <summary>How often a font's texture was rebuilt inside the outer parts.</summary>
        public int Rebuilds
        {
            get
            {
                var sum = 0;
                foreach (var part in _parts) if (part.Outer) sum += part.Rebuilds;
                return sum;
            }
        }

        /// <summary>The cleanups that ran inside the outer parts.</summary>
        public int Cleanups
        {
            get
            {
                var sum = 0;
                foreach (var part in _parts) if (part.Outer) sum += part.Cleanups;
                return sum;
            }
        }

        /// <summary>The time of the inner parts so far, in milliseconds.</summary>
        public double InnerMs
        {
            get
            {
                var sum = 0.0;
                foreach (var part in _parts) if (!part.Outer) sum += part.Ms;
                return sum;
            }
        }

        /// <summary>The time of one part, 0 when it has none.</summary>
        public double MsOf(string part) => part != null && _byName.TryGetValue(part, out var known) ? known.Ms : 0;

        /// <summary>
        /// What the frame's outer parts allocated less what one inner part did inside them (the
        /// self-test's own checks), never below none; an outer part is not taken out.
        /// </summary>
        public long BytesWithout(string inner)
        {
            var left = Bytes;
            if (inner != null && _byName.TryGetValue(inner, out var part) && !part.Outer) left -= part.Bytes;
            return Math.Max(0, left);
        }

        /// <summary>The frame's time less one inner part's, which runs inside an outer part; an outer part is not taken out.</summary>
        public double TotalWithout(string inner) => Total - (inner != null && _byName.TryGetValue(inner, out var part) && !part.Outer ? part.Ms : 0);

        /// <summary>The part that took longest: of the inner parts, which say more about what was slow, else of the outer ones.</summary>
        public (string Name, double Ms) Slowest => SlowestWithout(null);

        /// <summary>The outer part that took longest, all inner parts in it included; nothing for an empty frame.</summary>
        public (string Name, double Ms) SlowestOuter
        {
            get
            {
                Part outer = null;
                foreach (var part in _parts) if (part.Outer && part.Ms > 0 && (outer == null || part.Ms > outer.Ms)) outer = part;
                return outer == null ? ("", 0.0) : (outer.Name, outer.Ms);
            }
        }

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

        /// <summary>Adds what each part allocated this frame, inner parts too, to a count by part; a part that allocated nothing is left out.</summary>
        public void AddBytesTo(Dictionary<string, long> byPart)
        {
            foreach (var part in _parts)
            {
                if (part.Bytes <= 0) continue;
                byPart.TryGetValue(part.Name, out var sum);
                byPart[part.Name] = sum + part.Bytes;
            }
        }

        /// <summary>Adds to a part: its time, what it allocated, how many cleanups ran inside it, and how often a font's texture was rebuilt in it.</summary>
        public void Add(string part, double ms, long bytes, int cleanups, int rebuilds = 0)
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
            known.Rebuilds += Math.Max(0, rebuilds);
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
                part.Rebuilds = 0;
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
            var rebuilds = Rebuilds;
            var kb = Kilobytes(Bytes);

            var head = $"Scry took {Numbers.Amount(Total, 0)} ms of a {Numbers.Amount(frameMs, 0)} ms frame";
            if (cleanups == 1) head += ", a memory cleanup ran inside it";
            else if (cleanups > 1) head += $", {Numbers.Count(cleanups)} memory cleanups ran inside it";
            if (rebuilds == 1) head += ", a font's texture was rebuilt inside it";
            else if (rebuilds > 1) head += $", a font's texture was rebuilt {Numbers.Count(rebuilds)} times inside it";
            if (kb > 0) head += $", allocating about {Numbers.Count(kb)} KB";

            var told = _parts
                .Where(p => Math.Round(p.Ms) >= 1 || Kilobytes(p.Bytes) > 0 || p.Cleanups > 0 || p.Rebuilds > 0)
                .OrderByDescending(p => p.Ms)
                .Select(Told);
            return head + ": " + string.Join(", ", told) + ".";
        }

        /// <summary>A part as a slow frame tells it: its time, then the cleanups and font rebuilds in it, and what it allocated where no cleanup hides that.</summary>
        private static string Told(Part part)
        {
            var text = $"{part.Name} {Numbers.Amount(part.Ms, 0)}";
            var marks = new List<string>();
            if (part.Cleanups == 1) marks.Add("memory cleanup");
            else if (part.Cleanups > 1) marks.Add($"{Numbers.Count(part.Cleanups)} memory cleanups");
            if (part.Rebuilds == 1) marks.Add("font rebuilt");
            else if (part.Rebuilds > 1) marks.Add($"{Numbers.Count(part.Rebuilds)} font rebuilds");
            var kb = Kilobytes(part.Bytes);
            if (part.Cleanups == 0 && kb > 0) marks.Add($"{Numbers.Count(kb)} KB");
            return marks.Count == 0 ? text : text + " [" + string.Join(", ", marks) + "]";
        }

        private static long Kilobytes(long bytes) => (long)Math.Round(bytes / 1024.0);
    }
}
