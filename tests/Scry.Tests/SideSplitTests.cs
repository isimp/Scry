using Xunit;

namespace Scry.Tests
{
    public class SideSplitTests
    {
        // The stage at a page's top is as tall as it is dragged, within its bounds, and never so
        // tall that the page's header under it is pushed out of the panel.

        [Fact]
        public void TheStageIsAsTallAsDraggedWhereThePageHasRoom()
        {
            Assert.Equal(300f, SideSplit.StageHeight(wanted: 300f, side: 1000f, least: 120f, keep: 200f));
        }

        [Fact]
        public void TheStageNeverTakesMoreThanItsShareOfThePage()
        {
            Assert.Equal(850f, SideSplit.StageHeight(wanted: 900f, side: 1000f, least: 120f, keep: 100f));
        }

        [Fact]
        public void DraggedTallTheStageLeavesThePagesHeaderItsRoom()
        {
            // A header of 250 under a stage dragged to 900 of a page 1,000 tall: the stage gives way.
            Assert.Equal(750f, SideSplit.StageHeight(wanted: 900f, side: 1000f, least: 120f, keep: 250f));
        }

        [Fact]
        public void TheStageNeverShrinksBelowItsLeastHeight()
        {
            Assert.Equal(120f, SideSplit.StageHeight(wanted: 50f, side: 1000f, least: 120f, keep: 200f));
            // On a page too short for both, the stage keeps its least and the header is cut at the page's end.
            Assert.Equal(120f, SideSplit.StageHeight(wanted: 300f, side: 250f, least: 120f, keep: 200f));
        }
    }
}
