using Xunit;

namespace Scry.Tests
{
    public class FrameTimesTests
    {
        [Fact]
        public void AFrameCountsOnlyItsOuterPartsSoAPartInsideAnotherIsNotCountedTwice()
        {
            var frame = new FrameTimes();
            frame.Add("update", 10, 0, 0);
            frame.Add("update catalog", 9, 0, 0);
            frame.Add("panel", 5, 0, 0);
            frame.Add("panel list", 4, 0, 0);

            Assert.Equal(15, frame.Total, 3);
            Assert.StartsWith("Scry took 15 ms of a 40 ms frame: ", frame.Line(40));
        }

        [Fact]
        public void PartsAreListedSlowestFirstAndThoseThatTookNothingAreLeftOut()
        {
            var frame = new FrameTimes();
            frame.Add("panel", 12, 0, 0);
            frame.Add("side title", 0.2, 0, 0);
            frame.Add("panel list", 11, 0, 0);
            frame.Add("side facts", 0.4, 0, 0);

            Assert.Equal("Scry took 12 ms of a 30 ms frame: panel 12, panel list 11.", frame.Line(30));
        }

        [Fact]
        public void TimeAddedToAPartTwiceInAFrameIsSummed()
        {
            var frame = new FrameTimes();
            frame.Add("panel", 3, 0, 0);
            frame.Add("panel", 4, 0, 0);

            Assert.Equal(7, frame.Total, 3);
        }

        [Fact]
        public void AMemoryCleanupIsToldOnThePartItRanInAndCountedForTheFrame()
        {
            // A cleanup pauses whatever runs; told on its part, a slow part is not taken for slow code.
            var frame = new FrameTimes();
            frame.Add("panel", 31, 0, 1);
            frame.Add("panel list", 30, 0, 1);
            frame.Add("update", 1, 0, 0);

            Assert.Equal("Scry took 32 ms of a 50 ms frame, a memory cleanup ran inside it: panel 31 [memory cleanup], panel list 30 [memory cleanup], update 1.",
                frame.Line(50));
        }

        [Fact]
        public void WhatAPartAllocatedIsToldInKilobytesAndTheFrameSumsItsOuterParts()
        {
            var frame = new FrameTimes();
            frame.Add("panel", 2, 300 * 1024, 0);
            frame.Add("panel list", 1, 200 * 1024, 0);
            frame.Add("update", 1, 100 * 1024, 0);
            frame.Add("render", 1, 500, 0);

            Assert.Equal("Scry took 4 ms of a 20 ms frame, allocating about 400 KB: panel 2 [300 KB], panel list 1 [200 KB], update 1 [100 KB], render 1.",
                frame.Line(20));
        }

        [Fact]
        public void APartThatTookNoTimeButAllocatedMuchIsStillTold()
        {
            var frame = new FrameTimes();
            frame.Add("panel", 25, 0, 0);
            frame.Add("side links", 0.1, 64 * 1024, 0);

            Assert.Contains("side links 0 [64 KB]", frame.Line(40));
        }

        [Fact]
        public void TwoCleanupsInAFrameAreCountedAsTwo()
        {
            var frame = new FrameTimes();
            frame.Add("update", 30, 0, 1);
            frame.Add("panel", 30, 0, 1);

            Assert.Contains("frame, 2 memory cleanups ran inside it:", frame.Line(70));
        }

        [Fact]
        public void ClearingStartsTheNextFrameEmpty()
        {
            var frame = new FrameTimes();
            frame.Add("panel", 30, 1024, 1);

            frame.Clear();

            Assert.Equal(0, frame.Total, 3);
            Assert.DoesNotContain("panel", frame.Line(16));
            Assert.DoesNotContain("cleanup", frame.Line(16));
        }
    }
}
