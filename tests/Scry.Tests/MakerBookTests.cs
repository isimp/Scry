using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class MakerBookTests
    {
        private static Making Turns(string station, string from, string to, int makes = 1) =>
            new Making { Station = station, Output = to, Inputs = { (from, 1) }, Makes = makes };

        [Fact]
        public void AStationThatTurnsSeveralItemsIntoOneIsOneRowOfAnyOne()
        {
            // A smelter makes copper from copper ore or from copper scrap.
            var book = new MakerBook();
            book.Add(Turns("smelter", "CopperOre", "Copper"));
            book.Add(Turns("smelter", "CopperScrap", "Copper"));
            book.Add(Turns("smelter", "TinOre", "Tin"));

            var copper = book.Of("Copper").Single();
            Assert.True(copper.AnyOne);
            Assert.Equal(new[] { "CopperOre", "CopperScrap" }, copper.Inputs.Select(i => i.Item));
            Assert.Equal("Made in Smelter, from any one of these", MakerBook.ItemTitle("Smelter", copper));
        }

        [Fact]
        public void DifferentStationsStayApart()
        {
            var book = new MakerBook();
            book.Add(Turns("smelter", "IronScrap", "Iron"));
            book.Add(Turns("blastfurnace", "IronScrap", "Iron"));

            Assert.Equal(new[] { "smelter", "blastfurnace" }, book.Of("Iron").Select(m => m.Station));
            Assert.All(book.Of("Iron"), m => Assert.False(m.AnyOne));
        }

        [Fact]
        public void HowManyOneBatchMakesIsTold()
        {
            var book = new MakerBook();
            book.Add(Turns("fermenter", "MeadBaseHealthMinor", "MeadHealthMinor", 6));

            var mead = book.Of("MeadHealthMinor").Single();
            Assert.Equal("Made in Fermenter, makes 6", MakerBook.ItemTitle("Fermenter", mead));
            Assert.Equal("Makes 6 Minor healing mead from", MakerBook.StationTitle("Minor healing mead", mead));
        }

        [Fact]
        public void ARecipeNeedingAllItsIngredientsIsNotMergedWithOthers()
        {
            // An obliterator conversion that takes two things at once.
            var book = new MakerBook();
            book.Add(new Making { Station = "incinerator", Output = "Coal", Inputs = { ("Wood", 2), ("Resin", 1) } });
            book.Add(Turns("incinerator", "Stone", "Coal"));

            var rows = book.Of("Coal");
            Assert.Equal(2, rows.Count);
            Assert.Equal("Makes Coal from", MakerBook.StationTitle("Coal", rows[0]));
            Assert.Equal(2, rows[0].Inputs.Count);
        }

        [Fact]
        public void AStationTellsEverythingItMakesInTheOrderNoted()
        {
            var book = new MakerBook();
            book.Add(Turns("smelter", "CopperOre", "Copper"));
            book.Add(Turns("smelter", "TinOre", "Tin"));
            book.Add(Turns("smelter", "CopperScrap", "Copper"));
            book.Add(Turns("kiln", "Wood", "Coal"));

            var made = book.At("smelter");
            Assert.Equal(new[] { "Copper", "Tin" }, made.Select(m => m.Output));
            Assert.Equal("Makes Copper from any one of these", MakerBook.StationTitle("Copper", made[0]));
        }

        [Fact]
        public void AConversionTakingSeveralThingsNotedTwiceIsKeptOnce()
        {
            // The same conversion listed twice on one station is told once.
            var book = new MakerBook();
            for (var i = 0; i < 2; i++) book.Add(new Making { Station = "incinerator", Output = "Coal", Inputs = { ("Wood", 2), ("Resin", 1) } });

            Assert.Single(book.Of("Coal"));
        }

        [Fact]
        public void TheSameConversionNotedTwiceIsKeptOnce()
        {
            var book = new MakerBook();
            book.Add(Turns("smelter", "CopperOre", "Copper"));
            book.Add(Turns("smelter", "CopperOre", "Copper"));

            var copper = book.Of("Copper").Single();
            Assert.False(copper.AnyOne);
            Assert.Single(copper.Inputs);
        }
    }
}
