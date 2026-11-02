using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scry
{
    /// <summary>
    /// Show on map: a page's kind shown on Valheim's large map, a dot on every spot of it the
    /// character saw in the world played (<see cref="SeenSpotsKeeper"/>), until <see cref="Hide"/>,
    /// until the map opened for it closes, or until it never opens (<see cref="MapShowing"/>). The
    /// button closes Scry and opens the map as its key does, centred on the character. Each dot is
    /// a picture in the kind's list colour with a dark ring, on the layer of the game's pins and
    /// first on it, so pins and their names stand over it and Scry's dungeon marks, on the names'
    /// layer, too; it takes no clicks, so a click on it is the map's. Dots are placed where the map
    /// shows them each frame, smaller as it zooms out (<see cref="SeenOnMap.DotSize"/>); made a few
    /// a frame from a pool, as the dungeon marks are. The strip naming the kind stands in Scry's
    /// look at the map's top left (<see cref="ScryPanel.MapStripGUI"/>).
    /// </summary>
    internal static class MapSpots
    {
        /// <summary>Leaving a world lets go of the dots and what is shown (<see cref="WorldCaches"/>).</summary>
        static MapSpots() => WorldCaches.Register(nameof(MapSpots), Forget);

        private static readonly MapShowing Showing = new MapShowing();

        /// <summary>The strip's words, worked out as the kind is shown, and the dots' colour.</summary>
        private static string _strip;
        private static Color _colour = Color.white;

        /// <summary>
        /// The layer the dots stand on; what holds them there, first on it and as big as it, so
        /// putting them all away or back is one switch; the dots made in it, and how many stand.
        /// </summary>
        private static RectTransform _layer, _holder;
        private static readonly List<(Image Picture, RectTransform Rect)> Dots = new List<(Image, RectTransform)>();
        private static int _standing;

        /// <summary>How many dots are switched on inside what holds them, the first so many, kept while it is put away.</summary>
        private static int _live;

        /// <summary>What the dots were last placed for: the map's window, its size, the spots, their size; placed again only as one changes, or while dots are still being made.</summary>
        private static (Rect Window, Vector2 Area, IReadOnlyList<(int X, int Z)> Spots, float Size) _placedFor;
        private static bool _allMade;

        /// <summary>The page whose kind was last worked out, and its kind, as the page asks each time it is drawn.</summary>
        private static Entry _kindFor;
        private static string _kindOf;

        /// <summary>The dots' picture: a white disc with a dark ring, drawn in each kind's colour.</summary>
        private static Sprite _picture;

        /// <summary>A couple of milliseconds a frame for making dots.</summary>
        private static readonly FrameShare Share = new FrameShare(2.0);

        /// <summary>How many dots stood on the map at its last frame open, for the self-test.</summary>
        [ForTest]
        public static int DotsShown => _standing;

        /// <summary>How big the dots stood, in the map's units, for the self-test; 0 with none.</summary>
        [Diagnostic]
        public static float DotSizeForTest => _standing > 0 ? Dots[0].Rect.rect.width : 0f;

        /// <summary>The kind shown, for the self-test; none.</summary>
        [ForTest]
        public static string KindShown => Showing.Kind;

        /// <summary>Where the strip was last drawn, in the GUI's own coordinates, for the self-test.</summary>
        [Diagnostic]
        public static Rect StripRectForTest => _stripAt;

        /// <summary>Whether every dot stands first on the pins' layer, under each of the game's pins, for the self-test; false with none.</summary>
        [Diagnostic]
        public static bool DotsUnderPinsForTest
        {
            get
            {
                if (_layer == null || _holder == null || Dots.Count == 0 || _holder.parent != _layer || _holder.GetSiblingIndex() != 0) return false;
                foreach (var (_, rect) in Dots) if (rect.parent != _holder) return false;
                return true;
            }
        }

        /// <summary>The strip's words while the large map shows a kind; none otherwise.</summary>
        public static string Strip => Showing.Showing && LargeOpen ? _strip : null;

        private static bool LargeOpen => Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large;

        /// <summary>
        /// The kind a page's spots are kept by: a place's own key, else the kind its prefab is kept
        /// by, a deposit's broken form by the whole one (<see cref="SpotKinds"/>); none for what
        /// is never kept, as a tree, an item or a dungeon's room.
        /// </summary>
        public static string KindOf(Entry entry)
        {
            if (ReferenceEquals(entry, _kindFor)) return _kindOf;
            _kindFor = entry;
            _kindOf = entry == null ? null
                : entry.Kind == Kind.Location ? (entry.IsRoom ? null : entry.Key)
                : entry.Source is GameObject prefab && prefab != null ? SpotKinds.Of(prefab.name.GetStableHashCode()) : null;
            return _kindOf;
        }

        /// <summary>Why a kind with so many seen shows nothing now; none where it can show them.</summary>
        public static MapBlock BlockOf(int count) =>
            SeenOnMap.Block(Game.m_noMap, Settings.SeenOnMapReach, Settings.RecordSeenSpots, SeenSpotsKeeper.Unreadable, count);

        /// <summary>A page's kind shown on the map: its spots in the world played, by its name, in its list colour.</summary>
        public static void Show(Entry entry)
        {
            var kind = KindOf(entry);
            if (kind == null) return;
            // A broken form's page shows the whole one's spots, by the whole one's name.
            var named = entry.Kind == Kind.Location ? entry : WorldCatalog.Find(kind) ?? entry;
            Show(kind, named.ShownName, Skin.KindColor(entry.Kind), SeenSpotsKeeper.Of(kind));
        }

        /// <summary>A kind's spots shown on the map, as the button shows them, and the map opened as its key opens it; the self-test gives spots of its own.</summary>
        public static void Show(string kind, string name, Color colour, IReadOnlyList<(int X, int Z)> spots)
        {
            Showing.Show(CompendiumMap.World, kind, name, spots);
            _colour = colour;
            var player = Player.m_localPlayer;
            var nearest = player != null ? SeenOnMap.Nearest(spots, player.transform.position.x, player.transform.position.z) : null;
            _strip = nearest is (float away, float dx, float dz)
                ? SpotWords.Strip(name, spots.Count, away, SpotWords.Direction(dx, dz))
                : SpotWords.Strip(name, spots.Count, null, null);
            Minimap.instance.OrNull()?.SetMapMode(Minimap.MapMode.Large);
        }

        public static void Hide()
        {
            Showing.Hide();
            PutAway();
        }

        public static void Forget()
        {
            Showing.Hide();
            ForgetDots();
            _strip = null;
            _kindFor = null;
            _kindOf = null;
        }

        /// <summary>Lets go of the dots made and what holds them, for another map or another world.</summary>
        private static void ForgetDots()
        {
            if (_holder != null) Object.Destroy(_holder.gameObject);
            Dots.Clear();
            _holder = null;
            _layer = null;
            _standing = 0;
            _live = 0;
            _placedFor = default;
            _allMade = false;
        }

        /// <summary>Each frame, once the map has moved: the dots placed where the large map shows them while a kind is shown; put away otherwise.</summary>
        public static void Update()
        {
            var map = Minimap.instance;
            var large = LargeOpen;
            if (Showing.Showing) Showing.Tick(large, CompendiumMap.World);
            if (!Showing.Showing || !large)
            {
                PutAway();
                return;
            }
            var layer = map.m_pinRootLarge;
            if (layer != _layer || (_layer != null && _holder == null))
            {
                ForgetDots();
                _layer = layer;
                if (_layer != null) MakeHolder();
            }
            if (_holder == null) return;
            if (!_holder.gameObject.activeSelf) _holder.gameObject.SetActive(true);
            var image = map.m_mapImageLarge;
            var window = image.uvRect;
            var area = image.rectTransform.rect;
            var size = SeenOnMap.DotSize(map.m_pinSizeLarge, window.height);
            // A map standing still with every dot made costs nothing more: nothing moved.
            var placing = (window, area.size, Showing.Spots, size);
            if (_allMade && placing == _placedFor) return;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var made = 0;
            var standing = 0;
            var allMade = true;
            for (var i = 0; i < Showing.Spots.Count; i++)
            {
                var (x, z) = Showing.Spots[i];
                if (!MapMarks.Place(new Vec3(x, 0f, z), map.m_pixelSize, map.m_textureSize, (window.xMin, window.yMin, window.width, window.height), (area.width, area.height), out var at)) continue;
                if (standing == Dots.Count)
                {
                    // Making a dot is a piece of the frame's share; the rest wait for the frames after.
                    var pieceFrom = Since(started);
                    if (!Share.MayBegin(pieceFrom, made) || !MakeDot(map))
                    {
                        allMade = false;
                        break;
                    }
                    Share.Took(Since(started) - pieceFrom);
                    made++;
                }
                Stand(Dots[standing++], new Vector2(at.X, at.Y), size);
            }
            // Those no spot holds now put away, only as fewer stand than before.
            for (var i = standing; i < _live && i < Dots.Count; i++) Away(Dots[i].Picture);
            _live = standing;
            _standing = standing;
            _placedFor = placing;
            _allMade = allMade;
        }

        /// <summary>A dot stood where the map shows its spot, as big and in the colour asked, each set only as it changes.</summary>
        private static void Stand((Image Picture, RectTransform Rect) dot, Vector2 at, float size)
        {
            if (dot.Rect.anchoredPosition != at) dot.Rect.anchoredPosition = at;
            var sized = new Vector2(size, size);
            if (dot.Rect.sizeDelta != sized) dot.Rect.sizeDelta = sized;
            if (dot.Picture.color != _colour) dot.Picture.color = _colour;
            if (!dot.Picture.gameObject.activeSelf) dot.Picture.gameObject.SetActive(true);
        }

        private static void Away(Image picture)
        {
            if (picture != null && picture.gameObject.activeSelf) picture.gameObject.SetActive(false);
        }

        /// <summary>Puts every dot away at once, by what holds them, each left as it is inside; placed afresh as a kind is shown again.</summary>
        private static void PutAway()
        {
            if (_holder != null && _holder.gameObject.activeSelf) _holder.gameObject.SetActive(false);
            _standing = 0;
            _placedFor = default;
        }

        /// <summary>What holds the dots: first on the pins' layer, so every pin stands over them, and as big as it, so a dot placed in it stands where it would on the layer.</summary>
        private static void MakeHolder()
        {
            var made = new GameObject("Scry seen spots", typeof(RectTransform));
            _holder = (RectTransform)made.transform;
            _holder.SetParent(_layer, false);
            _holder.SetAsFirstSibling();
            _holder.anchorMin = Vector2.zero;
            _holder.anchorMax = Vector2.one;
            _holder.offsetMin = Vector2.zero;
            _holder.offsetMax = Vector2.zero;
        }

        /// <summary>A new dot in what holds them, laid out as the game lays out its pins; false where the map has no pin to follow.</summary>
        private static bool MakeDot(Minimap map)
        {
            var pin = map.m_pinPrefab != null ? map.m_pinPrefab.transform as RectTransform : null;
            if (pin == null) return false;
            var made = new GameObject("Scry seen spot", typeof(RectTransform));
            var rect = (RectTransform)made.transform;
            rect.SetParent(_holder, false);
            rect.anchorMin = pin.anchorMin;
            rect.anchorMax = pin.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            // A picture taking no rays, so the map under it gets every click.
            var picture = made.AddComponent<Image>();
            picture.sprite = Picture();
            picture.raycastTarget = false;
            Dots.Add((picture, rect));
            return true;
        }

        /// <summary>The dots' picture, drawn once: a white disc for the kind's colour, its ring dark in any colour.</summary>
        private static Sprite Picture()
        {
            if (_picture != null) return _picture;
            const int side = 32;
            var pixels = new Color32[side * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var dx = x + 0.5f - side / 2f;
                    var dy = y + 0.5f - side / 2f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    // Soft at the edge: a pixel's coverage by how far its middle lies inside the disc.
                    var a = Mathf.Clamp01(side / 2f - r);
                    var ring = r > side / 2f - 5f;
                    // The ring in the dark the dungeon marks are outlined in.
                    pixels[y * side + x] = ring ? Skin.Alpha(Skin.OnAccent, a) : Skin.Light(a);
                }
            }
            var texture = Kept.Add(new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry seen spot", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _picture = Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f)));
            return _picture;
        }

        /// <summary>Milliseconds since a timestamp.</summary>
        private static double Since(long started) => (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        /// <summary>Where the strip stood on the screen as last drawn, in the GUI's own coordinates, and the frame it was drawn in.</summary>
        private static Rect _stripAt;
        private static int _stripFrame = -10;

        /// <summary>Where the strip was drawn, for its clicks to be kept from the map under it.</summary>
        public static void StripDrawn(Rect at)
        {
            _stripAt = at;
            _stripFrame = Time.frameCount;
        }

        /// <summary>Whether the pointer stands on the strip, drawn this frame or the one before (a Hide pressed takes it away before the map hears the click), so the map takes no click there.</summary>
        public static bool PointerOnStrip()
        {
            if (Time.frameCount > _stripFrame + 1) return false;
            var pointer = ZInput.pointerPosition;
            return _stripAt.Contains(new Vector2(pointer.x, Screen.height - pointer.y));
        }

        /// <summary>Where the large map's picture stands on the screen, in the GUI's own coordinates; none while it is shut.</summary>
        public static Rect? MapOnScreen()
        {
            var map = Minimap.instance;
            if (!LargeOpen || map.m_mapImageLarge == null) return null;
            map.m_mapImageLarge.rectTransform.GetWorldCorners(Corners);
            // An overlay canvas: its corners are the screen's points, y up.
            return Rect.MinMaxRect(Corners[0].x, Screen.height - Corners[1].y, Corners[2].x, Screen.height - Corners[0].y);
        }

        private static readonly Vector3[] Corners = new Vector3[4];
    }
}
