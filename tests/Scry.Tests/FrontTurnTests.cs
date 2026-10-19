using Xunit;

namespace Scry.Tests
{
    public class FrontTurnTests
    {
        // The stage's camera starts in front of a prefab, along its +Z, as a creature walks. A trader
        // the game never turns may face otherwise in its prefab, and stood its back to the camera;
        // a body's front is found from where its left and right legs stand, and it is turned to it.

        [Fact]
        public void ABodyFacingAlongZIsLeftAsItIs()
        {
            // Its right leg towards +X: it faces +Z.
            Assert.Equal(0f, FrontTurn.Degrees(-0.1f, 0f, 0.1f, 0f), 3);
        }

        [Fact]
        public void ABodyFacingAwayIsTurnedRound()
        {
            Assert.Equal(180f, System.Math.Abs(FrontTurn.Degrees(0.1f, 0f, -0.1f, 0f)), 3);
        }

        [Fact]
        public void ABodyFacingSidewaysIsTurnedAQuarter()
        {
            // Facing +X (its right leg towards -Z): a quarter turn back to +Z.
            Assert.Equal(-90f, FrontTurn.Degrees(0f, 0.1f, 0f, -0.1f), 3);
            // Facing -X: a quarter turn the other way.
            Assert.Equal(90f, FrontTurn.Degrees(0f, -0.1f, 0f, 0.1f), 3);
        }

        [Fact]
        public void LegsStandingOnOneSpotTellNoFront()
        {
            Assert.Equal(0f, FrontTurn.Degrees(0.2f, 0.3f, 0.2f, 0.3f));
        }
    }
}
