using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// What a place's example leaves to chance is rolled by chance in play, and the same way each
    /// time while the self-test runs, so its pictures compare run to run.
    /// </summary>
    public class StageDiceTests
    {
        [Fact]
        public void FixedAPlaceRollsTheSameByItsNameAndAnotherPlaceOtherwise()
        {
            StageDice.Fix(true);
            try
            {
                Assert.True(StageDice.Fixed);
                var first = StageDice.RollsFor("Crypt2");
                var again = StageDice.RollsFor("Crypt2");
                Assert.Equal((first.NextDouble(), first.NextDouble(), first.NextDouble()), (again.NextDouble(), again.NextDouble(), again.NextDouble()));
                Assert.Equal(StageDice.SeedFor("Crypt2"), StageDice.SeedFor("Crypt2"));
                Assert.NotEqual(StageDice.SeedFor("Crypt2"), StageDice.SeedFor("WoodVillage1"));
                var other = StageDice.RollsFor("WoodVillage1");
                var crypt = StageDice.RollsFor("Crypt2");
                Assert.NotEqual((other.NextDouble(), other.NextDouble(), other.NextDouble()), (crypt.NextDouble(), crypt.NextDouble(), crypt.NextDouble()));
            }
            finally
            {
                StageDice.Fix(false);
            }
        }

        [Fact]
        public void LeftToChanceEveryRollDrawsFromOneSourceAndSeedsDiffer()
        {
            StageDice.Fix(false);
            Assert.False(StageDice.Fixed);
            Assert.Same(StageDice.RollsFor("Crypt2"), StageDice.RollsFor("WoodVillage1"));
            var seeds = new[] { StageDice.SeedFor("Crypt2"), StageDice.SeedFor("Crypt2"), StageDice.SeedFor("Crypt2"), StageDice.SeedFor("Crypt2") };
            Assert.True(seeds[0] != seeds[1] || seeds[1] != seeds[2] || seeds[2] != seeds[3]);
            // A name is no part of a roll by chance: none, or the empty name, draws as any other.
            Assert.Same(StageDice.RollsFor(null), StageDice.RollsFor(""));
        }

        [Fact]
        public void AFixedSeedIsTheSameAcrossRunsNotTheRuntimesOwnHash()
        {
            // The seed comes from the name's characters, so it is the same in every process and run.
            Assert.Equal(StageDice.StableHash("Crypt2"), StageDice.StableHash("Crypt2"));
            Assert.NotEqual(StageDice.StableHash("Crypt2"), StageDice.StableHash("Crypt3"));
            Assert.Equal(StageDice.StableHash(""), StageDice.StableHash(null));
            Assert.True(StageDice.StableHash("WoodVillage1") >= 0);
        }
    }
}
