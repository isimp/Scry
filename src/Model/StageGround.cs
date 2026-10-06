using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>A place's paint on its ground (<c>TerrainModifier</c>): where, how far it reaches, how strongly, the colour it paints the mask and whether it clears the vegetation.</summary>
    internal struct GroundPaint
    {
        public float X, Z, Radius, Strength;
        public (float R, float G, float B, float A) Color;
        public bool ClearsVegetation;
    }

    /// <summary>
    /// What the stage's backdrop puts behind and under a model: the plain floor, the sky, the
    /// grid ruled in metres, or with the Ground backdrop its own biome's ground, painted by the
    /// world's terrain as the game paints it. Of the biomes it is in, the first players meet
    /// stands for it, Meadows for none. A dungeon's inside or a room of one has no ground of the
    /// world's: there the plain floor stays.
    /// </summary>
    internal static class StageGround
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
        /// The ground a creature's footsteps sound on, standing on the stage: the plain floor is
        /// the game's default ground (<c>FootStep.GetGroundMaterial</c> off the terrain); the
        /// sea's floor is water, as the feet stand in the water over it and the game steps on
        /// water first; and a biome's ground follows the game's rule for flat terrain
        /// (<c>Heightmap.GetGroundMaterial</c>): snow in the mountains and the deep north, mud in
        /// the swamp, grass in the meadows and the black forest, ash in the Ashlands, ground in
        /// general elsewhere.
        /// </summary>
        public static StepGround Footsteps(string biome, bool groundShown)
        {
            if (!groundShown) return StepGround.Default;
            if (WaterOver(biome) != null) return StepGround.Water;
            switch (biome)
            {
                case "Mountain":
                case "DeepNorth":
                    return StepGround.Snow;
                case "Swamp":
                    return StepGround.Mud;
                case "Meadows":
                case "BlackForest":
                    return StepGround.Grass;
                case "AshLands":
                    return StepGround.Ashlands;
                default:
                    return StepGround.GenericGround;
            }
        }

        /// <summary>
        /// The step the game plays from a creature's step table, by its place there, or -1 for
        /// none (<c>FootStep.FindBestStepEffect</c>): of the steps fitting the way of moving, the
        /// last made for the ground, else the first made for the default ground.
        /// </summary>
        /// <param name="made">The grounds each step is made for, in the table's order.</param>
        /// <param name="fits">Whether each step is made for the way of moving.</param>
        /// <param name="ground">The ground stepped on.</param>
        public static int GameStep(IReadOnlyList<StepGround> made, IReadOnlyList<bool> fits, StepGround ground) => Step(made, fits, ground, anyWay: false);

        /// <summary>
        /// The step the stage plays from a creature's steps that have something to play: the one
        /// the game would play (<see cref="GameStep"/>); past the game, where it would fall
        /// silent, one made for ground in general, then the same two whatever way of moving
        /// they are made for, then the first, so a creature is never mute on the stage.
        /// </summary>
        /// <param name="made">The grounds each step is made for, in the table's order.</param>
        /// <param name="fits">Whether each step is made for the way of moving.</param>
        /// <param name="ground">The ground stepped on.</param>
        public static int StageStep(IReadOnlyList<StepGround> made, IReadOnlyList<bool> fits, StepGround ground)
        {
            foreach (var anyWay in new[] { false, true })
            {
                var step = Step(made, fits, ground, anyWay);
                if (step < 0) step = Step(made, fits, StepGround.GenericGround, anyWay);
                if (step >= 0) return step;
            }
            return made.Count > 0 ? 0 : -1;
        }

        private static int Step(IReadOnlyList<StepGround> made, IReadOnlyList<bool> fits, StepGround ground, bool anyWay)
        {
            var best = -1;
            for (var i = 0; i < made.Count; i++)
            {
                if (!anyWay && !fits[i]) continue;
                if ((made[i] & ground) != 0 || (best < 0 && (made[i] & StepGround.Default) != 0)) best = i;
            }
            return best;
        }

        /// <summary>
        /// How far round the model the stage's grass is kept off, in metres: the ground it stands
        /// on and a margin (its footprint, half its widest side, a fifth more and 0.3 m); and for
        /// something lower than grass grows (under 0.6 m), at least 1.5 m, so the grass between it
        /// and the camera does not hide it.
        /// </summary>
        public static float ClearOfGrass(float footprint, float height)
        {
            var margin = footprint * 1.2f + 0.3f;
            return height < 0.6f ? Math.Max(margin, 1.5f) : margin;
        }

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
        /// How many points the ground's paint mask has across: two while nothing is painted on
        /// it, else half a metre a point, at least 32 and at most 256, however wide the ground.
        /// </summary>
        public static int MaskSize(int paints, float across) => paints == 0 ? 2 : Math.Max(32, Math.Min(256, (int)Math.Ceiling(across * 2f)));

        /// <summary>
        /// How far across the ground reaches from what is framed, by its radius: several times it,
        /// and with the sky far out toward a horizon, never less than some way.
        /// </summary>
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

        /// <summary>The cells each side of the ground's mask is split into, to note which paints reach each.</summary>
        private const int MaskCells = 32;

        /// <summary>
        /// The ground's mask, a point at a time as <see cref="Painted"/> paints each: size by size
        /// points across a square <paramref name="across"/> wide around a middle, written row by
        /// row into the points given. Each paint is first noted in the cells of a coarse grid it
        /// may reach, in the game's order, so a point weighs only the paints of its cell rather
        /// than every one of a camp's hundreds; the mask is the same.
        /// </summary>
        public static void PaintMask(int size, float middleX, float middleZ, float across, IReadOnlyList<GroundPaint> paints, (float R, float G, float B, float A) bare, (float R, float G, float B, float A)[] into)
        {
            var cells = Math.Min(size, MaskCells);
            var reach = new List<int>[cells * cells];
            for (var n = 0; n < paints.Count; n++)
            {
                var paint = paints[n];
                if (!(paint.Radius >= 0f)) continue;
                var (i0, i1) = Span(paint.X, paint.Radius, middleX, across, size);
                var (j0, j1) = Span(paint.Z, paint.Radius, middleZ, across, size);
                if (i0 > i1 || j0 > j1) continue;
                for (var cj = j0 * cells / size; cj <= j1 * cells / size; cj++)
                {
                    for (var ci = i0 * cells / size; ci <= i1 * cells / size; ci++)
                    {
                        var cell = cj * cells + ci;
                        (reach[cell] ?? (reach[cell] = new List<int>())).Add(n);
                    }
                }
            }

            var near = new List<GroundPaint>();
            for (var j = 0; j < size; j++)
            {
                var z = middleZ + ((j + 0.5f) / size - 0.5f) * across;
                var row = j * cells / size * cells;
                for (var i = 0; i < size; i++)
                {
                    var x = middleX + ((i + 0.5f) / size - 0.5f) * across;
                    near.Clear();
                    var those = reach[row + i * cells / size];
                    if (those != null)
                    {
                        foreach (var n in those)
                        {
                            var paint = paints[n];
                            if (Math.Abs(x - paint.X) <= paint.Radius && Math.Abs(z - paint.Z) <= paint.Radius) near.Add(paint);
                        }
                    }
                    into[j * size + i] = near.Count == 0 ? bare : Painted(bare, x, z, near);
                }
            }
        }

        /// <summary>The points along one side of the mask a paint may reach; from past to where it reaches none, kept within the mask before counting so a paint far off cannot overflow.</summary>
        private static (int From, int To) Span(float at, float radius, float middle, float across, int size)
        {
            var from = Math.Floor(((at - radius - middle) / (double)across + 0.5) * size - 0.5);
            var to = Math.Ceiling(((at + radius - middle) / (double)across + 0.5) * size - 0.5);
            return ((int)Math.Max(0, Math.Min(size, from)), (int)Math.Max(-1, Math.Min(size - 1, to)));
        }

        /// <summary>The biome whose ground it stands on: the first of its own players meet, a mod's after the game's; Meadows for none.</summary>
        public static string BiomeFor(IEnumerable<string> biomes)
        {
            string best = null;
            var bestRank = int.MaxValue;
            foreach (var biome in biomes ?? Array.Empty<string>())
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
