using Xunit;

namespace Scry.Tests
{
    public class PieceWordsTests
    {
        // What a piece gives and what building it takes, in words: its comfort group, the station
        // it upgrades, what it costs and near what, the tool and tab it is built with, and what an
        // item is used for.

        [Fact]
        public void AComfortGroupSaysOnlyItsBestCountsAndNoGroupSaysASecondAddsNothing()
        {
            Assert.Equal("Chair: only the best of these within 10 m counts", BuildWords.ComfortGroup("Chair"));
            Assert.Equal("none: a second one within 10 m adds nothing", BuildWords.ComfortGroup(null));
        }

        [Fact]
        public void AStationUpgradeSaysHowNearItStandsAndHowFarFromTheOthers()
        {
            Assert.Equal("Workbench, within 5 m of it, 2 m from its other upgrades", BuildWords.Upgrades("Workbench", 5f, 2f));
            Assert.Equal("Workbench, within 5 m of it", BuildWords.Upgrades("Workbench", 5f, 0f));
        }

        [Fact]
        public void TheCostSaysTheStationItIsBuiltNear()
        {
            Assert.Equal("Built near Workbench", BuildWords.Cost("Workbench"));
            Assert.Equal("Build cost", BuildWords.Cost(""));
        }

        [Fact]
        public void APieceAModMadeBuildableSaysWhichModAndNearWhat()
        {
            Assert.Equal("through MoreVanillaBuildPrefabs", BuildWords.Through("MoreVanillaBuildPrefabs"));
            Assert.Equal("Built through MVBP near Workbench", BuildWords.CostThrough("MVBP", "Workbench"));
            Assert.Equal("Built through MVBP with", BuildWords.CostThrough("MVBP", ""));
        }

        [Fact]
        public void ABuildToolSaysTheTabEachPieceIsOn()
        {
            Assert.Equal("Hammer, on its Building tab", BuildWords.OnTab("Hammer", "Building"));
            Assert.Equal("Builds on its Crafting tab (1,234)", BuildWords.BuildsOnTab("Crafting", 1234));
        }

        [Fact]
        public void AnIngredientSaysHowManyMoreEachQualityNeeds()
        {
            Assert.Equal("1,000, +5 per quality", BuildWords.Needs(1000, 5));
            Assert.Equal("3", BuildWords.Needs(3, 0));
        }

        [Theory]
        [InlineData(UseKind.Crafts, "Forge", "Used to make at Forge")]
        [InlineData(UseKind.Crafts, null, "Used to make by hand")]
        [InlineData(UseKind.UpgradesPastTop, null, "Takes these past their top quality, at Black forge")]
        [InlineData(UseKind.Builds, "Workbench", "Used to build near Workbench")]
        [InlineData(UseKind.Builds, null, "Used to build")]
        [InlineData(UseKind.TurnsInto, "Smelter", "Smelter turns it into")]
        [InlineData(UseKind.TurnsInto, null, "Turned into")]
        [InlineData(UseKind.Fuels, "Smelter", "Burnt as fuel by")]
        [InlineData(UseKind.EatenBy, null, "Eaten by")]
        public void EachUseIsTitledByItsKindAndPlace(object kind, string place, string title) =>
            Assert.Equal(title, UseWords.Title((UseKind)kind, place, "Black forge"));

        [Fact]
        public void TheUpgradeStationIsSaidPlainlyWhenNoneIsKnown() =>
            Assert.Equal("Takes these past their top quality, at an upgrade station", UseWords.Title(UseKind.UpgradesPastTop, null, null));

        [Fact]
        public void ARecipeTakingNoneAtFirstNeedsItOnlyForUpgrades()
        {
            Assert.Equal("Iron sword (upgrades)", UseWords.Target("Iron sword", upgradesOnly: true));
            Assert.Equal("Iron sword", UseWords.Target("Iron sword", upgradesOnly: false));
        }
    }
}
