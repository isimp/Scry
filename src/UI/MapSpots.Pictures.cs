using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>The pictures the map's dots stand in, drawn once for each colour, shape and picture.</summary>
    internal static partial class MapSpots
    {
        /// <summary>The dots' pictures, one drawn for each colour and shape a kind is shown in.</summary>
        private static readonly Dictionary<(Color32, MarkShape, bool), Sprite> Pictures = new Dictionary<(Color32, MarkShape, bool), Sprite>();

        /// <summary>
        /// The dots' picture in a colour, drawn once for it: the colour inside a dark edge, the map
        /// showing between it and a light ring, a dark halo either side of the ring
        /// (<see cref="SeenOnMap.DotAt"/>); the edge and halo in the dark the dungeon marks are
        /// outlined in, the ring in Scry's writing colour; in a circle, or the shape of a kind's mark
        /// (<see cref="SeenOnMap.ShapeShare"/>). Each pixel is the mean of four by four points in
        /// it, so the edges stay smooth when the dot is drawn small.
        /// </summary>
        public static Sprite Picture(Color colour, MarkShape shape = MarkShape.Circle, bool hollow = false)
        {
            var key = ((Color32)colour, shape, hollow);
            if (Pictures.TryGetValue(key, out var made) && made != null) return made;
            const int side = 64, points = 4;
            var pixels = new Color32[side * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var sum = Skin.Alpha(Skin.OnAccent, 0f) * 0f;
                    var a = 0f;
                    for (var py = 0; py < points; py++)
                    {
                        for (var px = 0; px < points; px++)
                        {
                            var dx = (x + (px + 0.5f) / points - side / 2f) / (side / 2f);
                            var dy = (y + (py + 0.5f) / points - side / 2f) / (side / 2f);
                            var shade = Shade(SeenOnMap.DotAt(SeenOnMap.ShapeShare(shape, dx, dy)), colour, hollow);
                            sum += shade * shade.a;
                            a += shade.a;
                        }
                    }
                    // Each point's colour by how much of it shows, so the parts' borders blend as they lie on the map.
                    pixels[y * side + x] = a > 0f ? Skin.Alpha(sum / a, a / (points * points)) : Skin.Alpha(Skin.OnAccent, 0f);
                }
            }
            var texture = Kept.Add(new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry seen spot", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            made = Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f)));
            Pictures[key] = made;
            return made;
        }

        /// <summary>The dots' pictures in a picture of the game's or a kind's own, one drawn for each colour and picture.</summary>
        private static readonly Dictionary<(Color32, int, bool, bool), Sprite> PicturedMarks = new Dictionary<(Color32, int, bool, bool), Sprite>();

        /// <summary>
        /// The picture a kept line's mark stands in (<see cref="KeptMarks"/>): its colour in its
        /// shape, or a picture inside the ring (<see cref="KeptMarkPictures"/>); where the picture
        /// cannot be had, as a square's tab asks for a kind's own without naming the line, the
        /// colour in the round. The dungeons' in the accent.
        /// </summary>
        public static Sprite MarkPicture(int mark, string kept, bool hollow = false)
        {
            var colour = Skin.MarkColour(mark);
            var picture = mark >= KeptMarks.Most ? KeptMarkPictures.Of(mark, kept) : null;
            if (picture == null) return Picture(colour, KeptMarks.ShapeOf(mark), hollow);
            var pictured = Pictured(colour, picture, own: KeptMarks.IsOwn(mark), hollow);
            return pictured != null ? pictured : Picture(colour, MarkShape.Circle, hollow);
        }

        /// <summary>
        /// A mark standing in a picture, drawn once for each colour and picture: inside the ring and
        /// its halo, as every dot has, a dark disc holding one of the game's map pictures in the
        /// colour, or the colour's disc holding a kind's own picture as the game draws it; a place
        /// the server told of with the picture faint and the disc's edge in the colour. None where
        /// the picture cannot be copied.
        /// </summary>
        private static Sprite Pictured(Color colour, Sprite picture, bool own, bool hollow)
        {
            var key = ((Color32)colour, picture.GetInstanceID(), own, hollow);
            if (PicturedMarks.TryGetValue(key, out var made)) return made;
            const int side = 64, points = 4;
            var (w, h) = KeptMarkPictures.Fit(picture, Mathf.RoundToInt(side * (own ? 0.6f : 0.58f)));
            var drawn = KeptMarkPictures.Pixels(picture, w, h);
            if (drawn == null)
            {
                PicturedMarks[key] = null;
                return null;
            }
            var backing = own && !hollow ? colour : Skin.Alpha(Skin.OnAccent, 0.92f);
            var pixels = new Color32[side * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var sum = Skin.Alpha(Skin.OnAccent, 0f) * 0f;
                    var a = 0f;
                    for (var py = 0; py < points; py++)
                    {
                        for (var px = 0; px < points; px++)
                        {
                            var dx = (x + (px + 0.5f) / points - side / 2f) / (side / 2f);
                            var dy = (y + (py + 0.5f) / points - side / 2f) / (side / 2f);
                            var share = SeenOnMap.ShapeShare(MarkShape.Circle, dx, dy);
                            var part = SeenOnMap.DotAt(share);
                            // The disc fills the ring's inside, the map's gap with it, so the picture stands as large as it can.
                            var shade = share > 1f ? Skin.Alpha(Skin.OnAccent, 0f)
                                : part == DotPart.Halo ? Skin.Alpha(Skin.OnAccent, 0.55f)
                                : part == DotPart.Ring ? Skin.Text
                                : hollow && part == DotPart.Edge ? colour
                                : backing;
                            sum += shade * shade.a;
                            a += shade.a;
                        }
                    }
                    pixels[y * side + x] = a > 0f ? Skin.Alpha(sum / a, a / (points * points)) : Skin.Alpha(Skin.OnAccent, 0f);
                }
            }
            // The picture over the disc, in the middle; the game's tinted in the colour, as its grey pictures take one.
            int left = (side - w) / 2, bottom = (side - h) / 2;
            var faint = hollow ? 0.6f : 1f;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var p = drawn[y * w + x];
                    if (p.a == 0) continue;
                    Color over = p;
                    if (!own) over *= colour;
                    var at = (bottom + y) * side + left + x;
                    Color under = pixels[at];
                    var t = p.a / 255f * faint;
                    pixels[at] = Skin.Alpha(under + (over - under) * t, Mathf.Max(under.a, t));
                }
            }
            var texture = Kept.Add(new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry pictured spot", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            made = Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f)));
            PicturedMarks[key] = made;
            return made;
        }

        /// <summary>The colour a part of a dot is drawn in; a hollow one, a place the server told of, dark inside its edge in the colour.</summary>
        private static Color Shade(DotPart part, Color colour, bool hollow)
        {
            switch (part)
            {
                case DotPart.Disc: return hollow ? Skin.Alpha(Skin.OnAccent, 0.7f) : colour;
                case DotPart.Edge: return hollow ? colour : Skin.OnAccent;
                case DotPart.Halo: return Skin.Alpha(Skin.OnAccent, 0.55f);
                case DotPart.Ring: return Skin.Text;
                default: return Skin.Alpha(Skin.OnAccent, 0f);
            }
        }
    }
}
