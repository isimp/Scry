using System;
using Xunit;

namespace Scry.Tests
{
    public class OtherThreadsTests
    {
        // The game's runtime keeps no count of what a thread allocates, so the self-test tells what
        // Scry allocates idling by the memory in use, which grows by every thread's allocations
        // made while Scry works. As long a wait as Scry's work took, doing nothing, tells how much
        // of that is other threads'.

        /// <summary>A clock that moves on a little each time it is read, with the memory in use and the cleanups run, and what happens as time passes.</summary>
        private sealed class World
        {
            public const double Tick = 0.01;
            public double Now;
            public long Memory = 100_000;
            public int Cleanups;
            public int MemoryReads;
            public Action<World> Passing;

            public double Clock()
            {
                Now += Tick;
                Passing?.Invoke(this);
                return Now;
            }

            public long ReadMemory()
            {
                MemoryReads++;
                return Memory;
            }

            public OtherThreads Watch() => new OtherThreads(ReadMemory, () => Cleanups, Clock);
        }

        /// <summary>Another thread taking a 4 KB block once, as soon as so many milliseconds have passed.</summary>
        private static Action<World> BlockTakenAfter(double ms)
        {
            var taken = false;
            return world =>
            {
                if (taken || world.Now < ms) return;
                taken = true;
                world.Memory += 4096;
            };
        }

        [Fact]
        public void WhatTheMemoryInUseGrewByOverTheWaitIsCounted()
        {
            var world = new World();
            var others = world.Watch();
            world.Passing = BlockTakenAfter(world.Now + 0.15);

            others.Wait(0.3);

            Assert.Equal(4096, others.Bytes);
        }

        [Fact]
        public void TheWaitLastsTheTimeGivenAndNoLonger()
        {
            var world = new World();
            var others = world.Watch();

            others.Wait(0.3);

            Assert.InRange(others.Ms, 0.3, 0.3 + 2 * World.Tick);
            Assert.InRange(world.Now, 0.3, 0.3 + 3 * World.Tick);
        }

        [Fact]
        public void WaitsAddUp()
        {
            var world = new World();
            var others = world.Watch();

            world.Passing = BlockTakenAfter(world.Now + 0.1);
            others.Wait(0.2);
            world.Passing = BlockTakenAfter(world.Now + 0.1);
            others.Wait(0.2);

            Assert.Equal(8192, others.Bytes);
            Assert.InRange(others.Ms, 0.4, 0.4 + 4 * World.Tick);
        }

        [Fact]
        public void AWaitAcrossAMemoryCleanupCountsNothing()
        {
            // A cleanup gives back what it frees, so the memory in use tells nothing of what was
            // allocated across it; Scry's own parts are not counted across one either.
            var world = new World();
            var others = world.Watch();
            var cleaned = false;
            var block = BlockTakenAfter(world.Now + 0.2);
            var another = BlockTakenAfter(world.Now + 0.25);
            world.Passing = w =>
            {
                if (!cleaned && w.Now >= 0.1)
                {
                    cleaned = true;
                    w.Cleanups++;
                    w.Memory -= 4096;
                }
                block(w);
                another(w);
            };

            others.Wait(0.3);

            Assert.Equal(0, others.Bytes);
            Assert.InRange(others.Ms, 0.3, 0.3 + 2 * World.Tick);
        }

        [Fact]
        public void MemoryGivenBackCountsNothingNotLessThanNothing()
        {
            // As Scry's own parts are counted, a wait in which the memory in use shrank adds none.
            var world = new World();
            var others = world.Watch();
            world.Passing = w => w.Memory -= 4096;
            others.Wait(0.1);
            Assert.Equal(0, others.Bytes);

            world.Passing = BlockTakenAfter(world.Now + 0.05);
            others.Wait(0.1);
            Assert.Equal(4096, others.Bytes);
        }

        [Fact]
        public void APartGivesUpOtherThreadsShareForTheTimeItRanAtTheRateTheWaitsSaw()
        {
            // The parts of Scry that run longest, as the render, would otherwise carry most of
            // what other threads allocated meanwhile, and read as Scry's own.
            var world = new World();
            var others = world.Watch();
            world.Passing = BlockTakenAfter(world.Now + 0.1);
            others.Wait(0.2);
            var rate = others.Bytes / others.Ms;

            Assert.Equal(10_000 - (long)Math.Round(rate * 0.1), others.Left(10_000, 0.1));
            Assert.True(others.Left(10_000, 0.1) < 10_000);
        }

        [Fact]
        public void APartGivesUpNoMoreThanItAllocated()
        {
            var world = new World();
            var others = world.Watch();
            world.Passing = BlockTakenAfter(world.Now + 0.1);
            others.Wait(0.2);

            Assert.Equal(0, others.Left(1_000, 5));
        }

        [Fact]
        public void WithoutAWaitOrWithNothingSeenInItAPartKeepsAllItAllocated()
        {
            var world = new World();
            var others = world.Watch();
            Assert.Equal(5_000, others.Left(5_000, 2));

            others.Wait(0.2);
            Assert.Equal(0, others.Bytes);
            Assert.Equal(5_000, others.Left(5_000, 2));
        }

        [Fact]
        public void NoTimeIsNoWait()
        {
            var world = new World();
            var others = world.Watch();

            others.Wait(0);
            others.Wait(-1);
            others.Wait(double.NaN);

            Assert.Equal(0, others.Ms);
            Assert.Equal(0, others.Bytes);
            Assert.Equal(0, world.MemoryReads);
        }
    }
}
