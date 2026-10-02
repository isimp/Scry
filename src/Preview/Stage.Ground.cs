using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>
    /// The Ground backdrop: a round piece of ground under the model, its own biome's
    /// (<see cref="StageGround"/>), drawn with the world's own terrain material as the game draws
    /// its terrain: the biome told by each point's colour (<c>Heightmap.GetBiomeColor</c>), nothing
    /// painted on it (<c>Heightmap.m_paintMaskNothing</c>), and, for the sea's floor, water over
    /// it. Where the world has no terrain to borrow it from yet, or what is shown is underground,
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

        /// <summary>How far under the sea the sea's floor lies, as the terrain is told of it (<c>Heightmap.UpdateCornerDepths</c>).</summary>
        private const float SeaDepth = 20f;

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
            if (!ground) return;
            var biome = GroundBiomeOverride ?? StageGround.BiomeFor(_lastShown?.Biomes);
            if (biome != _terrainBiome) PaintGround(biome);
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
            _terrainMask = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Scry stage ground mask", wrapMode = TextureWrapMode.Clamp };
            _terrainMask.SetPixels(new[] { Heightmap.m_paintMaskNothing, Heightmap.m_paintMaskNothing, Heightmap.m_paintMaskNothing, Heightmap.m_paintMaskNothing });
            _terrainMask.Apply(false);
            _terrainMaterial.SetTexture("_ClearedMaskTex", _terrainMask);
            _terrainMesh = Disc();

            _terrain = new GameObject("Scry stage ground") { layer = _layer };
            _terrain.transform.SetParent(_root.transform, false);
            _terrain.AddComponent<MeshFilter>().sharedMesh = _terrainMesh;
            var renderer = _terrain.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _terrainMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            _terrainBiome = null;
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

        /// <summary>Gives the ground a biome: each point's colour as the game gives its terrain's, and the sea over the sea's floor.</summary>
        private static void PaintGround(string biome)
        {
            var color = Enum.TryParse<Heightmap.Biome>(biome, out var parsed) ? Heightmap.GetBiomeColor(parsed) : new Color32(0, 0, 0, 0);
            var colors = new Color32[_terrainMesh.vertexCount];
            for (var i = 0; i < colors.Length; i++) colors[i] = color;
            _terrainMesh.colors32 = colors;
            var depth = biome == "Ocean" ? SeaDepth : 0f;
            _terrainMaterial.SetFloatArray("_depth", new[] { depth, depth, depth, depth });
            _terrainBiome = biome;
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
            if (_terrainMaterial != null) Object.Destroy(_terrainMaterial);
            if (_terrainMesh != null) Object.Destroy(_terrainMesh);
            if (_terrainMask != null) Object.Destroy(_terrainMask);
            _terrain = null;
            _terrainMaterial = null;
            _terrainMesh = null;
            _terrainMask = null;
            _terrainBiome = null;
            _terrainTriedAt = -1000;
        }
    }
}
