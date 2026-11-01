using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A run's outcome as the card shows it, each thing once: how many parts passed, failed and
    /// were skipped, how many rows were untried and checks not judged; each failed part with its
    /// failed checks split into what was checked and what was found; the untried rows grouped by
    /// where to stand, with the parts they belong to and what near the character frees them; the
    /// checks not judged by part; the skipped parts with why.
    /// </summary>
    internal sealed class SelfTestOutcome
    {
        private const string FailMark = "  FAIL ";
        private const string BusyMark = "  BUSY ";
        private const string UntriedMark = "  untried ";
        private const string SkipMark = "  SKIP ";

        public int Passed { get; private set; }
        public int Failed { get; private set; }
        public int Skipped { get; private set; }
        public int UntriedRows { get; private set; }
        public int NotJudged { get; private set; }

        public List<(string Part, double Seconds, List<(string What, string Found)> Checks)> FailedParts { get; } = new List<(string, double, List<(string, string)>)>();
        public List<(string Part, List<(string What, string Found)> Checks)> NotJudgedParts { get; } = new List<(string, List<(string, string)>)>();
        public List<(string Part, string Why)> SkippedParts { get; } = new List<(string, string)>();
        public List<UntriedGroup> UntriedByWhere { get; } = new List<UntriedGroup>();

        /// <summary>The untried rows sharing where to stand: what each would show, their parts, and what near frees them (none where nothing near can).</summary>
        internal sealed class UntriedGroup
        {
            public string Where;
            public string Need;
            public readonly List<string> Whats = new List<string>();
            public readonly List<string> Parts = new List<string>();
        }

        public static SelfTestOutcome Of(IEnumerable<ScenarioReport> reports)
        {
            var outcome = new SelfTestOutcome();
            foreach (var report in reports ?? Enumerable.Empty<ScenarioReport>()) outcome.Add(report);
            return outcome;
        }

        private void Add(ScenarioReport report)
        {
            if (report.Result == Result.Pass) Passed++;
            else if (report.Result == Result.Fail) Failed++;
            else Skipped++;
            var failed = Marked(report, FailMark).Select(Split).ToList();
            if (report.Result == Result.Fail) FailedParts.Add((report.Name, report.Seconds, failed));
            var busy = Marked(report, BusyMark).Select(Split).ToList();
            NotJudged += busy.Count;
            if (busy.Count > 0) NotJudgedParts.Add((report.Name, busy));
            if (report.Result == Result.Skip) SkippedParts.Add((report.Name, Marked(report, SkipMark).FirstOrDefault() ?? ""));
            foreach (var row in Marked(report, UntriedMark))
            {
                UntriedRows++;
                var (what, where) = Split(row);
                var group = UntriedByWhere.Find(g => g.Where == where);
                if (group == null)
                {
                    group = new UntriedGroup { Where = where };
                    UntriedByWhere.Add(group);
                }
                if (!group.Whats.Contains(what)) group.Whats.Add(what);
                if (!group.Parts.Contains(report.Name)) group.Parts.Add(report.Name);
                if (group.Need == null && report.Needs.TryGetValue(row, out var need)) group.Need = need;
            }
        }

        private static IEnumerable<string> Marked(ScenarioReport report, string mark) =>
            report.Lines.Where(l => l.StartsWith(mark, StringComparison.Ordinal)).Select(l => l.Substring(mark.Length));

        /// <summary>A check's line split into what was checked and what was found, at its first colon; all of it what was checked where it has none.</summary>
        private static (string What, string Found) Split(string line)
        {
            var at = line.IndexOf(": ", StringComparison.Ordinal);
            return at < 0 ? (line, "") : (line.Substring(0, at), line.Substring(at + 2));
        }
    }
}
