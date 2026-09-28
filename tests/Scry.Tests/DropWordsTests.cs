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
            Assert.Equal("3", DropWords.Range(3, 3));
            Assert.Equal("1–4", DropWords.Range(1, 4));
            Assert.Equal("2", DropWords.Range(2, 1));
        }
    }
}
