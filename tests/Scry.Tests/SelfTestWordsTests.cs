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
            var said = SelfTestWords.Started(60, RunScope.Quick, 540);
            Assert.Contains("quick self-test", said);
            Assert.Contains("60 parts", said);
            Assert.Contains("about 9 min", said);
            Assert.Contains("Stand still", said);
            Assert.Contains("/scry selftest stop", said);
            Assert.Contains("/scry selftest deep", said);
            Assert.Contains("/scry selftest failed", said);

            var full = SelfTestWords.Started(70, RunScope.Full, 1500);
            Assert.Contains("full self-test", full);
            Assert.Contains("70 parts", full);
            Assert.Contains("about 25 min", full);
            Assert.DoesNotContain("/scry selftest deep", full);

            Assert.Contains("1 part", SelfTestWords.Started(1, RunScope.Named, 20));
            Assert.Contains("less than a minute", SelfTestWords.Started(1, RunScope.Named, 20));
            // Only a quick run points to the deeper ones.
            Assert.DoesNotContain("/scry selftest deep", SelfTestWords.Started(1, RunScope.Named, 20));
        }

        [Fact]
        public void NothingToRunSaysWhy()
        {
            Assert.Contains("no part's name holds", SelfTestWords.NothingToRun(SelfTestCommand.Of("selftest troll")));
            Assert.Contains("troll", SelfTestWords.NothingToRun(SelfTestCommand.Of("selftest troll")));
            Assert.Contains("nothing failed", SelfTestWords.NothingToRun(SelfTestCommand.Of("selftest failed")));
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
        public void TheHeadlineSaysHowLongTheRunTook()
        {
            Assert.Equal("Self-test done in 42 s: 2 of 4 parts passed, 1 failed, 1 skipped.", SelfTestWords.Finished(Run(), 42.4));
            Assert.Equal("Self-test done in 12 min 5 s: all 1 parts passed.", SelfTestWords.Finished(new List<ScenarioReport> { Report("a", Result.Pass) }, 725));
            Assert.Equal("Self-test done in 3 min: all 1 parts passed.", SelfTestWords.Finished(new List<ScenarioReport> { Report("a", Result.Pass) }, 180.2));
            Assert.Equal("59 s", SelfTestWords.Duration(59.4));
            Assert.Equal("1 min", SelfTestWords.Duration(59.5));
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

        [Fact]
        public void HelpSaysWhatEachRunTakesAndAboutHowLong()
        {
            var help = SelfTestWords.Help(new RunSize(160, 640), new RunSize(160, 1260), new RunSize(161, 1560), new RunSize(2, 40),
                new[] { "the plan box shows a dungeon", "rune stones are read" },
                new[] { ("rune", new RunSize(2, 30)), ("plan box", new RunSize(6, 170)) }, "BepInEx/Scry-selftest.log").Split('\n');
            Assert.Equal("self-test commands, each after /scry:", help[0]);
            Assert.Equal("  selftest: a quick run, the long parts through a few of their places; 160 parts, about 11 min.", help[1]);
            Assert.Equal("  selftest deep: every part through every place it names; 160 parts, about 21 min.", help[2]);
            Assert.Equal("  selftest full: also reads every location again; 161 parts, about 26 min.", help[3]);
            Assert.Equal("  selftest failed: the 2 parts that failed last, less than a minute: the plan box shows a dungeon; rune stones are read.", help[4]);
            Assert.Equal("  selftest and any words: the parts whose names hold every word, such as selftest rune (2 parts, less than a minute) or selftest plan box (6 parts, about 3 min).", help[5]);
            Assert.Equal("  selftest list: writes every part's name, with how long it took last, to BepInEx/Scry-selftest.log.", help[6]);
            Assert.Equal("  selftest stop: stops a run.", help[7]);
        }

        [Fact]
        public void HelpSaysWhenNothingFailedAndNamesNoMoreThanFiveFailed()
        {
            var none = SelfTestWords.Help(new RunSize(1, 1), new RunSize(1, 1), new RunSize(1, 1), new RunSize(0, 0), new string[0], new (string, RunSize)[0], "f.log");
            Assert.Contains("  selftest failed: nothing failed in the last run.", none.Split('\n'));
            Assert.DoesNotContain("selftest and any words: the parts whose names hold every word, such as", none);

            var many = SelfTestWords.Help(new RunSize(1, 1), new RunSize(1, 1), new RunSize(1, 1), new RunSize(7, 90),
                new[] { "a", "b", "c", "d", "e", "f", "g" }, new (string, RunSize)[0], "f.log");
            Assert.Contains("  selftest failed: the 7 parts that failed last, about 2 min: a; b; c; d; e; and 2 more.", many.Split('\n'));
        }
    }
}
