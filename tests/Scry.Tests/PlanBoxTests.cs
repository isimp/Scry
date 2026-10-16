using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PlanBoxTests
    {
        /// <summary>A room 4 m each way, centred at a point, turned as placed.</summary>
        private static PlacedRoom Room(float x, float y, float z, string name = "room") =>
            new PlacedRoom { Room = new RoomShape { Name = name, Size = new Vec3(4f, 4f, 4f) }, Position = new Vec3(x, y, z) };

        // ----- The map: each room across against its depth, at any angle -----

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
        public void TwoRoomsOverlapOnTheMapOnlyWhereTheyShareBothSpotAcrossAndHeight()
        {
            var a = Room(0f, 0f, 0f);
            var sameSpot = Room(1f, 0f, 0f);
            var below = Room(1f, -10f, 0f);
            var beside = Room(4f, 0f, 0f);
            Assert.Equal(1, PlanMap.Overlaps(new[] { a, sameSpot }, 0f));
            Assert.Equal(0, PlanMap.Overlaps(new[] { a, below }, 0f));
            Assert.Equal(0, PlanMap.Overlaps(new[] { below, a }, 0f));
            // One standing right on top of another is not over it either.
            Assert.Equal(0, PlanMap.Overlaps(new[] { a, Room(1f, 4f, 0f) }, 0f));
            Assert.Equal(0, PlanMap.Overlaps(new[] { Room(1f, 4f, 0f), a }, 0f));
            // Wall to wall is not over each other.
            Assert.Equal(0, PlanMap.Overlaps(new[] { a, beside }, 0f));
        }

        [Fact]
        public void HalfATurnRoundShowsAsManyOverlapping()
        {
            var rooms = new[] { Room(0f, 0f, 0f), Room(6f, -1f, 3f), Room(3f, 0f, 9f), Room(-5f, -2f, 4f), Room(8f, 1f, -6f) };
            for (var angle = 0f; angle < 180f; angle += 5f)
            {
                Assert.Equal(PlanMap.Overlaps(rooms, angle), PlanMap.Overlaps(rooms, angle + 180f));
            }
        }

        /// <summary>Three rooms 10 m apart in a line north, at one height.</summary>
        private static PlacedRoom[] Row() => new[] { Room(0f, 0f, 0f), Room(0f, 0f, 10f), Room(0f, 0f, 20f) };

        [Fact]
        public void LeastOverlapTurnsTheMapToTheNearestAngleWhereTheFewestRoomsCoverEachOther()
        {
            // Facing along the row every room covers the others; from 35 degrees round they stand apart.
            Assert.Equal(3, PlanMap.Overlaps(Row(), 0f));
            Assert.Equal(0, PlanMap.Overlaps(Row(), 35f));
            Assert.True(PlanMap.Overlaps(Row(), 30f) > 0);
            Assert.Equal(35f, PlanMap.LeastOverlap(Row(), 0f));
        }

        [Fact]
        public void OnATieTheAngleNearestTheOneShownIsKept()
        {
            // 100 degrees already shows none overlapping: the map stays where it was put.
            Assert.Equal(100f, PlanMap.LeastOverlap(Row(), 100f));
            Assert.Equal(100f, PlanMap.LeastOverlap(Row(), 102f));
            // The far side of the turn is as good, and nearer the angle shown.
            Assert.Equal(215f, PlanMap.LeastOverlap(Row(), 200f));
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
        public void TheMapStandsTheGroundAtTheTopAndTheDeepestRoomAtTheBottom()
        {
            var rooms = new[] { Room(0f, -2f, 0f), Room(5f, -20f, 0f), Room(9f, 1f, 0f) };
            var (top, bottom) = PlanMap.Heights(rooms);
            Assert.Equal(3f, top, 3);
            Assert.Equal(-22f, bottom, 3);
        }

        private static (float, float) Rounded((float Min, float Max) span) => ((float)Math.Round(span.Min, 3), (float)Math.Round(span.Max, 3));

        private static float Middle((float Min, float Max) span) => (span.Min + span.Max) / 2f;

        // ----- The tree: rooms at their spots on the plan, pushed apart only where they crowd -----

        [Fact]
        public void NodesTooCloseArePushedApartUntilAGapLiesBetweenEach()
        {
            var homes = Enumerable.Repeat((100f, 75f), 5).ToList();
            var laid = PinnedLayout.Of(homes, 24f, 0f, 0f, 220f, 150f);
            AllApart(laid, 24f);
        }

        [Fact]
        public void ManyRoomsOnOneSpotSpreadBothWaysWhereARowWouldNotFit()
        {
            // Twelve nodes a gap apart in a row would need 264 pixels; the box is 220 wide.
            var homes = Enumerable.Repeat((110f, 75f), 12).ToList();
            var laid = PinnedLayout.Of(homes, 24f, 0f, 0f, 220f, 150f);
            AllApart(laid, 24f);
        }

        [Fact]
        public void ANodeWithRoomToSpareStaysOnItsSpot()
        {
            var homes = new List<(float, float)> { (20f, 20f), (200f, 130f), (100f, 75f), (102f, 75f) };
            var laid = PinnedLayout.Of(homes, 24f, 0f, 0f, 220f, 150f);
            Assert.Equal((20f, 20f), laid[0]);
            Assert.Equal((200f, 130f), laid[1]);
            AllApart(laid, 24f);
        }

        [Fact]
        public void NodesStayInsideTheBox()
        {
            var homes = new List<(float, float)> { (-50f, 300f), (5f, 5f), (5f, 5f), (5f, 5f), (230f, -10f) };
            var laid = PinnedLayout.Of(homes, 24f, 0f, 0f, 220f, 150f);
            foreach (var (x, y) in laid)
            {
                Assert.InRange(x, 0f, 220f);
                Assert.InRange(y, 0f, 150f);
            }
            AllApart(laid, 24f);
        }

        [Fact]
        public void TheSameRoomsAreLaidOutTheSameEachTime()
        {
            var homes = new List<(float, float)> { (50f, 50f), (52f, 51f), (55f, 49f), (51f, 53f), (120f, 90f) };
            Assert.Equal(PinnedLayout.Of(homes, 24f, 0f, 0f, 220f, 150f), PinnedLayout.Of(homes, 24f, 0f, 0f, 220f, 150f));
        }

        [Fact]
        public void ANodeMovedOffItsSpotIsToldSo()
        {
            Assert.False(PinnedLayout.Moved((10f, 10f), (10.5f, 10f)));
            Assert.True(PinnedLayout.Moved((10f, 10f), (14f, 10f)));
        }

        private static void AllApart(IReadOnlyList<(float X, float Y)> laid, float gap)
        {
            for (var a = 0; a < laid.Count; a++)
            {
                for (var b = a + 1; b < laid.Count; b++)
                {
                    var d = Math.Sqrt(Math.Pow(laid[a].X - laid[b].X, 2) + Math.Pow(laid[a].Y - laid[b].Y, 2));
                    Assert.True(d >= gap - 0.5f, $"nodes {a} and {b} only {d:0.0} apart");
                }
            }
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
