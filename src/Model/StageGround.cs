using System;
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
    /// <summary>A place's paint on its ground (<c>TerrainModifier</c>): where, how far it reaches, how strongly, the colour it paints the mask and whether it clears the vegetation.</summary>
    public struct GroundPaint
    {
        public float X, Z, Radius, Strength;
        public (float R, float G, float B, float A) Color;
        public bool ClearsVegetation;
    }

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

        /// <summary>
        /// The height over the sea a biome's ground is taken at, for the game's rules for where
        /// grass grows (<c>ClutterSystem</c>, its heights over the sea): the stage stands at none,
        /// so one typical of the biome, the mountains high, the swamp low, the sea's floor under
        /// its water, the rest a little over the sea.
        /// </summary>
        public static float Altitude(string biome)
        {
            switch (biome)
            {
                case "Mountain":
                case "DeepNorth":
                    return 120f;
                case "Swamp":
                    return 2f;
                default:
                    return WaterOver(biome) is float water ? -water : 10f;
            }
        }

        /// <summary>The ground's biome as the View box offers it: Auto for the entry's own, else any of the game's, in the order players meet them.</summary>
        public static readonly string[] Choices = { "Auto", "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands", "DeepNorth", "Ocean" };

        /// <summary>The biome the ground is of: the one chosen, or with Auto the entry's own (<see cref="BiomeFor"/>).</summary>
        public static string Chosen(string choice, IEnumerable<string> biomes) =>
            string.IsNullOrEmpty(choice) || choice == "Auto" ? BiomeFor(biomes) : choice;

        /// <summary>The choice after one, back to Auto after the last.</summary>
        public static string Next(string choice)
        {
            var at = System.Array.IndexOf(Choices, choice);
            return at == Choices.Length - 1 ? Choices[0] : Choices[at + 1];
        }

        /// <summary>
        /// How far across the ground reaches from what is framed, by its radius: several times it,
        /// and with the sky far out toward a horizon, never less than some way.
        /// </summary>
        /// <summary>
        /// How many points the ground's paint mask has across: two while nothing is painted on
        /// it, else half a metre a point, at least 32 and at most 256, however wide the ground.
        /// </summary>
        public static int MaskSize(int paints, float across) => paints == 0 ? 2 : Math.Max(32, Math.Min(256, (int)Math.Ceiling(across * 2f)));

        public static float Across(float radius, bool sky) =>
            sky ? System.Math.Max(160f, radius * 20f) : System.Math.Max(24f, radius * 6f);

        /// <summary>How much the ground fades into what is behind it, by how far out from its middle it is as a share of its half width: none in its middle, all at its edge.</summary>
        public static float Fade(float share)
        {
            var t = System.Math.Max(0f, System.Math.Min(1f, (share - 0.6f) / 0.35f));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The time of day the game's colours for a biome are taken from.</summary>
        public enum Time { Day, Evening, Night }

        /// <summary>The time of day of the stage's lighting, by its name: Studio and Day by day, Dusk by evening, Night by night; none for the cave, which keeps its own.</summary>
        public static Time? TimeFor(string lighting)
        {
            switch (lighting)
            {
                case "Studio":
                case "Day":
                    return Time.Day;
                case "Dusk":
                    return Time.Evening;
                case "Night":
                    return Time.Night;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The ground's paint mask at a point, as the game paints its terrain under a place
        /// (<c>Heightmap.PaintCleared</c>): each paint in turn blends toward its colour by
        /// (1 - its distance over its reach) to the tenth power times its strength, keeping the
        /// mask's fourth share unless it clears the vegetation.
        /// </summary>
        public static (float R, float G, float B, float A) Painted((float R, float G, float B, float A) ground, float x, float z, IEnumerable<GroundPaint> paints)
        {
            var color = ground;
            foreach (var paint in paints)
            {
                if (paint.Radius <= 0f) continue;
                var dx = x - paint.X;
                var dz = z - paint.Z;
                var share = 1f - System.Math.Max(0f, System.Math.Min(1f, (float)System.Math.Sqrt(dx * dx + dz * dz) / paint.Radius));
                var f = (float)System.Math.Pow(share, 0.1) * paint.Strength;
                var keep = color.A;
                color = (color.R + (paint.Color.R - color.R) * f, color.G + (paint.Color.G - color.G) * f, color.B + (paint.Color.B - color.B) * f, color.A + (paint.Color.A - color.A) * f);
                if (!paint.ClearsVegetation) color.A = keep;
            }
            return color;
        }

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
