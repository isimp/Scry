using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The kinds the character keeps on Valheim's map (<see cref="CompendiumBook.Kept"/>), worked
    /// out as Show on map works out one kind: the dots of each where it was seen, in its list
    /// colour, the nearest standing where there are too many (<see cref="SeenOnMap.MostKept"/>);
    /// each place kind's squares never come near, kept by kind so a kind shown on demand is left
    /// out of them; every kind of dungeon by its marks (<c>MapMarkButtons</c>), only its squares
    /// here; each kind once, however many kept lines hold it. Nothing where the rules keep Show on
    /// map from showing anything (no map, nothing remembered). Worked out again at once as what
    /// is kept changes, and as what was seen or told changes at most every few seconds
    /// (<see cref="Again"/>); drawn with the kind shown on demand (<see cref="MapSpots"/>) and on
    /// the minimap (<see cref="MiniKept"/>), at the map's own brightness.
    /// </summary>
    internal static class MapKept
    {
        static MapKept() => WorldCaches.Register(nameof(MapKept), Forget);

        private static List<(int X, int Z, Sprite Picture, string Name, string Kind, bool Told)> _dots = new List<(int, int, Sprite, string, string, bool)>();
        private static Dictionary<string, Dictionary<(int X, int Z), int>> _squaresByKind = new Dictionary<string, Dictionary<(int X, int Z), int>>(System.StringComparer.Ordinal);
        private static readonly Dictionary<string, string> NamesByKind = new Dictionary<string, string>(System.StringComparer.Ordinal);

        /// <summary>The mark each kind kept stands in, its kept line's.</summary>
        private static readonly Dictionary<string, int> MarkByKind = new Dictionary<string, int>(System.StringComparer.Ordinal);
        private static readonly Dictionary<string, (int Seen, int Squares, int Told)> Counted = new Dictionary<string, (int, int, int)>(System.StringComparer.Ordinal);

        /// <summary>What was kept and in which world as last worked out, worked out again at once as it changes; what was seen and told, and where the player stands where too many are kept, worked out again as it changes, at most every few seconds.</summary>
        private static (CompendiumBook Book, int Kept, bool Squares, bool Remembered, long World) _keptFor;
        private static (int Sources, (int, int) Spots, int Counts, int Walks, int Kinds, (int, int) Cell, int Marks) _seenFor;
        private static float _workedAt = float.MinValue;

        /// <summary>How long, in seconds, what is kept waits before it is worked out again for what was seen or told since, as the sight keeps spots while exploring.</summary>
        public const float Again = 2f;

        /// <summary>How far, in metres, the player walks before the nearest of too many kept dots are picked again.</summary>
        private const int NearestCell = 250;

        /// <summary>Counts up as what stands changes, for the maps to be placed again; not where the same stands after being worked out again.</summary>
        public static int Version { get; private set; }

        /// <summary>The dots standing, each with its picture, its kind's name, its key, and whether the server told of it rather than the character seeing it.</summary>
        public static IReadOnlyList<(int X, int Z, Sprite Picture, string Name, string Kind, bool Told)> Dots => _dots;

        /// <summary>How many dots the kept kinds hold in all, the nearest <see cref="SeenOnMap.MostKept"/> standing.</summary>
        public static int AllDots { get; private set; }

        /// <summary>Whether anything kept stands on the map.</summary>
        public static bool Any => _dots.Count > 0 || _squaresByKind.Count > 0;

        /// <summary>The kept kinds' squares, each with how many of each mark's kinds it holds, those of the kinds in <paramref name="except"/> left out, as they stand shown on demand.</summary>
        public static Dictionary<(int X, int Z), List<(int Mark, int Left)>> Squares(ICollection<string> except)
        {
            var merged = new Dictionary<(int X, int Z), List<(int Mark, int Left)>>();
            foreach (var pair in _squaresByKind)
            {
                if (except.Contains(pair.Key)) continue;
                var mark = MarkByKind.TryGetValue(pair.Key, out var of) ? of : 0;
                foreach (var square in pair.Value)
                {
                    if (!merged.TryGetValue(square.Key, out var held)) merged[square.Key] = held = new List<(int Mark, int Left)>();
                    var at = held.FindIndex(h => h.Mark == mark);
                    if (at >= 0) held[at] = (mark, held[at].Left + square.Value);
                    else held.Add((mark, square.Value));
                }
            }
            return merged;
        }

        /// <summary>What kept stands somewhere inside a square, by name and how many, the kinds in <paramref name="except"/> left out.</summary>
        public static IEnumerable<(string Name, int Count)> Inside(int x, int z, ICollection<string> except)
        {
            foreach (var pair in _squaresByKind)
                if (!except.Contains(pair.Key) && pair.Value.TryGetValue((x, z), out var many)) yield return (NamesByKind.TryGetValue(pair.Key, out var name) ? name : pair.Key, many);
        }

        /// <summary>How many of a kept kind stand: those seen, or for the dungeons those marked, the squares, and those the server told of; none worked out yet.</summary>
        public static (int Seen, int Squares, int Told) CountOf(string kept) => Counted.TryGetValue(kept, out var counted) ? counted : default;

        /// <summary>The kinds a kept key holds: one, or several of a place shown at once (<see cref="MapShowing.AllKindsKey"/>).</summary>
        public static string[] KindsIn(string kept) => kept.Split('+');

        /// <summary>The place a kind's key names, bare; none for one that is no place.</summary>
        public static string PlaceOf(string kind) => kind.StartsWith(EntryKeys.Location, System.StringComparison.Ordinal) ? kind.Substring(EntryKeys.Location.Length) : null;

        /// <summary>Whether a kind is one of dungeon, marked by its discs rather than dots.</summary>
        public static bool IsDungeon(string kind) => PlaceOf(kind) is string place && DungeonKinds.Is(place);

        /// <summary>A kind's name as the list and the pointer say it.</summary>
        public static string NameOf(string kind) => WorldCatalog.Find(kind)?.ShownName ?? Naming.Readable(PlaceOf(kind) ?? kind);

        /// <summary>The mark a kept line stands in: every kind of dungeon, and a line of dungeon kinds alone, in the dungeons' amber, as their discs stand; another its own.</summary>
        public static int MarkOf(CompendiumBook book, string kept)
        {
            if (kept == CompendiumBook.Dungeons || KindsIn(kept).All(IsDungeon)) return KeptMarks.Dungeons;
            return book?.MarkOf(kept) ?? 0;
        }

        /// <summary>The kept line that keeps a kind: its own, every kind of dungeon for a dungeon, or several kinds of a place shown at once; none.</summary>
        public static string KeptBy(CompendiumBook book, string kind)
        {
            if (book == null || kind == null) return null;
            if (book.Keeps(kind)) return kind;
            if (IsDungeon(kind) && book.Keeps(CompendiumBook.Dungeons)) return CompendiumBook.Dungeons;
            foreach (var kept in book.Kept) if (kept.Contains('+') && KindsIn(kept).Contains(kind)) return kept;
            return null;
        }

        /// <summary>Whether what is kept is set aside while a self-test part shows only its own on the map.</summary>
        private static bool AsideForTest { get; set; }

        /// <summary>Sets what is kept aside while a self-test part shows only its own on the map, or brings it back.</summary>
        [ForTest]
        public static void SetAsideForTest(bool aside)
        {
            AsideForTest = aside;
            Forget();
        }

        /// <summary>Whether what the character's sight kept may stand on the map: a map in this world, and its server remembering what is seen; else only the dungeons walked stand.</summary>
        public static bool Remembered => SeenOnMap.Remembers(Game.m_noMap, Settings.SeenOnMapReach);

        /// <summary>Each frame a map shows what is kept: worked out anew where what is kept, seen or told changed.</summary>
        public static void Refresh()
        {
            var book = CompendiumKeeper.Book;
            var worldId = ZNet.instance != null && ZNet.instance.GetWorld() != null ? ZNet.instance.GetWorldUID() : 0L;
            var kept = (book, book?.KeptVersion ?? -1, Settings.MapSquares, Remembered, worldId);
            var player = Player.m_localPlayer;
            var cell = AllDots > SeenOnMap.MostKept && player != null ? ((int)player.transform.position.x / NearestCell, (int)player.transform.position.z / NearestCell) : (0, 0);
            var seen = (book?.SourcesVersion ?? -1, SeenSpotsKeeper.Stamp, CompendiumMap.Counts.Version, DungeonWalks.Maps.Stamp, DungeonKinds.All.Count, cell, MapGameMarks.Version);
            if (kept == _keptFor && (seen == _seenFor || Time.unscaledTime - _workedAt < Again)) return;
            _keptFor = kept;
            _seenFor = seen;
            _workedAt = Time.unscaledTime;
            var started = Timing.Start();
            var dotsBefore = _dots;
            var squaresBefore = _squaresByKind;
            Work(book, CompendiumMap.World, kept.Remembered);
            if (!SameDots(dotsBefore, _dots) || !SameSquares(squaresBefore, _squaresByKind)) Version++;
            Timing.Add("map kept", started);
        }

        private static void Work(CompendiumBook book, string world, bool remembered)
        {
            _dots = new List<(int, int, Sprite, string, string, bool)>();
            _squaresByKind = new Dictionary<string, Dictionary<(int X, int Z), int>>(System.StringComparer.Ordinal);
            NamesByKind.Clear();
            MarkByKind.Clear();
            Counted.Clear();
            TypeCounts.Clear();
            Worked++;
            AllDots = 0;
            if (book == null || world == null || AsideForTest) return;
            var counted = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var kept in book.Kept) Count(kept, kept == CompendiumBook.Dungeons ? DungeonKinds.All.Select(k => EntryKeys.For(Kind.Location, k)).ToArray() : KindsIn(kept), MarkOf(book, kept), world, remembered, counted);
            AllDots = _dots.Count;
            var player = Player.m_localPlayer;
            if (_dots.Count > SeenOnMap.MostKept && player != null)
            {
                var at = player.transform.position;
                _dots = _dots.OrderBy(d => (d.X - at.x) * (d.X - at.x) + (d.Z - at.z) * (d.Z - at.z)).Take(SeenOnMap.MostKept).ToList();
            }
        }

        /// <summary>
        /// A kept key's kinds, each once over every key: their dots, but a dungeon's, and their
        /// squares less those seen; how many of each stand, a dungeon's by its marks. Where nothing
        /// seen is remembered, none of them but the dungeons walked.
        /// </summary>
        private static void Count(string kept, string[] kinds, int mark, string world, bool remembered, HashSet<string> counted)
        {
            var seen = 0;
            var square = 0;
            var toldOf = 0;
            var dungeons = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var kind in kinds)
            {
                if (IsDungeon(kind)) dungeons.Add(PlaceOf(kind));
                if (!remembered) continue;
                var spots = MapSpots.SpotsOf(kind);
                if (!IsDungeon(kind)) seen += spots.Count;
                if (!counted.Add(kind)) continue;
                var name = NameOf(kind);
                NamesByKind[kind] = name;
                MarkByKind[kind] = mark;
                if (!IsDungeon(kind))
                {
                    var picture = MapSpots.MarkPicture(mark, kept);
                    foreach (var (x, z) in spots) _dots.Add((x, z, picture, name, kind, false));
                }
                if (!(PlaceOf(kind) is string place) || !Settings.MapSquares) continue;
                // Where the server told one stands, a dungeon's too, hollow, but where a mark stands already.
                var told = MapSpots.ToldOf(place, spots);
                if (told.Count > 0)
                {
                    var hollow = MapSpots.MarkPicture(mark, kept, hollow: true);
                    foreach (var (x, z) in told) _dots.Add((x, z, hollow, name, kind, true));
                    toldOf += told.Count;
                    if (kept == CompendiumBook.Dungeons) TypeCounts[kind] = (0, told.Count);
                }
                var shown = PlaceSquares.Shown(CompendiumMap.Counts.Squares(place, MapSpots.SquaresGround), MapGameMarks.With(place, spots));
                if (shown.Count == 0) continue;
                var left = new Dictionary<(int X, int Z), int>();
                foreach (var (x, z, many) in shown) left[(x, z)] = many;
                _squaresByKind[kind] = left;
                square += shown.Count;
            }
            // The dungeons count those marked, walked or seen, each once.
            if (dungeons.Count > 0)
            {
                var marked = new Dictionary<string, string>(System.StringComparer.Ordinal);
                foreach (var (id, map) in DungeonWalks.Maps.OfWorld(world).Where(m => dungeons.Contains(m.Map.Location))) marked[id] = map.Location;
                foreach (var (id, location, _, _) in DungeonMarks.Seen(world, dungeons, SeenSpotsKeeper.Of, remembered)) marked[id] = location;
                seen += marked.Count;
                if (kept == CompendiumBook.Dungeons)
                {
                    foreach (var location in marked.Values)
                    {
                        var key = EntryKeys.For(Kind.Location, location);
                        TypeCounts.TryGetValue(key, out var had);
                        TypeCounts[key] = (had.Seen + 1, had.Told);
                    }
                }
            }
            Counted[kept] = (seen, square, toldOf);
        }

        private static bool SameDots(List<(int X, int Z, Sprite Picture, string Name, string Kind, bool Told)> a, List<(int X, int Z, Sprite Picture, string Name, string Kind, bool Told)> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++) if (a[i].X != b[i].X || a[i].Z != b[i].Z || a[i].Kind != b[i].Kind || a[i].Picture != b[i].Picture) return false;
            return true;
        }

        private static bool SameSquares(Dictionary<string, Dictionary<(int X, int Z), int>> a, Dictionary<string, Dictionary<(int X, int Z), int>> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var pair in a)
            {
                if (!b.TryGetValue(pair.Key, out var other) || other.Count != pair.Value.Count) return false;
                foreach (var square in pair.Value) if (!other.TryGetValue(square.Key, out var many) || many != square.Value) return false;
            }
            return true;
        }

        /// <summary>How many of each type of dungeon stand while every kind is kept, by the type's key: marked, and told of alone; and how many times what is kept was worked out.</summary>
        private static readonly Dictionary<string, (int Seen, int Told)> TypeCounts = new Dictionary<string, (int, int)>(System.StringComparer.Ordinal);

        public static IReadOnlyDictionary<string, (int Seen, int Told)> DungeonCounts => TypeCounts;

        public static int Worked { get; private set; }

        /// <summary>The category a kept line stands under on the lists.</summary>
        public static KeptCategory CategoryOf(string kept) => KeptCategories.Of(kept, IsDungeon);

        public static void Forget()
        {
            TypeCounts.Clear();
            _dots = new List<(int, int, Sprite, string, string, bool)>();
            _squaresByKind = new Dictionary<string, Dictionary<(int X, int Z), int>>(System.StringComparer.Ordinal);
            NamesByKind.Clear();
            MarkByKind.Clear();
            Counted.Clear();
            _keptFor = default;
            _seenFor = default;
            _workedAt = float.MinValue;
            AllDots = 0;
            Version++;
        }
    }
}
