using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class ServerWalksTests
    {
        // With insides, a source shows what anyone on this server walked inside the dungeons it
        // holds: the kinds of room stood in, and which kinds of place have a dungeon walked through
        // whole, its end caps and dividers left out, as no one stands in them. Each source holds its
        // own dungeons, so a walk counts only for the sources whose dungeons it was in.

        /// <summary>A made-up hash of a room's name, as the game's stable hash stands in for it.</summary>
        private static int Hash(string room) => room.Length * 1000 + room[room.Length - 1];

        private static DungeonMap Map(string location, string[] rooms, params int[] walked)
        {
            var map = new DungeonMap { Location = location, Rooms = rooms.Select((r, i) => (Hash(r), new Vec3(i, 0f, 0f), new Vec3(0f, 0f, 0f))).ToList() };
            foreach (var room in walked) map.Walked.Add(room);
            return map;
        }

        private static readonly string[] CryptRooms = { "crypt_room1", "crypt_room2", "crypt_endcap", "crypt_divider" };

        /// <summary>The shapes read of the crypt's rooms: an end cap and a divider among them.</summary>
        private static RoomShape ShapeOf(int hash) =>
            hash == Hash("crypt_endcap") ? new RoomShape { EndCap = true }
            : hash == Hash("crypt_divider") ? new RoomShape { Divider = true }
            : hash == Hash("crypt_room1") || hash == Hash("crypt_room2") ? new RoomShape() : null;

        private static ServerWalks Of(Dictionary<WorldSource, DungeonMap[]> held) =>
            ServerWalks.Of(source => held.TryGetValue(source, out var maps) ? maps : new DungeonMap[0], ShapeOf, Hash);

        [Fact]
        public void TheKindsOfRoomWalkedInASourcesDungeonsAreItsOwn()
        {
            var walks = Of(new Dictionary<WorldSource, DungeonMap[]>
            {
                [WorldSource.Explored] = new[] { Map("Crypt2", CryptRooms, 0), Map("Crypt3", CryptRooms) },
                [WorldSource.SharedMaps] = new[] { Map("Crypt2", CryptRooms, 1) },
            });
            Assert.True(walks.Walked(WorldSource.Explored, "crypt_room1"));
            Assert.False(walks.Walked(WorldSource.Explored, "crypt_room2"));
            Assert.True(walks.Walked(WorldSource.SharedMaps, "crypt_room2"));
            Assert.False(walks.Walked(WorldSource.SharedMaps, "crypt_room1"));
            Assert.False(walks.Walked(WorldSource.Marked, "crypt_room1"));
        }

        [Fact]
        public void ADungeonWithEveryRoomWalkedButItsEndCapsAndDividersIsWalkedWhole()
        {
            var walks = Of(new Dictionary<WorldSource, DungeonMap[]>
            {
                [WorldSource.Explored] = new[] { Map("Crypt2", CryptRooms, 0, 1), Map("Crypt3", CryptRooms, 0) },
            });
            Assert.True(walks.Whole(WorldSource.Explored, "Crypt2"));
            Assert.False(walks.Whole(WorldSource.Explored, "Crypt3"));
            Assert.False(walks.Whole(WorldSource.SharedMaps, "Crypt2"));
        }

        [Fact]
        public void ARoomOfAKindNotReadCountsAsARoomSoNotWalkedItKeepsADungeonFromWhole()
        {
            var walks = Of(new Dictionary<WorldSource, DungeonMap[]>
            {
                [WorldSource.Explored] = new[] { Map("Crypt2", new[] { "crypt_room1", "unread_room" }, 0) },
            });
            Assert.False(walks.Whole(WorldSource.Explored, "Crypt2"));
        }

        [Fact]
        public void AWalkPastADungeonsRoomsIsPassedOver()
        {
            // As a record whose rooms the world holds otherwise now.
            var map = Map("Crypt2", CryptRooms, 0);
            map.Walked.Add(9);
            var walks = Of(new Dictionary<WorldSource, DungeonMap[]> { [WorldSource.Explored] = new[] { map } });
            Assert.True(walks.Walked(WorldSource.Explored, "crypt_room1"));
        }

        [Fact]
        public void WalksGatheredAgainTheSameAreTheSameSoNothingIsCountedAfresh()
        {
            var held = new Dictionary<WorldSource, DungeonMap[]> { [WorldSource.Explored] = new[] { Map("Crypt2", CryptRooms, 0) } };
            var first = Of(held);
            Assert.True(first.SameAs(Of(held)));

            // A room more, walking it whole.
            Assert.False(first.SameAs(Of(new Dictionary<WorldSource, DungeonMap[]> { [WorldSource.Explored] = new[] { Map("Crypt2", CryptRooms, 0, 1) } })));
            // The same room in another source's dungeon.
            Assert.False(first.SameAs(Of(new Dictionary<WorldSource, DungeonMap[]> { [WorldSource.SharedMaps] = new[] { Map("Crypt2", CryptRooms, 0) } })));
            // Only walked whole: the same rooms, one kind of place walked whole more.
            var oneRoom = new[] { "crypt_room1", "crypt_endcap" };
            Assert.False(Of(new Dictionary<WorldSource, DungeonMap[]> { [WorldSource.Explored] = new[] { Map("Crypt2", oneRoom, 0), Map("Crypt3", CryptRooms, 0) } })
                .SameAs(Of(new Dictionary<WorldSource, DungeonMap[]> { [WorldSource.Explored] = new[] { Map("Crypt2", CryptRooms, 0), Map("Crypt3", CryptRooms, 0) } })));
            Assert.True(ServerWalks.None.SameAs(Of(new Dictionary<WorldSource, DungeonMap[]>())));
            Assert.False(first.SameAs(null));
        }

        [Fact]
        public void ADungeonWithNoRoomsKnownIsNeverWalkedWhole()
        {
            // Only its entrance is known: its area never loaded.
            var walks = Of(new Dictionary<WorldSource, DungeonMap[]>
            {
                [WorldSource.Explored] = new[] { new DungeonMap { Location = "Crypt2", Rooms = new List<(int, Vec3, Vec3)>() } },
            });
            Assert.False(walks.Whole(WorldSource.Explored, "Crypt2"));
            Assert.False(ServerWalks.None.Walked(WorldSource.Explored, "crypt_room1"));
            Assert.False(ServerWalks.None.Whole(WorldSource.Explored, "Crypt2"));
        }
    }
}
