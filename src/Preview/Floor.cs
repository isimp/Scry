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

        /// <summary>A flat unit quad facing up, centred on its origin.</summary>
        private static Mesh Quad()
        {
            var mesh = new Mesh { name = "Scry floor" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f),
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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
            return texture;
        }
    }
}
