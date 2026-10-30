using Xunit;

namespace Scry.Tests
{
    public class FlyHeightTests
    {
        // A creature the game starts flying is kept at least its least flying altitude over the
        // ground under it (BaseAI.m_flyAltitudeMin): shown alone, in a raid's crowd or in a place,
        // it flies that high over the stage's ground rather than standing on it.

        [Fact]
        public void AFlyerFliesAsHighOverItsGroundAsTheGameKeepsItAtTheLeast()
        {
            // Moder at least ten metres up, a Drake three.
            Assert.Equal(10f, FlyHeight.Over(flies: true, flyAltitudeMin: 10f), 3);
            Assert.Equal(3f, FlyHeight.Over(flies: true, flyAltitudeMin: 3f), 3);
        }

        [Fact]
        public void AWalkerStandsOnItsGround()
        {
            Assert.Equal(0f, FlyHeight.Over(flies: false, flyAltitudeMin: 3f), 3);
        }

        [Fact]
        public void AFlyerNeverSinksUnderItsGround()
        {
            Assert.Equal(0f, FlyHeight.Over(flies: true, flyAltitudeMin: -2f), 3);
        }
    }
}
