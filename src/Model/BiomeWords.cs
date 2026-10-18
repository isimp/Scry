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

        /// <summary>The game's own English names of its biomes, by their enum names, until the game's text names them (<see cref="NameBy"/>).</summary>
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Meadows", "Meadows" }, { "BlackForest", "Black Forest" }, { "Swamp", "Swamp" }, { "Mountain", "Mountain" }, { "Plains", "Plains" },
            { "Mistlands", "Mistlands" }, { "AshLands", "Ashlands" }, { "DeepNorth", "Deep North" }, { "Ocean", "Ocean" },
        };

        private static Func<string, string> _named;

        /// <summary>Has biomes named as the game shows them in its language, by their enum names (<c>Knowledge.BiomeName</c>); null goes back to its English names.</summary>
        public static void NameBy(Func<string, string> named) => _named = named;

        /// <summary>A biome by its enum name as the game shows it: "BlackForest" as "Black Forest"; one the game has no name for in its words.</summary>
        public static string Shown(string biome)
        {
            if (string.IsNullOrEmpty(biome)) return biome ?? "";
            var named = _named?.Invoke(biome);
            if (!string.IsNullOrEmpty(named)) return named;
            return English.TryGetValue(biome, out var english) ? english : Naming.FieldLabel(biome);
        }

        /// <summary>The name a biome goes by before it is translated, by its enum name, as the game's records keep the biomes a character entered (<c>BiomeSector.GetBiomeName</c>).</summary>
        public static string Token(string biome) => "$biome_" + (biome ?? "").ToLowerInvariant();

        /// <summary>
        /// The name token of the game's no-biome (<c>Heightmap.Biome.None</c>), which a save from
        /// before the game kept biomes by their shown names may hold among the biomes entered: no
        /// biome a character enters, and none of the biomes' entries goes by it.
        /// </summary>
        public static readonly string NoBiome = Token("None");

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

        /// <summary>How many weathers a biome has, on its card.</summary>
        public static string WeatherCount(int weathers) => weathers == 1 ? "One weather" : $"{Numbers.Count(weathers)} weathers";

        /// <summary>The title and columns of a biome's weathers table: each weather, its share of the time and what it does.</summary>
        public const string WeathersTitle = "Weathers";

        public static readonly string[] WeatherColumns = { "Weather", "Of the time", "What it does" };

        /// <summary>The title and columns of a biome's music table: each music and when it plays.</summary>
        public const string MusicTitle = "Music";

        public static readonly string[] MusicColumns = { "Music", "When" };

        /// <summary>Its music by the time of day, each that it has.</summary>
        public static List<(string When, string Music)> Music(string morning, string evening, string day, string night)
        {
            var music = new List<(string, string)>();
            if (!string.IsNullOrEmpty(morning)) music.Add(("In the morning", morning));
            if (!string.IsNullOrEmpty(day)) music.Add(("By day", day));
            if (!string.IsNullOrEmpty(evening)) music.Add(("In the evening", evening));
            if (!string.IsNullOrEmpty(night)) music.Add(("At night", night));
            return music;
        }
    }
}
