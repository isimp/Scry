using Xunit;

namespace Scry.Tests
{
    public class RaidFactsTests
    {
        // A raid's page tells when it is on the table, how it is rolled, and each creature it
        // brings with its stars, groups, time of day and whether it hunts you.

        [Fact]
        public void AWorldPickingRaidsByEachPlayersProgressSaysSoInPlaceOfTheKeys()
        {
            Assert.Equal("for a player whose own progress calls for it", RaidWords.OnTheTable(true, new[] { "defeated_bonemass" }, null, k => "Bonemass"));
            Assert.Equal(RaidWords.Starts(new[] { "defeated_bonemass" }, null, k => "Bonemass"), RaidWords.OnTheTable(false, new[] { "defeated_bonemass" }, null, k => "Bonemass"));
        }

        [Fact]
        public void ARaidOnlySomethingElseStartsIsNeverRolled()
        {
            Assert.Equal("never by the raid roll; only something else starts it", RaidWords.NeverRolled);
        }

        [Fact]
        public void EachCreatureItBringsIsALineOfItsOwn() => Assert.Equal("Brings Troll", RaidWords.Brings("Troll"));

        [Fact]
        public void WhatItBringsTellsItsStarsThenItsGroupsTimeOfDayAndHunting()
        {
            var line = RaidWords.BringLine("Troll", 2, 60f, 50f, 1, 3, 2, 4, night: true, day: false, hunts: true);
            Assert.Equal(SpawnWords.Stars(1, 3), line[4]);
            Assert.Equal($"{SpawnWords.Group(2, 4)}, at night, hunting you", line[5]);
            Assert.Equal("by day", RaidWords.BringLine("Boar", 2, 60f, 50f, 1, 1, 1, 1, night: false, day: true, hunts: false)[5]);
        }

        [Fact]
        public void ACreatureWithoutGroupsOrATimeOfItsOwnHasNothingMoreToSay()
        {
            // Coming by day and by night alike is no time of its own.
            Assert.Equal("\u2013", RaidWords.BringLine("Neck", 2, 60f, 50f, 1, 1, 1, 1, night: true, day: true, hunts: false)[5]);
        }
    }
}
