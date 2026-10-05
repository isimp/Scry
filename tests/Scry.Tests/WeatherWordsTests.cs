using Xunit;

namespace Scry.Tests
{
    public class WeatherWordsTests
    {
        // What a weather a raid or a boss brings does (EnvSetup): wet soaks you outside without a
        // roof (Player.UpdateEnvStatusEffects), cold and freezing give their statuses by day or by
        // night (EnvMan.CalculateCold, CalculateFreezing), some are always dark, and each sets a
        // range of wind.

        [Fact]
        public void AWeatherTellsWhatItDoesToYou()
        {
            var storm = new WeatherFacts { Wet = true, ColdAtNight = true, WindMin = 0.5f, WindMax = 1f };
            Assert.Equal("wet outside without a roof, cold at night, wind 50–100%", WeatherWords.Effects(storm));
        }

        [Fact]
        public void FreezingOutranksColdByDayAndByNight()
        {
            Assert.Equal("freezing, wind 0–20%", WeatherWords.Effects(new WeatherFacts { Freezing = true, Cold = true, ColdAtNight = true, WindMax = 0.2f }));
            Assert.Equal("cold, freezing at night, wind 0–20%", WeatherWords.Effects(new WeatherFacts { Cold = true, FreezingAtNight = true, WindMax = 0.2f }));
        }

        [Fact]
        public void ABiomeSaysWhatItsWeathersPutOnYouInAllTheWorstByDayAndAtNight()
        {
            // The Mountain's: one weather freezing, another only cold.
            Assert.Equal("Freezing", WeatherWords.PutsOnYou(new[] { new WeatherFacts { Freezing = true, Cold = true }, new WeatherFacts { Cold = true } }));
            // The Swamp's rain soaks you; the Deep North's clear sky is cold by day and freezing at night.
            Assert.Equal("Wet", WeatherWords.PutsOnYou(new[] { new WeatherFacts { Wet = true }, new WeatherFacts() }));
            Assert.Equal("Wet, cold, freezing at night", WeatherWords.PutsOnYou(new[] { new WeatherFacts { Wet = true }, new WeatherFacts { Cold = true, FreezingAtNight = true } }));
            Assert.Equal("Cold at night", WeatherWords.PutsOnYou(new[] { new WeatherFacts { ColdAtNight = true } }));
            Assert.Equal("Nothing", WeatherWords.PutsOnYou(new[] { new WeatherFacts { WindMax = 1f } }));
            Assert.Equal("Nothing", WeatherWords.PutsOnYou(new WeatherFacts[0]));
            Assert.Equal("freezing at night, wind 0–20%", WeatherWords.Effects(new WeatherFacts { ColdAtNight = true, FreezingAtNight = true, WindMax = 0.2f }));
            Assert.Equal("cold, wind 0–20%", WeatherWords.Effects(new WeatherFacts { Cold = true, ColdAtNight = true, WindMax = 0.2f }));
        }

        [Fact]
        public void DarknessAndStillAirAreSaid()
        {
            Assert.Equal("always dark, no wind", WeatherWords.Effects(new WeatherFacts { AlwaysDark = true }));
        }

        [Fact]
        public void TheLineNamesTheWeatherThenWhatItDoes()
        {
            Assert.Equal("Thunder storm: wet outside without a roof, no wind", WeatherWords.Line("Thunder storm", new WeatherFacts { Wet = true }));
        }
    }
}
