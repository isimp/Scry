using Xunit;

namespace Scry.Tests
{
    public class TreeElbowTests
    {
        // The tree's ways run square as the plan's walkways do: down from the higher node to half way
        // to the lower node's row, across, and down into it; between two nodes of one row, down,
        // across and back up, or above them on the last row.

        private static void Same((float X, float Y)[] expected, (float X, float Y)[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i].X, actual[i].X, 3);
                Assert.Equal(expected[i].Y, actual[i].Y, 3);
            }
        }

        [Fact]
        public void AChildToTheSideGoesDownAcrossHalfWayAndDownIntoIt()
        {
            var points = TreeElbow.Points(10f, 0f, 40f, 30f, half: 6f, rowGap: 30f, below: true);
            Same(new[] { (10f, 6f), (10f, 15f), (40f, 15f), (40f, 24f) }, points);
        }

        [Fact]
        public void AChildStraightUnderGoesStraightDown()
        {
            var points = TreeElbow.Points(10f, 0f, 10f, 30f, half: 6f, rowGap: 30f, below: true);
            Same(new[] { (10f, 6f), (10f, 24f) }, points);
        }

        [Fact]
        public void TheOrderTheTwoAreGivenInMakesNoDifference()
        {
            Same(TreeElbow.Points(10f, 0f, 40f, 30f, 6f, 30f, true), TreeElbow.Points(40f, 30f, 10f, 0f, 6f, 30f, true));
        }

        [Fact]
        public void TwoNodesOfOneRowAreJoinedUnderIt()
        {
            var points = TreeElbow.Points(10f, 30f, 50f, 30f, half: 6f, rowGap: 30f, below: true);
            Same(new[] { (10f, 36f), (10f, 45f), (50f, 45f), (50f, 36f) }, points);
        }

        [Fact]
        public void OnTheLastRowTheyAreJoinedOverIt()
        {
            // Nothing stands under the last row, and the box ends there.
            var points = TreeElbow.Points(10f, 60f, 50f, 60f, half: 6f, rowGap: 30f, below: false);
            Same(new[] { (10f, 54f), (10f, 45f), (50f, 45f), (50f, 54f) }, points);
        }

        [Fact]
        public void ANodeSeveralRowsLowerIsReachedHalfWayOverItsOwnRow()
        {
            // A way closing a loop down the tree turns over the lower node's row, clear of the rows between.
            var points = TreeElbow.Points(10f, 0f, 40f, 90f, half: 6f, rowGap: 30f, below: true);
            Same(new[] { (10f, 6f), (10f, 75f), (40f, 75f), (40f, 84f) }, points);
        }
    }
}
