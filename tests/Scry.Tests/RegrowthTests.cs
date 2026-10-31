using Xunit;

namespace Scry.Tests
{
    public class RegrowthTests
    {
        // A bush, a mushroom or a flower grows back while the player is away, as its ground loads,
        // so the usual way to meet its regrowth is coming back to it bearing again. The game keeps
        // when it was last picked after it bears again, so one seen bearing that was picked before
        // has been seen grown back, whoever picked it.

        [Fact]
        public void OneSeenBearingAgainAfterAPickHasGrownBack()
        {
            Assert.True(Regrowth.SeenBearingAgain(picked: false, pickedTicks: 638_000_000_000_000_000L));
        }

        [Fact]
        public void OneNeverPickedOrPickedNowHasNotGrownBack()
        {
            Assert.False(Regrowth.SeenBearingAgain(picked: false, pickedTicks: 0L));
            Assert.False(Regrowth.SeenBearingAgain(picked: false, pickedTicks: 1L));
            Assert.False(Regrowth.SeenBearingAgain(picked: true, pickedTicks: 638_000_000_000_000_000L));
        }
    }
}
