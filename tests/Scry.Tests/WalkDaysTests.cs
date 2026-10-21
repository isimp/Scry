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
        public void ThePagesLineTellsTheDaysOnHoverYoursApartFromTheServers()
        {
            // The line says where it lies and how far it was walked; its days are on its hover.
            var line = new WalkLine { Yours = (6, 18), YourFirst = 103, YourLast = 241, Server = (14, 18, 0), ServerFirst = 1203, ServerLast = 1240, Found = true, Distance = 500f };
            Assert.Equal("500 m north · you 6 of 18 rooms · on this server 14 of 18 rooms", DungeonMapWords.Line(line));
            Assert.Equal(Naming.Lines("You first walked it on day 103, last on day 241", "Anyone on this server first walked it on day 1,203, last on day 1,240"), DungeonMapWords.LineTip(line));
            Assert.Equal("Where you stand · you 1 of 1 room", DungeonMapWords.Line(new WalkLine { Yours = (1, 1), Found = true }));
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
        public void TheMarkLeavesTheDaysToTheDungeonsPage()
        {
            // The mark's words name the dungeon and how far it was walked alone, readable on the
            // map; the days walked are told on the dungeon's page, by its walk lines (DaysTip).
            Assert.DoesNotContain("day", DungeonMapWords.MarkLabel("Sunken Crypts", 7, 12, "F5B857"));
            Assert.DoesNotContain("day", DungeonMapWords.ServerLabel("Burial Chambers", 3, 12, "F5B857", 0));
        }
    }
}
