using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// A creature with no drop list dying on this game, a starred piglet or cub: no loot tells its
    /// death, so it is told by itself, with its stars and who hit it, once, by its remains where
    /// they go on every game, or at once where nothing else will.
    /// </summary>
    public class DroplessDeathsTests
    {
        private static readonly long[] You = { 7L };

        [Fact]
        public void AStarredPigletDyingHereWithNoRemainsTakingLootIsToldAtOnceWithItsStars()
        {
            var deaths = new DroplessDeaths();
            deaths.Dying("Boar_piggy", 2, You, player: false, runsHere: true, hasDrops: false);
            Assert.Null(deaths.TakenByRemains(remainsTakeLoot: false));
            var told = deaths.Died();
            Assert.NotNull(told);
            Assert.Equal("Boar_piggy", told.Value.Creature);
            Assert.Equal(2, told.Value.Level);
            Assert.Equal(You, told.Value.Doers);
            Assert.Null(deaths.Died());
        }

        [Fact]
        public void RemainsThatGoOnEveryGameTellItSoItIsNotToldTwice()
        {
            var deaths = new DroplessDeaths();
            deaths.Dying("Wolf_cub", 3, You, player: false, runsHere: true, hasDrops: false);
            var taken = deaths.TakenByRemains(remainsTakeLoot: true);
            Assert.NotNull(taken);
            Assert.Equal(3, taken.Value.Level);
            Assert.Null(deaths.Died());
        }

        [Fact]
        public void ACreatureWithADropListIsToldByItsLootNotHere()
        {
            var deaths = new DroplessDeaths();
            deaths.Dying("Boar", 2, You, player: false, runsHere: true, hasDrops: true);
            Assert.Null(deaths.TakenByRemains(remainsTakeLoot: true));
            Assert.Null(deaths.Died());
        }

        [Fact]
        public void APlayerOrADeathAnotherGameRunsIsNotTold()
        {
            var deaths = new DroplessDeaths();
            deaths.Dying("Player", 1, You, player: true, runsHere: true, hasDrops: false);
            Assert.Null(deaths.Died());
            deaths.Dying("Boar_piggy", 2, You, player: false, runsHere: false, hasDrops: false);
            Assert.Null(deaths.TakenByRemains(remainsTakeLoot: true));
            Assert.Null(deaths.Died());
        }

        [Fact]
        public void ADeathLeftUntoldByAFaultIsNotToldForTheNextOne()
        {
            var deaths = new DroplessDeaths();
            deaths.Dying("Boar_piggy", 2, You, player: false, runsHere: true, hasDrops: false);
            deaths.Dying("Boar", 1, You, player: false, runsHere: true, hasDrops: true);
            Assert.Null(deaths.Died());
        }
    }
}
