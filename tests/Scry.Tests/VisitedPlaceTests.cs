using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class VisitedPlaceTests
    {
        // A place the game builds rooms for in the world itself, as Hildir's Sealed Tower, is no
        // dungeon below the world: it is walked into, not entered through a door to rooms high above.
        // So it gets no dungeon's entrance mark, and once you stood in it, a mark of its own on the
        // map: you were there, with no rooms counted, opening its page.

        [Fact]
        public void APlaceWhoseRoomsStandInTheWorldDoesNotLieInside()
        {
            // The game builds a dungeon's rooms about 5,000 m over its entrance, or at its zone's own interior spot.
            Assert.True(new DungeonPlan { GeneratorAt = new Vec3(0f, 5000f, 0f) }.LiesInside);
            Assert.True(new DungeonPlan { CustomInterior = true }.LiesInside);
            // The Sealed Tower's generator stands in the tower itself.
            Assert.False(new DungeonPlan { GeneratorAt = new Vec3(0f, 0.5f, 0f) }.LiesInside);
            Assert.False(new DungeonPlan { GeneratorAt = new Vec3(3f, -12f, 4f) }.LiesInside);
        }

        [Fact]
        public void StandingInSuchAPlaceKeepsAVisitByItsPlaceAndDay()
        {
            var maps = new DungeonMaps();
            var id = DungeonMaps.EntranceIdOf("w", 100.4f, -200.6f);
            Assert.True(maps.Visited(id, "Hildir_plainsfortress", new Vec3(100.4f, 30f, -200.6f), 393));
            var map = maps.Of(id);
            Assert.Equal("Hildir_plainsfortress", map.Location);
            Assert.Empty(map.Rooms);
            Assert.Equal((393, 393), (map.FirstDay, map.LastDay));
            // Again the next day: its last day moves on, nothing else.
            Assert.True(maps.Visited(id, "Hildir_plainsfortress", new Vec3(100.4f, 30f, -200.6f), 395));
            Assert.Equal((393, 395), (map.FirstDay, map.LastDay));
            Assert.False(maps.Visited(id, "Hildir_plainsfortress", new Vec3(100.4f, 30f, -200.6f), 395));
        }

        [Fact]
        public void AVisitIsKeptWithItsDaysAcrossSessions()
        {
            var maps = new DungeonMaps();
            var id = DungeonMaps.EntranceIdOf("w", 10f, 20f);
            maps.Visited(id, "Hildir_plainsfortress", new Vec3(10f, 1f, 20f), 393);
            maps.Visited(id, "Hildir_plainsfortress", new Vec3(10f, 1f, 20f), 396);
            var read = DungeonMaps.Load(maps.ToSave()());
            var map = read.Of(id);
            Assert.NotNull(map);
            Assert.Empty(map.Rooms);
            Assert.Equal((393, 396), (map.FirstDay, map.LastDay));
        }

        [Fact]
        public void AVisitedPlaceIsMarkedAsYouWereThere()
        {
            var maps = new DungeonMaps();
            var id = DungeonMaps.EntranceIdOf("w", 10f, 20f);
            maps.Visited(id, "Hildir_plainsfortress", new Vec3(10f, 1f, 20f), 393);
            var marks = DungeonMarks.Of(new CompendiumBook(), maps.OfWorld("w"), _ => new (string, DungeonMap)[0], _ => null);
            Assert.Equal(MarkLook.Visited, marks.Single().Look);
            // Shown as yours, whole, on the small map too.
            Assert.True(MapMarks.Shown(MarkLook.Visited, walkedByYou: true, walkedOnServer: false, entrances: false));
            Assert.False(MapMarks.Shown(MarkLook.Visited, walkedByYou: false, walkedOnServer: true, entrances: true));
            Assert.Equal(1f, MapMarks.Alpha(MarkLook.Visited, pointedAt: false), 3);
            Assert.True(MapMarks.OnSmallMap(MarkLook.Visited));
        }

        [Fact]
        public void AVisitedPlacesWordsCountNoRooms()
        {
            var said = DungeonMapWords.VisitedLabel("Sealed Tower", "ffffff");
            Assert.Contains("Sealed Tower", said);
            Assert.Contains("You were here", said);
            Assert.DoesNotContain("room", said);
        }

        [Fact]
        public void AVisitIsNeverListedToTheServerAsAWalk()
        {
            var maps = new DungeonMaps();
            maps.Visited(DungeonMaps.EntranceIdOf("w", 10f, 20f), "Hildir_plainsfortress", new Vec3(10f, 1f, 20f), 393);
            Assert.Empty(WalkList.Of(maps, "w"));
        }
    }
}
