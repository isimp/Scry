using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A raid on the stage: the first roll of each of its creatures as the game rolls it (<see cref="RaidRoll"/>), its creatures
    /// standing together, each group around a point of its own as far apart as the game spreads
    /// it (<c>m_groupRadius</c>), the groups side by side in a ring. A new copy rolls them anew.
    /// </summary>
    internal static class RaidCrowd
    {
        /// <summary>The raid last rolled and what its roll brought, for the words under the stage.</summary>
        public static Entry LastFor { get; private set; }
        public static IReadOnlyList<RolledCreature> LastRoll { get; private set; } = new List<RolledCreature>();

        /// <summary>Whether a raid brings any creature to roll.</summary>
        public static bool Brings(RandomEvent raid) => raid?.m_spawn != null && raid.m_spawn.Exists(s => s != null && s.m_enabled && s.m_prefab != null);

        /// <summary>A new roll of the raid, made under the parent; null when it has no creature to roll.</summary>
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

            var rolled = RaidRoll.FirstRoll(spawns, () => Random.Range(0f, 100f), Random.Range);
            LastFor = entry;
            LastRoll = rolled;

            var holder = new GameObject("Scry raid");
            holder.transform.SetParent(parent, false);
            holder.transform.position = origin;
            if (layer >= 0) holder.layer = layer;

            // Each group around a point of its own with room for every body, the groups apart
            // (CrowdLayout): the copies cannot push each other aside as live creatures do.
            var entries = new Dictionary<string, Entry>();
            Entry EntryOf(string prefab)
            {
                if (!entries.TryGetValue(prefab, out var found))
                {
                    // Mostly creatures; some raids bring spawners (the Ashlands' charred spawners).
                    found = Session.Explorer?.Find(prefab);
                    entries[prefab] = found;
                }
                return found;
            }
            var groups = rolled.GroupBy(c => c.Group).Select(g => g.ToList()).ToList();
            var shapes = groups.Select(g => new CrowdGroup { Count = g.Count, Body = Body(EntryOf(g[0].Prefab)?.Source as GameObject), Spread = radii[g[0].Prefab] }).ToList();
            var places = CrowdLayout.Place(shapes);
            var at = 0;
            foreach (var members in groups)
            {
                foreach (var creature in members)
                {
                    var place = places[at++];
                    var creatureEntry = EntryOf(creature.Prefab);
                    if (creatureEntry == null) continue;
                    var modifiers = new Modifiers();
                    modifiers.ResetFor(creatureEntry);
                    modifiers.Level = creature.Level;
                    var turned = Quaternion.Euler(0f, Random.Range(-30f, 30f), 0f);
                    Looks.Copy(creatureEntry, modifiers, holder.transform, origin + new Vector3(place.X, 0f, place.Z), turned, layer, null);
                }
            }
            return holder;
        }

        /// <summary>How wide a creature's body is, its capsule's radius as it stands (<c>Character</c>'s collider); a person's for anything else.</summary>
        private static float Body(GameObject prefab)
        {
            var capsule = prefab != null ? prefab.GetComponent<CapsuleCollider>() : null;
            if (capsule == null) return 0.5f;
            var scale = prefab.transform.localScale;
            return Mathf.Max(0.3f, capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        }
    }
}
