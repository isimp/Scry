using Xunit;

namespace Scry.Tests
{
    public class BiomeWeatherTests
    {
        // A biome's weathers are counted out of the ones its own table brings. Inside a crypt, during
        // a raid or a boss fight, in an altered stretch of a biome or in the intro, the game puts
        // another weather in place of the biome's, which is none of the biome's own.

        [Fact]
        public void AWeatherTheBiomeBroughtCounts()
        {
            Assert.True(BiomeWeather.Own(forced: "", overriding: null));
        }

        [Fact]
        public void AWeatherSomethingElseForcedDoesNot()
        {
            Assert.False(BiomeWeather.Own(forced: "Crypt", overriding: null));
            Assert.False(BiomeWeather.Own(forced: "", overriding: "ThunderStorm"));
        }
    }
}
