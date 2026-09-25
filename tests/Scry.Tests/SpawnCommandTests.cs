using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class SpawnCommandTests
    {
        private static Entry Registered(string name, Kind kind)
        {
            var entry = E(name, kind);
            entry.Registered = true;
            return entry;
        }

        [Fact]
        public void APlainCreatureIsSpawnedByName()
        {
            Assert.Equal("spawn Troll", SpawnCommand.For(Registered("Troll", Kind.Creature), amount: 1, level: 1, give: false));
        }

        [Fact]
        public void ACreatureWithStarsCarriesItsLevel()
        {
            Assert.Equal("spawn Troll 1 3", SpawnCommand.For(Registered("Troll", Kind.Creature), amount: 1, level: 3, give: false));
        }

        [Fact]
        public void AnItemIsGivenIntoTheInventory()
        {
            Assert.Equal("spawn Wood 20 1 p", SpawnCommand.For(Registered("Wood", Kind.Item), amount: 20, level: 1, give: true));
        }

        [Fact]
        public void AnItemCarriesItsQuality()
        {
            Assert.Equal("spawn SwordIron 1 3 p", SpawnCommand.For(Registered("SwordIron", Kind.Item), amount: 1, level: 3, give: true));
        }

        [Fact]
        public void QualityAndLevelStayWithinWhatTheGameAccepts()
        {
            Assert.Equal("spawn SwordIron 1 4 p", SpawnCommand.For(Registered("SwordIron", Kind.Item), amount: 1, level: 9, give: true));
            Assert.Equal("spawn Troll 1 9", SpawnCommand.For(Registered("Troll", Kind.Creature), amount: 1, level: 20, give: false));
            Assert.Equal("spawn Troll", SpawnCommand.For(Registered("Troll", Kind.Creature), amount: 0, level: 0, give: false));
        }

        [Fact]
        public void OnlyItemsCanBeGiven()
        {
            Assert.Equal("spawn Troll", SpawnCommand.For(Registered("Troll", Kind.Creature), amount: 1, level: 1, give: true));
        }

        [Fact]
        public void WhatTheSceneDoesNotKnowCannotBeSpawned()
        {
            Assert.Null(SpawnCommand.For(E("sfx_troll_idle", Kind.Sound), 1, 1, false));
            Assert.Null(SpawnCommand.For(Registered("Rested", Kind.StatusEffect), 1, 1, false));
        }
    }
}
