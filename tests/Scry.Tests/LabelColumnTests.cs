using Xunit;

namespace Scry.Tests
{
    public class LabelColumnTests
    {
        // A card's labels stand in a column beside what they name. Fixed at a width, the column
        // left the settings card's longer choices no room to stand on one line; as wide as its
        // widest label, the choices keep the rest of the row.

        [Fact]
        public void TheColumnIsAsWideAsTheWidestLabelAndAGap()
        {
            Assert.Equal(154f, LabelColumn.Width(new[] { 60f, 140f, 90f }, 14f, 412f));
        }

        [Fact]
        public void ALongLabelTakesNoMoreThanItsShareOfTheCard()
        {
            Assert.Equal(412f * LabelColumn.MostShare, LabelColumn.Width(new[] { 300f }, 14f, 412f), 3);
        }

        [Fact]
        public void NoLabelsLeaveOnlyTheGap()
        {
            Assert.Equal(14f, LabelColumn.Width(new float[0], 14f, 412f));
        }
    }
}
