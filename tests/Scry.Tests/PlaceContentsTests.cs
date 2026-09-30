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
    }
}
