using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>
    /// What the Ground backdrop lays with a biome's ground (<see cref="StageGround"/>). The ground
    /// lies under the model, several times as wide as what is framed and far out toward a horizon
    /// with the sky, fading at its edge into what is behind it. A place paints its own paths,
    /// dirt and paving on it as the game paints its terrain under it (<c>TerrainModifier</c>,
    /// <c>Heightmap.PaintCleared</c>). The biome's grass and small plants grow on it within forty
    /// metres, scattered by the game's own rules for them (<c>ClutterSystem</c>). The sea's floor
    /// has the world's water over it. The stage's light and sky take the colours the game's most
    /// common weather gives the biome at the time of day the lighting is set to, and its key
    /// light casts shadows. The biome is the entry's own, or one picked in the View box.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>The ground's biome as picked in the View box: Auto for the entry's own (<see cref="StageGround.Choices"/>); remembered.</summary>
        public static string GroundChoice
        {
            get => _groundChoice;
            set => _groundChoice = Array.IndexOf(StageGround.Choices, value) >= 0 ? value : StageGround.Choices[0];
        }

        private static string _groundChoice = "Auto";

        /// <summary>Whether a biome's ground is laid now.</summary>
        private static bool _groundOn;

        /// <summary>How the ground was last laid: for which entry and biome, with the sky or not, how far across, for what it framed and where.</summary>
        private static Entry _groundFor;
        private static string _groundLaidBiome;
        private static bool _groundSky;
        private static float _groundAcross = 24f;
        private static float _groundBasis;
        private static Vector3 _groundAt;
        private static int _groundPaintsLaid = -1;

        /// <summary>How far from the middle grass grows, as far as the game grows it around you (<c>ClutterSystem.m_distance</c>).</summary>
        private const float GrassReach = 40f;

        /// <summary>A place's paint on its ground, read off its copy while it sleeps, as stripping takes it off; the example's room it is in, if any.</summary>
        internal sealed class GroundPaintAt
        {
            public Transform At;
            public float Radius, Strength;
            public TerrainModifier.PaintType Type;
            public int Order;
            public GameObject Room;
        }

        private static readonly List<GroundPaintAt> GroundPaints = new List<GroundPaintAt>();
        private static int _groundPaintsVersion;

        /// <summary>Takes a copy's paints on its ground, once it stands.</summary>
        private static void TakeGroundPaints(List<GroundPaintAt> paints, GameObject room)
        {
            if (paints == null || paints.Count == 0) return;
            foreach (var paint in paints) paint.Room = room;
            GroundPaints.AddRange(paints);
            _groundPaintsVersion++;
        }

        /// <summary>Lets go of the paints of the example's rooms, as its rooms go, or of all of them.</summary>
        private static void ForgetGroundPaints(bool rooms)
        {
            var before = GroundPaints.Count;
            if (rooms) GroundPaints.RemoveAll(paint => paint.Room != null);
            else GroundPaints.Clear();
            if (GroundPaints.Count != before) _groundPaintsVersion++;
        }

        /// <summary>How many paints the ground has, how many grass stems and plants grow on it, and whether water lies over it, for the self-test.</summary>
        public static int GroundPaintCount => _groundOn ? GroundPaints.Count : 0;
        public static int GrassCount
        {
            get
            {
                var count = GrassCopies.Count;
                foreach (var kind in Grass) count += kind.Count;
                return count;
            }
        }
        public static bool WaterShown => _water != null && _water.activeSelf;

        /// <summary>
        /// Lays the ground under the model at its height: again for another entry, biome, sky or
        /// paint, or as what is framed grows; its grass goes up and down with it.
        /// </summary>
        private static void LayGround(float groundY)
        {
            var on = _groundOn && _terrain != null && _terrain.activeSelf;
            if (_groundFade != null && _groundFade.activeSelf != on) _groundFade.SetActive(on);
            if (!on)
            {
                if (_water != null && _water.activeSelf) _water.SetActive(false);
                if (Grass.Count > 0 || GrassCopies.Count > 0) ForgetGrass();
                _groundFor = null;
                return;
            }

            var sky = StageGround.Sky(_backdrop);
            var basis = Mathf.Max(0.5f, _frameRadius);
            var at = new Vector3(Origin.x, groundY - GroundBelow, Origin.z);
            var another = !ReferenceEquals(_groundFor, _lastShown) || _groundLaidBiome != _terrainBiome || _groundSky != sky || _groundPaintsLaid != _groundPaintsVersion;
            if (another || basis > _groundBasis * 1.25f)
            {
                _groundBasis = another ? basis : Mathf.Max(basis, _groundBasis);
                _groundFor = _lastShown;
                _groundLaidBiome = _terrainBiome;
                _groundSky = sky;
                _groundPaintsLaid = _groundPaintsVersion;
                _groundAcross = StageGround.Across(_groundBasis, sky);
                _groundAt = at;
                PaintMask();
                ScatterGrass();
            }
            else if (Mathf.Abs(at.y - _groundAt.y) > 0.005f)
            {
                LiftGrass(at.y - _groundAt.y);
                _groundAt = at;
            }

            PlaceGroundAt(_groundAt, _groundAcross);
            PlaceWater();
            PlaceFade();
        }

        // ----- Paint -----

        /// <summary>
        /// The ground's paint mask: as the world leaves it under the biome, and under a place
        /// painted by its paints in their order (<see cref="StageGround.Painted"/>), half a metre
        /// a point where the ground is small, coarser where it is wide.
        /// </summary>
        private static void PaintMask()
        {
            var ground = Heightmap.m_paintMaskNothing;
            ground.a = StageGround.MaskShare(_terrainBiome);
            var paints = PaintsNow();
            var size = paints.Count == 0 ? 2 : Mathf.Clamp(Mathf.CeilToInt(_groundAcross * 2f), 32, 256);
            if (_terrainMask.width != size) _terrainMask.Reinitialize(size, size);
            var pixels = new Color[size * size];
            var bare = (ground.r, ground.g, ground.b, ground.a);
            var near = new List<GroundPaint>();
            for (var j = 0; j < size; j++)
            {
                var z = _groundAt.z + ((j + 0.5f) / size - 0.5f) * _groundAcross;
                for (var i = 0; i < size; i++)
                {
                    var x = _groundAt.x + ((i + 0.5f) / size - 0.5f) * _groundAcross;
                    near.Clear();
                    foreach (var paint in paints) if (Mathf.Abs(x - paint.X) <= paint.Radius && Mathf.Abs(z - paint.Z) <= paint.Radius) near.Add(paint);
                    var (r, g, b, a) = near.Count == 0 ? bare : StageGround.Painted(bare, x, z, near);
                    pixels[j * size + i] = new Color(r, g, b, a);
                }
            }
            _terrainMask.SetPixels(pixels);
            _terrainMask.Apply(false);
        }

        /// <summary>The paints as they stand now, in the order the game applies them.</summary>
        private static List<GroundPaint> PaintsNow()
        {
            var sorted = new List<GroundPaintAt>();
            foreach (var paint in GroundPaints) if (paint.At != null) sorted.Add(paint);
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

        private static readonly List<GrassKind> Grass = new List<GrassKind>();
        private static readonly List<GameObject> GrassCopies = new List<GameObject>();

        /// <summary>The most plants made as copies of their own, rather than drawn many times over.</summary>
        private const int MostGrassCopies = 300;

        /// <summary>
        /// Scatters the biome's grass and small plants over the ground within forty metres, as the
        /// game scatters them around you (<c>ClutterSystem.GenerateVegPatch</c>): patch by patch,
        /// each kind for the biome with its own dice for the patch, its places kept by its forest,
        /// noise, height, slope, sea depth, vegetation and cleared ground rules, the ground taken
        /// at a height typical of the biome (<see cref="StageGround.Altitude"/>).
        /// </summary>
        private static void ScatterGrass()
        {
            ForgetGrass();
            var system = ClutterSystem.instance;
            if (system == null || _root == null || !Enum.TryParse<Heightmap.Biome>(_terrainBiome, out var biome)) return;
            var paints = PaintsNow();
            var ground = Heightmap.m_paintMaskNothing;
            ground.a = StageGround.MaskShare(_terrainBiome);
            var bare = (ground.r, ground.g, ground.b, ground.a);
            var altitude = StageGround.Altitude(_terrainBiome);
            var depth = StageGround.WaterOver(_terrainBiome) ?? 0f;
            var size = Mathf.Max(1f, system.m_grassPatchSize);
            var half = size / 2f;
            var reach = Mathf.Min(_groundAcross / 2f, GrassReach);
            var patches = Mathf.CeilToInt(reach / size);
            var kinds = new Dictionary<GameObject, GrassKind>();
            var state = UnityEngine.Random.state;
            try
            {
                for (var px = -patches; px <= patches; px++)
                {
                    for (var pz = -patches; pz <= patches; pz++)
                    {
                        var cx = _groundAt.x + px * size;
                        var cz = _groundAt.z + pz * size;
                        if (new Vector2(cx - _groundAt.x, cz - _groundAt.z).magnitude > reach) continue;
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
                                point.y = clutter.m_snapToWater ? _groundAt.y + depth : _groundAt.y + GroundBelow;
                                if (clutter.m_randomOffset != 0f) point.y += UnityEngine.Random.Range(0f - clutter.m_randomOffset, clutter.m_randomOffset);
                                var turn = Quaternion.Euler(0f, yaw, 0f);
                                if (clutter.m_instanced) AddGrass(kinds, clutter, point, turn);
                                else if (GrassCopies.Count < MostGrassCopies)
                                {
                                    var copy = Ghost.Make(clutter.m_prefab, _root.transform, point, turn, _layer);
                                    if (copy != null) GrassCopies.Add(copy);
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
            Grass.AddRange(kinds.Values);
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
        private static void LiftGrass(float by)
        {
            foreach (var kind in Grass)
            {
                for (var b = 0; b < kind.Batches.Count; b++)
                {
                    var batch = kind.Batches[b];
                    for (var i = 0; i < kind.Counts[b]; i++) batch[i].m13 += by;
                }
            }
            foreach (var copy in GrassCopies) if (copy != null) copy.transform.position += Vector3.up * by;
        }

        /// <summary>Before the stage is filmed: the grass drawn many times over, for its camera alone.</summary>
        private static void DrawGrass()
        {
            if (!_groundOn || _camera == null) return;
            foreach (var kind in Grass)
            {
                if (kind.Mesh == null || kind.Material == null) continue;
                for (var b = 0; b < kind.Batches.Count; b++)
                {
                    Graphics.DrawMeshInstanced(kind.Mesh, 0, kind.Material, kind.Batches[b], kind.Counts[b], null, kind.Shadows, true, _layer, _camera);
                }
            }
        }

        private static void ForgetGrass()
        {
            Grass.Clear();
            foreach (var copy in GrassCopies) if (copy != null) Object.Destroy(copy);
            GrassCopies.Clear();
        }

        // ----- Water -----

        private static GameObject _water;
        private static Material _waterMaterial;
        private static float _waterAcross = 1f;

        /// <summary>
        /// The world's water over the sea's floor (<c>WaterVolume</c>): its surface's mesh and
        /// material, at the water's height, its depth and time told as the game tells them.
        /// </summary>
        private static void PlaceWater()
        {
            var over = StageGround.WaterOver(_terrainBiome);
            if (over == null)
            {
                if (_water != null && _water.activeSelf) _water.SetActive(false);
                return;
            }
            if (_water == null && !MakeWater()) return;
            if (!_water.activeSelf) _water.SetActive(true);
            _water.transform.position = new Vector3(_groundAt.x, _groundAt.y + over.Value, _groundAt.z);
            var scale = _groundAcross / Mathf.Max(0.01f, _waterAcross);
            _water.transform.localScale = new Vector3(scale, 1f, scale);
            var depth = Mathf.Clamp01(over.Value / 10f);
            _waterMaterial.SetFloat("_WaterTime", Time.time);
            _waterMaterial.SetFloatArray("_depth", new[] { depth, depth, depth, depth });
            _waterMaterial.SetFloat("_UseGlobalWind", 1f);
        }

        private static bool MakeWater()
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
            var mesh = surface != null ? surface.GetComponent<MeshFilter>()?.sharedMesh : null;
            if (mesh == null || _root == null) return false;
            _waterMaterial = new Material(surface.sharedMaterial) { name = "Scry stage water" };
            _water = new GameObject("Scry stage water") { layer = _layer };
            _water.transform.SetParent(_root.transform, false);
            _water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = _water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _waterMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            _waterAcross = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
            return true;
        }

        // ----- The edge -----

        private static GameObject _groundFade;
        private static Texture2D _fadeTexture;

        /// <summary>The ground's edge fading into what is behind it: a sheet over it, clear in its middle and the backdrop's colour at its rim, the horizon's with the sky.</summary>
        private static void PlaceFade()
        {
            if (_groundFade == null)
            {
                if (_fadeTexture == null) _fadeTexture = Kept.Add(FadeTexture());
                _groundFade = Floor.Surface("Scry stage ground fade", _layer, _fadeTexture);
                if (_groundFade == null) return;
                _groundFade.transform.SetParent(_root.transform, true);
            }
            var look = _look ?? Presets[_lighting];
            var color = StageGround.Sky(_backdrop) ? look.Horizon : look.Backdrop;
            color.a = 1f;
            var material = _groundFade.GetComponent<MeshRenderer>().sharedMaterial;
            if (material.color != color) material.color = color;
            var top = _groundAt.y + (StageGround.WaterOver(_terrainBiome) ?? 0f) + GroundBelow + 0.02f;
            _groundFade.transform.position = new Vector3(_groundAt.x, top, _groundAt.z);
            _groundFade.transform.localScale = new Vector3(_groundAcross, _groundAcross, _groundAcross);
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

        // ----- The biome's light and sky -----

        /// <summary>The lighting as the stage stands now: the preset chosen, in the colours of the biome laid with the Ground backdrop.</summary>
        private static Lighting _look;

        private static readonly Dictionary<string, Texture2D> BiomeSkies = new Dictionary<string, Texture2D>();

        /// <summary>
        /// The lighting chosen, its sun, ambient, backdrop and sky in the colours the game's most
        /// common weather in the biome has at the lighting's time of day (<c>EnvSetup</c>); the
        /// preset itself without a biome laid, for the cave, or where the weather is not known.
        /// </summary>
        private static Lighting Look()
        {
            var preset = Presets[_lighting];
            if (!_groundOn || _terrainBiome == null || !(StageGround.TimeFor(preset.Name) is StageGround.Time time)) return preset;
            var weather = BiomeWeather(_terrainBiome);
            if (weather == null) return preset;
            Color sun, ambient, fog, fogSun;
            var power = preset.KeyPower;
            switch (time)
            {
                case StageGround.Time.Evening:
                    sun = weather.m_sunColorEvening;
                    ambient = Color.Lerp(weather.m_ambColorDay, weather.m_ambColorNight, 0.5f);
                    fog = weather.m_fogColorEvening;
                    fogSun = weather.m_fogColorSunEvening;
                    break;
                case StageGround.Time.Night:
                    sun = weather.m_sunColorNight;
                    ambient = weather.m_ambColorNight;
                    fog = weather.m_fogColorNight;
                    fogSun = weather.m_fogColorSunNight;
                    break;
                default:
                    sun = weather.m_sunColorDay;
                    ambient = weather.m_ambColorDay;
                    fog = weather.m_fogColorDay;
                    fogSun = weather.m_fogColorSunDay;
                    power *= Mathf.Clamp(weather.m_lightIntensityDay / 1.2f, 0.4f, 1.4f);
                    break;
            }
            fog.a = fogSun.a = sun.a = ambient.a = 1f;
            return new Lighting
            {
                Name = preset.Name, Key = sun, KeyPower = power, KeyAngle = preset.KeyAngle, Fill = preset.Fill, FillPower = preset.FillPower,
                Rim = preset.Rim, RimPower = preset.RimPower, Ambient = ambient, Backdrop = fog,
                SkyTop = fog * 0.85f, Horizon = Color.Lerp(fog, fogSun, 0.5f), Ground = fog * 0.5f,
            };
        }

        /// <summary>The weather most common in a biome, as the game weighs its weathers (<c>EnvMan.m_biomes</c>).</summary>
        private static EnvSetup BiomeWeather(string biome)
        {
            if (EnvMan.instance == null || !Enum.TryParse<Heightmap.Biome>(biome, out var wanted)) return null;
            EnvSetup best = null;
            var weight = float.NegativeInfinity;
            foreach (var setup in EnvMan.instance.m_biomes)
            {
                if (setup == null || setup.m_biome != wanted || setup.m_environments == null) continue;
                foreach (var entry in setup.m_environments)
                {
                    if (entry?.m_env == null || entry.m_weight <= weight) continue;
                    best = entry.m_env;
                    weight = entry.m_weight;
                }
            }
            return best;
        }

        /// <summary>The sky for the look: the lighting's own, or one in the biome's colours, made once.</summary>
        private static Texture2D SkyFor(Lighting look)
        {
            if (ReferenceEquals(look, Presets[_lighting]))
            {
                if (SkyTextures[_lighting] == null) SkyTextures[_lighting] = Floor.SkyTexture(look.SkyTop, look.Horizon, look.Ground);
                return SkyTextures[_lighting];
            }
            var key = look.Name + "|" + _terrainBiome;
            if (!BiomeSkies.TryGetValue(key, out var sky) || sky == null) BiomeSkies[key] = sky = Floor.SkyTexture(look.SkyTop, look.Horizon, look.Ground);
            return sky;
        }

        /// <summary>Lets go of what was made for the dressing, as the stage is taken down.</summary>
        private static void ForgetDressing()
        {
            ForgetGrass();
            if (_waterMaterial != null) Object.Destroy(_waterMaterial);
            _water = null;
            _waterMaterial = null;
            _groundFade = null;
            _groundFor = null;
            _look = null;
            ForgetGroundPaints(rooms: false);
        }
    }
}
