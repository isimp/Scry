using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A place's terrain modifier as it shapes the ground (<c>TerrainModifier</c>): where it stands
    /// from the ground's middle and how high above the ground, whether it levels and how far, by how
    /// much its height is moved, whether it smooths and how far and how steeply, whether its level
    /// takes a square, and the order the game sorts it by.
    /// </summary>
    internal sealed class GroundShaping
    {
        public float X, Y, Z;
        public bool Level, Smooth, Square;
        public float LevelRadius, LevelOffset, SmoothRadius;
        public float SmoothPower = 3f;
        public int Order;

        /// <summary>The height it shapes the ground toward: its own, moved by its offset.</summary>
        public float Target => Y + LevelOffset;

        /// <summary>Whether it shapes the ground at all, rather than only painting it.</summary>
        public bool Shapes => (Level && LevelRadius > 0f) || (Smooth && SmoothRadius > 0f);
    }

    /// <summary>
    /// The ground a place's modifiers shape, as the game shapes its terrain under them
    /// (<c>Heightmap.ApplyModifier</c>): flat ground first, then each modifier in the order the game
    /// sorts them (by its order, those of one order as read), on the terrain's one-metre grid, each
    /// put on its nearest point (<c>Heightmap.WorldToVertex</c>). A level sets every point within
    /// its radius, or its square, to its height (<c>LevelTerrain</c>); a smooth pulls every point
    /// within its radius toward it by one less its distance's share of the radius to its power
    /// (<c>SmoothTerrain2</c>). Between the grid's points the ground runs straight. Heights are
    /// from the flat ground the place stands on, which the stage has in place of the world's.
    /// </summary>
    internal sealed class GroundShape
    {
        /// <summary>The terrain's grid, in metres (<c>Heightmap.m_scale</c>).</summary>
        public const float Grid = 1f;

        private readonly float[] _heights;
        private readonly int _half;

        private GroundShape(float[] heights, int half)
        {
            _heights = heights;
            _half = half;
        }

        /// <summary>Whether no modifier shapes the ground: it lies flat.</summary>
        public bool Flat => _heights == null;

        /// <summary>How far from the middle the shaped ground reaches, in metres, along either axis; past it the ground is flat.</summary>
        public float Reach => _half * Grid;

        private static readonly GroundShape None = new GroundShape(null, 0);

        /// <summary>The ground the modifiers given shape; flat where none shapes it.</summary>
        public static GroundShape Of(IEnumerable<GroundShaping> shapings)
        {
            var shaping = (shapings ?? Enumerable.Empty<GroundShaping>()).Where(s => s != null && s.Shapes).OrderBy(s => s.Order).ToList();
            if (shaping.Count == 0) return None;
            var half = 1;
            foreach (var s in shaping)
            {
                var radius = Math.Max(s.Level ? s.LevelRadius : 0f, s.Smooth ? s.SmoothRadius : 0f);
                var far = Math.Max(Math.Abs(Vertex(s.X)), Math.Abs(Vertex(s.Z))) + (int)Math.Ceiling(radius / Grid) + 1;
                half = Math.Max(half, far);
            }
            var side = half * 2 + 1;
            var heights = new float[side * side];
            foreach (var s in shaping)
            {
                var cx = Vertex(s.X) + half;
                var cz = Vertex(s.Z) + half;
                if (s.Level && s.LevelRadius > 0f)
                {
                    var r = s.LevelRadius / Grid;
                    var n = (int)Math.Ceiling(r);
                    for (var i = cz - n; i <= cz + n; i++)
                    {
                        for (var j = cx - n; j <= cx + n; j++)
                        {
                            if (i < 0 || j < 0 || i >= side || j >= side) continue;
                            if (s.Square || Distance(j - cx, i - cz) <= r) heights[i * side + j] = s.Target;
                        }
                    }
                }
                if (s.Smooth && s.SmoothRadius > 0f)
                {
                    var r = s.SmoothRadius / Grid;
                    var n = (int)Math.Ceiling(r);
                    for (var i = cz - n; i <= cz + n; i++)
                    {
                        for (var j = cx - n; j <= cx + n; j++)
                        {
                            if (i < 0 || j < 0 || i >= side || j >= side) continue;
                            var d = Distance(j - cx, i - cz);
                            if (d > r) continue;
                            var share = d / r;
                            var weight = s.SmoothPower == 3f ? share * share * share : (float)Math.Pow(share, s.SmoothPower);
                            var h = heights[i * side + j];
                            heights[i * side + j] = h + (s.Target - h) * (1f - weight);
                        }
                    }
                }
            }
            return new GroundShape(heights, half);
        }

        /// <summary>The grid point nearest a position along one axis, as the game rounds it.</summary>
        private static int Vertex(float at) => (int)Math.Floor(at / Grid + 0.5f);

        private static float Distance(int x, int z) => (float)Math.Sqrt(x * x + z * z);

        /// <summary>The ground's height at a point, from the middle, straight between the grid's points; flat past its reach.</summary>
        public float HeightAt(float x, float z)
        {
            if (_heights == null) return 0f;
            var side = _half * 2 + 1;
            var gx = x / Grid + _half;
            var gz = z / Grid + _half;
            if (gx < 0f || gz < 0f || gx > side - 1 || gz > side - 1) return 0f;
            var j = Math.Min((int)Math.Floor(gx), side - 2);
            var i = Math.Min((int)Math.Floor(gz), side - 2);
            var fx = gx - j;
            var fz = gz - i;
            var a = _heights[i * side + j] * (1f - fx) + _heights[i * side + j + 1] * fx;
            var b = _heights[(i + 1) * side + j] * (1f - fx) + _heights[(i + 1) * side + j + 1] * fx;
            return a * (1f - fz) + b * fz;
        }

        /// <summary>How far the ground leans from level at a point, in degrees.</summary>
        public float TiltAt(float x, float z)
        {
            var (_, up, _) = NormalAt(x, z);
            return (float)(Math.Acos(Math.Min(1f, up)) * 180.0 / Math.PI);
        }

        /// <summary>The way the ground faces at a point, a length of one: straight up where it is level.</summary>
        public (float X, float Y, float Z) NormalAt(float x, float z)
        {
            if (_heights == null) return (0f, 1f, 0f);
            const float step = 0.5f;
            var dx = (HeightAt(x + step, z) - HeightAt(x - step, z)) / (2f * step);
            var dz = (HeightAt(x, z + step) - HeightAt(x, z - step)) / (2f * step);
            var length = (float)Math.Sqrt(dx * dx + 1f + dz * dz);
            return (-dx / length, 1f / length, -dz / length);
        }
    }

    /// <summary>
    /// A place's liquid as it settles (<c>LiquidVolume</c>): the game starts it as a block over the
    /// middle of its grid, each point of the block as deep as its volume over its area squared
    /// (<c>InitializeLevels</c>, both ends of the block kept), and lets it flow until it lies level.
    /// Settled, it fills the ground's lowest reaches joined to where it started, up to the level
    /// its volume reaches; each point is as deep as that level stands over the ground there.
    /// </summary>
    internal sealed class LiquidSettle
    {
        private readonly int _width;

        /// <summary>The height it lies at, from the ground's flat height.</summary>
        public float Level { get; private set; }

        /// <summary>All of it, as the depths of its grid's points add up.</summary>
        public float Volume { get; private set; }

        /// <summary>Each point's depth, row by row along its grid.</summary>
        public float[] Depths { get; private set; }

        private LiquidSettle(int width) => _width = width;

        /// <summary>The depth at a point of its grid, counted from its middle.</summary>
        public float DepthAt(int x, int z)
        {
            var side = _width + 1;
            var j = x + _width / 2;
            var i = z + _width / 2;
            return i < 0 || j < 0 || i >= side || j >= side ? 0f : Depths[i * side + j];
        }

        /// <summary>
        /// Settles a liquid: its grid's width in points and metres a point, the area and volume it
        /// starts with, how far it reaches up and down (<c>m_maxDepth</c>, past which the game holds
        /// the ground's height), over the ground at each point from its middle.
        /// </summary>
        public static LiquidSettle Of(int width, float scale, int area, float volume, float maxDepth, Func<float, float, float> groundAt)
        {
            var side = width + 1;
            var ground = new float[side * side];
            for (var i = 0; i < side; i++)
            {
                for (var j = 0; j < side; j++)
                {
                    var h = groundAt((j - width / 2) * scale, (i - width / 2) * scale);
                    ground[i * side + j] = Math.Max(-maxDepth, Math.Min(maxDepth, h));
                }
            }
            var middle = width / 2;
            var starts = new List<int>();
            for (var i = middle - area / 2; i <= middle + area / 2; i++)
            {
                for (var j = middle - area / 2; j <= middle + area / 2; j++)
                {
                    if (i >= 0 && j >= 0 && i < side && j < side) starts.Add(i * side + j);
                }
            }
            var each = area > 0 ? volume / (area * area) : 0f;
            var settled = new LiquidSettle(width) { Volume = each * starts.Count, Depths = new float[side * side] };
            if (starts.Count == 0 || settled.Volume <= 0f)
            {
                settled.Level = starts.Count > 0 ? starts.Min(s => ground[s]) : 0f;
                return settled;
            }

            var low = starts.Min(s => ground[s]);
            var high = ground.Max() + settled.Volume + 1f;
            var region = new bool[side * side];
            for (var round = 0; round < 60; round++)
            {
                var level = (low + high) / 2f;
                if (Filled(ground, side, starts, level, region) > settled.Volume) high = level;
                else low = level;
            }
            settled.Level = low;
            Filled(ground, side, starts, low, region);
            for (var k = 0; k < region.Length; k++) settled.Depths[k] = region[k] ? Math.Max(0f, low - ground[k]) : 0f;
            return settled;
        }

        /// <summary>How much it holds lying at a level: the points under it joined to where it started, marked in <paramref name="region"/>.</summary>
        private static float Filled(float[] ground, int side, List<int> starts, float level, bool[] region)
        {
            Array.Clear(region, 0, region.Length);
            var open = new Stack<int>();
            foreach (var s in starts)
            {
                if (ground[s] >= level || region[s]) continue;
                region[s] = true;
                open.Push(s);
            }
            var held = 0f;
            while (open.Count > 0)
            {
                var k = open.Pop();
                held += level - ground[k];
                var i = k / side;
                var j = k % side;
                if (j > 0) Spread(k - 1);
                if (j < side - 1) Spread(k + 1);
                if (i > 0) Spread(k - side);
                if (i < side - 1) Spread(k + side);
            }
            return held;

            void Spread(int next)
            {
                if (region[next] || ground[next] >= level) return;
                region[next] = true;
                open.Push(next);
            }
        }

        /// <summary>A point of its surface as the game lays it (<c>LiquidVolume.CalcVertex</c>): under the liquid at least a tenth of a metre over its ground and seen, dry on the ground and unseen.</summary>
        public static (float Height, bool Wet) Point(float ground, float depth) =>
            depth > 0f ? (ground + Math.Max(0.1f, depth), depth >= 0.001f) : (ground, false);
    }

    /// <summary>
    /// Where a part the game lets settle comes to rest on the shaped ground, as it would in the
    /// world: a falling one (<c>StaticPhysics.m_fall</c>) drops to the ground under it, or keeps its
    /// height where it falls onto solids (<c>m_checkSolids</c>), as the stage has none of the
    /// world's; one pushed up (<c>m_pushUp</c>) rises out of the ground, each by the game's
    /// twentieth of a metre; a floating one (<c>Floating</c>) rides a liquid's level where the
    /// liquid lies over the ground, by its own offset (<c>m_waterLevelOffset</c>).
    /// </summary>
    internal static class PartRest
    {
        public const int Fall = 1, PushUp = 2, Solids = 4, Floats = 8;

        /// <summary>How far past the ground a part may stand before the game moves it.</summary>
        private const float Slack = 0.05f;

        /// <summary>The height a part rests at, from its own, the ground's under it and the liquid's there if any.</summary>
        public static float Settled(float y, float ground, float? liquid, int how, float floatOffset = 0f)
        {
            if ((how & Floats) != 0) return liquid is float level && level > ground ? level + floatOffset : y;
            if ((how & Fall) != 0)
            {
                var fallTo = (how & Solids) != 0 ? y : ground;
                if (y > fallTo + Slack) y = fallTo;
            }
            if ((how & PushUp) != 0 && y < ground - Slack) y = ground;
            return y;
        }
    }
}
