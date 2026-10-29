using UnityEngine;

namespace Scry
{
    /// <summary>The skeleton a part of a page stands as until it is laid: a faint bar with a band of light sweeping over it.</summary>
    internal static partial class Skin
    {
        /// <summary>A soft band of light, clear at both ends, swept over the bars of a part not laid yet (<see cref="Skeleton"/>); made as the first is drawn.</summary>
        private static Texture2D _sheen;

        /// <summary>How long the band of light takes to cross a skeleton's bar, in seconds.</summary>
        private const float SheenSeconds = 1.2f;

        /// <summary>
        /// A part of a page not laid yet: a faint bar with a band of light sweeping over it, the
        /// same sweep on every bar. Crisp and moving, unlike the still, soft smudge of what the
        /// strict rules keep back.
        /// </summary>
        public static void Skeleton(Rect bar)
        {
            if (Event.current.type != EventType.Repaint) return;
            DrawPill(bar, Alpha(Text, 0.07f));
            if (_sheen == null) _sheen = Band(64);
            var phase = Time.unscaledTime % SheenSeconds / SheenSeconds;
            var band = Mathf.Max(bar.width * 0.5f, bar.height * 4f);
            var x = -band + phase * (bar.width + band);
            var was = GUI.color;
            GUI.BeginGroup(bar);
            GUI.color = Alpha(Text, 0.10f);
            GUI.DrawTexture(new Rect(x, 0f, band, bar.height), _sheen);
            GUI.color = was;
            GUI.EndGroup();
        }

        /// <summary>A band a pixel high, clear at both ends and brightest in its middle, eased in and out.</summary>
        private static Texture2D Band(int width)
        {
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color[width];
            for (var x = 0; x < width; x++)
            {
                var s = Mathf.Sin(Mathf.PI * (x + 0.5f) / width);
                pixels[x] = Alpha(Text, s * s);
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            return Kept.Add(texture);
        }
    }
}
