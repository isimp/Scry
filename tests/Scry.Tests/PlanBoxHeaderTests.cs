using Xunit;

namespace Scry.Tests
{
    public class PlanBoxHeaderTests
    {
        // The plan box's header holds its views from the left and, in its top right corner, grow
        // and fold, beside the views: at their width where that leaves room, else drawn tighter;
        // only where even that leaves none do grow and fold take a row of their own. The plan's
        // own switches, the rooms below and the names, stand joined in the plan's top right corner.

        [Fact]
        public void TheViewsStandLeftAtTheirWidthGrowAndFoldInTheBoxsCorner()
        {
            var laid = PlanBoxHeader.Lay(width: 300f, views: 180f, tight: 140f, button: 22f, gap: 4f);
            Assert.Equal(1, laid.Rows);
            Assert.False(laid.Tight);
            Assert.Equal(300f - 22f, laid.FoldX);
            Assert.Equal(300f - 22f - 4f - 22f, laid.GrowX);
            Assert.Equal(0, laid.ControlsRow);
        }

        [Fact]
        public void InTheCompactBoxTheViewsDrawTighterSoGrowAndFoldStillStandBesideThem()
        {
            var laid = PlanBoxHeader.Lay(width: 222f, views: 212f, tight: 146f, button: 22f, gap: 4f);
            Assert.Equal(1, laid.Rows);
            Assert.True(laid.Tight);
            Assert.Equal(0, laid.ControlsRow);
            Assert.Equal(222f - 22f, laid.FoldX);
            Assert.Equal(222f - 22f - 4f - 22f, laid.GrowX);
            // Just room at their width: they keep it.
            Assert.False(PlanBoxHeader.Lay(212f + 4f + 22f + 4f + 22f, 212f, 146f, 22f, 4f).Tight);
            Assert.True(PlanBoxHeader.Lay(212f + 4f + 22f + 4f + 22f - 0.5f, 212f, 146f, 22f, 4f).Tight);
        }

        [Fact]
        public void WhereEvenTighterViewsLeaveNoRoomGrowAndFoldTakeASecondRowStillInTheCorner()
        {
            var laid = PlanBoxHeader.Lay(width: 180f, views: 212f, tight: 146f, button: 22f, gap: 4f);
            Assert.Equal(2, laid.Rows);
            Assert.False(laid.Tight);
            Assert.Equal(1, laid.ControlsRow);
            Assert.Equal(180f - 22f, laid.FoldX);
            // Just room when tighter: one row.
            Assert.Equal(1, PlanBoxHeader.Lay(146f + 4f + 22f + 4f + 22f, 212f, 146f, 22f, 4f).Rows);
            Assert.Equal(2, PlanBoxHeader.Lay(146f + 4f + 22f + 4f + 22f - 0.5f, 212f, 146f, 22f, 4f).Rows);
        }

        [Fact]
        public void ThePlansSwitchesStandJoinedInItsTopRightCornerTheNamesOutermost()
        {
            var corner = PlanBoxHeader.Corner(planWidth: 220f, button: 22f, below: true, names: true);
            Assert.Equal(220f - 22f, corner.NamesX);
            Assert.Equal(220f - 22f - 22f, corner.BelowX);
            Assert.Equal(220f - 22f - 22f, corner.Left);
        }

        [Fact]
        public void WithoutTheSwitchForTheRoomsBelowTheNamesStandAlone()
        {
            var corner = PlanBoxHeader.Corner(planWidth: 220f, button: 22f, below: false, names: true);
            Assert.Equal(220f - 22f, corner.NamesX);
            Assert.True(float.IsNaN(corner.BelowX));
            Assert.Equal(220f - 22f, corner.Left);
        }

        [Fact]
        public void InTheCompactBoxTheNamesSwitchIsLeftOutTheRoomsBelowStandingAloneInTheCorner()
        {
            var corner = PlanBoxHeader.Corner(planWidth: 220f, button: 22f, below: true, names: false);
            Assert.True(float.IsNaN(corner.NamesX));
            Assert.Equal(220f - 22f, corner.BelowX);
            Assert.Equal(220f - 22f, corner.Left);
            // Neither shown: nothing stands in the corner.
            var none = PlanBoxHeader.Corner(220f, 22f, below: false, names: false);
            Assert.True(float.IsNaN(none.NamesX) && float.IsNaN(none.BelowX));
            Assert.Equal(220f, none.Left);
        }
    }
}
