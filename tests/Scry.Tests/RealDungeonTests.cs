using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class RealDungeonTests
    {
        // A world keeps a dungeon's rooms on its generator: a count, then for each room the hash of
        // its prefab's name, its position and its turn as angles in degrees (the game writes a
        // turn as its Euler angles). Scry reads them and lays the dungeon out as it stands.

        private static byte[] Saved(int count, params (int Hash, float[] Values)[] rooms)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(count);
                foreach (var (hash, values) in rooms)
                {
                    writer.Write(hash);
                    foreach (var value in values) writer.Write(value);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void Near(Vec3 want, Vec3 got)
        {
            Assert.Equal(want.X, got.X, 3);
            Assert.Equal(want.Y, got.Y, 3);
            Assert.Equal(want.Z, got.Z, 3);
        }

        [Fact]
        public void ASavedDungeonIsReadRoomByRoom()
        {
            var data = Saved(2, (11, new[] { 1f, 5000f, 2f, 0f, 90f, 0f }), (-7, new[] { 13f, 5001f, 2f, 0f, 180f, 0f }));
            var rooms = SavedRooms.Read(data);
            Assert.Equal(2, rooms.Count);
            Assert.Equal(11, rooms[0].Hash);
            Near(new Vec3(1f, 5000f, 2f), rooms[0].Position);
            Near(new Vec3(0f, 90f, 0f), rooms[0].Angles);
            Assert.Equal(-7, rooms[1].Hash);
            Near(new Vec3(13f, 5001f, 2f), rooms[1].Position);
            Near(new Vec3(0f, 180f, 0f), rooms[1].Angles);
        }

        [Fact]
        public void SavedRoomsCutShortGiveTheRoomsWholeAndNoMore()
        {
            var whole = Saved(3, (11, new[] { 1f, 2f, 3f, 0f, 0f, 0f }), (12, new[] { 4f, 5f, 6f, 0f, 90f, 0f }));
            var cut = whole.Concat(new byte[] { 1, 2, 3, 4, 5 }).ToArray();
            Assert.Equal(2, SavedRooms.Read(cut).Count);
        }

        [Fact]
        public void SavedRoomsAreReadAsManyAsTheCountSays()
        {
            var data = Saved(1, (11, new[] { 1f, 2f, 3f, 0f, 0f, 0f }), (12, new[] { 4f, 5f, 6f, 0f, 90f, 0f }));
            Assert.Equal(new[] { 11 }, SavedRooms.Read(data).Select(r => r.Hash));
        }

        [Fact]
        public void NoSavedRoomsGiveNone()
        {
            Assert.Empty(SavedRooms.Read(null));
            Assert.Empty(SavedRooms.Read(new byte[0]));
            Assert.Empty(SavedRooms.Read(new byte[] { 1, 0 }));
            Assert.Empty(SavedRooms.Read(Saved(-4)));
        }

        [Fact]
        public void AnOlderWorldKeptEachRoomUnderKeysOfItsOwn()
        {
            Assert.Equal(("room0", "room0_pos", "room0_rot"), SavedRooms.OldKeys(0));
            Assert.Equal(("room12", "room12_pos", "room12_rot"), SavedRooms.OldKeys(12));
        }

        [Fact]
        public void RoomsWrittenReadBackAsTheyWere()
        {
            var rooms = new List<(int Hash, Vec3 Position, Vec3 Angles)> { (5, new Vec3(1f, 2f, 3f), new Vec3(0f, 270f, 0f)), (6, new Vec3(-4f, 5f, -6f), new Vec3(0f, 0f, 0f)) };
            var back = SavedRooms.Read(SavedRooms.Write(rooms));
            Assert.Equal(rooms.Select(r => r.Hash), back.Select(r => r.Hash));
            for (var i = 0; i < rooms.Count; i++)
            {
                Near(rooms[i].Position, back[i].Position);
                Near(rooms[i].Angles, back[i].Angles);
            }
        }

        [Fact]
        public void AnglesTurnAsTheGameTurnsThem()
        {
            // About the upright axis as a yaw; about the other axes too, z first, then x, then y.
            Near(Quat.Yaw(90f) * new Vec3(0f, 0f, 1f), Quat.Euler(0f, 90f, 0f) * new Vec3(0f, 0f, 1f));
            Near(new Vec3(0f, -1f, 0f), Quat.Euler(90f, 0f, 0f) * new Vec3(0f, 0f, 1f));
            Near(new Vec3(0f, 1f, 0f), Quat.Euler(0f, 90f, 90f) * new Vec3(1f, 0f, 0f));
            Near(new Vec3(1f, 0f, 0f), Quat.Euler(90f, 90f, 0f) * new Vec3(0f, 1f, 0f));
        }

        private static readonly RoomShape Entrance = new RoomShape { Name = "entrance", Entrance = true, Size = new Vec3(12f, 8f, 12f), Doorways = { new Doorway { Position = new Vec3(0f, 0f, 6f) } } };
        private static readonly RoomShape Hall = new RoomShape { Name = "hall", Size = new Vec3(12f, 8f, 12f), Doorways = { new Doorway { Position = new Vec3(0f, 0f, -6f) }, new Doorway { Position = new Vec3(0f, 0f, 6f) } } };
        private static readonly RoomShape Cap = new RoomShape { Name = "cap", EndCap = true, Size = new Vec3(4f, 4f, 0.2f), Doorways = { new Doorway { Position = new Vec3(0f, 0f, 0f) } } };
        private static readonly RoomShape Divider = new RoomShape { Name = "divider", Divider = true, Size = new Vec3(4f, 4f, 0.2f), Doorways = { new Doorway { Position = new Vec3(0f, 0f, 0f) } } };

        private static RoomShape Known(int hash) => hash == 1 ? Entrance : hash == 2 ? Hall : hash == 3 ? Cap : hash == 4 ? Divider : null;

        [Fact]
        public void TheDungeonStandsAsSavedEachRoomByItsHashItsGeneratorsPlaceTheSite()
        {
            var saved = new List<(int Hash, Vec3 Position, Vec3 Angles)>
            {
                (1, new Vec3(0f, 5000f, 0f), new Vec3(0f, 0f, 0f)),
                (2, new Vec3(0f, 5000f, 12f), new Vec3(0f, 90f, 0f)),
                (99, new Vec3(30f, 5000f, 30f), new Vec3(0f, 0f, 0f)),
            };
            var dungeon = RealDungeon.Of(saved, Known, new Vec3(0f, 5000f, 6f), Quat.Yaw(180f), out var unknown);
            Assert.Equal(1, unknown);
            Assert.Equal(new[] { "entrance", "hall" }, dungeon.Rooms.Select(r => r.Room.Name));
            Near(new Vec3(0f, 5000f, 12f), dungeon.Rooms[1].Position);
            Assert.Equal(90f, dungeon.Rooms[1].Rotation.YawDegrees, 2);
            Near(new Vec3(0f, 5000f, 6f), dungeon.Site.Generator);
            Assert.Equal(180f, dungeon.Site.Turn.YawDegrees, 2);
        }

        [Fact]
        public void ARoomTurnedOtherThanAboutTheUprightStandsTurnedSo()
        {
            var saved = new List<(int Hash, Vec3 Position, Vec3 Angles)> { (2, new Vec3(0f, 5000f, 0f), new Vec3(0f, 0f, 90f)) };
            var dungeon = RealDungeon.Of(saved, Known, new Vec3(0f, 5000f, 0f), Quat.Identity, out _);
            Near(new Vec3(0f, 1f, 0f), dungeon.Rooms[0].Rotation * new Vec3(1f, 0f, 0f));
        }

        [Fact]
        public void TheEntranceComesFirstAsTheGameSavesItsStartRoomFirst()
        {
            var saved = new List<(int Hash, Vec3 Position, Vec3 Angles)>
            {
                (2, new Vec3(0f, 5000f, 12f), new Vec3(0f, 0f, 0f)),
                (1, new Vec3(0f, 5000f, 0f), new Vec3(0f, 0f, 0f)),
                (2, new Vec3(0f, 5000f, 24f), new Vec3(0f, 0f, 0f)),
            };
            // A start room not read stands first; the first entrance read is the way in then.
            var dungeon = RealDungeon.Of(saved, Known, new Vec3(0f, 5000f, 0f), Quat.Identity, out _);
            Assert.Equal(new[] { 1, 0, 2 }, dungeon.Rooms.Select(r => r.PlaceOrder));
            Assert.Equal(Entrance, DungeonWays.Of(dungeon.Rooms).Entrance.Room);
            var plain = RealDungeon.Of(saved.Where(r => r.Hash == 2).ToList(), Known, new Vec3(0f, 5000f, 0f), Quat.Identity, out _);
            Assert.Equal(new[] { 0, 1 }, plain.Rooms.Select(r => r.PlaceOrder));
        }

        [Fact]
        public void AnEndCapIsJoinedToTheRoomWhoseDoorwayItCloses()
        {
            var saved = new List<(int Hash, Vec3 Position, Vec3 Angles)>
            {
                (1, new Vec3(0f, 5000f, 0f), new Vec3(0f, 0f, 0f)),
                (2, new Vec3(0f, 5000f, 12f), new Vec3(0f, 0f, 0f)),
                (3, new Vec3(0f, 5000f, 18f), new Vec3(0f, 0f, 0f)),
                (3, new Vec3(40f, 5000f, 40f), new Vec3(0f, 0f, 0f)),
            };
            var dungeon = RealDungeon.Of(saved, Known, new Vec3(0f, 5000f, 0f), Quat.Identity, out _);
            Assert.Same(dungeon.Rooms[1], dungeon.Rooms[2].JoinedTo);
            Assert.Null(dungeon.Rooms[3].JoinedTo);
            Assert.Null(dungeon.Rooms[1].JoinedTo);
        }

        [Fact]
        public void ADividerIsJoinedAsAnEndCapIsAndNeitherToTheOther()
        {
            // A divider and an end cap at one doorway, listed before the hall: each joined to the hall.
            var saved = new List<(int Hash, Vec3 Position, Vec3 Angles)>
            {
                (1, new Vec3(0f, 5000f, 0f), new Vec3(0f, 0f, 0f)),
                (4, new Vec3(0f, 5000f, 18f), new Vec3(0f, 0f, 0f)),
                (3, new Vec3(0f, 5000f, 18f), new Vec3(0f, 0f, 0f)),
                (2, new Vec3(0f, 5000f, 12f), new Vec3(0f, 0f, 0f)),
            };
            var dungeon = RealDungeon.Of(saved, Known, new Vec3(0f, 5000f, 0f), Quat.Identity, out _);
            Assert.Same(dungeon.Rooms[3], dungeon.Rooms[1].JoinedTo);
            Assert.Same(dungeon.Rooms[3], dungeon.Rooms[2].JoinedTo);
        }

        [Fact]
        public void AnEndCapAboveADoorwayIsNotJoinedToIt()
        {
            var saved = new List<(int Hash, Vec3 Position, Vec3 Angles)>
            {
                (2, new Vec3(0f, 5000f, 12f), new Vec3(0f, 0f, 0f)),
                (3, new Vec3(0f, 5008f, 18f), new Vec3(0f, 0f, 0f)),
            };
            var dungeon = RealDungeon.Of(saved, Known, new Vec3(0f, 5000f, 0f), Quat.Identity, out _);
            Assert.Null(dungeon.Rooms[1].JoinedTo);
        }
    }
}
