using Xunit;

namespace Scry.Tests
{
    public class RowScrollTests
    {
        // A list showing some of its rows: the On the map list, once more is kept than fits.

        [Fact]
        public void TheWheelMovesTheListDownAndUpAndStopsAtItsEnds()
        {
            Assert.Equal(RowScroll.WheelRows, RowScroll.Wheeled(0, 3f, 20, 8));
            Assert.Equal(0, RowScroll.Wheeled(RowScroll.WheelRows, -3f, 20, 8));
            Assert.Equal(0, RowScroll.Wheeled(0, -3f, 20, 8));
            Assert.Equal(12, RowScroll.Wheeled(12, 3f, 20, 8));
        }

        [Fact]
        public void AListThatFitsDoesNotMove()
        {
            Assert.Equal(0, RowScroll.Wheeled(0, 3f, 5, 8));
            Assert.Equal(0, RowScroll.Wheeled(0, 3f, 8, 8));
        }

        [Fact]
        public void TakingRowsOffNeverLeavesTheListScrolledPastItsEnd()
        {
            Assert.Equal(4, RowScroll.Clamp(12, 12, 8));
            Assert.Equal(0, RowScroll.Clamp(12, 3, 8));
        }

        [Fact]
        public void TheThumbSpansTheShareShownAndRunsTheWholeTrack()
        {
            Assert.Equal((100f, 80f), RowScroll.Thumb(100f, 160f, 0, 20, 10, 10f));
            var (y, height) = RowScroll.Thumb(100f, 160f, 10, 20, 10, 10f);
            Assert.Equal(80f, height);
            Assert.Equal(260f, y + height);
        }

        [Fact]
        public void ALongListKeepsAThumbThatCanBeCaught()
        {
            var (y, height) = RowScroll.Thumb(0f, 100f, 491, 500, 9, 20f);
            Assert.Equal(20f, height);
            Assert.Equal(100f, y + height);
        }

        [Fact]
        public void DraggingTheThumbPicksTheRowItStandsAt()
        {
            Assert.Equal(0, RowScroll.FirstAt(100f, 100f, 160f, 20, 10, 10f));
            Assert.Equal(10, RowScroll.FirstAt(180f, 100f, 160f, 20, 10, 10f));
            Assert.Equal(5, RowScroll.FirstAt(140f, 100f, 160f, 20, 10, 10f));
            // Dragged past either end it stays at that end.
            Assert.Equal(0, RowScroll.FirstAt(-500f, 100f, 160f, 20, 10, 10f));
            Assert.Equal(10, RowScroll.FirstAt(5000f, 100f, 160f, 20, 10, 10f));
        }
    }
}
