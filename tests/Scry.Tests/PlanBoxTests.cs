using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PlanBoxTests
    {
        /// <summary>A room 4 m each way, or as tall as given, centred at a point, turned as placed.</summary>
        private static PlacedRoom Room(float x, float y, float z, string name = "room", float tall = 4f) =>
            new PlacedRoom { Room = new RoomShape { Name = name, Size = new Vec3(4f, tall, 4f) }, Position = new Vec3(x, y, z) };

        // ----- The map: each room across against its depth, a storey tall from its ground, at any angle -----

        [Fact]
        public void TheMapPutsEachRoomAcrossByWhereItStandsAlongItsAngle()
        {
            var west = Room(0f, 0f, 0f);
            var east = Room(10f, 0f, 0f);
            Assert.Equal((-2f, 2f), Rounded(PlanMap.Across(west, 0f)));
            Assert.Equal((8f, 12f), Rounded(PlanMap.Across(east, 0f)));
            // Turned a quarter, the two stand at one spot across, as both stand at the same z.
            Assert.Equal((-2f, 2f), Rounded(PlanMap.Across(east, 90f)));
        }

        [Fact]
        public void ADoorwayStandsAcrossTheMapWhereItIsAlongTheAngle()
        {
            Assert.Equal(3f, PlanMap.AcrossAt(new Vec3(3f, -5f, 4f), 0f), 3);
            Assert.Equal(4f, PlanMap.AcrossAt(new Vec3(3f, -5f, 4f), 90f), 3);
        }

        [Fact]
        public void EachRoomStandsOnTheMapAStoreyTallFromItsGround()
        {
            Assert.Equal((-10f, -10f + PlanMap.Storey), PlanMap.Tile(-10f));
        }

        [Fact]
        public void TwoRoomsOverlapOnTheMapOnlyWhereTheyShareSpotAcrossAndTheirStorey()
        {
            var a = Room(0f, 0f, 0f);
            var sameSpot = Room(1f, 0f, 0f);
            var beside = Room(4f, 0f, 0f);
            Assert.Equal(1, PlanMap.Overlaps(new[] { a, sameSpot }, new[] { 0f, 0f }, 0f));
            Assert.Equal(1, PlanMap.Overlaps(new[] { a, sameSpot }, new[] { 0f, -2.9f }, 0f));
            Assert.Equal(0, PlanMap.Overlaps(new[] { a, sameSpot }, new[] { 0f, -10f }, 0f));
            Assert.Equal(0, PlanMap.Overlaps(new[] { a, sameSpot }, new[] { -10f, 0f }, 0f));
            // A storey right over another is not over it.
            Assert.Equal(0, PlanMap.Overlaps(new[] { a, sameSpot }, new[] { 0f, -PlanMap.Storey }, 0f));
            Assert.Equal(0, PlanMap.Overlaps(new[] { a, sameSpot }, new[] { -PlanMap.Storey, 0f }, 0f));
            // Wall to wall is not over each other.
            Assert.Equal(0, PlanMap.Overlaps(new[] { a, beside }, new[] { 0f, 0f }, 0f));
        }

        [Fact]
        public void TallBoxesDoNotCoverEachOtherWhereTheirGroundsLieAStoreyApart()
        {
            // Two of the caves' shafts, 30 m tall, one under the other at one spot.
            var upper = Room(0f, 0f, 0f, tall: 30f);
            var lower = Room(1f, -10f, 0f, tall: 30f);
            Assert.Equal(0, PlanMap.Overlaps(new[] { upper, lower }, new[] { -12f, -22f }, 0f));
        }

        [Fact]
        public void HalfATurnRoundShowsAsManyOverlapping()
        {
            var rooms = new[] { Room(0f, 0f, 0f), Room(6f, -1f, 3f), Room(3f, 0f, 9f), Room(-5f, -2f, 4f), Room(8f, 1f, -6f) };
            var grounds = new[] { 0f, -1f, 0f, -2f, 1f };
            for (var angle = 0f; angle < 180f; angle += 5f)
            {
                Assert.Equal(PlanMap.Overlaps(rooms, grounds, angle), PlanMap.Overlaps(rooms, grounds, angle + 180f));
            }
        }

        /// <summary>Three rooms 10 m apart in a line north, on one ground.</summary>
        private static PlacedRoom[] Row() => new[] { Room(0f, 0f, 0f), Room(0f, 0f, 10f), Room(0f, 0f, 20f) };

        private static readonly float[] Level = { 0f, 0f, 0f };

        [Fact]
        public void LeastOverlapTurnsTheMapToTheNearestAngleWhereTheFewestRoomsCoverEachOther()
        {
            // Facing along the row every room covers the others; from 35 degrees round they stand apart.
            Assert.Equal(3, PlanMap.Overlaps(Row(), Level, 0f));
            Assert.Equal(0, PlanMap.Overlaps(Row(), Level, 35f));
            Assert.True(PlanMap.Overlaps(Row(), Level, 30f) > 0);
            Assert.Equal(35f, PlanMap.LeastOverlap(Row(), Level, 0f));
        }

        [Fact]
        public void OnATieTheAngleNearestTheOneShownIsKept()
        {
            // 100 degrees already shows none overlapping: the map stays where it was put.
            Assert.Equal(100f, PlanMap.LeastOverlap(Row(), Level, 100f));
            Assert.Equal(100f, PlanMap.LeastOverlap(Row(), Level, 102f));
            // The far side of the turn is as good, and nearer the angle shown.
            Assert.Equal(215f, PlanMap.LeastOverlap(Row(), Level, 200f));
        }

        [Fact]
        public void AnglesAreApartTheShortWayRound()
        {
            Assert.Equal(30f, PlanMap.Apart(350f, 20f), 3);
            Assert.Equal(30f, PlanMap.Apart(20f, 350f), 3);
            Assert.Equal(180f, PlanMap.Apart(0f, 180f), 3);
            Assert.Equal(10f, PlanMap.Apart(-5f, 5f), 3);
        }

        [Fact]
        public void FollowingTheViewTheMapShowsOnTheRightWhatTheStageShowsOnTheRight()
        {
            var p = Room(10f, 0f, 0f);
            var q = Room(0f, 0f, 10f);
            foreach (var yaw in new[] { 0f, 30f, 135f, 250f, 330f })
            {
                var (pRight, _) = ExamplePlan.Turn(p.Position.X, p.Position.Z, yaw);
                var (qRight, _) = ExamplePlan.Turn(q.Position.X, q.Position.Z, yaw);
                var angle = PlanMap.Following(yaw);
                Assert.Equal(Math.Sign(pRight - qRight), Math.Sign(Middle(PlanMap.Across(p, angle)) - Middle(PlanMap.Across(q, angle))));
            }
        }

        [Fact]
        public void DraggingAcrossTheMapTurnsItAndGoesOnRound()
        {
            Assert.Equal(20f, PlanMap.Turned(0f, 25f), 3);
            Assert.Equal(10f, PlanMap.Turned(350f, 25f), 3);
            Assert.Equal(350f, PlanMap.Turned(10f, -25f), 3);
        }

        [Fact]
        public void TheMapStandsTheHighestRoomsStoreyAtTheTopAndTheDeepestGroundAtTheBottom()
        {
            var (top, bottom) = PlanMap.Heights(new[] { -2f, -20f, 1f });
            Assert.Equal(1f + PlanMap.Storey, top, 3);
            Assert.Equal(-20f, bottom, 3);
        }

        private static (float, float) Rounded((float Min, float Max) span) => ((float)Math.Round(span.Min, 3), (float)Math.Round(span.Max, 3));

        private static float Middle((float Min, float Max) span) => (span.Min + span.Max) / 2f;

        // ----- The tree: rows by rooms in from the entrance, each room under the room it is reached from -----

        private static PlacedRoom Placed(string name, float x, float z, int order, params Vec3[] doorways)
        {
            var shape = new RoomShape { Name = name, Size = new Vec3(4f, 4f, 4f) };
            var position = new Vec3(x, 0f, z);
            foreach (var at in doorways) shape.Doorways.Add(new Doorway { Position = at - position });
            return new PlacedRoom { Room = shape, Position = position, PlaceOrder = order };
        }

        private static Vec3 At(float x, float z) => new Vec3(x, 0f, z);

        /// <summary>
        /// An entrance with a branch to the west and one to the east; the west room leads on to one
        /// room, standing far east of everything; the east room to two, the nearer west first.
        /// </summary>
        private static (DungeonWays Ways, PlacedRoom Entrance, PlacedRoom West, PlacedRoom East, PlacedRoom FarOff, PlacedRoom Near, PlacedRoom Far) Branches()
        {
            var entrance = Placed("entrance", 0f, 0f, 0, At(-5f, 0f), At(5f, 0f));
            var west = Placed("west", -10f, 0f, 1, At(-5f, 0f), At(-10f, 5f));
            var east = Placed("east", 10f, 0f, 2, At(5f, 0f), At(8f, 5f), At(14f, 5f));
            var farOff = Placed("far off", 40f, 30f, 3, At(-10f, 5f));
            var near = Placed("near", 8f, 10f, 4, At(8f, 5f));
            var far = Placed("far", 14f, 10f, 5, At(14f, 5f));
            return (DungeonWays.Of(new[] { entrance, west, east, farOff, near, far }), entrance, west, east, farOff, near, far);
        }

        [Fact]
        public void EachRowOfTheTreeIsOneMoreRoomInFromTheEntrance()
        {
            var (ways, entrance, west, east, farOff, near, far) = Branches();
            var tree = TreeRows.Of(ways, r => r.Position.X);
            Assert.Equal(3, tree.Rows);
            Assert.Equal(0, tree.RowOf(entrance));
            Assert.Equal(new[] { 1, 1 }, new[] { tree.RowOf(west), tree.RowOf(east) });
            Assert.Equal(new[] { 2, 2, 2 }, new[] { tree.RowOf(farOff), tree.RowOf(near), tree.RowOf(far) });
        }

        [Fact]
        public void TheEntranceStandsInTheMiddleAndARowSpreadsEvenlyAcross()
        {
            var (ways, entrance, west, east, _, _, _) = Branches();
            var tree = TreeRows.Of(ways, r => r.Position.X);
            Assert.Equal(0.5f, tree.AcrossOf(entrance), 3);
            Assert.Equal(0.25f, tree.AcrossOf(west), 3);
            Assert.Equal(0.75f, tree.AcrossOf(east), 3);
        }

        [Fact]
        public void EachRoomStandsUnderTheRoomItIsReachedFromWhereverItLies()
        {
            // The west branch's room stands far east, yet stays under the west room, so no way crosses.
            var (ways, _, _, _, farOff, near, far) = Branches();
            var tree = TreeRows.Of(ways, r => r.Position.X);
            Assert.True(tree.AcrossOf(farOff) < tree.AcrossOf(near));
            Assert.True(tree.AcrossOf(near) < tree.AcrossOf(far));
            Assert.Equal(1f / 6f, tree.AcrossOf(farOff), 3);
        }

        [Fact]
        public void ARoomReachedFromTwoRoomsStandsBetweenThem()
        {
            // A loop: the middle room joins both the west and the east room; each also leads to one of its own.
            var entrance = Placed("entrance", 0f, 0f, 0, At(-5f, 0f), At(5f, 0f));
            var west = Placed("west", -10f, 0f, 1, At(-5f, 0f), At(-10f, 5f), At(-6f, 5f));
            var east = Placed("east", 10f, 0f, 2, At(5f, 0f), At(10f, 5f), At(6f, 5f));
            var middle = Placed("middle", 100f, 10f, 3, At(-6f, 5f), At(6f, 5f));
            var westOwn = Placed("west's own", -10f, 10f, 4, At(-10f, 5f));
            var eastOwn = Placed("east's own", 10f, 10f, 5, At(10f, 5f));
            var tree = TreeRows.Of(DungeonWays.Of(new[] { entrance, west, east, middle, westOwn, eastOwn }), r => r.Position.X);
            Assert.True(tree.AcrossOf(westOwn) < tree.AcrossOf(middle));
            Assert.True(tree.AcrossOf(middle) < tree.AcrossOf(eastOwn));
        }

        [Fact]
        public void RoomsUnderOneRoomStandInTheOrderTheyLieAcrossThePlan()
        {
            var (ways, _, _, _, _, near, far) = Branches();
            var flipped = TreeRows.Of(ways, r => -r.Position.X);
            Assert.True(flipped.AcrossOf(far) < flipped.AcrossOf(near));
        }

        [Fact]
        public void ARoomNoWayReachesStandsInARowOfItsOwnUnderTheRest()
        {
            var entrance = Placed("entrance", 0f, 0f, 0, At(5f, 0f));
            var hall = Placed("hall", 10f, 0f, 1, At(5f, 0f));
            var apart = Placed("apart", 50f, 50f, 2, At(60f, 50f));
            var tree = TreeRows.Of(DungeonWays.Of(new[] { entrance, hall, apart }), r => r.Position.X);
            Assert.Equal(3, tree.Rows);
            Assert.Equal(2, tree.RowOf(apart));
        }

        [Fact]
        public void TheSameWaysLayOutTheSameEachTime()
        {
            var (ways, _, _, _, _, _, _) = Branches();
            var one = TreeRows.Of(ways, r => r.Position.X);
            var two = TreeRows.Of(ways, r => r.Position.X);
            Assert.All(ways.Rooms, r => Assert.Equal(one.AcrossOf(r), two.AcrossOf(r)));
        }

        // ----- What the box says -----

        [Fact]
        public void TheBoxSaysWhatItShows()
        {
            Assert.Equal("Whole dungeon, 12 rooms", PlanBoxWords.Caption(12));
            Assert.Equal("Whole dungeon, 1 room", PlanBoxWords.Caption(1));
            Assert.Equal("3 overlapping", PlanBoxWords.Overlapping(3));
            Assert.Equal("None overlapping", PlanBoxWords.Overlapping(0));
            Assert.Equal("35°", PlanBoxWords.Angle(35.4f));
            Assert.Equal("33°", PlanBoxWords.Angle(32.5f));
            Assert.Equal("0°", PlanBoxWords.Angle(359.6f));
            Assert.Equal("Above", PlanBoxWords.ViewName(PlanView.Above));
            Assert.Equal("Map", PlanBoxWords.ViewName(PlanView.Map));
            Assert.Equal("Tree", PlanBoxWords.ViewName(PlanView.Tree));
        }

        [Fact]
        public void ATreeNodeTellsHowDeepItsRoomIs()
        {
            Assert.Equal("0", PlanBoxWords.Depth(0.3f));
            Assert.Equal("-12", PlanBoxWords.Depth(-12.4f));
            Assert.Equal("+3", PlanBoxWords.Depth(3.2f));
            Assert.Equal("Burial chamber  -12", PlanBoxWords.Node("Burial chamber", "-12"));
        }
    }
}
