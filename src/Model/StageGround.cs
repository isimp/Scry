using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What the stage's backdrop puts behind and under a model: the plain floor, the sky, the
    /// grid ruled in metres, or with the Ground backdrop its own biome's ground, painted by the
    /// world's terrain as the game paints it. Of the biomes it is in, the first players meet
    /// stands for it, Meadows for none. A dungeon's inside or a room of one has no ground of the
    /// world's: there the plain floor stays.
    /// </summary>
    public static class StageGround
    {
        /// <summary>The backdrops, by index.</summary>
        public static readonly string[] Backdrops = { "Plain", "Sky", "Grid", "Sky and grid", "Ground", "Sky and ground" };

        public static bool Sky(int backdrop) => backdrop == 1 || backdrop == 3 || backdrop == 5;

        public static bool Grid(int backdrop) => backdrop == 2 || backdrop == 3;

        /// <summary>Whether the biome's ground shows: with a Ground backdrop, where there is ground.</summary>
        public static bool Ground(int backdrop, bool underground) => (backdrop == 4 || backdrop == 5) && !underground;

        /// <summary>Whether the plain floor shows: where neither the grid nor the ground does.</summary>
        public static bool Floor(int backdrop, bool underground) => !Grid(backdrop) && !Ground(backdrop, underground);

        /// <summary>
        /// The fourth share of the terrain's paint mask under the biome's ground: the world keeps
        /// the Ashlands' lava and the Mistlands' own share there, and leaves it whole elsewhere
        /// (<c>WorldGenerator.GetBiomeHeight</c>). Ashlands ground without lava, the Mistlands' at
        /// its most common, the rest as the world leaves it.
        /// </summary>
        public static float MaskShare(string biome) => biome == "AshLands" || biome == "Mistlands" ? 0f : 1f;

        /// <summary>How deep the water over the biome's ground is, the sea's floor lying under it; null for ground in the open.</summary>
        public static float? WaterOver(string biome) => biome == "Ocean" ? 4f : (float?)null;

        /// <summary>The biome whose ground it stands on: the first of its own players meet, a mod's after the game's; Meadows for none.</summary>
        public static string BiomeFor(IEnumerable<string> biomes)
        {
            string best = null;
            var bestRank = int.MaxValue;
            foreach (var biome in biomes ?? new string[0])
            {
                if (string.IsNullOrEmpty(biome)) continue;
                var rank = LocationWords.BiomeRank(biome);
                if (rank < 0) rank = LocationWords.BiomeCount;
                if (rank >= bestRank) continue;
                best = biome;
                bestRank = rank;
            }
            return best ?? "Meadows";
        }
    }
}
