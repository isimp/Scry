using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>What limits where and when something spawns, for <see cref="SpawnWords.Line"/>. Unset parts are left out.</summary>
    public sealed class SpawnFacts
    {
        /// <summary>The biomes, already in the names the game shows.</summary>
        public string Biomes = "";

        public bool AtNight;
        public bool AtDay;

        /// <summary>The game's levels, one more than the stars; 0 where there are none to tell.</summary>
        public int MinLevel;
        public int MaxLevel;

        public int GroupMin = 1;
        public int GroupMax = 1;

        public bool InForest;
        public bool OutsideForest;

        /// <summary>The weather it needs, any one of them.</summary>
        public string[] Weather = new string[0];

        /// <summary>The world keys it waits for, and those that end it.</summary>
        public string[] Keys = new string[0];
        public string[] NotKeys = new string[0];
    }

    /// <summary>
    /// Where, when and how many of something spawn, in words, the same for world spawns, raids,
    /// nests and plants: stars counted as the game shows them over a head, ranges written as
    /// drops write them, and a world key named by the creature whose defeat sets it (a boss's,
    /// or a troll's "KilledTroll").
    /// </summary>
    public static class SpawnWords
    {
        /// <summary>Stars from the game's levels, which count one more: "no stars", "1 star", "up to 2 stars", "1–2 stars".</summary>
        public static string Stars(int minLevel, int maxLevel)
        {
            var most = Math.Max(0, maxLevel - 1);
            var least = Math.Max(0, minLevel - 1);
            if (most == 0) return "no stars";
            var noun = most == 1 ? "star" : "stars";
            if (least == most) return $"{most} {noun}";
            return least == 0 ? $"up to {most} {noun}" : $"{DropWords.Range(least, most)} {noun}";
        }

        /// <summary>How many come together, or null for one at a time.</summary>
        public static string Group(int min, int max) => max > 1 ? "in groups of " + DropWords.Range(min, max) : null;

        /// <summary>
        /// Where the world grows something (<c>ZoneSystem.m_vegetation</c>): its biomes, named, and
        /// every limit it has, its height above the sea in whole metres where it is held to one
        /// (the game's -1000 and 1000 hold it to none), the sea, forests, and groups.
        /// </summary>
        public static string Grows(string biomes, float minAltitude, float maxAltitude, bool inSea, bool inForest, int groupMin, int groupMax)
        {
            var line = "Grows in " + biomes;
            if (minAltitude > -1000f || maxAltitude < 1000f)
            {
                line += maxAltitude < 1000f
                    ? $", {DropWords.Range((int)Math.Round(minAltitude), (int)Math.Round(maxAltitude))} m up"
                    : $", from {(int)Math.Round(minAltitude)} m up";
            }
            if (inSea) line += ", in the sea";
            if (inForest) line += ", in forests";
            var group = Group(groupMin, groupMax);
            if (group != null) line += ", " + group;
            return line;
        }

        /// <summary>What a world key it waits for means: the creature that sets it defeated, else the key by name.</summary>
        public static string Once(string key, Func<string, string> bossOf) => "once " + KeyWords(key, bossOf);

        /// <summary>What a world key that ends it means.</summary>
        public static string Until(string key, Func<string, string> bossOf) => "until " + KeyWords(key, bossOf);

        private static string KeyWords(string key, Func<string, string> bossOf)
        {
            var boss = bossOf?.Invoke(key);
            return string.IsNullOrEmpty(boss) ? $"the world key \"{key}\" is set" : $"{boss} is defeated";
        }

        // ----- Spawners (SpawnArea) -----

        private static string Metres(float value) => value.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture) + " m";

        /// <summary>
        /// How often a spawner spawns. <c>SpawnArea.UpdateSpawn</c> runs every two seconds, adds two
        /// seconds to its timer and spawns once the timer is past the interval, so the pace is the
        /// first even second past it.
        /// </summary>
        public static string SpawnerPace(float interval)
        {
            var seconds = 2f * ((float)Math.Floor(Math.Max(0f, interval) / 2f) + 1f);
            return "one every " + Naming.Duration(seconds);
        }

        /// <summary>A spawner works only while a player is within its trigger distance.</summary>
        public static string SpawnerWakes(float triggerDistance) => $"while someone is within {Metres(triggerDistance)}";

        /// <summary>The most of its own creatures, untamed, it lets be alive near it and farther out (<c>SpawnArea.GetInstances</c>).</summary>
        public static string SpawnerCaps(int maxNear, float nearRadius, int maxTotal, float farRadius)
        {
            return $"{Naming.Count(maxNear)} within {Metres(nearRadius)}, {Naming.Count(maxTotal)} within {Metres(farRadius)}";
        }

        /// <summary>How far from itself it puts what it spawns, and whether only where nothing is built (<c>SpawnArea.FindSpawnPoint</c>).</summary>
        public static string SpawnerPlaces(float radius, bool groundOnly)
        {
            return $"up to {Metres(radius)} away" + (groundOnly ? ", on open ground only" : "");
        }

        /// <summary>
        /// A creature's share of a spawner's spawns, by its weight among the whole pool
        /// (<c>SpawnArea.SelectWeightedPrefab</c>), and its stars. The only one in the pool, or a
        /// pool without weights, is every spawn.
        /// </summary>
        public static string PoolShare(float weight, float totalWeight, int minLevel, int maxLevel)
        {
            var share = weight < totalWeight ? DropWords.Share(weight / totalWeight) + " of the spawns" : "every spawn";
            return share + ", " + Stars(minLevel, maxLevel);
        }

        /// <summary>
        /// The levels a location sets for its spawn points (<c>CreatureSpawner.Spawn</c>): each of
        /// its overrides set (0 or more) replaces that point's own least level, most level or star
        /// chance, and spawn groups it excludes keep their own. Null when it sets none that matters.
        /// </summary>
        public static string LocationLevels(int minLevel, int maxLevel, float levelUpChance, bool someExcluded)
        {
            var parts = new List<string>();
            if (maxLevel >= 0) parts.Add(Stars(Math.Max(1, minLevel), maxLevel));
            else if (minLevel > 1) parts.Add($"at least {minLevel - 1} {(minLevel == 2 ? "star" : "stars")}");
            if (levelUpChance > 0f) parts.Add(StarChance(levelUpChance));
            else if (levelUpChance == 0f) parts.Add("never a star above the least");
            if (parts.Count == 0) return null;
            if (someExcluded) parts.Add("except at some of them");
            return string.Join(", ", parts);
        }

        /// <summary>The chance of each star beyond the least, each rolled on its own until one misses (<c>SpawnArea.SpawnOne</c>).</summary>
        public static string StarChance(float levelUpChance) => levelUpChance > 0f ? $"{Naming.Number(levelUpChance)}% for each star" : null;

        /// <summary>A whole line: how it starts ("Spawns in"), then the biomes and every limit it has.</summary>
        public static string Line(string start, SpawnFacts spawn, Func<string, string> bossOf)
        {
            var parts = new List<string> { string.IsNullOrEmpty(spawn.Biomes) ? start : start + " " + spawn.Biomes };
            if (spawn.AtNight != spawn.AtDay) parts.Add(spawn.AtNight ? "at night" : "by day");
            if (spawn.MaxLevel > 0) parts.Add(Stars(spawn.MinLevel, spawn.MaxLevel));
            var group = Group(spawn.GroupMin, spawn.GroupMax);
            if (group != null) parts.Add(group);
            if (spawn.InForest != spawn.OutsideForest) parts.Add(spawn.InForest ? "in forests" : "outside forests");
            if (spawn.Weather != null && spawn.Weather.Length > 0) parts.Add("in weather " + string.Join(" or ", spawn.Weather));
            if (spawn.Keys != null) foreach (var key in spawn.Keys) if (!string.IsNullOrEmpty(key)) parts.Add(Once(key, bossOf));
            if (spawn.NotKeys != null) foreach (var key in spawn.NotKeys) if (!string.IsNullOrEmpty(key)) parts.Add(Until(key, bossOf));
            return string.Join(", ", parts);
        }
    }
}
