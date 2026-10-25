using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The pictures of the dungeons' marks on Valheim's map, one for each glyph and look
    /// (<see cref="MarkGlyph"/>, <see cref="MarkLook"/>), drawn once at 128 pixels in the colours
    /// given and kept, so the map and a dungeon's page show the same mark: the glyph filled on the
    /// accent disc for one walked by you, a tick in its place for one walked whole, the glyph in
    /// outline on a dark disc ringed in the accent for one walked on this server, and ringed faint
    /// for an entrance alone (<see cref="MarkRaster"/>).
    /// </summary>
    internal static class MarkPictures
    {
        private const int Side = 128;

        /// <summary>Leaving a world lets go of the pictures made, drawn again as the next world's map opens (<see cref="WorldCaches"/>).</summary>
        static MarkPictures() => WorldCaches.Register(nameof(MarkPictures), () => Made.Clear());

        private static readonly Dictionary<(MarkGlyph Glyph, MarkLook Look), Sprite> Made = new Dictionary<(MarkGlyph, MarkLook), Sprite>();
        private static Color _accent, _dark, _faint, _ring;

        /// <summary>A mark's picture by its glyph and look: the accent, the dark it is outlined in, the faint of an entrance alone and its ring.</summary>
        public static Sprite Of(MarkGlyph glyph, MarkLook look, Color accent, Color dark, Color faint, Color ring)
        {
            _accent = accent;
            _dark = dark;
            _faint = faint;
            _ring = ring;
            if (Made.TryGetValue((glyph, look), out var made) && made != null) return made;
            made = Make(glyph, look);
            Made[(glyph, look)] = made;
            return made;
        }

        /// <summary>A mark's picture by its look alone, with the doorway glyph: for the ticks and the marks beside a page's lines.</summary>
        public static Sprite Of(MarkLook look, Color accent, Color dark, Color faint, Color ring) => Of(MarkGlyph.Doorway, look, accent, dark, faint, ring);

        /// <summary>Draws a picture: each pixel the average of four, so the disc's edge and the glyph's are soft.</summary>
        private static Sprite Make(MarkGlyph glyph, MarkLook look)
        {
            var twice = Side * 2;
            var filled = look == MarkLook.Yours ? MarkRaster.FilledBits(glyph, twice) : null;
            var outline = look == MarkLook.Server || look == MarkLook.Entrance ? MarkRaster.OutlineBits(glyph, twice) : null;
            var texture = Scry.Kept.Add(new Texture2D(Side, Side, TextureFormat.RGBA32, false) { name = "Scry dungeon mark " + glyph + " " + look, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color32[Side * Side];
            for (var y = 0; y < Side; y++)
            {
                for (var x = 0; x < Side; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (var sy = 0; sy < 2; sy++)
                    {
                        for (var sx = 0; sx < 2; sx++)
                        {
                            // The texture's rows go up; the bits' rows go down.
                            var tx = x * 2 + sx;
                            var ty = y * 2 + sy;
                            var bit = (twice - 1 - ty) * twice + tx;
                            var ux = (tx + 0.5f) * 64f / twice - 32f;
                            var uy = (ty + 0.5f) * 64f / twice - 32f;
                            var colour = Pixel(look, filled != null && filled[bit], outline != null && outline[bit], ux, uy);
                            r += colour.r * colour.a;
                            g += colour.g * colour.a;
                            b += colour.b * colour.a;
                            a += colour.a;
                        }
                    }
                    pixels[y * Side + x] = a <= 0f ? default : (Color32)new Color(r / a, g / a, b / a, a / 4f);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Scry.Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, Side, Side), new Vector2(0.5f, 0.5f)));
        }

        /// <summary>A point's colour by the look: the disc and its ring by the distance from the middle, the glyph or the tick within.</summary>
        private static Color Pixel(MarkLook look, bool filled, bool outline, float ux, float uy)
        {
            var r = Mathf.Sqrt(ux * ux + uy * uy);
            if (r > 31f) return Color.clear;
            // A tick of two strokes with round ends, its corner low and left of the middle, in the mark's 64 units from its lower left.
            var tick = Mathf.Min(ToStroke(ux + 32f, uy + 32f, 18f, 31f, 28f, 21f), ToStroke(ux + 32f, uy + 32f, 28f, 21f, 46f, 42f));
            switch (look)
            {
                case MarkLook.Yours: return r > 27f ? _dark : filled ? _dark : _accent;
                case MarkLook.Whole: return r > 27f ? _dark : tick <= 3.5f ? _dark : _accent;
                case MarkLook.Server: return r > 26f ? _accent : outline ? _accent : _dark;
                case MarkLook.ServerWhole: return r > 26f ? _accent : tick <= 3.5f ? (tick <= 1.5f ? _dark : _accent) : _dark;
                default: return r > 27f ? _ring : outline ? _faint : _dark;
            }
        }

        /// <summary>How far a point is from a stroke between two points.</summary>
        private static float ToStroke(float px, float py, float ax, float ay, float bx, float by)
        {
            var vx = bx - ax;
            var vy = by - ay;
            var t = Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy));
            var cx = ax + t * vx - px;
            var cy = ay + t * vy - py;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }
    }
}
