using Xunit;

namespace Scry.Tests
{
    public class AttemptsTests
    {
        [Fact]
        public void WorkThatFailsOnceIsTriedAgain()
        {
            var attempts = new Attempts(2);
            Assert.True(attempts.Failed("Troll"));
            Assert.False(attempts.GivenUp("Troll"));
        }

        [Fact]
        public void WorkThatFailsTwiceIsGivenUp()
        {
            // A trigger that throws would otherwise be pulled again every time its clips are asked about.
            var attempts = new Attempts(2);
            attempts.Failed("Troll");
            Assert.False(attempts.Failed("Troll"));
            Assert.True(attempts.GivenUp("Troll"));
        }

        [Fact]
        public void GivingUpOnOneLeavesTheOthersToBeTried()
        {
            var attempts = new Attempts(2);
            attempts.Failed("Troll");
            attempts.Failed("Troll");
            Assert.False(attempts.GivenUp("Boar"));
            Assert.True(attempts.Failed("Boar"));
        }

        [Fact]
        public void NothingIsGivenUpBeforeItFails()
        {
            Assert.False(new Attempts(2).GivenUp("Troll"));
        }
    }
}
