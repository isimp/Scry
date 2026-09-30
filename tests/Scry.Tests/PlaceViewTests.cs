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
    }
}
