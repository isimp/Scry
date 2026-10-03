using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A place's creature spawn points, as <c>CreatureSpawner.Spawn</c> spawns: each switched-on
    /// spawner with a creature, its group kept or blocked and its levels overridden as the
    /// location says (<c>Location.m_blockSpawnGroups</c>, the enemy level overrides), and how high
    /// a creature that flies keeps above its ground. Read off the prefab for whether a place
    /// leaves anything to chance, and off the stage's copy, while it sleeps, for the creatures
    /// standing on the stage.
    /// </summary>
    internal static class PlaceSpawns
    {
        /// <summary>A spawner of a place: where it is, what it spawns, how high a flyer keeps, and its point's figures.</summary>
        internal sealed class Spawn
        {
            public Transform At;
            public GameObject Creature;
            public float Lift;
            public SpawnPoint Point;
        }

        public static List<Spawn> Of(GameObject place, Location rules)
        {
            var found = new List<Spawn>();
            if (place == null) return found;
            var root = place.transform;
            foreach (var spawner in place.GetComponentsInChildren<CreatureSpawner>(true))
            {
                if (spawner == null || spawner.m_creaturePrefab == null || !spawner.enabled || !Animators.SwitchedOn(spawner.transform, root)) continue;
                var group = spawner.m_spawnGroupID;
                if (rules != null && rules.m_blockSpawnGroups != null && rules.m_blockSpawnGroups.Contains(group)) continue;
                int least = spawner.m_minLevel, most = spawner.m_maxLevel;
                var chance = spawner.m_levelupChance;
                if (rules != null && (rules.m_excludeEnemyLevelOverrideGroups == null || !rules.m_excludeEnemyLevelOverrideGroups.Contains(group)))
                {
                    if (rules.m_enemyMinLevelOverride >= 0) least = rules.m_enemyMinLevelOverride;
                    if (rules.m_enemyMaxLevelOverride >= 0) most = rules.m_enemyMaxLevelOverride;
                    if (rules.m_enemyLevelUpOverride >= 0f) chance = rules.m_enemyLevelUpOverride;
                }
                var at = root.InverseTransformPoint(spawner.transform.position);
                found.Add(new Spawn
                {
                    At = spawner.transform,
                    Creature = spawner.m_creaturePrefab,
                    Lift = FlyingHeight(spawner.m_creaturePrefab),
                    Point = new SpawnPoint
                    {
                        At = new Vec3(at.x, at.y, at.z), Group = group, GroupRadius = spawner.m_spawnGroupRadius, MaxInGroup = spawner.m_maxGroupSpawned,
                        Weight = spawner.m_spawnerWeight, MinLevel = least, MaxLevel = most, LevelUpChance = LevelUpChance(chance),
                    },
                });
            }
            return found;
        }

        /// <summary>
        /// How high above the ground a creature that flies keeps (<c>Character.m_flying</c>,
        /// <c>BaseAI.m_flyAltitudeMin</c>): the game drops what it spawns to the ground, and one
        /// that flies takes off and is kept at least this high; 0 for one that walks.
        /// </summary>
        private static float FlyingHeight(GameObject creature)
        {
            var character = creature.GetComponent<Character>();
            if (character == null || !character.m_flying) return 0f;
            var ai = creature.GetComponent<BaseAI>();
            return ai != null ? Mathf.Max(0f, ai.m_flyAltitudeMin) : 0f;
        }

        /// <summary>
        /// A spawn point's chance of each star more, as the world has it (<c>SpawnSystem.GetLevelUpChance</c>):
        /// its own or 10%, by the world's enemy level-up rate or its world level; the share a
        /// world's sector adds is left out, as the stage stands in none.
        /// </summary>
        private static float LevelUpChance(float own)
        {
            var chance = own > 0f ? own : 10f;
            if (Game.m_worldLevel > 0 && Game.instance != null && Game.instance.m_worldLevelEnemyLevelUpExponent > 0f)
            {
                return Mathf.Min(70f, Mathf.Pow(chance, Game.m_worldLevel * Game.instance.m_worldLevelEnemyLevelUpExponent));
            }
            return chance * Game.m_enemyLevelUpRate;
        }
    }
}
