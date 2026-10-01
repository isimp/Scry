using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PlaceViewTests
    {
        // A location stands on the stage on the ground the game stands it on, its root, whatever of
        // it reaches below (foundations, stones sunk in the earth); a dungeon room on the floor it
        // is walked into on, its lowest doorway, whatever of it reaches below (a pit, its rock).

        private static Doorway At(float y) => new Doorway { Type = "d", Position = new Vec3(0f, y, 4f) };

        private static PlaceContents Room(params float[] doorways)
        {
            var shape = new RoomShape { Name = "room", Size = new Vec3(12f, 16f, 12f) };
            foreach (var y in doorways) shape.Doorways.Add(At(y));
            return new PlaceContents { Room = shape };
        }

        [Fact]
        public void ALocationStandsOnItsRoot()
        {
            Assert.Equal(0f, PlaceView.Ground(new PlaceContents(), room: false));
        }

        [Fact]
        public void ALocationStandsOnItsRootEvenWhenItHasARoomShape()
        {
            Assert.Equal(0f, PlaceView.Ground(Room(-3f), room: false));
        }

        [Fact]
        public void ARoomStandsOnTheFloorItIsWalkedIntoOn()
        {
            Assert.Equal(-6f, PlaceView.Ground(Room(1.5f, -6f, 1.5f, -6f), room: true));
        }

        [Fact]
        public void ARoomWithoutDoorwaysStandsOnItsRoot()
        {
            Assert.Equal(0f, PlaceView.Ground(Room(), room: true));
        }

        [Fact]
        public void ARoomNotReadYetStandsOnItsRoot()
        {
            Assert.Equal(0f, PlaceView.Ground(null, room: true));
            Assert.Equal(0f, PlaceView.Ground(new PlaceContents(), room: true));
        }

        // To look in, the stage cuts away what is above head height over a floor. A room's floors
        // are where its doorways are, from the top down; doorways less than a storey apart (a step,
        // a slope) are one floor, the highest of them, so the cut clears every doorway on it.

        [Fact]
        public void ARoomsFloorsAreItsDoorwaysHeightsFromTheTopDown()
        {
            Assert.Equal(new[] { 1.5f, -6f }, PlaceView.RoomFloors(Room(-6f, 1.5f, 1.5f, -6f).Room));
        }

        [Fact]
        public void DoorwaysLessThanAStoreyApartAreOneFloorTheHighestOfThem()
        {
            Assert.Equal(new[] { 0.5f, -2.5f }, PlaceView.RoomFloors(Room(-3.5f, 0.5f, -0.5f, -2.5f).Room));
            Assert.Equal(new[] { 0f, -2f }, PlaceView.Floors(new[] { 0f, -1.9f, -2f }));
        }

        [Fact]
        public void ARoomWithoutDoorwaysHasOneFloorAtItsRoot()
        {
            Assert.Equal(new[] { 0f }, PlaceView.RoomFloors(Room().Room));
            Assert.Equal(new[] { 0f }, PlaceView.RoomFloors(null));
        }

        [Fact]
        public void TheCutIsAboveHeadHeightOverItsFloor()
        {
            Assert.Equal(2.5f, PlaceView.CutHeight(0f));
            Assert.Equal(-3.5f, PlaceView.CutHeight(-6f));
        }

        [Fact]
        public void UnderALowStoreyTheCutStaysBelowTheFloorAbove()
        {
            // Head height over the floor, or just under the floor above where that is lower, so
            // the storey above is cut away whole; never lower than 1.4 m, to keep what stands on it.
            Assert.Equal(2.5f, PlaceView.CutHeight(0f, 4.2f), 3);
            Assert.Equal(2.4f, PlaceView.CutHeight(0f, 3f), 3);
            Assert.Equal(1.4f, PlaceView.CutHeight(0f, 1.8f), 3);
            Assert.Equal(2.5f, PlaceView.CutHeight(0f, null), 3);
            Assert.Equal(new[] { 10.5f, 6.7f, 2.5f }, PlaceView.CutHeights(new List<float> { 8f, 4.2f, 0f }).Select(c => (float)System.Math.Round(c, 3)));
            Assert.Equal(new[] { 5.5f, 2.4f }, PlaceView.CutHeights(new List<float> { 3f, 0f }).Select(c => (float)System.Math.Round(c, 3)));
        }

        [Theory]
        [InlineData(3, 3, true, 0)]
        [InlineData(0, 3, true, 1)]
        [InlineData(2, 3, true, 2)]
        [InlineData(2, 3, false, 1)]
        [InlineData(0, 3, false, 3)]
        [InlineData(3, 3, false, 3)]
        [InlineData(1, 1, true, 0)]
        [InlineData(0, 1, false, 1)]
        public void TheCutStepsAFloorDownOrUpWithTheRoofAboveTheTopFloor(int level, int floors, bool down, int next)
        {
            Assert.Equal(next, PlaceView.StepCut(level, floors, down));
        }

        [Fact]
        public void ALocationsGroundIsAFloorEvenWhereNothingButTheWorldsGroundIsThere()
        {
            // A location stands on its root; its ground floor is often bare earth, the world's own,
            // which no ray finds in the location.
            Assert.Equal(new[] { 8f, 4f, 0f }, PlaceView.WithGround(new List<float> { 8f, 4f }));
            Assert.Equal(new[] { 8f, 4f, 0.2f }, PlaceView.WithGround(new List<float> { 8f, 4f, 0.2f }));
            Assert.Equal(new[] { 1.5f }, PlaceView.WithGround(new List<float> { 1.5f }));
            Assert.Equal(new[] { 3f, 0f, -6f }, PlaceView.WithGround(new List<float> { 3f, -6f }));
            Assert.Equal(new[] { 0f }, PlaceView.WithGround(new List<float>()));
        }

        [Theory]
        [InlineData(9f, 0)]
        [InlineData(8.6f, 0)]
        [InlineData(8.4f, 1)]
        [InlineData(6f, 1)]
        [InlineData(4.3f, 2)]
        [InlineData(1f, 2)]
        [InlineData(-3f, 2)]
        public void ACutSetByHandOpensTheFloorItIsOver(float height, int level)
        {
            // Over a floor by half a metre at least; below them all, the lowest.
            Assert.Equal(level, PlaceView.LevelAt(new List<float> { 8f, 4f, 0f }, height));
        }

        [Theory]
        [InlineData(0, 1, "Roof off")]
        [InlineData(1, 1, "Roof on")]
        [InlineData(0, 3, "Roof off, top floor")]
        [InlineData(1, 3, "Roof off, floor 2 of 3")]
        [InlineData(2, 3, "Roof off, floor 3 of 3")]
        [InlineData(3, 3, "Roof on")]
        public void TheCutSaysWhichFloorItOpens(int level, int floors, string said)
        {
            Assert.Equal(said, PlaceView.CutLabel(level, floors));
        }

        [Theory]
        [InlineData(0, 3, 14, "Top floor, 14 rooms")]
        [InlineData(1, 3, 1, "Floor 2 of 3, 1 room")]
        [InlineData(2, 3, -1, "Floor 3 of 3")]
        [InlineData(1, 3, 0, "Floor 2 of 3, no rooms")]
        [InlineData(0, 1, 6, "6 rooms")]
        [InlineData(0, 1, -1, null)]
        [InlineData(3, 3, 14, null)]
        public void TheFloorOpenedIsNamedByTheRulerWithItsRooms(int level, int floors, int rooms, string said)
        {
            // Rooms are counted only for an example; with the roof on nothing is named.
            Assert.Equal(said, PlaceView.FloorLabel(level, floors, rooms));
        }
    }
}
