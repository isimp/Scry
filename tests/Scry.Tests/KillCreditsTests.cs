using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// A kill the game credits the character with, and the remains of it seen: told within seconds
    /// of each other, in either order, they are one death the character did, whichever game ran it.
    /// </summary>
    public class KillCreditsTests
    {
        private static WorldEvent Remains(int number) => new WorldEvent(7, number, Happened.Died, "Troll", 5f, 0f, 0f, new[] { ("TrollHide", 2) }, 2, null, null, "7:12");

        [Fact]
        public void ACreditThenTheRemainsWithinSecondsIsTheCharactersKillOnce()
        {
            var credits = new KillCredits();
            credits.Credited(new[] { "Troll", "Troll_Summer" }, 10.0);
            Assert.True(credits.TakeCredit("Troll", 12.0));
            Assert.False(credits.TakeCredit("Troll", 12.5));
        }

        [Fact]
        public void TheRemainsThenTheCreditWithinSecondsNameTheRemainsTheCharactersKill()
        {
            var credits = new KillCredits();
            var told = Remains(1);
            credits.RemainsTold("Troll", told, 10.0);
            var claimed = credits.ClaimRemains(new[] { "Troll" }, 13.0);
            Assert.Equal(new[] { told }, claimed);
            Assert.Empty(credits.ClaimRemains(new[] { "Troll" }, 13.5));
        }

        [Fact]
        public void TooLongApartOrOfAnotherCreatureTheyAreTwoThings()
        {
            var credits = new KillCredits();
            credits.Credited(new[] { "Troll" }, 10.0);
            Assert.False(credits.TakeCredit("Troll", 10.0 + KillCredits.Within + 1.0));
            credits.Credited(new[] { "Troll" }, 20.0);
            Assert.False(credits.TakeCredit("Greydwarf", 21.0));
            credits.RemainsTold("Troll", Remains(2), 30.0);
            Assert.Empty(credits.ClaimRemains(new[] { "Troll" }, 30.0 + KillCredits.Within + 1.0));
            Assert.Empty(credits.ClaimRemains(new[] { "Greydwarf" }, 31.0));
        }

        [Fact]
        public void LeavingTheWorldForgetsBoth()
        {
            var credits = new KillCredits();
            credits.Credited(new[] { "Troll" }, 10.0);
            credits.RemainsTold("Troll", Remains(3), 10.0);
            credits.Forget();
            Assert.False(credits.TakeCredit("Troll", 11.0));
            Assert.Empty(credits.ClaimRemains(new[] { "Troll" }, 11.0));
        }
    }
}
