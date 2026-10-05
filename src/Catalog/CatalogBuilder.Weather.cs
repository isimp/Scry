using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What the world's weather puts on you, as the game's code gives it (<see cref="CodeGivers.OfWeather"/>):
    /// each biome by its weathers, each raid or boss fight by the weather it brings, linked both
    /// ways as any giver is, a status effect's page naming them under Given by.
    /// </summary>
    internal static partial class CatalogBuilder
    {
        /// <summary>Links each biome and raid to what its weather gives, once every entry, biomes too, is made.</summary>
        private static void WeatherGivers(List<Entry> entries)
        {
            var envs = EnvMan.instance.OrNull()?.m_environments;
            if (envs == null) return;
            var book = new LinkBook();
            void Give(Entry giver, string weather)
            {
                var env = envs.Find(e => e != null && e.m_name == weather);
                if (env == null) return;
                foreach (var (effect, how) in CodeGivers.OfWeather(Naming.FieldLabel(weather), env.m_isWet, env.m_isCold, env.m_isColdAtNight, env.m_isFreezing, env.m_isFreezingAtNight))
                {
                    book.Add(giver.Key, Relations.StatusEffects, EntryKeys.For(Kind.StatusEffect, effect), Relations.GivenBy, how);
                }
            }
            foreach (var entry in entries)
            {
                if (entry.Source is BiomeSource biome)
                {
                    foreach (var (weather, weight) in biome.Weathers) if (weight > 0f) Give(entry, weather);
                }
                else if (entry.Source is RandomEvent raid && !string.IsNullOrEmpty(raid.m_forceEnvironment))
                {
                    Give(entry, raid.m_forceEnvironment);
                }
            }
            book.Apply(entries);
        }
    }
}
