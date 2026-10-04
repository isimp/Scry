using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A biome's page in words: where it stands among the biomes, in the order players meet
    /// them; how likely each of its weathers is, as the game picks one by weight
    /// (<c>EnvMan.SelectWeightedEnvironment</c>), the most likely first; and its music by the time
    /// of day (<c>BiomeEnvSetup</c>'s morning, day, evening and night music).
    /// </summary>
    internal static class BiomeWords
    {
        /// <summary>Its place in the list: the order players meet the biomes, a biome a mod adds after them all.</summary>
        public static int Rank(string biome)
        {
            var at = LocationWords.BiomeRank(biome);
            return at >= 0 ? at : Math.Min(63, LocationWords.BiomeCount);
        }

        /// <summary>Each weather once, with its share of the weights, the most likely first; none weighing nothing.</summary>
        public static List<(string Weather, string Share)> Weathers(IEnumerable<(string Name, float Weight)> weathers)
        {
            var merged = new List<(string Name, float Weight)>();
            foreach (var (name, weight) in weathers)
            {
                if (string.IsNullOrEmpty(name) || weight <= 0f) continue;
                var at = merged.FindIndex(w => w.Name == name);
                if (at >= 0) merged[at] = (name, merged[at].Weight + weight);
                else merged.Add((name, weight));
            }
            var total = merged.Sum(w => w.Weight);
            return merged.OrderByDescending(w => w.Weight).Select(w => (w.Name, DropWords.Share(w.Weight / total))).ToList();
        }

        /// <summary>Its music by the time of day, each that it has.</summary>
        /// <summary>A weather's line, by how much of the time the biome has it.</summary>
        public static string ShareOfTime(string share) => share + " of the time";

        public static List<(string Label, string Music)> Music(string morning, string evening, string day, string night)
        {
            var music = new List<(string, string)>();
            if (!string.IsNullOrEmpty(morning)) music.Add(("Music in the morning", morning));
            if (!string.IsNullOrEmpty(day)) music.Add(("Music by day", day));
            if (!string.IsNullOrEmpty(evening)) music.Add(("Music in the evening", evening));
            if (!string.IsNullOrEmpty(night)) music.Add(("Music at night", night));
            return music;
        }
    }
}
