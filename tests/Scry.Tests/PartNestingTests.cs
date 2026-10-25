using Xunit;

namespace Scry.Tests
{
    public class PartNestingTests
    {
        // A part's own cost is its whole less what the parts inside it cost, so what allocates
        // in code nobody timed shows under the part around it, not under its inner parts.

        [Fact]
        public void APartsOwnCostIsItsWholeLessItsInnerParts()
        {
            var nesting = new PartNesting();
            var update = nesting.Open();
            var previews = nesting.Open();
            Assert.Equal((1000L, 2.0), nesting.Close(previews, 1000L, 2.0));
            var walks = nesting.Open();
            Assert.Equal((500L, 1.0), nesting.Close(walks, 500L, 1.0));
            // The whole of update was 4,000 bytes and 10 ms; 1,500 bytes and 3 ms were its inner parts'.
            Assert.Equal((2500L, 7.0), nesting.Close(update, 4000L, 10.0));
            Assert.Equal(0, nesting.Depth);
        }

        [Fact]
        public void AnInnerPartLeftOpenIsDroppedByTheOuterClose()
        {
            var nesting = new PartNesting();
            var outer = nesting.Open();
            nesting.Open();
            var (bytes, ms) = nesting.Close(outer, 300L, 3.0);
            Assert.Equal(300L, bytes);
            Assert.Equal(3.0, ms, 3);
            Assert.Equal(0, nesting.Depth);
        }

        [Fact]
        public void AClosedPartNeverOpenedCountsItsWholeAsItsOwn()
        {
            var nesting = new PartNesting();
            Assert.Equal((70L, 0.5), nesting.Close(99, 70L, 0.5));
            var part = nesting.Open();
            nesting.Clear();
            Assert.Equal((70L, 0.5), nesting.Close(part, 70L, 0.5));
        }

        [Fact]
        public void TheInnerPartsOfAPartNeverGoBelowNothing()
        {
            // Measured by the memory in use, an inner part can read more than its outer: the own cost floors at nothing.
            var nesting = new PartNesting();
            var outer = nesting.Open();
            var inner = nesting.Open();
            nesting.Close(inner, 900L, 5.0);
            Assert.Equal((0L, 0.0), nesting.Close(outer, 800L, 4.0));
        }
    }
}
