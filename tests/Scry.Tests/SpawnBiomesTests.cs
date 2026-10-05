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
        public void ASpawnWaitingForAWorldEventMakesNoHome()
        {
            // Fimbulvinter's Jotun warriors spawn in every biome only while the jotun_invasion
            // event is on where they would spawn (SpawnSystem, m_requiredPersistentEvent).
            var (home, events) = SpawnBiomes.Split(new[] { "Meadows", "Swamp" }, new string[0], Of, persistentEvent: "jotun_invasion");
            Assert.Empty(home);
            Assert.Equal(new[] { "Meadows", "Swamp" }, events);
            Assert.Equal(new[] { "Meadows" }, SpawnBiomes.Split(new[] { "Meadows" }, new string[0], Of, persistentEvent: "").Home);
        }

        [Fact]
        public void WhereACreatureComesOnlyOnceAKeyIsSetIsNoHomeWhereItLivesWithoutOne()
        {
            // Charred warriors live in the Ashlands, and come to the other biomes once Fader is defeated.
            var (home, later) = SpawnBiomes.Later(new[] { "AshLands" }, new[] { "Meadows", "BlackForest", "AshLands" });
            Assert.Equal(new[] { "AshLands" }, home);
            Assert.Equal(new[] { "Meadows", "BlackForest" }, later);
        }

        [Fact]
        public void ACreatureThatComesOnlyOnceAKeyIsSetLivesWhereItThenComes()
        {
            // The Jotun warriors' patrols in the Deep North wait for a Jotun's fall; that is still their home.
            var (home, later) = SpawnBiomes.Later(new string[0], new[] { "DeepNorth" });
            Assert.Equal(new[] { "DeepNorth" }, home);
            Assert.Empty(later);
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
