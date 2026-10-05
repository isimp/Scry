using Xunit;

namespace Scry.Tests
{
    public class ItemWordsTests
    {
        // Some answers are worth telling even when they are "no": whether an item goes through
        // portals, whether gear can be upgraded, whether it wears out, and where it is repaired.

        [Fact]
        public void AWeaponsDamageIsATableByQualityLevel()
        {
            Assert.Equal("By quality", ItemWords.ByQualityTitle);
            Assert.Equal(new[] { "", "1", "2", "3", "4" }, ItemWords.QualityColumns(4));
            // The base, then each level's added: the game's damage at a quality (ItemData.GetDamage).
            var damage = new[] { ("blunt", 40f, 6f), ("fire", 0f, 0f), ("spirit", 10f, 5f) };
            Assert.Equal(new[] { "Damage", "40 blunt, 10 spirit", "46 blunt, 15 spirit", "52 blunt, 20 spirit" }, ItemWords.DamageByQuality(damage, 3));
            // Nothing added by quality, nothing to tell by it.
            Assert.Null(ItemWords.DamageByQuality(new[] { ("slash", 20f, 0f) }, 3));
            Assert.Null(ItemWords.DamageByQuality(new[] { ("slash", 20f, 5f) }, 1));
        }

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
        public void AFigureSaysHowMuchMoreEachQualityAdds()
        {
            Assert.Equal("1,000, +5 per quality", ItemWords.PerQuality(1000, 5));
            Assert.Equal("12.5, +0.5 per quality", ItemWords.PerQuality(12.5f, 0.5f));
            Assert.Equal("3", ItemWords.PerQuality(3, 0));
        }

        [Fact]
        public void ArmourAndBlockingSayNoneWhereThereIsNone()
        {
            Assert.Equal("24, +2 per quality", ItemWords.Armour(24f, 2f));
            Assert.Equal("none", ItemWords.Armour(0f, 2f));
            // AddBlockTooltip tells blocking only above 1.
            Assert.Equal("10, +5 per quality", ItemWords.Block(10f, 5f));
            Assert.Equal("none", ItemWords.Block(1f, 5f));
        }

        [Fact]
        public void AnItemIsWorthCoins() => Assert.Equal("1,500 coins", ItemWords.Coins(1500));

        [Fact]
        public void FoodSaysWhatItGivesAndHowItHeals()
        {
            Assert.Equal("50 health, 40 stamina, 20 eitr", ItemWords.Food(50f, 40f, 20f));
            Assert.Equal("30 stamina", ItemWords.Food(0f, 30f, 0f));
            Assert.Null(ItemWords.Food(0f, 0f, 0f));
            Assert.Equal("2.5 a tick", ItemWords.Heals(2.5f));
        }

        [Fact]
        public void ASetBonusSaysHowManyPiecesItTakes()
        {
            Assert.Equal("Troll leather set (3 pieces)", ItemWords.SetBonus("Troll leather set", 3));
            Assert.Equal("Troll leather set", ItemWords.SetBonus("Troll leather set", 0));
        }

        [Theory]
        [InlineData("Forge", 2, 1, false, "Made at Forge level 2")]
        [InlineData("Forge", 1, 1, false, "Made at Forge")]
        [InlineData("", 1, 1, false, "Made by hand")]
        [InlineData("Workbench", 1, 1000, true, "Made at Workbench, makes 1,000, from any one of these")]
        public void ARecipeIsTitledByWhereItIsMadeAndWhatItMakes(string station, int level, int makes, bool anyOne, string title) =>
            Assert.Equal(title, ItemWords.RecipeTitle(station, level, makes, anyOne));

        [Fact]
        public void UpgradesSayTheStationLevelsEachQualityNeeds() =>
            Assert.Equal("Forge level 2–4, one more for each quality", ItemWords.UpgradesNeed("Forge", 2, 4));

        [Fact]
        public void GearResistsWhileWornOrWhileBlocking()
        {
            Assert.Equal("Damage it takes while worn", ItemWords.DamageTaken(worn: true));
            Assert.Equal("Damage it takes while blocking", ItemWords.DamageTaken(worn: false));
            Assert.Equal("Resists while worn", ItemWords.ResistsNothing(worn: true));
            Assert.Equal("Resists while blocking", ItemWords.ResistsNothing(worn: false));
        }

        [Fact]
        public void TheUpgradeKitsSayTheStationThatTakesItPastItsTopQuality()
        {
            Assert.Equal("Past its top quality, at Black forge", ItemWords.PastTop("Black forge"));
            Assert.Equal("Past its top quality, at an upgrade station", ItemWords.PastTop(null));
        }
    }
}
