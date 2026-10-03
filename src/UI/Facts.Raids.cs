using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>A raid's facts: for whom and when it comes, how it is rolled, how long it lasts and what it brings.</summary>
    internal sealed partial class Facts
    {
        // ----- Raids -----

        /// <summary>A link that plays music rather than going anywhere: the entry's own (<see cref="Previews.PlacesMusic"/>), or after a colon a piece by its name (<see cref="Previews.NamedMusic"/>).</summary>
        public const string PlayMusic = "play:music";

        /// <summary>A weather by name and what it does to whoever is out in it (<see cref="WeatherWords"/>); the name alone when the world has no such weather.</summary>
        private static string Weather(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var env = EnvMan.instance?.m_environments?.Find(e => e != null && e.m_name == name);
            if (env == null) return Naming.FieldLabel(name);
            return WeatherWords.Line(Naming.FieldLabel(name), new WeatherFacts
            {
                Wet = env.m_isWet,
                Cold = env.m_isCold,
                ColdAtNight = env.m_isColdAtNight,
                Freezing = env.m_isFreezing,
                FreezingAtNight = env.m_isFreezingAtNight,
                AlwaysDark = env.m_alwaysDark,
                WindMin = env.m_windMin,
                WindMax = env.m_windMax,
            });
        }

        private void Raid(RandomEvent raid)
        {
            // A boss's own event: its fight's music and weather, on while its health bar shows.
            var boss = Knowledge.BossOfEvent(raid.m_name);
            if (boss != null)
            {
                var range = EnemyHud.instance != null ? EnemyHud.instance.m_maxShowDistanceBoss : 100f;
                Add("On", RaidWords.WhileFighting(AnyName(boss, boss.name), range), boss.name);
                Add("Music", Naming.FieldLabel(raid.m_forceMusic), PlayMusic);
                Add("Weather", Weather(raid.m_forceEnvironment));
                if (!string.IsNullOrEmpty(raid.m_forceEnvironment)) Hooked(HookedRule.Weather);
                if (!raid.m_random && raid.m_standaloneInterval <= 0f) return;
            }

            Add("Comes for", RaidWords.ComesFor(raid.m_biome != 0 ? Knowledge.BiomeNames(raid.m_biome) : "", raid.m_nearBaseOnly));

            // A world set to pick raids by each player's own progress checks other keys for them.
            var perPlayer = Knowledge.ByEachPlayer(raid);
            // The first boss it waits for, or else stops at, goes to its page.
            var waitsFor = perPlayer ? null : (raid.m_requiredGlobalKeys ?? new List<string>()).Concat(raid.m_notRequiredGlobalKeys ?? new List<string>()).Select(Knowledge.BossPrefabOf).FirstOrDefault(b => b != null);
            Add("On the table", perPlayer ? "for a player whose own progress calls for it" : RaidWords.Starts(raid.m_requiredGlobalKeys, raid.m_notRequiredGlobalKeys, Knowledge.BossOf), waitsFor);

            var system = RandEventSystem.instance;
            if (raid.m_random && system != null) Add("Rolled", RaidWords.Roll(system.m_eventIntervalMin, system.m_eventChance));
            Add("Also rolled", RaidWords.OwnRoll(raid.m_standaloneInterval, raid.m_standaloneChance));
            if (!raid.m_random && raid.m_standaloneInterval <= 0f) Add("Rolled", "never by the raid roll; only something else starts it");
            Add("Lasts", RaidWords.Lasts(raid.m_duration, raid.m_pauseIfNoPlayerInArea, raid.m_eventRange));
            var brought = (raid.m_spawn ?? new List<SpawnSystem.SpawnData>()).Where(d => d != null && d.m_enabled && d.m_prefab != null).ToList();
            Add("Keeps coming", RaidWords.KeepsComing(brought.Count(d => d.m_maxSpawned > 0), brought.Count));
            Add("Ends with", CatalogBuilder.Localize(raid.m_endMessage));
            if (boss == null)
            {
                Add("Music", Naming.FieldLabel(raid.m_forceMusic), PlayMusic);
                Add("Weather", Weather(raid.m_forceEnvironment));
                if (!string.IsNullOrEmpty(raid.m_forceEnvironment)) Hooked(HookedRule.Weather);
            }
            Hooked(HookedRule.Raids);

            foreach (var data in ContentOrder.ToughestFirst((raid.m_spawn ?? new List<SpawnSystem.SpawnData>()).Where(d => d?.m_prefab != null), d => FoeOf(d.m_prefab)))
            {
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
