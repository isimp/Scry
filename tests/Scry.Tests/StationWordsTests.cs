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

        [Fact]
        public void AWindmillSaysWhatWindItNeedsAndWhatBlocksIt()
        {
            // Windmill.GetPowerOutput: (1 - cover) times the wind's strength stepped from its least
            // to full; Cover.GetCoverForPoint: 17 rays of 30 m from the propeller's hub, none down.
            Assert.Equal("10 s in full wind, longer as the wind weakens or is blocked", StationWords.InFullWind(10f));
            Assert.Equal("wind: none below 10% strength, full at 100%, whichever way it faces", StationWords.RunsOnWind(0.1f));
            Assert.Equal("anything solid within 30 m of its hub, level with it or above; each of 17 directions blocked takes 6%", StationWords.WindBlockedBy);
            Assert.Equal("hurt what they touch above 30% power", StationWords.BladesHurt(180f, -600f));
            Assert.Null(StationWords.BladesHurt(180f, 0f));
            Assert.Equal("an hour at most, at the wind blowing on return", StationWords.CatchesUp(3600f));
            Assert.Equal("10 min at most, at the wind blowing on return", StationWords.CatchesUp(600f));
        }
    }
}
