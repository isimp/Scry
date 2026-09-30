using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Scry;
using Xunit;

namespace Scry.Tests
{
    /// <summary>The in-game self-test runs its scenarios through this; it has to be trustworthy on its own.</summary>
    public class ScenarioRunnerTests
    {
        private readonly List<string> _log = new List<string>();

        private ScenarioRunner Runner(params Scenario[] scenarios) => new ScenarioRunner(scenarios, _log.Add);

        private static void RunToEnd(ScenarioRunner runner, double step = 0.1, double limit = 1000)
        {
            for (double t = 0; t < limit; t += step)
            {
                if (!runner.Tick(t)) return;
            }
            throw new Exception("runner never finished");
        }

        private static Scenario Passing(string name, List<string> order = null) => new Scenario(name, probe => Body(probe, name, order));

        private static IEnumerator Body(Probe probe, string name, List<string> order)
        {
            order?.Add(name);
            probe.Check(true, "it ran");
            yield break;
        }

        [Fact]
        public void ScenariosRunInOrderAndEachIsReported()
        {
            var order = new List<string>();
            var runner = Runner(Passing("a", order), Passing("b", order), Passing("c", order));
            RunToEnd(runner);

            Assert.Equal(new[] { "a", "b", "c" }, order);
            Assert.Equal(new[] { Result.Pass, Result.Pass, Result.Pass }, runner.Reports.Select(r => r.Result));
        }

        private static IEnumerator WaitsThenChecks(Probe probe, List<double> seen, Func<double> clock)
        {
            seen.Add(clock());
            yield return new Wait(2.0);
            seen.Add(clock());
            probe.Check(true, "waited");
        }

        [Fact]
        public void AWaitPausesTheScenarioUntilItsTimeHasPassed()
        {
            double now = 0;
            var seen = new List<double>();
            var runner = Runner(new Scenario("waiting", p => WaitsThenChecks(p, seen, () => now)));

            for (now = 0; now < 10 && runner.Tick(now); now += 0.5) { }

            Assert.Equal(2, seen.Count);
            Assert.True(seen[1] - seen[0] >= 2.0);
        }

        private static IEnumerator Fails(Probe probe)
        {
            probe.Check(false, "something that should hold", "it did not");
            yield break;
        }

        [Fact]
        public void AFailedCheckFailsItsScenarioAndTheRunGoesOn()
        {
            var runner = Runner(new Scenario("bad", Fails), Passing("good"));
            RunToEnd(runner);

            Assert.Equal(Result.Fail, runner.Reports[0].Result);
            Assert.Equal(Result.Pass, runner.Reports[1].Result);
            Assert.Contains(runner.Reports[0].Lines, l => l.Contains("FAIL") && l.Contains("it did not"));
        }

        private static IEnumerator Throws(Probe probe)
        {
            probe.Check(true, "before the throw");
            yield return null;
            throw new InvalidOperationException("boom");
        }

        [Fact]
        public void AScenarioThatThrowsFailsWithTheErrorAndTheNextStillRuns()
        {
            var runner = Runner(new Scenario("throws", Throws), Passing("after"));
            RunToEnd(runner);

            Assert.Equal(Result.Fail, runner.Reports[0].Result);
            Assert.Contains(runner.Reports[0].Lines, l => l.Contains("boom"));
            Assert.Equal(Result.Pass, runner.Reports[1].Result);
        }

        [Fact]
        public void CleanupRunsAfterEveryScenarioEvenOneThatFailed()
        {
            var cleaned = new List<string>();
            var runner = Runner(
                new Scenario("bad", Fails, () => cleaned.Add("bad")),
                new Scenario("throws", Throws, () => cleaned.Add("throws")),
                new Scenario("good", p => Body(p, "good", null), () => cleaned.Add("good")));
            RunToEnd(runner);

            Assert.Equal(new[] { "bad", "throws", "good" }, cleaned);
        }

        [Fact]
        public void ACleanupThatThrowsIsReportedAndDoesNotStopTheRun()
        {
            var runner = Runner(new Scenario("messy", p => Body(p, "messy", null), () => throw new Exception("cleanup broke")), Passing("next"));
            RunToEnd(runner);

            Assert.Equal(Result.Fail, runner.Reports[0].Result);
            Assert.Contains(runner.Reports[0].Lines, l => l.Contains("cleanup broke"));
            Assert.Equal(Result.Pass, runner.Reports[1].Result);
        }

        private static IEnumerator Forever(Probe probe)
        {
            probe.Check(true, "started");
            while (true) yield return null;
        }

        [Fact]
        public void AStuckScenarioTimesOutAndTheRunGoesOn()
        {
            var runner = Runner(new Scenario("stuck", Forever) { Timeout = 5 }, Passing("next"));
            RunToEnd(runner);

            Assert.Equal(Result.Fail, runner.Reports[0].Result);
            Assert.Contains(runner.Reports[0].Lines, l => l.Contains("timed out"));
            Assert.Equal(Result.Pass, runner.Reports[1].Result);
        }

        private static IEnumerator Skips(Probe probe)
        {
            probe.Skip("no creature nearby");
            probe.Check(false, "never reached");
            yield break;
        }

        [Fact]
        public void ASkippedScenarioStopsThereAndSaysWhy()
        {
            var runner = Runner(new Scenario("needs a creature", Skips), Passing("next"));
            RunToEnd(runner);

            Assert.Equal(Result.Skip, runner.Reports[0].Result);
            Assert.Contains(runner.Reports[0].Lines, l => l.Contains("no creature nearby"));
            Assert.DoesNotContain(runner.Reports[0].Lines, l => l.Contains("never reached"));
        }

        private static IEnumerator ChecksNothing(Probe probe)
        {
            yield break;
        }

        [Fact]
        public void AScenarioThatChecksNothingFails()
        {
            // A test that asserted nothing proves nothing; better to hear about it.
            var runner = Runner(new Scenario("empty", ChecksNothing));
            RunToEnd(runner);

            Assert.Equal(Result.Fail, runner.Reports[0].Result);
        }

        [Fact]
        public void TheFinishingStepRunsOnceAfterEverythingEvenAfterFailures()
        {
            var finished = 0;
            var runner = new ScenarioRunner(new[] { new Scenario("throws", Throws), Passing("good") }, _log.Add, () => finished++);
            RunToEnd(runner);
            runner.Tick(2000);

            Assert.Equal(1, finished);
        }

        [Fact]
        public void WhileItRunsItTellsHowFarItHasGotWhatRunsNowAndWhatFailedSoFar()
        {
            var runner = Runner(new Scenario("a", probe => Pausing(probe, true)), new Scenario("b", probe => Pausing(probe, false)), new Scenario("c", probe => Pausing(probe, true)));
            Assert.Equal((3, 0, (string)null, 0), (runner.Total, runner.Done, runner.Current, runner.FailedSoFar));

            runner.Tick(0);
            Assert.Equal(("a", 0), (runner.Current, runner.Done));
            runner.Tick(0.1);
            Assert.Equal(((string)null, 1), (runner.Current, runner.Done));
            runner.Tick(0.2);
            Assert.Equal("b", runner.Current);
            runner.Tick(0.3);
            Assert.Equal((2, 1), (runner.Done, runner.FailedSoFar));

            RunToEnd(runner);
            Assert.Equal((3, 3, (string)null, 1), (runner.Total, runner.Done, runner.Current, runner.FailedSoFar));
        }

        /// <summary>A scenario that checks, waits a tick, and ends.</summary>
        private static IEnumerator Pausing(Probe probe, bool holds)
        {
            probe.Check(holds, "it held");
            yield return null;
        }

        [Fact]
        public void TheSummaryCountsEachResult()
        {
            var runner = Runner(Passing("a"), new Scenario("b", Fails), new Scenario("c", Skips));
            RunToEnd(runner);

            Assert.Contains("1 passed, 1 failed, 1 skipped", runner.Summary);
            Assert.Contains(_log, l => l.Contains("1 passed, 1 failed, 1 skipped"));
        }

        private static IEnumerator WaitsTwoSeconds(Probe probe)
        {
            yield return new Wait(2);
            probe.Check(true, "it waited");
        }

        [Fact]
        public void EachPartsLastLineTellsHowLongItTook()
        {
            var runner = Runner(new Scenario("slow", WaitsTwoSeconds), new Scenario("bad", Fails));
            for (double t = 0; t < 100 && runner.Tick(t); t += 0.5) { }

            Assert.Contains("PASS slow (2.0 s)", _log);
            Assert.Contains("FAIL bad (0.0 s)", _log);
        }

        [Fact]
        public void TheLastLineTellsHowLongTheWholeRunTook()
        {
            var runner = Runner(new Scenario("slow", WaitsTwoSeconds), Passing("quick"));
            for (double t = 10; t < 100 && runner.Tick(t); t += 0.5) { }

            Assert.Equal(2.5, runner.Seconds, 3);
            Assert.Equal("DONE 2 passed, 0 failed, 0 skipped, in 3 s", _log.Last());
        }

        [Fact]
        public void EveryLineGoesToTheLogAsItHappens()
        {
            var runner = Runner(new Scenario("bad", Fails));
            RunToEnd(runner);

            Assert.Contains(_log, l => l.Contains("bad") && l.Contains("START"));
            Assert.Contains(_log, l => l.Contains("FAIL") && l.Contains("something that should hold"));
        }
    }
}
