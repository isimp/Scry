using System;
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
            var map = maps.Keep("w:1:2", "Crypt2", Rooms(3), new Vec3(10f, 30f, 20f), new Vec3(10f, 5030f, 26f), Quat.Yaw(90f));
            Assert.Same(map, maps.Of("w:1:2"));
            Assert.Equal("Crypt2", map.Location);
            Assert.Equal(3, map.Rooms.Count);
            Assert.Equal(30f, map.Entrance.Y);
            Assert.Equal(5030f, map.Generator.Y);
            Assert.Equal(90f, map.Turn.YawDegrees, 3);
            Assert.Empty(map.Walked);
            Assert.Null(maps.Of("w:1:3"));
        }

        [Fact]
        public void KeptAgainItKeepsWhatWasFirstRead()
        {
            var maps = new DungeonMaps();
            maps.Keep("w:1:2", "Crypt2", Rooms(3), new Vec3(10f, 30f, 20f), new Vec3(0f, 5000f, 0f), Quat.Identity);
            var stamp = maps.Stamp;
            var again = maps.Keep("w:1:2", "Crypt2", Rooms(5), new Vec3(0f, 0f, 0f), new Vec3(0f, 5000f, 0f), Quat.Identity);
            Assert.Equal(3, again.Rooms.Count);
            Assert.Equal(stamp, maps.Stamp);
        }

        [Fact]
        public void ARoomStoodInIsWalkedOnce()
        {
            var maps = new DungeonMaps();
            maps.Keep("w:1:2", "Crypt2", Rooms(3), new Vec3(0f, 0f, 0f), new Vec3(0f, 5000f, 0f), Quat.Identity);
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
            maps.Keep("w:1:2", "Crypt2", Rooms(3), new Vec3(0f, 0f, 0f), new Vec3(0f, 5000f, 0f), Quat.Identity);
            Assert.False(maps.Walk("w:1:2", 3));
            Assert.False(maps.Walk("w:1:2", -1));
            Assert.False(maps.Walk("w:9:9", 0));
            Assert.Empty(maps.Of("w:1:2").Walked);
        }

        [Fact]
        public void OneDungeonsMapOrAllAreForgotten()
        {
            var maps = new DungeonMaps();
            maps.Keep("w:1:2", "Crypt2", Rooms(3), new Vec3(0f, 0f, 0f), new Vec3(0f, 5000f, 0f), Quat.Identity);
            maps.Keep("w:1:3", "Crypt3", Rooms(2), new Vec3(0f, 0f, 0f), new Vec3(0f, 5000f, 0f), Quat.Identity);
            var stamp = maps.Stamp;
            Assert.True(maps.Forget("w:1:2"));
            Assert.NotEqual(stamp, maps.Stamp);
            Assert.Null(maps.Of("w:1:2"));
            Assert.False(maps.Forget("w:1:2"));
            Assert.Equal(new[] { "w:1:3" }, maps.All.Select(m => m.Id));
            maps.ForgetAll();
            Assert.Empty(maps.All);
        }

        private static DungeonMaps Two()
        {
            var maps = new DungeonMaps();
            maps.Keep("w:1:2", "Crypt2", Rooms(3), new Vec3(10.25f, 30.5f, -20.125f), new Vec3(10f, 5030f, 26f), Quat.Yaw(90f));
            maps.Walk("w:1:2", 0);
            maps.Walk("w:1:2", 2);
            maps.Keep("v:4:5", "Crypt3", Rooms(2), new Vec3(0f, 0f, 0f), new Vec3(0f, 5000f, 0f), Quat.Identity);
            return maps;
        }

        [Fact]
        public void KeptMapsAreReadBackAsTheyWere()
        {
            var back = DungeonMaps.Load(Two().ToSave()());
            Assert.Equal(new[] { "w:1:2", "v:4:5" }, back.All.Select(m => m.Id));
            var map = back.Of("w:1:2");
            Assert.Equal("Crypt2", map.Location);
            Assert.Equal(new[] { 0, 2 }, map.Walked);
            Assert.Equal(-20.125f, map.Entrance.Z);
            Assert.Equal(5030f, map.Generator.Y);
            Assert.Equal(90f, map.Turn.YawDegrees, 3);
            Assert.Equal(Rooms(3).Select(r => r.Hash), map.Rooms.Select(r => r.Hash));
            Assert.Equal(24f, map.Rooms[2].Position.Z);
            Assert.Empty(back.Of("v:4:5").Walked);
            Assert.False(back.Changed);
        }

        [Fact]
        public void AMapLineThatCannotBeReadIsPassedOver()
        {
            var text = Two().ToSave()();
            var lines = text.Split('\n').ToList();
            lines.Insert(1, "w:9:9\tCrypt2\tnot numbers");
            lines.Insert(1, "w:9:8\tCrypt2\t0 0 0\t0 0 0\t0 0 0 1\t\t@@@@");
            lines.Insert(1, "w:9:7\tCrypt2\t0 0 0\t0 0 0\t0 0 0 1\t\tAAA=AAA=");
            var oneRoom = Convert.ToBase64String(SavedRooms.Write(Rooms(1)));
            // Each of these but for one thing is a line as written: a field too many or too few, no
            // rooms, a room walked that is no number, and rooms that are no base64 in other ways.
            lines.Insert(1, "w:9:6\tCrypt2\t0 0 0\t0 0 0\t0 0 0 1\t\t" + oneRoom + "\textra");
            lines.Insert(1, "w:9:5\tCrypt2\t0 0 0\t0 0 0\t0 0 0 1\t");
            lines.Insert(1, "w:9:4\tCrypt2\t0 0 0\t0 0 0\t0 0 0 1\t\t" + Convert.ToBase64String(SavedRooms.Write(Rooms(0))));
            lines.Insert(1, "w:9:3\tCrypt2\t0 0 0\t0 0 0\t0 0 0 1\t0,x\t" + oneRoom);
            lines.Insert(1, "w:8:1\tCrypt2\t0 0 0\t0 0 0\t0 0 0 1\t\tAAAAA");
            lines.Insert(1, "w:8:2\tCrypt2\t0 0 0\t0 0 0\t0 0 0 1\t\tA===");
            lines.Insert(1, "w:8:3\tCrypt2\t0 0 0\t0 0 0\t0 0 0 1\t\tAA=A");
            var back = DungeonMaps.Load(string.Join("\n", lines));
            Assert.Equal(new[] { "w:1:2", "v:4:5" }, back.All.Select(m => m.Id));
            Assert.Empty(DungeonMaps.Load(null).All);
            Assert.Empty(DungeonMaps.Load("").All);
        }

        [Fact]
        public void ADungeonIsKnownByWhereItsLocationStandsItsOldNumberReKeyedAsItIsRead()
        {
            // The game numbers its objects afresh each time it loads the world, so a generator's number
            // names another object after a restart; where the dungeon's location stands never moves.
            Assert.Equal(DungeonMaps.EntranceIdOf("w", 1200.4f, -340.2f), DungeonMaps.PlaceIdOf("w", new Vec3(1200.4f, 30f, -340.2f)));
            Assert.Null(DungeonMaps.PlaceIdOf(null, new Vec3(0f, 0f, 0f)));

            var maps = new DungeonMaps();
            var rooms = Rooms(3);
            maps.Keep("w:1:369448", "MountainCave02", rooms, new Vec3(1200.4f, 30f, -340.2f), default, Quat.Identity, 41);
            maps.Walk("w:1:369448", 0);
            // The same cave under the number a later load gave it, walked further on another day.
            maps.Keep("w:1:370001", "MountainCave02", rooms, new Vec3(1200.4f, 30f, -340.2f), default, Quat.Identity, 45);
            maps.Walk("w:1:370001", 2);
            maps.Keep("w:-77:5", "Crypt2", Rooms(2), new Vec3(10f, 0f, 20f), default, Quat.Identity);
            // Another world's crypt at the same spot stays that world's.
            maps.Keep("v:7:5", "Crypt2", Rooms(2), new Vec3(10f, 0f, 20f), default, Quat.Identity);
            // Re-keyed where real maps come in: the character's file, the server's records and its answers.
            var read = DungeonMaps.Load(maps.ToSave()());
            Assert.Equal(new[] { "w:1:369448", "w:1:370001", "w:-77:5", "v:7:5" }, read.All.Select(m => m.Id));
            var back = read.ByPlace();
            Assert.Equal(new[] { "w:at:1200:-340", "w:at:10:20", "v:at:10:20" }, back.All.Select(m => m.Id));
            var cave = back.Of("w:at:1200:-340");
            Assert.Equal(new[] { 0, 2 }, cave.Walked);
            Assert.Equal((41, 45), (cave.FirstDay, cave.LastDay));
            // Written again under the ids by place; read and re-keyed again, nothing changes.
            Assert.True(back.Changed);
            var again = DungeonMaps.Load(back.ToSave()());
            Assert.Same(again, again.ByPlace());
            Assert.False(again.Changed);
            Assert.Equal(back.All.Select(m => m.Id), again.All.Select(m => m.Id));

            // An id of no form this Scry knows, a newer one's say, stays as it is.
            var other = new DungeonMaps();
            other.Keep("w:1:369448", "Crypt2", Rooms(2), new Vec3(10f, 0f, 20f), default, Quat.Identity);
            other.Keep("w:1:x", "Crypt2", Rooms(2), new Vec3(30f, 0f, 40f), default, Quat.Identity);
            other.Keep("w:1:-5", "Crypt2", Rooms(2), new Vec3(50f, 0f, 60f), default, Quat.Identity);
            Assert.Equal(new[] { "w:at:10:20", "w:1:x", "w:1:-5" }, other.ByPlace().All.Select(m => m.Id));
        }

        [Fact]
        public void ADungeonKnownByItsEntranceTakesItsRoomsFromAWalkOfItThatComesIn()
        {
            // Kept by where it lies, the dungeon seen from its entrance and the one walked are one.
            var at = new Vec3(1200.6f, 31f, -340.2f);
            var id = DungeonMaps.PlaceIdOf("w", at);
            var maps = new DungeonMaps();
            maps.Keep(id, "Crypt2", new List<(int, Vec3, Vec3)>(), at, default, Quat.Identity);
            maps.ToSave();
            var walk = new DungeonMaps();
            var rooms = Rooms(3);
            walk.Keep(id, "Crypt2", rooms, at, new Vec3(1f, 2f, 3f), Quat.Identity, 41);
            walk.Walk(id, 1);
            Assert.Equal(1, maps.Merge(walk));
            var map = maps.Of(id);
            Assert.Equal(rooms, map.Rooms);
            Assert.Equal((1f, 2f, 3f), (map.Generator.X, map.Generator.Y, map.Generator.Z));
            Assert.Equal(new[] { 1 }, map.Walked);
            Assert.True(maps.Changed);
            // Rooms once kept stay those first read.
            var later = new DungeonMaps();
            later.Keep(id, "Crypt2", Rooms(5), at, default, Quat.Identity);
            maps.Merge(later);
            Assert.Equal(rooms, maps.Of(id).Rooms);
        }

        [Fact]
        public void TheDayAWorldsRecordsWereForgottenIsKeptOnALineOfItsOwn()
        {
            var maps = Two();
            Assert.Equal(-1, maps.ForgottenDay("w"));
            maps.ToSave();
            maps.NoteForgotten("w", 120);
            Assert.True(maps.Changed);
            Assert.Equal(120, maps.ForgottenDay("w"));
            Assert.Equal(-1, maps.ForgottenDay("v"));
            // A later forget moves it on, never back.
            maps.NoteForgotten("w", 90);
            Assert.Equal(120, maps.ForgottenDay("w"));
            var text = maps.ToSave()();
            Assert.Contains("forgotten\tw\t120", text);
            Assert.Equal(120, DungeonMaps.Load(text).ForgottenDay("w"));
            // Three fields, no dungeon's line: an older Scry passes it over; one that cannot be read is passed over.
            Assert.Equal(-1, DungeonMaps.Load("scry-maps 1\nforgotten\tw\tnot a day").ForgottenDay("w"));
            // Re-keyed by place, the day stays the world's.
            Assert.Equal(120, DungeonMaps.Load(text).ByPlace().ForgottenDay("w"));
        }

        [Fact]
        public void TheKindsOfRoomWalkedAreThoseOfEveryWorldsMaps()
        {
            // Under the strict rules a kind of room walked once counts in every world, as all else the character met does.
            var maps = new DungeonMaps();
            var rooms = Rooms(3);
            maps.Keep("w:1:1", "Crypt2", rooms, new Vec3(0f, 0f, 0f), default, Quat.Identity);
            maps.Keep("v:1:1", "Crypt2", rooms, new Vec3(0f, 0f, 0f), default, Quat.Identity);
            maps.Walk("w:1:1", 0);
            maps.Walk("v:1:1", 2);
            maps.Of("v:1:1").Walked.Add(7);
            Assert.Equal(new[] { rooms[0].Hash, rooms[2].Hash }.OrderBy(h => h), maps.RoomsWalked().OrderBy(h => h));

            // A map forgotten takes its rooms with it.
            maps.Forget("v:1:1");
            Assert.Equal(new[] { rooms[0].Hash }, maps.RoomsWalked());
        }

        [Fact]
        public void ADungeonKnownOnlyByItsEntranceIsKeptOnALineOfItsOwnThatAnOlderReaderPassesOver()
        {
            // A dungeon whose area never loaded: where it lies is known, none of its rooms.
            var id = DungeonMaps.EntranceIdOf("w", 1200.6f, -340.2f);
            Assert.Equal("w:at:1201:-340", id);
            var maps = Two();
            maps.Keep(id, "Crypt2", new List<(int, Vec3, Vec3)>(), new Vec3(1200.6f, 31f, -340.2f), default, Quat.Identity);
            Assert.False(maps.Walk(id, 0));
            Assert.Contains(id, maps.OfWorld("w").Select(m => m.Id));

            var text = maps.ToSave()();
            var line = text.Split('\n').Single(l => l.StartsWith("entrance\t", StringComparison.Ordinal));
            Assert.Equal(4, line.Split('\t').Length);
            var back = DungeonMaps.Load(text);
            var map = back.Of(id);
            Assert.NotNull(map);
            Assert.Equal("Crypt2", map.Location);
            Assert.Empty(map.Rooms);
            Assert.Equal((1200.6f, 31f, -340.2f), (map.Entrance.X, map.Entrance.Y, map.Entrance.Z));
            Assert.Equal(new[] { "w:1:2", "v:4:5", id }, back.All.Select(m => m.Id));
            Assert.Equal((0, 0), DungeonMaps.Walked(map, _ => null));

            // Its four fields are no dungeon's seven, so an older Scry passes the line over; one that cannot be read is passed over.
            Assert.Empty(DungeonMaps.Load("scry-maps 1\nentrance\tw:at:1:2\tCrypt2\tnot a point").All);
        }

        [Fact]
        public void WhatIsSavedIsWhatTheMapsHeldWhenAskedNotAfter()
        {
            var maps = Two();
            var save = maps.ToSave();
            maps.Walk("w:1:2", 1);
            Assert.Equal(new[] { 0, 2 }, DungeonMaps.Load(save()).Of("w:1:2").Walked);
        }

        [Fact]
        public void MapsChangedSinceSavedAreToBeSavedAgain()
        {
            var maps = Two();
            Assert.True(maps.Changed);
            maps.ToSave();
            Assert.False(maps.Changed);
            maps.Walk("w:1:2", 1);
            Assert.True(maps.Changed);
        }

        [Fact]
        public void AWorldsMapsAreForgottenTheOthersKept()
        {
            var maps = Two();
            Assert.Equal(1, maps.ForgetWorld("w"));
            Assert.Equal(new[] { "v:4:5" }, maps.All.Select(m => m.Id));
            Assert.Equal(0, maps.ForgetWorld("w"));
        }

        [Fact]
        public void ALocationsMapsAreThoseOfItInTheWorldPlayed()
        {
            var maps = Two();
            maps.Keep("w:1:7", "Crypt2", Rooms(4), new Vec3(0f, 0f, 0f), new Vec3(0f, 5000f, 0f), Quat.Identity);
            maps.Keep("v:1:8", "Crypt2", Rooms(4), new Vec3(0f, 0f, 0f), new Vec3(0f, 5000f, 0f), Quat.Identity);
            maps.Keep("w:1:9", "Crypt4", Rooms(4), new Vec3(0f, 0f, 0f), new Vec3(0f, 5000f, 0f), Quat.Identity);
            Assert.Equal(new[] { "w:1:2", "w:1:7" }, maps.OfLocation("w", "Crypt2").Select(m => m.Id));
            Assert.Empty(maps.OfLocation(null, "Crypt2"));
            Assert.Equal(new[] { "w:1:2", "w:1:7", "w:1:9" }, maps.OfWorld("w").Select(m => m.Id));
            Assert.Empty(maps.OfWorld(null));
        }

        [Fact]
        public void AMapCountsItsRoomsWalkedOfAllEndCapsAndDividersLeftOut()
        {
            var maps = new DungeonMaps();
            var map = maps.Keep("w:1:2", "Crypt2", Rooms(5), new Vec3(0f, 0f, 0f), new Vec3(0f, 5000f, 0f), Quat.Identity);
            maps.Walk("w:1:2", 0);
            maps.Walk("w:1:2", 3);
            // Rooms 3 and 4 are an end cap and a divider, room 5 of a kind not read, counted as a room.
            RoomShape Of(int hash) => hash == 4 ? new RoomShape { EndCap = true } : hash == 5 ? new RoomShape { Divider = true } : hash == 3 ? null : new RoomShape();
            Assert.Equal((1, 3), DungeonMaps.Walked(map, Of));
        }

        [Fact]
        public void APageListsOnlyTheWalksWithARoomWalked()
        {
            // A walk kept with no room walked holds nothing to show; it stays kept, for forgetting.
            var maps = new DungeonMaps();
            maps.Keep("w:1:2", "Crypt2", Rooms(4), new Vec3(0f, 0f, 0f), new Vec3(0f, 5000f, 0f), Quat.Identity);
            maps.Keep("w:1:3", "Crypt2", Rooms(4), new Vec3(0f, 0f, 0f), new Vec3(0f, 5000f, 0f), Quat.Identity);
            maps.Walk("w:1:3", 1);
            var listed = maps.WalkedOf("w", "Crypt2", m => DungeonMaps.Walked(m, _ => null)).ToList();
            Assert.Equal(new[] { "w:1:3" }, listed.Select(m => m.Id));
            Assert.Equal((1, 4), (listed[0].Walked, listed[0].Rooms));
            Assert.Equal(2, maps.OfLocation("w", "Crypt2").Count());
        }

        [Fact]
        public void ThePageSaysHowFarEachMapWasWalked()
        {
            Assert.Equal("Show it", DungeonMapWords.Open);
            Assert.Equal("Show this dungeon on the stage as far as you walked it, wherever you are", DungeonMapWords.OpenTip);
            Assert.Equal("Forget", DungeonMapWords.Forget);
            Assert.Equal("Forget this walk: its rooms show again only as you walk them", DungeonMapWords.ForgetTip);
            Assert.Equal("the maps of 3 dungeons of this world are forgotten.", DungeonMapWords.WorldForgotten(3));
            Assert.Equal("the map of 1 dungeon of this world is forgotten.", DungeonMapWords.WorldForgotten(1));
            Assert.Equal("there is no map of a dungeon of this world to forget.", DungeonMapWords.WorldForgotten(0));
        }
    }
}
