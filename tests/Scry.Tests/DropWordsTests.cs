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
        public void AnEmptyTableDropsNothing()
        {
            Assert.True(DropWords.IsEmpty(Table()));
            Assert.False(DropWords.IsEmpty(Table(drops: new DropInfo("Wood", 1, 1, 1f))));
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
