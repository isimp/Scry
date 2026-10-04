using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// What the self-test tells the player: that it started and how long it takes, how far it has
    /// got while it runs, and at the end what passed, each failed part with the checks that failed
    /// in it, each skipped part with why, and what to do next.
    /// </summary>
    internal static class SelfTestWords
    {
        private const string FailMark = "  FAIL ";
        private const string SkipMark = "  SKIP ";

        public static string Started(int parts) =>
            $"self-test started: {Numbers.Count(parts)} parts, about five to fifteen minutes. Stand still and leave the panel be; it shows how far the test has got. /scry selftest stop stops it.";

        /// <summary>The line the panel shows while the test runs: the part running now, or how many are done between parts.</summary>
        public static string Progress(int done, int total, string current, int failed)
        {
            var at = current != null ? $"Self-test, part {Numbers.Count(done + 1)} of {Numbers.Count(total)}: {current}." : $"Self-test, {Numbers.Count(done)} of {Numbers.Count(total)} parts done.";
            var so = failed > 0 ? $" {Numbers.Count(failed)} failed so far." : "";
            return at + so + " Stand still until it is done.";
        }

        /// <summary>The headline at the end: all passed, or how many passed, failed and were skipped; with how long it took, when given.</summary>
        public static string Finished(IReadOnlyList<ScenarioReport> reports, double? seconds = null)
        {
            var passed = reports.Count(r => r.Result == Result.Pass);
            var failed = reports.Count(r => r.Result == Result.Fail);
            var skipped = reports.Count(r => r.Result == Result.Skip);
            var done = seconds.HasValue ? $"Self-test done in {Duration(seconds.Value)}" : "Self-test done";
            if (passed == reports.Count) return $"{done}: all {Numbers.Count(reports.Count)} parts passed.";
            var told = $"{done}: {Numbers.Count(passed)} of {Numbers.Count(reports.Count)} parts passed";
            if (failed > 0) told += $", {Numbers.Count(failed)} failed";
            if (skipped > 0) told += $", {Numbers.Count(skipped)} skipped";
            return told + ".";
        }

        /// <summary>Each failed part with the checks that failed in it, each skipped part with why, then how many passed.</summary>
        public static List<string> Summary(IReadOnlyList<ScenarioReport> reports)
        {
            var lines = new List<string>();
            var failed = reports.Where(r => r.Result == Result.Fail).ToList();
            if (failed.Count > 0)
            {
                lines.Add(Naming.Counted("Failed", failed.Count) + ":");
                foreach (var report in failed)
                {
                    lines.Add("  " + report.Name);
                    foreach (var line in report.Lines.Where(l => l.StartsWith(FailMark, System.StringComparison.Ordinal))) lines.Add("    " + line.Substring(FailMark.Length));
                }
            }
            var skipped = reports.Where(r => r.Result == Result.Skip).ToList();
            if (skipped.Count > 0)
            {
                lines.Add(Naming.Counted("Skipped", skipped.Count) + ":");
                foreach (var report in skipped)
                {
                    var why = report.Lines.FirstOrDefault(l => l.StartsWith(SkipMark, System.StringComparison.Ordinal));
                    lines.Add("  " + report.Name + (why != null ? ": " + why.Substring(SkipMark.Length) : ""));
                }
            }
            lines.Add($"Passed: {Numbers.Count(reports.Count(r => r.Result == Result.Pass))}.");
            return lines;
        }

        /// <summary>A run's length in whole seconds, or minutes and seconds from a minute on.</summary>
        public static string Duration(double seconds)
        {
            var whole = (int)Math.Round(seconds, MidpointRounding.AwayFromZero);
            if (whole < 60) return $"{Numbers.Count(whole)} s";
            return whole % 60 == 0 ? $"{Numbers.Count(whole / 60)} min" : $"{Numbers.Count(whole / 60)} min {Numbers.Count(whole % 60)} s";
        }

        /// <summary>What to do next: nothing when all went well, else send the log.</summary>
        public static string Advice(IReadOnlyList<ScenarioReport> reports, string file) =>
            reports.Any(r => r.Result == Result.Fail)
                ? $"Send {file} to whoever looks after Scry: the failed parts above say what went wrong, and the file has the rest."
                : $"Nothing to do. The whole run is written in {file}.";
    }
}
