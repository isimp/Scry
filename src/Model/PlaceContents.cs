using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>One kind of networked part a location or room places, how many of it, and the chance each is there.</summary>
    public sealed class PlacePart
    {
        public string Prefab = "";
        public int Count;

        /// <summary>From 0 to 1; 1 is always there.</summary>
        public float Chance;
    }

    /// <summary>
    /// The networked parts of a location or room as the game places them (<c>ZoneSystem.SpawnLocation</c>,
    /// <c>DungeonGenerator.PlaceRoom</c>): each rolled once when its zone is first built, at the
    /// chance of every <c>RandomSpawn</c> above it and the share of every <c>RandomObject</c> pick.
    /// </summary>
    public static class PlaceParts
    {
        /// <summary>
        /// Each part by its prefab and chance, counted together, what is always there first, then
        /// the likeliest, then by name. Chances equal to a tenth of a percent are one.
        /// </summary>
        public static List<PlacePart> Group(IEnumerable<(string Prefab, float Chance)> raw)
        {
            var parts = new Dictionary<(string, int), PlacePart>();
            foreach (var (prefab, chance) in raw)
            {
                if (string.IsNullOrEmpty(prefab)) continue;
                var key = (prefab, (int)Math.Round(chance * 1000f));
                if (!parts.TryGetValue(key, out var part)) parts[key] = part = new PlacePart { Prefab = prefab, Chance = chance };
                part.Count++;
            }
            return parts.Values
                .OrderByDescending(p => (int)Math.Round(p.Chance * 1000f))
                .ThenBy(p => p.Prefab, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>How many and how likely, as a chip's amount: nothing for one that is always there.</summary>
        public static string Amount(int count, float chance)
        {
            var always = chance >= 0.9995f;
            if (always) return count > 1 ? count.ToString() : "";
            return count > 1 ? $"{count}, {DropWords.Share(chance)} each" : DropWords.Share(chance);
        }
    }
}
