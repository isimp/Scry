using Xunit;

namespace Scry.Tests
{
    public class ClipMatchTests
    {
        private static readonly string[] Troll = { "idle", "walk", "run", "attack_punch", "hit", "hit_big", "death", "alert", "wakeup" };

        [Theory]
        [InlineData("Death", "death")]
        [InlineData("Alerted", "alert")]
        [InlineData("Wakeup", "wakeup")]
        public void AnEffectOfACreaturePlaysTheAnimationThatGoesWithIt(string effect, string clip)
        {
            Assert.Equal(clip, ClipMatch.For(effect, Troll));
        }

        [Fact]
        public void AHitPlaysNoAnimationAsInTheGame()
        {
            // The game animates no hit; it only staggers a creature when a hit breaks its balance.
            Assert.Null(ClipMatch.For("Hit", new[] { "idle", "hit", "stagger" }));
            Assert.Null(ClipMatch.For("Crit hit", new[] { "idle", "hit" }));
            Assert.Equal("stagger", ClipMatch.For("Stagger", new[] { "idle", "stagger", "death" }));
        }

        [Fact]
        public void TheSimplestOfSeveralMatchingAnimationsIsPlayed()
        {
            Assert.Equal("Death", ClipMatch.For("Death", new[] { "death_left_big", "Death", "death_right" }));
        }

        [Fact]
        public void ADeathPlaysADieOrDeadAnimationWhenThereIsNoDeathOne()
        {
            Assert.Equal("die", ClipMatch.For("Death", new[] { "idle", "die" }));
        }

        [Fact]
        public void AnEffectLabelledWithItsPartStillMatches()
        {
            Assert.Equal("death", ClipMatch.For("Death (humanoid)", Troll));
        }

        [Fact]
        public void AnEffectWithoutAnAnimationPlaysNone()
        {
            Assert.Null(ClipMatch.For("Water", Troll));
            Assert.Null(ClipMatch.For("Death", new[] { "idle", "walk" }));
        }

        [Fact]
        public void OnlyWholeWordsOfTheEffectCount()
        {
            Assert.Null(ClipMatch.For("Deathless", Troll));
        }
    }
}
