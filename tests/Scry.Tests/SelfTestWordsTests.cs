using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class SelfTestWordsTests
    {
        // The self-test tells the player what goes on in plain words: that it started and how
        // long it takes, how far it has got while it runs, and at the end what passed, with every
        // part that failed named with the checks that failed in it and every part skipped with
        // why, and what to do next.

        private static ScenarioReport Report(string name, Result result, params string[] lines)
        {
            var report = new ScenarioReport { Name = name, Result = result };
            report.Lines.AddRange(lines);
            return report;
        }

        private static List<ScenarioReport> Run() => new List<ScenarioReport>
        {
            Report("the panel opens and draws", Result.Pass, "  PASS the panel is open"),
            Report("a dungeon lays out an example", Result.Fail, "  PASS an example has been laid out", "  FAIL every kind of room could be loaded: 2 could not", "  note 12 rooms", "  FAIL it has rooms: 0"),
            Report("a model stands in the world", Result.Skip, "  SKIP something of yours stands there already"),
            Report("a sound plays and stops", Result.Pass, "  PASS it plays"),
        };

        [Fact]
        public void StartingSaysHowLongAndHowToStop()
        {
            var said = SelfTestWords.Started(60);
            Assert.Contains("60 parts", said);
            Assert.Contains("Stand still", said);
            Assert.Contains("/scry selftest stop", said);
        }

        [Fact]
        public void WhileRunningItSaysWhichPartRunsAndWhatFailedSoFar()
        {
            Assert.Equal("Self-test, part 14 of 60: a dungeon lays out an example. Stand still until it is done.", SelfTestWords.Progress(13, 60, "a dungeon lays out an example", 0));
            Assert.Equal("Self-test, part 14 of 60: a dungeon lays out an example. 2 failed so far. Stand still until it is done.", SelfTestWords.Progress(13, 60, "a dungeon lays out an example", 2));
            Assert.Equal("Self-test, 13 of 60 parts done. Stand still until it is done.", SelfTestWords.Progress(13, 60, null, 0));
        }

        [Fact]
        public void TheHeadlineCountsWhatPassedFailedAndWasSkipped()
        {
            Assert.Equal("Self-test done: 2 of 4 parts passed, 1 failed, 1 skipped.", SelfTestWords.Finished(Run()));
            var clean = new List<ScenarioReport> { Report("a", Result.Pass), Report("b", Result.Pass) };
            Assert.Equal("Self-test done: all 2 parts passed.", SelfTestWords.Finished(clean));
        }

        [Fact]
        public void TheSummaryNamesEachFailedPartWithItsFailedChecksThenTheSkippedThenThePassed()
        {
            Assert.Equal(new[]
            {
                "Failed (1):",
                "  a dungeon lays out an example",
                "    every kind of room could be loaded: 2 could not",
                "    it has rooms: 0",
                "Skipped (1):",
                "  a model stands in the world: something of yours stands there already",
                "Passed: 2.",
            }, SelfTestWords.Summary(Run()));
        }

        [Fact]
        public void ARunWithASkipButNoFailureIsNotAllPassedYetNeedsNothingDone()
        {
            var skipped = new List<ScenarioReport> { Report("a", Result.Pass), Report("b", Result.Skip, "  SKIP nothing to test") };

            Assert.Equal("Self-test done: 1 of 2 parts passed, 1 skipped.", SelfTestWords.Finished(skipped));
            Assert.StartsWith("Nothing to do", SelfTestWords.Advice(skipped, "f.log"));
        }

        [Fact]
        public void ACleanRunSummaryOnlyCountsThePassed()
        {
            Assert.Equal(new[] { "Passed: 1." }, SelfTestWords.Summary(new List<ScenarioReport> { Report("a", Result.Pass, "  PASS x") }));
        }

        [Fact]
        public void WhatToDoDependsOnWhetherAnythingFailed()
        {
            Assert.Contains("Send", SelfTestWords.Advice(Run(), "BepInEx/Scry-selftest.log"));
            Assert.Contains("BepInEx/Scry-selftest.log", SelfTestWords.Advice(Run(), "BepInEx/Scry-selftest.log"));
            Assert.StartsWith("Nothing to do", SelfTestWords.Advice(new List<ScenarioReport> { Report("a", Result.Pass) }, "f.log"));
        }
    }
}
