using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The tiers of progress for the compendium's milestone floor: each boss closes its biome's
    /// tier, Eikthyr the Meadows (1) up to Fader the Ashlands (7). The Ocean and the Deep North
    /// have no boss and no tier: only meeting their things shows them.
    /// </summary>
    internal static class Tiers
    {
        /// <summary>No tier: nothing beaten opens it.</summary>
        public const int None = int.MaxValue;

        private static readonly string[] BiomeOrder = { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands" };
        private static readonly string[] BossOrder = { "Eikthyr", "gd_king", "Bonemass", "Dragon", "GoblinKing", "SeekerQueen", "Fader" };

        /// <summary>The last tier there is.</summary>
        public static int Last => BiomeOrder.Length;

        /// <summary>A biome's tier, by the game's name for it; none for the Ocean, the Deep North and any other.</summary>
        public static int OfBiome(string biome)
        {
            var at = Array.IndexOf(BiomeOrder, biome);
            return at < 0 ? None : at + 1;
        }

        /// <summary>The tier a boss closes, by its prefab; none for any other creature.</summary>
        public static int OfBoss(string prefab)
        {
            var at = Array.IndexOf(BossOrder, prefab);
            return at < 0 ? None : at + 1;
        }

        /// <summary>The boss closing a tier, by its prefab; none outside the tiers.</summary>
        public static string BossOf(int tier) => tier >= 1 && tier <= Last ? BossOrder[tier - 1] : null;

        /// <summary>The biome of a tier, by the game's name for it; none outside the tiers.</summary>
        public static string BiomeOf(int tier) => tier >= 1 && tier <= Last ? BiomeOrder[tier - 1] : null;
    }

    /// <summary>Where a way to come by an entry was read from, so a tier can be traced to what set it.</summary>
    internal enum WayFrom
    {
        /// <summary>Not told.</summary>
        Unnamed,
        /// <summary>A biome it lives in, or the biome itself.</summary>
        Biome,
        /// <summary>A place that holds it.</summary>
        Place,
        /// <summary>What plays it.</summary>
        Played,
        /// <summary>What leaves it behind.</summary>
        LeftBehind,
        /// <summary>What gives it, of a status effect.</summary>
        Given,
        /// <summary>What spawns it.</summary>
        Spawned,
        /// <summary>What drops, yields, grows, bears or sells it.</summary>
        Source,
        /// <summary>A station's making.</summary>
        Station,
        /// <summary>A recipe or a piece's cost.</summary>
        Crafted,
        /// <summary>A raid once it is ready.</summary>
        Raid,
    }

    /// <summary>
    /// Works out each entry's tier the way progression works (the design's milestone tiers): an
    /// entry's tier is the lowest over the ways to come by it, and a way costs the highest of its
    /// own tier (a biome lived in, a boss whose fall a trader waits for) and the tiers of what it
    /// needs (the creature that drops it, a recipe's ingredients and station). A way needing what
    /// has no tier leads nowhere, and an entry no way reaches has none. Passes repeat until no
    /// tier lowers, so ways that need each other settle on the cheapest way in.
    /// </summary>
    internal sealed class TierPass
    {
        private sealed class Way
        {
            public int Tier;
            public string[] Needs;
            public WayFrom From;
        }

        private readonly Dictionary<string, List<Way>> _ways = new Dictionary<string, List<Way>>(StringComparer.Ordinal);

        /// <summary>Adds a way to come by an entry: at least its own tier (0 for none of its own), and what it needs.</summary>
        public void Add(string key, int tier, params string[] needs) => Add(WayFrom.Unnamed, key, tier, needs);

        /// <summary>Adds a way to come by an entry, telling where it was read from.</summary>
        public void Add(WayFrom from, string key, int tier, params string[] needs)
        {
            if (string.IsNullOrEmpty(key) || tier == Tiers.None) return;
            if (!_ways.TryGetValue(key, out var ways)) _ways[key] = ways = new List<Way>();
            ways.Add(new Way { Tier = tier, Needs = needs ?? Array.Empty<string>(), From = from });
        }

        /// <summary>The tier of every entry some way reaches.</summary>
        public Dictionary<string, int> Work()
        {
            var tiers = new Dictionary<string, int>(StringComparer.Ordinal);
            bool lowered;
            do
            {
                lowered = false;
                foreach (var pair in _ways)
                {
                    var best = tiers.TryGetValue(pair.Key, out var had) ? had : Tiers.None;
                    foreach (var way in pair.Value)
                    {
                        var cost = Cost(way, tiers);
                        if (cost >= best) continue;
                        best = cost;
                        tiers[pair.Key] = cost;
                        lowered = true;
                    }
                }
            }
            while (lowered);
            return tiers;
        }

        /// <summary>
        /// The way that gives an entry its tier, as worked out: its own tier, what it needs and
        /// where it was read from; none for an entry no way reaches. Tells why a thing has the
        /// tier it has.
        /// </summary>
        public (int Tier, string[] Needs, WayFrom From)? WayIn(string key, Dictionary<string, int> tiers)
        {
            if (key == null || !_ways.TryGetValue(key, out var ways) || !tiers.TryGetValue(key, out var tier)) return null;
            foreach (var way in ways)
            {
                if (Cost(way, tiers) == tier) return (way.Tier, way.Needs, way.From);
            }
            return null;
        }

        private static int Cost(Way way, Dictionary<string, int> tiers)
        {
            var cost = way.Tier;
            foreach (var need in way.Needs)
            {
                if (!tiers.TryGetValue(need, out var tier)) return Tiers.None;
                if (tier > cost) cost = tier;
            }
            return cost;
        }
    }
}
