using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// The self-test judges fairly: a time or memory budget missed while the machine runs well
    /// below its best pace is not judged rather than failed, a part stuck only as long as the
    /// machine is slow gets that long, a row that had nothing near to try is counted as untried
    /// rather than hidden among notes, and every patch Scry registers is checked in place.
    /// </summary>
    public class SelfTestJudgingTests
    {
        private readonly List<string> _log = new List<string>();

        private static void RunToEnd(ScenarioRunner runner, double step = 0.1, double limit = 1000)
        {
            for (double t = 0; t < limit; t += step)
            {
                if (!runner.Tick(t)) return;
            }
        }

        private static IEnumerator Budget(Probe p, double value)
        {
            p.Within(value, 50, "every search takes under 50 ms", $"{value} ms");
            yield break;
        }

        [Fact]
        public void TheMachinesPaceIsItsReferenceWorkAgainstTheQuickestSeenKeptBetweenRuns()
        {
            var pace = new MachinePace();
            Assert.Equal(1.0, pace.Slowdown(4.0));
            Assert.Equal(2.5, pace.Slowdown(10.0));
            Assert.Equal(1.0, pace.Slowdown(2.0));
            var kept = MachinePace.Load(pace.Save());
            Assert.Equal(2.0, kept.Best);
            Assert.Equal(3.0, kept.Slowdown(6.0));
            Assert.Equal(0.0, MachinePace.Load("garbage").Best);
        }

        [Fact]
        public void APaceKeptForOtherReferenceWorkIsNotTaken()
        {
            // The quickest time of a shorter piece of work would make every sample of a longer one look slow.
            Assert.Equal(0.0, MachinePace.Load("best\t0.1517").Best);
            Assert.Equal(0.0, MachinePace.Load("best\t0.1517\t8").Best);
            Assert.Equal(1.2, MachinePace.Load("best\t1.2\t" + MachinePace.Work).Best, 3);
        }

        [Fact]
        public void ABudgetMissedOnABusyMachineIsNotJudgedButAGrossMissStillFails()
        {
            Assert.Equal(Verdict.Within, MachinePace.Judge(40, 50, 3.0));
            Assert.Equal(Verdict.Over, MachinePace.Judge(60, 50, 1.2));
            Assert.Equal(Verdict.Busy, MachinePace.Judge(126, 50, 2.0));
            Assert.Equal(Verdict.Over, MachinePace.Judge(500, 50, 2.0));
        }

        [Fact]
        public void ABusyMissIsWrittenAsNotJudgedAndLeavesThePartPassing()
        {
            var runner = new ScenarioRunner(new[] { new Scenario("search is quick", p => Budget(p, 126)) }, _log.Add, slowdown: () => 2.0);
            RunToEnd(runner);
            Assert.Equal(Result.Pass, runner.Reports[0].Result);
            Assert.Contains(_log, l => l.StartsWith("  BUSY every search takes under 50 ms: 126 ms", System.StringComparison.Ordinal) && l.Contains("2.0 times"));
        }

        [Fact]
        public void TheSameMissOnAQuietMachineFails()
        {
            var runner = new ScenarioRunner(new[] { new Scenario("search is quick", p => Budget(p, 126)) }, _log.Add, slowdown: () => 1.0);
            RunToEnd(runner);
            Assert.Equal(Result.Fail, runner.Reports[0].Result);
            Assert.Contains("  FAIL every search takes under 50 ms: 126 ms", _log);
        }

        private static IEnumerator Slow(Probe p)
        {
            yield return new Wait(25);
            p.Check(true, "it finished");
        }

        [Fact]
        public void APartGetsAsMuchLongerAsTheMachineIsSlowBeforeItTimesOut()
        {
            var busy = new ScenarioRunner(new[] { new Scenario("slow part", Slow) { Timeout = 20 } }, _log.Add, slowdown: () => 2.0);
            RunToEnd(busy);
            Assert.Equal(Result.Pass, busy.Reports[0].Result);
            var quiet = new ScenarioRunner(new[] { new Scenario("slow part", Slow) { Timeout = 20 } }, _log.Add, slowdown: () => 1.0);
            RunToEnd(quiet);
            Assert.Equal(Result.Fail, quiet.Reports[0].Result);
        }

        private static ScenarioReport Report(string name, Result result, params string[] lines)
        {
            var report = new ScenarioReport { Name = name, Result = result };
            report.Lines.AddRange(lines);
            return report;
        }

        [Fact]
        public void UntriedRowsAndChecksNotJudgedAreCountedInTheHeadlineAndListedInTheSummary()
        {
            var reports = new List<ScenarioReport>
            {
                Report("learning paths", Result.Pass, "  PASS twins share", "  untried a boss seen at its altar: stand near an altar", "  untried food cooking: stand by a lit cooking station"),
                Report("search is quick", Result.Pass, "  BUSY every search takes under 50 ms: 126 ms; this machine ran 2.0 times slower than its best"),
                Report("the panel opens", Result.Pass, "  PASS it opens"),
            };
            Assert.Equal("Self-test done: all 3 parts passed; 1 check not judged as the machine was busy, 2 rows untried.", SelfTestWords.Finished(reports));
            Assert.Equal(new[]
            {
                "Not judged, the machine busy (1):",
                "  search is quick",
                "    every search takes under 50 ms: 126 ms; this machine ran 2.0 times slower than its best",
                "Untried, nothing near to try them on (2):",
                "  learning paths",
                "    a boss seen at its altar: stand near an altar",
                "    food cooking: stand by a lit cooking station",
                "Passed: 3.",
            }, SelfTestWords.Summary(reports));
        }

        [Fact]
        public void ARowWithNothingNearIsWrittenAsUntriedWithWhereToStand()
        {
            var runner = new ScenarioRunner(new[] { new Scenario("altar", p => Untried(p)) }, _log.Add);
            RunToEnd(runner);
            Assert.Contains("  untried a boss seen at its altar: stand near an altar", _log);
            Assert.Equal(Result.Pass, runner.Reports[0].Result);
        }

        [Fact]
        public void APartWhoseOnlyRowHadNothingNearIsUntriedNotFailedAsCheckingNothing()
        {
            var runner = new ScenarioRunner(new[] { new Scenario("hive", p => OnlyUntried(p)) }, _log.Add);
            RunToEnd(runner);
            Assert.Equal(Result.Pass, runner.Reports[0].Result);
            Assert.DoesNotContain(_log, l => l.Contains("checked nothing"));
        }

        private static IEnumerator OnlyUntried(Probe p)
        {
            p.Untried("collecting from a beehive", "stand near a beehive");
            yield break;
        }

        private static IEnumerator Untried(Probe p)
        {
            p.Check(true, "something else held");
            p.Untried("a boss seen at its altar", "stand near an altar");
            yield break;
        }

        [Fact]
        public void EveryRegisteredPatchSitsOnAGameMethodSaveThoseOfAFeatureTheGameTurnedOff()
        {
            var registered = new[] { ("DeathLoot", false, false), ("AltarCalled", false, false), ("BrokenOne", true, false) };
            Assert.Equal(new[] { "AltarCalled" }, PatchesInPlace.Missing(registered, new HashSet<string> { "DeathLoot" }));
            Assert.Empty(PatchesInPlace.Missing(registered, new HashSet<string> { "DeathLoot", "AltarCalled" }));
        }

        [Fact]
        public void AClassThatOnlyCopiesAGameMethodsBodySitsOnNothingAndIsNotLookedFor()
        {
            // A reverse patch (UnmoddedTooltip) borrows the game's method as it was; it hooks nothing.
            var registered = new[] { ("DeathLoot", false, false), ("UnmoddedTooltip", false, true) };
            Assert.Empty(PatchesInPlace.Missing(registered, new HashSet<string> { "DeathLoot" }));
        }
    }
}
