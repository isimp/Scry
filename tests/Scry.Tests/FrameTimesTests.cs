using System.Collections.Generic;
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
        public void TheFramesAllocationAndCleanupsAreThoseOfItsOuterParts()
        {
            var frame = new FrameTimes();
            frame.Add("panel", 30, 2048, 1);
            frame.Add("panel list", 29, 1024, 1);
            frame.Add("update", 1, 4096, 0);

            Assert.Equal(6144, frame.Bytes);
            Assert.Equal(1, frame.Cleanups);
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
        public void TheSelfTestsOwnChecksCanBeLeftOutOfTheFrame()
        {
            // The self-test runs inside Scry's update; its own checks (reading every entry's
            // details at once) are not work a player's frame does, so its share is taken out.
            var frame = new FrameTimes();
            frame.Add("update", 700, 0, 0);
            frame.Add("update self-test", 640, 0, 0);
            frame.Add("update previews", 30, 0, 0);
            frame.Add("selection copy", 20, 0, 0);
            frame.Add("panel", 20, 0, 0);

            Assert.Equal(720, frame.Total, 3);
            Assert.Equal(80, frame.TotalWithout("update self-test"), 3);
            Assert.Equal(("update previews", 30.0), frame.SlowestWithout("update self-test"));
            Assert.Equal(("update self-test", 640.0), frame.Slowest);
            Assert.Equal(640, frame.MsOf("update self-test"), 3);
            Assert.Equal(0, frame.MsOf("nothing"), 3);
            Assert.Equal(690, frame.InnerMs, 3);
        }

        [Fact]
        public void WhatTheSelfTestAllocatesInsideAnOuterPartIsLeftOutOfScrysOwn()
        {
            // The self-test steps inside the frame's update: what its own checks allocate is no part
            // of what Scry costs a player, as its time is not.
            var frame = new FrameTimes();
            frame.Add("update", 5, 1500, 0);
            frame.Add("update self-test", 1, 1200, 0);
            frame.Add("panel", 2, 300, 0);

            Assert.Equal(1800, frame.Bytes);
            Assert.Equal(600, frame.BytesWithout("update self-test"));
            // An outer part or one not there is not taken out, and none goes below nothing.
            Assert.Equal(1800, frame.BytesWithout("panel"));
            Assert.Equal(1800, frame.BytesWithout("update nothing"));
            frame.Add("update self-test", 0, 1000, 0);
            Assert.Equal(0, frame.BytesWithout("update self-test"));
        }

        [Fact]
        public void LeavingOutAnOuterPartOrOneNotThereChangesNothing()
        {
            var frame = new FrameTimes();
            frame.Add("update", 30, 0, 0);
            frame.Add("panel", 20, 0, 0);

            Assert.Equal(50, frame.TotalWithout("panel"), 3);
            Assert.Equal(50, frame.TotalWithout("update self-test"), 3);
        }

        [Fact]
        public void WhatEachPartAllocatesAddsUpFrameByFrameToTellWhichAllocates()
        {
            // Idling, Scry's allocation is told by part, inner ones too, so a run says which part it is.
            var byPart = new Dictionary<string, long>();
            var frame = new FrameTimes();
            frame.Add("update", 1, 600, 0);
            frame.Add("update probe", 1, 400, 0);
            frame.Add("render", 1, 0, 0);
            frame.AddBytesTo(byPart);
            frame.Clear();
            frame.Add("update", 1, 300, 0);
            frame.AddBytesTo(byPart);

            Assert.Equal(900, byPart["update"]);
            Assert.Equal(400, byPart["update probe"]);
            // A part that allocated nothing is not counted.
            Assert.False(byPart.ContainsKey("render"));
        }

        [Fact]
        public void ReadingAFramesTotalsAllocatesNothingSoTheMeasuringIsNotTakenForScrysOwn()
        {
            // The measuring reads a frame's totals several times a frame, inside the update it
            // measures, so whatever reading them allocated would be counted as Scry's own.
            var frame = new FrameTimes();
            frame.Add("update", 1, 10, 0);
            frame.Add("update probe", 1, 5, 0);
            frame.Add("update self-test", 1, 5, 0);
            double Read() => frame.Total + frame.Bytes + frame.Cleanups + frame.InnerMs + frame.BytesWithout("update self-test") + frame.TotalWithout("update self-test")
                             + frame.SlowestWithout("update self-test").Ms + frame.SlowestOuter.Ms;
            Read();
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            var sum = 0.0;
            for (var i = 0; i < 100; i++) sum += Read();
            Assert.Equal(0, System.GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.True(sum > 0);
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
