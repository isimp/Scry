using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class RaidRollTests
    {
        // A raid's preview rolls one wave as SpawnSystem.UpdateSpawnList does the first time a
        // raid's spawners run with none of its creatures about: each creature tried as often as
        // its cap (once without one), each try at its chance, a group of its size, and each one's
        // stars rolled up from its lowest level (SpawnSystem.Spawn).

        private static Func<float> Rolls(params float[] rolls)
        {
            var queue = new Queue<float>(rolls);
            return () => queue.Dequeue();
        }

        private static Func<int, int, int> Sizes(params int[] sizes)
        {
            var queue = new Queue<int>(sizes);
            return (min, maxExclusive) =>
            {
                var size = queue.Dequeue();
                Assert.InRange(size, min, maxExclusive - 1);
                return size;
            };
        }

        private static RaidSpawn Spawn(string prefab, int max, float chance = 100f, int groupMin = 1, int groupMax = 1, int minLevel = 1, int maxLevel = 1, float levelUp = 10f) =>
            new RaidSpawn { Prefab = prefab, Enabled = true, MaxSpawned = max, Chance = chance, GroupMin = groupMin, GroupMax = groupMax, MinLevel = minLevel, MaxLevel = maxLevel, LevelUpChance = levelUp };

        [Fact]
        public void EachCreatureIsTriedAsOftenAsItsCapEachTryAtItsChance()
        {
            // Three tries: 10 hits, 60 misses, 50 hits (a roll above the chance misses).
            var wave = RaidRoll.FirstRoll(new[] { Spawn("Greydwarf", max: 3, chance: 50f) }, Rolls(10f, 60f, 50f), Sizes(1, 1));

            Assert.Equal(new[] { "Greydwarf", "Greydwarf" }, wave.Select(c => c.Prefab));
            Assert.Equal(new[] { 0, 1 }, wave.Select(c => c.Group));
        }

        [Fact]
        public void ACreatureWithNoCapIsTriedOnce()
        {
            var wave = RaidRoll.FirstRoll(new[] { Spawn("Skeleton", max: 0) }, Rolls(0f), Sizes(1));
            Assert.Single(wave);
        }

        [Fact]
        public void AGroupComesWholeUpToTheCreaturesOwnCap()
        {
            // A group of 4 rolled, cut to the cap of 2; the next try finds the cap reached.
            var wave = RaidRoll.FirstRoll(new[] { Spawn("Wolf", max: 2, groupMin: 3, groupMax: 5) }, Rolls(0f, 0f), Sizes(4));

            Assert.Equal(2, wave.Count);
            Assert.All(wave, c => Assert.Equal(0, c.Group));
        }

        [Fact]
        public void TheWaveSoFarCountsAgainstEachLaterCreaturesCap()
        {
            // UpdateSpawnList checks each cap against every creature it spawned in the pass, while
            // a group is cut only by the cap less those about before it (none as a raid starts):
            // two groups of 3 come under a cap of 4, and a creature with a cap of 2 brings none.
            var spawns = new[] { Spawn("Draugr", max: 4, groupMin: 3, groupMax: 3), Spawn("Draugr_Elite", max: 2) };
            var wave = RaidRoll.FirstRoll(spawns, Rolls(0f, 0f, 0f, 0f), Sizes(3, 3));

            Assert.Equal(6, wave.Count(c => c.Prefab == "Draugr"));
            Assert.Equal(new[] { 0, 0, 0, 1, 1, 1 }, wave.Select(c => c.Group));
            Assert.DoesNotContain(wave, c => c.Prefab == "Draugr_Elite");
        }

        [Fact]
        public void StarsAreRolledUpFromTheLowestLevelAtTheLevelUpChance()
        {
            // Rolls of the level-up chance (10) or under add a level, until one misses or the top is reached.
            var spawn = Spawn("Greydwarf", max: 2, groupMin: 2, groupMax: 2, minLevel: 1, maxLevel: 3, levelUp: 10f);
            var wave = RaidRoll.FirstRoll(new[] { spawn }, Rolls(0f, 5f, 50f, 10f, 3f, 0f), Sizes(2));

            Assert.Equal(new[] { 2, 3 }, wave.Select(c => c.Level));
        }

        [Fact]
        public void ACreatureAtItsTopLevelRollsNoStars()
        {
            var wave = RaidRoll.FirstRoll(new[] { Spawn("Troll", max: 1, minLevel: 2, maxLevel: 2) }, Rolls(0f), Sizes(1));
            Assert.Equal(2, wave.Single().Level);
        }

        [Fact]
        public void ACreatureSwitchedOffOrMissingComesNot()
        {
            var off = Spawn("Boar", max: 1);
            off.Enabled = false;
            var missing = Spawn(null, max: 1);
            Assert.Empty(RaidRoll.FirstRoll(new[] { off, missing }, Rolls(), Sizes()));
        }

        [Fact]
        public void TheLevelUpChanceIsTheCreaturesOwnOrTenScaledByTheWorld()
        {
            // SpawnSystem.GetLevelUpChance: the creature's own chance when set, else 10; with a world
            // level, raised to its power by the world's exponent and capped at 70; else times the
            // world's enemy level-up rate. The biome's own multiplier is left at 1.
            Assert.Equal(10f, RaidRoll.LevelUpChance(-1f, 0, 0f, 1f), 3);
            Assert.Equal(10f, RaidRoll.LevelUpChance(0f, 0, 0f, 1f), 3);
            Assert.Equal(25f, RaidRoll.LevelUpChance(25f, 0, 0f, 1f), 3);
            Assert.Equal(20f, RaidRoll.LevelUpChance(-1f, 0, 0f, 2f), 3);
            Assert.Equal(70f, RaidRoll.LevelUpChance(-1f, 2, 1.3f, 1f), 3);
            Assert.Equal((float)Math.Sqrt(10), RaidRoll.LevelUpChance(-1f, 1, 0.5f, 3f), 3);
            Assert.Equal(20f, RaidRoll.LevelUpChance(-1f, 3, 0f, 2f), 3);
        }

        [Fact]
        public void TheWaveIsToldByEachCreatureHowManyAndHowManyWithStars()
        {
            var wave = new List<RolledCreature>
            {
                new RolledCreature { Prefab = "Greydwarf", Level = 1 },
                new RolledCreature { Prefab = "Greydwarf_Elite", Level = 1 },
                new RolledCreature { Prefab = "Greydwarf", Level = 2 },
                new RolledCreature { Prefab = "Greydwarf", Level = 1 },
            };
            string Name(string prefab) => prefab == "Greydwarf" ? "Greydwarf" : "Greydwarf brute";

            Assert.Equal("The first roll of each, as the game rolls it: Greydwarf × 3 (1 with stars), Greydwarf brute × 1.", RaidWords.FirstRoll(wave, Name));
            Assert.Equal("This roll brought nothing; roll again.", RaidWords.FirstRoll(new List<RolledCreature>(), Name));
        }
    }
}
