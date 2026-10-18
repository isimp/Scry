using Xunit;

namespace Scry.Tests
{
    public class PlanBoxHeaderTests
    {
        // The plan box's header holds its views from the left and, in its top right corner, grow
        // and fold; the plan's own switches, the rooms below and the names, stand joined in the
        // plan's top right corner.

        [Fact]
        public void TheViewsStandLeftGrowAndFoldInTheBoxsCorner()
        {
            var laid = PlanBoxHeader.Lay(width: 300f, views: 180f, button: 22f, gap: 4f);
            Assert.Equal(1, laid.Rows);
            Assert.Equal(300f - 22f, laid.FoldX);
            Assert.Equal(300f - 22f - 4f - 22f, laid.GrowX);
            Assert.Equal(0, laid.ControlsRow);
        }

        [Fact]
        public void WhereTheViewsLeaveNoRoomGrowAndFoldTakeASecondRowStillInTheCorner()
        {
            var laid = PlanBoxHeader.Lay(width: 220f, views: 180f, button: 22f, gap: 4f);
            Assert.Equal(2, laid.Rows);
            Assert.Equal(1, laid.ControlsRow);
            Assert.Equal(220f - 22f, laid.FoldX);
            // Just room: the views, a gap, grow, a gap and fold.
            Assert.Equal(1, PlanBoxHeader.Lay(180f + 4f + 22f + 4f + 22f, 180f, 22f, 4f).Rows);
            Assert.Equal(2, PlanBoxHeader.Lay(180f + 4f + 22f + 4f + 22f - 0.5f, 180f, 22f, 4f).Rows);
        }

        [Fact]
        public void ThePlansSwitchesStandJoinedInItsTopRightCornerTheNamesOutermost()
        {
            var corner = PlanBoxHeader.Corner(planWidth: 220f, button: 22f, below: true);
            Assert.Equal(220f - 22f, corner.NamesX);
            Assert.Equal(220f - 22f - 22f, corner.BelowX);
            Assert.Equal(220f - 22f - 22f, corner.Left);
        }

        [Fact]
        public void WithoutTheSwitchForTheRoomsBelowTheNamesStandAlone()
        {
            var corner = PlanBoxHeader.Corner(planWidth: 220f, button: 22f, below: false);
            Assert.Equal(220f - 22f, corner.NamesX);
            Assert.True(float.IsNaN(corner.BelowX));
            Assert.Equal(220f - 22f, corner.Left);
        }
    }
}
