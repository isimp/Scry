using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A soft round floor under the turntable with faint rings, so a model reads as standing on
    /// something rather than floating in the dark.
    /// </summary>
    internal static class Floor
    {
        private const int Size = 256;

        private static readonly string[] Shaders =
        {
            "Sprites/Default",
            "Unlit/Transparent",
            "Legacy Shaders/Particles/Alpha Blended",
            "Particles/Standard Unlit",
        };

        private static Material _material;
        private static bool _tried;
        private static Texture2D _grid;

        /// <summary>
        /// The quads and the surfaces' own materials made for the stage now standing. Unity does
        /// not destroy them with the objects that draw them, so <see cref="Release"/> does when
        /// the stage is taken down. The shared floor material and the textures are kept.
        /// </summary>
        private static readonly List<Object> Made = new List<Object>();

        public static GameObject Make(int layer)
        {
            var material = Material();
            if (material == null) return null;

            var floor = new GameObject("Scry stage floor") { layer = layer };
            floor.AddComponent<MeshFilter>().sharedMesh = Quad();
            var renderer = floor.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return floor;
        }

        /// <summary>A flat quad on the stage layer with a material of its own, showing a texture.</summary>
        public static GameObject Surface(string name, int layer, Texture2D texture)
        {
            var shared = Material();
            if (shared == null) return null;

            var surface = new GameObject(name) { layer = layer };
            surface.AddComponent<MeshFilter>().sharedMesh = Quad();
            var renderer = surface.AddComponent<MeshRenderer>();
            var material = new Material(shared.shader) { mainTexture = texture, name = name };
            Made.Add(material);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return surface;
        }

        /// <summary>Destroys the quads and materials made for the stage, once it is taken down.</summary>
        public static void Release()
        {
            foreach (var made in Made) if (made != null) Object.Destroy(made);
            Made.Clear();
        }

        /// <summary>How many metres one tile of the grid texture covers.</summary>
        public const float GridMetres = 5f;

        /// <summary>
        /// Five metres of grid: squares of one metre in faint lines, a brighter line every five,
        /// over a faint fill. Tiled across the floor by its mesh, since the stage's shaders ignore
        /// a material's tiling. Made once and kept, as the sky's textures are.
        /// </summary>
        public static Texture2D GridTexture()
        {
            if (_grid != null) return _grid;
            const int perMetre = 64;
            const int size = perMetre * 5;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8, name = "Scry grid",
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var major = x < 3 || y < 3;
                    var minor = x % perMetre < 2 || y % perMetre < 2;
                    pixels[y * size + x] = major ? new Color32(240, 242, 248, 150)
                        : minor ? new Color32(225, 228, 236, 80)
                        : new Color32(200, 205, 215, 14);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            _grid = texture;
            return Kept.Add(texture);
        }

        /// <summary>Repeats a surface's texture the given number of times each way, through its mesh.</summary>
        public static void Tile(GameObject surface, float times)
        {
            var mesh = surface != null ? surface.GetComponent<MeshFilter>()?.sharedMesh : null;
            if (mesh == null) return;
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, times), new Vector2(times, times), new Vector2(times, 0) };
        }

        /// <summary>A sky from top to bottom: its colour above, a brighter horizon, and the ground below.</summary>
        public static Texture2D SkyTexture(Color top, Color horizon, Color ground)
        {
            const int height = 128;
            var texture = new Texture2D(4, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Scry sky" };
            var pixels = new Color[4 * height];
            for (var y = 0; y < height; y++)
            {
                // Row 0 is the top of the view, as the quad is turned to face the camera.
                var t = y / (height - 1f);
                var colour = t < 0.55f
                    ? Color.Lerp(top, horizon, Mathf.SmoothStep(0f, 1f, t / 0.55f))
                    : Color.Lerp(horizon, ground, Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.25f));
                colour.a = 1f;
                for (var x = 0; x < 4; x++) pixels[y * 4 + x] = colour;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Kept.Add(texture);
        }

        private static Material Material()
        {
            if (_tried) return _material;
            _tried = true;

            foreach (var name in Shaders)
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;

                _material = new Material(shader) { mainTexture = Texture(), name = "Scry stage floor" };
                return _material;
            }

            Plugin.Log.LogInfo("Scry found no shader for the stage floor; the turntable shows without one.");
            return null;
        }

        /// <summary>A flat unit quad facing up, centred on its origin, one for each surface, as the grid's is tiled through its own.</summary>
        private static Mesh Quad()
        {
            var mesh = new Mesh { name = "Scry floor" };
            Made.Add(mesh);
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f),
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return Kept.Add(mesh);
        }

        /// <summary>A disc that fades out towards its edge, with thin rings.</summary>
        private static Texture2D Texture()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Scry floor" };
            var pixels = new Color32[Size * Size];
            const float rings = 5f;

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var dx = (x + 0.5f) / Size * 2f - 1f;
                    var dy = (y + 0.5f) / Size * 2f - 1f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);

                    var disc = Mathf.Clamp01(1f - r) * 0.5f;
                    var ring = Mathf.Abs(Mathf.Repeat(r * rings + 0.5f, 1f) - 0.5f);
                    var line = Mathf.Clamp01(1f - ring * 22f) * Mathf.Clamp01(1f - r) * 0.55f;

                    var alpha = Mathf.Clamp01(disc * 0.35f + line);
                    var shade = (byte)Mathf.RoundToInt(Mathf.Lerp(150f, 235f, line * 2f));
                    pixels[y * Size + x] = new Color32(shade, shade, (byte)Mathf.Min(255, shade + 8), (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return Kept.Add(texture);
        }
    }
}
