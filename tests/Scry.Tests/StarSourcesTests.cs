using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class StarSourcesTests
    {
        // A creature has stars only where something in the world can give it a level above one, as
        // the game levels creatures: a world spawn or a raid from its least level up to its most
        // (SpawnSystem.Spawn), a spawner, nest or trigger only where its most is over one
        // (CreatureSpawner, SpawnArea, TriggerSpawner), a summon to the level its skill sets
        // (SpawnAbility), and a young one, an egg or one grown up at its parent's level
        // (Procreation, EggGrow, Growup). Its stars are the most of them less one.

        [Fact]
        public void AWorldSpawnReachesItsMostOrItsLeast()
        {
            var stars = new StarSources();
            stars.Add("Deer", LevelWay.World, 1, 3);
            stars.Add("Lox", LevelWay.World, 1, 1);
            stars.Add("Odd", LevelWay.World, 3, 1);
            Assert.Equal(3, stars.MostLevel("Deer"));
            Assert.Equal(1, stars.MostLevel("Lox"));
            // The game starts it at its least whatever its most.
            Assert.Equal(3, stars.MostLevel("Odd"));
        }

        [Fact]
        public void ASpawnerLevelsOnlyWhereItsMostIsOverOne()
        {
            var stars = new StarSources();
            stars.Add("Troll", LevelWay.Spawner, 1, 3);
            stars.Add("Fenring", LevelWay.Spawner, 3, 1);
            stars.Add("Raised", LevelWay.Spawner, 3, 2);
            Assert.Equal(3, stars.MostLevel("Troll"));
            Assert.Equal(1, stars.MostLevel("Fenring"));
            Assert.Equal(3, stars.MostLevel("Raised"));
        }

        [Fact]
        public void TheMostOfAllItsSourcesCounts()
        {
            var stars = new StarSources();
            stars.Add("Wolf", LevelWay.World, 1, 1);
            stars.Add("Wolf", LevelWay.World, 1, 3);
            stars.Add("Skeleton_Friendly", LevelWay.Set, 1, 1);
            Assert.Equal(3, stars.MostLevel("Wolf"));
            Assert.Equal(1, stars.MostLevel("Skeleton_Friendly"));
            // Whichever comes first.
            stars.Add("Troll", LevelWay.World, 1, 3);
            stars.Add("Troll", LevelWay.Spawner, 1, 1);
            Assert.Equal(3, stars.MostLevel("Troll"));
            Assert.Null(stars.MostLevel("Draugr_sleeping"));
        }

        [Fact]
        public void TheYoungTakeTheirParentsLevelThroughEggAndGrowingUp()
        {
            var stars = new StarSources();
            stars.Add("Boar", LevelWay.World, 1, 3);
            stars.Passes("Boar", "Boar_piggy");
            stars.Passes("Boar_piggy", "Boar");
            stars.Add("Hen", LevelWay.Spawner, 1, 1);
            stars.Passes("Hen", "ChickenEgg");
            stars.Passes("ChickenEgg", "Chicken");
            stars.Passes("Chicken", "Hen");
            Assert.Equal(3, stars.MostLevel("Boar_piggy"));
            Assert.Equal(1, stars.MostLevel("Chicken"));
            Assert.Equal(1, stars.MostLevel("ChickenEgg"));
            // A young of a parent with no source has none either.
            stars.Passes("Nobody", "Orphan");
            Assert.Null(stars.MostLevel("Orphan"));
            // A young one with a lower source of its own still comes at its parent's level.
            stars.Add("Wolf", LevelWay.World, 1, 3);
            stars.Add("Wolf_cub", LevelWay.Spawner, 1, 1);
            stars.Passes("Wolf", "Wolf_cub");
            Assert.Equal(3, stars.MostLevel("Wolf_cub"));
            // A parent's young at least as high as the breeding sets.
            stars.Passes("Hen", "Golden", least: 2);
            Assert.Equal(2, stars.MostLevel("Golden"));
        }

        [Fact]
        public void WhereTheWorldLetsNoLevelRiseOnlyTheLeastCounts()
        {
            var stars = new StarSources { LevelsRise = false };
            stars.Add("Deer", LevelWay.World, 1, 3);
            stars.Add("Odd", LevelWay.World, 3, 1);
            stars.Add("Troll", LevelWay.Spawner, 2, 3);
            stars.Add("Skeleton_Friendly", LevelWay.Set, 2, 2);
            Assert.Equal(1, stars.MostLevel("Deer"));
            Assert.Equal(3, stars.MostLevel("Odd"));
            Assert.Equal(2, stars.MostLevel("Troll"));
            Assert.Equal(2, stars.MostLevel("Skeleton_Friendly"));
        }

        [Fact]
        public void APlaceOverridesItsSpawnersLevelsButForTheGroupsItLeavesOut()
        {
            // Hildir's caves set 2 to 3 for their spawn points, the bosses' groups left out (Location.m_excludeEnemyLevelOverrideGroups).
            var cave = new PlaceLevels { Min = 2, Max = 3, Excluded = new HashSet<int> { 7 } };
            Assert.Equal((2, 3), cave.Over(1, 1, group: 0));
            Assert.Equal((1, 1), cave.Over(1, 1, group: 7));
            Assert.Equal((1, 2), new PlaceLevels { Max = 2 }.Over(1, 1, group: 0));
            Assert.Equal((1, 3), new PlaceLevels().Over(1, 3, group: 0));
            Assert.Equal((1, 1), PlaceLevels.None.Over(1, 1, group: 0));
        }

        [Fact]
        public void APlacesSpawnPointsCountByEachPlaceTheyStandIn()
        {
            // A room of Hildir's cave: its cultists' points at 1, raised to 2 to 3 by the cave, the boss's group left out.
            var stars = new StarSources();
            var spawns = new[] { new PlaceSpawnLevel { Creature = "Fenring_Cultist_Hildir", Group = 0, Min = 1, Max = 1 }, new PlaceSpawnLevel { Creature = "Boss", Group = 7, Min = 1, Max = 1 } };
            stars.Place(spawns, new[] { new PlaceLevels { Min = 2, Max = 3, Excluded = new HashSet<int> { 7 } } });
            Assert.Equal(3, stars.MostLevel("Fenring_Cultist_Hildir"));
            Assert.Equal(1, stars.MostLevel("Boss"));
            // A room in no place that sets levels keeps its points' own.
            var plain = new StarSources();
            plain.Place(new[] { new PlaceSpawnLevel { Creature = "Ulv", Min = 1, Max = 1 } }, new PlaceLevels[0]);
            Assert.Equal(1, plain.MostLevel("Ulv"));
            // A room in two places counts by the higher.
            var both = new StarSources();
            both.Place(new[] { new PlaceSpawnLevel { Creature = "Skeleton", Min = 1, Max = 1 } }, new[] { PlaceLevels.None, new PlaceLevels { Max = 2 } });
            Assert.Equal(2, both.MostLevel("Skeleton"));
        }

        [Fact]
        public void ACopyKeepsEverySourceAndTakesMoreApart()
        {
            var stars = new StarSources();
            stars.Add("Boar", LevelWay.World, 1, 3);
            stars.Passes("Boar", "Boar_piggy");
            var copy = stars.Copy();
            copy.Add("Ulv", LevelWay.Spawner, 1, 3);
            Assert.Equal(3, copy.MostLevel("Boar_piggy"));
            Assert.Equal(3, copy.MostLevel("Ulv"));
            Assert.Null(stars.MostLevel("Ulv"));
        }

        [Fact]
        public void APagesStarsFollowItsSources()
        {
            // Stars are the most level less one; none where nothing can level it, the game's own creature never, a mod's unsure.
            Assert.Equal((2, false), StarSources.Shown(3, mod: false));
            Assert.Equal((0, false), StarSources.Shown(1, mod: false));
            Assert.Equal((0, false), StarSources.Shown(null, mod: false));
            Assert.Equal((2, true), StarSources.Shown(null, mod: true));
            Assert.Equal((4, false), StarSources.Shown(5, mod: true));
        }
    }
}
