using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// While the self-test runs, the strip shows it live: one segment a part coloured by how it
    /// went, and each new failure, untried row or check not judged for a moment in its line. A
    /// pause holds the run between parts, and the card then shows a tile a part by area with a
    /// feed of what needs a look, newest first.
    /// </summary>
    public class SelfTestLiveTests
    {
        private readonly List<string> _log = new List<string>();

        private static IEnumerator Lines(Probe p, params string[] lines)
        {
            foreach (var line in lines)
            {
                if (line == "fail") p.Check(false, "it failed", "1 ms");
                else if (line == "untried") p.Untried("a beehive", "stand near a beehive", "beehive");
                else if (line == "busy") p.Within(126, 50, "searches are quick", "126 ms");
                else p.Check(true, "it held");
                yield return new Wait(1);
            }
        }

        private static List<Scenario> Parts() => new List<Scenario>
        {
            new Scenario("opens", p => Lines(p, "ok")) { Area = "Panel" },
            new Scenario("watched", p => Lines(p, "fail")) { Area = "Compendium" },
            new Scenario("hive", p => Lines(p, "untried")) { Area = "Learning" },
            new Scenario("search", p => Lines(p, "busy", "ok")) { Area = "Panel" },
            new Scenario("later", p => Lines(p, "ok")) { Area = "Cost" },
        };

        [Fact]
        public void EachPartIsATileByAreaColouredByHowItWentTheRunningOneLitTheRestToCome()
        {
            var runner = new ScenarioRunner(Parts(), _log.Add, slowdown: () => 2.0);
            for (double t = 0; t < 5.0; t += 0.5) runner.Tick(t);
            var live = SelfTestLive.Of(runner);
            Assert.Equal(new[] { "Panel", "Compendium", "Learning", "Cost" }, live.Areas.Select(a => a.Area));
            Assert.Equal(new[] { TileState.Pass, TileState.Running }, live.Areas[0].Tiles.Select(t => t.State));
            Assert.Equal(TileState.Fail, live.Areas[1].Tiles[0].State);
            Assert.Equal(TileState.Untried, live.Areas[2].Tiles[0].State);
            Assert.Equal(TileState.ToCome, live.Areas[3].Tiles[0].State);
            Assert.Equal(new[] { "opens", "watched", "hive", "search", "later" }, live.InRunOrder.Select(t => t.Name));
        }

        [Fact]
        public void TheFeedHoldsWhatNeedsALookNewestFirstTheRunningPartsToo()
        {
            var runner = new ScenarioRunner(Parts(), _log.Add, slowdown: () => 2.0);
            for (double t = 0; t < 5.0; t += 0.5) runner.Tick(t);
            var feed = SelfTestLive.Of(runner).Feed;
            Assert.Equal(new[] { FeedKind.Busy, FeedKind.Untried, FeedKind.Failed }, feed.Select(f => f.Kind));
            Assert.Equal("search", feed[0].Part);
            Assert.StartsWith("searches are quick: 126 ms", feed[0].Text);
            Assert.Equal("a beehive: stand near a beehive", feed[1].Text);
            Assert.Equal("it failed: 1 ms", feed[2].Text);
        }

        [Fact]
        public void APauseHoldsTheRunBetweenPartsUntilItGoesOn()
        {
            var runner = new ScenarioRunner(Parts(), _log.Add);
            runner.Tick(0);
            runner.Pause();
            for (double t = 0.5; t < 30; t += 0.5) runner.Tick(t);
            Assert.True(runner.Paused);
            Assert.Equal(1, runner.Done);
            Assert.Null(runner.Current);
            runner.GoOn();
            for (double t = 30; t < 60; t += 0.5) runner.Tick(t);
            Assert.True(runner.Finished);
            Assert.Equal(5, runner.Done);
        }

        [Fact]
        public void APausedPartsTimeIsNotCountedAgainstTheRun()
        {
            var runner = new ScenarioRunner(Parts(), _log.Add);
            runner.Tick(0);
            runner.Pause();
            for (double t = 0.5; t < 100; t += 0.5) runner.Tick(t);
            runner.GoOn();
            for (double t = 100; t < 120; t += 0.5) runner.Tick(t);
            Assert.True(runner.Seconds < 20, $"{runner.Seconds} s");
        }

        [Fact]
        public void TheStripSaysHowFarItHasGotHowLongIsLeftAndWhatJustNeededALook()
        {
            Assert.Equal("96 / 202 · about 11 min left · idling costs nothing · failed: the compendium watches fights", SelfTestWords.Running(95, 202, 660, "idling costs nothing", "failed: the compendium watches fights", 2.1, paused: false));
            Assert.Equal("96 / 202 · about 11 min left · idling costs nothing · machine 2.1× slower than its best", SelfTestWords.Running(95, 202, 660, "idling costs nothing", null, 2.1, paused: false));
            Assert.Equal("3 / 5 · less than a minute left · idling costs nothing", SelfTestWords.Running(2, 5, 20, "idling costs nothing", null, 1.0, paused: false));
            Assert.Equal("Paused after part 96 of 202, about 11 min left: move as you like, then Go on.", SelfTestWords.Running(96, 202, 660, null, null, 1.0, paused: true));
        }

        [Fact]
        public void ThePartRunningHasAClockOfItsOwnAgainstItsLastTime()
        {
            Assert.Equal("3 of ~5 s", SelfTestWords.PartClock(3.2, 5.1));
            Assert.Equal("12 s", SelfTestWords.PartClock(12.4, null));
            Assert.Equal("1 min 5 s of ~2 min", SelfTestWords.PartClock(65, 118));
        }

        [Fact]
        public void ChatSaysOnceWhatNearLetsUntriedRowsBeTriedAndWhenTheMachineIsQuietAgain()
        {
            Assert.Equal("Scry self-test: an altar is near: 1 untried row can be tried here. Type /scry selftest near to try it.", SelfTestWords.NearChat("an altar", 1));
            Assert.Equal("Scry self-test: a beehive is near: 2 untried rows can be tried here. Type /scry selftest near to try them.", SelfTestWords.NearChat("a beehive", 2));
            Assert.Equal("Scry self-test: the machine runs near its best again: /scry selftest busy judges the 8 checks it could not.", SelfTestWords.QuietChat(8));
            Assert.Equal("Scry self-test: the machine runs near its best again: /scry selftest busy judges the 1 check it could not.", SelfTestWords.QuietChat(1));
        }

        [Fact]
        public void ANewEntryOfTheFeedStandsInTheStripForAFewSeconds()
        {
            Assert.Equal("failed: it failed: 1 ms", SelfTestLive.Latest(new FeedEntry(FeedKind.Failed, "watched", "it failed: 1 ms", 10.0), 12.0));
            Assert.Equal("untried: a beehive", SelfTestLive.Latest(new FeedEntry(FeedKind.Untried, "hive", "a beehive", 10.0), 14.0));
            Assert.Null(SelfTestLive.Latest(new FeedEntry(FeedKind.Busy, "search", "x", 10.0), 10.0 + SelfTestLive.ShownFor + 0.1));
            Assert.Null(SelfTestLive.Latest(null, 3.0));
        }
    }
}
