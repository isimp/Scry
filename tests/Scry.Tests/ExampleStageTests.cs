using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class ExampleStageTests
    {
        // An example dungeon or camp stands on the stage from its generator: the generator where the
        // stage's copy has it, unturned, every room where it stands from there. The mouse points at
        // the room its ray meets first, below the cut that opens the example, and the example's
        // floors are where its doorways are.

        private static Doorway Door(float x, float y, float z, float yaw, bool entrance = false) =>
            new Doorway { Type = "d", Position = new Vec3(x, y, z), Rotation = Quat.Yaw(yaw), Entrance = entrance };

        private static RoomShape Entrance() => new RoomShape
        {
            Name = "entrance", Entrance = true, Size = new Vec3(8f, 4f, 8f),
            Doorways = { Door(0f, -1f, 4f, 0f), Door(0f, -1f, -4f, 180f, entrance: true) },
        };

        private static RoomShape Hall() => new RoomShape
        {
            Name = "hall", Size = new Vec3(8f, 4f, 8f),
            Doorways = { Door(0f, -1f, -4f, 180f), Door(0f, -1f, 4f, 0f), Door(4f, -1f, 0f, 90f), Door(-4f, -1f, 0f, 270f) },
        };

        private static RoomShape Box(string name, float x, float y, float z) => new RoomShape { Name = name, Size = new Vec3(x, y, z) };

        private static PlacedRoom At(RoomShape room, float x, float y, float z, float yaw = 0f) =>
            new PlacedRoom { Room = room, Position = new Vec3(x, y, z), Rotation = Quat.Yaw(yaw) };

        private static void Near(Vec3 expected, Vec3 actual) => Assert.True(Vec3.Distance(expected, actual) < 0.01f, $"expected {expected}, was {actual}");

        /// <summary>Whether two turns in degrees are the same turn, 0 and 360 alike.</summary>
        private static void SameTurn(float expected, float actual)
        {
            var off = ((actual - expected) % 360f + 540f) % 360f - 180f;
            Assert.True(System.Math.Abs(off) < 0.1f, $"expected a turn of {expected}, was {actual}");
        }

        [Fact]
        public void TheExampleStandsFromItsGeneratorUnturned()
        {
            var site = new DungeonSite { Generator = new Vec3(10f, 5000f, -4f), Turn = Quat.Yaw(90f), ZoneSize = new Vec3(1000f, 1000f, 1000f) };
            for (var seed = 1; seed <= 20; seed++)
            {
                var example = DungeonLayout.Build(new DungeonPlan { MaxRooms = 8 }, new[] { Entrance(), Hall() }, site, new Dice(seed));
                var staged = example.FromGenerator();

                // The entrance's own doorway is where the generator stands, facing as it did unturned.
                var entrance = staged.Rooms[0];
                var door = entrance.Room.Doorways.FindIndex(d => d.Entrance);
                Near(new Vec3(0f, 0f, 0f), entrance.DoorwayAt(door));
                SameTurn(0f, entrance.DoorwayTurn(door).YawDegrees);

                // Every room keeps where it stands from every other, and its turn from theirs.
                Assert.Equal(example.Rooms.Count, staged.Rooms.Count);
                for (var i = 1; i < example.Rooms.Count; i++)
                {
                    var was = Vec3.Distance(example.Rooms[i].Position, example.Rooms[0].Position);
                    Assert.Equal(was, Vec3.Distance(staged.Rooms[i].Position, staged.Rooms[0].Position), 2);
                    SameTurn(example.Rooms[i].Rotation.YawDegrees - 90f, staged.Rooms[i].Rotation.YawDegrees);
                    Assert.Same(example.Rooms[i].Room, staged.Rooms[i].Room);
                }
                Assert.Equal(example.Doors.Count, staged.Doors.Count);
            }
        }

        [Fact]
        public void AnUnturnedExampleIsOnlyMovedToItsGenerator()
        {
            var example = new DungeonExample
            {
                Site = new DungeonSite { Generator = new Vec3(3f, 100f, 3f) },
                Rooms = { At(Box("a", 8f, 4f, 8f), 3f, 100f, 13f, 45f) },
                Doors = { new Vec3(3f, 99f, 9f) },
            };

            var staged = example.FromGenerator();

            Near(new Vec3(0f, 0f, 10f), staged.Rooms[0].Position);
            SameTurn(45f, staged.Rooms[0].Rotation.YawDegrees);
            Near(new Vec3(0f, -1f, 6f), staged.Doors[0]);
        }

        [Fact]
        public void TheExamplesFloorsAreWhereItsDoorwaysAre()
        {
            var example = new DungeonExample { Rooms = { At(Hall(), 0f, 0f, 0f), At(Hall(), 0f, -4f, 8f), At(Hall(), 0f, -4.5f, 16f) } };

            Assert.Equal(new[] { -1f, -5f }, PlaceView.ExampleFloors(example));
        }

        [Fact]
        public void TheMousePointsAtTheNearestRoomItsRayMeets()
        {
            var upper = At(Box("upper", 8f, 4f, 8f), 0f, 0f, 0f);
            var lower = At(Box("lower", 8f, 4f, 8f), 0f, -8f, 0f);
            var example = new DungeonExample { Rooms = { lower, upper } };

            Assert.Same(upper, example.Pick(new Vec3(1f, 50f, 1f), new Vec3(0f, -1f, 0f)));
            Assert.Same(upper, example.Pick(new Vec3(-30f, 1f, 1f), new Vec3(1f, 0f, 0f)));
            Assert.Null(example.Pick(new Vec3(20f, 50f, 1f), new Vec3(0f, -1f, 0f)));
            Assert.Null(example.Pick(new Vec3(1f, 50f, 1f), new Vec3(0f, 1f, 0f)));
        }

        [Fact]
        public void ARoomCutAwayIsPointedThroughToTheOneBelow()
        {
            var upper = At(Box("upper", 8f, 4f, 8f), 0f, 0f, 0f);
            var lower = At(Box("lower", 8f, 4f, 8f), 0f, -8f, 0f);
            var example = new DungeonExample { Rooms = { upper, lower } };
            var down = new Vec3(0f, -1f, 0f);

            // Cut through the upper room, what is left of it still takes the pointer.
            Assert.Same(upper, example.Pick(new Vec3(1f, 50f, 1f), down, below: 1f));
            // Cut below it, the pointer goes on to the room under it.
            Assert.Same(lower, example.Pick(new Vec3(1f, 50f, 1f), down, below: -3f));
            // A ray running level above the cut meets nothing.
            Assert.Null(example.Pick(new Vec3(-30f, 1f, 1f), new Vec3(1f, 0f, 0f), below: -3f));

            // Looking up from under the example, the ray stops at the cut.
            var alone = new DungeonExample { Rooms = { upper } };
            Assert.Null(alone.Pick(new Vec3(1f, -50f, 1f), new Vec3(0f, 1f, 0f), below: -3f));
            Assert.Same(upper, alone.Pick(new Vec3(1f, -50f, 1f), new Vec3(0f, 1f, 0f), below: 1f));
        }

        [Fact]
        public void ARoomPutAwayIsNotPointedAt()
        {
            // With a floor opened, the rooms of other floors are put away: the pointer goes past them.
            var upper = At(Box("upper", 8f, 4f, 8f), 0f, 0f, 0f);
            var lower = At(Box("lower", 8f, 4f, 8f), 0f, -8f, 0f);
            var example = new DungeonExample { Rooms = { upper, lower } };
            var down = new Vec3(0f, -1f, 0f);

            Assert.Same(lower, example.Pick(new Vec3(1f, 50f, 1f), down, shown: r => r != upper));
            Assert.Null(example.Pick(new Vec3(1f, 50f, 1f), down, shown: r => false));
        }

        [Fact]
        public void ATurnedRoomIsMetWhereItStands()
        {
            var long_ = At(Box("long", 2f, 4f, 10f), 0f, 0f, 0f, 90f);
            var example = new DungeonExample { Rooms = { long_ } };
            var down = new Vec3(0f, -1f, 0f);

            Assert.Same(long_, example.Pick(new Vec3(4f, 10f, 0f), down));
            Assert.Null(example.Pick(new Vec3(0f, 10f, 4f), down));

            // Across its turned length, and along it beside it.
            Assert.Same(long_, example.Pick(new Vec3(4f, 0f, -30f), new Vec3(0f, 0f, 1f)));
            Assert.Null(example.Pick(new Vec3(-30f, 0f, 4f), new Vec3(1f, 0f, 0f)));
        }

        [Fact]
        public void AnEndCapWithoutDepthCanStillBePointedAt()
        {
            var cap = At(Box("cap", 4f, 4f, 0f), 0f, 0f, 0f);
            var example = new DungeonExample { Rooms = { cap } };

            Assert.Same(cap, example.Pick(new Vec3(1f, 0f, -10f), new Vec3(0f, 0f, 1f)));
            Assert.Same(cap, example.Pick(new Vec3(1f, 10f, 0.2f), new Vec3(0f, -1f, 0f)));
        }
    }
}
