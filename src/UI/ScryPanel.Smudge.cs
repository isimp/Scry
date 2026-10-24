using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A value the strict rules keep back shows as a soft smudge where it would stand, never as
    /// text: a bar with soft edges, the same width for every value, so it tells nothing of the
    /// value's length; its hover says what frees it, as the cell's or line's tip. A name kept
    /// back in running text still reads ??? (<see cref="CompendiumWords.Hidden"/>).
    /// </summary>
    internal static partial class ScryPanel
    {
        private static Texture2D _smudge;

        /// <summary>How wide a smudge is drawn: about a short number's width.</summary>
        private static float SmudgeWidth => U(26f);

        /// <summary>The smudge's picture, made once: a bar with soft edges, white, tinted as it is drawn.</summary>
        private static Texture2D SmudgeTexture()
        {
            if (_smudge != null) return _smudge;
            const int width = 64, height = 24;
            const float feather = 7f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            // White pixels, their alpha the shape: the panel's colour comes from the skin as it is drawn.
            var pixels = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    // How far outside the core bar, softened over the feather.
                    var dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - width / 2f) - (width / 2f - feather - 4f));
                    var dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - height / 2f) - (height / 2f - feather - 2f));
                    var alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / feather);
                    alpha *= alpha * (3f - 2f * alpha);
                    var at = (y * width + x) * 4;
                    pixels[at] = pixels[at + 1] = pixels[at + 2] = 255;
                    pixels[at + 3] = (byte)(alpha * 255f);
                }
            }
            texture.SetPixelData(pixels, 0);
            texture.Apply();
            return _smudge = Kept.Add(texture);
        }

        /// <summary>Draws the smudge in a cell, at its left or centred, the cell's height centred on, in the faint tone.</summary>
        private static void Smudge(Rect cell, bool centred = false)
        {
            var w = Mathf.Min(SmudgeWidth, cell.width);
            var h = U(12f);
            var rect = new Rect(centred ? cell.x + (cell.width - w) / 2f : cell.x, cell.y + (cell.height - h) / 2f, w, h);
            if (Event.current.type == EventType.Repaint)
            {
                var was = GUI.color;
                GUI.color = Skin.Faint;
                GUI.DrawTexture(rect, SmudgeTexture(), ScaleMode.StretchToFill, true);
                GUI.color = was;
            }
            CountDrawn(PanelPart.Smudge);
        }
    }
}
