using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class WalkSyncTests
    {
        // On joining, a player's game lists its walks of the world to the server, a few bytes a
        // dungeon: which, how many of its rooms were walked and the last day. The server answers
        // with those it lacks, has fewer rooms of, or an older day of, and the game sends just
        // those. After an admin forgot the world's records, a walk last walked before then is not
        // wanted, so a forget sticks.

        private static List<(int, Vec3, Vec3)> Rooms(int count) =>
            Enumerable.Range(0, count).Select(i => (i + 1, new Vec3(0f, 5000f, i * 12f), new Vec3(0f, 0f, 0f))).ToList();

        private static DungeonMaps Walks(params (string Id, int[] Walked, int Last)[] maps)
        {
            var kept = new DungeonMaps();
            foreach (var (id, walked, last) in maps)
            {
                var map = kept.Keep(id, "Crypt2", Rooms(6), new Vec3(1f, 2f, 3f), default, Quat.Identity, last);
                foreach (var room in walked) kept.Walk(id, room);
            }
            return kept;
        }

        [Fact]
        public void TheListNamesEachWalkOfTheWorldWithHowFarAndTheLastDay()
        {
            var own = Walks(("w:at:1:1", new[] { 0, 2 }, 41), ("w:at:2:2", new int[0], 42), ("v:at:1:1", new[] { 1 }, 43), ("w:at:3:3", new[] { 4 }, -1));
            var listed = WalkList.Of(own, "w");
            Assert.Equal(new[] { ("w:at:1:1", 2, 41), ("w:at:3:3", 1, -1) }, listed.Select(l => (l.Id, l.Walked, l.LastDay)));
            Assert.Empty(WalkList.Of(own, null));
        }

        [Fact]
        public void TheListGoesAsBytesAndComesBackTheSame()
        {
            var listed = new List<WalkList.Walk> { new WalkList.Walk("w:at:1:1", 2, 41), new WalkList.Walk("w:at:-3:7", 1, -1) };
            var back = WalkList.Read(WalkList.Write(listed));
            Assert.Equal(listed.Select(l => (l.Id, l.Walked, l.LastDay)), back.Select(l => (l.Id, l.Walked, l.LastDay)));
            Assert.Null(WalkList.Read(null));
            Assert.Null(WalkList.Read(new byte[] { 1, 2, 3 }));
            var cut = WalkList.Write(listed);
            Assert.Null(WalkList.Read(cut.Take(cut.Length - 2).ToArray()));
            Assert.Null(WalkList.Read(cut.Concat(new byte[] { 0 }).ToArray()));
            // Far more than any game keeps reads as none, so a made-up count asks for nothing.
            Assert.Null(WalkList.Read(new byte[] { (byte)'S', (byte)'C', (byte)'W', (byte)'L', 0xFF, 0xFF, 0xFF, 0x7F }));
            // A whole list longer than any game's is none either: a hundred thousand walks are the most.
            var most = Enumerable.Repeat(new WalkList.Walk("w:at:1:1", 1, 1), 100000).ToList();
            Assert.Equal(100000, WalkList.Read(WalkList.Write(most)).Count);
            most.Add(new WalkList.Walk("w:at:1:1", 1, 1));
            Assert.Null(WalkList.Read(WalkList.Write(most)));
        }

        [Fact]
        public void TheServerWantsWhatItLacksHasFewerRoomsOfOrAnOlderDayOf()
        {
            var records = Walks(("w:at:1:1", new[] { 0, 2 }, 41), ("w:at:2:2", new[] { 1 }, 40), ("w:at:3:3", new[] { 0 }, 50));
            var listed = new List<WalkList.Walk>
            {
                new WalkList.Walk("w:at:1:1", 2, 41),
                new WalkList.Walk("w:at:2:2", 3, 40),
                new WalkList.Walk("w:at:3:3", 1, 52),
                new WalkList.Walk("w:at:4:4", 1, 30),
            };
            Assert.Equal(new[] { "w:at:2:2", "w:at:3:3", "w:at:4:4" }, WalkList.Wanted(records, listed, forgottenDay: -1));
            // A walk of no known day is wanted only where the server lacks it or has fewer rooms of it.
            Assert.Equal(new[] { "w:at:5:5" }, WalkList.Wanted(records, new[] { new WalkList.Walk("w:at:1:1", 2, -1), new WalkList.Walk("w:at:5:5", 1, -1) }, -1));
        }

        [Fact]
        public void AfterAForgetAWalkLastWalkedBeforeItIsNotWanted()
        {
            var records = new DungeonMaps();
            var listed = new List<WalkList.Walk>
            {
                new WalkList.Walk("w:at:1:1", 2, 119),
                new WalkList.Walk("w:at:2:2", 2, 120),
                new WalkList.Walk("w:at:3:3", 2, -1),
            };
            Assert.Equal(new[] { "w:at:2:2" }, WalkList.Wanted(records, listed, forgottenDay: 120));
            Assert.Equal(new[] { "w:at:1:1", "w:at:2:2", "w:at:3:3" }, WalkList.Wanted(records, listed, forgottenDay: -1));
        }

        [Fact]
        public void TheWalksWantedGoTogetherInBatchesSmallEnoughForOneMessage()
        {
            var own = new DungeonMaps();
            for (var i = 0; i < 45; i++) own.Keep("w:at:" + i + ":0", "Crypt2", Rooms(6), new Vec3(i, 0f, 0f), default, Quat.Identity, 10);
            var batches = WalkList.Batches(own, own.All.Select(m => m.Id).ToList()).ToList();
            Assert.Equal(new[] { 20, 20, 5 }, batches.Select(b => b.All.Count()));
            Assert.Equal(45, batches.SelectMany(b => b.All).Select(m => m.Id).Distinct().Count());
            Assert.Empty(WalkList.Batches(own, new[] { "w:at:99:0" }));
        }
    }
}
