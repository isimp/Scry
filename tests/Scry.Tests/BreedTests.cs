using Xunit;

namespace Scry.Tests
{
    public class BreedTests
    {
        // A tame creature breeds as Procreation.Procreate has it: every so often, when it is fed,
        // calm and has a partner near, it may gain a love point; with enough it gets pregnant,
        // and after a while has its young, unless too many of its kind and their young are near.
        // The game skips a check at the chance it calls the pregnancy chance, so a love point
        // comes at the rest of it.

        [Fact]
        public void ACreatureBreedsWhenTameFedCalmAndWithAPartner()
        {
            Assert.Equal("tame, fed and calm, with another of its kind within 3 m", BreedWords.Needs(3f, null, false));
            Assert.Equal("tame, fed and calm, with another of its kind within 3 m", BreedWords.Needs(3f, "", false));
            Assert.Equal("tame, fed and calm, with a Lox within 4 m", BreedWords.Needs(4f, "Lox", false));
            Assert.Equal("tame, fed and calm; a partner within 3 m is not needed", BreedWords.Needs(3f, null, true));
        }

        [Fact]
        public void LovePointsComeAtTheChanceTheGameDoesNotSkip()
        {
            Assert.Equal("a love point every 30 s at a 50% chance; 4 make it pregnant", BreedWords.Love(30f, 0.5f, 4));
            Assert.Equal("a 25% chance every 60 s to get pregnant", BreedWords.Love(60f, 0.75f, 1));
            Assert.Equal("a 25% chance every 60 s to get pregnant", BreedWords.Love(60f, 0.75f, 0));
            Assert.Equal("never: the game skips every check", BreedWords.Love(30f, 1f, 3));
        }

        [Fact]
        public void TooManyNearStopIt()
        {
            Assert.Equal("once 4 of its kind and its young are within 10 m", BreedWords.Crowd(4, 10f));
        }

        [Fact]
        public void YoungHaveTheirParentsStars()
        {
            Assert.Equal("with its parent's stars", BreedWords.Stars(1));
            Assert.Equal("with its parent's stars, at least 1", BreedWords.Stars(2));
            Assert.Equal("with its parent's stars, at least 2", BreedWords.Stars(3));
        }

        [Fact]
        public void AYoungOneGrowsIntoOneOfItsKindsByWeight()
        {
            Assert.Equal(new[] { "75%", "25%" }, BreedWords.Shares(new[] { 3f, 1f }));
            Assert.Equal(new[] { "100%" }, BreedWords.Shares(new[] { 2f }));
            Assert.Equal(new[] { "0%", "0%" }, BreedWords.Shares(new[] { 0f, 0f }));
            Assert.Equal(new[] { "over 99.9%", "under 0.1%" }, BreedWords.Shares(new[] { 9999f, 0.1f }));
        }

        // An egg hatches (EggGrow.CanGrow) only lying on its own, and as it asks, by a fire and
        // under a roof with enough cover.
        [Fact]
        public void AnEggHatchesWhereItIsKeptRight()
        {
            Assert.Equal("lying on its own, by a fire, under a roof with at least 70% cover", BreedWords.Hatches(true, true, 0.7f));
            Assert.Equal("lying on its own, under a roof", BreedWords.Hatches(false, true, 0f));
            Assert.Equal("lying on its own", BreedWords.Hatches(false, false, 0.7f));
        }

        // Ridden, a creature has stamina of its own (Sadle): it regains it, slower when hungry,
        // and running and swimming drain it.
        [Fact]
        public void ARiddenCreaturesStaminaIsTold()
        {
            Assert.Equal("200, regaining 2 a second, 1 when hungry", RideWords.Stamina(200f, 2f, 1f));
            Assert.Equal("200, regaining 2 a second", RideWords.Stamina(200f, 2f, 2f));
            Assert.Equal("running 10 a second, swimming 5 a second", RideWords.Drains(10f, 5f));
            Assert.Equal("running 10 a second", RideWords.Drains(10f, 0f));
            Assert.Null(RideWords.Drains(0f, 0f));
        }
    }
}
