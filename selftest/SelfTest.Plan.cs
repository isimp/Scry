using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The plan a session writes (<see cref="SelfTestPlan"/>, <c>BepInEx/Scry-selftest.plan</c>) and
    /// each part's last outcome the self-test writes back (<see cref="SelfTestResults"/>,
    /// <c>BepInEx/Scry-selftest.result</c>): the runs of what the last run left are chosen from
    /// them, a plan asking for it starts once the world is ready, and after every run the outcomes
    /// are kept and the parts that passed come off the plan, which goes once nothing is left.
    /// </summary>
    internal static partial class SelfTest
    {
        public static string PlanFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-selftest.plan");
        public static string ResultsFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-selftest.result");

        /// <summary>Every part's name, in the order a run takes them.</summary>
        private static List<string> PartNames() => _partNames ?? (_partNames = Scenarios().Select(s => s.Name).ToList());

        /// <summary>Every part's name, read once: the list does not change while the game runs.</summary>
        private static List<string> _partNames;

        /// <summary>The parts a run of what the last run left takes: the plan's, those with rows untried (all, or those whose need is near now), or those with checks not judged.</summary>
        private static ICollection<string> Listed(RunScope scope)
        {
            var results = SelfTestResults.Load(Kept(ResultsFile));
            switch (scope)
            {
                case RunScope.Plan:
                    return new HashSet<string>(SelfTestPlan.Load(Kept(PlanFile)).Parts(PartNames()));
                case RunScope.Untried:
                    return new HashSet<string>(results.Untried().Select(u => u.Part));
                case RunScope.Near:
                    var near = NeedsNear();
                    return new HashSet<string>(results.Untried().Where(u => u.Need != null && near.Contains(u.Need)).Select(u => u.Part));
                case RunScope.Busy:
                    return new HashSet<string>(results.WithBusy());
                default:
                    return null;
            }
        }

        /// <summary>What the plan asks for that no part matches, written to the log as a plan run starts.</summary>
        private static void PlanUnmatched()
        {
            var unmatched = SelfTestPlan.Load(Kept(PlanFile)).Unmatched(PartNames());
            if (unmatched.Count > 0) Write("The plan asks for what no part's name matches: " + string.Join("; ", unmatched));
        }

        /// <summary>Whether a plan asks to start once the world is ready.</summary>
        private static bool PlanOnLoad()
        {
            var plan = SelfTestPlan.Load(Kept(PlanFile));
            return plan.OnLoad && !plan.Done;
        }

        /// <summary>After a run: every part's outcome kept for the next, and the parts that passed taken off the plan, which goes once nothing is left.</summary>
        private static void KeepOutcomes(IReadOnlyList<ScenarioReport> reports)
        {
            var kept = SelfTestResults.Merge(Kept(ResultsFile), reports);
            Steps.Run(() => File.WriteAllText(ResultsFile, kept), null);
            var planText = Kept(PlanFile);
            if (planText == null) return;
            var left = SelfTestPlan.Load(planText).After(PartNames(), reports.Select(r => (r.Name, r.Result)));
            Steps.Run(() =>
            {
                if (left.Done) File.Delete(PlanFile);
                else File.WriteAllText(PlanFile, left.Save());
            }, null);
        }
    }
}
