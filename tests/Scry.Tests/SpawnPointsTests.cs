using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class SpawnPointsTests
    {
        // A location's or room's creatures stand on the stage as its spawn points would put them
        // (CreatureSpawner): each point its own creature, but points of one group, within their two
        // radii of each other step by step, keep only so many alive, picked by weight; each
        // creature's stars rolled up from the point's least while a roll under its chance holds.

        private static SpawnPoint Point(float x, int group = 0, float radius = 0f, int most = 1, float weight = 1f, int min = 1, int max = 1, float chance = 0f) =>
            new SpawnPoint { At = new Vec3(x, 0f, 0f), Group = group, GroupRadius = radius, MaxInGroup = most, Weight = weight, MinLevel = min, MaxLevel = max, LevelUpChance = chance };

        /// <summary>Dice that give these numbers in turn, from 0 to 1, then 0.99 for ever.</summary>
        private static Func<double> Dice(params double[] rolls)
        {
            var next = 0;
            return () => next < rolls.Length ? rolls[next++] : 0.99;
        }

        [Fact]
        public void APointOnItsOwnAlwaysSpawns()
        {
            var points = new[] { Point(0f), Point(5f), Point(10f, group: 3) };
            Assert.Equal(new[] { 0, 1, 2 }, SpawnPoints.Roll(points, Dice()).Select(s => s.Point));
        }

        [Fact]
        public void AGroupKeepsOnlyItsMostPickedByWeight()
        {
            // Four points of group 7 a metre apart, one at a time; weights 1, 1, 2 and 0.
            var points = new[]
            {
                Point(0f, 7, 2f, weight: 1f), Point(1f, 7, 2f, weight: 1f), Point(2f, 7, 2f, weight: 2f), Point(3f, 7, 2f, weight: 0f),
            };
            // A roll of 0.7 of the whole weight, 4: past 1 and 2, inside the third's share.
            var spawned = SpawnPoints.Roll(points, Dice(0.7));
            Assert.Equal(2, Assert.Single(spawned).Point);

            // Two at a time: two different ones, never the one of no weight.
            var two = points.Select(p => { p.MaxInGroup = 2; return p; }).ToArray();
            var picked = SpawnPoints.Roll(two, Dice(0.1, 0.9)).Select(s => s.Point).ToList();
            Assert.Equal(2, picked.Distinct().Count());
            Assert.DoesNotContain(3, picked);
        }

        [Fact]
        public void APickLandingOnAShareEdgeGoesToTheNext()
        {
            // Two of weight 1: a roll of half the whole, 1, is past the first's share (Group.SpawnWeighted).
            var points = new[] { Point(0f, 7, 2f), Point(1f, 7, 2f) };
            Assert.Equal(1, Assert.Single(SpawnPoints.Roll(points, Dice(0.5))).Point);
        }

        [Fact]
        public void APointPickedIsNotPickedAgain()
        {
            // Two at a time from two: both, though the second roll lands where the first did.
            var points = new[] { Point(0f, 7, 2f, most: 2), Point(1f, 7, 2f, most: 2) };
            Assert.Equal(new[] { 0, 1 }, SpawnPoints.Roll(points, Dice(0.1, 0.1)).Select(s => s.Point));
        }

        [Fact]
        public void AGroupIsThePointsWithinTheirTwoRadiiStepByStep()
        {
            // 0 and 4 are 4 m apart, within 2 + 2; 4 and 8 likewise; 0 and 8 not, yet one group.
            var chain = new[] { Point(0f, 1, 2f), Point(4f, 1, 2f), Point(8f, 1, 2f) };
            Assert.Single(SpawnPoints.Groups(chain));
            Assert.Single(SpawnPoints.Roll(chain, Dice()));

            // Too far apart, or of another group: groups of their own.
            var apart = new[] { Point(0f, 1, 2f), Point(10f, 1, 2f), Point(1f, 2, 2f) };
            Assert.Equal(3, SpawnPoints.Groups(apart).Count);
            Assert.Equal(3, SpawnPoints.Roll(apart, Dice()).Count);
        }

        [Fact]
        public void APointOfNoRadiusOrNoMostIsNoGroupsAndSpawns()
        {
            var points = new[] { Point(0f, 1, 0f), Point(0.5f, 1, 0f), Point(1f, 1, 2f, most: 0), Point(1.5f, 1, 2f, most: 0) };
            Assert.Equal(4, SpawnPoints.Roll(points, Dice()).Count);

            // Beside a group of its own number, it still spawns on its own.
            var beside = new[] { Point(0f, 1, 2f), Point(1f, 1, 0f) };
            Assert.Equal(2, SpawnPoints.Roll(beside, Dice()).Count);
        }

        [Fact]
        public void StarsAreRolledUpFromTheLeastWhileARollHolds()
        {
            // 50%: a roll of 0.4 (40) holds, 0.6 (60) does not: one level up.
            Assert.Equal(2, SpawnPoints.Level(1, 3, 50f, Dice(0.4, 0.6)));
            // Never past the most.
            Assert.Equal(3, SpawnPoints.Level(1, 3, 100f, Dice(0.1, 0.1, 0.1)));
            // From the least, which may be above one.
            Assert.Equal(2, SpawnPoints.Level(2, 3, 10f, Dice(0.5)));
            Assert.Equal(1, SpawnPoints.Level(1, 1, 100f, Dice(0.0)));
        }

        [Fact]
        public void APlacesCreaturesAreLeftToChanceWhereAStarOrAPickIsRolled()
        {
            // Roll again shows for a place whose creatures can come out more than one way.
            Assert.True(SpawnPoints.LeftToChance(new[] { Point(0f, max: 3, chance: 10f) }));
            Assert.True(SpawnPoints.LeftToChance(new[] { Point(0f, 7, 2f), Point(1f, 7, 2f), Point(2f, 7, 2f) }));

            // Stars fixed, every point of a group kept, or a chance of none: one way only.
            Assert.False(SpawnPoints.LeftToChance(new[] { Point(0f, min: 2, max: 2, chance: 50f), Point(5f) }));
            Assert.False(SpawnPoints.LeftToChance(new[] { Point(0f, 7, 2f, most: 2), Point(1f, 7, 2f, most: 2) }));
            Assert.False(SpawnPoints.LeftToChance(new[] { Point(0f, max: 3, chance: 0f) }));
            Assert.False(SpawnPoints.LeftToChance(new SpawnPoint[0]));
        }

        [Fact]
        public void ACreatureStandsOnTheGroundUnderItsPointOrFliesOverIt()
        {
            // The game drops what it spawns to the first solid thing under a metre above its point.
            Assert.Equal(-6f, SpawnPoints.Rise(grounded: true, drop: 6f, lift: 0f), 3);
            Assert.Equal(0.4f, SpawnPoints.Rise(grounded: true, drop: -0.4f, lift: 0f), 3);
            // One that flies takes off and keeps at least so high over that ground.
            Assert.Equal(-3f, SpawnPoints.Rise(grounded: true, drop: 6f, lift: 3f), 3);
            // With nothing under it, it stays at its point, flying or not.
            Assert.Equal(0f, SpawnPoints.Rise(grounded: false, drop: 0f, lift: 0f), 3);
            Assert.Equal(0f, SpawnPoints.Rise(grounded: false, drop: 0f, lift: 3f), 3);
        }

        [Fact]
        public void ACreatureOnAFloorAboveACutIsCutAwayWithIt()
        {
            // Its ground above the cut, it stands on a floor above; one flying over the floor
            // opened belongs to that floor though it flies higher than the cut.
            Assert.True(SpawnPoints.AboveCut(feet: 12f, lift: 0f, cut: 10f));
            Assert.False(SpawnPoints.AboveCut(feet: 1f, lift: 0f, cut: 10f));
            Assert.False(SpawnPoints.AboveCut(feet: 11f, lift: 3f, cut: 10f));
            Assert.True(SpawnPoints.AboveCut(feet: 14f, lift: 3f, cut: 10f));
            Assert.False(SpawnPoints.AboveCut(feet: 50f, lift: 0f, cut: float.PositiveInfinity));
        }

        [Fact]
        public void EachCreatureSpawnedGetsItsOwnStars()
        {
            var points = new[] { Point(0f, max: 3, chance: 50f), Point(5f, min: 2, max: 2) };
            var spawned = SpawnPoints.Roll(points, Dice(0.2, 0.3, 0.9));
            Assert.Equal(new[] { (0, 3), (1, 2) }, spawned.Select(s => (s.Point, s.Level)));
        }
    }
}
