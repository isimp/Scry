using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Scry
{
    /// <summary>A run kept: when it began, which run it was, how long its parts took, and every part's report.</summary>
    internal sealed class KeptRun
    {
        public readonly DateTime Started;
        public readonly RunScope Scope;
        public readonly double Seconds;
        public readonly List<ScenarioReport> Reports;

        public KeptRun(DateTime started, RunScope scope, double seconds, IEnumerable<ScenarioReport> reports)
        {
            Started = started;
            Scope = scope;
            Seconds = seconds;
            Reports = reports.ToList();
        }

        public DateTime Ended => Started.AddSeconds(Seconds);

        public int Failed => Reports.Count(r => r.Result == Result.Fail);
        public int Passed => Reports.Count(r => r.Result == Result.Pass);
    }

    /// <summary>
    /// The self-test's runs kept (<c>BepInEx/Scry-selftest.history</c>), the last <see cref="Kept"/>,
    /// each with every part's result, time and lines: a run set against the last run of its kind,
    /// a part's trail through the runs before, and the runs a log holds read back, for the runs
    /// made before any were kept.
    /// </summary>
    internal sealed class SelfTestHistory
    {
        /// <summary>How many runs are kept, the oldest going first.</summary>
        public const int Kept = 30;

        private const string When = "yyyy-MM-dd HH:mm:ss";

        public List<KeptRun> Runs { get; } = new List<KeptRun>();

        public void Add(KeptRun run)
        {
            Runs.Add(run);
            if (Runs.Count > Kept) Runs.RemoveRange(0, Runs.Count - Kept);
        }

        /// <summary>A run against the last run of its kind before it: the parts fixed since, those failing new, and those failing again; none before where it is the first of its kind.</summary>
        public (KeptRun Before, List<string> Fixed, List<string> New, List<string> Again) Against(KeptRun run)
        {
            var at = Runs.IndexOf(run);
            var before = at <= 0 ? null : Runs.Take(at).LastOrDefault(r => r.Scope == run.Scope);
            var failedBefore = new HashSet<string>(before?.Reports.Where(r => r.Result == Result.Fail).Select(r => r.Name) ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var failed = run.Reports.Where(r => r.Result == Result.Fail).Select(r => r.Name).ToList();
            var fixedNow = before == null ? new List<string>() : run.Reports.Where(r => r.Result == Result.Pass && failedBefore.Contains(r.Name)).Select(r => r.Name).ToList();
            return (before, fixedNow, failed.Where(n => !failedBefore.Contains(n)).ToList(), failed.Where(failedBefore.Contains).ToList());
        }

        /// <summary>A part through the runs up to one, those it ran in, the last five, oldest first: when, its result and what its first failed check found.</summary>
        public List<(DateTime At, Result Result, string Found)> Trail(string part, KeptRun upTo)
        {
            var trail = new List<(DateTime, Result, string)>();
            foreach (var run in Runs.Take(Runs.IndexOf(upTo) + 1))
            {
                var report = run.Reports.FirstOrDefault(r => r.Name == part);
                if (report == null) continue;
                var found = report.Lines.FirstOrDefault(l => l.StartsWith("  FAIL ", StringComparison.Ordinal));
                trail.Add((run.Started, report.Result, found == null ? "" : found.Substring(7)));
            }
            return trail.Skip(Math.Max(0, trail.Count - 5)).ToList();
        }

        /// <summary>What a failed part's trail says of it: failed so many runs in a row, flaky where it failed often but not in a row, else new.</summary>
        public static string Badge(IReadOnlyList<(DateTime At, Result Result, string Found)> trail)
        {
            var inARow = 0;
            for (var i = trail.Count - 1; i >= 0 && trail[i].Result == Result.Fail; i--) inARow++;
            if (inARow >= 2) return $"failed {Numbers.Count(inARow)} runs in a row";
            var failed = trail.Count(t => t.Result == Result.Fail);
            return failed >= 2 ? $"flaky, failed {Numbers.Count(failed)} of the last {Numbers.Count(trail.Count)}" : "new";
        }

        public string Save()
        {
            var text = new StringBuilder();
            foreach (var run in Runs)
            {
                text.Append("run\t").Append(run.Started.ToString(When, CultureInfo.InvariantCulture)).Append('\t').Append(run.Scope.ToString().ToLowerInvariant())
                    .Append('\t').Append(Stored.Number(run.Seconds, 1)).Append('\n');
                foreach (var report in run.Reports)
                {
                    text.Append("part\t").Append(Stored.Escaped(report.Name)).Append('\t').Append(report.Result.ToString().ToLowerInvariant()).Append('\t').Append(Stored.Number(report.Seconds, 1)).Append('\n');
                    foreach (var line in report.Lines) text.Append("line\t").Append(Stored.Escaped(line)).Append('\n');
                    foreach (var need in report.Needs) text.Append("need\t").Append(Stored.Escaped(need.Key)).Append('\t').Append(Stored.Escaped(need.Value)).Append('\n');
                }
            }
            return text.ToString();
        }

        public static SelfTestHistory Load(string text)
        {
            var history = new SelfTestHistory();
            var reports = new List<ScenarioReport>();
            (DateTime At, RunScope Scope, double Seconds)? run = null;
            ScenarioReport report = null;
            void Close()
            {
                if (run != null) history.Add(new KeptRun(run.Value.At, run.Value.Scope, run.Value.Seconds, reports));
                reports = new List<ScenarioReport>();
            }
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var parts = raw.TrimEnd('\r').Split('\t');
                switch (parts[0])
                {
                    case "run" when parts.Length == 4 && DateTime.TryParseExact(parts[1], When, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
                                                      && Enum.TryParse(parts[2], true, out RunScope scope) && Stored.TryNumber(parts[3], out var seconds):
                        Close();
                        run = (at, scope, seconds);
                        report = null;
                        break;
                    case "part" when run != null && parts.Length == 4 && Stored.TryUnescaped(parts[1], out var name) && Enum.TryParse(parts[2], true, out Result result) && Stored.TryNumber(parts[3], out var took):
                        report = new ScenarioReport { Name = name, Result = result, Seconds = took };
                        reports.Add(report);
                        break;
                    case "line" when report != null && parts.Length == 2 && Stored.TryUnescaped(parts[1], out var line):
                        report.Lines.Add(line);
                        break;
                    case "need" when report != null && parts.Length == 3 && Stored.TryUnescaped(parts[1], out var row) && Stored.TryUnescaped(parts[2], out var need):
                        report.Needs[row] = need;
                        break;
                }
            }
            Close();
            return history;
        }

        private static readonly Regex Header = new Regex(@"^==== Scry \S+ (\w+) self-test, (\d{4}-\d\d-\d\d \d\d:\d\d:\d\d),", RegexOptions.CultureInvariant);
        private static readonly Regex PartEnd = new Regex(@"^(PASS|FAIL|SKIP) (.+) \(([\d.,]+) s\)$", RegexOptions.CultureInvariant);

        /// <summary>The runs a log holds, each part from its START to its end line with the lines between; a run stopped part way keeps the parts it ended.</summary>
        public static SelfTestHistory FromLog(string log)
        {
            var history = new SelfTestHistory();
            (DateTime At, RunScope Scope)? run = null;
            var reports = new List<ScenarioReport>();
            ScenarioReport open = null;
            void Close()
            {
                if (run != null && reports.Count > 0) history.Add(new KeptRun(run.Value.At, run.Value.Scope, reports.Sum(r => r.Seconds), reports));
                reports = new List<ScenarioReport>();
                open = null;
            }
            foreach (var raw in (log ?? "").Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                var header = Header.Match(line);
                if (header.Success)
                {
                    Close();
                    run = DateTime.TryParseExact(header.Groups[2].Value, When, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) && Enum.TryParse(header.Groups[1].Value, true, out RunScope scope)
                        ? (at, scope) : ((DateTime, RunScope)?)null;
                    continue;
                }
                if (run == null) continue;
                if (line.StartsWith("START ", StringComparison.Ordinal)) open = new ScenarioReport { Name = line.Substring(6) };
                else if (open != null && line.StartsWith("  ", StringComparison.Ordinal)) open.Lines.Add(line);
                else if (open != null && PartEnd.Match(line) is Match end && end.Success && end.Groups[2].Value == open.Name)
                {
                    open.Result = end.Groups[1].Value == "PASS" ? Result.Pass : end.Groups[1].Value == "FAIL" ? Result.Fail : Result.Skip;
                    open.Seconds = Stored.TryNumber(end.Groups[3].Value.Replace(",", ""), out var seconds) ? seconds : 0;
                    reports.Add(open);
                    open = null;
                }
            }
            Close();
            return history;
        }
    }
}
