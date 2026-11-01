using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// What the self-test recommends running next, each a run of its own: the parts a session
    /// planned (<see cref="SelfTestPlan"/>), the rows untried last whose need is near the
    /// character now, the parts that failed last, and the checks not judged as the machine was
    /// busy, ready once it runs near its best again (<see cref="QuietAt"/>); with the untried rows
    /// nothing near frees, counted.
    /// </summary>
    internal sealed class SelfTestRecommended
    {
        /// <summary>How near its best the machine must run again for the checks not judged to be worth running.</summary>
        public const double QuietAt = 1.3;

        public List<string> Planned { get; private set; } = new List<string>();
        public List<(string Part, string Need, string Row)> Near { get; private set; } = new List<(string, string, string)>();
        public List<string> Failed { get; private set; } = new List<string>();
        public List<string> BusyParts { get; private set; } = new List<string>();
        public int BusyChecks { get; private set; }
        public bool BusyReady { get; private set; }
        public int UntriedElsewhere { get; private set; }

        /// <summary>What to recommend, from the plan, the last outcomes, the needs near the character now and how fast the machine runs.</summary>
        public static SelfTestRecommended Of(SelfTestPlan plan, IReadOnlyList<string> all, SelfTestResults results, ICollection<string> near, double slowdown)
        {
            var untried = results.Untried();
            return new SelfTestRecommended
            {
                Planned = plan.Parts(all),
                Near = untried.Where(u => u.Need != null && near.Contains(u.Need)).ToList(),
                UntriedElsewhere = untried.Count(u => u.Need == null || !near.Contains(u.Need)),
                Failed = results.WithFailed(),
                BusyParts = results.WithBusy(),
                BusyChecks = results.BusyChecks,
                BusyReady = slowdown < QuietAt,
            };
        }

        /// <summary>The strip's line: each recommendation counted; none where nothing is recommended.</summary>
        public string Line
        {
            get
            {
                var parts = new List<string>();
                if (Planned.Count > 0) parts.Add($"{Numbers.Count(Planned.Count)} {(Planned.Count == 1 ? "part" : "parts")} planned for a change");
                if (Near.Count > 0) parts.Add($"{Numbers.Count(Near.Count)} untried {(Near.Count == 1 ? "row" : "rows")} can be tried near you");
                if (Failed.Count > 0) parts.Add($"{Numbers.Count(Failed.Count)} {(Failed.Count == 1 ? "part" : "parts")} failed last run");
                if (BusyChecks > 0) parts.Add($"{Numbers.Count(BusyChecks)} {(BusyChecks == 1 ? "check waits" : "checks wait")} for a quiet machine");
                return parts.Count == 0 ? null : "Self-test: " + string.Join(", ", parts) + ".";
            }
        }
    }
}
