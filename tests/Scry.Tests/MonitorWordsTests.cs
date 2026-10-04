using Xunit;

namespace Scry.Tests
{
    public class MonitorWordsTests
    {
        // The monitor's figures: milliseconds to two places under ten, one above; memory in
        // bytes, kilobytes, megabytes or gigabytes, whichever reads best; and its lines: the frame
        // time, where it went, what is allocated, what Scry holds and has made, and the game's
        // memory.

        [Fact]
        public void MillisecondsAreToTwoPlacesUnderTenAndOneAbove()
        {
            Assert.Equal("0.42", MonitorWords.Ms(0.4213));
            Assert.Equal("0.00", MonitorWords.Ms(0));
            Assert.Equal("9.99", MonitorWords.Ms(9.99));
            Assert.Equal("12.3", MonitorWords.Ms(12.34));
        }

        [Fact]
        public void MemoryReadsInTheUnitThatFits()
        {
            Assert.Equal("512 B", MonitorWords.Bytes(512));
            Assert.Equal("14 KB", MonitorWords.Bytes(14 * 1024));
            Assert.Equal("6.2 MB", MonitorWords.Bytes((long)(6.2 * 1024 * 1024)));
            Assert.Equal("3.4 GB", MonitorWords.Bytes((long)(3.4 * 1024 * 1024 * 1024)));
            Assert.Equal("0 B", MonitorWords.Bytes(-5));
        }

        [Fact]
        public void TheFrameLineSaysNowAverageMostAndShare()
        {
            Assert.Equal("1.25 ms a frame now, 0.80 on average, 14.2 at the most; 3.5% of the frames", MonitorWords.Frame(1.25, 0.8, 14.2, 0.035));
        }

        [Fact]
        public void WhereTheTimeWentIsListedPartByPart()
        {
            Assert.Equal("Panel 0.40", MonitorWords.Part("Panel", 0.4));
            Assert.Equal("Panel 0.40 · Stage 1.10", MonitorWords.Parts(new[] { "Panel 0.40", "Stage 1.10" }));
            Assert.Equal("nothing measured", MonitorWords.Parts(new string[0]));
        }

        [Fact]
        public void AllocationSaysHowMuchAndHowManyCleanups()
        {
            Assert.Equal("allocates 14 KB a second; 1 memory cleanups in its work, 1,200 in all in 10 s", MonitorWords.Allocates(14 * 1024, 1, 1200, 9.6));
        }

        [Fact]
        public void WhatScryHoldsAndMadeAndTheGamesMemoryAreCounted()
        {
            Assert.Equal("holds 5,000 entries, 2 bundles loaded; on the stage 40 parts, 1 creatures, 1,500 grass; previews in the world",
                MonitorWords.Holds(5000, 2, 40, 1, 1500, inWorld: true));
            Assert.EndsWith("grass; nothing in the world", MonitorWords.Holds(0, 0, 0, 0, 0, inWorld: false));
            Assert.Equal("made 3 textures 14 KB, 1 render textures 512 B, 2 meshes 6.2 MB",
                MonitorWords.Made(3, 14 * 1024, 1, 512, 2, (long)(6.2 * 1024 * 1024)));
            Assert.Equal("the game's managed memory 512 B, native 14 KB", MonitorWords.Game(512, 14 * 1024));
        }
    }
}
