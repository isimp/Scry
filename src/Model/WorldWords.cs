using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// A note for a world whose settings change what the facts show, which are the prefab's own
    /// numbers: its world level gives enemies more health (<c>Character.GetMaxHealthBase</c>:
    /// level times <c>Game.m_worldLevelEnemyHPMultiplier</c>), and its resource rate scales most
    /// drops (<c>Game.ScaleDrops</c>, all but a few item types, and the rolls of a drop table).
    /// </summary>
    internal static class WorldWords
    {
        /// <summary>The note, or null when this world changes nothing the entry shows.</summary>
        public static string Note(int worldLevel, float healthMultiplier, float resourceRate, bool health, bool drops)
        {
            var parts = new List<string>();
            if (health && worldLevel > 0) parts.Add($"health {Numbers.Times(worldLevel * healthMultiplier)} (world level {Numbers.Count(worldLevel)})");
            if (drops && resourceRate > 0f && System.Math.Abs(resourceRate - 1f) > 0.001f) parts.Add($"most drops {Numbers.Times(resourceRate)} (resource rate)");
            return parts.Count > 0 ? string.Join(" and ", parts) + ", not counted in these figures" : null;
        }
    }
}
