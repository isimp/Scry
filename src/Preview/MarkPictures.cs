using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The pictures of the dungeons' marks on Valheim's large map, one for each look
    /// (<see cref="MarkLook"/>), made once in the colours given and kept, so the map and a
    /// dungeon's page show the same mark.
    /// </summary>
    internal static class MarkPictures
    {
        private static Sprite _sprite, _whole, _hollow, _hollowWhole, _entrance;
        private static Color _accent, _dark, _faint, _ring;

        /// <summary>A mark's picture by its look: the accent, the dark it is outlined in, the faint of an entrance alone and its ring.</summary>
        public static Sprite Of(MarkLook look, Color accent, Color dark, Color faint, Color ring)
        {
            _accent = accent;
            _dark = dark;
            _faint = faint;
            _ring = ring;
            return SpriteOf(look);
        }

        /// <summary>A mark's picture by its look.</summary>
        private static Sprite SpriteOf(MarkLook look)
        {
            switch (look)
            {
                case MarkLook.Yours: return MarkSprite();
                case MarkLook.Whole: return WholeSprite();
                case MarkLook.Server: return HollowSprite();
                case MarkLook.ServerWhole: return HollowWholeSprite();
                default: return EntranceSprite();
            }
        }

        /// <summary>The picture of a mark of a dungeon you walked whole: the walked mark's accent disc, outlined dark, with a tick where its doorway is, made once.</summary>
        private static Sprite WholeSprite()
        {
            if (_whole != null) return _whole;
            const int side = 64;
            var texture = Scry.Kept.Add(new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry dungeon mark whole", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color32[side * side];
            var accent = (Color32)_accent;
            var dark = (Color32)_dark;
            var clear = default(Color32);
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var px = x + 0.5f;
                    var py = y + 0.5f;
                    var r = Mathf.Sqrt((px - side / 2f) * (px - side / 2f) + (py - side / 2f) * (py - side / 2f));
                    // A tick of two strokes 7 wide with round ends, its corner low and left of the middle.
                    var tick = Mathf.Min(ToStroke(px, py, 18f, 31f, 28f, 21f), ToStroke(px, py, 28f, 21f, 46f, 42f)) <= 3.5f;
                    pixels[y * side + x] = r > 31f ? clear : r > 27f ? dark : tick ? dark : accent;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _whole = Scry.Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f)));
            return _whole;
        }


        /// <summary>The picture of a mark of a dungeon others on this server walked whole: the hollow mark's dark disc ringed in the accent, with the tick in the accent where its doorway is, made once.</summary>
        private static Sprite HollowWholeSprite()
        {
            if (_hollowWhole != null) return _hollowWhole;
            const int side = 64;
            var texture = Scry.Kept.Add(new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry dungeon mark hollow whole", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color32[side * side];
            var accent = (Color32)_accent;
            var dark = (Color32)_dark;
            var clear = default(Color32);
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var px = x + 0.5f;
                    var py = y + 0.5f;
                    var r = Mathf.Sqrt((px - side / 2f) * (px - side / 2f) + (py - side / 2f) * (py - side / 2f));
                    // The walked whole mark's tick, a little thinner, as the hollow mark's doorway is drawn in outline.
                    var tick = Mathf.Min(ToStroke(px, py, 18f, 31f, 28f, 21f), ToStroke(px, py, 28f, 21f, 46f, 42f)) <= 3f;
                    pixels[y * side + x] = r > 31f ? clear : r > 26f ? accent : tick ? accent : dark;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _hollowWhole = Scry.Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f)));
            return _hollowWhole;
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

        /// <summary>The mark's picture: an accent disc, outlined dark, with a doorway in it, made once.</summary>
        private static Sprite MarkSprite()
        {
            if (_sprite != null) return _sprite;
            const int side = 64;
            var texture = Scry.Kept.Add(new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry dungeon mark", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color32[side * side];
            var accent = (Color32)_accent;
            var dark = (Color32)_dark;
            var clear = default(Color32);
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var dx = x + 0.5f - side / 2f;
                    var dy = y + 0.5f - side / 2f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    // A rounded doorway: an arch over an opening, in the disc's middle.
                    var arch = dy > 4f ? Mathf.Sqrt(dx * dx + (dy - 4f) * (dy - 4f)) <= 12f : Mathf.Abs(dx) <= 12f && dy >= -16f;
                    pixels[y * side + x] = r > 31f ? clear : r > 27f ? dark : arch ? dark : accent;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _sprite = Scry.Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f)));
            return _sprite;
        }

        /// <summary>The picture of a mark of a dungeon's entrance alone: a dark disc ringed faint, the doorway's outline faint in it, made once.</summary>
        private static Sprite EntranceSprite()
        {
            if (_entrance != null) return _entrance;
            const int side = 64;
            var texture = Scry.Kept.Add(new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry dungeon mark entrance", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color32[side * side];
            var faint = (Color32)_faint;
            var ring = (Color32)_ring;
            var dark = (Color32)_dark;
            var clear = default(Color32);
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var dx = x + 0.5f - side / 2f;
                    var dy = y + 0.5f - side / 2f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    // The same doorway as the others', its outline alone, faint.
                    var outer = dy > 4f ? Mathf.Sqrt(dx * dx + (dy - 4f) * (dy - 4f)) <= 12f : Mathf.Abs(dx) <= 12f && dy >= -16f;
                    var inner = dy > 4f ? Mathf.Sqrt(dx * dx + (dy - 4f) * (dy - 4f)) <= 8.5f : Mathf.Abs(dx) <= 8.5f && dy >= -12.5f;
                    pixels[y * side + x] = r > 31f ? clear : r > 27f ? ring : outer && !inner ? faint : dark;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _entrance = Scry.Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f)));
            return _entrance;
        }

        /// <summary>The picture of a mark of a dungeon walked on this server, shown with insides: a dark disc ringed in the accent, the doorway drawn in its outline, made once.</summary>
        private static Sprite HollowSprite()
        {
            if (_hollow != null) return _hollow;
            const int side = 64;
            var texture = Scry.Kept.Add(new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry dungeon mark hollow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color32[side * side];
            var accent = (Color32)_accent;
            var dark = (Color32)_dark;
            var clear = default(Color32);
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var dx = x + 0.5f - side / 2f;
                    var dy = y + 0.5f - side / 2f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    // The same doorway as the walked mark's, its outline alone.
                    var outer = dy > 4f ? Mathf.Sqrt(dx * dx + (dy - 4f) * (dy - 4f)) <= 12f : Mathf.Abs(dx) <= 12f && dy >= -16f;
                    var inner = dy > 4f ? Mathf.Sqrt(dx * dx + (dy - 4f) * (dy - 4f)) <= 8.5f : Mathf.Abs(dx) <= 8.5f && dy >= -12.5f;
                    pixels[y * side + x] = r > 31f ? clear : r > 26f ? accent : outer && !inner ? accent : dark;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _hollow = Scry.Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f)));
            return _hollow;
        }
    }
}
