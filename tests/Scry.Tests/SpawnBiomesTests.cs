using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class SpawnBiomesTests
    {
        // A world spawn that asks for a weather spawns only while that weather is on. A biome
        // whose own weathers include it is the creature's home; one where only a world event
        // brings that weather (Fimbulvinter's, an invasion's) sees it only during the event.

        private static readonly Dictionary<string, string[]> Weathers = new Dictionary<string, string[]>
        {
            ["Meadows"] = new[] { "Clear", "Rain", "Misty" },
            ["Swamp"] = new[] { "SwampRain", "Misty" },
            ["Mountain"] = new[] { "Snow", "SnowStorm" },
        };

        private static ICollection<string> Of(string biome) => Weathers.TryGetValue(biome, out var w) ? w : new string[0];

        [Fact]
        public void ASpawnAskingNoWeatherIsAtHomeInEveryBiome()
        {
            var (home, events) = SpawnBiomes.Split(new[] { "Meadows", "Swamp" }, new string[0], Of);
            Assert.Equal(new[] { "Meadows", "Swamp" }, home);
            Assert.Empty(events);
        }

        [Fact]
        public void ABiomeWithTheWeatherOfItsOwnIsHome()
        {
            var (home, events) = SpawnBiomes.Split(new[] { "Meadows", "Swamp", "Mountain" }, new[] { "Misty" }, Of);
            Assert.Equal(new[] { "Meadows", "Swamp" }, home);
            Assert.Equal(new[] { "Mountain" }, events);
        }

        [Fact]
        public void AWeatherOnlyAnEventBringsMakesNoHome()
        {
            var (home, events) = SpawnBiomes.Split(new[] { "Meadows", "Swamp", "Mountain" }, new[] { "Fimbulvinter_Meadows", "Fimbulvinter_Swamp", "" }, Of);
            Assert.Empty(home);
            Assert.Equal(new[] { "Meadows", "Swamp", "Mountain" }, events);
        }

        [Fact]
        public void BlankWeathersAskForNothing()
        {
            var (home, events) = SpawnBiomes.Split(new[] { "Mountain" }, new[] { "", null }, Of);
            Assert.Equal(new[] { "Mountain" }, home);
            Assert.Empty(events);
        }
    }
}
