using Xunit;

namespace Scry.Tests
{
    public class MotionSwitchesTests
    {
        // The game tells a creature's animator every frame how it moves: flying while it flies,
        // in water while it swims, neither while it walks. A Drake or Moder left untold drops from
        // its flight to its landed pose, which a Drake never shows in the game; a Serpent left
        // untold lies stranded. The stage tells each what the game tells it.

        [Fact]
        public void ADrakeOnTheStageIsToldItFlies()
        {
            Assert.Equal((false, true), MotionSwitches.Told(flies: true, SeaHeld.Not, seaShown: false, livesInWater: false));
        }

        [Fact]
        public void AFlyerOverTheSeaFliesRatherThanSwims()
        {
            // Moder can swim, yet the game tells a flying creature it is out of the water.
            Assert.Equal((false, true), MotionSwitches.Told(flies: true, SeaHeld.Swimming, seaShown: true, livesInWater: false));
        }

        [Fact]
        public void ASwimmerInTheSeaIsToldItIsInWaterAndNotFlying()
        {
            Assert.Equal((true, false), MotionSwitches.Told(flies: false, SeaHeld.Swimming, seaShown: true, livesInWater: false));
            Assert.Equal((true, false), MotionSwitches.Told(flies: false, SeaHeld.Swimming, seaShown: false, livesInWater: true));
        }

        [Fact]
        public void AWalkerIsToldNeither()
        {
            Assert.Equal((false, false), MotionSwitches.Told(flies: false, SeaHeld.Swimming, seaShown: false, livesInWater: false));
            Assert.Equal((false, false), MotionSwitches.Told(flies: false, SeaHeld.Not, seaShown: true, livesInWater: false));
        }
    }
}
