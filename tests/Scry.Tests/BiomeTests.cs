using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class BiomeTests
    {
        // Each biome is an entry of its own, no prefab, with a key and a tab of its own; its page
        // tells its weathers with how likely each is (EnvMan's biome setup, picked by weight),
        // its music by the time of day, and what lives, grows and stands there.

        [Fact]
        public void ABiomeIsKeptUnderAKeyOfItsOwn()
        {
            Assert.Equal("biome:Swamp", EntryKeys.For(Kind.Biome, "Swamp"));
            Assert.Equal("Swamp", EntryKeys.Split("biome:Swamp", out var kind));
            Assert.Equal(Kind.Biome, kind);
            Assert.True(EntryKeys.HasOwnNamespace(Kind.Biome));
        }

        [Fact]
        public void BiomesHaveATabAndASearchTermOfTheirOwn()
        {
            Assert.Equal("Biomes", Kinds.Label(Kind.Biome));
            Assert.True(Search.KindMatches(Kind.Biome, "biome"));
            Assert.False(Search.KindMatches(Kind.Location, "biome"));
        }

        [Fact]
        public void BiomesAreListedInTheOrderPlayersMeetThem()
        {
            Assert.True(BiomeWords.Rank("Meadows") < BiomeWords.Rank("BlackForest"));
            Assert.True(BiomeWords.Rank("Mistlands") < BiomeWords.Rank("AshLands"));
            Assert.True(BiomeWords.Rank("Ocean") < BiomeWords.Rank("SomeModBiome"));
            Assert.True(BiomeWords.Rank("SomeModBiome") <= 63);
        }

        [Fact]
        public void EachWeatherTellsHowLikelyItIsMostLikelyFirst()
        {
            var weathers = BiomeWords.Weathers(new List<(string, float)> { ("Rain", 1f), ("Clear", 2.5f), ("Misty", 0.5f), ("Rain", 1f) });

            Assert.Equal(new[] { ("Clear", "50%"), ("Rain", "40%"), ("Misty", "10%") }, weathers);
            Assert.Empty(BiomeWords.Weathers(new List<(string, float)> { ("Clear", 0f) }));
            Assert.Empty(BiomeWords.Weathers(new List<(string, float)>()));
        }

        [Fact]
        public void ItsWeathersAreATableOfEachWeatherItsShareAndWhatItDoes()
        {
            Assert.Equal("Weathers", BiomeWords.WeathersTitle);
            Assert.Equal(new[] { "Weather", "Of the time", "What it does" }, BiomeWords.WeatherColumns);
        }

        [Fact]
        public void ItsMusicIsATableByTheTimeOfDay()
        {
            Assert.Equal("Music", BiomeWords.MusicTitle);
            Assert.Equal(new[] { "Music", "When" }, BiomeWords.MusicColumns);
            Assert.Equal(new[] { ("In the morning", "morning"), ("By day", "meadows"), ("At night", "night") },
                BiomeWords.Music("morning", "", "meadows", "night"));
            Assert.Equal(("In the evening", "evening"), BiomeWords.Music(null, "evening", null, null)[0]);
            Assert.Empty(BiomeWords.Music(null, "", null, ""));
        }
    }
}
