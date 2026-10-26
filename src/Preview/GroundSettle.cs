using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>
    /// What a place's copy brings to the ground under it, read while it sleeps, as stripping takes
    /// the game's scripts off: its paints, how its modifiers shape the ground, the parts the game
    /// lets settle, and its liquids.
    /// </summary>
    internal sealed class PlaceGround
    {
        public readonly List<GroundPaintAt> Paints = new List<GroundPaintAt>();
        public readonly List<GroundShapingAt> Shapings = new List<GroundShapingAt>();
        public readonly List<SettlingAt> Settling = new List<SettlingAt>();
        public readonly List<LiquidAt> Liquids = new List<LiquidAt>();
    }

    /// <summary>A modifier shaping the ground (<c>TerrainModifier</c>), read off the copy; where it stands is read as the ground is laid.</summary>
    internal sealed class GroundShapingAt
    {
        public Transform At;
        public bool Level, Smooth, Square;
        public float LevelRadius, LevelOffset, SmoothRadius, SmoothPower;
        public int Order;
        public GameObject Room;
    }

    /// <summary>A part the game lets settle (<c>StaticPhysics</c>, <c>Floating</c>): how it settles (<see cref="PartRest"/>), and where it stood in its parent as made.</summary>
    internal sealed class SettlingAt
    {
        public Transform At;
        public int How;
        public float FloatOffset;
        public Vector3 Made;
        public GameObject Room;
    }

    /// <summary>A place's liquid (<c>LiquidVolume</c>), read off the copy: its grid and what it starts with, the mesh it is drawn by, and the surface made for it.</summary>
    internal sealed class LiquidAt
    {
        public Transform At;
        public MeshFilter Filter;
        public int Width, Area;
        public float Scale, Volume, MaxDepth;
        public Mesh Made;
        public LiquidSettle Settled;
        public GameObject Room;
    }

    /// <summary>
    /// The ground a shown place stands on, as its own modifiers shape it (<see cref="GroundShape"/>),
    /// whatever the backdrop: the shape the Ground backdrop lays its ground by, the place's liquids
    /// settled over it and drawn as the game draws them (<see cref="LiquidSettle"/>), and its
    /// settling parts at rest on it (<see cref="PartRest"/>). Worked out again only as the
    /// modifiers move, as when the stage is resized or an example's rooms come in.
    /// </summary>
    internal sealed class GroundSettle
    {
        private readonly List<GroundShapingAt> _shapings = new List<GroundShapingAt>();
        private readonly List<SettlingAt> _settling = new List<SettlingAt>();
        private readonly List<LiquidAt> _liquids = new List<LiquidAt>();
        private readonly List<GroundShaping> _shaping = new List<GroundShaping>();
        private float _signature = float.NaN;
        private int _taken;
        private int _laidTaken = -1;

        /// <summary>The ground's shape now, from its middle (<see cref="Middle"/>).</summary>
        public GroundShape Shape { get; private set; } = GroundShape.Of(null);

        /// <summary>Where the shaped ground's middle stands: over the model's root, at the ground's flat height.</summary>
        public Vector3 Middle { get; private set; }

        /// <summary>Counts up each time the shape is worked out again, for the ground's mesh and grass to follow.</summary>
        public int Version { get; private set; }

        /// <summary>For the self-test: the liquids laid and their levels in the world, the parts at rest, and how many modifiers shape the ground.</summary>
        [ForTest]
        public IEnumerable<(Transform At, float Level, int Wet)> LiquidsLaid
        {
            get
            {
                foreach (var liquid in _liquids)
                {
                    if (liquid.At == null || liquid.Settled == null) continue;
                    var wet = 0;
                    foreach (var depth in liquid.Settled.Depths) if (depth >= 0.001f) wet++;
                    yield return (liquid.At, liquid.At.TransformPoint(0f, liquid.Settled.Level, 0f).y, wet);
                }
            }
        }

        [ForTest]
        public IEnumerable<(Transform At, int How)> Settled
        {
            get
            {
                foreach (var part in _settling) if (part.At != null) yield return (part.At, part.How);
            }
        }

        [ForTest]
        public int ShapingCount => _shaping.Count;

        /// <summary>Takes what a copy brings to its ground, once it stands; tied to an example's room where given.</summary>
        public void Take(PlaceGround ground, GameObject room)
        {
            if (ground == null) return;
            foreach (var s in ground.Shapings) s.Room = room;
            foreach (var s in ground.Settling) s.Room = room;
            foreach (var s in ground.Liquids) s.Room = room;
            _shapings.AddRange(ground.Shapings);
            _settling.AddRange(ground.Settling);
            _liquids.AddRange(ground.Liquids);
            _taken++;
        }

        /// <summary>Lets go of what the example's rooms brought, as its rooms go, or of all of it, the liquids' surfaces with it.</summary>
        public void Forget(bool rooms)
        {
            foreach (var liquid in _liquids)
            {
                if (rooms && liquid.Room == null) continue;
                if (liquid.Made != null) Object.Destroy(liquid.Made);
            }
            if (rooms)
            {
                _shapings.RemoveAll(s => s.Room != null);
                _settling.RemoveAll(s => s.Room != null);
                _liquids.RemoveAll(s => s.Room != null);
            }
            else
            {
                _shapings.Clear();
                _settling.Clear();
                _liquids.Clear();
            }
            _taken++;
        }

        /// <summary>
        /// Each frame, with where the ground's middle stands: works the shape out again where the
        /// modifiers moved or others came, then settles the liquids and rests the parts on it.
        /// </summary>
        public void Keep(Vector3 middle)
        {
            var signature = middle.x * 3.1f + middle.y * 7.3f + middle.z * 11.7f;
            foreach (var s in _shapings)
            {
                if (s.At == null) continue;
                var p = s.At.position;
                signature += p.x * 1.3f + p.y * 2.9f + p.z * 5.1f + s.At.lossyScale.x * 17.3f;
            }
            foreach (var liquid in _liquids)
            {
                if (liquid.At == null) continue;
                var p = liquid.At.position;
                signature += p.x * 0.7f + p.y * 1.9f + p.z * 3.7f + liquid.At.lossyScale.y * 13.1f;
            }
            if (_laidTaken == _taken && signature == _signature) return;
            _laidTaken = _taken;
            _signature = signature;
            Middle = middle;
            _shaping.Clear();
            foreach (var s in _shapings)
            {
                if (s.At == null) continue;
                var p = s.At.position;
                var scale = s.At.lossyScale;
                _shaping.Add(new GroundShaping
                {
                    X = p.x - middle.x, Y = p.y - middle.y, Z = p.z - middle.z, Level = s.Level, Smooth = s.Smooth, Square = s.Square,
                    LevelRadius = s.LevelRadius * scale.x, LevelOffset = s.LevelOffset * scale.y, SmoothRadius = s.SmoothRadius * scale.x,
                    SmoothPower = s.SmoothPower, Order = s.Order,
                });
            }
            Shape = GroundShape.Of(_shaping);
            foreach (var liquid in _liquids) Lay(liquid);
            foreach (var part in _settling) Rest(part);
            Version++;
        }

        /// <summary>The ground's height in the world under a point.</summary>
        private float GroundAt(Vector3 world) => Middle.y + Shape.HeightAt(world.x - Middle.x, world.z - Middle.z);

        /// <summary>Settles a liquid over the shaped ground and gives it a surface as the game makes its own (<c>LiquidVolume.RebuildMesh</c>).</summary>
        private void Lay(LiquidAt liquid)
        {
            if (liquid.At == null || liquid.Filter == null || liquid.Width <= 0) return;
            var at = liquid.At;
            liquid.Settled = LiquidSettle.Of(liquid.Width, liquid.Scale, liquid.Area, liquid.Volume, liquid.MaxDepth, (x, z) =>
            {
                var world = at.TransformPoint(x, 0f, z);
                return at.InverseTransformPoint(new Vector3(world.x, GroundAt(world), world.z)).y;
            });
            var side = liquid.Width + 1;
            var half = liquid.Width * liquid.Scale * 0.5f;
            var vertices = new Vector3[side * side];
            var colors = new Color[side * side];
            var uvs = new Vector2[side * side];
            for (var i = 0; i < side; i++)
            {
                for (var j = 0; j < side; j++)
                {
                    var k = i * side + j;
                    var x = j * liquid.Scale - half;
                    var z = i * liquid.Scale - half;
                    var depth = liquid.Settled.Depths[k];
                    var ground = liquid.Settled.Level - depth;
                    if (depth <= 0f)
                    {
                        var world = at.TransformPoint(x, 0f, z);
                        ground = at.InverseTransformPoint(new Vector3(world.x, GroundAt(world), world.z)).y;
                    }
                    var (height, wet) = LiquidSettle.Point(ground, depth);
                    vertices[k] = new Vector3(x, height, z);
                    colors[k] = new Color(1f, 1f, 1f, wet ? 1f : 0f);
                    uvs[k] = new Vector2((float)j / liquid.Width, (float)i / liquid.Width);
                }
            }
            var triangles = new int[(side - 1) * (side - 1) * 6];
            var t = 0;
            for (var i = 0; i < side - 1; i++)
            {
                for (var j = 0; j < side - 1; j++)
                {
                    var a = i * side + j;
                    triangles[t++] = a;
                    triangles[t++] = a + side;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + side;
                    triangles[t++] = a + side + 1;
                }
            }
            if (liquid.Made == null) liquid.Made = Kept.Add(new Mesh { name = "Scry stage liquid" });
            var mesh = liquid.Made;
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            liquid.Filter.sharedMesh = mesh;
        }

        /// <summary>Rests a part where the game would let it settle: from where it was made, on the ground under it or a liquid over it.</summary>
        private void Rest(SettlingAt part)
        {
            if (part.At == null) return;
            var parent = part.At.parent;
            var made = parent != null ? parent.TransformPoint(part.Made) : part.Made;
            var ground = GroundAt(made);
            var settled = PartRest.Settled(made.y, ground, LiquidOver(made), part.How, part.FloatOffset * part.At.lossyScale.y);
            part.At.position = new Vector3(made.x, settled, made.z);
        }

        /// <summary>The level in the world of a liquid lying over a point, if one does.</summary>
        private float? LiquidOver(Vector3 world)
        {
            foreach (var liquid in _liquids)
            {
                if (liquid.At == null || liquid.Settled == null || liquid.Scale <= 0f) continue;
                var local = liquid.At.InverseTransformPoint(world);
                var x = Mathf.FloorToInt(local.x / liquid.Scale + 0.5f);
                var z = Mathf.FloorToInt(local.z / liquid.Scale + 0.5f);
                if (liquid.Settled.DepthAt(x, z) < 0.001f) continue;
                return liquid.At.TransformPoint(0f, liquid.Settled.Level, 0f).y;
            }
            return null;
        }
    }
}
