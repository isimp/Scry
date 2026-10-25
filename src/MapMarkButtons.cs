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
    /// the character has been in in the world played (<see cref="DungeonWalks"/>), and of each lying
    /// in a ground whose pin is on (<see cref="DungeonMarks"/>), placed as the game
    /// places its pins and as big as one (<see cref="MapMarks"/>), on the layer of the pins' names
    /// so no name covers it. The pointer never reaches that layer through the game's event system,
    /// so each mark is a picture taking no clicks of its own: the pointer is read against it as the
    /// game's map and tips read it, with no camera, and the map's own click, made on the map under
    /// it, is handed to the mark under the pointer (<see cref="MapMarkClick"/>), so a click on a mark
    /// never ticks or adds a pin of the player's. A click closes the map and opens Scry on the
    /// dungeon's page, its map on the stage where anyone walked it, the page alone for an entrance.
    /// Its name and who walked it how far stand under it as
    /// the game names a pin, in a copy of the game's pin name, while the pointer rests on it, drawn
    /// above every other name on the map, and
    /// its name alone at all times where <c>DungeonMarkNamesAlways</c> is on. It is no pin of the game's: nothing is added to
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

            /// <summary>The words under it, a copy of the game's own pin name, shown while the pointer rests on it, and its name alone where names always show; none where the map has none.</summary>
            public GameObject Label;
            public RectTransform LabelRect;
            public Image Picture;
            public string Id;

            /// <summary>How it looks (<see cref="MarkLook"/>) and which glyph it carries (<see cref="MarkGlyph"/>), as last drawn.</summary>
            public MarkLook Look;
            public MarkGlyph Glyph;

            /// <summary>Its name's size as the game's pin names have it, and the scale it was last drawn at; 0 until read.</summary>
            public float NameSize;
            public float NameScaled;

            /// <summary>Its words as last set, for the self-test.</summary>
            public string Said;

            /// <summary>Its name alone, as the game names a pin, for names always shown.</summary>
            public string Plain;

            /// <summary>What its label holds now; none until set.</summary>
            public string Showing;
        }

        /// <summary>What the game makes a pin's name from (<c>Minimap.m_pinNamePrefab</c>), its words set as it sets them.</summary>
        private static readonly AccessTools.FieldRef<Minimap, GameObject> PinNamePrefab = AccessTools.FieldRefAccess<Minimap, GameObject>("m_pinNamePrefab");

        private static readonly List<Mark> Marks = new List<Mark>();
        private static readonly List<DungeonMark> Kept = new List<DungeonMark>();

        /// <summary>The layer the marks stand on, and the stamps of what they were last listed from.</summary>
        private static RectTransform _layer;
        private static (int Walked, int InGround, int Sources, int Server) _keptStamp = (-1, -1, -1, -1);

        /// <summary>Whether the server was asked for the marked dungeons' records since the map last opened.</summary>
        private static bool _askedServer;
        private static string _keptWorld;

        /// <summary>How many of the marks shown are of dungeons walked on this server shown with insides, hollow, for the self-test.</summary>
        [Diagnostic]
        public static int HollowShown { get; private set; }

        /// <summary>How many of the marks shown are of dungeons' entrances alone, for the self-test.</summary>
        [Diagnostic]
        public static int EntrancesShown { get; private set; }

        /// <summary>The dungeon listed first for the marks, as last listed, for the self-test; none.</summary>
        [Diagnostic]
        public static string FirstListedForTest => Kept.Count > 0 ? Kept[0].Id : null;

        /// <summary>A mark's words, for the self-test; none for one not shown.</summary>
        [Diagnostic]
        public static string SaidForTest(int mark) => mark >= 0 && mark < Marks.Count && Marks[mark].Made.activeSelf ? Marks[mark].Said : null;

        /// <summary>What a mark's label shows, for the self-test; none for one not shown or whose label is put away.</summary>
        [Diagnostic]
        public static string ShowingForTest(int mark) => mark >= 0 && mark < Marks.Count && Marks[mark].Label != null && Marks[mark].Label.activeSelf ? Marks[mark].Showing : null;

        /// <summary>How a mark looks, for the self-test; none for one not shown.</summary>
        [Diagnostic]
        public static MarkLook? LookForTest(int mark) => mark >= 0 && mark < Marks.Count && Marks[mark].Made.activeSelf ? Marks[mark].Look : (MarkLook?)null;

        /// <summary>Which glyph a mark carries, for the self-test; none for one not shown.</summary>
        [Diagnostic]
        public static MarkGlyph? GlyphForTest(int mark) => mark >= 0 && mark < Marks.Count && Marks[mark].Made.activeSelf ? Marks[mark].Glyph : (MarkGlyph?)null;

        /// <summary>How big a mark is drawn, in the map's units, for the self-test; 0 for one not shown.</summary>
        [Diagnostic]
        public static float SizeForTest(int mark) => mark >= 0 && mark < Marks.Count && Marks[mark].Made.activeSelf ? Marks[mark].Rect.rect.width : 0f;

        /// <summary>How faint a mark is drawn, for the self-test; 0 for one not shown.</summary>
        [Diagnostic]
        public static float AlphaForTest(int mark) => mark >= 0 && mark < Marks.Count && Marks[mark].Made.activeSelf && Marks[mark].Picture != null ? Marks[mark].Picture.color.a : 0f;

        /// <summary>The mark the pointer rests on, whose words are shown; none.</summary>
        private static Mark _hovered;

        /// <summary>Where the pointer was as the left button was last pressed, so a drag of the map let go over a mark is no click of it (<see cref="MapMarks.Clicked"/>).</summary>
        private static Vector2 _pressedAt;

        /// <summary>The marks' size on the screen at the last frame open, for the click's reach.</summary>
        private static float _sizeOnScreen;

        /// <summary>How many marks stood on the map at its last frame open, for the self-test.</summary>
        [Diagnostic]
        public static int MarksShown { get; private set; }

        /// <summary>Whether the pointer rests on a mark, its words shown, for the self-test.</summary>
        [Diagnostic]
        public static bool NameShownForTest => _hovered?.Label != null && _hovered.Label.activeSelf;

        /// <summary>Whether the words of the mark the pointer rests on stand above every other name on the map, for the self-test.</summary>
        public static bool NameOnTopForTest => NameShownForTest && OnTop(_hovered.LabelRect);

        private static bool OnTop(RectTransform rect) => rect != null && rect.parent != null && rect.GetSiblingIndex() == rect.parent.childCount - 1;

        /// <summary>Where a mark's middle is on the screen, for the self-test to put the pointer on it; none for one not shown.</summary>
        [Diagnostic]
        public static Vector2? MarkCenterForTest(int mark)
        {
            if (mark < 0 || mark >= Marks.Count || !Marks[mark].Made.activeSelf) return null;
            var rect = Marks[mark].Rect;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        /// <summary>Which mark a point of the screen stands on, as the pointer is read for its words and the click; -1 for none, for the self-test.</summary>
        [Diagnostic]
        public static int MarkAtForTest(Vector2 screen) => Marks.IndexOf(MarkAt(screen));

        /// <summary>Clicks the map at a point of the screen, as the map's own click hands a mark its click there, for the self-test.</summary>
        [Diagnostic]
        public static bool ClickAtForTest(Vector2 screen)
        {
            _pressedAt = screen;
            return TakeClick(screen);
        }

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
            HideName();
            foreach (var mark in Marks)
            {
                if (mark.Made != null) Object.Destroy(mark.Made);
                if (mark.Label != null) Object.Destroy(mark.Label);
            }
            Marks.Clear();
            Kept.Clear();
            _layer = null;
            _keptStamp = (-1, -1, -1, -1);
            _keptWorld = null;
            _askedServer = false;
            MarksShown = 0;
            HollowShown = 0;
            EntrancesShown = 0;
        }

        /// <summary>
        /// Each frame: the marks placed where the large map shows them while it is open, the words
        /// of the one the pointer rests on, and on the small map, where the settings ask, the marks
        /// of dungeons you walked at its pins' size, no words; put away, and nothing more, otherwise.
        /// </summary>
        public static void Update()
        {
            var map = Minimap.instance;
            var large = map != null && map.m_mode == Minimap.MapMode.Large;
            var small = map != null && map.m_mode == Minimap.MapMode.Small && Settings.DungeonMarksOnMinimap;
            if (map == null || !(large || small) || !Settings.DungeonMarksOnMap)
            {
                PutAway();
                return;
            }
            var world = CompendiumMap.World;
            var names = large ? map.m_pinNameRootLarge : map.m_pinNameRootSmall;
            var layer = names != null ? names : large ? map.m_pinRootLarge : map.m_pinRootSmall;
            if (layer != _layer)
            {
                Forget();
                _layer = layer;
            }
            var maps = DungeonWalks.Maps;
            var book = CompendiumKeeper.Book;
            var stamp = (maps.Stamp, DungeonsInGround.Stamp, book != null ? book.SourcesVersion : -1, GroupMapsLink.Group.Stamp);
            if (_keptStamp != stamp || _keptWorld != world)
            {
                Kept.Clear();
                Kept.AddRange(DungeonMarks.Of(book, maps.OfWorld(world), DungeonsInGround.Of, RoomKinds.Of, GroupMapsLink.Group.Of));
                _keptStamp = stamp;
                _keptWorld = world;
                // Their words told afresh, as the days in them may have moved.
                foreach (var mark in Marks) mark.Id = null;
            }
            // As the map opens, the server is asked for the records of the dungeons marked, for the last day anyone walked each.
            if (!_askedServer)
            {
                _askedServer = true;
                foreach (var location in Kept.Select(k => k.Map.Location).Distinct()) GroupMapsLink.Ask(location);
            }

            var image = large ? map.m_mapImageLarge : map.m_mapImageSmall;
            var window = image.uvRect;
            var area = image.rectTransform.rect;
            var scale = Settings.DungeonMarkSize;
            var size = MapMarks.Size(large ? map.m_pinSizeLarge : map.m_pinSizeSmall, scale);
            var shown = 0;
            var hollow = 0;
            var entrances = 0;
            foreach (var kept in Kept)
            {
                if (!MapMarks.Shown(kept.Look, Settings.DungeonMarksWalkedByYou, Settings.DungeonMarksWalkedOnServer, Settings.DungeonMarkEntrances)) continue;
                if (small && !MapMarks.OnSmallMap(kept.Look)) continue;
                if (!MapMarks.Place(kept.Map.Entrance, map.m_pixelSize, map.m_textureSize, (window.xMin, window.yMin, window.width, window.height), (area.width, area.height), out var at)) continue;
                var mark = MarkFor(shown++, map);
                if (mark == null) break;
                // On its entrance: a pin placed there stands under it.
                mark.Rect.anchoredPosition = new Vector2(at.X, at.Y);
                mark.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
                mark.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);
                if (kept.Look == MarkLook.Server || kept.Look == MarkLook.ServerWhole) hollow++;
                if (kept.Look == MarkLook.Entrance) entrances++;
                var glyph = MarkGlyphs.Of(kept.Map.Location);
                if (mark.Id != kept.Id || mark.Look != kept.Look || mark.Glyph != glyph)
                {
                    mark.Id = kept.Id;
                    mark.Look = kept.Look;
                    mark.Glyph = glyph;
                    if (mark.Picture != null) mark.Picture.sprite = ScryPanel.MarkPicture(glyph, kept.Look);
                    mark.Said = Said(kept);
                    mark.Plain = Shown(kept.Map.Location);
                    mark.Showing = null;
                }
                // An entrance alone stands faint until the pointer rests on it.
                if (mark.Picture != null) mark.Picture.color = new Color(1f, 1f, 1f, MapMarks.Alpha(kept.Look, mark == _hovered));
                // Its words stand where it stands, as the game's pin names do, the name's own layout putting them under it, as big as the mark is scaled.
                if (mark.LabelRect != null) mark.LabelRect.anchoredPosition = mark.Rect.anchoredPosition;
                SizeName(mark, MapMarks.NameScale(scale));
                if (!mark.Made.activeSelf) mark.Made.SetActive(true);
                if (small) Hide(mark.Label);
                else ShowWords(mark);
            }
            for (var i = shown; i < Marks.Count; i++) Hide(Marks[i]);
            MarksShown = shown;
            HollowShown = hollow;
            EntrancesShown = entrances;
            _sizeOnScreen = shown > 0 ? ScreenSize(Marks[0].Rect) : 0f;
            if (Input.GetMouseButtonDown(0)) _pressedAt = ZInput.pointerPosition;
            Hover();
        }

        /// <summary>How tall a mark stands on the screen, the canvas's scale with it.</summary>
        private static float ScreenSize(RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            return Mathf.Abs(Corners[1].y - Corners[0].y);
        }

        /// <summary>A mark's corners as read, kept so no frame makes a new array.</summary>
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>
        /// Shows the words of the mark the pointer rests on, as the pointer comes onto it, and takes
        /// them away as it leaves; none while the map is held, as when it is dragged under the pointer.
        /// </summary>
        private static void Hover()
        {
            // The small map takes no pointer: nothing is pointed at there.
            var over = Input.GetMouseButton(0) || Minimap.instance == null || Minimap.instance.m_mode != Minimap.MapMode.Large ? null : MarkAt(ZInput.pointerPosition);
            if (over == _hovered)
            {
                // The game adds a pin's name to the layer as the pin comes into view, above what stands there: the words hovered go back on top.
                if (over?.LabelRect != null && over.Label.activeSelf && !OnTop(over.LabelRect)) over.LabelRect.SetAsLastSibling();
                return;
            }
            var was = _hovered;
            _hovered = over;
            if (was != null) ShowWords(was);
            if (over != null) ShowWords(over);
        }

        private static void HideName()
        {
            var was = _hovered;
            _hovered = null;
            if (was != null) ShowWords(was);
        }

        /// <summary>A mark's label as it should stand now (<see cref="MapMarks.WordsOf"/>): all its words while the pointer rests on it, its name alone where names always show, else put away.</summary>
        private static void ShowWords(Mark mark)
        {
            if (mark.Label == null) return;
            var words = MapMarks.WordsOf(mark == _hovered, Settings.MarkNamesAlways);
            if (words == MarkWords.None)
            {
                if (mark.Label.activeSelf) mark.Label.SetActive(false);
                return;
            }
            var said = words == MarkWords.All ? mark.Said : mark.Plain;
            if (!ReferenceEquals(said, mark.Showing))
            {
                SetText(mark.Label, said);
                mark.Showing = said;
            }
            if (!mark.Label.activeSelf) mark.Label.SetActive(true);
            // The words hovered stand above every other name on the map, as a hover should.
            if (words == MarkWords.All && mark.LabelRect != null) mark.LabelRect.SetAsLastSibling();
        }

        /// <summary>Puts a mark away with its label.</summary>
        private static void Hide(Mark mark)
        {
            if (mark.Made != null && mark.Made.activeSelf) mark.Made.SetActive(false);
            Hide(mark.Label);
        }

        /// <summary>Puts a mark's label away.</summary>
        private static void Hide(GameObject label)
        {
            if (label != null && label.activeSelf) label.SetActive(false);
        }

        /// <summary>Draws a mark's name as big as the mark is scaled, from the size the game's pin names have, read once.</summary>
        private static void SizeName(Mark mark, float scale)
        {
            if (mark.Label == null || Mathf.Approximately(mark.NameScaled, scale)) return;
            var text = mark.Label.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c != null && IsText(c.GetType()));
            if (text == null) return;
            var property = text.GetType().GetProperty("fontSize");
            if (property == null || property.PropertyType != typeof(float)) return;
            if (mark.NameSize <= 0f) mark.NameSize = (float)property.GetValue(text);
            property.SetValue(text, mark.NameSize * scale);
            mark.NameScaled = scale;
        }

        /// <summary>A mark's words, by its look, on a dark plate: how far you walked it, how far anyone on this server did, or whether anyone did.</summary>
        private static string Said(DungeonMark kept) => DungeonMapWords.OnPlate(SaidOf(kept), ColorUtility.ToHtmlStringRGBA(new Color(Skin.TipFill.r, Skin.TipFill.g, Skin.TipFill.b, 0.92f)));

        private static string SaidOf(DungeonMark kept)
        {
            var name = Shown(kept.Map.Location);
            var accent = ColorUtility.ToHtmlStringRGB(Skin.Accent);
            var (walked, rooms) = RoomKinds.Walked(kept.Map);
            switch (kept.Look)
            {
                case MarkLook.Yours:
                case MarkLook.Whole:
                    // Rooms others on this server walked that you did not: all of you together, and your share.
                    var (together, of, byYou) = DungeonMaps.WalkedTogether(kept.Map, kept.Server, RoomKinds.Of);
                    return together > byYou ? DungeonMapWords.TogetherLabel(name, together, of, byYou, accent) : DungeonMapWords.MarkLabel(name, walked, rooms, accent);
                case MarkLook.Server:
                case MarkLook.ServerWhole: return DungeonMapWords.ServerLabel(name, walked, rooms, accent, kept.Map.Traced.Count);
                default: return DungeonMapWords.EntranceLabel(name, kept.Map.Walked.Count > 0, ColorUtility.ToHtmlStringRGB(Skin.Faint));
            }
        }

        /// <summary>
        /// Sets a mark's label's words (<see cref="DungeonMapWords.MarkLabel"/>, in the accent, or
        /// its name alone). The game's pin name writes with TextMeshPro, which Scry does not build
        /// against, so its text is set by the name the game's own code sets it by.
        /// </summary>
        private static void SetText(GameObject label, string said)
        {
            var text = label.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c != null && IsText(c.GetType()));
            if (text == null) return;
            text.GetType().GetProperty("text")?.SetValue(text, said);
        }

        /// <summary>Whether a component writes with TextMeshPro (<c>TMP_Text</c> or what derives from it).</summary>
        private static bool IsText(System.Type type)
        {
            for (var t = type; t != null; t = t.BaseType) if (t.Name == "TMP_Text") return true;
            return false;
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
            if (mark == null || !MapMarks.Clicked((_pressedAt.x, _pressedAt.y), (screen.x, screen.y), _sizeOnScreen)) return false;
            Open(mark.Id, mark.Look);
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
            _askedServer = false;
            HollowShown = 0;
            EntrancesShown = 0;
            if (MarksShown == 0) return;
            HideName();
            foreach (var mark in Marks) Hide(mark);
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
            picture.sprite = ScryPanel.MarkPicture(MarkGlyph.Doorway, MarkLook.Yours);
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            var mark = new Mark { Made = made, Rect = rect, Picture = picture };
            // Its words, a copy of the game's own pin name on the same layer, hidden until the pointer rests on it.
            var named = PinNamePrefab(map);
            if (named != null)
            {
                mark.Label = Object.Instantiate(named, _layer);
                mark.Label.name = "Scry dungeon mark name";
                mark.LabelRect = mark.Label.transform as RectTransform;
                mark.Label.SetActive(false);
            }
            Marks.Add(mark);
            return mark;
        }

        /// <summary>
        /// Closes the map and opens Scry on the dungeon's page, its own rooms on the stage as far as
        /// they were walked, every one in the fog where nobody walked it; one whose area never
        /// loaded has no rooms anywhere yet, so an example stands in for it, its note saying why.
        /// </summary>
        private static void Open(string id, MarkLook look)
        {
            var kept = DungeonWalks.Maps.Of(id) ?? DungeonsInGround.MapOf(id);
            if (kept == null) return;
            HideName();
            Minimap.instance.OrNull()?.SetMapMode(Minimap.MapMode.Small);
            var laidOut = kept.Rooms != null && kept.Rooms.Count > 0;
            if (laidOut) ExampleLayouts.OpenMapWhenShown(kept.Location, id);
            else ExampleLayouts.StandInFor(kept.Location);
            // Its page at once where the catalog is read, else once it is.
            Session.ShowEntry(EntryKeys.For(Kind.Location, kept.Location));
            Stage.Inside = true;
            OpenedForTest = (id, look, laidOut);
        }

        /// <summary>The mark last opened: its dungeon, how it looked and whether its own rooms were opened; for the self-test.</summary>
        [Diagnostic]
        public static (string Id, MarkLook Look, bool Rooms) OpenedForTest { get; private set; }
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
