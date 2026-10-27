using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        /// <summary>The colours a picture is drawn in: the accent, the dark it is outlined in, the faint of an entrance alone and its ring.</summary>
        private struct Colours
        {
            public Color Accent, Dark, Faint, Ring;
        }

        /// <summary>Every picture's pixels, drawn ahead on a worker thread in the colours asked for, made into a picture one a frame (<see cref="DrawAhead"/>).</summary>
        private static Task<Dictionary<(MarkGlyph, MarkLook), Color32[]>> _drawing;
        private static Colours _drawnIn;

        /// <summary>A mark's picture by its glyph and look: the accent, the dark it is outlined in, the faint of an entrance alone and its ring.</summary>
        public static Sprite Of(MarkGlyph glyph, MarkLook look, Color accent, Color dark, Color faint, Color ring)
        {
            if (Made.TryGetValue((glyph, look), out var made) && made != null) return made;
            var colours = new Colours { Accent = accent, Dark = dark, Faint = faint, Ring = ring };
            var drawn = Drawn(colours);
            made = Picture(glyph, look, drawn != null && drawn.TryGetValue((glyph, look), out var pixels) ? pixels : Pixels(glyph, look, colours));
            Made[(glyph, look)] = made;
            return made;
        }

        /// <summary>
        /// Draws every mark's picture ahead on a worker thread, as the map can ask for any at once,
        /// and makes one a frame into a picture once drawn: drawn as each was first asked for, one
        /// held its frame for tens of milliseconds. Called each frame while the marks may show.
        /// </summary>
        public static void DrawAhead(Color accent, Color dark, Color faint, Color ring)
        {
            var colours = new Colours { Accent = accent, Dark = dark, Faint = faint, Ring = ring };
            var drawn = Drawn(colours);
            if (_drawing == null || !SameColours(_drawnIn, colours))
            {
                _drawnIn = colours;
                _drawing = Task.Run(() => DrawAll(colours));
                return;
            }
            if (drawn == null) return;
            foreach (var pair in drawn)
            {
                if (Made.TryGetValue(pair.Key, out var made) && made != null) continue;
                Made[pair.Key] = Picture(pair.Key.Item1, pair.Key.Item2, pair.Value);
                return;
            }
        }

        /// <summary>The pixels drawn ahead, once drawn in these colours; none before, or in others.</summary>
        private static Dictionary<(MarkGlyph, MarkLook), Color32[]> Drawn(Colours colours) =>
            _drawing != null && _drawing.Status == TaskStatus.RanToCompletion && SameColours(_drawnIn, colours) ? _drawing.Result : null;

        private static bool SameColours(Colours a, Colours b) => a.Accent == b.Accent && a.Dark == b.Dark && a.Faint == b.Faint && a.Ring == b.Ring;

        /// <summary>Every glyph in every look a mark is drawn in; a place stood in is drawn as one you walked.</summary>
        private static Dictionary<(MarkGlyph, MarkLook), Color32[]> DrawAll(Colours colours)
        {
            var all = new Dictionary<(MarkGlyph, MarkLook), Color32[]>();
            foreach (MarkGlyph glyph in Enum.GetValues(typeof(MarkGlyph)))
            {
                foreach (MarkLook look in Enum.GetValues(typeof(MarkLook)))
                {
                    if (look != MarkLook.Visited) all[(glyph, look)] = Pixels(glyph, look, colours);
                }
            }
            return all;
        }

        /// <summary>A mark's picture by its look alone, with the doorway glyph: for the ticks and the marks beside a page's lines.</summary>
        public static Sprite Of(MarkLook look, Color accent, Color dark, Color faint, Color ring) => Of(MarkGlyph.Doorway, look, accent, dark, faint, ring);

        /// <summary>A picture of drawn pixels, kept with what Scry made.</summary>
        private static Sprite Picture(MarkGlyph glyph, MarkLook look, Color32[] pixels)
        {
            var texture = Scry.Kept.Add(new Texture2D(Side, Side, TextureFormat.RGBA32, false) { name = "Scry dungeon mark " + glyph + " " + look, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Scry.Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, Side, Side), new Vector2(0.5f, 0.5f)));
        }

        /// <summary>Draws a picture's pixels, on any thread: each pixel the average of four, so the disc's edge and the glyph's are soft.</summary>
        private static Color32[] Pixels(MarkGlyph glyph, MarkLook look, Colours colours)
        {
            var twice = Side * 2;
            var filled = look == MarkLook.Yours ? MarkRaster.FilledBits(glyph, twice) : null;
            var outline = look == MarkLook.Server || look == MarkLook.Entrance ? MarkRaster.OutlineBits(glyph, twice) : null;
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
                            var colour = Pixel(look, filled != null && filled[bit], outline != null && outline[bit], ux, uy, colours);
                            r += colour.r * colour.a;
                            g += colour.g * colour.a;
                            b += colour.b * colour.a;
                            a += colour.a;
                        }
                    }
                    pixels[y * Side + x] = a <= 0f ? default : (Color32)new Color(r / a, g / a, b / a, a / 4f);
                }
            }
            return pixels;
        }

        /// <summary>A point's colour by the look: the disc and its ring by the distance from the middle, the glyph or the tick within.</summary>
        private static Color Pixel(MarkLook look, bool filled, bool outline, float ux, float uy, Colours c)
        {
            var r = Mathf.Sqrt(ux * ux + uy * uy);
            if (r > 31f) return Color.clear;
            // A tick of two strokes with round ends, its corner low and left of the middle, in the mark's 64 units from its lower left.
            var tick = Mathf.Min(ToStroke(ux + 32f, uy + 32f, 18f, 31f, 28f, 21f), ToStroke(ux + 32f, uy + 32f, 28f, 21f, 46f, 42f));
            switch (look)
            {
                case MarkLook.Yours: return r > 27f ? c.Dark : filled ? c.Dark : c.Accent;
                case MarkLook.Whole: return r > 27f ? c.Dark : tick <= 3.5f ? c.Dark : c.Accent;
                case MarkLook.Server: return r > 26f ? c.Accent : outline ? c.Accent : c.Dark;
                case MarkLook.ServerWhole: return r > 26f ? c.Accent : tick <= 3.5f ? (tick <= 1.5f ? c.Dark : c.Accent) : c.Dark;
                default: return r > 27f ? c.Ring : outline ? c.Faint : c.Dark;
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
