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
    /// While Valheim's large map is open, a mark of Scry's on the entrance of each dungeon
    /// the character has been in in the world played (<see cref="DungeonWalks"/>), of each place whose
    /// rooms stand in the world it stood in, as Hildir's Sealed Tower, and of each whose entrance
    /// the character saw (<see cref="DungeonMarks"/>, <see cref="SeenSpotsKeeper"/>), placed as the game
    /// places its pins and as big as one (<see cref="MapMarks"/>), on the layer of the pins' names
    /// so no name covers it. The pointer never reaches that layer through the game's event system,
    /// so each mark is a picture taking no clicks of its own: the pointer is read against it as the
    /// game's map and tips read it, with no camera, and the map's own click, made on the map under
    /// it, is handed to the mark under the pointer (<see cref="MapMarkClick"/>), so a click on a mark
    /// never ticks or adds a pin of the player's. A click closes the map and opens Scry on the
    /// dungeon's page, its map on the stage where anyone walked it, the page alone for a place stood in.
    /// Its name and who walked it how far stand under it as
    /// the game names a pin, in a copy of the game's pin name, while the pointer rests on it, drawn
    /// above every other name on the map, and
    /// its name alone at all times where <c>DungeonMarkNamesAlways</c> is on. It is no pin of the game's: nothing is added to
    /// the player's pins, saved with the world or shared. No rooms are drawn on the map.
    /// They stand while the dungeons are kept on the map (<see cref="CompendiumBook.Kept"/>), every
    /// kind or a kind kept from its page; taken off in the title row's list, they go.
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

            /// <summary>Its kind's key, for its look to follow the kinds picked to stand out; and what fades its words with it, laid on once first faded.</summary>
            public string KindKey;
            public CanvasGroup WordsFade;

            /// <summary>Where its spot lies, its mode there, and what that lays on it, as last set.</summary>
            public int SpotX, SpotZ;
            public SpotMode Mode, ModeShown;
            public Image Cross, Ring;
            public float ModeAlpha = -1f;

            /// <summary>How it looks (<see cref="MarkLook"/>) and which glyph it carries (<see cref="MarkGlyph"/>), as last drawn.</summary>
            public MarkLook Look;
            public MarkGlyph Glyph;

            /// <summary>Its words to be told anew, as what they count may have moved; it shows the words it has until then.</summary>
            public bool Retell;

            /// <summary>The frame it was last placed in.</summary>
            public int PlacedIn = -1;

            /// <summary>Where it stands, how big and how faint it is drawn, as last set; set again only as they change.</summary>
            public Vector2 At = new Vector2(float.NaN, float.NaN);
            public float Size = -1f, Alpha = -1f;

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

        private static readonly List<Mark> Marks = new List<Mark>();
        private static readonly List<DungeonMark> Kept = new List<DungeonMark>();

        /// <summary>The layer the marks stand on, and the stamps of what they were last listed from.</summary>
        private static RectTransform _layer;
        private static (int Walked, (int, int) Seen, int Sources, int Server, int Kept, bool Remembered) _keptStamp = (-1, (-1, -1), -1, -1, -1, false);

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
        public static string SaidForTest(int mark) => mark >= 0 && mark < Placed.Count && Placed[mark].Made.activeSelf ? Placed[mark].Said : null;

        /// <summary>What a mark's label shows, for the self-test; none for one not shown or whose label is put away.</summary>
        [Diagnostic]
        public static string ShowingForTest(int mark) => mark >= 0 && mark < Placed.Count && Placed[mark].Label != null && Placed[mark].Label.activeSelf ? Placed[mark].Showing : null;

        /// <summary>How a mark looks, for the self-test; none for one not shown.</summary>
        [Diagnostic]
        public static MarkLook? LookForTest(int mark) => mark >= 0 && mark < Placed.Count && Placed[mark].Made.activeSelf ? Placed[mark].Look : (MarkLook?)null;

        /// <summary>Which glyph a mark carries, for the self-test; none for one not shown.</summary>
        [Diagnostic]
        public static MarkGlyph? GlyphForTest(int mark) => mark >= 0 && mark < Placed.Count && Placed[mark].Made.activeSelf ? Placed[mark].Glyph : (MarkGlyph?)null;

        /// <summary>How big a mark is drawn, in the map's units, for the self-test; 0 for one not shown.</summary>
        [Diagnostic]
        public static float SizeForTest(int mark) => mark >= 0 && mark < Placed.Count && Placed[mark].Made.activeSelf ? Placed[mark].Rect.rect.width : 0f;

        /// <summary>How faint a mark is drawn, for the self-test; 0 for one not shown.</summary>
        [Diagnostic]
        public static float AlphaForTest(int mark) => mark >= 0 && mark < Placed.Count && Placed[mark].Made.activeSelf && Placed[mark].Picture != null ? Placed[mark].Picture.color.a : 0f;

        /// <summary>The first mark standing of a kind, by its place among those placed, for the self-test; -1 for none.</summary>
        [ForTest]
        public static int MarkOfKindForTest(string kind) => Placed.FindIndex(m => m.KindKey == kind && m.Made.activeSelf);

        /// <summary>The mark the pointer rests on, whose words are shown; none.</summary>
        private static Mark _hovered;

        /// <summary>Where the pointer was as the left button was last pressed, so a drag of the map let go over a mark is no click of it (<see cref="MapMarks.Clicked"/>).</summary>
        private static Vector2 _pressedAt;

        /// <summary>The marks' size on the screen at the last frame open, for the click's reach.</summary>
        private static float _sizeOnScreen;

        /// <summary>How many marks stood on the map at its last frame open, for the self-test.</summary>
        [Diagnostic]
        public static int MarksShown { get; private set; }

        /// <summary>The marks standing this frame, in the order they were placed; what the self-test counts by.</summary>
        private static readonly List<Mark> Placed = new List<Mark>();

        /// <summary>Each dungeon's own mark, by its id.</summary>
        private static readonly Dictionary<string, Mark> ById = new Dictionary<string, Mark>(System.StringComparer.Ordinal);

        /// <summary>This frame's marks in view and their ids, and the marks made that none of them holds, kept so no frame makes new lists.</summary>
        private static readonly List<(DungeonMark Kept, (float X, float Y) At)> InView = new List<(DungeonMark, (float, float))>();
        private static readonly HashSet<string> InViewIds = new HashSet<string>(System.StringComparer.Ordinal);
        private static readonly List<Mark> Spare = new List<Mark>();

        /// <summary>A few milliseconds a frame for making marks and telling their words anew.</summary>
        private static readonly FrameShare Share = new FrameShare(4.0);

        /// <summary>Milliseconds since a timestamp.</summary>
        private static double Since(long started) => (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

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
            if (mark < 0 || mark >= Placed.Count || !Placed[mark].Made.activeSelf) return null;
            var rect = Placed[mark].Rect;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        /// <summary>Which mark a point of the screen stands on, as the pointer is read for its words and the click; -1 for none, for the self-test.</summary>
        [Diagnostic]
        public static int MarkAtForTest(Vector2 screen) => Placed.IndexOf(MarkAt(screen));

        /// <summary>Opens what a mark at a point of the screen stands for, as the last line of its menu does, for the self-test.</summary>
        [ForTest]
        public static bool OpenAtForTest(Vector2 screen)
        {
            if (MarkAt(screen) == null || !OpenMenuAt(screen)) return false;
            MapSpots.PickMenuForTest(MapSpots.MenuItems.Count - 1);
            return true;
        }

        /// <summary>A click on the map at a point of the screen, the press noted there, for the self-test.</summary>
        [ForTest]
        public static void PressedForTest(Vector2 screen) => _pressedAt = screen;

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
            _keptStamp = (-1, (-1, -1), -1, -1, -1, false);
            _keptWorld = null;
            _askedServer = false;
            MarksShown = 0;
            Placed.Clear();
            ById.Clear();
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
            var marked = DungeonsKept(CompendiumKeeper.Book);
            if (map != null && marked) Ahead(map);
            // On TargetPortal's map of portals, hiding the map's pins, the marks step aside too.
            if (map == null || !(large || small) || !marked || (large && TargetPortalMap.MarksAside))
            {
                if (!marked) MapSpots.MarkedKindsAre(System.Array.Empty<(string, string, int, int)>());
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
            var stamp = (maps.Stamp, SeenSpotsKeeper.Stamp, book != null ? book.SourcesVersion : -1, GroupMapsLink.Group.Stamp, book != null ? book.KeptVersion : -1, MapKept.Remembered);
            if (_keptStamp != stamp || _keptWorld != world)
            {
                var listing = Timing.Start();
                Kept.Clear();
                Kept.AddRange(DungeonMarks.Of(book, maps.OfWorld(world), SeenDungeons(world), RoomKinds.Of, GroupMapsLink.Group.Of).Where(m => KeptOnMap(book, m.Map.Location)));
                ListMarkedKinds();
                _modesListed = book?.SpotModesVersion ?? 0;
                Timing.Add("marks listed", listing);
                _keptStamp = stamp;
                _keptWorld = world;
                // Their words told afresh, as the days in them may have moved.
                foreach (var mark in Marks) mark.Retell = true;
            }
            // The kinds list counts what is crossed off as it changes.
            if (book != null && book.SpotModesVersion != _modesListed)
            {
                _modesListed = book.SpotModesVersion;
                ListMarkedKinds();
            }
            // As the map opens, the server is asked for the records of the dungeons marked, for the last day anyone walked each.
            if (!_askedServer)
            {
                _askedServer = true;
                foreach (var location in Kept.Where(k => k.Look != MarkLook.Visited).Select(k => k.Map.Location).Distinct()) GroupMapsLink.Ask(location);
            }

            var image = large ? map.m_mapImageLarge : map.m_mapImageSmall;
            var window = image.uvRect;
            var area = image.rectTransform.rect;
            var scale = Settings.DungeonMarkSize;
            var size = MapMarks.Size(large ? map.m_pinSizeLarge : map.m_pinSizeSmall, scale);
            var shown = 0;
            var hollow = 0;
            var entrances = 0;
            // The marks in view this frame, each held by its dungeon's own mark where it has one, so
            // a mark stays its dungeon's as others come into view or go: matched by their order
            // instead, one moved moved all after it to another dungeon's words.
            ListInView(map, small, window, area);
            Placed.Clear();
            var frame = Time.frameCount;
            // Making a mark, or telling its words anew, is a piece of the frame's share, the rest left
            // to the frames after: a ground of hundreds of dungeons made them all in one frame, held
            // for most of a second. One that would show another look waits hidden; one whose words
            // are told anew waits shown with the words it has.
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var pieces = 0;
            foreach (var (kept, at) in InView)
            {
                var glyph = MarkGlyphs.Of(kept.Map.Location);
                ById.TryGetValue(kept.Id, out var mark);
                var other = mark == null || mark.Look != kept.Look || mark.Glyph != glyph;
                var stale = other || mark.Retell;
                var pieceFrom = Since(started);
                if (stale && !Share.MayBegin(pieceFrom, pieces))
                {
                    if (other) continue;
                    stale = false;
                }
                if (mark == null)
                {
                    var making = Timing.Start();
                    mark = Spare.Count > 0 ? Spare[Spare.Count - 1] : MakeMark(map);
                    Timing.Add("marks made", making);
                    if (mark == null) break;
                    if (Spare.Count > 0 && ReferenceEquals(Spare[Spare.Count - 1], mark)) Spare.RemoveAt(Spare.Count - 1);
                    if (mark.Id != null) ById.Remove(mark.Id);
                    mark.Id = kept.Id;
                    ById[kept.Id] = mark;
                }
                Placed.Add(mark);
                mark.PlacedIn = frame;
                if (kept.Look == MarkLook.Server || kept.Look == MarkLook.ServerWhole) hollow++;
                if (kept.Look == MarkLook.Entrance) entrances++;
                shown++;
                if (stale)
                {
                    var telling = Timing.Start();
                    Tell(mark, kept, glyph);
                    Timing.Add("marks told", telling);
                }
                Stand(mark, kept.Look, new Vector2(at.X, at.Y), size, scale, small);
                if (!stale) continue;
                Share.Took(Since(started) - pieceFrom);
                pieces++;
            }
            // What is not placed this frame stands hidden: out of view, or waiting for its look.
            foreach (var mark in Marks) if (mark.PlacedIn != frame) Hide(mark);
            MarksShown = shown;
            HollowShown = hollow;
            EntrancesShown = entrances;
            _sizeOnScreen = shown > 0 ? ScreenSize(Placed[0].Rect) : 0f;
            if (Input.GetMouseButtonDown(0)) _pressedAt = ZInput.pointerPosition;
            Hover();
        }

        /// <summary>
        /// What the first marks would cost as the map opens, done while it is shut: their pictures
        /// drawn, and a first mark made on the large map's layer, the game's pin name copied the
        /// first time, which held the frame for tens of milliseconds.
        /// </summary>
        private static void Ahead(Minimap map)
        {
            ScryPanel.DrawMarkPicturesAhead();
            if (Marks.Count > 0 || _layer != null || map.m_mode == Minimap.MapMode.Large) return;
            var names = map.m_pinNameRootLarge;
            _layer = names != null ? names : map.m_pinRootLarge;
            if (_layer == null) return;
            var spare = MakeMark(map);
            if (spare != null) Hide(spare);
        }

        /// <summary>The kinds the marks kept are of, in the order first met, each with how many and how many are crossed off, told to the kinds list.</summary>
        private static void ListMarkedKinds()
        {
            var kinds = new List<(string Kind, string Name, int Count, int Crossed)>();
            foreach (var kept in Kept)
            {
                var kind = EntryKeys.For(Kind.Location, kept.Map.Location);
                var (x, z) = SpotOf(kept);
                var crossed = MapSpots.ModeAt(x, z) == SpotMode.Crossed ? 1 : 0;
                var at = kinds.FindIndex(k => k.Kind == kind);
                if (at >= 0) kinds[at] = (kind, kinds[at].Name, kinds[at].Count + 1, kinds[at].Crossed + crossed);
                else kinds.Add((kind, Shown(kept.Map.Location), 1, crossed));
            }
            MapSpots.MarkedKindsAre(kinds);
        }

        /// <summary>Where a dungeon's mark lies, as a spot's mode goes by it.</summary>
        private static (int X, int Z) SpotOf(DungeonMark kept) => (Mathf.RoundToInt(kept.Map.Entrance.X), Mathf.RoundToInt(kept.Map.Entrance.Z));

        /// <summary>The version of the modes the kinds list last heard of.</summary>
        private static int _modesListed = -1;

        /// <summary>A mode given to a mark's spot: its look and its words told anew.</summary>
        private static void SetMarkMode(Mark mark, SpotMode mode)
        {
            mark.Mode = mode;
            mark.Retell = true;
            MapSpots.SetModes(new[] { (mark.SpotX, mark.SpotZ) }, mode);
        }

        /// <summary>
        /// A right click of the map's own at a point of the screen (<see cref="MapRightClick"/>): the menu
        /// of the mark or dot there, or, on neither, the menu put away if one stands; false where there
        /// was nothing to take, so the game's own removes its pin.
        /// </summary>
        public static bool OpenMenuAt(Vector2 screen)
        {
            var mark = MarkAt(screen);
            if (mark != null)
            {
                var (id, look) = (mark.Id, mark.Look);
                var lines = new List<(string, System.Action)>();
                foreach (var choice in MapMenu.Of(mark.Mode, 1))
                {
                    var mode = MapMenu.ModeOf(choice);
                    lines.Add((SpotWords.MenuWord(choice, 1), () =>
                    {
                        if (mode is SpotMode to) SetMarkMode(mark, to);
                        else Open(id, look);
                    }));
                }
                MapSpots.OpenMenu(SpotWords.MenuTitle(mark.Plain, 1), screen, lines);
                return true;
            }
            if (MapSpots.DotMenuAt(screen, out var dot))
            {
                var lines = new List<(string, System.Action)>();
                var spots = dot.Spots;
                foreach (var choice in MapMenu.Of(dot.Mode, spots.Count))
                {
                    var mode = MapMenu.ModeOf(choice);
                    var kind = dot.Kind;
                    lines.Add((SpotWords.MenuWord(choice, spots.Count), () =>
                    {
                        if (mode is SpotMode to) MapSpots.SetModes(spots, to);
                        else if (choice == MapMenuChoice.ZoomIn) MapSpots.ZoomOn(spots[0].X, spots[0].Z);
                        else OpenPage(MapSpots.PageOf(kind));
                    }));
                }
                MapSpots.OpenMenu(SpotWords.MenuTitle(dot.Name, spots.Count), screen, lines);
                return true;
            }
            if (!MapSpots.MenuOpen) return false;
            MapSpots.CloseMenu();
            return true;
        }

        /// <summary>What a mark's mode lays on it, children of it made the first time each is needed: the game's cross, or the accent ring.</summary>
        private static void StandMode(Mark mark, Minimap map, float kindShare)
        {
            if (mark.ModeShown != mark.Mode)
            {
                mark.ModeShown = mark.Mode;
                if (mark.Mode == SpotMode.Crossed && mark.Cross == null) mark.Cross = ModePiece(mark, SpotMarks.CrossPicture(map), 0.05f, 0.95f);
                if (mark.Mode == SpotMode.Highlighted && mark.Ring == null) mark.Ring = ModePiece(mark, MapSpots.ActiveRing(), -0.28f, 1.28f);
                if (mark.Cross != null) mark.Cross.gameObject.SetActive(mark.Mode == SpotMode.Crossed);
                if (mark.Ring != null) mark.Ring.gameObject.SetActive(mark.Mode == SpotMode.Highlighted);
            }
            if (Mathf.Approximately(mark.ModeAlpha, kindShare)) return;
            mark.ModeAlpha = kindShare;
            if (mark.Cross != null) mark.Cross.color = new Color(1f, 1f, 1f, kindShare);
            if (mark.Ring != null) mark.Ring.color = new Color(1f, 1f, 1f, kindShare);
        }

        private static Image ModePiece(Mark mark, Sprite picture, float from, float to)
        {
            var made = new GameObject("Scry dungeon mark's mode", typeof(RectTransform));
            var rect = (RectTransform)made.transform;
            rect.SetParent(mark.Rect, false);
            rect.anchorMin = new Vector2(from, from);
            rect.anchorMax = new Vector2(to, to);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = made.AddComponent<Image>();
            image.sprite = picture;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>The marks the map shows now, in view and as the settings ask, and the marks made that none of them holds.</summary>
        private static void ListInView(Minimap map, bool small, Rect window, Rect area)
        {
            InView.Clear();
            InViewIds.Clear();
            foreach (var kept in Kept)
            {
                // Crossed off and left off the map, where the settings ask.
                if (Settings.CrossedMarks == CrossedMarks.Hidden && !MapSpots.ModeStands(MapSpots.ModeAt(SpotOf(kept).X, SpotOf(kept).Z))) continue;
                if (!MapMarks.Place(kept.Map.Entrance, map.m_pixelSize, map.m_textureSize, (window.xMin, window.yMin, window.width, window.height), (area.width, area.height), out var at)) continue;
                // Only where the map shows the ground: an entrance seen farther than it explores waits under its fog.
                if (!CompendiumMap.ShownAt(map, kept.Map.Entrance.X, kept.Map.Entrance.Z)) continue;
                InView.Add((kept, at));
                InViewIds.Add(kept.Id);
            }
            Spare.Clear();
            foreach (var mark in Marks) if (mark.Id == null || !InViewIds.Contains(mark.Id)) Spare.Add(mark);
        }

        /// <summary>
        /// Stands a mark on its entrance, a pin placed there under it, its words where it stands as
        /// the game's pin names do, as big as the mark is scaled; an entrance alone faint until the
        /// pointer rests on it. Each set only as it changes: hundreds set every frame took milliseconds.
        /// </summary>
        private static void Stand(Mark mark, MarkLook look, Vector2 spot, float size, float scale, bool small)
        {
            if (mark.At != spot)
            {
                mark.Rect.anchoredPosition = spot;
                if (mark.LabelRect != null) mark.LabelRect.anchoredPosition = spot;
                mark.At = spot;
            }
            // A highlighted mark stands bigger, its name as big as any.
            var drawn = mark.Mode == SpotMode.Highlighted ? size * SpotModes.HighlightScale : size;
            if (!Mathf.Approximately(mark.Size, drawn))
            {
                mark.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, drawn);
                mark.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, drawn);
                mark.Size = drawn;
            }
            var kindShare = MapSpots.ShownShare(mark.KindKey);
            StandMode(mark, Minimap.instance, kindShare);
            var share = SpotModes.ShareOf(mark.Mode, kindShare);
            var alpha = MapMarks.Alpha(look, mark == _hovered, share);
            if (mark.Picture != null && !Mathf.Approximately(mark.Alpha, alpha))
            {
                mark.Picture.color = new Color(1f, 1f, 1f, alpha);
                mark.Alpha = alpha;
            }
            FadeWords(mark, mark == _hovered ? 1f : share);
            SizeName(mark, MapMarks.NameScale(scale));
            if (!mark.Made.activeSelf) mark.Made.SetActive(true);
            if (small) Hide(mark.Label);
            else ShowWords(mark);
        }

        /// <summary>A mark's words as faded as it stands, by a group laid on them the first time they fade.</summary>
        private static void FadeWords(Mark mark, float share)
        {
            if (mark.Label == null) return;
            if (mark.WordsFade == null)
            {
                if (share >= 1f) return;
                mark.WordsFade = mark.Label.AddComponent<CanvasGroup>();
                mark.WordsFade.blocksRaycasts = false;
            }
            if (!Mathf.Approximately(mark.WordsFade.alpha, share)) mark.WordsFade.alpha = share;
        }

        /// <summary>Tells a mark its dungeon's look, glyph and words.</summary>
        private static void Tell(Mark mark, DungeonMark kept, MarkGlyph glyph)
        {
            mark.KindKey = EntryKeys.For(Kind.Location, kept.Map.Location);
            (mark.SpotX, mark.SpotZ) = SpotOf(kept);
            mark.Mode = MapSpots.ModeAt(mark.SpotX, mark.SpotZ);
            mark.Look = kept.Look;
            mark.Glyph = glyph;
            mark.Retell = false;
            // A place stood in is drawn as one you walked.
            if (mark.Picture != null) mark.Picture.sprite = ScryPanel.MarkPicture(glyph, kept.Look == MarkLook.Visited ? MarkLook.Yours : kept.Look);
            mark.Said = Said(kept, mark.Mode);
            mark.Plain = Shown(kept.Map.Location);
            mark.Showing = null;
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
            MapSpots.WordsOverAll(over?.LabelRect);
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
            var text = PinNames.Text(mark.Label);
            if (text == null) return;
            var property = text.GetType().GetProperty("fontSize");
            if (property == null || property.PropertyType != typeof(float)) return;
            if (mark.NameSize <= 0f) mark.NameSize = (float)property.GetValue(text);
            property.SetValue(text, mark.NameSize * scale);
            mark.NameScaled = scale;
        }

        /// <summary>A mark's words, by its look, on a dark plate: how far you walked it, how far anyone on this server did, or whether anyone did.</summary>
        private static string Said(DungeonMark kept, SpotMode mode) => DungeonMapWords.OnPlate(SpotWords.WithModeHint(SaidOf(kept), mode, 1), ColorUtility.ToHtmlStringRGBA(new Color(Skin.TipFill.r, Skin.TipFill.g, Skin.TipFill.b, 0.92f)));

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
                case MarkLook.Visited: return DungeonMapWords.VisitedLabel(name, accent);
                default: return DungeonMapWords.EntranceLabel(name, kept.Map.Walked.Count > 0, ColorUtility.ToHtmlStringRGB(Skin.Faint));
            }
        }

        /// <summary>Sets a mark's label's words (<see cref="DungeonMapWords.MarkLabel"/>, in the accent, or its name alone), as the game's pin names are set (<see cref="PinNames"/>).</summary>
        private static void SetText(GameObject label, string said) => PinNames.SetText(label, said);

        /// <summary>The mark standing at a point of the screen, read with no camera as the game's map and tips read the pointer; none.</summary>
        private static Mark MarkAt(Vector2 screen)
        {
            for (var i = 0; i < Placed.Count; i++)
            {
                if (Placed[i].Made.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(Placed[i].Rect, screen, null)) return Placed[i];
            }
            return null;
        }

        /// <summary>A kind's page opened in Scry, the map back to the minimap, as a dungeon's mark opens its dungeon.</summary>
        private static void OpenPage(string key)
        {
            if (key == null) return;
            HideName();
            Minimap.instance.OrNull()?.SetMapMode(Minimap.MapMode.Small);
            Session.ShowEntry(key);
            _dotOpened = key;
        }

        private static string _dotOpened;

        /// <summary>The page a dot's menu last opened, for the self-test.</summary>
        [ForTest]
        public static string DotOpenedForTest => _dotOpened;

        /// <summary>A click of the map's own at a point of the screen: the next mode for the mark there; false where it is on none, or the press was a drag.</summary>
        public static bool TakeClick(Vector2 screen)
        {
            var mark = MarkAt(screen);
            if (mark == null || !MapMarks.Clicked((_pressedAt.x, _pressedAt.y), (screen.x, screen.y), _sizeOnScreen)) return false;
            SetMarkMode(mark, SpotModes.Next(mark.Mode));
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
            if (Placed.Count == 0 && MarksShown == 0) return;
            HideName();
            foreach (var mark in Marks) Hide(mark);
            MarksShown = 0;
            Placed.Clear();
        }

        /// <summary>A new mark, kept among the marks, laid out as the game lays out its pins; none where the map has no pin to follow.</summary>
        private static Mark MakeMark(Minimap map)
        {
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
            mark.Label = PinNames.Make(map, _layer, "Scry dungeon mark name");
            if (mark.Label != null) mark.LabelRect = mark.Label.transform as RectTransform;
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
            // One seen by its entrance alone opens from its own mark: the server's rooms, where it sent them, or the stand-in.
            var kept = DungeonWalks.Maps.Of(id) ?? DungeonsInGround.MapOf(id) ?? Kept.FirstOrDefault(k => k.Id == id)?.Map;
            if (kept == null) return;
            HideName();
            Minimap.instance.OrNull()?.SetMapMode(Minimap.MapMode.Small);
            var laidOut = kept.Rooms != null && kept.Rooms.Count > 0;
            // A place stood in opens its page alone, as it stands.
            if (look != MarkLook.Visited)
            {
                if (laidOut) ExampleLayouts.OpenMapWhenShown(kept.Location, id, kept);
                else ExampleLayouts.StandInFor(kept.Location);
            }
            // Its page at once where the catalog is read, else once it is.
            Session.ShowEntry(EntryKeys.For(Kind.Location, kept.Location));
            if (look != MarkLook.Visited) Stage.Inside = true;
            OpenedForTest = (id, look, laidOut);
        }

        /// <summary>Whether any dungeon is kept on the map: every kind, or one kept from its page; all of them before a character's book is read.</summary>
        private static bool DungeonsKept(CompendiumBook book)
        {
            if (book == null || book.Keeps(CompendiumBook.Dungeons)) return true;
            // Worked out again only as what is kept changes, as this is asked each frame.
            if (_keptAsked.Book != book || _keptAsked.Version != book.KeptVersion) _keptAsked = (book, book.KeptVersion, book.Kept.Any(kept => MapKept.KindsIn(kept).Any(MapKept.IsDungeon)));
            return _keptAsked.Any;
        }

        private static (CompendiumBook Book, int Version, bool Any) _keptAsked;

        /// <summary>Whether a dungeon's mark is kept: every kind kept, or its own kind.</summary>
        private static bool KeptOnMap(CompendiumBook book, string location)
        {
            if (book == null || book.Keeps(CompendiumBook.Dungeons)) return true;
            var key = EntryKeys.For(Kind.Location, location);
            return book.Kept.Any(kept => MapKept.KindsIn(kept).Contains(key));
        }

        /// <summary>The dungeons whose entrances the character's sight kept in a world, or the self-test's in their place; none where the world remembers nothing seen.</summary>
        private static IEnumerable<(string Id, string Location, int X, int Z)> SeenDungeons(string world)
        {
            if (_seenInstead == null) return DungeonMarks.Seen(world, DungeonKinds.All, SeenSpotsKeeper.Of, MapKept.Remembered);
            return MapKept.Remembered ? _seenInstead : Enumerable.Empty<(string, string, int, int)>();
        }

        /// <summary>Entrances the self-test has seen in place of the sight's, or none for the sight's own; the marks listed again either way.</summary>
        [ForTest]
        public static void SeenInsteadForTest(List<(string Id, string Location, int X, int Z)> seen)
        {
            _seenInstead = seen;
            _keptStamp = (-1, (-1, -1), -1, -1, -1, false);
        }

        private static List<(string Id, string Location, int X, int Z)> _seenInstead;

        /// <summary>The mark last opened: its dungeon, how it looked and whether its own rooms were opened; for the self-test.</summary>
        [Diagnostic]
        public static (string Id, MarkLook Look, bool Rooms) OpenedForTest { get; private set; }
    }

    /// <summary>
    /// The game's map ticks the pin nearest a click (<c>Minimap.OnMapLeftClick</c>) and opens the box
    /// for a new pin on a double-click (<c>Minimap.OnMapDblClick</c>); with the pointer on a dungeon's
    /// mark or one of Scry's dots, the click goes through that spot's modes as the game's goes through
    /// a pin's, on Show on map's strip the click is the strip's, and the map does neither. Should it fail, it stands aside and
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

        /// <summary>A click on Show on map's strip is the strip's; on a mark or a dot, it goes through that spot's modes.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Take() => MapSpots.PointerOnStrip() || MapMarkButtons.TakeClick(ZInput.pointerPosition) || MapSpots.TakeLeftClick(ZInput.pointerPosition);
    }

    /// <summary>
    /// The game's map takes a right click to remove the pin under the pointer
    /// (<c>Minimap.RemovePinUnderPointer</c>); with the pointer on a dungeon's mark or one of Scry's
    /// dots it opens their menu instead (<see cref="MapMenu"/>), and on Show on map's strip, the kinds
    /// list or the menu it does nothing. Should it fail, it stands aside and leaves the map as it is.
    /// </summary>
    [HarmonyPatch(typeof(Minimap), "RemovePinUnderPointer")]
    internal static class MapRightClick
    {
        private static bool _off;

        private static bool Prefix()
        {
            if (_off) return true;
            if (!Guard.Run(Feature.ShowOnMap, "opening a mark's menu", Take, out var taken))
            {
                _off = true;
                return true;
            }
            return !taken;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Take()
        {
            return MapSpots.PointerOnStrip() || MapMarkButtons.OpenMenuAt(ZInput.pointerPosition);
        }
    }

    /// <summary>
    /// A press on Show on map's strip or the kinds list is theirs: the map neither starts dragging
    /// nor counts a click from it (<c>Minimap.OnMapLeftDown</c>, which notes when the press began),
    /// so dragging the list moves the list and not the map. Should it fail, it stands aside.
    /// </summary>
    [HarmonyPatch(typeof(Minimap), "OnMapLeftDown")]
    internal static class MapStripPress
    {
        private static bool _off;

        private static bool Prefix()
        {
            if (_off) return true;
            if (!Guard.Run(Feature.ShowOnMap, "keeping a press on the map's strip from the map", MapSpots.PressTaken, out var on))
            {
                _off = true;
                return true;
            }
            return !on;
        }
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
        private static bool On() => MapSpots.PointerOnStrip() || MapMarkButtons.PointerOnMark() || MapSpots.PointerOnDot();
    }
}
