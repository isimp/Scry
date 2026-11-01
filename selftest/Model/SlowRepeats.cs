using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The frames of a whole run past a budget, judged by the part of Scry that took most of each:
    /// a part taking most of more than one such frame is a hot spot and fails; a frame whose part
    /// took no other is most likely a stall of the machine or a memory cleanup, as the same work
    /// runs quickly in other runs, and is only noted.
    /// </summary>
    internal static class SlowRepeats
    {
        public static (List<(string Part, int Frames, double Most)> Repeated, List<(double Ms, string Part, double PartMs, string Outer, double OuterMs, int Cleanups)> Once) Of(
            IEnumerable<(double Ms, string Part, double PartMs, string Outer, double OuterMs, int Cleanups)> frames, double budgetMs)
        {
            var slow = frames.Where(f => f.Ms > budgetMs).ToList();
            var byPart = slow.GroupBy(f => f.Part ?? "").ToList();
            var repeated = byPart.Where(g => g.Count() > 1).Select(g => (g.Key, g.Count(), g.Max(f => f.Ms))).ToList();
            var once = byPart.Where(g => g.Count() == 1).Select(g => g.First()).ToList();
            return (repeated, once);
        }

        /// <summary>The parts that came back, the slowest first: "update previews in 3 frames, 412 ms at the most".</summary>
        public static string Told(IEnumerable<(string Part, int Frames, double Most)> repeated) =>
            string.Join("; ", repeated.OrderByDescending(r => r.Most).Select(r => $"{r.Part} in {Numbers.Count(r.Frames)} frames, {Numbers.Amount(r.Most, 0)} ms at the most"));
    }
}
