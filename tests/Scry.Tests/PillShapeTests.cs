using Xunit;

namespace Scry.Tests
{
    public class PillShapeTests
    {
        // A pill (a chip, a tab, a loot mark, a kind's badge) has round ends as high as it is,
        // however high it is drawn: a picture's ends sliced at one fixed size broke up into
        // stray lines on a pill drawn lower than they were tall.

        [Fact]
        public void APillsRoundEndsAreAsHighAsThePillAtAnyHeight()
        {
            for (var height = 2; height <= 80; height++)
            {
                // Each end holds a half circle as high as the pill, and both ends fit the picture
                // with some straight left between them to stretch.
                Assert.True(PillShape.End(height) * 2f >= height, $"{height}: an end holds its half circle");
                Assert.True(PillShape.Width(height) - 2 * PillShape.End(height) >= 1, $"{height}: a straight between its ends");
            }
        }

        [Fact]
        public void APillIsDrawnAtTheWholePixelsItIsTall()
        {
            Assert.Equal(20, PillShape.Height(20f));
            Assert.Equal(20, PillShape.Height(19.6f));
            Assert.Equal(21, PillShape.Height(20.6f));
            // Never so low it has no ends at all.
            Assert.Equal(2, PillShape.Height(0.3f));
            Assert.Equal(2, PillShape.Height(-4f));
        }

        [Fact]
        public void APillLongEnoughForBothItsEndsStretchesBetweenThem()
        {
            Assert.True(PillShape.Stretches(60f, 20));
            Assert.True(PillShape.Stretches(PillShape.Width(20), 20));
        }

        [Fact]
        public void APillShorterThanItsTwoEndsIsDrawnWholeSqueezedRatherThanBroken()
        {
            Assert.False(PillShape.Stretches(PillShape.Width(20) - 1, 20));
            Assert.False(PillShape.Stretches(10f, 20));
        }
    }
}
