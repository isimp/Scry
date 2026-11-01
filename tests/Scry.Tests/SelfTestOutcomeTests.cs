using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// A run's outcome as the card shows it: five numbers, each failed part once with its failed
    /// checks split into what was checked and what was found, the untried rows grouped by where
    /// to stand rather than by part, the checks not judged by part, and the skipped parts with why.
    /// </summary>
    public class SelfTestOutcomeTests
    {
        private static ScenarioReport Report(string name, Result result, double seconds, params string[] lines)
        {
            var report = new ScenarioReport { Name = name, Result = result, Seconds = seconds };
            report.Lines.AddRange(lines);
            return report;
        }

        private static List<ScenarioReport> Run()
        {
            var learning = Report("learning paths", Result.Pass, 12, "  PASS twins", "  untried a boss seen at its altar: stand near a boss's altar", "  untried a Vegvisir's place: stand near a Vegvisir");
            learning.Needs["a boss seen at its altar: stand near a boss's altar"] = "altar";
            learning.Needs["a Vegvisir's place: stand near a Vegvisir"] = "vegvisir";
            var watched = Report("compendium watched", Result.Pass, 4, "  untried the places a Vegvisir marks: stand near a Vegvisir");
            watched.Needs["the places a Vegvisir marks: stand near a Vegvisir"] = "vegvisir";
            return new List<ScenarioReport>
            {
                Report("opens", Result.Pass, 1, "  PASS it opens"),
                Report("page laid out", Result.Fail, 20.4, "  PASS answers", "  FAIL no frame of laying it out takes long: 61 ms at the most", "  FAIL timed out after 20 s"),
                learning,
                watched,
                Report("search", Result.Pass, 2, "  BUSY every search takes under 50 ms: 126 ms; this machine ran 2.0 times slower than its best"),
                Report("ground asks", Result.Skip, 0, "  SKIP this game runs the world"),
            };
        }

        [Fact]
        public void TheVerdictIsFiveNumbers()
        {
            var outcome = SelfTestOutcome.Of(Run());
            Assert.Equal((4, 1, 3, 1, 1), (outcome.Passed, outcome.Failed, outcome.UntriedRows, outcome.Skipped, outcome.NotJudged));
        }

        [Fact]
        public void EachFailedPartIsNamedOnceItsChecksSplitIntoWhatAndWhatWasFound()
        {
            var failed = SelfTestOutcome.Of(Run()).FailedParts;
            Assert.Single(failed);
            Assert.Equal("page laid out", failed[0].Part);
            Assert.Equal(20.4, failed[0].Seconds);
            Assert.Equal(new[] { ("no frame of laying it out takes long", "61 ms at the most"), ("timed out after 20 s", "") }, failed[0].Checks);
        }

        [Fact]
        public void UntriedRowsAreGroupedByWhereToStandWithTheirPartsAndWhatNearFreesThem()
        {
            var groups = SelfTestOutcome.Of(Run()).UntriedByWhere;
            Assert.Equal(new[] { "stand near a boss's altar", "stand near a Vegvisir" }, groups.Select(g => g.Where));
            Assert.Equal(new[] { "a Vegvisir's place", "the places a Vegvisir marks" }, groups[1].Whats);
            Assert.Equal(new[] { "learning paths", "compendium watched" }, groups[1].Parts);
            Assert.Equal("vegvisir", groups[1].Need);
        }

        [Fact]
        public void ChecksNotJudgedAreByPartAndSkippedPartsSayWhy()
        {
            var outcome = SelfTestOutcome.Of(Run());
            Assert.Equal("search", outcome.NotJudgedParts[0].Part);
            Assert.Equal(("every search takes under 50 ms", "126 ms; this machine ran 2.0 times slower than its best"), outcome.NotJudgedParts[0].Checks[0]);
            Assert.Equal(new[] { ("ground asks", "this game runs the world") }, outcome.SkippedParts);
        }

        [Fact]
        public void NothingToShowIsNoSection()
        {
            var clean = SelfTestOutcome.Of(new[] { Report("opens", Result.Pass, 1, "  PASS it opens") });
            Assert.Empty(clean.FailedParts);
            Assert.Empty(clean.UntriedByWhere);
            Assert.Empty(clean.NotJudgedParts);
            Assert.Empty(clean.SkippedParts);
            Assert.Equal(1, clean.Passed);
        }

        [Fact]
        public void ARunOfNamedPartsTakesJustThoseInTheRunsOrder()
        {
            var all = new[] { "opens", "page laid out", "search" }.Select(n => new Scenario(n, p => null)).ToList();
            var asked = new SelfTestRequest { Ask = SelfTestAsk.Start, Scope = RunScope.Parts, Words = new[] { "search", "opens" } };
            Assert.Equal(new[] { "opens", "search" }, Scenario.ForRun(all, asked, null).Select(s => s.Name));
            Assert.Equal("self-test of the parts picked started", SelfTestWords.Started(2, RunScope.Parts, 10).Substring(0, 37));
        }
    }
}
