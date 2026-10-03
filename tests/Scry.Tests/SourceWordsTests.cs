using Xunit;

namespace Scry.Tests
{
    public class SourceWordsTests
    {
        // Each way a thing comes from something is read as figures (SourceFacts) and told here,
        // in the same words on every page, with how sure it is to give the thing.

        private static string NoBoss(string key) => null;

        [Fact]
        public void ACreaturesDropSaysHowManyAndHowOften()
        {
            var drop = SourceFacts.Dropped("Greydwarf", "Greydwarf", 1, 3, false, 0.05f);

            Assert.Equal("Dropped by Greydwarf, 1–2 (5%)", SourceWords.Line(drop, NoBoss));
            Assert.Equal(0.05, SourceWords.Sureness(drop), 6);
            Assert.Equal("Dropped by Troll, 2", SourceWords.Line(SourceFacts.Dropped("Troll", "Troll", 2, 3, false, 1f), NoBoss));
        }

        [Fact]
        public void ASureDropSaysNoChanceAndIsSure()
        {
            var drop = SourceFacts.Dropped("Eikthyr", "Eikthyr", 1, 1, true, 1f);

            Assert.Equal("Dropped by Eikthyr, 1 per player", SourceWords.Line(drop, NoBoss));
            Assert.Equal(1.0, SourceWords.Sureness(drop), 6);
        }

        [Fact]
        public void ADropsChanceAboveAllIsSureAndBelowNoneNever()
        {
            Assert.Equal(1.0, SourceWords.Sureness(SourceFacts.Dropped("A", "A", 1, 2, false, 1.5f)), 6);
            Assert.Equal(0.0, SourceWords.Sureness(SourceFacts.Dropped("A", "A", 1, 2, false, -0.5f)), 6);
        }

        [Fact]
        public void APickedOrGrownThingIsSure()
        {
            var picked = SourceFacts.Of(SourceWay.Picked, "RaspberryBush", "Raspberry bush");
            var grown = SourceFacts.Of(SourceWay.GrowsFrom, "sapling_carrot", "Carrot sapling");

            Assert.Equal("Picked from Raspberry bush", SourceWords.Line(picked, NoBoss));
            Assert.Equal("Grows from Carrot sapling", SourceWords.Line(grown, NoBoss));
            Assert.Equal(1.0, SourceWords.Sureness(picked), 6);
            Assert.Equal(1.0, SourceWords.Sureness(grown), 6);
        }

        [Theory]
        [InlineData(TableOf.Tree, "Felled from")]
        [InlineData(TableOf.Log, "Chopped from")]
        [InlineData(TableOf.Rock, "Mined from")]
        [InlineData(TableOf.Container, "Found in")]
        [InlineData(TableOf.Pickable, "Also picked from")]
        [InlineData(TableOf.Broken, "Broken out of")]
        [InlineData(TableOf.Other, "Comes out of")]
        public void ATablesLineStartsWithHowItGives(TableOf table, string verb)
        {
            Assert.Equal(verb, SourceWords.Verb(table));
        }

        [Fact]
        public void ATablesItemSaysItsOddsAndIsAsSureAsOneOpening()
        {
            var rolls = new DropTableInfo { Min = 2, Max = 2, Chance = 1f };
            rolls.Drops.Add(new DropInfo("Coins", 5, 10, 1f));
            rolls.Drops.Add(new DropInfo("Amber", 1, 1, 1f));
            var amber = SourceFacts.FromTable("TreasureChest_meadows", "Chest", TableOf.Container, rolls, rolls.Drops[1]);

            Assert.Equal("Found in Chest, 1 (50% a roll, 2 rolls)", SourceWords.Line(amber, NoBoss));
            Assert.Equal(0.75, SourceWords.Sureness(amber), 6);
        }

        [Fact]
        public void ATableWithNoWeightGivesNothing()
        {
            var rolls = new DropTableInfo();
            rolls.Drops.Add(new DropInfo("Stone", 1, 1, 0f));

            Assert.Equal(0.0, SourceWords.Sureness(SourceFacts.FromTable("rock", "Rock", TableOf.Rock, rolls, rolls.Drops[0])), 6);
        }

        [Fact]
        public void WhatAProducerMakesSaysItsPaceAndStore()
        {
            var honey = SourceFacts.Made("piece_beehive", "Beehive", 1200f, 4, "Meadows");
            var sap = SourceFacts.Made("piece_sapcollector", "Sap extractor", 60f, 10, "");

            Assert.Equal("Made by Beehive, one every " + Naming.Duration(1200f) + ", holding up to 4, in Meadows", SourceWords.Line(honey, NoBoss));
            Assert.Equal("Made by Sap extractor, one every " + Naming.Duration(60f) + ", holding up to 10", SourceWords.Line(sap, NoBoss));
            Assert.Equal(1.0, SourceWords.Sureness(honey), 6);
        }

        [Fact]
        public void ATradersWareSaysItsPriceAndWhenItIsSold()
        {
            var plain = SourceFacts.Sold("Haldor", "Haldor", 1, 750, "");
            var stack = SourceFacts.Sold("Haldor", "Haldor", 50, 50, "");
            var later = SourceFacts.Sold("Haldor", "Haldor", 1, 350, "defeated_gdking");

            Assert.Equal("Sold by Haldor, 750 coins", SourceWords.Line(plain, NoBoss));
            Assert.Equal("Sold by Haldor, 50 for 50 coins", SourceWords.Line(stack, NoBoss));
            Assert.Equal("Sold by Haldor, 350 coins, " + SpawnWords.Once("defeated_gdking", k => k == "defeated_gdking" ? "The Elder" : null),
                SourceWords.Line(later, k => k == "defeated_gdking" ? "The Elder" : null));
            Assert.Equal(1.0, SourceWords.Sureness(later), 6);
        }

        [Fact]
        public void YoungTellWhoTheyComeFrom()
        {
            Assert.Equal("Born to a tame Asksvin", SourceWords.Line(SourceFacts.Of(SourceWay.Born, "Asksvin", "Asksvin"), NoBoss));
            Assert.Equal("Born to a tame Asksvin with no partner near", SourceWords.Line(SourceFacts.BornAlone("Asksvin", "Asksvin"), NoBoss));
            Assert.Equal("Grows up from Lox calf", SourceWords.Line(SourceFacts.Of(SourceWay.GrowsUp, "Lox_Calf", "Lox calf"), NoBoss));
            Assert.Equal("Hatches from Asksvin egg", SourceWords.Line(SourceFacts.Of(SourceWay.Hatches, "AsksvinEgg", "Asksvin egg"), NoBoss));
        }

        [Fact]
        public void AFactsGiverIsWhatTheLineGoesTo()
        {
            Assert.Equal("Greydwarf", SourceFacts.Dropped("Greydwarf", "Greydwarf (shown)", 1, 2, false, 1f).Giver);
        }
    }
}
