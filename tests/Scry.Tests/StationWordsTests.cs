using Xunit;

namespace Scry.Tests
{
    public class StationWordsTests
    {
        // What a station, smelter, kiln, oven, fermenter, hive, tap, fire, incinerator, shield or
        // altar does, in a player's words: what it burns and holds, how fast, and what it needs.

        [Fact]
        public void ASmelterTellsItsFuelForEachAndWhatItHolds()
        {
            Assert.Equal("Coal, 2 for each", StationWords.BurnsForEach("Coal", 2));
            Assert.Equal("10 to process, 20 fuel", StationWords.Holds(10, 20));
            Assert.Equal("10 to process", StationWords.Holds(10, null));
        }

        [Fact]
        public void AFireTellsWhatItBurnsHowOftenAndHowMuchItHolds()
        {
            Assert.Equal("Wood, one every 5 s", StationWords.BurnsOneEvery("Wood", 5f));
            Assert.Equal("10 fuel", StationWords.Fuel(10));
            Assert.Equal("2.5 fuel", StationWords.Fuel(2.5f));
        }

        [Fact]
        public void AnOvenTellsHowManyCookAtATime() => Assert.Equal("4 at a time", StationWords.AtATime(4));

        [Fact]
        public void WhatAStationNeedsIsTold()
        {
            Assert.Equal("a roof", StationWords.Roof);
            Assert.Equal("a fire under it", StationWords.FireUnder);
            Assert.Equal("a roof, and cover on most sides", StationWords.Sheltered);
            Assert.Equal("open sky, less than 40% covered", StationWords.OpenSky(0.4f));
            Assert.Equal("to be built on Ancient root, and takes only the sap it has left", StationWords.BuiltOn("Ancient root"));
        }

        [Theory]
        [InlineData(true, true, "a roof and a fire")]
        [InlineData(true, false, "a roof")]
        [InlineData(false, true, "a fire")]
        [InlineData(false, false, null)]
        public void CraftingTellsWhatItNeedsOrNothing(bool roof, bool fire, string needs) => Assert.Equal(needs, StationWords.CraftingNeeds(roof, fire));

        [Fact]
        public void WhatItMakesIsToldWithItsPace() => Assert.Equal("Honey, one every 20 min", StationWords.Makes("Honey", "one every 20 min"));

        [Fact]
        public void AnIncineratorTellsWhatAnythingElseBecomes() => Assert.Equal("becomes Coal, one for every 3", StationWords.Incinerates("Coal", 3));

        [Fact]
        public void AShieldTellsWhetherItBurnsAnyOfSeveral()
        {
            Assert.Equal("Burns", StationWords.BurnsAnyOf(several: false));
            Assert.Equal("Burns any one of these", StationWords.BurnsAnyOf(several: true));
        }

        [Fact]
        public void AnUpgraderTellsWhatItTakes() => Assert.Equal("takes items past their top quality, with the upgrade kits their recipes name", StationWords.Upgrader);

        [Fact]
        public void ATraderTellsWhenItsWaresAreSold()
        {
            Assert.Equal("Sells", StationWords.Sells(null));
            Assert.Equal("Sells once Eikthyr is slain", StationWords.Sells("once Eikthyr is slain"));
        }

        [Fact]
        public void AnAltarTellsWhomItSummonsAndWhere()
        {
            Assert.Equal("Summons Eikthyr with", StationWords.Summons("Eikthyr"));
            Assert.Equal("Summoned at Mystical altar with", StationWords.SummonedAt("Mystical altar", onStands: false));
            Assert.Equal("Summoned at Mystical altar with these on its item stands", StationWords.SummonedAt("Mystical altar", onStands: true));
        }
    }
}
