using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class FrameStatsTests
    {
        // The self-test measures Scry's own work in every frame it runs, and tells how it went:
        // the average, the 95th percentile, the most, and the slowest frames with what took longest.

        private static FrameStats OneToAHundred()
        {
            var stats = new FrameStats();
            for (var ms = 1; ms <= 100; ms++) stats.Add(ms, "part " + ms, ms / 2.0);
            return stats;
        }

        [Fact]
        public void ItTellsTheAverageThe95thPercentileAndTheMost()
        {
            var stats = OneToAHundred();

            Assert.Equal(100, stats.Frames);
            Assert.Equal(50.5, stats.Mean, 6);
            Assert.Equal(95.0, stats.Percentile(0.95), 6);
            Assert.Equal(100.0, stats.Max, 6);

            // Nearest rank: of ten frames, the tenth is the 95th percentile.
            var ten = new FrameStats();
            for (var ms = 1; ms <= 10; ms++) ten.Add(ms, "", 0);
            Assert.Equal(10.0, ten.Percentile(0.95), 6);
        }

        [Fact]
        public void ItCountsTheFramesAboveALine()
        {
            Assert.Equal(4, OneToAHundred().Over(96.0));
        }

        [Fact]
        public void TheSlowestFramesComeFirstWithWhatTookLongestInEach()
        {
            var slowest = OneToAHundred().Slowest(2);

            Assert.Equal(new[] { 100.0, 99.0 }, slowest.Select(s => s.Ms).ToArray());
            Assert.Equal("part 100", slowest[0].Part);
            Assert.Equal(50.0, slowest[0].PartMs, 6);
        }

        [Fact]
        public void ItReadsAsOneLine()
        {
            Assert.Equal("100 frames: Scry's own work 50.5 ms on average, 95 ms at the 95th percentile, 100 ms at the most; 84 frames over 16 ms",
                OneToAHundred().Line(16.0));
            Assert.Equal("no frames measured", new FrameStats().Line(16.0));
        }

        [Fact]
        public void ThePartSinceAFrameIsToldOnItsOwn()
        {
            // Each scenario tells the frames it ran in, out of the whole run's.
            var since = OneToAHundred().Since(90);

            Assert.Equal(10, since.Frames);
            Assert.Equal(95.5, since.Mean, 6);
            Assert.Equal("part 100", since.Slowest(1)[0].Part);
            Assert.Equal(0, OneToAHundred().Since(100).Frames);
        }

        [Fact]
        public void ClearingStartsAgain()
        {
            var stats = OneToAHundred();
            stats.Clear();

            Assert.Equal(0, stats.Frames);
            Assert.Empty(stats.Slowest(3));
        }

        // FrameTimes: a part named with a space runs inside a one-word part, so it says more about
        // what was slow; the slowest of those is told, else the slowest one-word part.

        [Fact]
        public void AFramesSlowestPartIsTheSlowestInnerOne()
        {
            var frame = new FrameTimes();
            frame.Add("panel", 3, 0, 0);
            frame.Add("panel list", 2, 0, 0);
            frame.Add("update", 12, 0, 0);
            frame.Add("update catalog", 11, 0, 0);

            Assert.Equal(("update catalog", 11.0), frame.Slowest);
        }

        [Fact]
        public void ASlowFrameSaysWhereItsTimeWentAndWhetherAMemoryCleanupRanInIt()
        {
            // The slowest inner part may be small beside the frame: the rest is in an outer part,
            // in no inner part named, which a memory cleanup in it may explain.
            var stats = new FrameStats();
            stats.Add(386.6, "facts item", 7.6, "update", 1391.25, 1);
            stats.Add(40, "panel list", 30, "panel", 35, 0);
            stats.Add(30, "facts item", 20, "update", 25, 2);

            var slowest = stats.Slowest(3);
            Assert.Equal("a slow frame: 386.6 ms, the slowest part facts item 7.6 ms, in update 1,391.3 ms, a memory cleanup ran in it", FrameStats.Told(slowest[0]));
            Assert.Equal("a slow frame: 40 ms, the slowest part panel list 30 ms, in panel 35 ms", FrameStats.Told(slowest[1]));
            Assert.Equal("a slow frame: 30 ms, the slowest part facts item 20 ms, in update 25 ms, 2 memory cleanups ran in it", FrameStats.Told(slowest[2]));
        }

        [Fact]
        public void AFrameTellsItsSlowestOuterPart()
        {
            var frame = new FrameTimes();
            frame.Add("update", 12, 0, 0);
            frame.Add("update catalog", 11, 0, 0);
            frame.Add("render", 5, 0, 0);

            Assert.Equal(("update", 12.0), frame.SlowestOuter);
            frame.Clear();
            Assert.Equal(("", 0.0), frame.SlowestOuter);

            // An inner part, however long, is no outer part.
            frame.Add("render", 5, 0, 0);
            frame.Add("panel list", 8, 0, 0);
            Assert.Equal(("render", 5.0), frame.SlowestOuter);
        }

        [Fact]
        public void WithoutInnerPartsTheSlowestOuterOneIsTold()
        {
            var frame = new FrameTimes();
            frame.Add("update", 2, 0, 0);
            frame.Add("render", 5, 0, 0);

            Assert.Equal(("render", 5.0), frame.Slowest);
            frame.Clear();
            Assert.Equal(("", 0.0), frame.Slowest);
        }
    }
}
