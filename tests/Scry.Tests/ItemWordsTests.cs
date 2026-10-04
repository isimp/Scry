using Xunit;

namespace Scry.Tests
{
    public class ItemWordsTests
    {
        // Some answers are worth telling even when they are "no": whether an item goes through
        // portals, whether gear can be upgraded, whether it wears out, and where it is repaired.

        [Fact]
        public void AnItemSaysWhetherItGoesThroughPortals()
        {
            Assert.Equal("can go through", ItemWords.Portals(true));
            Assert.Equal("cannot go through", ItemWords.Portals(false));
        }

        [Fact]
        public void GearSaysHowFarItCanBeUpgraded()
        {
            Assert.Equal("up to 4", ItemWords.Quality(4));
            Assert.Equal("1 only: it cannot be upgraded", ItemWords.Quality(1));
            Assert.Equal("1 only: it cannot be upgraded", ItemWords.Quality(0));
        }

        [Fact]
        public void GearThatNeverWearsOutSaysSo()
        {
            Assert.Equal("does not wear out", ItemWords.NoWear);
        }

        [Fact]
        public void GearSaysWhereItIsRepairedOrThatItCannotBe()
        {
            // InventoryGui.CanRepair: at the station its recipe names (or its repair station), from that level.
            Assert.Equal("Forge level 2", ItemWords.Repair(true, "Forge", 2));
            Assert.Equal("Workbench", ItemWords.Repair(true, "Workbench", 1));
            Assert.Equal("cannot be repaired", ItemWords.Repair(false, "Forge", 1));
            Assert.Equal("cannot be repaired: no recipe names a station for it", ItemWords.Repair(true, null, 1));
            Assert.Equal("cannot be repaired: no recipe names a station for it", ItemWords.Repair(true, "", 1));
        }

        [Fact]
        public void TheUpgradeKitsSayTheStationThatTakesItPastItsTopQuality()
        {
            Assert.Equal("Past its top quality, at Black forge", ItemWords.PastTop("Black forge"));
            Assert.Equal("Past its top quality, at an upgrade station", ItemWords.PastTop(null));
        }
    }
}
