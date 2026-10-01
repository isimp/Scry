using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A raid on the stage: one wave as the game rolls it (<see cref="RaidRoll"/>), its creatures
    /// standing together, each group around a point of its own as far apart as the game spreads
    /// it (<c>m_groupRadius</c>), the groups side by side in a ring. A new copy rolls a new wave.
    /// </summary>
    internal static class RaidCrowd
    {
        /// <summary>The raid last rolled and its wave, for the words under the stage.</summary>
        public static Entry LastFor { get; private set; }
        public static IReadOnlyList<RolledCreature> LastWave { get; private set; } = new List<RolledCreature>();

        /// <summary>Whether a raid brings any creature to roll.</summary>
        public static bool Brings(RandomEvent raid) => raid?.m_spawn != null && raid.m_spawn.Exists(s => s != null && s.m_enabled && s.m_prefab != null);

        /// <summary>A new wave of the raid, made under the parent; null when it has no creature to roll.</summary>
        public static GameObject Make(Entry entry, RandomEvent raid, Transform parent, Vector3 origin, int layer)
        {
            if (!Brings(raid)) return null;
            var spawns = new List<RaidSpawn>();
            var radii = new Dictionary<string, float>();
            foreach (var data in raid.m_spawn)
            {
                if (data?.m_prefab == null) continue;
                spawns.Add(new RaidSpawn
                {
                    Prefab = data.m_prefab.name,
                    Enabled = data.m_enabled,
                    MaxSpawned = data.m_maxSpawned,
                    Chance = data.m_spawnChance,
                    GroupMin = data.m_groupSizeMin,
                    GroupMax = data.m_groupSizeMax,
                    MinLevel = data.m_minLevel,
                    MaxLevel = data.m_maxLevel,
                    GroupRadius = data.m_groupRadius,
                    LevelUpChance = RaidRoll.LevelUpChance(data.m_overrideLevelupChance, Game.m_worldLevel, Game.instance != null ? Game.instance.m_worldLevelEnemyLevelUpExponent : 0f, Game.m_enemyLevelUpRate),
                });
                if (!radii.ContainsKey(data.m_prefab.name)) radii[data.m_prefab.name] = Mathf.Max(0f, data.m_groupRadius);
            }

            var wave = RaidRoll.Wave(spawns, () => Random.Range(0f, 100f), Random.Range);
            LastFor = entry;
            LastWave = wave;

            var holder = new GameObject("Scry raid");
            holder.transform.SetParent(parent, false);
            holder.transform.position = origin;
            if (layer >= 0) holder.layer = layer;

            // Each group around a point of its own, the points in a ring far enough apart that
            // the groups keep to themselves.
            var groups = wave.GroupBy(c => c.Group).ToList();
            var widest = groups.Count == 0 ? 0f : groups.Max(g => g.Count() > 1 ? radii[g.First().Prefab] : 0f);
            var apart = widest * 2f + 4f;
            var ring = groups.Count <= 1 ? 0f : apart / (2f * Mathf.Sin(Mathf.PI / groups.Count));
            var catalog = Session.Explorer?.Catalog;
            var entries = new Dictionary<string, Entry>();
            for (var g = 0; g < groups.Count; g++)
            {
                var angle = 2f * Mathf.PI * g / Mathf.Max(1, groups.Count);
                var centre = origin + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * ring;
                var members = groups[g].ToList();
                var spread = members.Count > 1 ? radii[members[0].Prefab] : 0f;
                foreach (var creature in members)
                {
                    if (!entries.TryGetValue(creature.Prefab, out var creatureEntry))
                    {
                        creatureEntry = catalog?.FirstOrDefault(e => e.Kind == Kind.Creature && e.Name == creature.Prefab);
                        entries[creature.Prefab] = creatureEntry;
                    }
                    if (creatureEntry == null) continue;
                    var modifiers = new Modifiers();
                    modifiers.ResetFor(creatureEntry);
                    modifiers.Level = creature.Level;
                    var offset = Random.insideUnitCircle * spread;
                    var turned = Quaternion.Euler(0f, Random.Range(-30f, 30f), 0f);
                    Looks.Copy(creatureEntry, modifiers, holder.transform, centre + new Vector3(offset.x, 0f, offset.y), turned, layer, null);
                }
            }
            return holder;
        }
    }
}
