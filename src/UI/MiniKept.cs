using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scry
{
    /// <summary>
    /// The kinds kept on Valheim's map (<see cref="MapKept"/>) on its minimap too, where the
    /// settings ask (<c>DungeonMarksOnMinimap</c>): each kind's dots at the minimap's pins' size,
    /// first on its pins' layer, so every pin stands over them; the dungeons' marks stand there by
    /// their own (<c>MapMarkButtons</c>); no squares and no names, as the minimap is small. The
    /// minimap follows the character, so the dots are placed again as it moves: only those inside
    /// its window stand, and a few are made a frame.
    /// </summary>
    internal static class MiniKept
    {
        static MiniKept() => WorldCaches.Register(nameof(MiniKept), Forget);

        private static RectTransform _layer, _holder;
        private static readonly List<(Image Picture, RectTransform Rect)> Dots = new List<(Image, RectTransform)>();
        private static int _live;
        private static (Rect Window, Vector2 Area, int Kept, float Size) _placedFor;

        /// <summary>How many dots may be made in a frame, the rest in the frames after, so a minimap opening on many holds no frame.</summary>
        private const int MostMadeAFrame = 40;

        /// <summary>How many dots stood on the minimap at its last frame, for the self-test.</summary>
        [ForTest]
        public static int DotsShown => _live;

        /// <summary>Each frame, once the map has moved: the kept kinds' dots inside the minimap's window stood where it shows them; put away otherwise.</summary>
        public static void Update()
        {
            var map = Minimap.instance;
            var small = map != null && map.m_mode == Minimap.MapMode.Small && Settings.DungeonMarksOnMinimap;
            if (small) MapKept.Refresh();
            if (!small || MapKept.Dots.Count == 0)
            {
                PutAway();
                return;
            }
            // First under every pin, or, where the settings put every mark over the pins, last on their names' layer.
            var always = MapLook.DotsOver(Settings.MarksOverPins) && map.m_pinNameRootSmall != null;
            var layer = always ? map.m_pinNameRootSmall : map.m_pinRootSmall;
            if (layer != _layer || (_layer != null && _holder == null))
            {
                Forget();
                _layer = layer;
                if (_layer != null) MakeHolder();
            }
            if (_holder == null) return;
            if (!_holder.gameObject.activeSelf) _holder.gameObject.SetActive(true);
            if (always ? _holder.GetSiblingIndex() < _layer.childCount - 2 : _holder.GetSiblingIndex() != 0)
            {
                if (always) _holder.SetAsLastSibling();
                else _holder.SetAsFirstSibling();
            }
            var pipsOver = MapLook.PipsOver(Settings.MarksOverPins, Settings.PipsOverPins) && !always && map.m_pinNameRootSmall != null;
            var image = map.m_mapImageSmall;
            var window = image.uvRect;
            var area = image.rectTransform.rect;
            var size = SeenOnMap.MiniDotSize(map.m_pinSizeSmall, MapLook.DotScale(Settings.MinimapDotSize));
            var placing = (window, area.size, MapKept.Version, size);
            if (placing == _placedFor) return;
            GatherStacks(map, window, area, size);
            var standing = 0;
            var made = 0;
            var allMade = true;
            var like = map.m_pinPrefab != null ? map.m_pinPrefab.transform as RectTransform : null;
            // The pips over the minimap's pins where the settings ask, on a holder last on their names' layer.
            Pips.Begin(pipsOver ? OverPins(map) : _holder);
            // Only the dots near enough to fall in the window are placed: the minimap shows a few hundred metres round the character.
            var player = Player.m_localPlayer;
            var reach = window.width * map.m_textureSize * map.m_pixelSize * 0.75f;
            var (px, pz) = player != null ? (player.transform.position.x, player.transform.position.z) : (0f, 0f);
            var dots = MapKept.Dots;
            for (var i = 0; i < dots.Count; i++)
            {
                var (x, z, picture, _, _, _) = dots[i];
                if (player != null && (Mathf.Abs(x - px) > reach || Mathf.Abs(z - pz) > reach)) continue;
                if (!MapMarks.Place(new Vec3(x, 0f, z), map.m_pixelSize, map.m_textureSize, (window.xMin, window.yMin, window.width, window.height), (area.width, area.height), out var at)) continue;
                // Only where the map shows the ground, as on the large map.
                if (!CompendiumMap.ShownAt(map, x, z)) continue;
                if (standing == Dots.Count)
                {
                    if (made >= MostMadeAFrame || !MakeDot(map))
                    {
                        allMade = false;
                        break;
                    }
                    made++;
                }
                Stand(Dots[standing++], new Vector2(at.X, at.Y), size, picture);
                // A stack's pips under its first dot, as on the large map; the minimap takes no pointer, so nothing fans out.
                var count = Stacks.AnchorOf(i) == i ? Stacks.CountAt(i) : 0;
                if (count > 1 && !Pips.Put(new Vector2(at.X, at.Y), size, count, like)) allMade = false;
            }
            Pips.End();
            for (var i = standing; i < _live && i < Dots.Count; i++) Dots[i].Picture.gameObject.SetActive(false);
            _live = standing;
            // Placed again next frame while dots are still being made.
            _placedFor = allMade ? placing : default;
        }

        private static readonly MarkStacks Stacks = new MarkStacks();
        private static readonly StackPips Pips = new StackPips("Scry minimap stack pips");
        private static (int Kept, int Step) _stackedFor;

        /// <summary>How near two dots stand to touch at the minimap's zoom, in metres, for the self-test.</summary>
        [ForTest]
        public static float TouchForTest => _touch;

        private static float _touch;

        /// <summary>How many pips stand under stacks on the minimap, for the self-test.</summary>
        [ForTest]
        public static int PipsShown => Pips.Shown;

        /// <summary>The kept dots gathered into stacks again as what is kept or the minimap's zoom changes, by where they lie, as on the large map.</summary>
        private static void GatherStacks(Minimap map, Rect window, Rect area, float size)
        {
            var touch = size * MarkStacks.Touching * window.width * map.m_textureSize * map.m_pixelSize / Mathf.Max(1f, area.width);
            var key = (MapKept.Version, Mathf.RoundToInt(Mathf.Log(Mathf.Max(0.001f, touch)) * 50f));
            _touch = touch;
            if (key == _stackedFor) return;
            _stackedFor = key;
            var dots = MapKept.Dots;
            Stacks.Gather(dots.Count, i => (dots[i].X, dots[i].Z), touch);
        }

        private static void Stand((Image Picture, RectTransform Rect) dot, Vector2 at, float size, Sprite picture)
        {
            if (dot.Rect.anchoredPosition != at) dot.Rect.anchoredPosition = at;
            var sized = new Vector2(size, size);
            if (dot.Rect.sizeDelta != sized) dot.Rect.sizeDelta = sized;
            if (dot.Picture.sprite != picture) dot.Picture.sprite = picture;
            if (!dot.Picture.gameObject.activeSelf) dot.Picture.gameObject.SetActive(true);
        }

        private static void PutAway()
        {
            if (_holder != null && _holder.gameObject.activeSelf) _holder.gameObject.SetActive(false);
            if (_over != null && _over.gameObject.activeSelf) _over.gameObject.SetActive(false);
            _placedFor = default;
        }

        /// <summary>What holds the pips over the minimap's pins: last on their names' layer, which shares the pins' layer's room.</summary>
        private static RectTransform _over;

        private static RectTransform OverPins(Minimap map)
        {
            var names = map.m_pinNameRootSmall;
            if (_over == null || _over.parent != names)
            {
                if (_over != null) Object.Destroy(_over.gameObject);
                _over = StackPips.Holder(names, "Scry minimap marks over pins");
            }
            if (!_over.gameObject.activeSelf) _over.gameObject.SetActive(true);
            if (_over.GetSiblingIndex() != names.childCount - 1) _over.SetAsLastSibling();
            return _over;
        }

        /// <summary>What holds the dots: first on the minimap's pins' layer and as big as it.</summary>
        private static void MakeHolder()
        {
            var made = new GameObject("Scry kept on the minimap", typeof(RectTransform));
            _holder = (RectTransform)made.transform;
            _holder.SetParent(_layer, false);
            _holder.SetAsFirstSibling();
            _holder.anchorMin = Vector2.zero;
            _holder.anchorMax = Vector2.one;
            _holder.offsetMin = Vector2.zero;
            _holder.offsetMax = Vector2.zero;
        }

        /// <summary>A new dot, laid out as the game lays out its pins, taking no rays; false where the map has no pin to follow.</summary>
        private static bool MakeDot(Minimap map)
        {
            var pin = map.m_pinPrefab != null ? map.m_pinPrefab.transform as RectTransform : null;
            if (pin == null) return false;
            var made = new GameObject("Scry kept spot", typeof(RectTransform));
            var rect = (RectTransform)made.transform;
            rect.SetParent(_holder, false);
            rect.anchorMin = pin.anchorMin;
            rect.anchorMax = pin.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            var picture = made.AddComponent<Image>();
            picture.raycastTarget = false;
            made.SetActive(false);
            Dots.Add((picture, rect));
            return true;
        }

        public static void Forget()
        {
            Pips.Forget();
            if (_over != null) Object.Destroy(_over.gameObject);
            _over = null;
            _stackedFor = default;
            if (_holder != null) Object.Destroy(_holder.gameObject);
            Dots.Clear();
            _holder = null;
            _layer = null;
            _live = 0;
            _placedFor = default;
        }
    }
}
