using Xunit;

namespace Scry.Tests
{
    public class StepPressTests
    {
        // The mouse's back and forward buttons reach the panel twice for one press: as a key in the
        // game's update and as an event in the panel's drawing, at times a frame apart. One press
        // steps once; a second press, however quick a person makes it, steps again.

        [Fact]
        public void OnePressArrivingTwiceAFrameApartStepsOnce()
        {
            var press = new StepPress();
            Assert.True(press.Take(back: true, at: 10.000f));
            Assert.False(press.Take(back: true, at: 10.017f));
            Assert.False(press.Take(back: true, at: 10.040f));
        }

        [Fact]
        public void ASecondPressStepsAgainAndTheOtherWayAtOnce()
        {
            var press = new StepPress();
            Assert.True(press.Take(back: true, at: 10f));
            Assert.True(press.Take(back: true, at: 10f + StepPress.SamePress + 0.05f));
            // Forward straight after back is another press.
            Assert.True(press.Take(back: false, at: 10f + StepPress.SamePress + 0.06f));
        }
    }
}
