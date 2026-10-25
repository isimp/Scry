using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Which timed parts are open inside which, so a part's own cost is its whole less its inner
    /// parts': a part opens, its inner parts open and close, then it closes with its whole, and
    /// gets back what was its own. A close drops any part left open inside it; a close of a part
    /// not open counts the whole as its own. Keeps no garbage: a list of value tuples, grown once.
    /// </summary>
    internal sealed class PartNesting
    {
        private readonly List<(int Id, long InnerBytes, double InnerMs)> _open = new List<(int, long, double)>();
        private int _next;

        /// <summary>Opens a part inside those open; its id, to close it by.</summary>
        public int Open()
        {
            _open.Add((++_next, 0L, 0.0));
            return _next;
        }

        /// <summary>How many parts are open.</summary>
        public int Depth => _open.Count;

        /// <summary>Closes a part by its id with its whole cost: what was its own, the inner parts' taken off, the whole added to the part around it.</summary>
        public (long OwnBytes, double OwnMs) Close(int id, long bytes, double ms)
        {
            var at = -1;
            for (var i = _open.Count - 1; i >= 0; i--)
            {
                if (_open[i].Id != id) continue;
                at = i;
                break;
            }
            if (at < 0) return (bytes, ms);
            var (_, innerBytes, innerMs) = _open[at];
            _open.RemoveRange(at, _open.Count - at);
            if (_open.Count > 0)
            {
                var outer = _open[_open.Count - 1];
                _open[_open.Count - 1] = (outer.Id, outer.InnerBytes + bytes, outer.InnerMs + ms);
            }
            return (Math.Max(0L, bytes - innerBytes), Math.Max(0.0, ms - innerMs));
        }

        /// <summary>Forgets every open part, as a frame ends with some still open.</summary>
        public void Clear() => _open.Clear();
    }
}
