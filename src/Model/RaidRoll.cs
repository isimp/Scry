using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>One creature a raid brings (<c>SpawnSystem.SpawnData</c>), as far as rolling it goes.</summary>
    public sealed class RaidSpawn
    {
        public string Prefab;
        public bool Enabled = true;
        public int MaxSpawned;
        public float Chance = 100f;
        public int GroupMin = 1, GroupMax = 1;
        public int MinLevel = 1, MaxLevel = 1;

        /// <summary>How far apart a group stands (<c>m_groupRadius</c>).</summary>
        public float GroupRadius;

        /// <summary>The chance of each further level, as <see cref="RaidRoll.LevelUpChance"/> gives it.</summary>
        public float LevelUpChance = 10f;
    }

    /// <summary>A creature of a rolled rolled: what it is, its level, and the group it came in.</summary>
    public struct RolledCreature
    {
        public string Prefab;
        public int Level;
        public int Group;
    }

    /// <summary>
    /// The first roll of each creature of a raid as the game rolls it: <c>SpawnSystem.UpdateSpawnList</c>
    /// the first time a raid's spawners run with none of its creatures about, and the levels of
    /// <c>SpawnSystem.Spawn</c>. A raid comes in no waves: for as long as it lasts, each creature is
    /// rolled again at its pace (<see cref="RaidWords.KeepsComing"/>).
    /// What depends on the world is left out: the time of day, the weather and world keys a creature
    /// waits for, finding ground for it, and the distance from the world's centre its stars may wait for.
    /// </summary>
    public static class RaidRoll
    {
        /// <param name="percent">A roll from 0 to 100, as <c>UnityEngine.Random.Range(0f, 100f)</c>.</param>
        /// <param name="range">A whole number from the first up to but not including the second, as <c>UnityEngine.Random.Range(int, int)</c>.</param>
        public static List<RolledCreature> FirstRoll(IReadOnlyList<RaidSpawn> spawns, Func<float> percent, Func<int, int, int> range)
        {
            var rolled = new List<RolledCreature>();
            var group = 0;
            foreach (var spawn in spawns)
            {
                if (spawn == null || !spawn.Enabled || string.IsNullOrEmpty(spawn.Prefab)) continue;

                // Tried as often as its cap allows, once without one; the time since its last
                // spawn is long as a raid starts. Each try rolls its chance, then stops at the cap,
                // counted against all the pass has spawned so far.
                var tries = spawn.MaxSpawned == 0 ? 1 : spawn.MaxSpawned;
                for (var i = 0; i < tries; i++)
                {
                    if (percent() > spawn.Chance) continue;
                    if (spawn.MaxSpawned > 0 && rolled.Count >= spawn.MaxSpawned) break;

                    var size = Math.Min(range(spawn.GroupMin, spawn.GroupMax + 1), spawn.MaxSpawned > 0 ? spawn.MaxSpawned : 100);
                    for (var j = 0; j < size; j++) rolled.Add(new RolledCreature { Prefab = spawn.Prefab, Level = Level(spawn, percent), Group = group });
                    group++;
                }
            }
            return rolled;
        }

        /// <summary>Its level: from its lowest, one more for each roll at the level-up chance or under, up to its highest.</summary>
        private static int Level(RaidSpawn spawn, Func<float> percent)
        {
            var level = spawn.MinLevel;
            while (level < spawn.MaxLevel && percent() <= spawn.LevelUpChance) level++;
            return level;
        }

        /// <summary>
        /// The chance of each further level (<c>SpawnSystem.GetLevelUpChance</c>): the creature's own
        /// when set, else 10; with a world level, raised to its power by the world's exponent and
        /// at most 70; else times the world's enemy level-up rate. The biome's multiplier, which
        /// goes by where a creature comes, is left at 1.
        /// </summary>
        public static float LevelUpChance(float own, int worldLevel, float worldExponent, float enemyLevelUpRate)
        {
            var chance = own > 0f ? own : 10f;
            if (worldLevel > 0 && worldExponent > 0f) return Math.Min(70f, (float)Math.Pow(chance, worldLevel * worldExponent));
            return chance * enemyLevelUpRate;
        }
    }
}
