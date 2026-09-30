using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// A raid in words: how the game rolls for raids (<c>RandEventSystem.UpdateRandomEvent</c>),
    /// for whom one comes (<c>RandEventSystem.GetValidEventPoints</c>, <c>CheckBase</c>), how long
    /// it lasts, and how its creatures come (<c>SpawnSystem.UpdateSpawnList</c>).
    /// </summary>
    public static class RaidWords
    {
        /// <summary>
        /// The roll every raid waits for: every so many minutes a chance, and on a hit one raid
        /// picked at random among those that can start (<c>RandEventSystem.StartRandomEvent</c>).
        /// </summary>
        public static string Roll(float intervalMinutes, float chance)
        {
            return $"every {Naming.Duration(intervalMinutes * 60f)}, {Naming.Number(chance)}% each time, one picked among the raids that can start";
        }

        /// <summary>A raid with a timer of its own (<c>m_standaloneInterval</c>), rolled besides; null for one without.</summary>
        public static string OwnRoll(float intervalSeconds, float chance)
        {
            if (intervalSeconds <= 0f) return null;
            var words = "on its own every " + Naming.Duration(intervalSeconds);
            return chance < 100f ? $"{words}, {Naming.Number(chance)}% each time" : words;
        }

        /// <summary>
        /// Whom a raid comes for: a player standing in one of its biomes and, for a raid that needs
        /// a base, with a base value of 3 or more, which counts the base pieces within 20 m
        /// (<c>EffectArea.GetBaseValue</c>).
        /// </summary>
        public static string ComesFor(string biomes, bool nearBaseOnly)
        {
            const string based = "with 3 or more base pieces within 20 m";
            if (string.IsNullOrEmpty(biomes)) return nearBaseOnly ? "someone " + based : "anyone, anywhere";
            return $"someone in {biomes}" + (nearBaseOnly ? " " + based : ", base or not");
        }

        /// <summary>
        /// When a raid is on the table: once every key it needs is set, until any key that ends it
        /// is (<c>RandEventSystem.HaveGlobalKeys</c>); from the start when it needs none.
        /// </summary>
        public static string Starts(IEnumerable<string> keys, IEnumerable<string> endingKeys, System.Func<string, string> bossOf)
        {
            var parts = new List<string>();
            if (keys != null) foreach (var key in keys) if (!string.IsNullOrEmpty(key)) parts.Add(SpawnWords.Once(key, bossOf));
            if (parts.Count == 0) parts.Add("from the start");
            if (endingKeys != null) foreach (var key in endingKeys) if (!string.IsNullOrEmpty(key)) parts.Add(SpawnWords.Until(key, bossOf));
            return string.Join(", ", parts);
        }

        /// <summary>How long it lasts, and whether its clock stops while no one is inside its range.</summary>
        public static string Lasts(float duration, bool pauses, float range)
        {
            var words = Naming.Duration(duration);
            return pauses ? $"{words}, paused while nobody is within {Naming.Number(range)} m" : words;
        }

        /// <summary>
        /// How one of its creatures comes: up to so many near at once, a roll every so often at a
        /// chance, and its stars. Without a cap the game spawns one each time and counts none.
        /// </summary>
        public static string Spawn(int maxSpawned, float interval, float chance, string stars)
        {
            var parts = new List<string>();
            var pace = Naming.Duration(interval);
            if (maxSpawned > 0)
            {
                parts.Add($"up to {maxSpawned} at once");
                parts.Add(chance < 100f ? $"every {pace} at {Naming.Number(chance)}%" : $"every {pace}");
            }
            else
            {
                parts.Add(chance < 100f ? $"one every {pace} at {Naming.Number(chance)}%" : $"one every {pace}");
            }
            if (!string.IsNullOrEmpty(stars)) parts.Add(stars);
            return string.Join(", ", parts);
        }
    }
}
