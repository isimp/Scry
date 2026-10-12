using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;

namespace Scry.Tests
{
    // What Scry keeps in the cache folder is found and written on a worker thread, one job after
    // another in the order asked: a file still being written is read once written, the newest
    // write is the one left in place, and a failure is told once on the main thread.
    public class FileQueueTests
    {
        private static readonly TimeSpan Long = TimeSpan.FromSeconds(10);

        private static void Done(FileQueue queue) => Assert.True(SpinWait.SpinUntil(() => !queue.Busy, Long));

        [Fact]
        public void JobsRunOneAfterAnotherInTheOrderAsked()
        {
            var queue = new FileQueue();
            var order = new List<int>();
            var first = new ManualResetEventSlim();
            queue.Then("first", () =>
            {
                first.Wait(Long);
                lock (order) order.Add(1);
            });
            queue.Then("second", () => { lock (order) order.Add(2); });
            queue.Then("third", () => { lock (order) order.Add(3); });

            Assert.True(queue.Busy);
            first.Set();
            Done(queue);

            Assert.Equal(new[] { 1, 2, 3 }, order);
        }

        [Fact]
        public void WhatIsFoundAfterAWriteIsWhatWasWritten()
        {
            var queue = new FileQueue();
            var file = "";
            var writing = new ManualResetEventSlim();
            queue.Then("writing", () =>
            {
                writing.Wait(Long);
                file = "newest";
            });
            var found = queue.Then(() => file);

            writing.Set();

            Assert.True(found.Wait(Long));
            Assert.Equal("newest", found.Result);
        }

        [Fact]
        public void AWriteWaitsForAFindingStillReadingTheFile()
        {
            // Swapping a file in while it is read fails on Windows.
            var queue = new FileQueue();
            var reading = new ManualResetEventSlim();
            var read = false;
            var wroteWhileReading = false;
            var found = queue.Then(() =>
            {
                reading.Wait(Long);
                read = true;
                return "kept";
            });
            queue.Then("writing", () => wroteWhileReading = !read);

            reading.Set();
            Done(queue);

            Assert.Equal("kept", found.Result);
            Assert.False(wroteWhileReading);
        }

        [Fact]
        public void AFailedWriteIsToldOnceAndTheNextStillRuns()
        {
            var queue = new FileQueue();
            var wrote = false;
            queue.Then("writing the first", () => throw new InvalidOperationException("disk full"));
            queue.Then("writing the second", () => wrote = true);
            Done(queue);

            var failures = queue.Failures();

            Assert.True(wrote);
            var failure = Assert.Single(failures);
            Assert.Equal("writing the first", failure.Doing);
            Assert.Equal("disk full", failure.Error.Message);
            Assert.Empty(queue.Failures());
        }

        [Fact]
        public void AFailedFindingIsLeftToWhoAskedForIt()
        {
            var queue = new FileQueue();
            var found = queue.Then<string>(() => throw new InvalidOperationException("unreadable"));
            var after = queue.Then(() => "read on");

            Assert.True(after.Wait(Long));
            Assert.True(found.IsFaulted);
            Assert.Equal("unreadable", found.Exception.GetBaseException().Message);
            Assert.Empty(queue.Failures());
        }

        [Fact]
        public void NothingAskedIsNothingBusyAndNothingFailed()
        {
            var queue = new FileQueue();

            Assert.False(queue.Busy);
            Assert.Empty(queue.Failures());
        }

        [Fact]
        public void AWriteStillRunningIsNotToldYet()
        {
            var queue = new FileQueue();
            var writing = new ManualResetEventSlim();
            queue.Then("writing", () =>
            {
                writing.Wait(Long);
                throw new InvalidOperationException("late");
            });

            Assert.Empty(queue.Failures());
            writing.Set();
            Done(queue);
            Assert.Single(queue.Failures());
        }
    }
}
