using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// The self-test keeps its runs, each with every part's result and lines, the last ones kept:
    /// a run is told against the last of its kind (fixed, failed new, failed again), a failed part
    /// says how it went in the runs before, and the runs the log already holds are read back once.
    /// </summary>
    public class SelfTestHistoryTests
    {
        private static ScenarioReport Report(string name, Result result, double seconds, params string[] lines)
        {
            var report = new ScenarioReport { Name = name, Result = result, Seconds = seconds };
            report.Lines.AddRange(lines);
            return report;
        }

        private static readonly DateTime Morning = new DateTime(2026, 11, 1, 10, 47, 17);

        private static KeptRun Run(DateTime at, RunScope scope, params ScenarioReport[] reports) => new KeptRun(at, scope, reports.Sum(r => r.Seconds), reports);

        private static SelfTestHistory Day()
        {
            var history = new SelfTestHistory();
            history.Add(Run(Morning, RunScope.Quick,
                Report("page laid out", Result.Fail, 20, "  FAIL timed out after 20 s"),
                Report("watched", Result.Fail, 4, "  FAIL patches sit: UnmoddedTooltip"),
                Report("whole run", Result.Fail, 0.1, "  FAIL no frame took long: 786 ms")));
            history.Add(Run(Morning.AddHours(2), RunScope.Quick,
                Report("page laid out", Result.Fail, 20.4, "  FAIL no frame of laying it out takes long: 61 ms"),
                Report("watched", Result.Pass, 4),
                Report("whole run", Result.Fail, 0.1, "  FAIL no frame took long: 404 ms"),
                Report("search", Result.Pass, 2)));
            history.Add(Run(Morning.AddHours(3), RunScope.Parts, Report("search", Result.Pass, 1)));
            history.Add(Run(Morning.AddHours(4), RunScope.Quick,
                Report("page laid out", Result.Fail, 20.1, "  FAIL no frame of laying it out takes long: 69 ms"),
                Report("watched", Result.Pass, 4),
                Report("whole run", Result.Fail, 0.1, "  FAIL no frame took long: 702 ms"),
                Report("search", Result.Fail, 2, "  FAIL every search takes under 50 ms: 126 ms")));
            return history;
        }

        [Fact]
        public void RunsAreKeptWithEveryPartAndReadBackTheSameTheOldestGoingPastThirty()
        {
            var history = Day();
            var back = SelfTestHistory.Load(history.Save());
            Assert.Equal(4, back.Runs.Count);
            var last = back.Runs[3];
            Assert.Equal(Morning.AddHours(4), last.Started);
            Assert.Equal(RunScope.Quick, last.Scope);
            Assert.Equal(new[] { "page laid out", "watched", "whole run", "search" }, last.Reports.Select(r => r.Name));
            Assert.Equal(new[] { "  FAIL no frame of laying it out takes long: 69 ms" }, last.Reports[0].Lines);
            Assert.Equal(20.1, last.Reports[0].Seconds, 3);
            for (var i = 0; i < 40; i++) back.Add(Run(Morning.AddDays(1).AddMinutes(i), RunScope.Parts, Report("search", Result.Pass, 1)));
            Assert.Equal(SelfTestHistory.Kept, back.Runs.Count);
            Assert.Equal(Morning.AddDays(1).AddMinutes(39), back.Runs.Last().Started);
        }

        [Fact]
        public void ARunIsToldAgainstTheLastOfItsKindFixedNewAndAgain()
        {
            var history = Day();
            var diff = history.Against(history.Runs[3]);
            Assert.Equal(history.Runs[1], diff.Before);
            Assert.Empty(diff.Fixed);
            Assert.Equal(new[] { "search" }, diff.New);
            Assert.Equal(new[] { "page laid out", "whole run" }, diff.Again);
            var earlier = history.Against(history.Runs[1]);
            Assert.Equal(new[] { "watched" }, earlier.Fixed);
            Assert.Null(history.Against(history.Runs[0]).Before);
        }

        [Fact]
        public void AFailedPartSaysHowItWentInTheRunsBefore()
        {
            var history = Day();
            var trail = history.Trail("page laid out", history.Runs[3]);
            Assert.Equal(new[] { Result.Fail, Result.Fail, Result.Fail }, trail.Select(t => t.Result));
            Assert.Equal(new[] { "timed out after 20 s", "no frame of laying it out takes long: 61 ms" }, trail.Take(2).Select(t => t.Found));
            Assert.Equal("failed 3 runs in a row", SelfTestHistory.Badge(trail));
            Assert.Equal("new", SelfTestHistory.Badge(history.Trail("search", history.Runs[3])));
            var flaky = new List<(DateTime, Result, string)> { (Morning, Result.Fail, ""), (Morning, Result.Pass, ""), (Morning, Result.Fail, ""), (Morning, Result.Pass, ""), (Morning, Result.Fail, "") };
            Assert.Equal("flaky, failed 3 of the last 5", SelfTestHistory.Badge(flaky));
        }

        [Fact]
        public void AKeptRunsTilesStandByTheAreasThePartsHaveNowAPartNoLongerListedApart()
        {
            var run = Day().Runs[3];
            var live = SelfTestLive.Of(run, name => name == "search" ? "Panel and search" : name == "watched" ? "Compendium" : null);
            Assert.Equal(new[] { SelfTestLive.Elsewhere, "Compendium", "Panel and search" }, live.Areas.Select(a => a.Area));
            Assert.Equal(new[] { TileState.Fail, TileState.Pass, TileState.Fail, TileState.Fail }, live.InRunOrder.Select(t => t.State));
        }

        [Fact]
        public void TheRunsTheLogHoldsAreReadBackPartByPart()
        {
            var log = string.Join("\n",
                "==== Scry 0.3.3 quick self-test, 2026-11-01 14:22:16, started because it was asked for with /scry selftest ====",
                "Each part uses Scry as a player would and checks what it did.",
                "FAIL one that did not (with what was found), and note is something worth knowing.",
                "START the panel opens",
                "  PASS it opens",
                "PASS the panel opens (0.2 s)",
                "START a page laid out",
                "  FAIL no frame of laying it out takes long: 69.3 ms",
                "  note slowest frame",
                "FAIL a page laid out (20.4 s)",
                "START ground asks",
                "  SKIP this game runs the world",
                "SKIP ground asks (0.0 s)",
                "==== Summary ====",
                "Failed (1):",
                "==== Scry 0.3.3 parts self-test, 2026-11-01 14:38:17, started because it was asked for from the card ====",
                "START the panel opens",
                "  PASS it opens",
                "  untried a beehive: stand near a beehive",
                "PASS the panel opens (0.1 s)");
            var runs = SelfTestHistory.FromLog(log).Runs;
            Assert.Equal(2, runs.Count);
            Assert.Equal(new DateTime(2026, 11, 1, 14, 22, 16), runs[0].Started);
            Assert.Equal(RunScope.Quick, runs[0].Scope);
            Assert.Equal(new[] { ("the panel opens", Result.Pass), ("a page laid out", Result.Fail), ("ground asks", Result.Skip) }, runs[0].Reports.Select(r => (r.Name, r.Result)));
            Assert.Equal(new[] { "  FAIL no frame of laying it out takes long: 69.3 ms", "  note slowest frame" }, runs[0].Reports[1].Lines);
            Assert.Equal(20.4, runs[0].Reports[1].Seconds, 3);
            Assert.Equal(RunScope.Parts, runs[1].Scope);
        }
    }
}
