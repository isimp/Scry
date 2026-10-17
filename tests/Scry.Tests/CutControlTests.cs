using Xunit;

namespace Scry.Tests
{
    public class CutControlTests
    {
        // One control cuts what the stage shows: Closed with the roof on, Top cut from above, and
        // inside a dungeon Side cut from the side. In a room gone into it acts on the room.

        [Fact]
        public void TheControlLightsHowTheStageIsCutNow()
        {
            Assert.Equal(CutMode.Side, CutControl.Shown(side: true, cutting: true));
            Assert.Equal(CutMode.Side, CutControl.Shown(side: true, cutting: false));
            Assert.Equal(CutMode.Top, CutControl.Shown(side: false, cutting: true));
            Assert.Equal(CutMode.Closed, CutControl.Shown(side: false, cutting: false));
        }

        [Fact]
        public void ADungeonsInsideIsOfferedAllThreeAPlaceClosedAndTop()
        {
            Assert.Equal(new[] { CutMode.Closed, CutMode.Top, CutMode.Side }, CutControl.Offered(inside: true));
            Assert.Equal(new[] { CutMode.Closed, CutMode.Top }, CutControl.Offered(inside: false));
        }
    }
}
