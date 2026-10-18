using Xunit;

namespace Scry.Tests
{
    public class WheelNotchesTests
    {
        // Every notch of the wheel that moves a dungeon's cut is one step, a level or a room's mark,
        // however the wheel tells its turns: a mouse's notch as a turn of three or of one, a
        // touchpad's as many small ones.

        [Fact]
        public void AMouseNotchIsOneStepWhicheverWayItIsTold()
        {
            var notches = new WheelNotches();
            Assert.Equal(1, notches.Take(3f));
            Assert.Equal(1, notches.Take(1f));
            Assert.Equal(-1, notches.Take(-3f));
            Assert.Equal(-1, notches.Take(-1f));
        }

        [Fact]
        public void SmallTurnsAddUpToAStep()
        {
            var notches = new WheelNotches();
            Assert.Equal(0, notches.Take(0.4f));
            Assert.Equal(0, notches.Take(0.4f));
            Assert.Equal(1, notches.Take(0.4f));
            // Begun afresh after the step.
            Assert.Equal(0, notches.Take(0.4f));
        }

        [Fact]
        public void ATurnTheOtherWayStartsAfresh()
        {
            var notches = new WheelNotches();
            Assert.Equal(0, notches.Take(0.8f));
            Assert.Equal(0, notches.Take(-0.4f));
            Assert.Equal(0, notches.Take(-0.4f));
            Assert.Equal(-1, notches.Take(-0.4f));
        }

        [Fact]
        public void NoTurnIsNoStep()
        {
            var notches = new WheelNotches();
            Assert.Equal(0, notches.Take(0f));
            Assert.Equal(0, notches.Take(float.NaN));
            Assert.Equal(1, notches.Take(1f));
        }
    }
}
