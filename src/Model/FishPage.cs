using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What a fish is by its own figures (<c>Fish</c>), with the rod's float and the prep table's recipe for it.</summary>
    internal sealed class FishTraits
    {
        /// <summary>How deep it swims (<c>m_minDepth</c>, <c>m_maxDepth</c>), and how far from where it came (<c>m_swimRange</c>).</summary>
        public float MinDepth, MaxDepth, SwimRange;

        /// <summary>How long it swims to each spot it picks (<c>m_wpDurationMin</c>, <c>m_wpDurationMax</c>), and its chance there of taking a float (<c>m_baseHookChance</c>).</summary>
        public float WaypointMin, WaypointMax, HookChance;

        /// <summary>What reeling it drains, resting and pulling away (<c>m_staminaUse</c>, <c>m_escapeStaminaUse</c>).</summary>
        public float StaminaUse, EscapeStaminaUse;

        /// <summary>How long it pulls away (<c>Fish.Escape</c>: <c>m_escapeMin</c> to <c>m_escapeMax</c> and <c>m_escapeMaxPerLevel</c> for each level) and rests between (<c>m_escapeWaitMin</c>, <c>m_escapeWaitMax</c>).</summary>
        public float EscapeMin, EscapeMax, EscapePerLevel, RestMin, RestMax;

        /// <summary>How often it leaps (<c>m_jumpChance</c>), and the highest level that still leaps (<c>m_jumpMaxLevel</c>).</summary>
        public float JumpChance, JumpMaxLevel;

        /// <summary>How its size and weight grow with each level (<c>ItemData.SharedData.m_scaleByQuality</c>, <c>m_scaleWeightByQuality</c>), and its weight.</summary>
        public float ScaleByQuality, ScaleWeightByQuality, Weight;

        /// <summary>The float's reach for a fish (<c>FishingFloat.m_range</c>) and when the line breaks (<c>m_breakDistance</c> past the line, <c>m_maxDistance</c> in all); none where no rod was read.</summary>
        public float FloatRange, LineBreakPast, LineMost;

        /// <summary>What the prep table makes of one (<c>Recipe.GetAmount</c>: its amount, more by <c>m_qualityResultAmountMultiplier</c> for each level); none for no such recipe.</summary>
        public int RawAmount;
        public float RawPerQuality;
    }

    /// <summary>A world spawn placing a fish (<c>SpawnSystem.SpawnData</c>), as far as it tells where and when it comes.</summary>
    internal sealed class FishSpawnRule
    {
        /// <summary>The height of the ground under it against the water's (<c>m_minAltitude</c>, <c>m_maxAltitude</c>, the spawn point less 30 m), and the sea's depth there where asked (<c>m_minOceanDepth</c>, <c>m_maxOceanDepth</c>).</summary>
        public float MinAltitude = -1000f, MaxAltitude = 1000f, MinOceanDepth, MaxOceanDepth;

        /// <summary>How often it is tried and the chance each try spawns (<c>m_spawnInterval</c>, <c>m_spawnChance</c> in percent).</summary>
        public float Interval, Chance;

        /// <summary>How many may be near at once (<c>m_maxSpawned</c>), and how far another must be (<c>m_spawnDistance</c>).</summary>
        public int MaxSpawned;
        public float SpawnDistance;

        public int GroupMin = 1, GroupMax = 1;

        /// <summary>How near a player it never comes (<c>m_spawnRadiusMin</c>, else 40 m; none where <c>m_canSpawnCloseToPlayer</c>), and whether it comes inside a base.</summary>
        public float NearPlayer;
        public bool InsideBase;

        /// <summary>Its levels and the chance of each level more (<c>SpawnSystem.GetLevelUpChance</c>, in percent).</summary>
        public int MinLevel = 1, MaxLevel = 1;
        public float LevelUpChance;
    }

    /// <summary>
    /// A fish's page by its own figures and the world spawns placing it: its tiles, where it lives
    /// and when it comes, how it finds a float and what drives it off, and its fight as a table
    /// by stars. A fish no world spawn places tells only what it is itself.
    /// </summary>
    internal sealed class FishPage
    {
        /// <summary>The labelled lines, tiles first (<see cref="FishWords"/> names each label).</summary>
        public readonly List<(string Label, string Value)> Lines = new List<(string, string)>();

        /// <summary>The fight by stars: its columns, then each line's cells, the label first.</summary>
        public (string[] Columns, List<string[]> Lines) Fight;

        public static FishPage Of(FishTraits fish, IReadOnlyList<FishSpawnRule> spawns)
        {
            var page = new FishPage();
            void Add(string label, string value)
            {
                if (!string.IsNullOrEmpty(value)) page.Lines.Add((label, value));
            }

            var any = spawns.Count > 0;
            Add(FishWords.Swims, FishWords.SwimsTile(fish.MinDepth, fish.MaxDepth));
            if (any) Add(FishWords.GroupsOf, Numbers.CountRange(spawns.Min(s => s.GroupMin), spawns.Max(s => s.GroupMax)));
            var most = any ? spawns.Max(s => s.MaxLevel) : 1;
            if (any) Add(FishWords.Stars, FishWords.StarsTile(most - 1));

            Add(FishWords.Water, Joined(spawns.Select(s => FishWords.WaterLine(s, fish))));
            if (!any) Add(FishWords.Water, FishWords.SwimsLine(fish));
            Add(FishWords.Never, Joined(spawns.Select(FishWords.NeverLine)));
            Add(FishWords.HowMany, Joined(spawns.Where(s => s.MaxSpawned > 0).Select(s => FishWords.HowManyLine(s.MaxSpawned))));
            Add(FishWords.Comes, Joined(spawns.Select(FishWords.ComesLine)));
            Add(FishWords.Leaps, FishWords.LeapsLine(fish.JumpChance, fish.JumpMaxLevel));

            if (fish.FloatRange > 0f && fish.HookChance > 0f) Add(FishWords.FindsFloat, FishWords.FindsFloatLine(fish.FloatRange, fish.HookChance, fish.WaypointMin, fish.WaypointMax));
            Add(FishWords.SwimsOff, FishWords.SwimsOffLine);
            Add(FishWords.WrongBait, FishWords.WrongBaitLine);
            if (fish.RestMax > 0f) Add(FishWords.Rests, FishWords.RestsLine(fish.RestMin, fish.RestMax));
            if (fish.LineMost > 0f) Add(FishWords.Line, FishWords.LineLine(fish.LineBreakPast, fish.LineMost));
            Add(FishWords.OnLand, FishWords.OnLandLine);

            var least = any ? spawns.Min(s => s.MinLevel) : 1;
            var chance = any ? spawns.Max(s => s.LevelUpChance) / 100f : 0f;
            page.Fight = FightTable(fish, Math.Max(1, least), Math.Max(least, most), chance);
            return page;
        }

        private static string Joined(IEnumerable<string> lines)
        {
            var told = lines.Where(l => !string.IsNullOrEmpty(l)).Distinct().ToList();
            return told.Count == 0 ? null : string.Join(" · ", told);
        }

        /// <summary>The fight by level: how likely each is, how long it pulls, what reeling drains, its size, weight and raw fish.</summary>
        private static (string[], List<string[]>) FightTable(FishTraits fish, int least, int most, float levelUp)
        {
            var levels = Enumerable.Range(least, most - least + 1).ToList();
            var columns = levels.Select(l => FishWords.StarColumn(l - 1)).Prepend("").ToArray();
            var lines = new List<string[]>();
            void Line(string label, Func<int, string> cell) => lines.Add(levels.Select(cell).Prepend(label).ToArray());

            if (levels.Count > 1) Line(FishWords.ComesWith, l => FishWords.Odds(LevelOdds(l, least, most, levelUp)));
            Line(FishWords.PullsFor, l => FishWords.Seconds(fish.EscapeMax + l * fish.EscapePerLevel));
            if (fish.StaminaUse > 0f || fish.EscapeStaminaUse > 0f) Line(FishWords.Drains, l => FishWords.Drain(fish.StaminaUse * l, fish.EscapeStaminaUse * l));
            if (fish.ScaleByQuality > 0f) Line(FishWords.Size, l => Numbers.Times(1f + (l - 1) * fish.ScaleByQuality, 1));
            if (fish.ScaleWeightByQuality != 0f && fish.Weight > 0f) Line(FishWords.WeightRow, l => Numbers.Amount(fish.Weight * (1f + (l - 1) * fish.ScaleWeightByQuality), 1));
            if (fish.RawAmount > 0) Line(FishWords.RawFish, l => Numbers.Count(fish.RawAmount + (int)Math.Ceiling((l - 1) * fish.RawAmount * fish.RawPerQuality)));
            return (columns, lines);
        }

        /// <summary>
        /// How likely a fish comes at a level (<c>SpawnSystem.Spawn</c>): from the least, each level
        /// more by the level-up chance until one roll fails, the most taking what is left.
        /// </summary>
        public static float LevelOdds(int level, int least, int most, float levelUp)
        {
            var reached = (float)Math.Pow(levelUp, level - least);
            return level >= most ? reached : reached * (1f - levelUp);
        }
    }
}
