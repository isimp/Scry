using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class LevelCutsTests
    {
        // A dungeon's level, as the plan box lays it out, can hold rooms one cut cannot open
        // together: the cut must stand over every room's floor and under every room's ceiling.
        // Its rooms are put in groups each one cut opens, the highest first.

        [Fact]
        public void RoomsOfALevelOneCutOpensShareAGroupCutUnderTheirLowestCeiling()
        {
            var cuts = LevelCuts.Of(grounds: new[] { -2.66f, -3.31f }, ceilings: new[] { 3f, 6f }, levels: new[] { 0, 0 });
            var group = Assert.Single(cuts.Groups);
            Assert.Equal(new[] { 0, 1 }, group.Rooms);
            Assert.Equal(-2.66f + PlaceView.CutAboveFloor, group.Cut, 3);
            Assert.True(group.OpensAll);
        }

        [Fact]
        public void ALowCorridorUnderTheLevelsHigherRoomsGetsAGroupOfItsOwn()
        {
            // Frost Caves' top level: rooms at -2.7 and -3.3 m, and an ice corridor at -5.3 m
            // only 2.2 m tall: no one height is over the first two's floors and under its ceiling.
            var cuts = LevelCuts.Of(grounds: new[] { -2.66f, -3.31f, -5.3f }, ceilings: new[] { 3f, 6f, -3.1f }, levels: new[] { 0, 0, 0 });
            Assert.Equal(2, cuts.Groups.Count);
            Assert.Equal(new[] { 0, 1 }, cuts.Groups[0].Rooms);
            Assert.Equal(new[] { 2 }, cuts.Groups[1].Rooms);
            Assert.Equal(-3.1f - PlaceView.UnderCeiling, cuts.Groups[1].Cut, 3);
            Assert.Equal(1, cuts.GroupOf(2));
            Assert.Equal(0, cuts.LevelOf(2));
        }

        [Fact]
        public void RoomsGivenInAnyOrderAreGroupedFromTheLevelsHighestDown()
        {
            var cuts = LevelCuts.Of(grounds: new[] { -5.3f, -2.66f, -3.31f }, ceilings: new[] { -3.1f, 3f, 6f }, levels: new[] { 0, 0, 0 });
            Assert.Equal(new[] { 1, 2 }, cuts.Groups[0].Rooms);
            Assert.Equal(-2.66f, cuts.Groups[0].Top, 3);
            Assert.Equal(new[] { 0 }, cuts.Groups[1].Rooms);
        }

        [Fact]
        public void ALowerRoomWithAHighCeilingStaysInTheGroupAboveIt()
        {
            // Five metres under the level's top rooms, but its ceiling over their cut: one cut opens all.
            var cuts = LevelCuts.Of(grounds: new[] { -2.66f, -7.73f }, ceilings: new[] { 6f, 1.5f }, levels: new[] { 0, 0 });
            Assert.Single(cuts.Groups);
        }

        [Fact]
        public void TheGroupsComeHighestFirstLevelByLevel()
        {
            var cuts = LevelCuts.Of(
                grounds: new[] { -20.25f, -2.66f, -23.4f, -5.3f, -38f },
                ceilings: new[] { -15f, 3f, -21f, -3.1f, -33f },
                levels: new[] { 1, 0, 1, 0, 2 });
            Assert.Equal(new[] { 0, 0, 1, 1, 2 }, cuts.Groups.Select(g => g.Level).ToArray());
            Assert.Equal(new[] { -2.66f, -5.3f, -20.25f, -23.4f, -38f }, cuts.Groups.Select(g => g.Top).ToArray());
            Assert.Equal(3, cuts.Levels);
        }

        [Fact]
        public void EveryGroupThatOpensAllCutsOverEachFloorAndUnderEachCeiling()
        {
            var grounds = new[] { 0f, -0.5f, -1.2f, -2.4f, -3f, -4.6f, -5f, -9f };
            var ceilings = new[] { 4f, 2.5f, 1.4f, 3f, -0.2f, 0.5f, -2f, -6f };
            var cuts = LevelCuts.Of(grounds, ceilings, new int[grounds.Length]);
            Assert.Equal(grounds.Length, cuts.Groups.Sum(g => g.Rooms.Count));
            foreach (var group in cuts.Groups.Where(g => g.OpensAll))
            {
                foreach (var room in group.Rooms)
                {
                    Assert.True(group.Cut >= grounds[room] + PlaceView.LeastHeadroom - 0.001f, $"room {room} cut through what stands on it");
                    Assert.True(group.Cut <= ceilings[room] - PlaceView.UnderCeiling + 0.001f, $"room {room} keeps its roof");
                }
            }
        }

        [Fact]
        public void ARoomTooLowToStandInStandsAloneAndSaysItKeepsItsRoof()
        {
            var cuts = LevelCuts.Of(grounds: new[] { 0f, -3f }, ceilings: new[] { 1f, 2f }, levels: new[] { 0, 0 });
            var low = cuts.Groups[cuts.GroupOf(0)];
            Assert.Equal(new[] { 0 }, low.Rooms);
            Assert.False(low.OpensAll);
            Assert.Equal(0f + PlaceView.LeastHeadroom, low.Cut, 3);
        }

        [Fact]
        public void ARoomOnNoLevelBelongsToNoGroup()
        {
            var cuts = LevelCuts.Of(grounds: new[] { 0f, -1f }, ceilings: new[] { 3f, 3f }, levels: new[] { 0, -1 });
            Assert.Equal(-1, cuts.GroupOf(1));
            Assert.Equal(-1, cuts.LevelOf(1));
            Assert.Single(cuts.Groups);
        }

        [Fact]
        public void ALevelsFirstGroupIsTheOneItsMarkOpens()
        {
            var cuts = LevelCuts.Of(grounds: new[] { -2.66f, -5.3f, -20f }, ceilings: new[] { 3f, -3.1f, -15f }, levels: new[] { 0, 0, 1 });
            Assert.Equal(0, cuts.FirstOf(0));
            Assert.Equal(2, cuts.FirstOf(1));
            Assert.Equal(2, cuts.GroupsOf(0));
            Assert.Equal(1, cuts.GroupsOf(1));
        }
    }
}
