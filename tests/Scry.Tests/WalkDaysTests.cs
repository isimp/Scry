using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class WalkDaysTests
    {
        // A dungeon walked keeps the game's days it was first and last walked in: yours in your own
        // record, and anyone's in the server's, which keeps the earliest first day and the latest
        // last one it was told. A walk kept before days were knows its last day once walked again,
        // never a first one made up. The page's lines and the big map's mark say them, yours apart
        // from the server's.

        private static List<(int Hash, Vec3 Position, Vec3 Angles)> Rooms(int count) =>
            Enumerable.Range(0, count).Select(i => (100 + i, new Vec3(i, 5000f, 0f), new Vec3(0f, 0f, 0f))).ToList();

        private static DungeonMap Keep(DungeonMaps maps, string id, int day = -1) =>
            maps.Keep(id, "Crypt2", Rooms(3), new Vec3(0f, 0f, 0f), new Vec3(0f, 5000f, 0f), Quat.Identity, day);

        [Fact]
        public void ADungeonKeptOnADayIsFirstAndLastWalkedThen()
        {
            var maps = new DungeonMaps();
            var map = Keep(maps, "w:1:1", 103);
            Assert.Equal((103, 103), (map.FirstDay, map.LastDay));
        }

        [Fact]
        public void BeingInItOnALaterDayMovesItsLastDayOnly()
        {
            var maps = new DungeonMaps();
            var map = Keep(maps, "w:1:1", 103);
            maps.ToSave();
            Assert.True(maps.Visit("w:1:1", 241));
            Assert.True(maps.Changed);
            Assert.Equal((103, 241), (map.FirstDay, map.LastDay));
            Assert.False(maps.Visit("w:1:1", 241));
            Assert.False(maps.Visit("w:1:1", 200));
            Assert.False(maps.Visit("w:1:9", 300));
            Assert.Equal((103, 241), (map.FirstDay, map.LastDay));
        }

        [Fact]
        public void AWalkKeptBeforeDaysWereKnowsOnlyItsLastDay()
        {
            var maps = new DungeonMaps();
            var map = Keep(maps, "w:1:1");
            Assert.Equal((-1, -1), (map.FirstDay, map.LastDay));
            maps.Visit("w:1:1", 241);
            Assert.Equal((-1, 241), (map.FirstDay, map.LastDay));
            // Kept again, as each visit does, it stays as it was.
            Keep(maps, "w:1:1", 250);
            Assert.Equal((-1, 241), (map.FirstDay, map.LastDay));
        }

        [Fact]
        public void TheDaysAreKeptWithTheMapsAsOlderScryStillReadsThem()
        {
            var maps = new DungeonMaps();
            Keep(maps, "w:1:1", 103);
            maps.Visit("w:1:1", 241);
            Keep(maps, "w:1:2");
            maps.Visit("w:1:2", 50);
            Keep(maps, "w:1:3");
            var text = maps.ToSave()();

            var back = DungeonMaps.Load(text);
            Assert.Equal((103, 241), (back.Of("w:1:1").FirstDay, back.Of("w:1:1").LastDay));
            Assert.Equal((-1, 50), (back.Of("w:1:2").FirstDay, back.Of("w:1:2").LastDay));
            Assert.Equal((-1, -1), (back.Of("w:1:3").FirstDay, back.Of("w:1:3").LastDay));
            Assert.False(back.Changed);

            // Each dungeon's own line is as before, so an older Scry passes over the days alone.
            var lines = text.Split('\n').Skip(1).Where(l => l.Length > 0).ToList();
            Assert.All(lines.Where(l => !l.StartsWith("days\t")), l => Assert.Equal(7, l.Split('\t').Length));
            Assert.Equal(2, lines.Count(l => l.StartsWith("days\t")));

            // Days of a dungeon not kept, or that cannot be read, are passed over; a file without them reads as before.
            var odd = DungeonMaps.Load(text + "days\tw:9:9\t1\t2\ndays\tw:1:3\tsoon\t2\n");
            Assert.Equal((-1, -1), (odd.Of("w:1:3").FirstDay, odd.Of("w:1:3").LastDay));
            Assert.Null(odd.Of("w:9:9"));
            var older = DungeonMaps.Load(string.Join("\n", text.Split('\n').Where(l => !l.StartsWith("days\t"))));
            Assert.Equal((-1, -1), (older.Of("w:1:1").FirstDay, older.Of("w:1:1").LastDay));
        }

        [Fact]
        public void TheServerKeepsTheEarliestFirstDayAndTheLatestLastDay()
        {
            var server = new DungeonMaps();
            DungeonMaps Told(int first, int last)
            {
                var one = new DungeonMaps();
                var map = Keep(one, "w:1:1", first);
                if (last >= 0) one.Visit("w:1:1", last);
                return one;
            }

            server.Merge(Told(103, 200), "w");
            Assert.Equal((103, 200), (server.Of("w:1:1").FirstDay, server.Of("w:1:1").LastDay));
            server.Merge(Told(98, 150), "w");
            Assert.Equal((98, 200), (server.Of("w:1:1").FirstDay, server.Of("w:1:1").LastDay));
            server.Merge(Told(120, 130), "w");
            Assert.Equal((98, 200), (server.Of("w:1:1").FirstDay, server.Of("w:1:1").LastDay));
            server.Merge(Told(-1, 245), "w");
            Assert.Equal((98, 245), (server.Of("w:1:1").FirstDay, server.Of("w:1:1").LastDay));
            server.Merge(Told(-1, -1), "w");
            Assert.Equal((98, 245), (server.Of("w:1:1").FirstDay, server.Of("w:1:1").LastDay));

            // Its answer to a player carries them.
            var answer = server.Only("w", new[] { "Crypt2" });
            Assert.Equal((98, 245), (answer.Of("w:1:1").FirstDay, answer.Of("w:1:1").LastDay));
        }

        [Fact]
        public void ThePagesLinesSayTheDaysYoursApartFromTheServers()
        {
            Assert.Equal("Walked by you: 6 of 18 rooms", DungeonMapWords.Yours(0, 1, 6, 18, -1, -1));
            Assert.Equal("Walked by you (2 of 3): 1 of 1 room · days 103–241", DungeonMapWords.Yours(1, 3, 1, 1, 103, 241));
            Assert.Equal("Walked by you: 6 of 18 rooms · day 103", DungeonMapWords.Yours(0, 1, 6, 18, 103, 103));
            Assert.Equal("Walked by you: 6 of 18 rooms · last day 241", DungeonMapWords.Yours(0, 1, 6, 18, -1, 241));
            Assert.Equal("Walked on this server: 14 of 18 rooms · days 98–245 · opened up to the Mountain tier", DungeonMapWords.Server(0, 1, 14, 18, "Mountain", 98, 245, 0));
            Assert.Equal("Walked on this server (2 of 2): 1 of 2 rooms · opened up to the Plains tier", DungeonMapWords.Server(1, 2, 1, 2, "Plains", -1, -1, 0));
            Assert.Equal("Walked on this server: 2 of 2 rooms · days 1,203–1,240 · opened up to the Plains tier", DungeonMapWords.Server(0, 1, 2, 2, "Plains", 1203, 1240, 0));
        }

        [Fact]
        public void TheDaysTipSaysThemInWords()
        {
            Assert.Equal("You first walked it on day 103, last on day 241", DungeonMapWords.DaysTip(true, 103, 241));
            Assert.Equal("You walked it on day 103", DungeonMapWords.DaysTip(true, 103, 103));
            Assert.Equal("You last walked it on day 241", DungeonMapWords.DaysTip(true, -1, 241));
            Assert.Equal("Anyone on this server first walked it on day 98, last on day 245", DungeonMapWords.DaysTip(false, 98, 245));
            Assert.Null(DungeonMapWords.DaysTip(true, -1, -1));
            Assert.Null(DungeonMapWords.Days(-1, -1));
        }

        [Fact]
        public void TheMarkSaysTheLastDaysYoursAndTheServers()
        {
            const string Walked = "Sunken Crypts\n<size=80%><color=#F5B857>Walked by you: 7 of 12 rooms</color></size>";
            Assert.Equal(Walked + "\n<size=75%>you last day 241 · server last day 245</size>", DungeonMapWords.MarkLabel("Sunken Crypts", 7, 12, "F5B857", 241, 245));
            Assert.Equal(Walked + "\n<size=75%>you last day 241</size>", DungeonMapWords.MarkLabel("Sunken Crypts", 7, 12, "F5B857", 241, -1));
            Assert.Equal(Walked, DungeonMapWords.MarkLabel("Sunken Crypts", 7, 12, "F5B857", -1, -1));
            const string Server = "Burial Chambers\n<size=80%><color=#F5B857>Walked on this server: 3 of 12 rooms</color></size>";
            Assert.Equal(Server + "\n<size=75%>server last day 245</size>", DungeonMapWords.ServerLabel("Burial Chambers", 3, 12, "F5B857", 245, 0));
            Assert.Equal(Server, DungeonMapWords.ServerLabel("Burial Chambers", 3, 12, "F5B857", -1, 0));
        }
    }
}
