using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class SpawnWordsTests
    {
        private static readonly Dictionary<string, string> Bosses = new Dictionary<string, string>
        {
            { "defeated_bonemass", "Bonemass" },
            { "defeated_eikthyr", "Eikthyr" },
        };

        private static string Boss(string key) => Bosses.TryGetValue(key, out var boss) ? boss : null;

        [Theory]
        [InlineData(1, 1, "no stars")]
        [InlineData(1, 3, "up to 2 stars")]
        [InlineData(1, 2, "up to 1 star")]
        [InlineData(2, 3, "1–2 stars")]
        [InlineData(2, 2, "1 star")]
        [InlineData(3, 3, "2 stars")]
        public void StarsAreCountedAsTheRestOfThePanelCountsThem(int minLevel, int maxLevel, string words)
        {
            // The game's level is one more than the stars shown over a creature's head.
            Assert.Equal(words, SpawnWords.Stars(minLevel, maxLevel));
        }

        [Theory]
        [InlineData(1, 1, null)]
        [InlineData(2, 3, "in groups of 2–3")]
        [InlineData(3, 3, "in groups of 3")]
        public void GroupSizesUseTheSameRangesAsDrops(int min, int max, string words)
        {
            Assert.Equal(words, SpawnWords.Group(min, max));
        }

        // A spawner (SpawnArea) checks every two seconds, adding two seconds to its timer, and
        // spawns once the timer is past its interval: a 10 s interval spawns every 12 s.

        [Theory]
        [InlineData(10f, "one every 12 s")]
        [InlineData(30f, "one every 32 s")]
        [InlineData(5f, "one every 6 s")]
        [InlineData(9.9f, "one every 10 s")]
        [InlineData(0f, "one every 2 s")]
        public void ASpawnerSpawnsOnItsTwoSecondBeatPastItsInterval(float interval, string words)
        {
            Assert.Equal(words, SpawnWords.SpawnerPace(interval));
        }

        [Fact]
        public void ASpawnerWakesWhileSomeoneIsNear()
        {
            Assert.Equal("while someone is within 60 m", SpawnWords.SpawnerWakes(60f));
        }

        [Fact]
        public void ASpawnerStopsAtItsCapsNearAndFar()
        {
            Assert.Equal("3 within 20 m, 100 within 1,000 m", SpawnWords.SpawnerCaps(3, 20f, 100, 1000f));
        }

        [Fact]
        public void ASpawnerPlacesWithinItsRadiusAndSaysWhenOnlyOnOpenGround()
        {
            Assert.Equal("up to 4 m away", SpawnWords.SpawnerPlaces(4f, groundOnly: false));
            Assert.Equal("up to 2.28 m away, on open ground only", SpawnWords.SpawnerPlaces(2.28f, groundOnly: true));
        }

        [Fact]
        public void EachCreatureOfASpawnersPoolHasItsShareOfTheSpawns()
        {
            // SpawnArea.SelectWeightedPrefab picks by weight among all of the pool.
            Assert.Equal("71% of the spawns, up to 2 stars", SpawnWords.PoolShare(5f, 7f, 1, 3));
            Assert.Equal("14% of the spawns, no stars", SpawnWords.PoolShare(1f, 7f, 1, 1));
        }

        [Fact]
        public void ASpawnerWithOneCreatureAlwaysSpawnsIt()
        {
            Assert.Equal("every spawn, up to 2 stars", SpawnWords.PoolShare(1f, 1f, 1, 3));
            Assert.Equal("every spawn, up to 2 stars", SpawnWords.PoolShare(0f, 0f, 1, 3));
        }

        [Fact]
        public void EachFurtherStarNeedsItsOwnRoll()
        {
            // SpawnArea.SpawnOne rolls once for each level above the least, stopping at the first miss.
            Assert.Equal("15% for each star", SpawnWords.StarChance(15f));
            Assert.Null(SpawnWords.StarChance(0f));
        }

        [Fact]
        public void ABossKeyNamesTheBossAndAnyOtherKeyIsToldAsAWorldKey()
        {
            Assert.Equal("once Bonemass is defeated", SpawnWords.Once("defeated_bonemass", Boss));
            Assert.Equal("once the world key \"KilledTroll\" is set", SpawnWords.Once("KilledTroll", Boss));
            Assert.Equal("until Eikthyr is defeated", SpawnWords.Until("defeated_eikthyr", Boss));
            Assert.Equal("until the world key \"nomap\" is set", SpawnWords.Until("nomap", Boss));
        }

        [Fact]
        public void ASpawnLineTellsWhereWhenHowManyAndWhatItWaitsFor()
        {
            var spawn = new SpawnFacts
            {
                Biomes = "Swamp", AtNight = true, AtDay = false, MinLevel = 1, MaxLevel = 3, GroupMin = 2, GroupMax = 3,
                Keys = new[] { "defeated_eikthyr" }, Weather = new[] { "Rain", "Thunder storm" }, InForest = true, OutsideForest = false,
            };

            Assert.Equal("Spawns in Swamp, at night, up to 2 stars, in groups of 2–3, in forests, in weather Rain or Thunder storm, once Eikthyr is defeated",
                SpawnWords.Line("Spawns in", spawn, Boss));
        }

        [Fact]
        public void WhatASpawnDoesNotLimitIsLeftOut()
        {
            var spawn = new SpawnFacts { Biomes = "Meadows", AtNight = true, AtDay = true, MinLevel = 1, MaxLevel = 1, GroupMin = 1, GroupMax = 1, InForest = true, OutsideForest = true };

            Assert.Equal("Spawns in Meadows, no stars", SpawnWords.Line("Spawns in", spawn, Boss));
        }

        [Fact]
        public void OutsideForestsAndByDayAreToldToo()
        {
            var spawn = new SpawnFacts { Biomes = "Plains", AtNight = false, AtDay = true, MinLevel = 1, MaxLevel = 1, GroupMin = 1, GroupMax = 1, InForest = false, OutsideForest = true };

            Assert.Equal("Spawns in Plains, by day, no stars, outside forests", SpawnWords.Line("Spawns in", spawn, Boss));
        }

        [Fact]
        public void ARaidWaitsForSomeKeysAndEndsWithOthers()
        {
            var raid = new SpawnFacts { Biomes = "Black Forest", Keys = new[] { "defeated_eikthyr" }, NotKeys = new[] { "defeated_bonemass" } };

            Assert.Equal("Comes in the raid \"The forest is moving\", in Black Forest, once Eikthyr is defeated, until Bonemass is defeated",
                SpawnWords.Line("Comes in the raid \"The forest is moving\", in", raid, Boss));
        }
    }
}
