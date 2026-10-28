using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The pieces of work spread over frames that each took longer than a limit in one go, by what
    /// they did, so the piece that holds up a frame can be named once the work is done.
    /// </summary>
    internal sealed class LongPieces
    {
        /// <summary>How many of them a line names, slowest first.</summary>
        private const int Told = 25;

        private readonly double _limitMs;
        private readonly List<(string What, double Ms)> _pieces = new List<(string, double)>();

        public LongPieces(double limitMs) => _limitMs = limitMs;

        /// <summary>Whether a piece that took so long is long, asked first where naming it costs.</summary>
        public bool Long(double ms) => ms > _limitMs;

        /// <summary>Keeps a piece that took longer than the limit.</summary>
        public void Add(string what, double ms)
        {
            if (Long(ms)) _pieces.Add((what, ms));
        }

        public void Clear() => _pieces.Clear();

        /// <summary>The long pieces under a title, with how many there were; none when none was long.</summary>
        public string Line(string title)
        {
            if (_pieces.Count == 0) return null;
            var named = _pieces.OrderByDescending(p => p.Ms).Take(Told).Select(p => $"{p.What} {Numbers.Amount(p.Ms, 0)}");
            return $"{Naming.Counted($"{title} over {Numbers.Amount(_limitMs, 0)} ms", _pieces.Count)}: {string.Join(", ", named)}.";
        }
    }
}
