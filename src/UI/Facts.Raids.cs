using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>A raid's facts: for whom and when it comes, how it is rolled, how long it lasts and what it brings.</summary>
    internal sealed partial class Facts
    {
        // ----- Raids -----

        private void Raid(RandomEvent raid)
        {
            Add("Comes for", RaidWords.ComesFor(raid.m_biome != 0 ? Knowledge.BiomeNames(raid.m_biome) : "", raid.m_nearBaseOnly));

            // A world set to pick raids by each player's own progress checks other keys for them.
            var byPlayer = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.PlayerEvents);
            int Count<T>(List<T> list) => list?.Count ?? 0;
            var perPlayer = byPlayer && (Count(raid.m_altRequiredPlayerKeysAny) > 0 || Count(raid.m_altRequiredPlayerKeysAll) > 0 || Count(raid.m_altRequiredKnownItems) > 0
                                         || Count(raid.m_altRequiredNotKnownItems) > 0 || Count(raid.m_altNotRequiredPlayerKeys) > 0);
            Add("On the table", perPlayer ? "for a player whose own progress calls for it" : RaidWords.Starts(raid.m_requiredGlobalKeys, raid.m_notRequiredGlobalKeys, Knowledge.BossOf));

            var system = RandEventSystem.instance;
            if (raid.m_random && system != null) Add("Rolled", RaidWords.Roll(system.m_eventIntervalMin, system.m_eventChance));
            Add("Also rolled", RaidWords.OwnRoll(raid.m_standaloneInterval, raid.m_standaloneChance));
            if (!raid.m_random && raid.m_standaloneInterval <= 0f) Add("Rolled", "never by the raid roll; only something else starts it");
            Add("Lasts", RaidWords.Lasts(raid.m_duration, raid.m_pauseIfNoPlayerInArea, raid.m_eventRange));
            Add("Ends with", CatalogBuilder.Localize(raid.m_endMessage));
            if (!string.IsNullOrEmpty(raid.m_forceEnvironment)) Add("Weather", Naming.FieldLabel(raid.m_forceEnvironment));

            foreach (var data in raid.m_spawn ?? new List<SpawnSystem.SpawnData>())
            {
                if (data?.m_prefab == null) continue;
                var key = "Brings " + AnyName(data.m_prefab, data.m_prefab.name);
                if (Pairs.Any(p => p.Key == key)) continue;
                var more = new List<string>();
                if (data.m_maxLevel > 1) more.Add(SpawnWords.Stars(data.m_minLevel, data.m_maxLevel));
                var group = SpawnWords.Group(data.m_groupSizeMin, data.m_groupSizeMax);
                if (group != null) more.Add(group);
                if (data.m_spawnAtNight != data.m_spawnAtDay) more.Add(data.m_spawnAtNight ? "at night" : "by day");
                if (data.m_huntPlayer) more.Add("hunting you");
                Add(key, RaidWords.Spawn(data.m_maxSpawned, data.m_spawnInterval, data.m_spawnChance, string.Join(", ", more)), data.m_prefab.name);
            }
        }
    }
}
