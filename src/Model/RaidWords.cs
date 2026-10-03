using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// A raid in words: how the game rolls for raids (<c>RandEventSystem.UpdateRandomEvent</c>),
    /// for whom one comes (<c>RandEventSystem.GetValidEventPoints</c>, <c>CheckBase</c>), how long
    /// it lasts, and how its creatures come (<c>SpawnSystem.UpdateSpawnList</c>).
    /// </summary>
    internal static class RaidWords
    {
        /// <summary>
        /// The roll every raid waits for: every so many minutes a chance, and on a hit one raid
        /// picked at random among those that can start (<c>RandEventSystem.StartRandomEvent</c>).
        /// </summary>
        public static string Roll(float intervalMinutes, float chance)
        {
            return $"every {Numbers.Duration(intervalMinutes * 60f)}, {Numbers.Amount(chance)}% each time, one picked among the raids that can start";
        }

        /// <summary>A raid with a timer of its own (<c>m_standaloneInterval</c>), rolled besides; null for one without.</summary>
        public static string OwnRoll(float intervalSeconds, float chance)
        {
            if (intervalSeconds <= 0f) return null;
            var words = "on its own every " + Numbers.Duration(intervalSeconds);
            return chance < 100f ? $"{words}, {Numbers.Amount(chance)}% each time" : words;
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
            var words = Numbers.Duration(duration);
            return pauses ? $"{words}, paused while nobody is within {Numbers.Amount(range)} m" : words;
        }

        /// <summary>
        /// How one of its creatures comes: up to so many near at once, a roll every so often at a
        /// chance, and its stars. Without a cap the game spawns one each time and counts none.
        /// </summary>
        public static string Spawn(int maxSpawned, float interval, float chance, string stars)
        {
            var parts = new List<string>();
            var pace = Numbers.Duration(interval);
            if (maxSpawned > 0)
            {
                parts.Add($"up to {Numbers.Count(maxSpawned)} at once");
                parts.Add(chance < 100f ? $"every {pace} at {Numbers.Amount(chance)}%" : $"every {pace}");
            }
            else
            {
                parts.Add(chance < 100f ? $"one every {pace} at {Numbers.Amount(chance)}%" : $"one every {pace}");
            }
            if (!string.IsNullOrEmpty(stars)) parts.Add(stars);
            return string.Join(", ", parts);
        }

        /// <summary>
        /// How a raid's creatures keep coming for as long as it lasts (<c>SpawnSystem.UpdateSpawnList</c>):
        /// each rolled again at its pace, one with a most topped up to it near you, one without one
        /// more each time; null for a raid bringing none. It comes in no waves.
        /// </summary>
        public static string KeepsComing(int capped, int all)
        {
            if (all <= 0) return null;
            const string again = "throughout the raid, each creature is rolled again at its pace";
            if (capped >= all) return again + " and topped up to its most near you, so the fallen are replaced";
            if (capped <= 0) return again + ", one more each time";
            return again + ", those with a most topped up to it near you, so the fallen are replaced";
        }

        /// <summary>The first roll of each of a raid's creatures (<see cref="RaidRoll"/>): each in the order it came, how many, and how many with stars.</summary>
        public static string FirstRoll(IReadOnlyList<RolledCreature> rolled, System.Func<string, string> nameOf)
        {
            if (rolled.Count == 0) return "This roll brought nothing; roll again.";
            var order = new List<string>();
            var counts = new Dictionary<string, (int All, int Starred)>();
            foreach (var creature in rolled)
            {
                if (!counts.TryGetValue(creature.Prefab, out var count)) order.Add(creature.Prefab);
                counts[creature.Prefab] = (count.All + 1, count.Starred + (creature.Level > 1 ? 1 : 0));
            }
            var parts = new List<string>();
            foreach (var prefab in order)
            {
                var count = counts[prefab];
                parts.Add($"{nameOf(prefab)} × {Numbers.Count(count.All)}" + (count.Starred > 0 ? $" ({Numbers.Count(count.Starred)} with stars)" : ""));
            }
            return "The first roll of each, as the game rolls it: " + string.Join(", ", parts) + ".";
        }

        /// <summary>The name of a boss's own event, which has no message of its own to go by.</summary>
        public static string Fighting(string boss) => $"Fighting {boss}";

        /// <summary>
        /// When a boss's event is on: while the boss's health bar shows, which it does while the
        /// boss is alerted within so many metres (<c>RandEventSystem.GetForcedEvent</c>,
        /// <c>EnemyHud.TestShow</c>).
        /// </summary>
        public static string WhileFighting(string boss, float range) => $"while {boss} is alerted within {Numbers.Amount(range)} m of you, its health bar showing";
    }
}
