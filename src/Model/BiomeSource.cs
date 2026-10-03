using System.Collections.Generic;

namespace Scry
{
    /// <summary>A biome as its entry holds it: its name (the game's enum name), its weathers with their weights, and its music by the time of day.</summary>
    internal sealed class BiomeSource
    {
        public string Name = "";
        public readonly List<(string Name, float Weight)> Weathers = new List<(string, float)>();
        public string Morning = "", Day = "", Evening = "", Night = "";
    }
}
