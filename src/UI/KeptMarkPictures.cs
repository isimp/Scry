using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The pictures a kept kind's mark may stand in (<see cref="KeptMarks"/>): the game's own map
    /// pictures, found by name among the map's pin and place pictures, and a kind's own picture,
    /// the icon <see cref="KeptMarks.OwnPictureOf"/> names. A game picture's pixels are copied
    /// through the graphics card once, as the game's textures cannot be read directly.
    /// </summary>
    internal static class KeptMarkPictures
    {
        /// <summary>The game's map pictures a mark may stand in, by their names, in the picker's order.</summary>
        public static readonly string[] Names =
        {
            "mapicon_fire", "mapicon_house", "mapicon_hammer", "mapicon_pin", "mapicon_portal",
            "mapicon_bogwitch_camp", "mapicon_trader", "mapicon_hildir_wagon", "mapicon_start", "mapicon_upgradestation",
        };

        /// <summary>Nothing showing, what a picture's bare pixels hold.</summary>
        private static readonly Color Clear = Skin.Alpha(Skin.OnAccent, 0f) * 0f;

        /// <summary>Leaving a world lets go of each kept line's own picture, worked out again from the next world's catalog.</summary>
        static KeptMarkPictures() => WorldCaches.Register(nameof(KeptMarkPictures), () =>
        {
            OwnByKept.Clear();
            _ownFor = null;
        });

        private static readonly Sprite[] Found = new Sprite[KeptMarks.Pictures];
        private static bool _looked;

        /// <summary>Each kept line's own picture and the name of what it shows, worked out once per catalog.</summary>
        private static readonly Dictionary<string, (Sprite Picture, string Name)> OwnByKept = new Dictionary<string, (Sprite, string)>(System.StringComparer.Ordinal);
        private static EntryCatalog _ownFor;

        /// <summary>Each picture's pixels at a size, copied once.</summary>
        private static readonly Dictionary<(int, int, int), Color32[]> Copied = new Dictionary<(int, int, int), Color32[]>();

        /// <summary>The picture a mark stands in for a kept line: one of the game's, the line's own, or none for a shape, or where it cannot be found.</summary>
        public static Sprite Of(int mark, string kept)
        {
            if (KeptMarks.PictureOf(mark) is int picture) return Game(picture);
            return KeptMarks.IsOwn(mark) ? OwnOf(kept) : null;
        }

        /// <summary>One of the game's map pictures; none until the map is up, or where the game has none by its name.</summary>
        public static Sprite Game(int picture)
        {
            if (picture < 0 || picture >= KeptMarks.Pictures) return null;
            if (!_looked && Minimap.instance != null)
            {
                _looked = true;
                Guard.Run(Feature.MarkPictures, "game map pictures", LookUp);
            }
            return Found[picture];
        }

        /// <summary>How many of the game's map pictures were found, for the self-test.</summary>
        [Diagnostic]
        public static int GameFoundForTest => Enumerable.Range(0, KeptMarks.Pictures).Count(p => Game(p) != null);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LookUp()
        {
            var map = Minimap.instance;
            var byName = new Dictionary<string, Sprite>(System.StringComparer.Ordinal);
            foreach (var each in map.m_icons) if (each.m_icon != null) byName[each.m_icon.name] = each.m_icon;
            foreach (var each in map.m_locationIcons) if (each.m_icon != null) byName[each.m_icon.name] = each.m_icon;
            if (Names.Any(n => !byName.ContainsKey(n)))
            {
                // Pictures the map's lists do not hold, as the boss and trader pictures some places give.
                foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (sprite != null && !byName.ContainsKey(sprite.name) && System.Array.IndexOf(Names, sprite.name) >= 0) byName[sprite.name] = sprite;
                }
            }
            for (var i = 0; i < Names.Length; i++) Found[i] = byName.TryGetValue(Names[i], out var sprite) ? sprite : null;
            var missing = Names.Where(n => !byName.ContainsKey(n)).ToList();
            if (missing.Count > 0) Log.Warn($"Map pictures not found in the game: {string.Join(", ", missing)}");
        }

        /// <summary>A kept line's own picture: its first kind's icon, a creature's trophy, a deposit's drop; none for a place.</summary>
        public static Sprite OwnOf(string kept) => Own(kept).Picture;

        /// <summary>The name of what a kept line's own picture shows; none where it has none.</summary>
        public static string OwnNameOf(string kept) => Own(kept).Name;

        private static (Sprite Picture, string Name) Own(string kept)
        {
            if (string.IsNullOrEmpty(kept)) return (null, null);
            var catalog = WorldCatalog.Current;
            if (catalog != _ownFor)
            {
                _ownFor = catalog;
                OwnByKept.Clear();
            }
            if (OwnByKept.TryGetValue(kept, out var known)) return known;
            (Sprite, string) found = (null, null);
            Guard.Run(Feature.MarkPictures, "own map picture", () => found = OwnFrom(kept));
            OwnByKept[kept] = found;
            return found;
        }

        private static (Sprite, string) OwnFrom(string kept)
        {
            var kinds = MapKept.KindsIn(kept);
            var entry = kinds.Length > 0 ? WorldCatalog.Find(kinds[0]) : null;
            if (entry == null) return (null, null);
            var drops = entry.TermLinks("drops");
            var name = KeptMarks.OwnPictureOf(entry.Name, entry.Kind, entry.Icon is Sprite, drops.Select(d => (d.Name, d.Icon is Sprite)));
            var shows = name == null ? null : name == entry.Name ? entry : drops.FirstOrDefault(d => d.Name == name);
            return shows?.Icon is Sprite picture ? (picture, shows.ShownName) : (null, null);
        }

        /// <summary>
        /// A picture's pixels at a size, its shape kept inside it and the rest clear: drawn at twice
        /// the size on the graphics card, read back and each two by two averaged, so its edges
        /// stay smooth; none where it cannot be copied.
        /// </summary>
        public static Color32[] Pixels(Sprite sprite, int width, int height)
        {
            if (sprite == null || sprite.texture == null) return null;
            var key = (sprite.GetInstanceID(), width, height);
            if (Copied.TryGetValue(key, out var made)) return made;
            Guard.Run(Feature.MarkPictures, "copying a map picture", () => made = Copy(sprite, width, height));
            Copied[key] = made;
            return made;
        }

        /// <summary>The size a picture takes inside a box, its sides as they stand to each other.</summary>
        public static (int Width, int Height) Fit(Sprite sprite, int box)
        {
            var rect = sprite.rect;
            if (rect.width <= 0f || rect.height <= 0f) return (box, box);
            var wide = rect.width >= rect.height;
            var w = wide ? box : Mathf.Max(1, Mathf.RoundToInt(box * rect.width / rect.height));
            var h = wide ? Mathf.Max(1, Mathf.RoundToInt(box * rect.height / rect.width)) : box;
            return (w, h);
        }

        private static Color32[] Copy(Sprite sprite, int width, int height)
        {
            var texture = sprite.texture;
            // A picture packed tight among others has no rectangle of its own; its whole frame stands in.
            var rect = sprite.packed && sprite.packingMode == SpritePackingMode.Tight ? sprite.rect : sprite.textureRect;
            var scale = new Vector2(rect.width / texture.width, rect.height / texture.height);
            var offset = new Vector2(rect.x / texture.width, rect.y / texture.height);
            int w2 = width * 2, h2 = height * 2;
            var target = RenderTexture.GetTemporary(w2, h2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var before = RenderTexture.active;
            var read = new Texture2D(w2, h2, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, Clear);
                Graphics.Blit(texture, target, scale, offset);
                RenderTexture.active = target;
                read.ReadPixels(new Rect(0, 0, w2, h2), 0, 0, false);
                var big = read.GetPixels32();
                var pixels = new Color32[width * height];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var sum = Clear;
                        var a = 0f;
                        for (var dy = 0; dy < 2; dy++)
                        {
                            for (var dx = 0; dx < 2; dx++)
                            {
                                Color p = big[(y * 2 + dy) * w2 + x * 2 + dx];
                                sum += p * p.a;
                                a += p.a;
                            }
                        }
                        // Each point's colour by how much of it shows, so the picture's edges blend over the disc.
                        pixels[y * width + x] = a > 0f ? Skin.Alpha(sum / a, a / 4f) : Clear;
                    }
                }
                return pixels;
            }
            finally
            {
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(read);
            }
        }
    }
}
