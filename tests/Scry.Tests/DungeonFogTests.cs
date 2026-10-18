using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class DungeonFogTests
    {
        // In the dungeon a player is in, the room they stand in is found by the rooms' boxes; with
        // the compendium on, only the rooms they have stood in show, a mark at each doorway leading
        // on to one not walked, and peek shows the rest marked as peeked.

        private static Vec3? SizeOf(int hash) => hash == 1 ? new Vec3(12f, 8f, 12f) : hash == 2 ? new Vec3(4f, 8f, 12f) : hash == 4 ? new Vec3(12f, 20f, 12f) : hash == 5 ? new Vec3(12f, 4f, 12f) : (Vec3?)null;

        [Fact]
        public void TheRoomAPlayerStandsInIsTheOneWhoseBoxHoldsThem()
        {
            var saved = new List<(int Hash, Vec3 Position, Vec3 Angles)>
            {
                (1, new Vec3(0f, 5000f, 0f), new Vec3(0f, 0f, 0f)),
                (2, new Vec3(0f, 5000f, 12f), new Vec3(0f, 90f, 0f)),
            };
            Assert.Equal(0, SavedRooms.Holding(saved, SizeOf, new Vec3(0f, 5000f, 1f)));
            // Turned a quarter, the second room's length lies across.
            Assert.Equal(1, SavedRooms.Holding(saved, SizeOf, new Vec3(5f, 5000f, 12f)));
            Assert.Equal(-1, SavedRooms.Holding(saved, SizeOf, new Vec3(0f, 5000f, 15f)));
            Assert.Equal(-1, SavedRooms.Holding(saved, SizeOf, new Vec3(0f, 5010f, 0f)));
        }

        [Fact]
        public void OfBoxesOverlappingTheRoomIsTheOneWhoseFootIsNearestUnderThePlayer()
        {
            var saved = new List<(int Hash, Vec3 Position, Vec3 Angles)>
            {
                (4, new Vec3(0f, 5000f, 0f), new Vec3(0f, 0f, 0f)),
                (5, new Vec3(0f, 5004f, 0f), new Vec3(0f, 0f, 0f)),
            };
            Assert.Equal(1, SavedRooms.Holding(saved, SizeOf, new Vec3(0f, 5003f, 0f)));
            Assert.Equal(0, SavedRooms.Holding(saved, SizeOf, new Vec3(0f, 4995f, 0f)));
        }

        [Fact]
        public void ARoomWithNoSizeKnownIsNoneToStandIn()
        {
            var saved = new List<(int Hash, Vec3 Position, Vec3 Angles)> { (3, new Vec3(0f, 5000f, 0f), new Vec3(0f, 0f, 0f)) };
            Assert.Equal(-1, SavedRooms.Holding(saved, SizeOf, new Vec3(0f, 5000f, 0f)));
        }

        private static readonly RoomShape Entrance = new RoomShape { Name = "entrance", Entrance = true, Size = new Vec3(12f, 8f, 12f), Doorways = { new Doorway { Position = new Vec3(0f, 0f, 6f) } } };
        private static readonly RoomShape Hall = new RoomShape { Name = "hall", Size = new Vec3(12f, 8f, 12f), Doorways = { new Doorway { Position = new Vec3(0f, 0f, -6f) }, new Doorway { Position = new Vec3(0f, 0f, 6f) } } };
        private static readonly RoomShape Cap = new RoomShape { Name = "cap", EndCap = true, Size = new Vec3(4f, 4f, 0.2f), Doorways = { new Doorway { Position = new Vec3(0f, 0f, 0f) } } };

        private static RoomShape Known(int hash) => hash == 1 ? Entrance : hash == 2 ? Hall : hash == 3 ? Cap : null;

        /// <summary>An entrance, a hall, another hall and an end cap closing that one, in a line; the second saved is of a kind not read.</summary>
        private static DungeonExample Line() => RealDungeon.Of(new List<(int Hash, Vec3 Position, Vec3 Angles)>
        {
            (1, new Vec3(0f, 5000f, 0f), new Vec3(0f, 0f, 0f)),
            (99, new Vec3(40f, 5000f, 40f), new Vec3(0f, 0f, 0f)),
            (2, new Vec3(0f, 5000f, 12f), new Vec3(0f, 0f, 0f)),
            (2, new Vec3(0f, 5000f, 24f), new Vec3(0f, 0f, 0f)),
            (3, new Vec3(0f, 5000f, 30f), new Vec3(0f, 0f, 0f)),
        }, Known, new Vec3(0f, 5000f, -6f), Quat.Identity, out _);

        [Fact]
        public void EachRoomKeepsItsPlaceInTheSavedList()
        {
            var line = Line();
            Assert.Equal(new[] { 0, 2, 3, 4 }, line.Rooms.Select(r => r.SavedAt));
            Assert.Equal(new[] { 0, 2, 3, 4 }, line.FromGenerator().Rooms.Select(r => r.SavedAt));
        }

        [Fact]
        public void WithTheCompendiumOffEveryRoomShows()
        {
            Assert.Equal(RoomFog.Shown, DungeonFog.Of(fogged: false, peeking: false, walked: false));
            Assert.Equal(RoomFog.Shown, DungeonFog.Of(fogged: false, peeking: false, walked: true));
        }

        [Fact]
        public void WithItOnOnlyTheRoomsWalkedShowAndPeekShowsTheRest()
        {
            Assert.Equal(RoomFog.Shown, DungeonFog.Of(fogged: true, peeking: false, walked: true));
            Assert.Equal(RoomFog.Dark, DungeonFog.Of(fogged: true, peeking: false, walked: false));
            Assert.Equal(RoomFog.Shown, DungeonFog.Of(fogged: true, peeking: true, walked: true));
            Assert.Equal(RoomFog.Peeked, DungeonFog.Of(fogged: true, peeking: true, walked: false));
        }

        [Fact]
        public void TheFoggedDungeonHoldsTheRoomsWalkedAndTheirEndCaps()
        {
            var line = Line();
            var walked = new HashSet<int> { 0, 2 };
            var shown = DungeonFog.Shown(line, r => walked.Contains(r.SavedAt), fogged: true, peeking: false, out var peeked);
            Assert.Equal(new[] { "entrance", "hall" }, shown.Rooms.Select(r => r.Room.Name));
            Assert.Same(line.Rooms[1], shown.Rooms[1]);
            Assert.Empty(peeked);
            Assert.Same(line.Site, shown.Site);

            walked.Add(3);
            shown = DungeonFog.Shown(line, r => walked.Contains(r.SavedAt), fogged: true, peeking: false, out _);
            Assert.Equal(new[] { "entrance", "hall", "hall", "cap" }, shown.Rooms.Select(r => r.Room.Name));
        }

        [Fact]
        public void PeekShowsEveryRoomTheOnesNotWalkedPeeked()
        {
            var line = Line();
            var walked = new HashSet<int> { 0 };
            var shown = DungeonFog.Shown(line, r => walked.Contains(r.SavedAt), fogged: true, peeking: true, out var peeked);
            Assert.Equal(4, shown.Rooms.Count);
            Assert.Equal(new[] { line.Rooms[1], line.Rooms[2], line.Rooms[3] }, peeked.OrderBy(r => r.SavedAt));
        }

        [Fact]
        public void WithTheCompendiumOffTheWholeDungeonShows()
        {
            var line = Line();
            var shown = DungeonFog.Shown(line, r => false, fogged: false, peeking: false, out var peeked);
            Assert.Equal(4, shown.Rooms.Count);
            Assert.Empty(peeked);
        }

        [Fact]
        public void OpenWaysLeadFromARoomWalkedToOneNot()
        {
            var line = Line();
            var ways = DungeonWays.Of(line.Rooms);
            var walked = new HashSet<int> { 0 };
            var open = DungeonFog.OpenWays(ways, r => walked.Contains(r.SavedAt));
            var way = Assert.Single(open);
            Assert.Same(line.Rooms[0], way.From);
            Assert.Same(line.Rooms[1], way.To);
            Assert.Equal(6f, way.At.Z, 3);

            walked.Add(2);
            way = Assert.Single(DungeonFog.OpenWays(ways, r => walked.Contains(r.SavedAt)));
            Assert.Same(line.Rooms[1], way.From);
            Assert.Equal(18f, way.At.Z, 3);

            walked.Add(3);
            Assert.Empty(DungeonFog.OpenWays(ways, r => walked.Contains(r.SavedAt)));
        }

        [Fact]
        public void RoomsAreCountedWalkedOfAllEndCapsLeftOut()
        {
            var line = Line();
            var walked = new HashSet<int> { 0, 4 };
            Assert.Equal((1, 3), DungeonFog.Walked(line, r => walked.Contains(r.SavedAt)));
        }

        [Fact]
        public void APointOfTheWorldStandsFromTheGeneratorAsTheRoomsDo()
        {
            var turned = RealDungeon.Of(new List<(int Hash, Vec3 Position, Vec3 Angles)> { (2, new Vec3(10f, 5000f, 20f), new Vec3(0f, 90f, 0f)) }, Known, new Vec3(4f, 5000f, 2f), Quat.Yaw(90f), out _);
            var staged = turned.FromGenerator();
            var at = turned.FromGeneratorPoint(new Vec3(10f, 5000f, 20f));
            Assert.Equal(staged.Rooms[0].Position.X, at.X, 3);
            Assert.Equal(staged.Rooms[0].Position.Y, at.Y, 3);
            Assert.Equal(staged.Rooms[0].Position.Z, at.Z, 3);
            Assert.Equal(30f, turned.FromGeneratorYaw(120f), 3);
        }

        [Fact]
        public void TheFogSaysHowMuchIsWalkedAndWhatPeekShows()
        {
            Assert.Equal("6 of 18 rooms walked", PlanBoxWords.Walked(6, 18));
            Assert.Equal("1 of 1 room walked", PlanBoxWords.Walked(1, 1));
            // Short enough for the plan box's strip; the rest in its tip.
            Assert.Equal("Peeking: 12 rooms not walked shown", PlanBoxWords.PeekingRooms(12));
            Assert.Equal("Peeking: 1 room not walked shown", PlanBoxWords.PeekingRooms(1));
            Assert.Equal("The rooms of this dungeon not walked yet show while you peek, and hide again once peeking stops", PlanBoxWords.PeekingRoomsTip);
            Assert.Equal("The rooms as they stand in your world, as far as you have walked them; the menu beside the dungeon shows an example instead", PlanBoxWords.ThisDungeonTipFogged);
            Assert.Equal("18 rooms; 1 room Scry has not read is left out. This dungeon as it stands in your world, 6 of 20 rooms walked.", DungeonWords.WorldsNote("18 rooms", 1, PlanBoxWords.Walked(6, 20)));
            Assert.Equal("A way on to a room not walked yet", PlanBoxWords.OpenWayTip);
        }

        [Fact]
        public void TheBreadcrumbEndsAtTheRoomThePlayerIsIn()
        {
            var inIt = DungeonTrail.Of("Howling Cavern", "Level 0 m", "Start corridor", roomIsYours: true);
            Assert.Equal("Start corridor · you", inIt[2].Text);
            Assert.True(inIt[2].Current);

            var notGoneIn = DungeonTrail.Of("Howling Cavern", "Level 0 m", null, you: "Start corridor");
            Assert.Equal(new[] { TrailPart.Dungeon, TrailPart.Level, TrailPart.You }, notGoneIn.Select(t => t.Part));
            Assert.Equal("Start corridor · you", notGoneIn[2].Text);
            Assert.Equal(new[] { false, true, false }, notGoneIn.Select(t => t.Current));

            var elsewhere = DungeonTrail.Of("Howling Cavern", "Level 0 m", "Shrine", you: "Start corridor");
            Assert.Equal(new[] { TrailPart.Dungeon, TrailPart.Level, TrailPart.Room }, elsewhere.Select(t => t.Part));
            Assert.Equal("Shrine", elsewhere[2].Text);
            Assert.Equal("Where you stand: click to go into it", PlanBoxWords.TrailYouTip);
        }
    }
}
