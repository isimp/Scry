using Xunit;

namespace Scry.Tests
{
    public class PageLayingWordsTests
    {
        // A page laid out over a few frames says what it waits for, in plain words.

        [Fact]
        public void ThePartLaidNextIsBeingLaidAndThoseAfterItWait()
        {
            Assert.Equal("Laying it out", PanelWords.Laying(next: true));
            Assert.Equal("Waiting", PanelWords.Laying(next: false));
        }

        [Fact]
        public void TheStageNamesTheGroundWhoseGrassItLays()
        {
            Assert.Equal("Laying the grass of the Black Forest", PanelWords.LayingGrass("BlackForest"));
            Assert.Equal("Laying the grass of the Meadows", PanelWords.LayingGrass("Meadows"));
        }
    }
}
