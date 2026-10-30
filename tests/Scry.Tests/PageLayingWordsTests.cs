using Xunit;

namespace Scry.Tests
{
    public class PageLayingWordsTests
    {
        // The stage says while it lays its ground's vegetation, in plain words: what the game
        // scatters there is grass in the Meadows but frosted tufts in the Deep North.

        [Fact]
        public void TheStageNamesTheGroundWhoseVegetationItLays()
        {
            Assert.Equal("Laying the vegetation of the Black Forest", PanelWords.LayingGrass("BlackForest"));
            Assert.Equal("Laying the vegetation of the Deep North", PanelWords.LayingGrass("DeepNorth"));
        }
    }
}
