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
    /// drops write them, and a boss's key named by the boss.
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

        /// <summary>What a world key it waits for means: a boss defeated, else the key by name.</summary>
        public static string Once(string key, Func<string, string> bossOf) => "once " + KeyWords(key, bossOf);

        /// <summary>What a world key that ends it means.</summary>
        public static string Until(string key, Func<string, string> bossOf) => "until " + KeyWords(key, bossOf);

        private static string KeyWords(string key, Func<string, string> bossOf)
        {
            var boss = bossOf?.Invoke(key);
            return string.IsNullOrEmpty(boss) ? $"the world key \"{key}\" is set" : $"{boss} is defeated";
        }

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
