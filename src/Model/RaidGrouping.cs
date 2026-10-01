using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What starts an event of <c>RandEventSystem.m_events</c>, which the Raids tab groups by.</summary>
    public enum RaidRole
    {
        /// <summary>Rolled for: by the raid roll, or on a timer of its own.</summary>
        Raid,

        /// <summary>Named by a boss (<c>Character.m_bossEvent</c>): its fight's music and weather.</summary>
        BossFight,

        /// <summary>Neither: a place or a command starts it.</summary>
        Other,
    }

    /// <summary>
    /// How the Raids tab lists its events: raids first, then bosses' fights, then the rest, and
    /// within each in the order the bosses are fought.
    /// </summary>
    public static class RaidGrouping
    {
        /// <summary>The highest rank the list sorts by within a group (<see cref="Entry.GroupRank"/>).</summary>
        private const int MaxRank = 63;

        /// <summary>What starts an event: a boss naming it comes first, as a boss's event is its fight whether or not it could be rolled too.</summary>
        public static RaidRole Role(bool random, float standaloneInterval, bool namedByBoss)
        {
            if (namedByBoss) return RaidRole.BossFight;
            return random || standaloneInterval > 0f ? RaidRole.Raid : RaidRole.Other;
        }

        /// <summary>How far along the game a raid comes: the health of the strongest boss whose defeat it waits for, 0 for none.</summary>
        public static float Strength(IEnumerable<string> requiredKeys, Func<string, float> bossHealth)
        {
            var strongest = 0f;
            foreach (var key in requiredKeys ?? Enumerable.Empty<string>()) strongest = Math.Max(strongest, bossHealth(key));
            return strongest;
        }

        /// <summary>
        /// Each event's rank within its group by its strength, weaker first; alike ones share a
        /// rank, so they stay in name order. Boss health grows boss by boss, so a mod's boss falls
        /// in place too.
        /// </summary>
        public static Dictionary<string, int> Ranks(IReadOnlyDictionary<string, float> strengths)
        {
            var levels = strengths.Values.Distinct().OrderBy(s => s).ToList();
            var rankOf = new Dictionary<float, int>();
            for (var i = 0; i < levels.Count; i++) rankOf[levels[i]] = Math.Min(i, MaxRank);
            return strengths.ToDictionary(pair => pair.Key, pair => rankOf[pair.Value], StringComparer.Ordinal);
        }
    }
}
