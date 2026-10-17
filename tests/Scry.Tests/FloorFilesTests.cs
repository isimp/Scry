using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Scry.Tests
{
    // Where places' floors are is kept for each kind of place in a file of its own. A profile
    // that has shown many places keeps many files, and a session shows few of them, so a file is
    // read only once its kind of place is wanted: on the worker thread when wanted ahead, as a
    // place's copy starts to be made, and waited for when asked for still being found. A record
    // made meanwhile is kept over the file's, and forgetting what was kept leaves no file.
    // The stage asks each frame whether a kind's file is found yet, and goes on with other work
    // until it is: the files are read one job after another, so a place shown after many others
    // waits behind every file they wanted, and no frame may wait that out.
    public sealed class FloorFilesTests : IDisposable
    {
        private static readonly TimeSpan Long = TimeSpan.FromSeconds(10);
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "scry-floors-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private static FloorRecord Record(string prefab, string stamp = "stamp", float width = 2f) =>
            FloorRecord.New(prefab, true, stamp, 0f, width, 0f, 2f, 6f, -2f, new[] { new RayPart { Name = "floor" } });

        private static void Done(FileQueue queue) => Assert.True(SpinWait.SpinUntil(() => !queue.Busy, Long));

        /// <summary>A session that kept these records in the folder, its files written.</summary>
        private void Kept(params FloorRecord[] records)
        {
            var queue = new FileQueue();
            var files = new FloorFiles(_folder, queue);
            foreach (var record in records) files.Put(record);
            Done(queue);
        }

        /// <summary>A queue held busy until the event is set, as by a long write.</summary>
        private static FileQueue Held(ManualResetEventSlim until)
        {
            var queue = new FileQueue();
            queue.Then("holding", () => until.Wait(Long));
            return queue;
        }

        [Fact]
        public void OnlyTheKindsOfPlaceShownAreHeldTheRestStayOnDisk()
        {
            Kept(Record("crypt2_Room_skull"), Record("crypt2_Corridor"), Record("cave_Hall"));
            var queue = new FileQueue();
            var files = new FloorFiles(_folder, queue);
            Done(queue);

            Assert.Equal(3, files.Count);
            Assert.Equal(0, files.Held);
            Assert.NotNull(files.Find("crypt2_Corridor", true, "stamp"));
            Assert.Equal(1, files.Held);
        }

        [Fact]
        public void AKindWantedAheadIsTakenOnceFoundAndWaitedForWhileStillBeingFound()
        {
            Kept(Record("crypt2_Room_skull", width: 4f));
            var until = new ManualResetEventSlim();
            var queue = Held(until);
            var files = new FloorFiles(_folder, queue);
            files.Want("crypt2_Room_skull", true);
            Assert.True(queue.Busy);

            // The file is found on the worker while asking waits for it.
            Task.Delay(100).ContinueWith(_ => until.Set());
            var found = files.Find("crypt2_Room_skull", true, "stamp");

            Assert.NotNull(found);
            Assert.Equal(4f, found.MaxX);
        }

        [Fact]
        public void AKindStillBeingFoundIsNotReadyAndAskingWaitsForNothing()
        {
            Kept(Record("crypt2_Room_skull"));
            var until = new ManualResetEventSlim();
            var queue = Held(until);
            var files = new FloorFiles(_folder, queue);
            files.Want("crypt2_Room_skull", true);

            var asked = System.Diagnostics.Stopwatch.StartNew();
            Assert.False(files.Ready("crypt2_Room_skull", true));
            Assert.True(asked.Elapsed < TimeSpan.FromSeconds(2), asked.Elapsed.ToString());
            until.Set();
            Assert.True(SpinWait.SpinUntil(() => files.Ready("crypt2_Room_skull", true), Long));
            Assert.Equal(1, files.Held);
        }

        [Fact]
        public void AKindIsReadyOnceFoundAndTakenWithoutWaitingForTheFilesWantedAfterIt()
        {
            Kept(Record("crypt2_Room_skull", width: 4f), Record("crypt2_Corridor"));
            var until = new ManualResetEventSlim();
            var queue = new FileQueue();
            var files = new FloorFiles(_folder, queue);
            files.Want("crypt2_Room_skull", true);
            // Wanted after it, behind a long job: the files stay busy long after its own is found.
            queue.Then("holding", () => until.Wait(Long));
            files.Want("crypt2_Corridor", true);

            Assert.True(SpinWait.SpinUntil(() => files.Ready("crypt2_Room_skull", true), Long));
            Assert.True(queue.Busy);
            var asked = System.Diagnostics.Stopwatch.StartNew();
            var found = files.Find("crypt2_Room_skull", true, "stamp");
            Assert.True(asked.Elapsed < TimeSpan.FromSeconds(2), asked.Elapsed.ToString());
            Assert.Equal(4f, found.MaxX);
            Assert.False(files.Ready("crypt2_Corridor", true));
            until.Set();
            Done(queue);
        }

        [Fact]
        public void AKindWithNoFileIsReadyAtOnceWhileFilesAreBeingWritten()
        {
            Kept(Record("crypt2_Room_skull"));
            var until = new ManualResetEventSlim();
            var queue = new FileQueue();
            var files = new FloorFiles(_folder, queue);
            Done(queue);
            queue.Then("holding", () => until.Wait(Long));

            Assert.True(files.Ready("cave_Hall", true));
            Assert.Null(files.Find("cave_Hall", true, "stamp"));
            until.Set();
        }

        [Fact]
        public void AskingWhetherAKindNotWantedAheadIsReadyStartsFindingIt()
        {
            Kept(Record("crypt2_Room_skull", width: 5f));
            var until = new ManualResetEventSlim();
            var queue = Held(until);
            var files = new FloorFiles(_folder, queue);

            Assert.False(files.Ready("crypt2_Room_skull", true));
            until.Set();
            Assert.True(SpinWait.SpinUntil(() => files.Ready("crypt2_Room_skull", true), Long));
            Assert.Equal(5f, files.Find("crypt2_Room_skull", true, "stamp").MaxX);
        }

        [Fact]
        public void ARecordMadeWhileItsFileIsStillBeingFoundIsReadyAtOnce()
        {
            Kept(Record("crypt2_Room_skull", width: 2f));
            var until = new ManualResetEventSlim();
            var queue = Held(until);
            var files = new FloorFiles(_folder, queue);
            files.Want("crypt2_Room_skull", true);

            files.Put(Record("crypt2_Room_skull", width: 8f));

            Assert.True(files.Ready("crypt2_Room_skull", true));
            Assert.Equal(8f, files.Find("crypt2_Room_skull", true, "stamp").MaxX);
            until.Set();
            Done(queue);
        }

        [Fact]
        public void AKindKeptWithAnotherStampIsPassedOver()
        {
            Kept(Record("crypt2_Room_skull", "before"));
            var files = new FloorFiles(_folder, new FileQueue());

            Assert.Null(files.Find("crypt2_Room_skull", true, "now"));
            Assert.NotNull(files.Find("crypt2_Room_skull", true, "before"));
        }

        [Fact]
        public void AStampNotKnownFindsNothing()
        {
            Kept(Record("crypt2_Room_skull", ""));
            Kept(Record("crypt2_Corridor"));
            var files = new FloorFiles(_folder, new FileQueue());

            Assert.Null(files.Find("crypt2_Corridor", true, ""));
            Assert.Equal(0, files.Held);
        }

        [Fact]
        public void AKindNeverKeptIsNone()
        {
            Kept(Record("crypt2_Room_skull"));
            var queue = new FileQueue();
            var files = new FloorFiles(_folder, queue);

            Assert.Null(files.Find("cave_Hall", true, "stamp"));
            Done(queue);
            Assert.Null(files.Find("crypt2_Corridor", true, "stamp"));
            Assert.Null(files.Find("crypt2_Room_skull", false, "stamp"));
            Assert.Equal(0, files.Held);
        }

        [Fact]
        public void AKindWithNoFileIsNoneWithoutWaitingForTheFilesBeingWritten()
        {
            Kept(Record("crypt2_Room_skull"));
            var until = new ManualResetEventSlim();
            var queue = new FileQueue();
            var files = new FloorFiles(_folder, queue);
            Done(queue);
            queue.Then("holding", () => until.Wait(Long));

            var asked = System.Diagnostics.Stopwatch.StartNew();
            Assert.Null(files.Find("cave_Hall", true, "stamp"));
            Assert.True(asked.Elapsed < TimeSpan.FromSeconds(2), asked.Elapsed.ToString());
            until.Set();
        }

        [Fact]
        public void ForgettingBeforeTheFolderIsListedLeavesNothingListed()
        {
            Kept(Record("crypt2_Room_skull"), Record("crypt2_Corridor"));
            var until = new ManualResetEventSlim();
            var queue = Held(until);
            var files = new FloorFiles(_folder, queue);

            files.Clear();
            until.Set();
            Done(queue);

            Assert.Equal(0, files.Count);
            Assert.Empty(Directory.GetFiles(_folder));
        }

        [Fact]
        public void ARecordMadeWhileItsFileIsStillBeingFoundIsKeptOverTheFile()
        {
            Kept(Record("crypt2_Room_skull", width: 2f));
            var until = new ManualResetEventSlim();
            var queue = Held(until);
            var files = new FloorFiles(_folder, queue);
            files.Want("crypt2_Room_skull", true);

            files.Put(Record("crypt2_Room_skull", width: 8f));
            until.Set();
            Done(queue);

            Assert.Equal(8f, files.Find("crypt2_Room_skull", true, "stamp").MaxX);
            var next = new FloorFiles(_folder, new FileQueue());
            Assert.Equal(8f, next.Find("crypt2_Room_skull", true, "stamp").MaxX);
        }

        [Fact]
        public void WhatIsPutIsWrittenForTheNextSessionUnlessItsStampIsNotKnown()
        {
            var queue = new FileQueue();
            var files = new FloorFiles(_folder, queue);
            files.Put(Record("crypt2_Room_skull", width: 3f));
            files.Put(Record("crypt2_Corridor", ""));
            Done(queue);

            Assert.Equal(1, files.Count);
            Assert.Equal(2, files.Held);
            var next = new FloorFiles(_folder, new FileQueue());
            Assert.Equal(3f, next.Find("crypt2_Room_skull", true, "stamp").MaxX);
            Assert.Equal(1, next.Count);
        }

        [Fact]
        public void ForgettingWhatWasKeptLeavesNoKindAndNoFile()
        {
            Kept(Record("crypt2_Room_skull"), Record("crypt2_Corridor"));
            // A save that stopped before swapping its file into place is kept too, and forgotten with the rest.
            File.WriteAllText(Path.Combine(_folder, FloorRecord.FileName("cave_Hall", true) + ".tmp"), Record("cave_Hall").Save());
            File.WriteAllText(Path.Combine(_folder, "notes.txt"), "not Scry's");
            var queue = new FileQueue();
            var files = new FloorFiles(_folder, queue);
            Done(queue);
            Assert.Equal(3, files.Count);
            Assert.NotNull(files.Find("cave_Hall", true, "stamp"));

            files.Clear();
            Done(queue);

            Assert.Equal(0, files.Count);
            Assert.Equal(0, files.Held);
            Assert.Null(files.Find("crypt2_Room_skull", true, "stamp"));
            Assert.Equal(new[] { "notes.txt" }, Directory.GetFiles(_folder).Select(Path.GetFileName));
            Assert.Equal(0, new FloorFiles(_folder, new FileQueue()).Count);
        }

        [Fact]
        public void AFileThatCannotBeReadIsNoneAndTheRestAreKept()
        {
            Kept(Record("crypt2_Room_skull"), Record("crypt2_Corridor"));
            File.WriteAllText(Path.Combine(_folder, FloorRecord.FileName("crypt2_Corridor", true)), "Scry floors 1\nplace\tcut");
            var files = new FloorFiles(_folder, new FileQueue());

            Assert.Null(files.Find("crypt2_Corridor", true, "stamp"));
            Assert.NotNull(files.Find("crypt2_Room_skull", true, "stamp"));
            Assert.Empty(files.Failures());
        }

        [Fact]
        public void AFileThatCannotBeOpenedIsNoneAndToldOnceHoweverOftenItIsAskedFor()
        {
            Kept(Record("crypt2_Room_skull"));
            // Held open by another, as an editor or a backup can: reading it fails.
            using var held = new FileStream(Path.Combine(_folder, FloorRecord.FileName("crypt2_Room_skull", true)), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var files = new FloorFiles(_folder, new FileQueue());

            Assert.True(SpinWait.SpinUntil(() => files.Ready("crypt2_Room_skull", true), Long));
            for (var frame = 0; frame < 3; frame++) Assert.True(files.Ready("crypt2_Room_skull", true));
            Assert.Null(files.Find("crypt2_Room_skull", true, "stamp"));

            Assert.Single(files.Failures());
            Assert.Empty(files.Failures());
        }

        [Fact]
        public void NothingKeptYetIsNoKindAndNoFailure()
        {
            var queue = new FileQueue();
            var files = new FloorFiles(_folder, queue);
            Done(queue);

            Assert.Equal(0, files.Count);
            Assert.Null(files.Find("crypt2_Room_skull", true, "stamp"));
            Assert.Empty(files.Failures());
        }
    }
}
