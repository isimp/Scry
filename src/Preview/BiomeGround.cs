using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>A place's paint on its ground, read off its copy while it sleeps, as stripping takes it off; the example's room it is in, if any.</summary>
    internal sealed class GroundPaintAt
    {
        public Transform At;
        public float Radius, Strength;
        public TerrainModifier.PaintType Type;
        public int Order;
        public GameObject Room;
    }

    /// <summary>
    /// The stage's Ground backdrop: a round piece of ground under the model, its biome's
    /// (<see cref="StageGround"/>), drawn with the world's own terrain material as the game draws
    /// its terrain: the biome told by each point's colour (<c>Heightmap.GetBiomeColor</c>), nothing
    /// painted on it (<c>Heightmap.m_paintMaskNothing</c>) but the share the world keeps in its
    /// mask, a distance to hide it by as the game gives each piece of its terrain, and, for the
    /// sea's floor, water over it (<c>_WaterLevel</c>).
    ///
    /// It lies several times as wide as what is framed and far out toward a horizon with the sky,
    /// fading at its edge into what is behind it. A place paints its own paths, dirt and paving
    /// on it as the game paints its terrain under it (<c>TerrainModifier</c>,
    /// <c>Heightmap.PaintCleared</c>). The biome's grass and small plants grow on it within forty
    /// metres, scattered by the game's own rules for them (<c>ClutterSystem</c>), and the sea's
    /// floor has the world's water over it.
    ///
    /// It keeps everything it makes and lets go of it itself (<see cref="Forget"/>); the stage
    /// says each frame whether it is wanted, of which biome, and where.
    /// </summary>
    internal sealed class BiomeGround
    {
        /// <summary>How far under the floor the ground lies, as the terrain's shader raises its bumps (<c>_Displacement</c>).</summary>
        public const float Below = 0.05f;

        /// <summary>How far from the middle grass grows, as far as the game grows it around you (<c>ClutterSystem.m_distance</c>).</summary>
        private const float GrassReach = 40f;

        /// <summary>The most plants made as copies of their own, rather than drawn many times over.</summary>
        private const int MostGrassCopies = 300;

        private GameObject _terrain;
        private Material _material;
        private Mesh _mesh;
        private Texture2D _mask;
        private int _triedAt = -1000;
        private float? _hide = 100000f;
        private bool _hideSet;
        private MaterialPropertyBlock _block;

        /// <summary>The height of the water the terrain's shader is told of (<c>_WaterLevel</c>) over the sea's floor, as last told; not a number for none.</summary>
        private float _waterAt = float.NaN;

        /// <summary>How the ground was last laid: for which entry and biome, with the sky or not, how far across, for what it framed and where.</summary>
        private Entry _for;
        private string _laidBiome;
        private bool _sky;
        private float _across = 24f;
        private float _basis;
        private Vector3 _at;
        private int _paintsLaid = -1;

        private readonly List<GroundPaintAt> _paints = new List<GroundPaintAt>();
        private int _paintsVersion;

        /// <summary>Whether a biome's ground is laid now.</summary>
        public bool On { get; private set; }

        /// <summary>The biome the ground is painted for; null before it is.</summary>
        public string Biome { get; private set; }

        /// <summary>
        /// The distance the terrain's shader is given to hide it by, as the game gives each piece
        /// of its terrain one of its own (<c>Heightmap.ApplySettings</c>, <c>_LodHideDistance</c>):
        /// far, so all of the stage's ground stands within it; null gives none, leaving the
        /// material's own. Settable for the self-test to try.
        /// </summary>
        public float? HideDistance
        {
            get => _hide;
            set
            {
                _hide = value;
                _hideSet = false;
            }
        }

        /// <summary>The biome whose ground is laid now, or null where none is, for the self-test.</summary>
        public string Shown => _terrain != null && _terrain.activeSelf ? Biome : null;

        /// <summary>How many paints the ground has, how many grass stems and plants grow on it, and whether water lies over it, for the self-test.</summary>
        public int PaintCount => On ? _paints.Count : 0;

        public int GrassCount
        {
            get
            {
                var count = _grassCopies.Count;
                foreach (var kind in _grass) count += kind.Count;
                return count;
            }
        }

        public bool WaterShown => _water != null && _water.activeSelf;

        /// <summary>
        /// Each frame: lays the ground or puts it away as it is wanted, of the biome given, making
        /// it once the world's terrain has a material to borrow. True when it was laid or put away
        /// or took another biome, for the light and sky to follow.
        /// </summary>
        public bool Keep(bool wanted, string biome, GameObject root, int layer)
        {
            var ground = wanted && EnsureTerrain(root, layer);
            if (_terrain != null && _terrain.activeSelf != ground) _terrain.SetActive(ground);
            var changed = ground != On;
            On = ground;
            if (ground)
            {
                if (biome != Biome)
                {
                    Paint(biome);
                    changed = true;
                }
                if (!_hideSet) ApplyBlock();
            }
            return changed;
        }

        /// <summary>Puts everything away for the frame after a fault in laying it, so the stage is filmed all the same.</summary>
        public void PutAway()
        {
            On = false;
            if (_terrain != null) _terrain.SetActive(false);
            if (_fade != null) _fade.SetActive(false);
            if (_water != null) _water.SetActive(false);
        }

        /// <summary>
        /// Lays the ground under the model at its height: again for another entry, biome, sky or
        /// paint, or as what is framed grows; its grass goes up and down with it. Its edge fades
        /// into the colour given, what is behind it.
        /// </summary>
        public void Lay(float groundY, Entry shown, bool sky, float frameRadius, Vector3 origin, Color edge, GameObject root, int layer)
        {
            var on = On && _terrain != null && _terrain.activeSelf;
            if (_fade != null && _fade.activeSelf != on) _fade.SetActive(on);
            if (!on)
            {
                if (_water != null && _water.activeSelf) _water.SetActive(false);
                if (_grass.Count > 0 || _grassCopies.Count > 0) ForgetGrass();
                _for = null;
                return;
            }

            var basis = Mathf.Max(0.5f, frameRadius);
            var at = new Vector3(origin.x, groundY - Below, origin.z);
            var another = !ReferenceEquals(_for, shown) || _laidBiome != Biome || _sky != sky || _paintsLaid != _paintsVersion;
            if (another || basis > _basis * 1.25f)
            {
                _basis = another ? basis : Mathf.Max(basis, _basis);
                _for = shown;
                _laidBiome = Biome;
                _sky = sky;
                _paintsLaid = _paintsVersion;
                _across = StageGround.Across(_basis, sky);
                _at = at;
                PaintMask();
                ScatterGrass(root, layer);
            }
            else if (Mathf.Abs(at.y - _at.y) > 0.005f)
            {
                LiftGrass(at.y - _at.y);
                _at = at;
            }

            PlaceAt(_at, _across);
            PlaceWater(root, layer);
            PlaceFade(edge, root, layer);
        }

        /// <summary>Makes the piece of ground once the world's terrain has a material to borrow, trying again a second apart.</summary>
        private bool EnsureTerrain(GameObject root, int layer)
        {
            if (_terrain != null) return true;
            if (root == null || Time.frameCount - _triedAt < 60) return false;
            _triedAt = Time.frameCount;
            var source = WorldTerrainMaterial();
            if (source == null) return false;

            _material = new Material(source) { name = "Scry stage ground" };
            _mask = Kept.Add(new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Scry stage ground mask", wrapMode = TextureWrapMode.Clamp });
            _material.SetTexture("_ClearedMaskTex", _mask);
            _mesh = Kept.Add(Disc());

            _terrain = new GameObject("Scry stage ground") { layer = layer };
            _terrain.transform.SetParent(root.transform, false);
            _terrain.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = _terrain.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            Biome = null;
            _hideSet = false;
            _terrain.SetActive(false);
            return true;
        }

        /// <summary>The material the world's terrain is drawn with, from the piece under you, else any near piece.</summary>
        private static Material WorldTerrainMaterial()
        {
            var player = Player.m_localPlayer;
            var map = player != null ? Heightmap.FindHeightmap(player.transform.position) : null;
            if (map == null || map.IsDistantLod)
            {
                foreach (var any in Object.FindObjectsByType<Heightmap>(FindObjectsSortMode.None))
                {
                    if (any == null || any.IsDistantLod || any.m_material == null) continue;
                    map = any;
                    break;
                }
            }
            return map != null && !map.IsDistantLod ? map.m_material : null;
        }

        /// <summary>
        /// Gives the ground a biome: each point's colour as the game gives its terrain's, and
        /// water over the sea's floor. Its mask is painted as it is laid for the biome
        /// (<see cref="PaintMask"/>), at whatever size a place's paints have made it.
        /// </summary>
        private void Paint(string biome)
        {
            var color = Enum.TryParse<Heightmap.Biome>(biome, out var parsed) ? Heightmap.GetBiomeColor(parsed) : new Color32(0, 0, 0, 0);
            var colors = new Color32[_mesh.vertexCount];
            for (var i = 0; i < colors.Length; i++) colors[i] = color;
            _mesh.colors32 = colors;
            Biome = biome;
            _waterAt = float.NaN;
            _hideSet = false;
        }

        /// <summary>Lays the ground at its height, with water over it for the sea's floor: told again only as the ground moves.</summary>
        private void PlaceAt(Vector3 at, float across)
        {
            _terrain.transform.position = at;
            _terrain.transform.localScale = new Vector3(across, 1f, across);
            var water = StageGround.WaterOver(Biome) is float over ? at.y + over : float.NaN;
            if (float.IsNaN(water) == float.IsNaN(_waterAt) && (float.IsNaN(water) || Mathf.Abs(water - _waterAt) < 0.1f)) return;
            _waterAt = water;
            _hideSet = false;
            ApplyBlock();
        }

        /// <summary>What the stage tells the terrain's shader of its piece alone: the distance to hide it by, and the water over it.</summary>
        private void ApplyBlock()
        {
            var renderer = _terrain.GetComponent<MeshRenderer>();
            if (_block == null) _block = new MaterialPropertyBlock();
            _block.Clear();
            if (_hide is float distance) _block.SetFloat("_LodHideDistance", distance);
            if (!float.IsNaN(_waterAt)) _block.SetFloat("_WaterLevel", _waterAt);
            renderer.SetPropertyBlock(_hide != null || !float.IsNaN(_waterAt) ? _block : null);
            _hideSet = true;
        }

        /// <summary>The value the terrain's material holds for each of its properties, without the stage's, and how it is drawn, for the self-test to tell.</summary>
        [Diagnostic]
        public string MaterialTold(Camera camera, Func<Bounds, bool> inView)
        {
            if (_material == null) return "no material";
            var shader = _material.shader;
            var names = new List<string>();
            for (var i = 0; shader != null && i < shader.GetPropertyCount(); i++)
            {
                var name = shader.GetPropertyName(i);
                var type = shader.GetPropertyType(i);
                names.Add(type == ShaderPropertyType.Float || type == ShaderPropertyType.Range
                    ? $"{name} {Numbers.Amount(_material.GetFloat(name), 3)}" : $"{name} ({type})");
            }
            var renderer = _terrain != null ? _terrain.GetComponent<MeshRenderer>() : null;
            var drawn = renderer == null ? "no renderer" : $"active {_terrain.activeInHierarchy}, layer {Numbers.Count(_terrain.layer)}, bounds {Figures.Point(renderer.bounds.center)} size {Figures.Point(renderer.bounds.size)}, in view {inView(renderer.bounds)}, drawn by a camera {renderer.isVisible}";
            var path = camera != null ? $"{camera.renderingPath} ({camera.actualRenderingPath})" : "no camera";
            return $"{drawn}; the stage camera renders {path}; shader {shader.OrNull()?.name}, passes {Numbers.Count(_material.passCount)}, queue {Numbers.Count(_material.renderQueue)}, keywords [{string.Join(", ", _material.shaderKeywords)}], properties: {string.Join(", ", names)}";
        }

        /// <summary>A flat round piece a metre across, in rings, facing up, its texture's corners at its square's.</summary>
        private static Mesh Disc()
        {
            const int rings = 8;
            const int around = 48;
            var vertices = new Vector3[1 + rings * around];
            var uvs = new Vector2[vertices.Length];
            var normals = new Vector3[vertices.Length];
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (var r = 1; r <= rings; r++)
            {
                for (var a = 0; a < around; a++)
                {
                    var angle = a * Mathf.PI * 2f / around;
                    var point = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (0.5f * r / rings);
                    var i = 1 + (r - 1) * around + a;
                    vertices[i] = point;
                    uvs[i] = new Vector2(point.x + 0.5f, point.z + 0.5f);
                }
            }
            for (var i = 0; i < normals.Length; i++) normals[i] = Vector3.up;

            var triangles = new List<int>();
            for (var a = 0; a < around; a++)
            {
                triangles.Add(0);
                triangles.Add(1 + (a + 1) % around);
                triangles.Add(1 + a);
            }
            for (var r = 1; r < rings; r++)
            {
                for (var a = 0; a < around; a++)
                {
                    var inner = 1 + (r - 1) * around;
                    var outer = 1 + r * around;
                    var next = (a + 1) % around;
                    triangles.Add(inner + a);
                    triangles.Add(inner + next);
                    triangles.Add(outer + a);
                    triangles.Add(inner + next);
                    triangles.Add(outer + next);
                    triangles.Add(outer + a);
                }
            }
            var mesh = new Mesh { name = "Scry stage ground" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ----- Paint -----

        /// <summary>Takes a copy's paints on its ground, once it stands.</summary>
        public void TakePaints(List<GroundPaintAt> paints, GameObject room)
        {
            if (paints == null || paints.Count == 0) return;
            foreach (var paint in paints) paint.Room = room;
            _paints.AddRange(paints);
            _paintsVersion++;
        }

        /// <summary>Lets go of the paints of the example's rooms, as its rooms go, or of all of them.</summary>
        public void ForgetPaints(bool rooms)
        {
            var before = _paints.Count;
            if (rooms) _paints.RemoveAll(paint => paint.Room != null);
            else _paints.Clear();
            if (_paints.Count != before) _paintsVersion++;
        }

        /// <summary>
        /// The ground's paint mask: as the world leaves it under the biome, and under a place
        /// painted by its paints in their order (<see cref="StageGround.Painted"/>), half a metre
        /// a point where the ground is small, coarser where it is wide.
        /// </summary>
        private void PaintMask()
        {
            var ground = Heightmap.m_paintMaskNothing;
            ground.a = StageGround.MaskShare(Biome);
            var paints = PaintsNow();
            var size = StageGround.MaskSize(paints.Count, _across);
            if (_mask.width != size) _mask.Reinitialize(size, size);
            var pixels = new Color[size * size];
            var bare = (ground.r, ground.g, ground.b, ground.a);
            var near = new List<GroundPaint>();
            for (var j = 0; j < size; j++)
            {
                var z = _at.z + ((j + 0.5f) / size - 0.5f) * _across;
                for (var i = 0; i < size; i++)
                {
                    var x = _at.x + ((i + 0.5f) / size - 0.5f) * _across;
                    near.Clear();
                    foreach (var paint in paints) if (Mathf.Abs(x - paint.X) <= paint.Radius && Mathf.Abs(z - paint.Z) <= paint.Radius) near.Add(paint);
                    var (r, g, b, a) = near.Count == 0 ? bare : StageGround.Painted(bare, x, z, near);
                    pixels[j * size + i] = new Color(r, g, b, a);
                }
            }
            _mask.SetPixels(pixels);
            _mask.Apply(false);
        }

        /// <summary>The paints as they stand now, in the order the game applies them.</summary>
        private List<GroundPaint> PaintsNow()
        {
            var sorted = new List<GroundPaintAt>();
            foreach (var paint in _paints) if (paint.At != null) sorted.Add(paint);
            // Stable, as the game keeps the order of those it sorts alike.
            var ordered = new List<GroundPaintAt>(sorted.Count);
            foreach (var order in DistinctOrders(sorted)) foreach (var paint in sorted) if (paint.Order == order) ordered.Add(paint);
            var paints = new List<GroundPaint>(ordered.Count);
            foreach (var paint in ordered)
            {
                var color = PaintColor(paint.Type);
                paints.Add(new GroundPaint
                {
                    X = paint.At.position.x, Z = paint.At.position.z, Radius = paint.Radius * paint.At.lossyScale.x, Strength = paint.Strength,
                    Color = (color.r, color.g, color.b, color.a), ClearsVegetation = paint.Type == TerrainModifier.PaintType.ClearVegetation,
                });
            }
            return paints;
        }

        private static List<int> DistinctOrders(List<GroundPaintAt> paints)
        {
            var orders = new List<int>();
            foreach (var paint in paints) if (!orders.Contains(paint.Order)) orders.Add(paint.Order);
            orders.Sort();
            return orders;
        }

        /// <summary>The colour a paint lays on the mask, as the game's terrain has it.</summary>
        private static Color PaintColor(TerrainModifier.PaintType type)
        {
            switch (type)
            {
                case TerrainModifier.PaintType.Dirt: return Heightmap.m_paintMaskDirt;
                case TerrainModifier.PaintType.Cultivate: return Heightmap.m_paintMaskCultivated;
                case TerrainModifier.PaintType.Paved: return Heightmap.m_paintMaskPaved;
                case TerrainModifier.PaintType.ClearVegetation: return Heightmap.m_paintMaskClearVegetation;
                case TerrainModifier.PaintType.DeepSnow: return Heightmap.m_paintMaskDeepSnow;
                default: return Heightmap.m_paintMaskNothing;
            }
        }

        // ----- Grass -----

        /// <summary>A kind of grass drawn many times over (<c>InstanceRenderer</c>): its mesh and material, whether it casts shadows, and where each stands.</summary>
        private sealed class GrassKind
        {
            public Mesh Mesh;
            public Material Material;
            public ShadowCastingMode Shadows;
            public readonly List<Matrix4x4[]> Batches = new List<Matrix4x4[]>();
            public readonly List<int> Counts = new List<int>();
            public int Count;
        }

        private readonly List<GrassKind> _grass = new List<GrassKind>();
        private readonly List<GameObject> _grassCopies = new List<GameObject>();

        /// <summary>
        /// Scatters the biome's grass and small plants over the ground within forty metres, as the
        /// game scatters them around you (<c>ClutterSystem.GenerateVegPatch</c>): patch by patch,
        /// each kind for the biome with its own dice for the patch, its places kept by its forest,
        /// noise, height, slope, sea depth, vegetation and cleared ground rules, the ground taken
        /// at a height typical of the biome (<see cref="StageGround.Altitude"/>).
        /// </summary>
        private void ScatterGrass(GameObject root, int layer)
        {
            ForgetGrass();
            var system = ClutterSystem.instance;
            if (system == null || root == null || !Enum.TryParse<Heightmap.Biome>(Biome, out var biome)) return;
            var paints = PaintsNow();
            var ground = Heightmap.m_paintMaskNothing;
            ground.a = StageGround.MaskShare(Biome);
            var bare = (ground.r, ground.g, ground.b, ground.a);
            var altitude = StageGround.Altitude(Biome);
            var depth = StageGround.WaterOver(Biome) ?? 0f;
            var size = Mathf.Max(1f, system.m_grassPatchSize);
            var half = size / 2f;
            var reach = Mathf.Min(_across / 2f, GrassReach);
            var patches = Mathf.CeilToInt(reach / size);
            var kinds = new Dictionary<GameObject, GrassKind>();
            var state = UnityEngine.Random.state;
            try
            {
                for (var px = -patches; px <= patches; px++)
                {
                    for (var pz = -patches; pz <= patches; pz++)
                    {
                        var cx = _at.x + px * size;
                        var cz = _at.z + pz * size;
                        if (new Vector2(cx - _at.x, cz - _at.z).magnitude > reach) continue;
                        for (var i = 0; i < system.m_clutter.Count; i++)
                        {
                            var clutter = system.m_clutter[i];
                            if (clutter == null || !clutter.m_enabled || clutter.m_prefab == null || (clutter.m_biome & biome) == 0) continue;
                            UnityEngine.Random.InitState(px * (pz * 1374) + i * 9321);
                            var offset = new Vector3(clutter.m_fractalOffset, 0f, 0f);
                            var amount = (int)(clutter.m_amount * system.m_amountScale);
                            for (var j = 0; j < amount; j++)
                            {
                                var point = new Vector3(UnityEngine.Random.Range(cx - half, cx + half), 0f, UnityEngine.Random.Range(cz - half, cz + half));
                                float yaw = UnityEngine.Random.Range(0, 360);
                                if (!Grows(clutter, point, offset, altitude, depth, bare, paints)) continue;
                                point.y = clutter.m_snapToWater ? _at.y + depth : _at.y + Below;
                                if (clutter.m_randomOffset != 0f) point.y += UnityEngine.Random.Range(0f - clutter.m_randomOffset, clutter.m_randomOffset);
                                var turn = Quaternion.Euler(0f, yaw, 0f);
                                if (clutter.m_instanced) AddGrass(kinds, clutter, point, turn);
                                else if (_grassCopies.Count < MostGrassCopies)
                                {
                                    var copy = Ghost.Make(clutter.m_prefab, root.transform, point, turn, layer);
                                    if (copy != null) _grassCopies.Add(copy);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Random.state = state;
            }
            _grass.AddRange(kinds.Values);
        }

        /// <summary>Whether a kind of grass grows at a point of the ground, by the game's rules for it.</summary>
        private static bool Grows(ClutterSystem.Clutter clutter, Vector3 point, Vector3 offset, float altitude, float depth, (float, float, float, float) bare, List<GroundPaint> paints)
        {
            if (clutter.m_inForest)
            {
                var forest = WorldGenerator.GetForestFactor(point);
                if (forest < clutter.m_forestTresholdMin || forest > clutter.m_forestTresholdMax) return false;
            }
            if (clutter.m_fractalScale > 0f)
            {
                var noise = Utils.Fbm(point * 0.01f * clutter.m_fractalScale + offset, 3, 1.6f, 0.7f);
                if (noise < clutter.m_fractalTresholdMin || noise > clutter.m_fractalTresholdMax) return false;
            }
            // The ground is flat: what grows only on a slope does not.
            if (altitude < clutter.m_minAlt || altitude > clutter.m_maxAlt || clutter.m_minTilt > 0f) return false;
            if (clutter.m_minOceanDepth != clutter.m_maxOceanDepth && (depth < clutter.m_minOceanDepth || depth > clutter.m_maxOceanDepth)) return false;
            var mask = StageGround.Painted(bare, point.x, point.z, paints);
            if (clutter.m_minVegetation != clutter.m_maxVegetation && (mask.A > clutter.m_maxVegetation || mask.A < clutter.m_minVegetation)) return false;
            if (!clutter.m_onCleared || !clutter.m_onUncleared)
            {
                var cleared = mask.R > 0.5f || mask.G > 0.5f || mask.B > 0.5f;
                if ((clutter.m_onCleared && !cleared) || (clutter.m_onUncleared && cleared)) return false;
            }
            return true;
        }

        /// <summary>One stem of a kind drawn many times over, at its size, as the game adds it (<c>InstanceRenderer.AddInstance</c>).</summary>
        private static void AddGrass(Dictionary<GameObject, GrassKind> kinds, ClutterSystem.Clutter clutter, Vector3 point, Quaternion turn)
        {
            var scale = UnityEngine.Random.Range(clutter.m_scaleMin, clutter.m_scaleMax);
            if (!kinds.TryGetValue(clutter.m_prefab, out var kind))
            {
                var renderer = clutter.m_prefab.GetComponent<InstanceRenderer>();
                kind = new GrassKind();
                if (renderer != null && renderer.m_mesh != null && renderer.m_material != null)
                {
                    kind.Mesh = renderer.m_mesh;
                    kind.Material = renderer.m_material;
                    kind.Shadows = renderer.m_shadowCasting;
                }
                kinds[clutter.m_prefab] = kind;
            }
            if (kind.Mesh == null) return;
            var instance = Matrix4x4.TRS(point, turn, clutter.m_prefab.GetComponent<InstanceRenderer>().m_scale * scale);
            if (kind.Batches.Count == 0 || kind.Counts[kind.Counts.Count - 1] == 1023)
            {
                kind.Batches.Add(new Matrix4x4[1023]);
                kind.Counts.Add(0);
            }
            var last = kind.Batches.Count - 1;
            kind.Batches[last][kind.Counts[last]] = instance;
            kind.Counts[last]++;
            kind.Count++;
        }

        /// <summary>Moves the grass up or down with the ground.</summary>
        private void LiftGrass(float by)
        {
            foreach (var kind in _grass)
            {
                for (var b = 0; b < kind.Batches.Count; b++)
                {
                    var batch = kind.Batches[b];
                    for (var i = 0; i < kind.Counts[b]; i++) batch[i].m13 += by;
                }
            }
            foreach (var copy in _grassCopies) if (copy != null) copy.transform.position += Vector3.up * by;
        }

        /// <summary>Before the stage is filmed: the grass drawn many times over, for its camera alone.</summary>
        public void DrawGrass(Camera camera, int layer)
        {
            if (!On || camera == null) return;
            foreach (var kind in _grass)
            {
                if (kind.Mesh == null || kind.Material == null) continue;
                for (var b = 0; b < kind.Batches.Count; b++)
                {
                    Graphics.DrawMeshInstanced(kind.Mesh, 0, kind.Material, kind.Batches[b], kind.Counts[b], null, kind.Shadows, true, layer, camera);
                }
            }
        }

        private void ForgetGrass()
        {
            _grass.Clear();
            foreach (var copy in _grassCopies) if (copy != null) Object.Destroy(copy);
            _grassCopies.Clear();
        }

        // ----- Water -----

        private GameObject _water;
        private Material _waterMaterial;
        private float _waterAcross = 1f;

        /// <summary>
        /// The world's water over the sea's floor (<c>WaterVolume</c>): its surface's mesh and
        /// material, at the water's height, its depth and time told as the game tells them.
        /// </summary>
        private void PlaceWater(GameObject root, int layer)
        {
            var over = StageGround.WaterOver(Biome);
            if (over == null)
            {
                if (_water != null && _water.activeSelf) _water.SetActive(false);
                return;
            }
            if (_water == null && !MakeWater(root, layer)) return;
            if (!_water.activeSelf) _water.SetActive(true);
            _water.transform.position = new Vector3(_at.x, _at.y + over.Value, _at.z);
            var scale = _across / Mathf.Max(0.01f, _waterAcross);
            _water.transform.localScale = new Vector3(scale, 1f, scale);
            var depth = Mathf.Clamp01(over.Value / 10f);
            // The game's time, as the game gives its own water: its waves move as the world's do.
            _waterMaterial.SetFloat("_WaterTime", Time.time);
            _waterMaterial.SetFloatArray("_depth", new[] { depth, depth, depth, depth });
            _waterMaterial.SetFloat("_UseGlobalWind", 1f);
        }

        private bool MakeWater(GameObject root, int layer)
        {
            MeshRenderer surface = null;
            foreach (var volume in WaterVolume.Instances)
            {
                if (volume is WaterVolume water && water.m_waterSurface != null && water.m_waterSurface.sharedMaterial != null)
                {
                    surface = water.m_waterSurface;
                    break;
                }
            }
            var mesh = surface != null ? surface.GetComponent<MeshFilter>().OrNull()?.sharedMesh : null;
            if (mesh == null || root == null) return false;
            _waterMaterial = new Material(surface.sharedMaterial) { name = "Scry stage water" };
            _water = new GameObject("Scry stage water") { layer = layer };
            _water.transform.SetParent(root.transform, false);
            _water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = _water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _waterMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            _waterAcross = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
            return true;
        }

        // ----- The edge -----

        private GameObject _fade;
        private Texture2D _fadeTexture;

        /// <summary>The ground's edge fading into what is behind it: a sheet over it, clear in its middle and the colour given at its rim.</summary>
        private void PlaceFade(Color edge, GameObject root, int layer)
        {
            if (_fade == null)
            {
                if (_fadeTexture == null) _fadeTexture = Kept.Add(FadeTexture());
                _fade = Floor.Surface("Scry stage ground fade", layer, _fadeTexture);
                if (_fade == null) return;
                _fade.transform.SetParent(root.transform, true);
            }
            var material = _fade.GetComponent<MeshRenderer>().sharedMaterial;
            if (material.color != edge) material.color = edge;
            var top = _at.y + (StageGround.WaterOver(Biome) ?? 0f) + Below + 0.02f;
            _fade.transform.position = new Vector3(_at.x, top, _at.z);
            _fade.transform.localScale = new Vector3(_across, _across, _across);
        }

        private static Texture2D FadeTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Scry ground fade" };
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f) / size - 0.5f;
                    var dy = (y + 0.5f) / size - 0.5f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, StageGround.Fade(Mathf.Sqrt(dx * dx + dy * dy) / 0.5f));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>
        /// Lets go of everything made for the ground, as the stage is taken down: the objects
        /// standing under the stage go with it, what was made beside them is destroyed here.
        /// </summary>
        public void Forget()
        {
            ForgetGrass();
            if (_waterMaterial != null) Object.Destroy(_waterMaterial);
            _water = null;
            _waterMaterial = null;
            _fade = null;
            _for = null;
            ForgetPaints(rooms: false);
            On = false;
            if (_material != null) Object.Destroy(_material);
            if (_mesh != null) Object.Destroy(_mesh);
            if (_mask != null) Object.Destroy(_mask);
            _terrain = null;
            _material = null;
            _mesh = null;
            _mask = null;
            Biome = null;
            _triedAt = -1000;
            _hideSet = false;
        }
    }
}
