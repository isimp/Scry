using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>The words of a fish's page (<see cref="FishPage"/>): its labels, and each line by the game's figures.</summary>
    internal static class FishWords
    {
        public const string Swims = "Swims";
        public const string GroupsOf = "Groups of";
        public const string Stars = "Stars";
        public const string Water = "Water";
        public const string Never = "Never";
        public const string HowMany = "How many";
        public const string Comes = "Comes";
        public const string Leaps = "Leaps";
        public const string FindsFloat = "Finds a float";
        public const string SwimsOff = "Swims off";
        public const string WrongBait = "Wrong bait";
        public const string Rests = "Rests";
        public const string Line = "Line";
        public const string OnLand = "On land";

        /// <summary>The title of the fight's table by stars.</summary>
        public const string FightTitle = "The fight, by stars";

        public const string ComesWith = "Comes with it";
        public const string PullsFor = "Pulls for up to";
        public const string Drains = "Reeling drains";
        public const string Size = "Size";
        public const string WeightRow = "Weight";
        public const string RawFish = "Raw fish at the table";


        /// <summary>The title of the baits a fish bites on, each with its chance once it reaches the hook (<c>Fish.TestBate</c>).</summary>
        public const string BitesOn = "Bites on, when it reaches the hook";

        /// <summary>What drives a fish off (<c>Fish.CustomFixedUpdate</c> through <c>Player.GetPlayerNoiseRange</c>): the noise a player makes, by <c>Character.AddNoise</c>'s ranges, effects changing it (<c>SEMan.ModifyNoise</c>).</summary>
        public const string SwimsOffLine = "from your noise: walking or swimming 15 m, running or jumping 30 m, building 50 m; some effects change it";

        /// <summary>A float a fish found with the wrong bait on it (<c>FishingFloat.RPC_Nibble</c> marks it the fish's failed bait).</summary>
        public const string WrongBaitLine = "it tries it once and never comes back to that float";

        /// <summary>A fish out of the water (<c>Fish.Interact</c>, only while out of it).</summary>
        public const string OnLandLine = "out of the water it can be picked up by hand";

        /// <summary>A fish that never leaps (<c>m_jumpChance</c> none).</summary>
        public const string NeverLeaps = "never leaps out of the water";

        /// <summary>Its swimming depth as a tile: "1.5–3 m down".</summary>
        public static string SwimsTile(float least, float most) => Range(least, most) + " m down";

        /// <summary>The most stars it comes with, as a tile.</summary>
        public static string StarsTile(int stars) => stars > 0 ? "up to " + Numbers.Count(stars) : "none";

        /// <summary>Where it swims, by its own figures: "it swims 1.5 to 3 m down, within 10 m of where it came".</summary>
        public static string SwimsLine(FishTraits fish) =>
            $"it swims {Numbers.Amount(fish.MinDepth, 1)} to {Numbers.Amount(fish.MaxDepth, 1)} m down" + (fish.SwimRange > 0f ? $", within {Numbers.Metres(fish.SwimRange, 0)} of where it came" : "");

        /// <summary>
        /// The water a world spawn puts it in (<c>SpawnSystem.IsSpawnPointGood</c>): how far below
        /// the surface the bottom lies, from the ground's height against the water's, the sea's
        /// depth where it asks, then how it swims.
        /// </summary>
        public static string WaterLine(FishSpawnRule spawn, FishTraits fish)
        {
            var parts = new List<string>();
            var shallowest = -spawn.MaxAltitude;
            var deepest = -spawn.MinAltitude;
            if (shallowest > 0f && deepest >= 900f) parts.Add($"where the bottom lies {Numbers.Amount(shallowest, 1)} m or more below the surface");
            else if (shallowest >= 0f && deepest > 0f && deepest < 900f) parts.Add($"where the bottom lies {Numbers.Amount(shallowest, 1)} to {Numbers.Amount(deepest, 1)} m below the surface");
            if (Math.Abs(spawn.MinOceanDepth - spawn.MaxOceanDepth) > 0.001f) parts.Add($"where the sea is {Numbers.Amount(spawn.MinOceanDepth, 1)} to {Numbers.Amount(spawn.MaxOceanDepth, 1)} m deep");
            parts.Add(SwimsLine(fish));
            return string.Join("; ", parts);
        }

        /// <summary>Where a world spawn never puts it: near a player, inside a base, near another of its kind.</summary>
        public static string NeverLine(FishSpawnRule spawn)
        {
            var parts = new List<string>();
            if (spawn.NearPlayer > 0f) parts.Add($"within {Numbers.Metres(spawn.NearPlayer, 0)} of a player");
            if (!spawn.InsideBase) parts.Add("inside a base");
            if (spawn.SpawnDistance > 0f) parts.Add($"where another of its kind swims within {Numbers.Metres(spawn.SpawnDistance, 0)}");
            if (parts.Count == 0) return null;
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.GetRange(0, parts.Count - 1)) + ", or " + parts[parts.Count - 1];
        }

        public static string HowManyLine(int most) => $"up to {Numbers.Count(most)} near you";

        /// <summary>How often a world spawn tries and how many tries bring some: "one try every 120 s, half of them bring a group".</summary>
        public static string ComesLine(FishSpawnRule spawn)
        {
            if (spawn.Interval <= 0f) return null;
            var brings = spawn.GroupMax > 1 ? "a group" : "one";
            var share = spawn.Chance >= 100f ? "each brings " + brings
                : Math.Abs(spawn.Chance - 50f) < 0.01f ? "half of them bring " + brings
                : Numbers.Percent(spawn.Chance / 100f) + " of them bring " + brings;
            return $"one try every {Numbers.Duration(spawn.Interval)}, {share}";
        }

        /// <summary>
        /// When it leaps (<c>Fish.ConsiderJump</c>): its chance times how near the time is to dawn
        /// or dusk, none at noon and midnight, and never above its highest leaping level.
        /// </summary>
        public static string LeapsLine(float chance, float maxLevel)
        {
            if (chance <= 0f) return NeverLeaps;
            var line = "most at dawn and dusk, least at noon and midnight";
            var stars = (int)Math.Floor(maxLevel);
            return maxLevel < 50f ? line + $"; one with {Numbers.Count(stars)} or more stars never leaps" : line;
        }

        /// <summary>
        /// How it finds a float (<c>Fish.RandomizeWaypoint</c>, <c>FindFloat</c>): within the
        /// float's reach, at its chance each time it picks a spot, a spot every so many seconds; on
        /// average once in the middle of those over the chance.
        /// </summary>
        public static string FindsFloatLine(float range, float chance, float least, float most)
        {
            var times = Math.Abs(1f / chance - Math.Round(1f / chance)) < 0.01f ? "one time in " + InWords((int)Math.Round(1f / chance)) : Numbers.Percent(chance) + " of the time";
            var average = (least + most) / 2f / chance;
            return $"within {Numbers.Metres(range, 0)}, {times} each time it swims somewhere new, every {Numbers.Amount(least, 1)} to {Numbers.Amount(most, 1)} s: about once in {Numbers.Duration(average)} for each one near";
        }

        public static string RestsLine(float least, float most) => $"{Numbers.Amount(least, 1)} to {Numbers.Amount(most, 1)} s between pulls";

        public static string LineLine(float past, float most) => $"breaks once the float is {Numbers.Metres(past, 0)} past the line, or {Numbers.Metres(most, 0)} out";

        /// <summary>A column of the fight's table by its stars.</summary>
        public static string StarColumn(int stars) => stars <= 0 ? "no star" : Numbers.Count(stars);

        /// <summary>A share of fish, to a tenth where small: "16%", "3.2%".</summary>
        public static string Odds(float share) => Numbers.Percent(share, share >= 0.1f ? 0 : 1);

        public static string Seconds(float seconds) => Numbers.Amount(seconds, 1) + " s";

        /// <summary>Reeling's drain a second, resting / pulling away: "3 / 10".</summary>
        public static string Drain(float resting, float pulling) => Numbers.Amount(resting, 1) + " / " + Numbers.Amount(pulling, 1);

        private static string Range(float least, float most) =>
            Math.Abs(least - most) < 0.001f ? Numbers.Amount(least, 1) : Numbers.Amount(least, 1) + "–" + Numbers.Amount(most, 1);

        private static readonly string[] Small = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };

        private static string InWords(int count) => count >= 0 && count < Small.Length ? Small[count] : Numbers.Count(count);
    }
}
