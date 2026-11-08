using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// A session lists the parts a change needs in a plan file, by name or by words a part's name
    /// holds; the game runs them, writes every part's outcome where a session reads it, takes a
    /// part off the plan once it passes, and recommends what to run next: the plan, untried rows
    /// near the character, the parts that failed, and the checks not judged once the machine is
    /// quiet again.
    /// </summary>
    public class SelfTestPlanTests
    {
        private static readonly string[] All =
        {
            "the panel opens and draws",
            "learning paths teach what they should",
            "a creature's page is laid out by topic, its fight first",
            "the compendium watches fights, hits and sight without fault",
            "every boss tells its altar, and every trader what it sells",
        };

        private const string PlanText = "why\tA creature that drops nothing counts its death\npart\tlearning paths teach what they should\nwords\tcreature's page\npart\tno such part\nrun\ton-load\n";

        [Fact]
        public void APlanNamesPartsByNameOrByWordsInItsOrderAndSaysWhatMatchedNothing()
        {
            var plan = SelfTestPlan.Load(PlanText);
            Assert.Equal("A creature that drops nothing counts its death", plan.Why);
            Assert.True(plan.OnLoad);
            Assert.Equal(new[] { "learning paths teach what they should", "a creature's page is laid out by topic, its fight first" }, plan.Parts(All));
            Assert.Equal(new[] { "no such part" }, plan.Unmatched(All));
        }

        [Fact]
        public void APartThatPassedComesOffAndThePlanGoesOnceAllHave()
        {
            var plan = SelfTestPlan.Load(PlanText);
            var left = plan.After(All, new[] { ("learning paths teach what they should", Result.Pass), ("a creature's page is laid out by topic, its fight first", Result.Fail) });
            Assert.Equal(new[] { "a creature's page is laid out by topic, its fight first" }, left.Parts(All));
            Assert.Equal(plan.Why, left.Why);
            var none = left.After(All, new[] { ("a creature's page is laid out by topic, its fight first", Result.Pass) });
            Assert.True(none.Done);
            Assert.True(SelfTestPlan.Load("").Done);
        }

        [Fact]
        public void TheNewWordsRunThePlanTheUntriedNearYouEveryUntriedAndTheNotJudged()
        {
            Assert.Equal(RunScope.Plan, SelfTestCommand.Of("selftest plan").Scope);
            Assert.Equal(RunScope.Near, SelfTestCommand.Of("selftest near").Scope);
            Assert.Equal(RunScope.Untried, SelfTestCommand.Of("selftest untried").Scope);
            Assert.Equal(RunScope.Busy, SelfTestCommand.Of("selftest busy").Scope);
            var all = All.Select(n => new Scenario(n, p => Empty())).ToList();
            var listed = new Dictionary<RunScope, ICollection<string>> { [RunScope.Busy] = new HashSet<string> { All[3], All[0] } };
            Assert.Equal(new[] { All[0], All[3] }, Scenario.ForRun(all, SelfTestCommand.Of("selftest busy"), null, s => listed.TryGetValue(s, out var names) ? names : null).Select(s => s.Name));
        }

        [Fact]
        public void EachNewRunSaysWhatItTakesAndWhyThereIsNothingWhenThereIsNothing()
        {
            Assert.StartsWith("self-test of the parts planned for a change started", SelfTestWords.Started(3, RunScope.Plan, 30));
            Assert.StartsWith("self-test of the untried rows near you started", SelfTestWords.Started(1, RunScope.Near, 30));
            Assert.StartsWith("self-test of every part with untried rows started", SelfTestWords.Started(5, RunScope.Untried, 30));
            Assert.StartsWith("self-test of the checks not judged as the machine was busy started", SelfTestWords.Started(2, RunScope.Busy, 30));
            Assert.Equal("no plan asks for a part; a session writes BepInEx/Scry-selftest.plan.", SelfTestWords.NothingToRun(SelfTestCommand.Of("selftest plan")));
            Assert.Equal("nothing near you lets an untried row be tried; the last run's summary says where to stand for each.", SelfTestWords.NothingToRun(SelfTestCommand.Of("selftest near")));
            Assert.Equal("no row was untried in the last runs.", SelfTestWords.NothingToRun(SelfTestCommand.Of("selftest untried")));
            Assert.Equal("every check was judged in the last runs.", SelfTestWords.NothingToRun(SelfTestCommand.Of("selftest busy")));
            var help = SelfTestWords.Help(new RunSize(1, 1), new RunSize(1, 1), new RunSize(1, 1), new RunSize(0, 0), new List<string>(), new List<(string, RunSize)>(), "f.log");
            Assert.Contains("selftest plan:", help);
            Assert.Contains("selftest near:", help);
            Assert.Contains("selftest untried:", help);
            Assert.Contains("selftest busy:", help);
        }

        private static IEnumerator Empty()
        {
            yield break;
        }

        private static ScenarioReport Report(string name, Result result, params string[] lines)
        {
            var report = new ScenarioReport { Name = name, Result = result };
            report.Lines.AddRange(lines);
            return report;
        }

        /// <summary>A report whose untried rows each name what near the character would let them be tried.</summary>
        private static ScenarioReport Needing(ScenarioReport report, params (string Row, string Need)[] needs)
        {
            foreach (var (row, need) in needs) report.Needs[row] = need;
            return report;
        }

        [Fact]
        public void EachPartsLastOutcomeIsWrittenOneLineAPartKeptForPartsNotRunAgain()
        {
            var first = SelfTestResults.Merge(null, new[]
            {
                Report(All[0], Result.Pass, "  PASS it opens"),
                Needing(Report(All[1], Result.Pass, "  untried a boss seen at its altar: stand near a boss's altar", "  BUSY every search takes under 50 ms: 126 ms"), ("a boss seen at its altar: stand near a boss's altar", "altar")),
                Report(All[3], Result.Fail, "  FAIL OfferingBowl.InitiateSpawnBoss is watched"),
            });
            var results = SelfTestResults.Load(first);
            Assert.Equal(Result.Fail, results.Of(All[3]));
            Assert.Equal(new[] { All[1] }, results.WithBusy());
            Assert.Equal(new[] { (All[1], "altar", "a boss seen at its altar: stand near a boss's altar") }, results.Untried());
            Assert.Contains("fail\tOfferingBowl.InitiateSpawnBoss is watched", first);

            var second = SelfTestResults.Load(SelfTestResults.Merge(first, new[] { Report(All[3], Result.Pass, "  PASS fixed") }));
            Assert.Equal(Result.Pass, second.Of(All[3]));
            Assert.Equal(new[] { All[1] }, second.WithBusy());
            Assert.Equal(Result.Pass, second.Of(All[0]));
        }

        [Fact]
        public void APlanMayAskForTheDocsPicturesAndKeepsAskingUntilItIsDone()
        {
            var plan = SelfTestPlan.Load("why\tthe docs\npart\tone\npart\ttwo\nshots\tyes\n");
            Assert.True(plan.TakesShots);
            Assert.False(SelfTestPlan.Load("part\tone\n").TakesShots);
            Assert.False(SelfTestPlan.Load("part\tone\nshots\tno\n").TakesShots);
            // One passed and one failed: what is left, written again, still asks for them.
            var left = plan.After(new[] { "one", "two" }, new[] { ("one", Result.Pass), ("two", Result.Fail) });
            Assert.True(SelfTestPlan.Load(left.Save()).TakesShots);
            Assert.Equal(new[] { "two" }, SelfTestPlan.Load(left.Save()).Parts(new[] { "one", "two" }));
        }

        [Fact]
        public void TheRecommendedRunsArePlanNearFailedAndBusyOnceTheMachineIsQuiet()
        {
            var results = SelfTestResults.Load(SelfTestResults.Merge(null, new[]
            {
                Needing(Report(All[1], Result.Pass, "  untried a boss seen at its altar: stand near a boss's altar", "  untried collecting from a beehive: stand near a beehive"), ("a boss seen at its altar: stand near a boss's altar", "altar"), ("collecting from a beehive: stand near a beehive", "beehive")),
                Report(All[2], Result.Pass, "  BUSY every search takes under 50 ms: 126 ms", "  BUSY no frame waits: 300 ms"),
                Report(All[3], Result.Fail, "  FAIL watched"),
            }));
            var plan = SelfTestPlan.Load("part\t" + All[4]);
            var busy = SelfTestRecommended.Of(plan, All, results, new HashSet<string> { "altar" }, slowdown: 2.1);
            Assert.Equal(new[] { All[4] }, busy.Planned);
            Assert.Equal(new[] { "a boss seen at its altar: stand near a boss's altar" }, busy.Near.Select(n => n.Row));
            Assert.Equal(new[] { All[3] }, busy.Failed);
            Assert.Equal(2, busy.BusyChecks);
            Assert.False(busy.BusyReady);
            Assert.Equal(1, busy.UntriedElsewhere);
            Assert.Equal("Self-test: 1 part planned for a change, 1 untried row can be tried near you, 1 part failed last run, 2 checks wait for a quiet machine.", busy.Line);
            Assert.True(SelfTestRecommended.Of(plan, All, results, new HashSet<string>(), slowdown: 1.1).BusyReady);
            var nothing = SelfTestRecommended.Of(SelfTestPlan.Load(""), All, SelfTestResults.Load(""), new HashSet<string>(), 1.0);
            Assert.Null(nothing.Line);
        }
    }
}
