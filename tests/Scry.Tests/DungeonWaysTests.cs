using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class DungeonWaysTests
    {
        /// <summary>A room placed at (x, y, 0)-ish as an example places it, its doorways where they stand in the world.</summary>
        private static PlacedRoom Room(string name, float x, float y, float z, int order, params Vec3[] doorways) =>
            Placed(new RoomShape { Name = name, Size = new Vec3(4f, 4f, 4f) }, new Vec3(x, y, z), order, doorways);

        private static PlacedRoom EndCap(string name, float x, float z, Vec3 doorway) =>
            Placed(new RoomShape { Name = name, Size = new Vec3(1f, 4f, 1f), EndCap = true }, new Vec3(x, 0f, z), 9, doorway);

        private static PlacedRoom Divider(string name, Vec3 at) =>
            Placed(new RoomShape { Name = name, Size = new Vec3(1f, 4f, 1f), Divider = true }, at, 9, at, at);

        private static PlacedRoom Placed(RoomShape shape, Vec3 position, int order, params Vec3[] doorways)
        {
            foreach (var at in doorways) shape.Doorways.Add(new Doorway { Position = at - position });
            return new PlacedRoom { Room = shape, Position = position, PlaceOrder = order };
        }

        private static Vec3 At(float x, float y, float z) => new Vec3(x, y, z);

        private static string[] Names(System.Collections.Generic.IEnumerable<PlacedRoom> rooms) => rooms.Select(r => r.Room.Name).OrderBy(n => n).ToArray();

        [Fact]
        public void AtACrossroadsEveryWayOnIsTold()
        {
            // The game joins each room at an open doorway of those placed before it, so a dungeon branches.
            var entrance = Room("entrance", 0f, 0f, 0f, 0, At(5f, 0f, 0f));
            var hall = Room("hall", 10f, 0f, 0f, 1, At(5f, 0f, 0f), At(15f, 0f, 0f), At(10f, 0f, 5f));
            var east = Room("east", 20f, 0f, 0f, 2, At(15f, 0f, 0f));
            var south = Room("south", 10f, 0f, 10f, 2, At(10f, 0f, 5f));

            var ways = DungeonWays.Of(new[] { entrance, hall, east, south });

            Assert.Equal(new[] { "east", "entrance", "south" }, Names(ways.WaysOf(hall)));
            Assert.True(ways.IsCrossroads(hall));
            Assert.True(ways.IsDeadEnd(east));
            Assert.True(ways.IsDeadEnd(south));
            Assert.False(ways.IsDeadEnd(entrance));
            Assert.Same(entrance, ways.Entrance);
            Assert.Equal(2, ways.StepsIn(east));
            Assert.Equal(0, ways.Loops);
        }

        [Fact]
        public void TheWayNotOnAnyRoomsRouteFromTheEntranceIsTheLoop()
        {
            // A ring of four: east is reached first, so the corner is reached through east, and west's way to it closes the loop.
            var entrance = Room("entrance", 0f, 0f, 0f, 0, At(5f, 0f, 0f), At(0f, 0f, 5f));
            var east = Room("east", 10f, 0f, 0f, 1, At(5f, 0f, 0f), At(10f, 0f, 5f));
            var corner = Room("corner", 10f, 0f, 10f, 2, At(10f, 0f, 5f), At(5f, 0f, 10f));
            var west = Room("west", 0f, 0f, 10f, 3, At(5f, 0f, 10f), At(0f, 0f, 5f));

            var ways = DungeonWays.Of(new[] { entrance, east, corner, west });

            Assert.Same(east, ways.CameFrom(corner));
            Assert.Null(ways.CameFrom(entrance));
            Assert.True(ways.IsLoop(west, corner));
            Assert.True(ways.IsLoop(corner, west));
            Assert.False(ways.IsLoop(east, corner));
            Assert.False(ways.IsLoop(entrance, west));
        }

        [Fact]
        public void TwoOpenDoorwaysMeetingAreASecondWayRound()
        {
            // At the end the game leaves two open doorways at one spot open: a loop.
            var entrance = Room("entrance", 0f, 0f, 0f, 0, At(5f, 0f, 0f), At(0f, 0f, 5f));
            var east = Room("east", 10f, 0f, 0f, 1, At(5f, 0f, 0f), At(10f, 0f, 5f));
            var corner = Room("corner", 10f, 0f, 10f, 2, At(10f, 0f, 5f), At(5f, 0f, 10f));
            var west = Room("west", 0f, 0f, 10f, 3, At(5f, 0f, 10f), At(0f, 0f, 5f));

            var ways = DungeonWays.Of(new[] { entrance, east, corner, west });

            Assert.Equal(1, ways.Loops);
            Assert.All(ways.Rooms, room => Assert.Equal(2, ways.WaysOf(room).Count));
            Assert.Equal(new[] { entrance, west }, ways.Route(west));
            Assert.Equal(2, ways.StepsIn(corner));
        }

        [Fact]
        public void ADividerWhereTwoDoorwaysMeetClosesTheWay()
        {
            var entrance = Room("entrance", 0f, 0f, 0f, 0, At(5f, 0f, 0f), At(0f, 0f, 5f));
            var east = Room("east", 10f, 0f, 0f, 1, At(5f, 0f, 0f), At(10f, 0f, 5f));
            var corner = Room("corner", 10f, 0f, 10f, 2, At(10f, 0f, 5f), At(5f, 0f, 10f));
            var west = Room("west", 0f, 0f, 10f, 3, At(5f, 0f, 10f), At(0f, 0f, 5f));
            var divider = Divider("divider", At(0f, 0f, 5f));

            var ways = DungeonWays.Of(new[] { entrance, east, corner, west, divider });

            Assert.Equal(0, ways.Loops);
            Assert.DoesNotContain(divider, ways.Rooms);
            Assert.Equal(new[] { "east" }, Names(ways.WaysOf(entrance)));
            Assert.Equal(3, ways.StepsIn(west));
        }

        [Fact]
        public void AnEndCapIsNoRoomAndLeadsNowhere()
        {
            var entrance = Room("entrance", 0f, 0f, 0f, 0, At(5f, 0f, 0f), At(0f, 0f, 5f));
            var hall = Room("hall", 10f, 0f, 0f, 1, At(5f, 0f, 0f));
            var cap = EndCap("cap", 0f, 6f, At(0f, 0f, 5f));

            var ways = DungeonWays.Of(new[] { entrance, hall, cap });

            Assert.Equal(new[] { "entrance", "hall" }, Names(ways.Rooms));
            Assert.Equal(new[] { "hall" }, Names(ways.WaysOf(entrance)));
            Assert.True(ways.IsDeadEnd(hall));
        }

        [Fact]
        public void TheWayToTheDeepestRoomGoesThroughTheFewestRooms()
        {
            var entrance = Room("entrance", 0f, 0f, 0f, 0, At(5f, 0f, 0f));
            var hall = Room("hall", 10f, 0f, 0f, 1, At(5f, 0f, 0f), At(15f, 0f, 0f), At(10f, 0f, 5f));
            var east = Room("east", 20f, 0f, 0f, 2, At(15f, 0f, 0f), At(20f, -2f, 5f));
            var stair = Room("stair", 10f, -4f, 10f, 2, At(10f, 0f, 5f), At(15f, -8f, 10f));
            var deep = Room("deep", 20f, -10f, 10f, 3, At(15f, -8f, 10f), At(20f, -2f, 5f));

            var ways = DungeonWays.Of(new[] { entrance, hall, east, stair, deep });

            Assert.Same(deep, ways.Deepest);
            Assert.Equal(3, ways.Route(deep).Count - 1);
            Assert.Same(entrance, ways.Route(deep).First());
            Assert.Same(deep, ways.Route(deep).Last());
            Assert.Equal(1, ways.Loops);
        }

        [Fact]
        public void OfTwoRoomsAsDeepTheNearerToTheEntranceIsTheDeepest()
        {
            var entrance = Room("entrance", 0f, 0f, 0f, 0, At(5f, 0f, 0f));
            var hall = Room("hall", 10f, 0f, 0f, 1, At(5f, 0f, 0f), At(15f, 0f, 0f));
            var far = Room("far", 20f, 0f, 0f, 2, At(15f, 0f, 0f), At(25f, 0f, 0f));
            var farther = Room("farther", 30f, -6f, 0f, 3, At(25f, 0f, 0f));
            var near = Room("near", 0f, -6f, 10f, 4, At(0f, 0f, 5f));
            entrance.Room.Doorways.Add(new Doorway { Position = At(0f, 0f, 5f) });

            var ways = DungeonWays.Of(new[] { entrance, hall, far, farther, near });

            Assert.Same(near, ways.Deepest);
        }

        [Fact]
        public void ARoomNoDoorwayReachesIsOnNoRouteNorTheDeepest()
        {
            var entrance = Room("entrance", 0f, 0f, 0f, 0, At(5f, 0f, 0f));
            var hall = Room("hall", 10f, -1f, 0f, 1, At(5f, 0f, 0f));
            var apart = Room("apart", 50f, -30f, 0f, 2, At(55f, -30f, 0f));

            var ways = DungeonWays.Of(new[] { entrance, hall, apart });

            Assert.Equal(-1, ways.StepsIn(apart));
            Assert.Empty(ways.Route(apart));
            Assert.Same(hall, ways.Deepest);
            Assert.Equal(0, ways.Loops);
        }

        [Fact]
        public void TheEntranceIsTheRoomTheExampleStartsWithWhereverItIsListed()
        {
            var hall = Room("hall", 10f, 0f, 0f, 1, At(5f, 0f, 0f));
            var entrance = Room("entrance", 0f, 0f, 0f, 0, At(5f, 0f, 0f));

            var ways = DungeonWays.Of(new[] { hall, entrance });

            Assert.Same(entrance, ways.Entrance);
            Assert.Equal(1, ways.StepsIn(hall));
        }

        [Fact]
        public void TwoDoorwaysIntoTheSameRoomAreOneWay()
        {
            // A wide hall opening twice onto one corridor leads to no other room.
            var entrance = Room("entrance", 0f, 0f, 0f, 0, At(5f, 0f, -2f), At(5f, 0f, 2f));
            var corridor = Room("corridor", 10f, 0f, 0f, 1, At(5f, 0f, -2f), At(5f, 0f, 2f));

            var ways = DungeonWays.Of(new[] { entrance, corridor });

            Assert.Single(ways.WaysOf(entrance));
            Assert.Equal(0, ways.Loops);
        }

        [Fact]
        public void DoorwaysMeetOnlyWithinATenthOfAMetre()
        {
            var entrance = Room("entrance", 0f, 0f, 0f, 0, At(5f, 0f, 0f), At(0f, 0f, 5f));
            var close = Room("close", 10f, 0f, 0f, 1, At(5.05f, 0f, 0f));
            var off = Room("off", 0f, 0f, 10f, 1, At(0f, 0f, 5.15f));

            var ways = DungeonWays.Of(new[] { entrance, close, off });

            Assert.Equal(new[] { "close" }, Names(ways.WaysOf(entrance)));
        }

        [Fact]
        public void WithNoStartRoomTheFirstRoomIsTheEntrance()
        {
            var first = Room("first", 0f, 0f, 0f, 3, At(5f, 0f, 0f));
            var second = Room("second", 10f, 0f, 0f, 4, At(5f, 0f, 0f));

            var ways = DungeonWays.Of(new[] { first, second });

            Assert.Same(first, ways.Entrance);
            Assert.Equal(1, ways.StepsIn(second));
        }

        [Fact]
        public void ARoomWithTwoDoorwaysAtOneSpotIsNoWayOfItself()
        {
            var entrance = Room("entrance", 0f, 0f, 0f, 0, At(5f, 0f, 0f), At(0f, 0f, 5f), At(0f, 0f, 5.02f));
            var hall = Room("hall", 10f, 0f, 0f, 1, At(5f, 0f, 0f));

            var ways = DungeonWays.Of(new[] { entrance, hall });

            Assert.Equal(new[] { "hall" }, Names(ways.WaysOf(entrance)));
            Assert.Equal(0, ways.Loops);
        }

        [Fact]
        public void AStackOfRoomsOneUnderAnotherIsOneStack()
        {
            // As Mörkhalla stands: tall rooms straight down, each joined to the next, no crossroads.
            var top = Room("top", 0f, 0f, 0f, 0, At(0f, -2f, 0f));
            var second = Room("second", 0f, -10f, 0f, 1, At(0f, -2f, 0f), At(0f, -12f, 0f));
            var third = Room("third", 0f, -20f, 0f, 2, At(0f, -12f, 0f), At(0f, -22f, 0f));
            var bottom = Room("bottom", 0f, -30f, 0f, 3, At(0f, -22f, 0f));

            Assert.True(DungeonWays.Of(new[] { top, second, third, bottom }).IsStack);
        }

        [Fact]
        public void ADungeonWithCrossroadsOrRoomsSideBySideIsNoStack()
        {
            // A cave branching at a crossroads, though most of its rooms lie under others.
            var entrance = Room("entrance", 0f, 0f, 0f, 0, At(5f, 0f, 0f));
            var hall = Room("hall", 10f, 0f, 0f, 1, At(5f, 0f, 0f), At(15f, 0f, 0f), At(10f, -5f, 0f));
            var east = Room("east", 20f, 0f, 0f, 2, At(15f, 0f, 0f));
            var under = Room("under", 10f, -8f, 0f, 2, At(10f, -5f, 0f), At(10f, -13f, 0f));
            var deeper = Room("deeper", 10f, -16f, 0f, 3, At(10f, -13f, 0f), At(10f, -21f, 0f));
            var deeper2 = Room("deeper2", 10f, -24f, 0f, 4, At(10f, -21f, 0f), At(10f, -29f, 0f));
            var deepest = Room("deepest", 10f, -32f, 0f, 5, At(10f, -29f, 0f));
            Assert.False(DungeonWays.Of(new[] { entrance, hall, east, under, deeper, deeper2, deepest }).IsStack);

            // Rooms stepping down sideways are lower but under nothing.
            var s1 = Room("s1", 0f, 0f, 0f, 0, At(5f, -2f, 0f));
            var s2 = Room("s2", 10f, -5f, 0f, 1, At(5f, -2f, 0f), At(15f, -7f, 0f));
            var s3 = Room("s3", 20f, -10f, 0f, 2, At(15f, -7f, 0f), At(25f, -12f, 0f));
            var s4 = Room("s4", 30f, -15f, 0f, 3, At(25f, -12f, 0f));
            Assert.False(DungeonWays.Of(new[] { s1, s2, s3, s4 }).IsStack);

            // Rooms overlapping on one level stand beside each other, not over.
            var near = Room("near", 0f, 0f, 0f, 0, At(0.5f, 0f, 0f));
            var niche = Room("niche", 1f, 0.5f, 0f, 1, At(0.5f, 0f, 0f), At(1.5f, 0f, 0f));
            var alcove = Room("alcove", 2f, 1f, 0f, 2, At(1.5f, 0f, 0f));
            Assert.False(DungeonWays.Of(new[] { near, niche, alcove }).IsStack);

            // Half its rooms under another is not most of them.
            var upper = Room("upper", 0f, 0f, 0f, 0, At(0f, -2f, 0f));
            var lower = Room("lower", 0f, -10f, 0f, 1, At(0f, -2f, 0f));
            Assert.False(DungeonWays.Of(new[] { upper, lower }).IsStack);

            // A crypt in a line on one level: nothing under anything.
            var a = Room("a", 0f, 0f, 0f, 0, At(5f, 0f, 0f));
            var b = Room("b", 10f, 0f, 0f, 1, At(5f, 0f, 0f), At(15f, 0f, 0f));
            var c = Room("c", 20f, 0f, 0f, 2, At(15f, 0f, 0f));
            Assert.False(DungeonWays.Of(new[] { a, b, c }).IsStack);

            Assert.False(DungeonWays.Of(new PlacedRoom[0]).IsStack);
        }

        [Fact]
        public void AnExampleWithNoRoomsHasNoWays()
        {
            var ways = DungeonWays.Of(new PlacedRoom[0]);

            Assert.Empty(ways.Rooms);
            Assert.Null(ways.Entrance);
            Assert.Null(ways.Deepest);
            Assert.Equal(0, ways.Loops);
        }
    }
}
