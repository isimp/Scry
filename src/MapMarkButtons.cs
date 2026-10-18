using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Scry
{
    /// <summary>
    /// While Valheim's large map is open, a mark of Scry's just above the entrance of each dungeon
    /// the character has been in in the world played (<see cref="DungeonWalks"/>), placed as the game
    /// places its pins and as big as one (<see cref="MapMarks"/>), on the layer of the pins' names
    /// so no name covers it. The pointer never reaches that layer through the game's event system,
    /// so each mark is a picture taking no clicks of its own: the pointer is read against it as the
    /// game's map and tips read it, with no camera, and the map's own click, made on the map under
    /// it, is handed to the mark under the pointer (<see cref="MapMarkClick"/>), so a click on a mark
    /// never ticks or adds a pin of the player's. A click closes the map and opens Scry on the
    /// dungeon's page, its map on the stage. Its tip, the game's own, gives its name and how far it
    /// was walked, shown as the pointer rests on it. It is no pin of the game's: nothing is added to
    /// the player's pins, saved with the world or shared. No rooms are drawn on the map.
    /// <c>DungeonMarksOnMap</c> turns the marks off.
    /// </summary>
    internal static class MapMarkButtons
    {
        /// <summary>Leaving a world lets go of what is held of it (<see cref="WorldCaches"/>).</summary>
        static MapMarkButtons() => WorldCaches.Register(nameof(MapMarkButtons), Forget);

        private sealed class Mark
        {
            public GameObject Made;
            public RectTransform Rect;
            public UITooltip Tip;
            public string Id;
        }

        private static readonly List<Mark> Marks = new List<Mark>();
        private static readonly List<(string Id, DungeonMap Map)> Kept = new List<(string, DungeonMap)>();

        /// <summary>The layer the marks stand on, and the maps' stamp they were last listed by.</summary>
        private static RectTransform _layer;
        private static int _keptStamp = -1;
        private static string _keptWorld;
        private static Sprite _sprite;

        /// <summary>The look the game's tips take, from a tip of its own; found once.</summary>
        private static GameObject _tipLook;

        /// <summary>The mark the pointer rests on, whose tip is shown; none.</summary>
        private static Mark _hovered;

        /// <summary>How many marks stood on the map at its last frame open, for the self-test.</summary>
        [Diagnostic]
        public static int MarksShown { get; private set; }

        /// <summary>Whether the pointer rests on a mark, its tip asked for, for the self-test.</summary>
        [Diagnostic]
        public static bool TipShownForTest => _hovered != null && _hovered.Tip != null;

        /// <summary>Where a mark's middle is on the screen, for the self-test to put the pointer on it; none for one not shown.</summary>
        [Diagnostic]
        public static Vector2? MarkCenterForTest(int mark)
        {
            if (mark < 0 || mark >= Marks.Count || !Marks[mark].Made.activeSelf) return null;
            var rect = Marks[mark].Rect;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        /// <summary>Which mark a point of the screen stands on, as the pointer is read for the tip and the click; -1 for none, for the self-test.</summary>
        [Diagnostic]
        public static int MarkAtForTest(Vector2 screen) => Marks.IndexOf(MarkAt(screen));

        /// <summary>Clicks the map at a point of the screen, as the map's own click hands a mark its click there, for the self-test.</summary>
        [Diagnostic]
        public static bool ClickAtForTest(Vector2 screen) => TakeClick(screen);

        /// <summary>What the pointer first meets at a mark's middle, as the game's event system finds it, for the self-test.</summary>
        [Diagnostic]
        public static string RaycastForTest(int mark)
        {
            var system = EventSystem.current;
            if (!(MarkCenterForTest(mark) is Vector2 at) || system == null) return null;
            var hits = new List<RaycastResult>();
            system.RaycastAll(new PointerEventData(system) { position = at }, hits);
            return hits.Count > 0 ? hits[0].gameObject.name : "";
        }

        public static void Forget()
        {
            HideTip();
            foreach (var mark in Marks)
            {
                if (mark.Made != null) Object.Destroy(mark.Made);
            }
            Marks.Clear();
            Kept.Clear();
            _layer = null;
            _keptStamp = -1;
            _keptWorld = null;
            _tipLook = null;
            MarksShown = 0;
        }

        /// <summary>Each frame: the marks placed where the large map shows them while it is open, the tip of the one the pointer rests on; put away, and nothing more, while it is not.</summary>
        public static void Update()
        {
            var map = Minimap.instance;
            if (map == null || map.m_mode != Minimap.MapMode.Large || !Settings.DungeonMarksOnMap)
            {
                PutAway();
                return;
            }
            var world = CompendiumMap.World;
            var layer = map.m_pinNameRootLarge != null ? map.m_pinNameRootLarge : map.m_pinRootLarge;
            if (layer != _layer)
            {
                Forget();
                _layer = layer;
            }
            var maps = DungeonWalks.Maps;
            if (_keptStamp != maps.Stamp || _keptWorld != world)
            {
                Kept.Clear();
                foreach (var kept in maps.OfWorld(world)) Kept.Add(kept);
                _keptStamp = maps.Stamp;
                _keptWorld = world;
            }

            var image = map.m_mapImageLarge;
            var window = image.uvRect;
            var area = image.rectTransform.rect;
            var size = MapMarks.Size(map.m_pinSizeLarge);
            var lift = MapMarks.Lift(map.m_pinSizeLarge);
            var shown = 0;
            foreach (var (id, kept) in Kept)
            {
                if (!MapMarks.Place(kept.Entrance, map.m_pixelSize, map.m_textureSize, (window.xMin, window.yMin, window.width, window.height), (area.width, area.height), out var at)) continue;
                var mark = MarkFor(shown++, map);
                if (mark == null) break;
                mark.Rect.anchoredPosition = new Vector2(at.X, at.Y + lift);
                mark.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
                mark.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);
                if (mark.Id != id)
                {
                    mark.Id = id;
                    if (mark.Tip != null)
                    {
                        var (walked, rooms) = DungeonMaps.Walked(kept, _ => null);
                        mark.Tip.Set(Shown(kept.Location), DungeonMapWords.MarkTip(walked, rooms));
                    }
                }
                if (!mark.Made.activeSelf) mark.Made.SetActive(true);
            }
            for (var i = shown; i < Marks.Count; i++)
            {
                if (Marks[i].Made.activeSelf) Marks[i].Made.SetActive(false);
            }
            MarksShown = shown;
            Hover();
        }

        /// <summary>Shows the tip of the mark the pointer rests on, as the pointer comes onto it, and takes it away as it leaves.</summary>
        private static void Hover()
        {
            var over = MarkAt(ZInput.pointerPosition);
            if (over == _hovered) return;
            HideTip();
            _hovered = over;
            if (over?.Tip != null) over.Tip.OnHoverStart(over.Made);
        }

        private static void HideTip()
        {
            if (_hovered?.Tip != null) UITooltip.HideTooltip();
            _hovered = null;
        }

        /// <summary>The mark standing at a point of the screen, read with no camera as the game's map and tips read the pointer; none.</summary>
        private static Mark MarkAt(Vector2 screen)
        {
            for (var i = 0; i < MarksShown && i < Marks.Count; i++)
            {
                if (Marks[i].Made.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(Marks[i].Rect, screen, null)) return Marks[i];
            }
            return null;
        }

        /// <summary>A click of the map's own at a point of the screen: the mark's there, opening its map; false where it is on none.</summary>
        public static bool TakeClick(Vector2 screen)
        {
            var mark = MarkAt(screen);
            if (mark == null) return false;
            Open(mark.Id);
            return true;
        }

        /// <summary>Whether the pointer stands on a mark, so a double-click there adds no pin.</summary>
        public static bool PointerOnMark() => MarkAt(ZInput.pointerPosition) != null;

        /// <summary>A location's name as the catalog shows it, else as its prefab is called.</summary>
        private static string Shown(string location)
        {
            var entry = WorldCatalog.Find(EntryKeys.For(Kind.Location, location));
            return entry != null ? entry.ShownName : LocationWords.RoomName(location);
        }

        private static void PutAway()
        {
            if (MarksShown == 0) return;
            HideTip();
            foreach (var mark in Marks)
            {
                if (mark.Made != null && mark.Made.activeSelf) mark.Made.SetActive(false);
            }
            MarksShown = 0;
        }

        /// <summary>The mark of this place in the list, made the first time, laid out as the game lays out its pins; none where the map has no pin to follow.</summary>
        private static Mark MarkFor(int index, Minimap map)
        {
            if (index < Marks.Count) return Marks[index];
            var pin = map.m_pinPrefab != null ? map.m_pinPrefab.transform as RectTransform : null;
            if (pin == null) return null;
            var made = new GameObject("Scry dungeon mark", typeof(RectTransform));
            var rect = (RectTransform)made.transform;
            rect.SetParent(_layer, false);
            rect.anchorMin = pin.anchorMin;
            rect.anchorMax = pin.anchorMax;
            rect.pivot = pin.pivot;
            // A picture taking no rays, so the map under it gets every click to hand on.
            var picture = made.AddComponent<Image>();
            picture.sprite = MarkSprite();
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            var mark = new Mark { Made = made, Rect = rect };
            mark.Tip = WithTip(made, map);
            Marks.Add(mark);
            return mark;
        }

        /// <summary>The game's own tip for a mark, its look taken from a tip the map has, else from any of the game's; none where none is loaded.</summary>
        private static UITooltip WithTip(GameObject made, Minimap map)
        {
            if (_tipLook == null)
            {
                var model = map.m_largeRoot != null ? map.m_largeRoot.GetComponentsInChildren<UITooltip>(true).FirstOrDefault(t => t.m_tooltipPrefab != null) : null;
                if (model == null) model = Resources.FindObjectsOfTypeAll<UITooltip>().FirstOrDefault(t => t.m_tooltipPrefab != null);
                _tipLook = model != null ? model.m_tooltipPrefab : null;
            }
            if (_tipLook == null) return null;
            var tip = made.AddComponent<UITooltip>();
            tip.m_tooltipPrefab = _tipLook;
            return tip;
        }

        /// <summary>Closes the map and opens Scry on the dungeon's page, its map on the stage.</summary>
        private static void Open(string id)
        {
            var kept = DungeonWalks.Maps.Of(id);
            if (kept == null) return;
            HideTip();
            Minimap.instance.OrNull()?.SetMapMode(Minimap.MapMode.Small);
            Session.Show(null);
            ExampleLayouts.OpenMapWhenShown(kept.Location, id);
            Session.Explorer?.Jump(EntryKeys.For(Kind.Location, kept.Location));
            Stage.Inside = true;
        }

        /// <summary>The mark's picture: an accent disc, outlined dark, with a doorway in it, made once.</summary>
        private static Sprite MarkSprite()
        {
            if (_sprite != null) return _sprite;
            const int side = 64;
            var texture = Scry.Kept.Add(new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Scry dungeon mark", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color32[side * side];
            var accent = (Color32)Skin.Accent;
            var dark = (Color32)Skin.OnAccent;
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
    }

    /// <summary>
    /// The game's map ticks the pin nearest a click (<c>Minimap.OnMapLeftClick</c>) and opens the box
    /// for a new pin on a double-click (<c>Minimap.OnMapDblClick</c>); with the pointer on a dungeon's
    /// mark, the click is the mark's and the map does neither. Should it fail, it stands aside and
    /// leaves the map as it is.
    /// </summary>
    [HarmonyPatch(typeof(Minimap), "OnMapLeftClick")]
    internal static class MapMarkClick
    {
        private static bool _off;

        private static bool Prefix()
        {
            if (_off) return true;
            if (!Guard.Run(Feature.DungeonMarksOnMap, "taking a click on a dungeon's mark", Take, out var taken))
            {
                _off = true;
                return true;
            }
            return !taken;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Take() => MapMarkButtons.TakeClick(ZInput.pointerPosition);
    }

    /// <summary>A double-click on a dungeon's mark adds no pin (<see cref="MapMarkClick"/>).</summary>
    [HarmonyPatch(typeof(Minimap), "OnMapDblClick")]
    internal static class MapMarkDoubleClick
    {
        private static bool _off;

        private static bool Prefix()
        {
            if (_off) return true;
            if (!Guard.Run(Feature.DungeonMarksOnMap, "keeping a double-click on a dungeon's mark", On, out var on))
            {
                _off = true;
                return true;
            }
            return !on;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool On() => MapMarkButtons.PointerOnMark();
    }
}
