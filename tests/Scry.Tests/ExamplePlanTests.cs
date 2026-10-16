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

        /// <summary>A room on its floors, where it stands as the stage reads it, with doorways where given.</summary>
        private static PlacedRoom On(string name, float y, float high, bool endCap = false, params Vec3[] doorways)
        {
            var shape = new RoomShape { Name = name, Size = new Vec3(10f, high, 10f), EndCap = endCap };
            foreach (var at in doorways) shape.Doorways.Add(new Doorway { Position = at - new Vec3(0f, y, 0f) });
            return new PlacedRoom { Room = shape, Position = new Vec3(0f, y, 0f) };
        }

        [Fact]
        public void OnAFloorThePlanTilesItsOwnRoomsAndDashesThoseOfTheFloorBelow()
        {
            // Floors from the top down. A tall hall standing on the lowest floor reaches up
            // through the middle one: on the middle floor's plan it is beneath, dashed, not a
            // room of that floor. An end cap reaching the floor closes its rooms, so it shows.
            var floors = new[] { 8f, 4f, 0f };
            var hall = On("hall", 5.5f, 3f);
            var attic = On("attic", 9.5f, 3f);
            var cellar = On("cellar", 1.5f, 3f);
            var tall = On("tall", 4f, 8f);
            var cap = On("cap", 5f, 4f, endCap: true);
            var lowCap = On("lowcap", 1f, 2f, endCap: true);
            var stands = new Dictionary<PlacedRoom, float[]> { { hall, new[] { 4f } }, { attic, new[] { 8f } }, { cellar, new[] { 0f } }, { tall, new[] { 0f } } };
            bool StandsOn(PlacedRoom room, float floor) => stands.TryGetValue(room, out var on) && on.Contains(floor);

            Assert.Equal(PlanTile.On, ExamplePlan.Tile(hall, floors, 1, StandsOn));
            Assert.Equal(PlanTile.Below, ExamplePlan.Tile(tall, floors, 1, StandsOn));
            Assert.Equal(PlanTile.Below, ExamplePlan.Tile(cellar, floors, 1, StandsOn));
            Assert.Equal(PlanTile.Off, ExamplePlan.Tile(attic, floors, 1, StandsOn));
            Assert.Equal(PlanTile.On, ExamplePlan.Tile(cap, floors, 1, StandsOn));
            Assert.Equal(PlanTile.Off, ExamplePlan.Tile(lowCap, floors, 1, StandsOn));
            var divider = On("divider", 5f, 4f);
            divider.Room.Divider = true;
            Assert.Equal(PlanTile.On, ExamplePlan.Tile(divider, floors, 1, StandsOn));

            // The top floor dashes the middle one's rooms; the lowest has nothing under it.
            Assert.Equal(PlanTile.On, ExamplePlan.Tile(attic, floors, 0, StandsOn));
            Assert.Equal(PlanTile.Below, ExamplePlan.Tile(hall, floors, 0, StandsOn));
            Assert.Equal(PlanTile.Off, ExamplePlan.Tile(cellar, floors, 0, StandsOn));
            Assert.Equal(PlanTile.On, ExamplePlan.Tile(cellar, floors, 2, StandsOn));
            Assert.Equal(PlanTile.Off, ExamplePlan.Tile(hall, floors, 2, StandsOn));

            // With the roof on every room shows.
            foreach (var room in new[] { hall, attic, cellar, tall, lowCap }) Assert.Equal(PlanTile.On, ExamplePlan.Tile(room, floors, floors.Length, StandsOn));
        }

        [Fact]
        public void ARoomWithNoFloorOfItsOwnShowsWhereItsBoxReaches()
        {
            // A tunnel sloping from one floor to the next, or a cave passage of rock, has no flat
            // ground of its own anywhere: it shows on the floors its box reaches, as a room did
            // before its rays were read, tiled where it reaches the floor opened, dashed under it.
            var floors = new[] { 8f, 4f, 0f };
            var tunnel = On("tunnel", 4.5f, 3f);
            var ramp = On("ramp", 4f, 6f);
            bool StandsOn(PlacedRoom room, float floor) => false;

            Assert.Equal(PlanTile.On, ExamplePlan.Tile(tunnel, floors, 1, StandsOn));
            Assert.Equal(PlanTile.Below, ExamplePlan.Tile(tunnel, floors, 0, StandsOn));
            Assert.Equal(PlanTile.Off, ExamplePlan.Tile(tunnel, floors, 2, StandsOn));
            Assert.Equal(PlanTile.On, ExamplePlan.Tile(ramp, floors, 1, StandsOn));
            Assert.Equal(PlanTile.On, ExamplePlan.Tile(ramp, floors, 2, StandsOn));

            // A doorway from a hall of the middle floor into a tunnel reaching only the floor under it leads down.
            var door = new Vec3(5f, 4f, 0f);
            var hall = On("hall", 5.5f, 3f, false, door);
            var shaft = On("shaft", 1f, 2f, false, door);
            bool HallOnly(PlacedRoom room, float floor) => room == hall && floor == 4f;
            Assert.Equal(DoorLeads.Down, ExamplePlan.Leads(door, new List<PlacedRoom> { hall, shaft }, floors, 1, HallOnly));
            // One its box still reaches on the floor opened leads along it.
            var passage = On("passage", 4f, 4f, false, door);
            Assert.Equal(DoorLeads.Level, ExamplePlan.Leads(door, new List<PlacedRoom> { hall, passage }, floors, 1, HallOnly));
            // From a passage on the floor opened by its box alone, a doorway down into the shaft leads down too.
            Assert.Equal(DoorLeads.Down, ExamplePlan.Leads(door, new List<PlacedRoom> { passage, shaft }, floors, 1, StandsOn));
        }

        [Fact]
        public void OnTheStageTheFloorsOwnRoomsStandAndTheRestAreDimmedOrPutAway()
        {
            // By the plan's rule: a room on the floor opened stands as it is; a tall room from the
            // floor under it reaching up through it is dimmed with the rooms below; a room above
            // it is put away. With the roof on, every room stands.
            var hall = On("hall", 5.5f, 3f);
            var tall = On("tall", 4f, 8f);
            var cellar = On("cellar", 1.5f, 3f);
            var attic = On("attic", 9.5f, 3f);
            var tunnel = On("tunnel", 4.5f, 3f);
            var stands = new Dictionary<PlacedRoom, float> { { hall, 4f }, { tall, 0f }, { cellar, 0f }, { attic, 8f } };
            bool StandsOn(PlacedRoom room, float floor) => stands.TryGetValue(room, out var on) && on == floor;

            Assert.Equal(PlanRoomShown.Whole, ExamplePlan.OnStage(hall, 4f, true, StandsOn));
            Assert.Equal(PlanRoomShown.Faint, ExamplePlan.OnStage(tall, 4f, true, StandsOn));
            Assert.Equal(PlanRoomShown.Faint, ExamplePlan.OnStage(cellar, 4f, true, StandsOn));
            Assert.Equal(PlanRoomShown.None, ExamplePlan.OnStage(attic, 4f, true, StandsOn));
            // A room with no floor of its own anywhere stands where its box reaches.
            Assert.Equal(PlanRoomShown.Whole, ExamplePlan.OnStage(tunnel, 4f, false, StandsOn));
            Assert.Equal(PlanRoomShown.None, ExamplePlan.OnStage(tunnel, 0f, false, StandsOn));
            Assert.Equal(PlanRoomShown.Whole, ExamplePlan.OnStage(attic, null, true, StandsOn));
        }

        [Fact]
        public void ADoorwayToARoomOnAnotherFloorIsMarkedUpOrDown()
        {
            var floors = new[] { 8f, 4f, 0f };
            var stairDoor = new Vec3(5f, 4f, 0f);
            var sideDoor = new Vec3(-5f, 4f, 0f);
            var hall = On("hall", 5.5f, 3f, false, stairDoor, sideDoor);
            var stairs = On("stairs", 2f, 5f, false, stairDoor);
            var side = On("side", 5.5f, 3f, false, sideDoor);
            var lost = On("lost", 9f, 2f, false, new Vec3(0f, 9f, 9f));
            var attic = On("attic", 9.5f, 3f, false, new Vec3(0f, 9f, 9f));
            var stands = new Dictionary<PlacedRoom, float[]> { { hall, new[] { 4f } }, { stairs, new[] { 0f } }, { side, new[] { 4f } }, { attic, new[] { 8f } } };
            bool StandsOn(PlacedRoom room, float floor) => stands.TryGetValue(room, out var on) && on.Contains(floor);
            var rooms = new List<PlacedRoom> { hall, stairs, side, lost, attic };

            Assert.Equal(DoorLeads.Down, ExamplePlan.Leads(stairDoor, rooms, floors, 1, StandsOn));
            Assert.Equal(DoorLeads.Up, ExamplePlan.Leads(stairDoor, rooms, floors, 2, StandsOn));
            Assert.Equal(DoorLeads.Level, ExamplePlan.Leads(sideDoor, rooms, floors, 1, StandsOn));
            // A doorway no room of the floor opened stands at leads nowhere from it.
            Assert.Equal(DoorLeads.Level, ExamplePlan.Leads(new Vec3(0f, 9f, 9f), rooms, floors, 1, StandsOn));
            // A room joined there standing on no floor at all says nothing of where it leads.
            Assert.Equal(DoorLeads.Level, ExamplePlan.Leads(new Vec3(0f, 9f, 9f), rooms, floors, 0, StandsOn));
            // With the roof on no doorway is marked.
            Assert.Equal(DoorLeads.Level, ExamplePlan.Leads(stairDoor, rooms, floors, floors.Length, StandsOn));

            // A shaft standing on floors both above and below leads to the nearer one.
            var four = new[] { 12f, 8f, 4f, 0f };
            var landing = new Vec3(5f, 8f, 0f);
            var gallery = On("gallery", 9.5f, 3f, false, landing);
            var shaft = On("shaft", 6f, 14f, false, landing);
            var levels = new Dictionary<PlacedRoom, float[]> { { gallery, new[] { 8f } }, { shaft, new[] { 12f, 0f } } };
            bool Level(PlacedRoom room, float floor) => levels.TryGetValue(room, out var on) && on.Contains(floor);
            Assert.Equal(DoorLeads.Up, ExamplePlan.Leads(landing, new List<PlacedRoom> { gallery, shaft }, four, 1, Level));
        }

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
