using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class WalkLinesTests
    {
        // A dungeon location's page lists the dungeons of it walked in this world, one line each:
        // where it lies from where you stand, how far you walked it and how far anyone on this
        // server did, the two of one dungeon on one line. The server's records carry no place of
        // the world: where one lies is known from your own walk of it, or from the ground you know
        // holding it. Those you know stand nearest first; one lying outside what you know says so,
        // its place kept hidden, after them. A line's tip tells the days and what was left inside.

        /// <summary>A record as the server sends it, with no place of the world.</summary>
        private static DungeonMap Sent(int first, int last, int traced = 0, TraceTally tally = null) => Map(0f, 0f, first, last, traced, tally);

        private static DungeonMap Map(float x, float z, int first, int last, int traced = 0, TraceTally tally = null)
        {
            var map = new DungeonMap { Location = "Crypt2", Rooms = new List<(int, Vec3, Vec3)>(), Entrance = new Vec3(x, 0f, z), FirstDay = first, LastDay = last, Tally = tally };
            for (var i = 0; i < traced; i++) map.Traced.Add(i);
            return map;
        }

        private static readonly Vec3 Origin = new Vec3(0f, 0f, 0f);

        private static List<WalkLine> Lines(Vec3 player)
        {
            var yours = new List<(string, DungeonMap, int, int)>
            {
                ("w:at:240:240", Map(240f, 240f, 288, 301), 4, 18),
                // Nor does a walk of yours with none.
                ("w:at:9:9", Map(9f, 9f, 5, 5), 0, 7),
            };
            var server = new List<(string, DungeonMap, int, int)>
            {
                // Nearer than any you know, yet after them: its place stays hidden.
                ("w:at:-200:0", Sent(-1, -1, 9), 9, 9),
                ("w:at:240:240", Sent(120, 301, 2), 6, 18),
                ("w:at:0:-1200", Sent(96, 96, 3), 3, 22),
                // A record with no room walked holds nothing to show.
                ("w:at:50:50", Sent(-1, -1), 0, 12),
            };
            // Where the ground you know holds them: the one walked by you is placed by your walk.
            var known = new Dictionary<string, Vec3> { ["w:at:0:-1200"] = new Vec3(0f, 30f, -1200f), ["w:at:50:50"] = new Vec3(50f, 0f, 50f), ["w:at:240:240"] = new Vec3(9999f, 0f, 9999f) };
            return WalkLines.Of(yours, server, id => known.TryGetValue(id, out var at) ? at : (Vec3?)null, player);
        }

        [Fact]
        public void EachDungeonHasOneLineSayingWhereItLiesAndHowFarYouAndTheServerWalkedIt()
        {
            var lines = Lines(Origin);
            Assert.Equal(new[] { "w:at:240:240", "w:at:0:-1200", "w:at:-200:0" }, lines.Select(l => l.Id));
            Assert.Equal(new[]
            {
                "340 m north-east · you 4 of 18 rooms · on this server 6 of 18 rooms, 2 from traces",
                "1.2 km south · on this server 3 of 22 rooms, from traces",
                // Outside what you know: its place stays hidden.
                "One you have not found · on this server 9 of 9 rooms, from traces",
            }, lines.Select(DungeonMapWords.Line));
            Assert.Equal(new[] { true, true, false }, lines.Select(l => l.Found));
            Assert.Equal(new[] { true, false, false }, lines.Select(l => l.Yours.HasValue));
        }

        [Fact]
        public void TheDungeonsYouKnowStandNearestFirstAsYouMove()
        {
            var lines = Lines(new Vec3(0f, 5000f, -1100f));
            Assert.Equal(new[] { "w:at:0:-1200", "w:at:240:240", "w:at:-200:0" }, lines.Select(l => l.Id));
            Assert.Equal("100 m south · on this server 3 of 22 rooms, from traces", DungeonMapWords.Line(lines[0]));
            Assert.Equal(4, WalkLines.FirstShown);
        }

        [Fact]
        public void TheDungeonsYouHaveNotFoundKeepTheirOrderSoNothingTellsWhereTheyLie()
        {
            var server = new List<(string, DungeonMap, int, int)>
            {
                ("w:at:3000:0", Map(3000f, 0f, -1, -1), 2, 9),
                ("w:at:100:0", Map(100f, 0f, -1, -1), 1, 9),
            };
            // Where this game is the server its records hold their places; still those not found stay as kept.
            var lines = WalkLines.Of(new List<(string, DungeonMap, int, int)>(), server, _ => null, Origin);
            Assert.Equal(new[] { "w:at:3000:0", "w:at:100:0" }, lines.Select(l => l.Id));
        }

        [Fact]
        public void WhereADungeonLiesIsSaidByDirectionInMetresThenKilometres()
        {
            Assert.Equal(0, WalkLines.DirectionOf(Origin, new Vec3(0f, 0f, 10f)));
            Assert.Equal(1, WalkLines.DirectionOf(Origin, new Vec3(10f, 0f, 10f)));
            Assert.Equal(2, WalkLines.DirectionOf(Origin, new Vec3(10f, 0f, 0f)));
            Assert.Equal(3, WalkLines.DirectionOf(Origin, new Vec3(10f, 0f, -10f)));
            Assert.Equal(4, WalkLines.DirectionOf(Origin, new Vec3(0f, 0f, -10f)));
            Assert.Equal(5, WalkLines.DirectionOf(Origin, new Vec3(-10f, 0f, -10f)));
            Assert.Equal(6, WalkLines.DirectionOf(Origin, new Vec3(-10f, 0f, 0f)));
            Assert.Equal(7, WalkLines.DirectionOf(Origin, new Vec3(-10f, 0f, 10f)));
            // Each direction spans 45 degrees, north from 337.5 to 22.5.
            Assert.Equal(0, WalkLines.DirectionOf(Origin, new Vec3(4.1f, 0f, 10f)));
            Assert.Equal(1, WalkLines.DirectionOf(Origin, new Vec3(4.2f, 0f, 10f)));
            Assert.Equal(0, WalkLines.DirectionOf(Origin, new Vec3(-4.1f, 0f, 10f)));
            Assert.Equal(500f, WalkLines.DistanceOf(new Vec3(100f, 5000f, 100f), new Vec3(400f, 0f, 500f)), 3);

            Assert.Equal("Where you stand", DungeonMapWords.Where(29f, 3));
            Assert.Equal("30 m north", DungeonMapWords.Where(30f, 0));
            Assert.Equal("340 m north-east", DungeonMapWords.Where(339.4f, 1));
            Assert.Equal("990 m north-east", DungeonMapWords.Where(985f, 1));
            Assert.Equal("990 m east", DungeonMapWords.Where(994f, 2));
            Assert.Equal("1.0 km south-east", DungeonMapWords.Where(995f, 3));
            Assert.Equal("1.2 km south", DungeonMapWords.Where(1234f, 4));
            Assert.Equal("9.9 km south-west", DungeonMapWords.Where(9940f, 5));
            Assert.Equal("10 km west", DungeonMapWords.Where(9960f, 6));
            Assert.Equal("12 km north-west", DungeonMapWords.Where(12345f, 7));
        }

        [Fact]
        public void ALinesTipTellsTheDaysYouAndTheServerWalkedItAndWhatWasLeftInside()
        {
            var tally = new TraceTally { Chests = 1, Doors = 1, Rooms = 1 };
            var yours = new List<(string, DungeonMap, int, int)> { ("w:at:1:1", Map(1f, 1f, 288, 301), 4, 18) };
            var server = new List<(string, DungeonMap, int, int)> { ("w:at:1:1", Map(1f, 1f, 120, 301, 2, tally), 6, 18) };
            var line = WalkLines.Of(yours, server, _ => null, Origin).Single();
            Assert.Equal(Naming.Lines(DungeonMapWords.DaysTip(true, 288, 301), DungeonMapWords.ServerTip(tally, 120, 301)), DungeonMapWords.LineTip(line));
            // A walk of yours alone tells your days; one with no day known has no tip.
            var mine = WalkLines.Of(yours, new List<(string, DungeonMap, int, int)>(), _ => null, Origin).Single();
            Assert.Equal(DungeonMapWords.DaysTip(true, 288, 301), DungeonMapWords.LineTip(mine));
            // One on this server alone tells its days, and what was left inside where anything was.
            Assert.Equal(DungeonMapWords.DaysTip(false, 98, 245), DungeonMapWords.LineTip(new WalkLine { Server = (2, 9, 0), ServerFirst = 98, ServerLast = 245 }));
            var none = WalkLines.Of(new List<(string, DungeonMap, int, int)> { ("w:at:2:2", Map(2f, 2f, -1, -1), 1, 3) }, new List<(string, DungeonMap, int, int)>(), _ => null, Origin).Single();
            Assert.Null(DungeonMapWords.LineTip(none));
            Assert.Equal("Walked in this world (3)", DungeonMapWords.Heading(3));
        }
    }
}
