using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PlacingWordsTests
    {
        // An item a build tool places as a piece of itself, as the Serving Tray places food, tells
        // on its page what places it and what that uses up, what a placed one does, and what it
        // stands as, in a topic of its own rather than as a piece's build cost.

        [Fact]
        public void APlacedItemSaysWhatPlacesItAndWhatThatUsesUp()
        {
            Assert.Equal("Serving Tray, using up one", PlacingWords.With("Serving Tray", 1));
            Assert.Equal("Serving Tray, using up 2", PlacingWords.With("Serving Tray", 2));
            // A cost besides the item itself goes as a row of its own.
            Assert.Equal("Serving Tray", PlacingWords.With("Serving Tray", 0));
        }

        [Fact]
        public void APlacedFoodIsEatenWhereItStandsAnythingElsePickedUp()
        {
            // ItemDrop.Interact: a placed consumable is eaten or drunk with use, the alternate use picks it up; anything else use picks up. A placed one never despawns (ItemDrop.SlowUpdate).
            Assert.Equal("eaten from where it stands with use, picked up with the alternate use, and never despawns", PlacingWords.Once(consumable: true, drink: false));
            Assert.Equal("drunk from where it stands with use, picked up with the alternate use, and never despawns", PlacingWords.Once(consumable: true, drink: true));
            Assert.Equal("picked up again with use, and never despawns", PlacingWords.Once(consumable: false, drink: false));
        }

        [Fact]
        public void APlacedItemStandsAsAPieceOfItsMaterial()
        {
            Assert.Equal("a wooden piece with 10 health", PlacingWords.StandsAs("Wood", 10f));
            Assert.Equal("an iron piece with 300 health", PlacingWords.StandsAs("Iron", 300f));
            Assert.Equal("a hardwood piece with 50 health", PlacingWords.StandsAs("HardWood", 50f));
            Assert.Equal("an ashstone piece with 1,000 health", PlacingWords.StandsAs("Ashstone", 1000f));
            // A material a mod adds goes by its own name.
            Assert.Equal("a glass piece with 5 health", PlacingWords.StandsAs("Glass", 5f));
        }

        [Fact]
        public void PlacingItIsATopicOfItsOwnOnAnItemsPage()
        {
            var pairs = new[] { ("Weight", ""), ("Placed with", "placing"), ("Once placed", "placing"), ("Stands as", "placing"), ("Rain", "placing"), ("Placed", "placing") };
            var plan = FactLayout.Plan(Kind.Item, pairs, new (string, string)[0], new[] { FactBlock.Uses });
            var placing = plan.Single(t => t.Heading == PlacingWords.Title);
            Assert.Equal("placing", placing.Id);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, placing.Bits.Select(b => b.Pair));
            // After what it is used for, before More.
            var headings = plan.Select(t => t.Heading).ToList();
            Assert.True(headings.IndexOf("What it is used for") < headings.IndexOf(PlacingWords.Title));
        }
    }
}
