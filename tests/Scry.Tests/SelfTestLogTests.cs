using Xunit;

namespace Scry.Tests
{
    public class SelfTestLogTests
    {
        // The self-test's log keeps its last runs, so whoever reads it for the latest result does
        // not wade through months of runs; the runs before them go to the older log.

        private static string Run(int n) => $"\n==== Scry 0.3.1 quick self-test, run {n} ====\nSTART a part\n==== Summary ====\nDONE {n}\n";

        [Fact]
        public void TheLogKeepsItsLastRunsAndHandsTheRestToTheOlderLog()
        {
            var text = Run(1) + Run(2) + Run(3) + Run(4);
            var (older, kept) = SelfTestLog.Split(text, runs: 2);
            Assert.Equal(Run(1) + Run(2), older);
            Assert.Equal(Run(3) + Run(4), kept);
            // Nothing is lost or doubled.
            Assert.Equal(text, older + kept);
        }

        [Fact]
        public void ALogOfNoMoreRunsThanAreKeptStaysWhole()
        {
            var text = Run(1) + Run(2);
            Assert.Equal(("", text), SelfTestLog.Split(text, runs: 2));
            Assert.Equal(("", ""), SelfTestLog.Split("", runs: 2));
            Assert.Equal(("", ""), SelfTestLog.Split(null, runs: 2));
        }

        [Fact]
        public void ARunsSummaryIsNoRunOfItsOwn()
        {
            // "==== Summary ====" closes a run; only "==== Scry " opens one.
            var text = Run(1) + Run(2) + Run(3);
            Assert.Equal(Run(1), SelfTestLog.Split(text, runs: 2).Older);
        }

        [Fact]
        public void TheLogIsSplitOnlyOnceItGrowsPastFourMegabytesKeepingFiveRuns()
        {
            Assert.Equal(4L * 1024 * 1024, SelfTestLog.KeptBytes);
            Assert.Equal(5, SelfTestLog.RunsKept);
        }
    }
}
