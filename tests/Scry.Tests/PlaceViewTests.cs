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
    }
}
