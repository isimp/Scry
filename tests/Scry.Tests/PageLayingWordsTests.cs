using Xunit;

namespace Scry.Tests
{
    public class PageLayingWordsTests
    {
        // The stage says while it lays its ground's grass, in plain words.

        [Fact]
        public void TheStageNamesTheGroundWhoseGrassItLays()
        {
            Assert.Equal("Laying the grass of the Black Forest", PanelWords.LayingGrass("BlackForest"));
            Assert.Equal("Laying the grass of the Meadows", PanelWords.LayingGrass("Meadows"));
        }
    }
}
