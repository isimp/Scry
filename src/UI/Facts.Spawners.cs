using System.Linq;

namespace Scry
{
    /// <summary>A spawner's facts: when it works, how fast, how many it keeps alive, and what it spawns how often.</summary>
    internal sealed partial class Facts
    {
        // ----- Spawners -----

        /// <summary>
        /// A spawner that keeps producing creatures while someone is near (<c>SpawnArea</c>), such
        /// as a greydwarf nest: each creature of its pool with its share and stars, then its pace,
        /// its caps and where it puts them.
        /// </summary>
        private void Spawner(SpawnArea area)
        {
            var pool = area.m_prefabs?.Where(p => p?.m_prefab != null).ToList();
            if (pool == null || pool.Count == 0) return;
            var total = pool.Sum(p => p.m_weight);
            foreach (var data in pool)
            {
                var key = "Spawns " + AnyName(data.m_prefab, data.m_prefab.name);
                if (Pairs.Any(p => p.Key == key)) continue;
                Add(key, SpawnWords.PoolShare(data.m_weight, total, data.m_minLevel, data.m_maxLevel), data.m_prefab.name);
            }
            if (pool.Any(p => p.m_maxLevel > p.m_minLevel)) Add("Star chance", SpawnWords.StarChance(area.m_levelupChance));
            Add("Works", SpawnWords.SpawnerWakes(area.m_triggerDistance));
            Add("Pace", SpawnWords.SpawnerPace(area.m_spawnIntervalSec));
            Add("Keeps alive", SpawnWords.SpawnerCaps(area.m_maxNear, area.m_nearRadius, area.m_maxTotal, area.m_farRadius));
            Add("Puts them", SpawnWords.SpawnerPlaces(area.m_spawnRadius, area.m_onGroundOnly));
        }
    }
}
