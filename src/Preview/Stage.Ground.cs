using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>
    /// The Ground backdrop: a round piece of ground under the model, its own biome's
    /// (<see cref="StageGround"/>), drawn with the world's own terrain material as the game draws
    /// its terrain: the biome told by each point's colour (<c>Heightmap.GetBiomeColor</c>), nothing
    /// painted on it (<c>Heightmap.m_paintMaskNothing</c>) but the share the world keeps in its
    /// mask, a distance to hide it by as the game gives each piece of its terrain, and, for the
    /// sea's floor, water over it (<c>_WaterLevel</c>). Where the world has no terrain to borrow it from yet, or what is shown is underground,
    /// the plain floor stays.
    /// </summary>
    internal static partial class Stage
    {
        private static GameObject _terrain;
        private static Material _terrainMaterial;
        private static Mesh _terrainMesh;
        private static Texture2D _terrainMask;
        private static string _terrainBiome;
        private static int _terrainTriedAt = -1000;


        /// <summary>
        /// The distance the terrain's shader is given to hide it by, as the game gives each piece
        /// of its terrain one of its own (<c>Heightmap.ApplySettings</c>, <c>_LodHideDistance</c>):
        /// far, so all of the stage's ground stands within it; null gives none, leaving the
        /// material's own. Settable for the self-test to try.
        /// </summary>
        public static float? GroundHideDistance
        {
            get => _groundHide;
            set
            {
                _groundHide = value;
                _groundHideSet = false;
            }
        }

        private static float? _groundHide = 100000f;
        private static bool _groundHideSet;
        private static MaterialPropertyBlock _groundBlock;

        /// <summary>For the self-test: a rendering path for the stage's camera to try; null for the game's own setting.</summary>
        public static RenderingPath? PathOverride;

        /// <summary>The value the terrain's material holds for it, without the stage's, for the self-test to tell.</summary>
        public static string GroundMaterialTold()
        {
            if (_terrainMaterial == null) return "no material";
            var shader = _terrainMaterial.shader;
            var names = new System.Collections.Generic.List<string>();
            for (var i = 0; shader != null && i < shader.GetPropertyCount(); i++)
            {
                var name = shader.GetPropertyName(i);
                var type = shader.GetPropertyType(i);
                names.Add(type == UnityEngine.Rendering.ShaderPropertyType.Float || type == UnityEngine.Rendering.ShaderPropertyType.Range
                    ? $"{name} {_terrainMaterial.GetFloat(name):0.###}" : $"{name} ({type})");
            }
            var renderer = _terrain != null ? _terrain.GetComponent<MeshRenderer>() : null;
            var drawn = renderer == null ? "no renderer" : $"active {_terrain.activeInHierarchy}, layer {_terrain.layer}, bounds {renderer.bounds.center} size {renderer.bounds.size}, in view {InView(renderer.bounds)}, drawn by a camera {renderer.isVisible}";
            var path = _camera != null ? $"{_camera.renderingPath} ({_camera.actualRenderingPath})" : "no camera";
            return $"{drawn}; the stage camera renders {path}; shader {shader?.name}, passes {_terrainMaterial.passCount}, queue {_terrainMaterial.renderQueue}, keywords [{string.Join(", ", _terrainMaterial.shaderKeywords)}], properties: {string.Join(", ", names)}";
        }

        /// <summary>How far under the floor the ground lies, as the terrain's shader raises its bumps (<c>_Displacement</c>).</summary>
        private const float GroundBelow = 0.05f;

        /// <summary>For the self-test: a biome whose ground to lay whatever is shown; null for the shown entry's own.</summary>
        public static string GroundBiomeOverride;

        /// <summary>The biome whose ground is laid now, or null where none is, for the self-test.</summary>
        public static string GroundShown => _terrain != null && _terrain.activeSelf ? _terrainBiome : null;

        /// <summary>Whether what is shown is underground: a dungeon's inside, or a room of one.</summary>
        private static bool Underground =>
            (_lastShown?.Source is PlaceSource place && place.IsRoom) || (_exampleIsDungeon && _exampleHolder != null && _exampleHolder.activeSelf);

        /// <summary>Each frame: the ground or the plain floor as the backdrop has it, of the biome of what is shown.</summary>
        private static void KeepGround()
        {
            var ground = StageGround.Ground(_backdrop, Underground) && EnsureTerrain();
            if (_terrain != null && _terrain.activeSelf != ground) _terrain.SetActive(ground);
            var floor = !StageGround.Grid(_backdrop) && !ground;
            if (_floor != null && _floor.activeSelf != floor) _floor.SetActive(floor);
            var changed = ground != _groundOn;
            _groundOn = ground;
            if (ground)
            {
                var biome = GroundBiomeOverride ?? StageGround.Chosen(GroundChoice, _lastShown?.Biomes);
                if (biome != _terrainBiome)
                {
                    PaintGround(biome);
                    changed = true;
                }
                if (!_groundHideSet) ApplyGroundBlock();
            }
            // The light and sky take the biome's colours, and the key light casts shadows, while it is laid.
            if (changed) ApplyLighting();
        }

        /// <summary>
        /// A fault in laying the ground is told once and puts the ground away for the frame, the
        /// plain floor standing in, so the stage is filmed all the same.
        /// </summary>
        private static void GroundFailed(Exception ex)
        {
            Faults.Tell("laying the stage's ground", ex);
            _groundOn = false;
            if (_terrain != null) _terrain.SetActive(false);
            if (_groundFade != null) _groundFade.SetActive(false);
            if (_water != null) _water.SetActive(false);
            if (_floor != null && !StageGround.Grid(_backdrop)) _floor.SetActive(true);
        }

        /// <summary>Makes the piece of ground once the world's terrain has a material to borrow, trying again a second apart.</summary>
        private static bool EnsureTerrain()
        {
            if (_terrain != null) return true;
            if (_root == null || Time.frameCount - _terrainTriedAt < 60) return false;
            _terrainTriedAt = Time.frameCount;
            var source = WorldTerrainMaterial();
            if (source == null) return false;

            _terrainMaterial = new Material(source) { name = "Scry stage ground" };
            _terrainMask = Kept.Add(new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Scry stage ground mask", wrapMode = TextureWrapMode.Clamp });
            _terrainMaterial.SetTexture("_ClearedMaskTex", _terrainMask);
            _terrainMesh = Kept.Add(Disc());

            _terrain = new GameObject("Scry stage ground") { layer = _layer };
            _terrain.transform.SetParent(_root.transform, false);
            _terrain.AddComponent<MeshFilter>().sharedMesh = _terrainMesh;
            var renderer = _terrain.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _terrainMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            _terrainBiome = null;
            _groundHideSet = false;
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
        private static void PaintGround(string biome)
        {
            var color = Enum.TryParse<Heightmap.Biome>(biome, out var parsed) ? Heightmap.GetBiomeColor(parsed) : new Color32(0, 0, 0, 0);
            var colors = new Color32[_terrainMesh.vertexCount];
            for (var i = 0; i < colors.Length; i++) colors[i] = color;
            _terrainMesh.colors32 = colors;
            _terrainBiome = biome;
            _groundWaterAt = float.NaN;
            _groundHideSet = false;
        }

        /// <summary>The height of the water the terrain's shader is told of (<c>_WaterLevel</c>) over the sea's floor, as last told; not a number for none.</summary>
        private static float _groundWaterAt = float.NaN;

        /// <summary>Lays the ground at its height, with water over it for the sea's floor: told again only as the ground moves.</summary>
        private static void PlaceGroundAt(Vector3 at, float across)
        {
            _terrain.transform.position = at;
            _terrain.transform.localScale = new Vector3(across, 1f, across);
            var water = StageGround.WaterOver(_terrainBiome) is float over ? at.y + over : float.NaN;
            if (float.IsNaN(water) == float.IsNaN(_groundWaterAt) && (float.IsNaN(water) || Mathf.Abs(water - _groundWaterAt) < 0.1f)) return;
            _groundWaterAt = water;
            _groundHideSet = false;
            ApplyGroundBlock();
        }

        /// <summary>What the stage tells the terrain's shader of its piece alone: the distance to hide it by, and the water over it.</summary>
        private static void ApplyGroundBlock()
        {
            var renderer = _terrain.GetComponent<MeshRenderer>();
            if (_groundBlock == null) _groundBlock = new MaterialPropertyBlock();
            _groundBlock.Clear();
            if (_groundHide is float distance) _groundBlock.SetFloat("_LodHideDistance", distance);
            if (!float.IsNaN(_groundWaterAt)) _groundBlock.SetFloat("_WaterLevel", _groundWaterAt);
            renderer.SetPropertyBlock(_groundHide != null || !float.IsNaN(_groundWaterAt) ? _groundBlock : null);
            _groundHideSet = true;
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

            var triangles = new System.Collections.Generic.List<int>();
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

        /// <summary>Lets go of the piece of ground and what was made for it, as the stage is taken down.</summary>
        private static void ForgetGround()
        {
            ForgetDressing();
            _groundOn = false;
            if (_terrainMaterial != null) Object.Destroy(_terrainMaterial);
            if (_terrainMesh != null) Object.Destroy(_terrainMesh);
            if (_terrainMask != null) Object.Destroy(_terrainMask);
            _terrain = null;
            _terrainMaterial = null;
            _terrainMesh = null;
            _terrainMask = null;
            _terrainBiome = null;
            _terrainTriedAt = -1000;
            _groundHideSet = false;
        }
    }
}
