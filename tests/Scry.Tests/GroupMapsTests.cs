using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class GroupMapsTests
    {
        // On a server running Scry, each dungeon's record gathers the rooms every player walked;
        // a player whose compendium floor reaches the dungeon's tier gets it, its rooms as they
        // stand from the generator, so no place of the world travels with it.

        private static void Near(Vec3 want, Vec3 got, int places = 3)
        {
            Assert.Equal(want.X, got.X, places);
            Assert.Equal(want.Y, got.Y, places);
            Assert.Equal(want.Z, got.Z, places);
        }

        private static void SameTurn(Quat want, Quat got)
        {
            foreach (var v in new[] { new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), new Vec3(0f, 0f, 1f) }) Near(want * v, got * v);
        }

        [Theory]
        [InlineData(0f, 90f, 0f)]
        [InlineData(30f, 45f, 60f)]
        [InlineData(350f, 200f, 10f)]
        [InlineData(0f, 0f, 0f)]
        [InlineData(90f, 30f, 0f)]
        [InlineData(90f, 30f, 40f)]
        [InlineData(270f, 10f, 20f)]
        public void ATurnIsToldAsTheAnglesThatMakeIt(float x, float y, float z)
        {
            var turn = Quat.Euler(x, y, z);
            var angles = turn.ToEuler();
            SameTurn(turn, Quat.Euler(angles.X, angles.Y, angles.Z));
            Assert.InRange(angles.X, 0f, 360f);
            Assert.InRange(angles.Y, 0f, 360f);
            Assert.InRange(angles.Z, 0f, 360f);
        }

        [Fact]
        public void TheAnglesOfAPlainTurnAreThoseItWasMadeWith()
        {
            Near(new Vec3(30f, 45f, 60f), Quat.Euler(30f, 45f, 60f).ToEuler(), 2);
            Near(new Vec3(0f, 270f, 0f), Quat.Yaw(-90f).ToEuler(), 2);
        }

        [Fact]
        public void ATurnALittleOffItsLengthStillGivesItsAngles()
        {
            // A quarter up, a hair longer than a turn should be, as sums of floats leave it.
            var angles = new Quat(0.7072f, 0f, 0f, 0.7072f).ToEuler();
            Assert.Equal(90f, angles.X, 1);
            Assert.False(float.IsNaN(angles.Y) || float.IsNaN(angles.Z));
        }

        private static List<(int Hash, Vec3 Position, Vec3 Angles)> Rooms() => new List<(int, Vec3, Vec3)>
        {
            (1, new Vec3(110f, 5000f, 220f), new Vec3(0f, 90f, 0f)),
            (2, new Vec3(110f, 5000f, 232f), new Vec3(0f, 180f, 0f)),
            (3, new Vec3(98f, 5004f, 220f), new Vec3(0f, 0f, 0f)),
        };

        private static RoomShape Shape(int hash) => new RoomShape { Name = "room" + hash, Size = new Vec3(12f, 8f, 12f) };

        [Fact]
        public void ARecordTravelsAsItStandsFromItsGenerator()
        {
            var maps = new DungeonMaps();
            var map = maps.Keep("w:1:2", "Crypt2", Rooms(), new Vec3(100f, 30f, 200f), new Vec3(104f, 5000f, 214f), Quat.Yaw(90f));
            maps.Walk("w:1:2", 1);
            var local = map.FromGenerator();
            Near(new Vec3(0f, 0f, 0f), local.Generator);
            Near(new Vec3(0f, 0f, 0f), local.Entrance);
            SameTurn(Quat.Identity, local.Turn);
            Assert.Equal(new[] { 1 }, local.Walked);
            Assert.Equal("Crypt2", local.Location);
            // Laid out, it stands as the world's own does from its generator.
            var world = RealDungeon.Of(map.Rooms, Shape, map.Generator, map.Turn, out _).FromGenerator();
            var sent = RealDungeon.Of(local.Rooms, Shape, local.Generator, local.Turn, out _).FromGenerator();
            for (var i = 0; i < world.Rooms.Count; i++)
            {
                Near(world.Rooms[i].Position, sent.Rooms[i].Position);
                SameTurn(world.Rooms[i].Rotation, sent.Rooms[i].Rotation);
            }
        }

        [Fact]
        public void TwoPlayersWalksMergeIntoOneRecordTheRoomsFirstReadKept()
        {
            var server = new DungeonMaps();
            var first = new DungeonMaps();
            first.Keep("w:1:2", "Crypt2", Rooms(), new Vec3(0f, 0f, 0f), new Vec3(104f, 5000f, 214f), Quat.Yaw(90f));
            first.Walk("w:1:2", 0);
            first.Walk("w:1:2", 1);
            Assert.Equal(2, server.Merge(first));
            var second = new DungeonMaps();
            second.Keep("w:1:2", "Crypt2", Rooms().Take(1).ToList(), new Vec3(0f, 0f, 0f), new Vec3(0f, 0f, 0f), Quat.Identity);
            second.Walk("w:1:2", 0);
            var other = new DungeonMaps();
            other.Keep("w:1:2", "Crypt2", Rooms(), new Vec3(0f, 0f, 0f), new Vec3(104f, 5000f, 214f), Quat.Yaw(90f));
            other.Walk("w:1:2", 2);
            other.Walk("w:1:2", 1);
            Assert.Equal(0, server.Merge(second));
            Assert.Equal(1, server.Merge(other));
            var merged = server.Of("w:1:2");
            Assert.Equal(new[] { 0, 1, 2 }, merged.Walked);
            Assert.Equal(3, merged.Rooms.Count);
        }

        [Fact]
        public void AServerTakesInOnlyTheRecordsOfItsOwnWorld()
        {
            var server = new DungeonMaps();
            var sent = new DungeonMaps();
            sent.Keep("w:1:2", "Crypt2", Rooms(), new Vec3(0f, 0f, 0f), new Vec3(104f, 5000f, 214f), Quat.Yaw(90f));
            sent.Walk("w:1:2", 0);
            sent.Keep("v:1:3", "Crypt2", Rooms(), new Vec3(0f, 0f, 0f), new Vec3(104f, 5000f, 214f), Quat.Yaw(90f));
            sent.Walk("v:1:3", 0);
            Assert.Equal(1, server.Merge(sent, "w"));
            Assert.Equal(new[] { "w:1:2" }, server.All.Select(m => m.Id));
            Assert.Equal(0, server.Merge(sent, null));
        }

        [Fact]
        public void AnAnswerHoldsTheRecordsOfTheLocationsAskedForInTheWorldAsked()
        {
            var server = new DungeonMaps();
            server.Keep("w:1:2", "Crypt2", Rooms(), new Vec3(1f, 2f, 3f), new Vec3(104f, 5000f, 214f), Quat.Yaw(90f));
            server.Walk("w:1:2", 2);
            server.Keep("w:1:3", "Crypt3", Rooms(), new Vec3(1f, 2f, 3f), new Vec3(104f, 5000f, 214f), Quat.Yaw(90f));
            server.Keep("v:1:4", "Crypt2", Rooms(), new Vec3(1f, 2f, 3f), new Vec3(104f, 5000f, 214f), Quat.Yaw(90f));
            var answer = server.Only("w", new[] { "Crypt2" });
            Assert.Equal(new[] { "w:1:2" }, answer.All.Select(m => m.Id));
            Near(new Vec3(0f, 0f, 0f), answer.Of("w:1:2").Generator);
            Assert.Equal(new[] { 2 }, answer.Of("w:1:2").Walked);
            Assert.Empty(server.Only(null, new[] { "Crypt2" }).All);
        }

        [Fact]
        public void TheGroupsLineSaysHowFarItWasWalkedAndWhatOpenedIt()
        {
            Assert.Equal("The group's walk: 14 of 18 rooms walked · opened up to the Mountain tier", DungeonMapWords.Group(0, 1, 14, 18, "Mountain"));
            Assert.Equal("The group's walk 2: 1 of 2 rooms walked · opened up to the Plains tier", DungeonMapWords.Group(1, 2, 1, 2, "Plains"));
            Assert.Equal("Show this dungeon on the stage as far as anyone on this server walked it", DungeonMapWords.GroupOpenTip);
            Assert.Equal("the server's records of 2 dungeons of this world are forgotten.", DungeonMapWords.GroupForgotten(2));
            Assert.Equal("the server's record of 1 dungeon of this world is forgotten.", DungeonMapWords.GroupForgotten(1));
            Assert.Equal("the server has no record of a dungeon of this world to forget.", DungeonMapWords.GroupForgotten(0));
            Assert.Equal("only an admin of the server can forget its records.", DungeonMapWords.GroupForgetRefused);
            Assert.Equal("the server is asked to forget its records of this world.", DungeonMapWords.GroupAsked);
        }

        [Fact]
        public void AServerKeepsAWorldsRecordsInAFileOfItsOwn()
        {
            Assert.Equal("maps--5170381.dat", DungeonMaps.ServerFile("-5170381"));
        }
    }
}
