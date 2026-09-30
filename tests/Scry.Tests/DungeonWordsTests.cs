using Xunit;

namespace Scry.Tests
{
    public class DungeonWordsTests
    {
        // DungeonGenerator: a dungeon grows room by room from its entrance, up to m_maxRooms tries
        // (PlaceRooms), stopping early only once enough required rooms are in; a camp is laid on a
        // grid (GenerateCampGrid) or scattered in a ring with a wall (GenerateCampRadial).

        [Fact]
        public void ADungeonGrowsFromItsEntranceForSoManyTries()
        {
            var plan = new DungeonPlan { Algorithm = "Dungeon", MinRooms = 20, MaxRooms = 40 };
            Assert.Equal("room by room from its entrance, 40 tries at a room, then end caps on every open doorway", DungeonWords.Layout(plan));
        }

        [Fact]
        public void ADungeonWithRequiredRoomsStopsOnceTheyAreIn()
        {
            var plan = new DungeonPlan { Algorithm = "Dungeon", MinRooms = 15, MaxRooms = 30, MinRequiredRooms = 1 };
            plan.RequiredRooms.Add("dvergrtown_boss");
            Assert.Equal("room by room from its entrance, up to 30 tries at a room, stopping once 1 of its required rooms and over 15 rooms are in, then end caps on every open doorway", DungeonWords.Layout(plan));
        }

        [Fact]
        public void AGridCampSaysItsGridAndHowLikelyEachSquare()
        {
            var plan = new DungeonPlan { Algorithm = "CampGrid", GridSize = 5, TileWidth = 10f, SpawnChance = 0.8f };
            Assert.Equal("on a 5 × 5 grid of 10 m squares, each built on 80% of the time", DungeonWords.Layout(plan));
        }

        [Fact]
        public void ARingCampSaysHowManyRoomsHowFarOutAndItsWall()
        {
            var plan = new DungeonPlan { Algorithm = "CampRadial", MinRooms = 15, MaxRooms = 25, CampRadiusMin = 20f, CampRadiusMax = 30f, PerimeterSections = 20 };
            Assert.Equal("15–24 rooms scattered within a ring of 20–30 m, then a wall of up to 20 sections round it", DungeonWords.Layout(plan));
        }

        [Fact]
        public void RoomsArePickedAlikeOrByWeight()
        {
            Assert.Equal("any room that fits the doorway, all alike", DungeonWords.Picks(new DungeonPlan { Algorithm = "Dungeon" }));
            Assert.Equal("by their weights, among those that fit the doorway", DungeonWords.Picks(new DungeonPlan { Algorithm = "Dungeon", Weighted = true }));
            Assert.Equal("by their weights", DungeonWords.Picks(new DungeonPlan { Algorithm = "CampGrid" }));
        }

        [Fact]
        public void DoorsSayHowOftenADoorwayGetsOne()
        {
            var plan = new DungeonPlan { DoorChance = 0.5f };
            plan.Doors.Add(("stone", 0f));
            Assert.Equal("50% of doorways", DungeonWords.Doors(plan));
            Assert.Null(DungeonWords.Doors(new DungeonPlan { DoorChance = 0.5f }));
        }

        [Fact]
        public void ARoomSaysItsSizeAndWhatKindOfRoomItIs()
        {
            var room = new RoomShape { Size = new Vec3(8f, 4f, 16f), EndCap = true };
            Assert.Equal("8 × 16 m, 4 m high", DungeonWords.Size(room));
            Assert.Equal("an end cap, closing a doorway nothing else took", DungeonWords.Role(room));
            Assert.Equal("an entrance, where the dungeon starts", DungeonWords.Role(new RoomShape { Entrance = true }));
            Assert.Equal("a room", DungeonWords.Role(new RoomShape()));
        }

        [Fact]
        public void ARoomsDoorwaysAreCountedByType()
        {
            var room = new RoomShape();
            room.Doorways.Add(new Doorway { Type = "crypt" });
            room.Doorways.Add(new Doorway { Type = "crypt" });
            room.Doorways.Add(new Doorway { Type = "large" });
            Assert.Equal("3: 2 crypt, 1 large", DungeonWords.Doorways(room));
            Assert.Equal("none", DungeonWords.Doorways(new RoomShape()));
        }
    }
}
