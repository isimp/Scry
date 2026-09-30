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

        /// <summary>
        /// Which entry of a <c>RandomObject</c> a roll from 0 to 1 picks (<c>GetWeightedObject</c>):
        /// the first whose running weight reaches the roll's share of them all, or -1 for none.
        /// </summary>
        public static int Pick(IReadOnlyList<float> weights, double roll)
        {
            var total = 0f;
            foreach (var weight in weights) total += weight;
            var at = (float)(roll * total);
            var running = 0f;
            for (var i = 0; i < weights.Count; i++)
            {
                running += weights[i];
                if (at <= running) return i;
            }
            return -1;
        }

        /// <summary>Each entry's share of a <c>RandomObject</c>'s picks; all 0 when no entry weighs anything.</summary>
        public static float[] Shares(IReadOnlyList<float> weights)
        {
            var total = 0f;
            foreach (var weight in weights) total += weight;
            return weights.Select(w => total > 0f ? w / total : 0f).ToArray();
        }

        /// <summary>
        /// Whether a place can come out otherwise when rolled again: a <c>RandomSpawn</c> between
        /// never and always (chances in percent), or a <c>RandomObject</c> with two or more
        /// entries it can pick.
        /// </summary>
        public static bool LeftToChance(IEnumerable<float> spawnChances, IEnumerable<IReadOnlyList<float>> pickWeights)
        {
            return spawnChances.Any(c => c > 0f && c < 100f) || pickWeights.Any(w => w.Count(x => x > 0f) > 1);
        }

        /// <summary>
        /// What a dungeon's or camp's kinds of room hold, each thing with how many of them it is
        /// in (once for a room however often it is there), the most widespread first, then by
        /// name; a room not read yet adds nothing.
        /// </summary>
        public static List<(string Prefab, int Rooms)> Across(IEnumerable<IReadOnlyList<PlacePart>> rooms)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var room in rooms)
            {
                if (room == null) continue;
                foreach (var prefab in room.Where(p => p != null && !string.IsNullOrEmpty(p.Prefab)).Select(p => p.Prefab).Distinct())
                {
                    counts.TryGetValue(prefab, out var n);
                    counts[prefab] = n + 1;
                }
            }
            return counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.OrdinalIgnoreCase).Select(c => (c.Key, c.Value)).ToList();
        }

        /// <summary>How widespread a thing is among a dungeon's kinds of room, as a chip's amount.</summary>
        public static string InRooms(int rooms) => rooms == 1 ? "in 1 kind of room" : $"in {rooms} kinds of room";

        /// <summary>How many and how likely, as a chip's amount: nothing for one that is always there.</summary>
        public static string Amount(int count, float chance)
        {
            var always = chance >= 0.9995f;
            if (always) return count > 1 ? count.ToString() : "";
            return count > 1 ? $"{count}, {DropWords.Share(chance)} each" : DropWords.Share(chance);
        }
    }
}
