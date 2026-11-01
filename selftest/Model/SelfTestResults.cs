using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>
    /// Each part's last outcome (<c>BepInEx/Scry-selftest.result</c>), for a session to read
    /// without the log and for the runs of what was left: a <c>part</c> line with its name and
    /// result, then a line for each check that failed (<c>fail</c>), each not judged as the machine
    /// was busy (<c>busy</c>) and each row untried (<c>untried</c>, with what near the character
    /// would let it be tried, or a dash). A run replaces the lines of the parts it ran; a part not
    /// run again keeps its last.
    /// </summary>
    internal sealed class SelfTestResults
    {
        private const string FailMark = "  FAIL ";
        private const string BusyMark = "  BUSY ";
        private const string UntriedMark = "  untried ";
        private const string NoNeed = "-";

        private sealed class Outcome
        {
            public Result Result;
            public readonly List<string> Failed = new List<string>();
            public readonly List<string> Busy = new List<string>();
            public readonly List<(string Need, string Row)> Untried = new List<(string, string)>();
        }

        private readonly List<string> _order = new List<string>();
        private readonly Dictionary<string, Outcome> _parts = new Dictionary<string, Outcome>(StringComparer.Ordinal);

        /// <summary>The outcomes kept, with the parts a run took put in place of their last.</summary>
        public static string Merge(string kept, IEnumerable<ScenarioReport> reports)
        {
            var results = Load(kept);
            foreach (var report in reports ?? Enumerable.Empty<ScenarioReport>()) results.Put(report.Name, Of(report));
            return results.Save();
        }

        private static Outcome Of(ScenarioReport report)
        {
            var outcome = new Outcome { Result = report.Result };
            foreach (var line in report.Lines)
            {
                if (line.StartsWith(FailMark, StringComparison.Ordinal)) outcome.Failed.Add(line.Substring(FailMark.Length));
                else if (line.StartsWith(BusyMark, StringComparison.Ordinal)) outcome.Busy.Add(line.Substring(BusyMark.Length));
                else if (line.StartsWith(UntriedMark, StringComparison.Ordinal))
                {
                    var row = line.Substring(UntriedMark.Length);
                    outcome.Untried.Add((report.Needs.TryGetValue(row, out var need) ? need : null, row));
                }
            }
            return outcome;
        }

        private void Put(string name, Outcome outcome)
        {
            if (!_parts.ContainsKey(name)) _order.Add(name);
            _parts[name] = outcome;
        }

        public static SelfTestResults Load(string text)
        {
            var results = new SelfTestResults();
            Outcome current = null;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var parts = raw.TrimEnd('\r').Split('\t');
                switch (parts[0])
                {
                    case "part" when parts.Length == 3 && Enum.TryParse(parts[2], true, out Result result):
                        current = new Outcome { Result = result };
                        results.Put(parts[1], current);
                        break;
                    case "fail" when current != null && parts.Length == 2: current.Failed.Add(parts[1]); break;
                    case "busy" when current != null && parts.Length == 2: current.Busy.Add(parts[1]); break;
                    case "untried" when current != null && parts.Length == 3: current.Untried.Add((parts[1] == NoNeed ? null : parts[1], parts[2])); break;
                }
            }
            return results;
        }

        public string Save()
        {
            var text = new StringBuilder();
            foreach (var name in _order)
            {
                var outcome = _parts[name];
                text.Append("part\t").Append(name).Append('\t').Append(outcome.Result.ToString().ToLowerInvariant()).Append('\n');
                foreach (var line in outcome.Failed) text.Append("fail\t").Append(line).Append('\n');
                foreach (var line in outcome.Busy) text.Append("busy\t").Append(line).Append('\n');
                foreach (var (need, row) in outcome.Untried) text.Append("untried\t").Append(need ?? NoNeed).Append('\t').Append(row).Append('\n');
            }
            return text.ToString();
        }

        /// <summary>A part's last result; none for a part never run.</summary>
        public Result? Of(string part) => _parts.TryGetValue(part, out var outcome) ? outcome.Result : (Result?)null;

        /// <summary>The parts that failed last, in the order they were last run.</summary>
        public List<string> WithFailed() => _order.Where(n => _parts[n].Result == Result.Fail).ToList();

        /// <summary>The parts with checks not judged last as the machine was busy.</summary>
        public List<string> WithBusy() => _order.Where(n => _parts[n].Busy.Count > 0).ToList();

        /// <summary>How many checks were not judged last as the machine was busy.</summary>
        public int BusyChecks => _parts.Values.Sum(o => o.Busy.Count);

        /// <summary>Every row untried last: its part, what near the character would let it be tried (none where nothing near can), and the row.</summary>
        public List<(string Part, string Need, string Row)> Untried() =>
            _order.SelectMany(n => _parts[n].Untried.Select(u => (n, u.Need, u.Row))).ToList();
    }
}
