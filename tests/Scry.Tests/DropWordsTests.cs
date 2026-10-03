using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class DropWordsTests
    {
        private static DropTableInfo Table(int min = 1, int max = 1, float chance = 1f, bool oneOfEach = false, params DropInfo[] drops) =>
            new DropTableInfo { Min = min, Max = max, Chance = chance, OneOfEach = oneOfEach, Drops = drops.ToList() };

        [Fact]
        public void ASingleSureDropNeedsNoMoreWords()
        {
            var table = Table(drops: new DropInfo("Wood", 10, 10, 1f));

            Assert.Equal("Drops", DropWords.Title(table));
            Assert.Equal("10", DropWords.Amount(table, table.Drops[0]));
        }

        [Fact]
        public void HowOftenAndHowManyTimesTheTableIsRolledAreTold()
        {
            var table = Table(2, 3, 0.5f, false, new DropInfo("Stone", 1, 1, 1f));

            Assert.Equal("Drops 2–3 times, 50% of the time", DropWords.Title(table));
        }

        [Fact]
        public void EachItemsShareOfARollIsToldWhereThereIsAChoice()
        {
            // A beech drops wood four times out of five and a beech seed one time out of five.
            var table = Table(drops: new[] { new DropInfo("Wood", 1, 3, 4f), new DropInfo("BeechSeeds", 1, 1, 1f) });

            Assert.Equal("1–3 (80%)", DropWords.Amount(table, table.Drops[0]));
            Assert.Equal("1 (20%)", DropWords.Amount(table, table.Drops[1]));
        }

        [Fact]
        public void OneOfEachDropsEveryItemOnceSoNoShareIsTold()
        {
            var table = Table(2, 2, 1f, true, new DropInfo("Resin", 1, 2, 1f), new DropInfo("Wood", 5, 5, 1f));

            Assert.Equal("Drops each of these once", DropWords.Title(table));
            Assert.Equal("1–2", DropWords.Amount(table, table.Drops[0]));
        }

        [Fact]
        public void ARareShareKeepsADecimalRatherThanReadingAsNone()
        {
            // An idol weighted 1 among 500 is 0.2% of a roll, not 0%.
            var table = Table(drops: new[] { new DropInfo("Coins", 5, 30, 499f), new DropInfo("Upgrader2Weapon", 1, 1, 1f) });

            Assert.Equal("1 (0.2%)", DropWords.Amount(table, table.Drops[1]));
            Assert.Equal("5–30 (99.8%)", DropWords.Amount(table, table.Drops[0]));
        }

        [Fact]
        public void AShareUnderATenthOfAPercentSaysSo()
        {
            var table = Table(drops: new[] { new DropInfo("Coins", 1, 1, 9999f), new DropInfo("Rare", 1, 1, 1f) });

            Assert.Equal("1 (under 0.1%)", DropWords.Amount(table, table.Drops[1]));
            Assert.Equal("1 (over 99.9%)", DropWords.Amount(table, table.Drops[0]));
        }

        [Fact]
        public void AnItemTellsItsOddsInEachTableThatGivesIt()
        {
            // As an item's details tell where it comes from: how many, its share of a roll, how many rolls.
            var chest = Table(3, 5, 1f, false, new DropInfo("Amber", 1, 2, 3f), new DropInfo("Coins", 5, 30, 10f));
            Assert.Equal("1–2 (23% a roll, 3–5 rolls)", DropWords.ForItem(chest, chest.Drops[0]));

            var tree = Table(drops: new DropInfo("Wood", 10, 10, 1f));
            Assert.Equal("10", DropWords.ForItem(tree, tree.Drops[0]));

            var bush = Table(1, 1, 0.5f, false, new DropInfo("Raspberry", 1, 3, 4f), new DropInfo("Seed", 1, 1, 1f));
            Assert.Equal("1–3 (80% a roll, 50% of the time)", DropWords.ForItem(bush, bush.Drops[0]));
        }

        [Fact]
        public void AnItemFromAOneOfEachTableSaysHowManyOfTheItemsArePicked()
        {
            var some = Table(2, 3, 1f, true, new DropInfo("A", 1, 1, 1f), new DropInfo("B", 1, 2, 1f), new DropInfo("C", 1, 1, 1f), new DropInfo("D", 1, 1, 1f));
            Assert.Equal("1–2 (at most once, 2–3 of 4 picked)", DropWords.ForItem(some, some.Drops[1]));

            var all = Table(2, 2, 1f, true, new DropInfo("Resin", 1, 2, 1f), new DropInfo("Wood", 5, 5, 1f));
            Assert.Equal("5", DropWords.ForItem(all, all.Drops[1]));
        }

        [Theory]
        [InlineData(2, 3, 1f, true, 5, "Holds 2–3 of these, each at most once")]
        [InlineData(3, 3, 1f, true, 3, "Holds each of these once")]
        [InlineData(2, 3, 1f, false, 4, "Holds 2–3 of these")]
        [InlineData(1, 1, 1f, false, 3, "Holds one of these")]
        [InlineData(1, 1, 1f, false, 1, "Holds")]
        [InlineData(2, 4, 0.5f, true, 6, "Holds 2–4 of these, each at most once, 50% of the time")]
        public void AChestsContentsAreToldAsWhatItHolds(int min, int max, float chance, bool oneOfEach, int items, string title)
        {
            var drops = Enumerable.Range(0, items).Select(i => new DropInfo("Item" + i, 1, 2, 1f)).ToArray();

            Assert.Equal(title, DropWords.HoldsTitle(Table(min, max, chance, oneOfEach, drops)));
        }

        [Fact]
        public void AnEmptyTableDropsNothing()
        {
            Assert.True(DropWords.IsEmpty(Table()));
            Assert.False(DropWords.IsEmpty(Table(drops: new DropInfo("Wood", 1, 1, 1f))));
        }

        [Theory]
        [InlineData(1, 3, "1–2")]
        [InlineData(1, 2, "1")]
        [InlineData(20, 30, "20–29")]
        [InlineData(3, 3, "3")]
        [InlineData(2, 1, "2")]
        public void ACreaturesDropNeverReachesItsTopAmount(int min, int max, string amount)
        {
            // CharacterDrop rolls Random.Range(int, int), which leaves the top value out:
            // a deer's hide set to 1 and 3 drops one or two.
            Assert.Equal(amount, DropWords.CreatureAmount(min, max, onePerPlayer: false));
        }

        [Fact]
        public void StarsDoubleACreaturesDropsForEachStarSaveThoseThatStayTheSame()
        {
            // CharacterDrop.GenerateDropList multiplies the chance and the amount by 2 to the power
            // of the stars, for each drop with m_levelMultiplier set; a troll's trophy has it off.
            Assert.Equal("amount and chance ×2 at 1 star, ×4 at 2 stars; Troll trophy stays the same",
                DropWords.StarDrops(2, new[] { "Troll trophy" }));
            Assert.Equal("amount and chance ×2 at 1 star, ×4 at 2 stars, ×8 at 3 stars", DropWords.StarDrops(3, new string[0]));
            Assert.Equal("amount and chance ×2 at 1 star; Deer trophy and Raw meat stay the same", DropWords.StarDrops(1, new[] { "Deer trophy", "Raw meat" }));
            Assert.Null(DropWords.StarDrops(0, new string[0]));
        }

        [Theory]
        [InlineData(1f, "1")]
        [InlineData(0.5f, "1 (50%)")]
        [InlineData(0.004f, "1 (0.4%)")]
        [InlineData(0.0004f, "1 (under 0.1%)")]
        public void ACreaturesDropTellsItsChanceAsEveryShareIsTold(float chance, string told)
        {
            // A rare drop keeps its decimal rather than reading as 0%, as every other share does.
            Assert.Equal(told, DropWords.CreatureDrop(1, 2, false, chance));
        }

        [Fact]
        public void ADropForEachPlayerSaysSo()
        {
            // The Elder's crypt key: the game drops one for each player online, whatever the amounts say.
            Assert.Equal("1 per player", DropWords.CreatureAmount(1, 1, onePerPlayer: true));
            Assert.Equal("1 per player", DropWords.CreatureAmount(2, 5, onePerPlayer: true));
        }

        [Fact]
        public void RangesAreWrittenTheSameEverywhere()
        {
            Assert.Equal("3", Numbers.CountRange(3, 3));
            Assert.Equal("1–4", Numbers.CountRange(1, 4));
            Assert.Equal("2", Numbers.CountRange(2, 1));
        }
    }
}
