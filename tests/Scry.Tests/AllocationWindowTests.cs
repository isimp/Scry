using Xunit;

namespace Scry.Tests
{
    public class AllocationWindowTests
    {
        private const long MB = 1024 * 1024;

        [Fact]
        public void WhatTheGameAllocatesIsTheGrowthOfMemoryInUseFromFrameToFrame()
        {
            var window = new AllocationWindow();
            window.Sample(100 * MB, 5);
            window.Sample(102 * MB, 5);
            window.Sample(105 * MB, 5);

            Assert.Equal(5 * MB, window.AllBytes);
        }

        [Fact]
        public void AFrameWithACleanupCountsTheCleanupButNotItsDropInMemory()
        {
            // Across a cleanup the memory in use falls, and what was allocated meanwhile is unknown.
            var window = new AllocationWindow();
            window.Sample(100 * MB, 5);
            window.Sample(40 * MB, 6);
            window.Sample(41 * MB, 6);

            Assert.Equal(1, window.Cleanups);
            Assert.Equal(1 * MB, window.AllBytes);
        }

        [Fact]
        public void GrowthAcrossACleanupIsNotTakenForWhatWasAllocated()
        {
            var window = new AllocationWindow();
            window.Sample(100 * MB, 5);
            window.Sample(120 * MB, 6);

            Assert.Equal(0, window.AllBytes);
        }

        [Fact]
        public void TheFirstSampleOnlySetsWhereCountingStarts()
        {
            var window = new AllocationWindow();
            window.Sample(100 * MB, 5);

            Assert.Equal(0, window.AllBytes);
            Assert.Equal(0, window.Cleanups);
        }

        [Fact]
        public void TheLineComparesScrysShareWithEverythingAndSaysWhichCleanupsRanInItsWork()
        {
            var window = new AllocationWindow();
            window.Sample(100 * MB, 0);
            window.Sample(130 * MB, 0);
            window.Sample(60 * MB, 2);
            window.AddScry(3 * MB, 1);

            Assert.Equal("In the last 30 s: 2 memory cleanups, 1 of them during Scry's work; Scry allocated about 3 MB of the 30 MB allocated in all (10%).",
                window.Line(30));
        }

        [Fact]
        public void ResettingStartsANewWindowFromTheLastSample()
        {
            var window = new AllocationWindow();
            window.Sample(100 * MB, 0);
            window.Sample(110 * MB, 1);
            window.AddScry(MB, 1);

            window.Reset();
            window.Sample(112 * MB, 1);

            Assert.Equal(2 * MB, window.AllBytes);
            Assert.Equal(0, window.Cleanups);
            Assert.Equal(0, window.ScryBytes);
        }

        [Fact]
        public void NothingAllocatedSaysNoShare()
        {
            var window = new AllocationWindow();

            Assert.Equal("In the last 30 s: 0 memory cleanups, 0 of them during Scry's work; Scry allocated about 0 MB of the 0 MB allocated in all.",
                window.Line(30));
        }
    }
}
