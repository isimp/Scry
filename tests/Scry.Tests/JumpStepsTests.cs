using Xunit;

namespace Scry.Tests
{
    public class JumpStepsTests
    {
        // The game's physics moves a body in steps of a fixed time, its speed lowered by gravity
        // before each step's move: a jump rises a little less than its speed and gravity alone tell.

        [Fact]
        public void ARealJumpRisesStepByStepLessThanItsSpeedAndGravityTell()
        {
            // A jump at 9.12 m/s under 20 m/s², stepped every 0.02 s: 22 steps up, 1.99 m, not 2.08 m.
            Assert.Equal(1.989, JumpSteps.Apex(9.12f, 20f, 0.02f), 3);
            Assert.True(JumpSteps.Apex(9.12f, 20f, 0.02f) < 9.12f * 9.12f / 40f);
        }

        [Fact]
        public void TheFinerTheStepsTheNearerItRisesToWhatSpeedAndGravityTell()
        {
            var told = 9.12f * 9.12f / 40f;
            Assert.InRange(JumpSteps.Apex(9.12f, 20f, 0.0001f), told * 0.999f, told);
            Assert.True(JumpSteps.Apex(9.12f, 20f, 0.01f) > JumpSteps.Apex(9.12f, 20f, 0.02f));
        }

        [Fact]
        public void NoSpeedUpOrNoStepRisesNothing()
        {
            Assert.Equal(0f, JumpSteps.Apex(0f, 20f, 0.02f));
            Assert.Equal(0f, JumpSteps.Apex(-3f, 20f, 0.02f));
            Assert.Equal(0f, JumpSteps.Apex(9f, 20f, 0f));
            Assert.Equal(0f, JumpSteps.Apex(9f, 0f, 0.02f));
        }
    }
}
