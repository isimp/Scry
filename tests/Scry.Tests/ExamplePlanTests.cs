using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class ExamplePlanTests
    {
        // An example's plan in the stage's corner turns with the view, what lies ahead of the
        // camera up on the plan, and is framed on its rooms alone, so a dungeon grown off to one
        // side of its zone fills the plan. With a floor opened, it shows that floor's rooms, those
        // below it faintly and none above it, as the stage shows the example cut open.

        private static PlacedRoom Room(float x, float y, float z, float wide, float high, float deep, float yaw = 0f) =>
            new PlacedRoom { Room = new RoomShape { Name = "room", Size = new Vec3(wide, high, deep) }, Position = new Vec3(x, y, z), Rotation = Quat.Yaw(yaw) };

        [Fact]
        public void LookingNorthThePlanIsNorthUp()
        {
            var (right, up) = ExamplePlan.Turn(3f, 5f, 0f);
            Assert.Equal(3f, right, 4);
            Assert.Equal(5f, up, 4);
        }

        [Fact]
        public void WhatLiesAheadOfTheCameraIsUpOnThePlan()
        {
            // Looking east (+X): east is up, north (+Z) to the left.
            var (right, up) = ExamplePlan.Turn(1f, 0f, 90f);
            Assert.Equal(0f, right, 4);
            Assert.Equal(1f, up, 4);
            (right, up) = ExamplePlan.Turn(0f, 1f, 90f);
            Assert.Equal(-1f, right, 4);
            Assert.Equal(0f, up, 4);

            // Looking south, the plan stands on its head.
            (right, up) = ExamplePlan.Turn(2f, 1f, 180f);
            Assert.Equal(-2f, right, 4);
            Assert.Equal(-1f, up, 4);
        }

        [Fact]
        public void APointOnThePlanLeadsBackToWhereItIs()
        {
            foreach (var yaw in new[] { 0f, 37f, 90f, 200f, -75f })
            {
                var (right, up) = ExamplePlan.Turn(4f, -7f, yaw);
                var (x, z) = ExamplePlan.Back(right, up, yaw);
                Assert.Equal(4f, x, 3);
                Assert.Equal(-7f, z, 3);
            }
        }

        [Fact]
        public void APlanIsFramedOnItsRoomsTurnedAsTheViewIs()
        {
            // Two rooms in a row along x, from -10 to 30: 40 m by 10 m looking north.
            var rooms = new List<PlacedRoom> { Room(0f, 0f, 0f, 20f, 4f, 10f), Room(20f, 0f, 0f, 20f, 4f, 10f) };

            var north = ExamplePlan.Extent(rooms, 0f);
            Assert.Equal(-10f, north.MinRight, 3);
            Assert.Equal(30f, north.MaxRight, 3);
            Assert.Equal(-5f, north.MinUp, 3);
            Assert.Equal(5f, north.MaxUp, 3);

            // Looking east the row runs up the plan: 10 m across, 40 m up.
            var east = ExamplePlan.Extent(rooms, 90f);
            Assert.Equal(10f, east.MaxRight - east.MinRight, 3);
            Assert.Equal(40f, east.MaxUp - east.MinUp, 3);

            Assert.Equal((0f, 0f, 0f, 0f), ExamplePlan.Extent(new List<PlacedRoom>(), 30f));
        }

        [Fact]
        public void ItsSizeOnScreenKeepsWhileTheViewTurns()
        {
            // The plan's scale goes by the widest the rooms are any way round: the farthest corners apart.
            var rooms = new List<PlacedRoom> { Room(0f, 0f, 0f, 20f, 4f, 10f), Room(20f, 0f, 0f, 20f, 4f, 10f) };
            Assert.Equal(Math.Sqrt(40 * 40 + 10 * 10), ExamplePlan.Widest(rooms), 3);
            Assert.Equal(0f, ExamplePlan.Widest(new List<PlacedRoom>()), 3);
        }

        [Fact]
        public void WithAFloorOpenedItsRoomsShowAndThoseBelowFaintly()
        {
            // A room on the ground floor (0 to 4 m), one in the cellar (-5 to -1 m), one upstairs
            // (5 to 9 m), and a stairwell from the cellar to upstairs.
            var ground = Room(0f, 2f, 0f, 10f, 4f, 10f);
            var cellar = Room(0f, -3f, 0f, 10f, 4f, 10f);
            var upstairs = Room(0f, 7f, 0f, 10f, 4f, 10f);
            var stairs = Room(20f, 2f, 0f, 4f, 14f, 4f);

            Assert.Equal(PlanRoomShown.Whole, ExamplePlan.Shown(ground, 0f));
            Assert.Equal(PlanRoomShown.Faint, ExamplePlan.Shown(cellar, 0f));
            Assert.Equal(PlanRoomShown.None, ExamplePlan.Shown(upstairs, 0f));
            Assert.Equal(PlanRoomShown.Whole, ExamplePlan.Shown(stairs, 0f));
            Assert.Equal(PlanRoomShown.Whole, ExamplePlan.Shown(stairs, -5f));

            // A cellar whose ceiling reaches into the floor's slab is still below it.
            Assert.Equal(PlanRoomShown.Faint, ExamplePlan.Shown(Room(0f, -1.95f, 0f, 10f, 4.1f, 10f), 0f));

            // With the roof on, every room shows.
            Assert.Equal(PlanRoomShown.Whole, ExamplePlan.Shown(upstairs, null));
            Assert.Equal(PlanRoomShown.Whole, ExamplePlan.Shown(cellar, null));
        }

        [Fact]
        public void AnEndCapWithNoDepthShowsOnTheFloorItClosesOff()
        {
            // End caps and dividers have no height: one standing on the floor, a little above it, is on it.
            var cap = Room(0f, 1.5f, 0f, 4f, 0f, 0f);
            Assert.Equal(PlanRoomShown.Whole, ExamplePlan.Shown(cap, 0f));
            Assert.Equal(PlanRoomShown.Faint, ExamplePlan.Shown(cap, 6f));

            // One standing level with the floor is on it too.
            Assert.Equal(PlanRoomShown.Whole, ExamplePlan.Shown(Room(0f, 0.1f, 0f, 4f, 0f, 0f), 0f));
        }

        [Fact]
        public void ADoorShowsOnTheFloorItOpensOn()
        {
            Assert.True(ExamplePlan.DoorShown(new Vec3(0f, 0.2f, 0f), 0f));
            Assert.False(ExamplePlan.DoorShown(new Vec3(0f, -4f, 0f), 0f));
            Assert.False(ExamplePlan.DoorShown(new Vec3(0f, 5f, 0f), 0f));
            Assert.True(ExamplePlan.DoorShown(new Vec3(0f, 5f, 0f), null));
        }

        [Fact]
        public void TheRoomUnderAPointIsTheTopmostShownOne()
        {
            var ground = Room(0f, 2f, 0f, 10f, 4f, 10f);
            var upstairs = Room(0f, 7f, 0f, 10f, 4f, 10f);
            var rooms = new List<PlacedRoom> { upstairs, ground };

            Assert.Same(upstairs, ExamplePlan.RoomAt(rooms, 1f, 1f, null));
            Assert.Same(ground, ExamplePlan.RoomAt(rooms, 1f, 1f, 0f));
            Assert.Null(ExamplePlan.RoomAt(rooms, 30f, 1f, 0f));
        }
    }
}
