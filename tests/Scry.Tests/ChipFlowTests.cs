using Xunit;

namespace Scry.Tests
{
    public class ChipFlowTests
    {
        // The panel's rows of chips (links, clips, effects, buttons, segments) flow one way: left
        // to right, a new row where the next would pass the edge.

        private static ChipFlow Flow(float left = 0f, float right = 100f, float top = 10f) => new ChipFlow(left, right, top, rowHeight: 20f, gap: 5f, rowGap: 4f);

        [Fact]
        public void ChipsGoLeftToRightEachAGapAfterTheLast()
        {
            var flow = Flow();
            Assert.Equal((0f, 10f), flow.Place(30f));
            Assert.Equal((35f, 10f), flow.Place(30f));
            Assert.Equal((70f, 10f), flow.Place(30f));
        }

        [Fact]
        public void AChipThatWouldPassTheEdgeStartsTheNextRow()
        {
            var flow = Flow();
            flow.Place(60f);
            Assert.Equal((0f, 34f), flow.Place(50f));
            Assert.Equal((55f, 34f), flow.Place(10f));
        }

        [Fact]
        public void AChipThatEndsAtTheEdgeStaysInItsRow()
        {
            var flow = Flow();
            flow.Place(60f);
            Assert.Equal((65f, 10f), flow.Place(35f));
        }

        [Fact]
        public void ARowsFirstChipStandsThereHoweverWide()
        {
            // Wider than the row, it takes the row alone rather than wrapping for ever.
            var flow = Flow();
            Assert.Equal((0f, 10f), flow.Place(150f));
            Assert.Equal((0f, 34f), flow.Place(10f));
        }

        [Fact]
        public void ARowAfterALabelWrapsBackToTheLabelsEdge()
        {
            var flow = Flow(left: 40f);
            Assert.Equal((40f, 10f), flow.Place(30f));
            Assert.Equal((75f, 10f), flow.Place(20f));
            Assert.Equal((40f, 34f), flow.Place(30f));
        }

        [Fact]
        public void AfterALabelTheRowsFirstChipStandsThereHoweverWide()
        {
            var flow = Flow(left: 40f);
            Assert.False(flow.InRow);
            Assert.Equal(10f, flow.Below);
            Assert.Equal((40f, 10f), flow.Place(90f));
            Assert.True(flow.InRow);
        }

        [Fact]
        public void ARowBegunAfterWhatStandsAtItsStartWrapsBackToItsLeftEdge()
        {
            // A play button and a label first, then the chips, which wrap to the row's edge.
            var flow = new ChipFlow(0f, 100f, 10f, 20f, 5f, 4f, start: 60f);
            Assert.True(flow.InRow);
            Assert.Equal((60f, 10f), flow.Place(30f));
            Assert.Equal((0f, 34f), flow.Place(30f));
        }

        [Fact]
        public void BelowTheFlowIsItsLastRowsBottomOrItsTopWhenItHoldsNothing()
        {
            var flow = Flow();
            Assert.False(flow.InRow);
            Assert.Equal(10f, flow.Below);
            flow.Place(60f);
            flow.Place(60f);
            Assert.True(flow.InRow);
            Assert.Equal(54f, flow.Below);
            Assert.Equal(54f, flow.RowBottom);
        }

        [Fact]
        public void TheRowsBottomIsThereThoughItHoldsNothing()
        {
            // A section that keeps a row's room whether or not anything stands in it.
            Assert.Equal(30f, Flow().RowBottom);
            Assert.Equal(20f, Flow().RowHeight);
        }
    }
}
