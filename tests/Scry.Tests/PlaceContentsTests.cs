using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PlaceContentsTests
    {
        // A location's parts are rolled when the game first builds its zone: each RandomSpawn at
        // its chance, each RandomObject picking one of its objects by weight (ZoneSystem.SpawnLocation).

        [Fact]
        public void PartsOfOnePrefabAtOneChanceAreCountedTogether()
        {
            var parts = PlaceParts.Group(new[] { ("MushroomYellow", 0.2f), ("MushroomYellow", 0.2f), ("MushroomYellow", 0.2f), ("Spawner_Skeleton", 0.33f) });

            Assert.Equal(2, parts.Count);
            Assert.Equal(3, parts.Single(p => p.Prefab == "MushroomYellow").Count);
        }

        [Fact]
        public void TheSamePrefabAtAnotherChanceIsAPartOfItsOwn()
        {
            var parts = PlaceParts.Group(new[] { ("SurtlingCoreStand", 0.55f), ("SurtlingCoreStand", 0.757f), ("SurtlingCoreStand", 0.55f) });

            Assert.Equal(new[] { (1, 0.757f), (2, 0.55f) }, parts.Select(p => (p.Count, p.Chance)).ToArray());
        }

        [Fact]
        public void WhatIsAlwaysThereComesFirstThenTheLikeliest()
        {
            var parts = PlaceParts.Group(new[] { ("B", 0.2f), ("Spawner", 1f), ("A", 0.5f), ("Chest", 1f) });

            Assert.Equal(new[] { "Chest", "Spawner", "A", "B" }, parts.Select(p => p.Prefab).ToArray());
        }

        [Fact]
        public void ChancesThatDifferOnlyByRoundingAreOne()
        {
            var parts = PlaceParts.Group(new[] { ("A", 0.33f), ("A", 0.3300001f) });

            Assert.Single(parts);
        }

        [Theory]
        [InlineData(1, 1f, "")]
        [InlineData(2, 1f, "2")]
        [InlineData(1, 0.33f, "33%")]
        [InlineData(6, 0.2f, "6, 20% each")]
        [InlineData(1, 0.004f, "0.4%")]
        public void APartSaysHowManyAndHowLikely(int count, float chance, string words)
        {
            Assert.Equal(words, PlaceParts.Amount(count, chance));
        }

        // RandomObject.GetWeightedObject: a roll from 0 to the sum of every entry's weight, an
        // entry without an object included, picks the first entry whose running weight reaches it.

        [Fact]
        public void APickTakesTheFirstObjectWhoseRunningWeightReachesTheRoll()
        {
            var weights = new[] { 1f, 3f };

            Assert.Equal(0, PlaceParts.Pick(weights, 0.0));
            Assert.Equal(0, PlaceParts.Pick(weights, 0.25));
            Assert.Equal(1, PlaceParts.Pick(weights, 0.26));
            Assert.Equal(1, PlaceParts.Pick(weights, 1.0));
        }

        [Fact]
        public void AnEntryWithoutAnObjectTakesItsShareOfThePicks()
        {
            Assert.Equal(new[] { 0.25f, 0.75f }, PlaceParts.Shares(new[] { 1f, 3f }));
            Assert.Equal(new[] { 0f, 0f }, PlaceParts.Shares(new[] { 0f, 0f }));
        }

        // Roll again shows a place otherwise only where the game rolls: a RandomSpawn between
        // never and always, or a RandomObject with two or more entries it can pick.

        [Fact]
        public void APlaceWhoseRollsAllComeOutOneWayHasNothingToRollAgain()
        {
            Assert.False(PlaceParts.LeftToChance(new[] { 100f, 0f }, new[] { new[] { 1f }, new[] { 2f, 0f } }));
            Assert.False(PlaceParts.LeftToChance(new float[0], new float[0][]));
        }

        [Fact]
        public void ASpawnBetweenNeverAndAlwaysOrAPickOfTwoCanComeOutOtherwise()
        {
            Assert.True(PlaceParts.LeftToChance(new[] { 100f, 50f }, new float[0][]));
            Assert.True(PlaceParts.LeftToChance(new float[0], new[] { new[] { 1f, 3f } }));
        }
    }
}
