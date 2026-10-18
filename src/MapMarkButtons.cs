using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Scry
{
    /// <summary>
    /// While Valheim's large map is open, a small mark of Scry's just above the entrance of each
    /// dungeon the character has been in in the world played (<see cref="DungeonWalks"/>),
    /// placed as the game places its pins (<see cref="MapMarks"/>). Each is a button of its own on
    /// the map's pin layer, so its click goes to Scry before the map, which would tick a pin of
    /// the player's near it: a click closes the map and opens Scry on the dungeon's page, its map
    /// on the stage. Its tip, the game's own, gives its name and how far it was walked. It is no
    /// pin of the game's: nothing is added to the player's pins, saved with the world or shared.
    /// No rooms are drawn on the map. <c>DungeonMarksOnMap</c> turns the marks off.
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

        /// <summary>The pin layer the marks stand on, and the maps' stamp they were last listed by.</summary>
        private static RectTransform _layer;
        private static int _keptStamp = -1;
        private static string _keptWorld;
        private static Sprite _sprite;

        /// <summary>How many marks stood on the map at its last frame open, for the self-test.</summary>
        [Diagnostic]
        public static int MarksShown { get; private set; }

        /// <summary>Clicks a mark as the mouse does, for the self-test.</summary>
        [Diagnostic]
        public static void ClickForTest(int mark)
        {
            if (mark >= 0 && mark < Marks.Count && Marks[mark].Made.activeSelf) Open(Marks[mark].Id);
        }

        public static void Forget()
        {
            foreach (var mark in Marks)
            {
                if (mark.Made != null) Object.Destroy(mark.Made);
            }
            Marks.Clear();
            Kept.Clear();
            _layer = null;
            _keptStamp = -1;
            _keptWorld = null;
            MarksShown = 0;
        }

        /// <summary>Each frame: the marks placed where the large map shows them while it is open; put away, and nothing more, while it is not.</summary>
        public static void Update()
        {
            var map = Minimap.instance;
            if (map == null || map.m_mode != Minimap.MapMode.Large || !Settings.DungeonMarksOnMap)
            {
                PutAway();
                return;
            }
            var world = CompendiumMap.World;
            if (map.m_pinRootLarge != _layer)
            {
                Forget();
                _layer = map.m_pinRootLarge;
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
            var layer = image.rectTransform.rect;
            var size = MapMarks.Size(map.m_pinSizeLarge);
            var lift = MapMarks.Lift(map.m_pinSizeLarge);
            var shown = 0;
            foreach (var (id, kept) in Kept)
            {
                if (!MapMarks.Place(kept.Entrance, map.m_pixelSize, map.m_textureSize, (window.xMin, window.yMin, window.width, window.height), (layer.width, layer.height), out var at)) continue;
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
        }

        /// <summary>A location's name as the catalog shows it, else as its prefab is called.</summary>
        private static string Shown(string location)
        {
            var entry = WorldCatalog.Find(EntryKeys.For(Kind.Location, location));
            return entry != null ? entry.ShownName : LocationWords.RoomName(location);
        }

        private static void PutAway()
        {
            if (MarksShown == 0) return;
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
            var picture = made.AddComponent<Image>();
            picture.sprite = MarkSprite();
            picture.preserveAspect = true;
            var button = made.AddComponent<Button>();
            button.targetGraphic = picture;
            var mark = new Mark { Made = made, Rect = rect };
            button.onClick.AddListener(() => Guard.Run(Feature.DungeonMarksOnMap, "opening a dungeon's map from Valheim's map", () => Open(mark.Id)));
            mark.Tip = WithTip(made, map);
            Marks.Add(mark);
            return mark;
        }

        /// <summary>The game's own tip for a mark, its look taken from a tip the map already has; none where it has none.</summary>
        private static UITooltip WithTip(GameObject made, Minimap map)
        {
            var model = map.m_largeRoot != null ? map.m_largeRoot.GetComponentInChildren<UITooltip>(true) : null;
            if (model == null || model.m_tooltipPrefab == null) return null;
            var tip = made.AddComponent<UITooltip>();
            tip.m_tooltipPrefab = model.m_tooltipPrefab;
            return tip;
        }

        /// <summary>Closes the map and opens Scry on the dungeon's page, its map on the stage.</summary>
        private static void Open(string id)
        {
            var kept = DungeonWalks.Maps.Of(id);
            if (kept == null) return;
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
            const int side = 32;
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
                    var arch = dy > 2f ? Mathf.Sqrt(dx * dx + (dy - 2f) * (dy - 2f)) <= 6f : Mathf.Abs(dx) <= 6f && dy >= -8f;
                    pixels[y * side + x] = r > 15.5f ? clear : r > 13.5f ? dark : arch ? dark : accent;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _sprite = Scry.Kept.Add(Sprite.Create(texture, new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f)));
            return _sprite;
        }
    }
}
