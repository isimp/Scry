using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scry
{
    /// <summary>
    /// Show on map's squares (B): for a place shown, the 512 m squares the world's server told hold
    /// one in ground the character explored or saw on a shared map but never came near
    /// (<see cref="PlaceSquares"/>), hatched with a dashed edge in the dots' colour. Each is cut to
    /// explored ground: it stands in a stencil (<see cref="Mask"/>) made of the map's own explored
    /// pixels under it (<see cref="PlaceSquares.MaskPixels"/>), so the fog keeps hiding what was
    /// never seen and the edge stops where the fog starts. The hatch and the dashes repeat at a
    /// fixed size on the screen however near the map is zoomed. The squares stand on the pins'
    /// layer under the dots and every pin, clipped to the map's window (<see cref="RectMask2D"/>);
    /// one holding more than one place carries their count at its corner in the game's pin-name
    /// writing, on the names' layer. Made a few a frame, as the dots are, and placed again only as
    /// the map moves.
    /// </summary>
    internal static class MapSquares
    {
        /// <summary>Leaving a world lets go of the squares (<see cref="WorldCaches"/>).</summary>
        static MapSquares() => WorldCaches.Register(nameof(MapSquares), Forget);

        /// <summary>A square made: its stencil of explored ground, its hatch and four dashed edges inside it, and its count's name.</summary>
        private sealed class Square
        {
            public RectTransform Rect;
            public RawImage Mask;
            public RawImage Hatch;
            public RawImage[] Edges;
            public GameObject Badge;
            public RectTransform BadgeRect;
            public (int X, int Z) MaskedFor = (int.MinValue, int.MinValue);
            public int Said;
        }

        /// <summary>How far apart the hatch's lines are, the dashes' length and gap, and how thick the edge is, in the screen's points.</summary>
        private const float HatchEvery = 9f, DashEvery = 11f, EdgeWidth = 1.5f;

        private static IReadOnlyList<(int X, int Z, int Left)> _shown = new List<(int, int, int)>();
        private static RectTransform _layer, _holder, _names, _badges;
        private static readonly List<Square> Made = new List<Square>();

        /// <summary>The square each made one stands for, kept while the kind is shown so its mask is made once, and those standing for none.</summary>
        private static readonly Dictionary<(int X, int Z), Square> Assigned = new Dictionary<(int X, int Z), Square>();
        private static readonly Stack<Square> Free = new Stack<Square>();
        private static int _standing;
        private static (Rect Window, Vector2 Area, IReadOnlyList<(int X, int Z, int Left)> Shown, Color Colour) _placedFor;
        private static bool _allMade;
        private static Texture2D _hatch, _dashAcross, _dashAlong;
        private static readonly FrameShare Share = new FrameShare(2.0);

        /// <summary>How many squares stood on the map at its last frame open, and how many carried a count, for the self-test.</summary>
        [ForTest]
        public static int SquaresShown => _standing;

        [ForTest]
        public static int CountsShown
        {
            get
            {
                var shown = 0;
                foreach (var square in Assigned.Values) if (square.Rect.gameObject.activeSelf && square.Badge != null && square.Badge.activeSelf) shown++;
                return shown;
            }
        }

        /// <summary>Whether every square stands first on the pins' layer under the dots, clipped to the map's window, for the self-test.</summary>
        [Diagnostic]
        public static bool UnderDotsForTest => _holder != null && _holder.parent == _layer && _holder.GetSiblingIndex() == 0 && _holder.GetComponent<RectMask2D>() != null;

        /// <summary>
        /// Each standing square's fog mask against the map's explored pixels under it, for the
        /// self-test: how many pixels were read, how many the mask has otherwise, and how many of
        /// them lie in fog (none where nothing in view touches fog); a mask laid on its square
        /// other than its pixels lie under it counts all its pixels wrong.
        /// </summary>
        [Diagnostic]
        public static (int Read, int Wrong, int Fog) MaskAgainstMapForTest()
        {
            var map = Minimap.instance;
            var (own, shared) = CompendiumMap.ExploredBits(map);
            int read = 0, wrong = 0, fog = 0;
            if (map == null) return (0, 0, 0);
            foreach (var pair in Assigned)
            {
                var square = pair.Value;
                if (!square.Rect.gameObject.activeSelf || !(square.Mask.texture is Texture2D texture)) continue;
                var (fromX, fromY, side, u, v, span) = PlaceSquares.MaskPixels(pair.Key.X, pair.Key.Z, map.m_pixelSize, map.m_textureSize);
                var laid = square.Mask.uvRect;
                if (square.MaskedFor != pair.Key || Mathf.Abs(laid.x - u) > 1e-4f || Mathf.Abs(laid.y - v) > 1e-4f || Mathf.Abs(laid.width - span) > 1e-4f || Mathf.Abs(laid.height - span) > 1e-4f)
                {
                    read += side * side;
                    wrong += side * side;
                    continue;
                }
                var pixels = texture.GetPixels32();
                for (var y = 0; y < side; y++)
                {
                    for (var x = 0; x < side; x++)
                    {
                        var explored = Explored(own, shared, map.m_textureSize, fromX + x, fromY + y);
                        read++;
                        if (!explored) fog++;
                        if (pixels[y * side + x].a > 0 != explored) wrong++;
                    }
                }
            }
            return (read, wrong, fog);
        }

        /// <summary>The squares to shade for the kind shown, in place of those before; none takes them all away.</summary>
        public static void Show(IReadOnlyList<(int X, int Z, int Left)> squares)
        {
            _shown = squares ?? new List<(int, int, int)>();
            _placedFor = default;
            // Each made square free for the squares now shown, its mask made again, as explored ground may have grown since.
            foreach (var square in Assigned.Values)
            {
                Away(square);
                square.MaskedFor = (int.MinValue, int.MinValue);
                Free.Push(square);
            }
            Assigned.Clear();
        }

        /// <summary>Each frame the map shows a kind: the squares placed where it shows them, in the colour asked.</summary>
        public static void Update(Minimap map, Color colour)
        {
            var layer = map.m_pinRootLarge;
            if (layer != _layer || (_layer != null && _holder == null))
            {
                ForgetMade();
                _layer = layer;
                _names = map.m_pinNameRootLarge;
                if (_layer != null) MakeHolders();
            }
            if (_holder == null) return;
            if (_shown.Count == 0)
            {
                PutAway();
                return;
            }
            if (!_holder.gameObject.activeSelf) _holder.gameObject.SetActive(true);
            if (_badges != null && !_badges.gameObject.activeSelf) _badges.gameObject.SetActive(true);
            // Under the dots, which stand first on the layer as they are made: first again.
            if (_holder.GetSiblingIndex() != 0) _holder.SetAsFirstSibling();
            var image = map.m_mapImageLarge;
            var window = image.uvRect;
            var area = image.rectTransform.rect;
            var placing = (window, area.size, _shown, colour);
            if (_allMade && placing == _placedFor) return;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var made = 0;
            var standing = 0;
            var allMade = true;
            var (own, shared) = CompendiumMap.ExploredBits(map);
            foreach (var (x, z, left) in _shown)
            {
                Assigned.TryGetValue((x, z), out var square);
                if (!PlaceSquares.OnMap(x, z, map.m_pixelSize, map.m_textureSize, (window.xMin, window.yMin, window.width, window.height), (area.width, area.height), out var at))
                {
                    // Out of the window: put away, its mask kept for when it comes back.
                    if (square != null) Away(square);
                    continue;
                }
                if (square == null)
                {
                    // Making a square or its mask is a piece of the frame's share; the rest wait for the frames after, those made standing meanwhile.
                    var pieceFrom = Since(started);
                    if (!Share.MayBegin(pieceFrom, made) || (square = Free.Count > 0 ? Free.Pop() : Make(map)) == null)
                    {
                        allMade = false;
                        continue;
                    }
                    MaskFor(square, x, z, map, own, shared);
                    Assigned[(x, z)] = square;
                    Share.Took(Since(started) - pieceFrom);
                    made++;
                }
                Stand(square, at, left, colour, area.size);
                standing++;
            }
            _standing = standing;
            _placedFor = placing;
            _allMade = allMade;
        }

        /// <summary>A square stood where the map shows it: its hatch and dashes at their size on the screen, its count at its upper right corner where it holds more than one.</summary>
        private static void Stand(Square square, (float X, float Y, float W, float H) at, int left, Color colour, Vector2 area)
        {
            var size = new Vector2(at.W, at.H);
            square.Rect.anchoredPosition = new Vector2(at.X, at.Y);
            if (square.Rect.sizeDelta != size) square.Rect.sizeDelta = size;
            square.Hatch.uvRect = new Rect(0f, 0f, at.W / HatchEvery, at.H / HatchEvery);
            square.Hatch.color = colour;
            for (var e = 0; e < 4; e++)
            {
                var edge = square.Edges[e];
                var across = e < 2;
                edge.rectTransform.sizeDelta = across ? new Vector2(at.W, EdgeWidth) : new Vector2(EdgeWidth, at.H);
                edge.uvRect = across ? new Rect(0f, 0f, at.W / DashEvery, 1f) : new Rect(0f, 0f, 1f, at.H / DashEvery);
                edge.color = colour;
            }
            if (!square.Rect.gameObject.activeSelf) square.Rect.gameObject.SetActive(true);
            if (square.Badge == null) return;
            if (left > 1)
            {
                if (square.Said != left)
                {
                    PinNames.SetText(square.Badge, DungeonMapWords.OnPlate(Numbers.Count(left), ColorUtility.ToHtmlStringRGBA(Skin.Alpha(Skin.TipFill, 0.92f))));
                    square.Said = left;
                }
                // At the square's corner, only while that corner is in the map's window: the names' layer clips nothing.
                var corner = new Vector2(at.X + at.W - 10f, at.Y + at.H - 10f);
                var inWindow = corner.x >= 0f && corner.y >= 0f && corner.x <= area.x && corner.y <= area.y;
                square.BadgeRect.anchoredPosition = corner;
                if (square.Badge.activeSelf != inWindow) square.Badge.SetActive(inWindow);
            }
            else if (square.Badge.activeSelf) square.Badge.SetActive(false);
        }

        private static void Away(Square square)
        {
            if (square.Rect.gameObject.activeSelf) square.Rect.gameObject.SetActive(false);
            if (square.Badge != null && square.Badge.activeSelf) square.Badge.SetActive(false);
        }

        /// <summary>Puts every square away at once, by what holds them.</summary>
        public static void PutAway()
        {
            if (_holder != null && _holder.gameObject.activeSelf) _holder.gameObject.SetActive(false);
            if (_badges != null && _badges.gameObject.activeSelf) _badges.gameObject.SetActive(false);
            _standing = 0;
            _placedFor = default;
        }

        public static void Forget()
        {
            ForgetMade();
            _shown = new List<(int, int, int)>();
        }

        /// <summary>Lets go of the squares made and what holds them, their masks with them.</summary>
        private static void ForgetMade()
        {
            foreach (var square in Made) if (square.Mask != null && square.Mask.texture != null) Object.Destroy(square.Mask.texture);
            if (_holder != null) Object.Destroy(_holder.gameObject);
            if (_badges != null) Object.Destroy(_badges.gameObject);
            Made.Clear();
            Assigned.Clear();
            Free.Clear();
            _holder = null;
            _badges = null;
            _layer = null;
            _names = null;
            _standing = 0;
            _placedFor = default;
            _allMade = false;
        }

        /// <summary>What holds the squares, first on the pins' layer and as big as it, clipped to it; and what holds their counts, first on the names' layer.</summary>
        private static void MakeHolders()
        {
            _holder = Stretched("Scry place squares", _layer);
            _holder.SetAsFirstSibling();
            _holder.gameObject.AddComponent<RectMask2D>();
            if (_names == null) return;
            _badges = Stretched("Scry place square counts", _names);
            _badges.SetAsFirstSibling();
        }

        private static RectTransform Stretched(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>A new square in what holds them: its stencil, hatch and edges taking no rays, so a click on it is the map's.</summary>
        private static Square Make(Minimap map)
        {
            var square = new Square();
            square.Rect = Child("Scry place square", _holder, Vector2.zero, Vector2.zero);
            square.Rect.pivot = Vector2.zero;
            square.Mask = square.Rect.gameObject.AddComponent<RawImage>();
            square.Mask.raycastTarget = false;
            square.Rect.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            square.Hatch = Picture("Scry place square hatch", square.Rect, Vector2.zero, Vector2.one, Hatch());
            square.Edges = new[]
            {
                Picture("Scry place square edge", square.Rect, new Vector2(0f, 0f), new Vector2(0f, 0f), DashAcross()),
                Picture("Scry place square edge", square.Rect, new Vector2(0f, 1f), new Vector2(0f, 1f), DashAcross()),
                Picture("Scry place square edge", square.Rect, new Vector2(0f, 0f), new Vector2(0f, 0f), DashAlong()),
                Picture("Scry place square edge", square.Rect, new Vector2(1f, 0f), new Vector2(1f, 0f), DashAlong()),
            };
            // Each edge lies inside the square along its side.
            square.Edges[0].rectTransform.pivot = new Vector2(0f, 0f);
            square.Edges[1].rectTransform.pivot = new Vector2(0f, 1f);
            square.Edges[2].rectTransform.pivot = new Vector2(0f, 0f);
            square.Edges[3].rectTransform.pivot = new Vector2(1f, 0f);
            if (_badges != null)
            {
                square.Badge = PinNames.Make(map, _badges, "Scry place square count");
                square.BadgeRect = square.Badge != null ? square.Badge.transform as RectTransform : null;
            }
            Made.Add(square);
            return square;
        }

        private static RectTransform Child(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RawImage Picture(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Texture texture)
        {
            var rect = Child(name, parent, anchorMin, anchorMax);
            var picture = rect.gameObject.AddComponent<RawImage>();
            picture.texture = texture;
            picture.raycastTarget = false;
            return picture;
        }

        /// <summary>A square's stencil: the map's explored pixels under it, the character's own or from shared maps, made again only for another square.</summary>
        private static void MaskFor(Square square, int x, int z, Minimap map, BitArray own, BitArray shared)
        {
            var (fromX, fromY, side, u, v, span) = PlaceSquares.MaskPixels(x, z, map.m_pixelSize, map.m_textureSize);
            if (!(square.Mask.texture is Texture2D texture) || texture.width != side)
            {
                if (square.Mask.texture != null) Object.Destroy(square.Mask.texture);
                texture = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry place square mask", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                square.Mask.texture = texture;
            }
            var pixels = new Color32[side * side];
            var size = map.m_textureSize;
            Color32 shown = Skin.Light(1f), hidden = Skin.Light(0f);
            for (var py = 0; py < side; py++)
            {
                for (var px = 0; px < side; px++)
                {
                    pixels[py * side + px] = Explored(own, shared, size, fromX + px, fromY + py) ? shown : hidden;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            square.Mask.uvRect = new Rect(u, v, span, span);
            square.MaskedFor = (x, z);
        }

        private static bool Explored(BitArray own, BitArray shared, int size, int x, int y)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return false;
            var i = y * size + x;
            return own != null && i < own.Length && own[i] || shared != null && i < shared.Length && shared[i];
        }

        /// <summary>The hatch, a tile of one diagonal line on a faint fill, repeated across a square.</summary>
        private static Texture2D Hatch()
        {
            if (_hatch != null) return _hatch;
            const int side = 16;
            var pixels = new Color32[side * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    // Distance across the tile's diagonal, wrapping, so tiles join into unbroken lines.
                    var d = Mathf.Abs(((x - y) % side + side) % side - side / 2f);
                    var line = Mathf.Clamp01(1.6f - Mathf.Abs(d - side / 2f));
                    pixels[y * side + x] = Skin.Light(Mathf.Max(0.16f, 0.75f * line));
                }
            }
            _hatch = Tile("Scry place square hatch", side, side, pixels);
            return _hatch;
        }

        /// <summary>The edge's dashes, a tile of a dash and its gap, across a square and along it.</summary>
        private static Texture2D DashAcross() => _dashAcross != null ? _dashAcross : _dashAcross = Dash(across: true);
        private static Texture2D DashAlong() => _dashAlong != null ? _dashAlong : _dashAlong = Dash(across: false);

        private static Texture2D Dash(bool across)
        {
            const int length = 11, on = 7;
            var pixels = new Color32[length];
            for (var i = 0; i < length; i++) pixels[i] = Skin.Light(i < on ? 1f : 0f);
            return across ? Tile("Scry place square dash", length, 1, pixels) : Tile("Scry place square dash", 1, length, pixels);
        }

        private static Texture2D Tile(string name, int width, int height, Color32[] pixels)
        {
            var texture = Kept.Add(new Texture2D(width, height, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat });
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static double Since(long started) => (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }
}
