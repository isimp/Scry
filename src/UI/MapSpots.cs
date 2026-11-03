using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Scry
{
    /// <summary>
    /// Show on map: a page's kind shown on Valheim's large map, a dot on every spot of it the
    /// character saw in the world played (<see cref="SeenSpotsKeeper"/>), until <see cref="Hide"/>,
    /// until the map opened for it closes, or until it never opens (<see cref="MapShowing"/>). The
    /// button closes Scry and opens the map as its key does, centred on the character. Each dot is
    /// a picture in the kind's list colour inside a light ring (<see cref="SeenOnMap.DotAt"/>), on
    /// the layer of the game's pins and first on it, so pins and their names stand over it and
    /// Scry's dungeon marks, on the names' layer, too; it takes no clicks, so a click on it is the
    /// map's. While a kind is shown the other pins stand dimmed (<see cref="MapDim"/>). Dots are
    /// placed where the map shows them each frame, smaller as it zooms out
    /// (<see cref="SeenOnMap.DotSize"/>); made a few a frame from a pool, as the dungeon marks are. The strip naming the kind stands in Scry's
    /// look at the map's top left (<see cref="ScryPanel.MapStripGUI"/>). A place's page shows
    /// the server's squares too (<see cref="MapSquares"/>), each round one of its kind the
    /// character never came near, in ground it explored or saw on a shared map.
    /// </summary>
    internal static class MapSpots
    {
        /// <summary>Leaving a world lets go of the dots and what is shown (<see cref="WorldCaches"/>).</summary>
        static MapSpots() => WorldCaches.Register(nameof(MapSpots), Forget);

        private static readonly MapShowing Showing = new MapShowing();

        /// <summary>The strip's words, worked out as the kind is shown, the dots' colour and their picture in it.</summary>
        private static string _strip;
        private static Color _colour = Color.white;
        private static Sprite _sprite;

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
        private static (Rect Window, Vector2 Area, (IReadOnlyList<(int X, int Z)> Spots, IReadOnlyList<(int X, int Z)> Told, Sprite Picture, int Kept) Dots, float Size) _placedFor;
        private static bool _allMade;

        /// <summary>The page whose kind was last worked out, and its kind, as the page asks each time it is drawn.</summary>
        private static Entry _kindFor;
        private static string _kindOf;

        /// <summary>The dots' pictures, one drawn for each colour and shape a kind is shown in.</summary>
        private static readonly Dictionary<(Color32, MarkShape, bool), Sprite> Pictures = new Dictionary<(Color32, MarkShape, bool), Sprite>();

        /// <summary>A couple of milliseconds a frame for making dots.</summary>
        private static readonly FrameShare Share = new FrameShare(2.0);

        /// <summary>How many dots stood on the map at its last frame open, for the self-test.</summary>
        [ForTest]
        public static int DotsShown => _standing;

        /// <summary>How many of the dots standing are places the server told of, hollow, for the self-test.</summary>
        [Diagnostic]
        public static int ToldDotsShownForTest
        {
            get
            {
                var told = 0;
                for (var i = 0; i < _standing && i < PlacedFrom.Count; i++) if (StandingDots[PlacedFrom[i]].Told) told++;
                return told;
            }
        }

        /// <summary>Where the first hollow dot standing stands on the screen; none, for the self-test.</summary>
        [Diagnostic]
        public static Vector2? ToldDotCenterForTest()
        {
            for (var i = 0; i < _standing && i < PlacedFrom.Count; i++) if (StandingDots[PlacedFrom[i]].Told) return Dots[i].Rect.position;
            return null;
        }

        /// <summary>How big the dots stood, in the map's units, for the self-test; 0 with none.</summary>
        [Diagnostic]
        public static float DotSizeForTest => _standing > 0 ? Dots[0].Rect.rect.width : 0f;

        /// <summary>
        /// What the first dot shows from its middle out, for the self-test: its picture's pixels at
        /// the middle, the edge, the gap, the ring and a corner past it, with the colour drawn over
        /// them; none with no dot.
        /// </summary>
        [Diagnostic]
        public static (Color Middle, Color Edge, Color Gap, Color Ring, Color Outside)? DotLookForTest()
        {
            if (_standing == 0 || !(Dots[0].Picture.sprite is Sprite sprite) || sprite.texture == null) return null;
            var texture = sprite.texture;
            var tint = Dots[0].Picture.color;
            var side = texture.width;
            Color At(float share) => texture.GetPixel(side / 2 + (int)(share * side / 2f), side / 2) * tint;
            return (At(0f), At(0.49f), At(0.64f), At(0.855f), texture.GetPixel(0, 0) * tint);
        }

        /// <summary>What holds the dots, for the self-test's look at how bright they stand.</summary>
        [Diagnostic]
        public static Transform DotsHolderForTest => _holder;

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
                if (_layer == null || _holder == null || Dots.Count == 0 || _holder.parent != _layer) return false;
                // Only Scry's own layers come before it (the squares, under the dots): every pin after.
                for (var i = 0; i < _holder.GetSiblingIndex(); i++) if (!_layer.GetChild(i).name.StartsWith("Scry ", System.StringComparison.Ordinal)) return false;
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

        /// <summary>Why a kind with so many seen, and so many squares, shows nothing now; none where it can show them.</summary>
        public static MapBlock BlockOf(int count, int squares = 0) =>
            SeenOnMap.Block(Game.m_noMap, Settings.SeenOnMapReach, Settings.RecordSeenSpots, SeenSpotsKeeper.Unreadable, count, squares);

        /// <summary>
        /// A place's squares to shade (<see cref="PlaceSquares.Shown"/>): those the server told for
        /// the grounds the book shows, the character's own explored ground and shared maps, each with
        /// how many of its places the character did not see; none for anything but a place, or under
        /// rules that show neither ground.
        /// </summary>
        public static IReadOnlyList<(int X, int Z, int Left)> SquaresOf(Entry entry, IReadOnlyList<(int X, int Z)> seen)
        {
            if (entry == null || entry.Kind != Kind.Location || entry.IsRoom) return NoSquares;
            // Less those seen, and those the game's map marks itself.
            return PlaceSquares.Shown(CompendiumMap.Counts.Squares(entry.Name, SquaresGround), MapGameMarks.With(entry.Name, seen));
        }

        /// <summary>How many squares a place's page would shade, worked out again only as the answers or its spots change, as the page asks each time it is drawn.</summary>
        public static int SquareCount(Entry entry) => CountsOf(entry).Squares;

        /// <summary>How many of a page's place the server told of where it stands, unmarked, for its button's tip, worked out with the squares.</summary>
        public static int ToldCount(Entry entry) => CountsOf(entry).Told;

        private static (int Squares, int Told) CountsOf(Entry entry)
        {
            // Turned off in the settings' Map section they count for nothing, the button offered by what was seen alone.
            if (!Settings.MapSquares) return default;
            if (entry == null || entry.Kind != Kind.Location) return default;
            var kind = KindOf(entry);
            // The book's sources too: a ground turned down takes its squares away.
            var stamp = (entry, CompendiumMap.Counts.Version, kind != null ? SeenSpotsKeeper.CountOf(kind) : 0, CompendiumKeeper.Book?.SourcesVersion ?? -1, MapGameMarks.Version);
            if (stamp.Equals(_countedFor)) return _counted;
            _countedFor = stamp;
            var seen = kind != null ? SeenSpotsKeeper.Of(kind) : None;
            _counted = (SquaresOf(entry, seen).Count, ToldOf(entry, seen).Count);
            return _counted;
        }

        private static (Entry, int, int, int, int) _countedFor;
        private static (int Squares, int Told) _counted;

        private static readonly IReadOnlyList<(int X, int Z, int Left)> NoSquares = new List<(int, int, int)>();

        /// <summary>The grounds whose squares a page shows: the explored ground and shared maps, as the book shows them above Off (none under the rules' Off, never the whole world).</summary>
        public static bool SquaresGround(WorldSource source)
        {
            if (source != WorldSource.Explored && source != WorldSource.SharedMaps) return false;
            var book = CompendiumKeeper.Book;
            return book != null && book.Shows(source) > SourceLevel.Off;
        }

        /// <summary>Why a place shows no squares, where it shows none: nothing heard yet, a server without Scry or with an older one, or one giving none; none where neither ground is shown.</summary>
        private static string SquaresNote()
        {
            if (!SquaresGround(WorldSource.Explored) && !SquaresGround(WorldSource.SharedMaps)) return null;
            return SpotWords.SquaresNote(CompendiumMap.Counts.Heard(SquaresGround));
        }

        /// <summary>The page shown on the map, and the kinds of its place met, two or more where it can show them all (<see cref="AllKinds"/>); none.</summary>
        private static Entry _shownEntry;
        private static IReadOnlyList<Entry> _shownKinds = System.Array.Empty<Entry>();
        private static string _shownFamily;

        /// <summary>Whether a place of several kinds shows them all rather than the page's alone, kept for the session; the strip's button.</summary>
        public static bool AllKinds { get; private set; }

        /// <summary>How many kinds the strip offers to show at once: those of the place shown met, none for one of a kind alone.</summary>
        public static int KindsOffered => Showing.Showing && _shownKinds.Count >= 2 ? _shownKinds.Count : 0;

        /// <summary>
        /// A page's kind shown on the map: its spots in the world played, by its name, in its list
        /// colour; a place of several kinds (<paramref name="kinds"/>, those met, under its family's
        /// name) all of them while <see cref="AllKinds"/>, their spots together and their squares
        /// added up.
        /// </summary>
        public static void Show(Entry entry, IReadOnlyList<Entry> kinds = null, string family = null)
        {
            var kind = KindOf(entry);
            if (kind == null) return;
            _shownEntry = entry;
            _shownKinds = entry.Kind == Kind.Location && kinds != null && kinds.Count >= 2 ? kinds : System.Array.Empty<Entry>();
            _shownFamily = family ?? entry.ShownName;
            if (AllKinds && _shownKinds.Count >= 2)
            {
                ShowAll();
                return;
            }
            // A broken form's page shows the whole one's spots, by the whole one's name.
            var named = entry.Kind == Kind.Location ? entry : WorldCatalog.Find(kind) ?? entry;
            var spots = SpotsOf(kind);
            var squares = Settings.MapSquares ? SquaresOf(entry, spots) : NoSquares;
            var told = ToldOf(entry, spots);
            var note = entry.Kind == Kind.Location && squares.Count == 0 && told.Count == 0 && Settings.MapSquares ? SquaresNote() : null;
            Show(kind, named.ShownName, DemandMark(kind), spots, squares, note, told);
        }

        /// <summary>Where the server told a place's kind stands in the grounds shown, but where it stands marked already (seen, by the game, walked); none for what is no place, or with the server's places off.</summary>
        public static IReadOnlyList<(int X, int Z)> ToldOf(Entry entry, IReadOnlyList<(int X, int Z)> seen)
        {
            if (entry == null || entry.Kind != Kind.Location || entry.IsRoom || !Settings.MapSquares) return None;
            return ToldOf(entry.Name, seen);
        }

        /// <summary>Where the server told a place stands, by its prefab's name, but where it stands marked already.</summary>
        public static IReadOnlyList<(int X, int Z)> ToldOf(string place, IReadOnlyList<(int X, int Z)> seen)
        {
            var told = CompendiumMap.Counts.Spots(place, SquaresGround);
            return told.Count == 0 ? None : PlacesToldBy.Besides(told, MapGameMarks.With(place, seen));
        }

        /// <summary>The mark a kind shown on demand stands in: the one of the line that keeps it, else the lowest no kept kind stands in.</summary>
        private static int DemandMark(string key)
        {
            var book = CompendiumKeeper.Book;
            if (book == null) return 0;
            return MapKept.KeptBy(book, key) is string line && book.MarkOf(line) is int kept ? kept : book.FreeMark();
        }

        /// <summary>Every kind of the place shown at once: their spots together, their squares added up, less those seen.</summary>
        private static void ShowAll()
        {
            var spots = new List<(int X, int Z)>();
            foreach (var member in _shownKinds) spots.AddRange(SpotsOf(member.Key));
            IReadOnlyList<(int X, int Z)> marked = spots;
            foreach (var member in _shownKinds) marked = MapGameMarks.With(member.Name, marked);
            var squares = Settings.MapSquares && !_shownEntry.IsRoom
                ? PlaceSquares.Shown(PlaceSquares.Merged(_shownKinds.Select(member => CompendiumMap.Counts.Squares(member.Name, SquaresGround))), marked)
                : NoSquares;
            var told = new List<(int X, int Z)>();
            if (Settings.MapSquares && !_shownEntry.IsRoom) foreach (var member in _shownKinds) told.AddRange(ToldOf(member.Name, spots));
            var note = squares.Count == 0 && told.Count == 0 && Settings.MapSquares ? SquaresNote() : null;
            var key = MapShowing.AllKindsKey(_shownKinds.Select(member => member.Key));
            Show(key, SpotWords.AllKindsName(_shownFamily, _shownKinds.Count), DemandMark(key), spots, squares, note, told);
        }

        /// <summary>The key the kind shown on demand is kept by; none while none is shown.</summary>
        public static string ShownKey => Showing.Showing ? Showing.Kind : null;

        /// <summary>The strip's button: all the place's kinds shown, or the page's alone again, kept for the session.</summary>
        public static void ShowAllKinds(bool all)
        {
            AllKinds = all;
            if (_shownEntry != null && Showing.Showing) Show(_shownEntry, _shownKinds, _shownFamily);
        }

        /// <summary>The spots of a kind, or the self-test's in their place.</summary>
        public static IReadOnlyList<(int X, int Z)> SpotsOf(string kind) => _spotsInstead?.Invoke(kind) ?? SeenSpotsKeeper.Of(kind);

        /// <summary>Spots of the self-test's for each kind in place of the sight's, or none for the sight's own.</summary>
        [ForTest]
        public static void SpotsInsteadForTest(System.Func<string, IReadOnlyList<(int X, int Z)>> spots)
        {
            _spotsInstead = spots;
            // What is kept is worked out again from them.
            MapKept.Forget();
        }

        /// <summary>Where a dot standing on the large map stands on the screen, by its place among them; none, for the self-test.</summary>
        [Diagnostic]
        public static Vector2? DotCenterForTest(int dot)
        {
            if (dot < 0 || dot >= _standing) return null;
            return Dots[dot].Rect.position;
        }

        private static System.Func<string, IReadOnlyList<(int X, int Z)>> _spotsInstead;

        /// <summary>
        /// A kind's spots shown on the map, and a place's squares with them, as the button shows
        /// them, and the map opened as its key opens it; the self-test gives spots and squares of its
        /// own. A note says why a place shows no squares. Its dots and squares stand in its mark (<see cref="KeptMarks"/>).
        /// </summary>
        public static void Show(string kind, string name, int mark, IReadOnlyList<(int X, int Z)> spots, IReadOnlyList<(int X, int Z, int Left)> squares = null, string note = null,
            IReadOnlyList<(int X, int Z)> told = null)
        {
            Showing.Show(CompendiumMap.World, kind, name, spots);
            squares = squares ?? NoSquares;
            _demandSquares = squares;
            _demandTold = told ?? None;
            _mark = mark;
            _colour = Skin.MarkColour(mark);
            _sprite = Picture(_colour, KeptMarks.ShapeOf(mark));
            // The hollow picture drawn only where the server told of some, as drawing a picture holds its frame a while.
            _toldSprite = _demandTold.Count > 0 ? Picture(_colour, KeptMarks.ShapeOf(mark), hollow: true) : null;
            var player = Player.m_localPlayer;
            var nearest = player != null ? SeenOnMap.Nearest(spots, player.transform.position.x, player.transform.position.z) : null;
            var more = PlaceSquares.More(squares);
            _strip = nearest is (float away, float dx, float dz)
                ? SpotWords.Strip(name, spots.Count, away, SpotWords.Direction(dx, dz), more, note, _demandTold.Count)
                : SpotWords.Strip(name, spots.Count, null, null, more, note, _demandTold.Count);
            var map = Minimap.instance;
            if (map == null) return;
            // Opened, the game centres the map on the character; already open, it is centred here, as the map key leaves it panned.
            if (map.m_mode == Minimap.MapMode.Large) MapOffset(map) = Vector3.zero;
            map.SetMapMode(Minimap.MapMode.Large);
            if (player != null && Settings.MapZoomOut) FitZoom(map, player.transform.position, spots, squares);
        }

        /// <summary>How far the large map is panned from the character (<c>Minimap.m_mapOffset</c>).</summary>
        private static readonly AccessTools.FieldRef<Minimap, Vector3> MapOffset = AccessTools.FieldRefAccess<Minimap, Vector3>("m_mapOffset");

        /// <summary>
        /// The large map's zoom before a kind was shown zoomed out, and the zoom it was shown at,
        /// the one put back as the map closes while the other still stands; none while it was
        /// left as it was.
        /// </summary>
        private static float? _zoomBefore;
        private static float _zoomFitted;

        /// <summary>
        /// The large map, opened centred on the character, zoomed out just far enough to hold the
        /// nearest of the spots and squares shown (<see cref="SeenOnMap.FitZoom"/>), its zoom before
        /// kept to be put back as the map closes.
        /// </summary>
        private static void FitZoom(Minimap map, Vector3 at, IReadOnlyList<(int X, int Z)> spots, IReadOnlyList<(int X, int Z, int Left)> squares)
        {
            var marks = new List<(float Dx, float Dz)>(spots.Count + squares.Count);
            foreach (var (x, z) in spots) marks.Add((x - at.x, z - at.z));
            foreach (var (x, z, _) in squares) marks.Add(((x + 0.5f) * PlaceSquares.Metres - at.x, (z + 0.5f) * PlaceSquares.Metres - at.z));
            var area = map.m_mapImageLarge != null ? map.m_mapImageLarge.rectTransform.rect : default;
            var aspect = area.height > 0f ? area.width / area.height : 1f;
            var zoom = SeenOnMap.FitZoom(map.LargeZoom, marks, map.m_textureSize * map.m_pixelSize, aspect);
            if (zoom <= map.LargeZoom) return;
            if (_zoomBefore == null) _zoomBefore = map.LargeZoom;
            map.LargeZoom = zoom;
            _zoomFitted = map.LargeZoom;
        }

        /// <summary>
        /// The zoom the map had before a kind was shown zoomed out, back once the map is closed, so
        /// the map key opens it as it was left; a zoom the player chose since stands.
        /// </summary>
        private static void PutZoomBack(Minimap map)
        {
            if (_zoomBefore == null || map == null) return;
            if (Mathf.Approximately(map.LargeZoom, _zoomFitted)) map.LargeZoom = _zoomBefore.Value;
            _zoomBefore = null;
        }

        /// <summary>The mark the kind shown on demand stands in, and its dots' picture, as the strip shows it beside the kind's name.</summary>
        private static int _mark;
        public static Sprite ShownPicture => Showing.Showing ? _sprite : null;

        /// <summary>The mark the kind shown on demand stands in, for the self-test.</summary>
        [Diagnostic]
        public static int MarkShownForTest => _mark;

        /// <summary>The colour the dots stand in now, for the self-test.</summary>
        [Diagnostic]
        public static Color ColourShownForTest => _colour;

        public static void Hide()
        {
            Showing.Hide();
            _shownEntry = null;
            _shownKinds = System.Array.Empty<Entry>();
            _demandSquares = NoSquares;
            _demandTold = None;
            PutAway();
        }

        public static void Forget()
        {
            Showing.Hide();
            ForgetDots();
            _strip = null;
            _shownEntry = null;
            _shownKinds = System.Array.Empty<Entry>();
            _shownFamily = null;
            _kindFor = null;
            _kindOf = null;
            _countedFor = default;
            _counted = default;
            _zoomBefore = null;
            _demandSquares = NoSquares;
            _demandTold = None;
            _dotsFor = default;
            _mergedFor = default;
            StandingDots.Clear();
            PlacedFrom.Clear();
            _shownKindsOf = null;
            ShownKinds.Clear();
            MapSquares.Forget();
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

        /// <summary>The squares of the kind shown on demand, shaded with those of the kinds kept.</summary>
        private static IReadOnlyList<(int X, int Z, int Left)> _demandSquares = NoSquares;

        /// <summary>Where the server told the kind shown on demand stands, and the hollow picture they stand in.</summary>
        private static IReadOnlyList<(int X, int Z)> _demandTold = None;
        private static Sprite _toldSprite;

        /// <summary>The dots standing: the kind shown on demand in its picture, then those kept, each in its kind's; and what they were worked out for.</summary>
        private static readonly List<(int X, int Z, Sprite Picture, string Name, bool Told)> StandingDots = new List<(int, int, Sprite, string, bool)>();

        /// <summary>Which of the dots worked out each dot placed stands for, as only those in the map's window are placed.</summary>
        private static readonly List<int> PlacedFrom = new List<int>();

        /// <summary>The kinds the kind shown on demand holds, by key, left out of those kept as they stand shown; and the kind they were worked out for.</summary>
        private static readonly HashSet<string> ShownKinds = new HashSet<string>(System.StringComparer.Ordinal);
        private static string _shownKindsOf;

        private static HashSet<string> KindsShownNow()
        {
            var kind = Showing.Showing ? Showing.Kind : null;
            if (kind == _shownKindsOf) return ShownKinds;
            _shownKindsOf = kind;
            ShownKinds.Clear();
            if (kind != null) foreach (var each in MapKept.KindsIn(kind)) ShownKinds.Add(each);
            return ShownKinds;
        }
        private static (IReadOnlyList<(int X, int Z)> Spots, IReadOnlyList<(int X, int Z)> Told, Sprite Picture, int Kept) _dotsFor;
        private static (IReadOnlyList<(int X, int Z, int Left)> Demand, int Kept) _mergedFor;

        /// <summary>What stands somewhere inside a square shaded, by name and how many, the kind shown on demand among those kept; none.</summary>
        public static IReadOnlyList<(string Name, int Count)> InsideSquare(int x, int z)
        {
            var inside = new List<(string Name, int Count)>();
            if (Showing.Showing) foreach (var (sx, sz, left) in _demandSquares) if (sx == x && sz == z) inside.Add((Showing.Name, left));
            inside.AddRange(MapKept.Inside(x, z, KindsShownNow()));
            return inside;
        }

        /// <summary>The dots to stand, worked out again only as the kind shown or what is kept changes.</summary>
        private static void WorkOutDots()
        {
            var demand = Showing.Showing ? Showing.Spots : None;
            var told = Showing.Showing ? _demandTold : None;
            var now = (demand, told, _sprite, MapKept.Version);
            if (now == _dotsFor) return;
            _dotsFor = now;
            StandingDots.Clear();
            foreach (var (x, z) in demand) StandingDots.Add((x, z, _sprite, Showing.Name, false));
            foreach (var (x, z) in told) StandingDots.Add((x, z, _toldSprite, Showing.Name, true));
            // A kind kept and shown on demand stands once, as shown.
            var shown = KindsShownNow();
            foreach (var (x, z, picture, name, kind, toldDot) in MapKept.Dots) if (!shown.Contains(kind)) StandingDots.Add((x, z, picture, name, toldDot));
        }

        /// <summary>The squares shaded: the kind shown on demand's with the kinds kept, a square both share counting each, given again only as either changes.</summary>
        private static void MergeSquares()
        {
            var demand = Showing.Showing ? _demandSquares : NoSquares;
            var now = (demand, MapKept.Version);
            if (now == _mergedFor) return;
            _mergedFor = now;
            // The kept squares of a kind shown on demand are its own already.
            var merged = MapKept.Squares(KindsShownNow());
            foreach (var (x, z, left) in demand)
            {
                if (!merged.TryGetValue((x, z), out var held)) merged[(x, z)] = held = new List<(int Mark, int Left)>();
                held.Add((_mark, left));
            }
            MapSquares.Show(merged.Select(s => (s.Key.X, s.Key.Z, (IReadOnlyList<(int Mark, int Left)>)s.Value)).ToList());
        }

        private static readonly IReadOnlyList<(int X, int Z)> None = System.Array.Empty<(int, int)>();

        /// <summary>
        /// Each frame, once the map has moved: the dots and squares placed where the large map
        /// shows them, those of a kind shown on demand and those kept; put away otherwise. Only a
        /// kind shown on demand dims the other pins and darkens the map.
        /// </summary>
        public static void Update() => Update(false);

        /// <summary>Each frame, as above; <paramref name="onMark"/> the pointer resting on a dungeon's mark, whose words it tells itself.</summary>
        public static void Update(bool onMark)
        {
            var map = Minimap.instance;
            var large = LargeOpen;
            if (!large) PutZoomBack(map);
            // A server ceasing to remember what is seen while a kind is shown takes it away, as it does what is kept.
            if (Showing.Showing && !MapKept.Remembered) Hide();
            if (Showing.Showing) Showing.Tick(large, CompendiumMap.World);
            if (large) MapKept.Refresh();
            if (!large || !Showing.Showing && !MapKept.Any)
            {
                PutAway();
                return;
            }
            var layer = map.m_pinRootLarge;
            if (layer != _layer || (_layer != null && _holder == null))
            {
                ForgetDots();
                MapDim.Restore();
                _layer = layer;
                if (_layer != null) MakeHolder();
            }
            if (_holder == null) return;
            if (!_holder.gameObject.activeSelf) _holder.gameObject.SetActive(true);
            // The squares under the dots, placed as the map moves, as the dots are; the other pins dimmed while a kind is shown on demand.
            var squaring = Timing.Start();
            MergeSquares();
            MapSquares.Update(map);
            Timing.Add("spots squares", squaring);
            if (Showing.Showing) MapDim.Hold(map, _holder, MapSquares.Holder);
            else MapDim.Restore();
            var working = Timing.Start();
            WorkOutDots();
            Timing.Add("spots worked out", working);
            var image = map.m_mapImageLarge;
            var window = image.uvRect;
            var area = image.rectTransform.rect;
            var size = SeenOnMap.DotSize(map.m_pinSizeLarge, window.height) * MapLook.DotScale(Settings.MapDotSize);
            // A map standing still with every dot made costs nothing more: nothing moved; the pointer is read all the same.
            var placing = (window, area.size, _dotsFor, size);
            var placingDots = Timing.Start();
            if (!_allMade || placing != _placedFor) PlaceDots(map, window, area, size, placing);
            Timing.Add("spots placed", placingDots);
            var reading = Timing.Start();
            if (onMark) Pointed = null;
            else ReadPointer();
            Timing.Add("spots pointer", reading);
        }

        /// <summary>The dots inside the map's window stood where it shows them, a couple of milliseconds' making a frame.</summary>
        private static void PlaceDots(Minimap map, Rect window, Rect area, float size, (Rect, Vector2, (IReadOnlyList<(int X, int Z)>, IReadOnlyList<(int X, int Z)>, Sprite, int), float) placing)
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            PlacedFrom.Clear();
            var made = 0;
            var standing = 0;
            var allMade = true;
            for (var i = 0; i < StandingDots.Count; i++)
            {
                var (x, z, picture, _, _) = StandingDots[i];
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
                Stand(Dots[standing++], new Vector2(at.X, at.Y), size, picture);
                PlacedFrom.Add(i);
            }
            // Those no spot holds now put away, only as fewer stand than before.
            for (var i = standing; i < _live && i < Dots.Count; i++) Away(Dots[i].Picture);
            _live = standing;
            _standing = standing;
            _placedFor = placing;
            _allMade = allMade;
        }

        private static (Vector2 Pointer, (Rect Window, Vector2 Area, (IReadOnlyList<(int X, int Z)> Spots, IReadOnlyList<(int X, int Z)> Told, Sprite Picture, int Kept) Dots, float Size) Placed, int Squares) _readFor;

        /// <summary>What the pointer rests on, its words and, for a dot, where it stands on the screen to be ringed; none.</summary>
        public static (string Words, Rect? Ring)? Pointed { get; private set; }

        /// <summary>
        /// The dot or square the pointer rests on, as the map stands: a dot named, and how far and
        /// which way from the character; a square, what stands somewhere inside and in which ground.
        /// Nothing while a mouse button is held, as the map is dragged.
        /// </summary>
        private static void ReadPointer()
        {
            var pointer = (Vector2)ZInput.pointerPosition;
            // Read again only as the pointer or the map moves.
            var reading = (pointer, _placedFor, MapSquares.Version);
            if (reading == _readFor) return;
            _readFor = reading;
            Pointed = null;
            if (Input.GetMouseButton(0) || PointerOnStrip()) return;
            var player = Player.m_localPlayer;
            // Every dot is as big and in one holder: its half on the screen read once.
            var half = _standing > 0 ? Dots[0].Rect.rect.width * Dots[0].Rect.lossyScale.x / 2f : 0f;
            for (var i = _standing - 1; i >= 0; i--)
            {
                var rect = Dots[i].Rect;
                // A dot is round and centred on its place: how far the pointer stands from its middle, on the screen.
                if (((Vector2)rect.position - pointer).sqrMagnitude > half * half || i >= PlacedFrom.Count) continue;
                var (x, z, _, name, toldDot) = StandingDots[PlacedFrom[i]];
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                var ring = Rect.MinMaxRect(corners[0].x, Screen.height - corners[2].y, corners[2].x, Screen.height - corners[0].y);
                if (player == null)
                {
                    Pointed = (name, ring);
                    return;
                }
                var (dx, dz) = (x - player.transform.position.x, z - player.transform.position.z);
                var away = Mathf.Sqrt(dx * dx + dz * dz);
                Pointed = (toldDot ? SpotWords.ToldHover(name, away, SpotWords.Direction(dx, dz)) : SpotWords.DotHover(name, away, SpotWords.Direction(dx, dz)), ring);
                return;
            }
            if (MapSquares.SquareAt(pointer) is (int sx, int sz))
            {
                var inside = InsideSquare(sx, sz);
                if (inside.Count > 0) Pointed = (SpotWords.SquareHover(inside, SquaresGround(WorldSource.Explored), SquaresGround(WorldSource.SharedMaps)), null);
            }
        }

        /// <summary>A dot stood where the map shows its spot, as big and in the picture of the colour asked, each set only as it changes.</summary>
        private static void Stand((Image Picture, RectTransform Rect) dot, Vector2 at, float size, Sprite picture)
        {
            if (dot.Rect.anchoredPosition != at) dot.Rect.anchoredPosition = at;
            var sized = new Vector2(size, size);
            if (dot.Rect.sizeDelta != sized) dot.Rect.sizeDelta = sized;
            if (dot.Picture.sprite != picture) dot.Picture.sprite = picture;
            if (!dot.Picture.gameObject.activeSelf) dot.Picture.gameObject.SetActive(true);
        }

        private static void Away(Image picture)
        {
            if (picture != null && picture.gameObject.activeSelf) picture.gameObject.SetActive(false);
        }

        /// <summary>Puts every dot away at once, by what holds them, each left as it is inside; placed afresh as a kind is shown again.</summary>
        private static void PutAway()
        {
            Pointed = null;
            _readFor = default;
            MapSquares.PutAway();
            MapDim.Restore();
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
            picture.sprite = _sprite;
            picture.raycastTarget = false;
            Dots.Add((picture, rect));
            return true;
        }

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
