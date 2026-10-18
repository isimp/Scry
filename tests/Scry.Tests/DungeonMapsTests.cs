using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class DungeonMapsTests
    {
        // Each dungeon a character has been in is kept: its rooms as the world saved them, where
        // its entrance is, and the rooms stood in, so its map shows as far as it was walked.

        private static List<(int Hash, Vec3 Position, Vec3 Angles)> Rooms(int count) =>
            Enumerable.Range(0, count).Select(i => (i + 1, new Vec3(0f, 5000f, i * 12f), new Vec3(0f, 0f, 0f))).ToList();

        [Fact]
        public void AMapIsKnownByItsWorldAndItsGeneratorsNetworkId()
        {
            Assert.Equal("-5170381:7766554433:42", DungeonMaps.IdOf("-5170381", 7766554433L, 42u));
            Assert.Null(DungeonMaps.IdOf(null, 1L, 2u));
        }

        [Fact]
        public void ADungeonBeenInIsKeptWithItsRoomsAndEntrance()
        {
            var maps = new DungeonMaps();
            var map = maps.Keep("w:1:2", "Crypt2", Rooms(3), new Vec3(10f, 30f, 20f));
            Assert.Same(map, maps.Of("w:1:2"));
            Assert.Equal("Crypt2", map.Location);
            Assert.Equal(3, map.Rooms.Count);
            Assert.Equal(30f, map.Entrance.Y);
            Assert.Empty(map.Walked);
            Assert.Null(maps.Of("w:1:3"));
        }

        [Fact]
        public void KeptAgainItKeepsWhatWasFirstRead()
        {
            var maps = new DungeonMaps();
            maps.Keep("w:1:2", "Crypt2", Rooms(3), new Vec3(10f, 30f, 20f));
            var stamp = maps.Stamp;
            var again = maps.Keep("w:1:2", "Crypt2", Rooms(5), new Vec3(0f, 0f, 0f));
            Assert.Equal(3, again.Rooms.Count);
            Assert.Equal(stamp, maps.Stamp);
        }

        [Fact]
        public void ARoomStoodInIsWalkedOnce()
        {
            var maps = new DungeonMaps();
            maps.Keep("w:1:2", "Crypt2", Rooms(3), new Vec3(0f, 0f, 0f));
            var stamp = maps.Stamp;
            Assert.True(maps.Walk("w:1:2", 1));
            Assert.NotEqual(stamp, maps.Stamp);
            stamp = maps.Stamp;
            Assert.False(maps.Walk("w:1:2", 1));
            Assert.Equal(stamp, maps.Stamp);
            Assert.Equal(new[] { 1 }, maps.Of("w:1:2").Walked);
        }

        [Fact]
        public void NoRoomIsWalkedOutsideAKeptDungeonsRooms()
        {
            var maps = new DungeonMaps();
            maps.Keep("w:1:2", "Crypt2", Rooms(3), new Vec3(0f, 0f, 0f));
            Assert.False(maps.Walk("w:1:2", 3));
            Assert.False(maps.Walk("w:1:2", -1));
            Assert.False(maps.Walk("w:9:9", 0));
            Assert.Empty(maps.Of("w:1:2").Walked);
        }

        [Fact]
        public void OneDungeonsMapOrAllAreForgotten()
        {
            var maps = new DungeonMaps();
            maps.Keep("w:1:2", "Crypt2", Rooms(3), new Vec3(0f, 0f, 0f));
            maps.Keep("w:1:3", "Crypt3", Rooms(2), new Vec3(0f, 0f, 0f));
            var stamp = maps.Stamp;
            Assert.True(maps.Forget("w:1:2"));
            Assert.NotEqual(stamp, maps.Stamp);
            Assert.Null(maps.Of("w:1:2"));
            Assert.False(maps.Forget("w:1:2"));
            Assert.Equal(new[] { "w:1:3" }, maps.All.Select(m => m.Id));
            maps.ForgetAll();
            Assert.Empty(maps.All);
        }
    }
}
