using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Catches a character's book up from its explored map once per world each session
    /// (<see cref="MapCatchUp"/>), and again with the ground new since each minute it grew by
    /// enough (<see cref="MapGrowth"/>): the kinds of place lying where it explored count as seen,
    /// by cells of the map's own pixels, about 12 m. Where this game is the world's
    /// server (single player, or hosting) it holds where every place is
    /// (<c>ZoneSystem.m_locationInstances</c>); a dedicated server's client asks the server's Scry
    /// (<see cref="CompendiumLink"/>), which never sends positions back. The character's own
    /// exploring is caught up so (<c>Minimap.m_explored</c>); the ground it took from maps shared
    /// at a cartography table, which the game keeps apart (<c>Minimap.m_exploredOthers</c>), is
    /// asked about the same way, once a session and again after a map is read at a table
    /// (<c>MapTable.OnRead</c>), its kinds kept as found on shared maps (<see cref="WorldSource.SharedMaps"/>)
    /// and never written as seen. The places with a map icon, which the game itself tells every
    /// client of (<c>ZoneSystem.GetLocationIcons</c>), are kept as the places on every map
    /// (<see cref="WorldSource.OnEveryMap"/>), looked at every half minute. The cells are worked
    /// out on a worker thread from copies taken here; nothing is read before the character has
    /// stood in the world a while, so its map is in.
    /// </summary>
    internal static class CompendiumMap
    {
        static CompendiumMap() => WorldCaches.Register(nameof(CompendiumMap), Forget);

        /// <summary>How long the character stands in the world before its map is read.</summary>
        private const float SettleSeconds = 10f;

        private static AccessTools.FieldRef<Minimap, BitArray> _exploredBits;
        private static AccessTools.FieldRef<Minimap, BitArray> _sharedBits;

        /// <summary>The world whose shared maps were asked about this session; none while they are to be asked again.</summary>
        private static string _sharedFor;
        private static Task<List<(string Kind, int Count)>> _sharing;
        private static string _sharingFor;

        /// <summary>How often the places on every map are looked at.</summary>
        private const float IconsEvery = 30f;
        private static float _iconsAt = float.NegativeInfinity;
        private static readonly Dictionary<Vector3, string> Icons = new Dictionary<Vector3, string>();
        private static Task<(ExploredGrid Grid, List<(string Kind, int Count)> Counted)> _reading;
        private static string _readingFor;

        /// <summary>The explored map last asked about in this world this session, for the ground new since; none before the first ask.</summary>
        private static ExploredGrid _askedGrid;
        private static string _askedGridFor;

        /// <summary>When the map was last looked at for new ground, and the looking under way on a worker thread: the map now, the ground new since and it packed for the server.</summary>
        private static float _grownAt = float.NegativeInfinity;
        private static Task<(ExploredGrid Now, ExploredGrid New, byte[] Packed)> _growing;
        private static string _growingFor;
        private static Task<List<(string Kind, int Count)>> _growingKinds;
        private static string _growingKindsFor;

        /// <summary>How many times the ground new since was asked about this session, for the self-test.</summary>
        [Diagnostic]
        public static int AskedMore { get; private set; }
        private static string _failedFor;
        private static Player _player;
        private static float _playerSince;
        private static Task<List<string>> _biomes;
        private static string _biomesFor;

        /// <summary>How many of each kind of place the world holds, by the server's census, and lie in each ground, by its answers; the session's, the world's own.</summary>
        public static PlaceCounts Counts { get; } = new PlaceCounts();

        /// <summary>The world the census was taken for here, where this game runs it; none before.</summary>
        private static string _censusFor;

        private static HashSet<Entry> _held;
        private static int _heldVersion;
        private static (int, EntryCatalog, int, bool) _heldStamp;

        /// <summary>
        /// The places the world holds by its census, and their rooms, while *The whole world* shows
        /// it (<see cref="PlaceCensus.Held"/>), with a count that moves as they change; none
        /// otherwise. Worked out again only as the census, the catalog or the choice change.
        /// </summary>
        public static (HashSet<Entry> Held, int Version) HeldPlaces(CompendiumBook book, EntryCatalog catalog)
        {
            var shown = book != null && catalog != null && book.Shows(WorldSource.WholeWorld) == SourceLevel.Counted && Counts.CensusKnown;
            var stamp = (Counts.Version, catalog, catalog?.Regroups ?? -1, shown);
            if (stamp.Equals(_heldStamp)) return (_held, _heldVersion);
            _heldStamp = stamp;
            _held = shown ? PlaceCensus.Held(catalog.All, kind => Counts.Held(kind) > 0) : null;
            _heldVersion++;
            return (_held, _heldVersion);
        }

        /// <summary>The world as the book knows it: its id, as the game keeps the character's map of it; none outside a world.</summary>
        public static string World => ZNet.instance != null ? Stored.Count(ZNet.instance.GetWorldUID()) : null;

        /// <summary>Whether the explored map is being read now.</summary>
        public static bool Reading => _reading != null;

        /// <summary>The worlds whose map was read again this session, each once; kept across worlds left, until the game closes.</summary>
        private static readonly HashSet<string> ReadThisSession = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Whether this world's map is read again this session (<see cref="Update"/>), for the self-test.</summary>
        public static bool ReadAgainThisSession => World != null && ReadThisSession.Contains(World);

        /// <summary>
        /// Once a second: catches the book up when its world has not been, or takes in what was
        /// worked out. Each session reads a world's map once again, so ground explored since
        /// counts too, what it brings news in Learned.
        /// </summary>
        public static void Update(CompendiumBook book)
        {
            var player = Player.m_localPlayer;
            if (player != _player)
            {
                _player = player;
                _playerSince = Time.unscaledTime;
            }
            var world = World;
            if (book != null && player != null && world != null && ReadThisSession.Add(world)) book.Unmap(world);
            var settled = player != null && Time.unscaledTime - _playerSince >= SettleSeconds;
            UpdateBiomes(book, world, settled);
            UpdateIcons(book, world, settled);
            UpdateShared(book, world, settled);
            UpdateGrowth(book, world, settled);
            UpdateCensus(book, world, settled);

            if (_reading != null)
            {
                if (_reading.IsCompleted) Take(book);
                return;
            }
            if (book == null || player == null || world == null || book.Mapped(world) || world == _failedFor) return;
            if (!settled) return;

            if (ZNet.instance.IsServer()) Start(world, Places());
            else CompendiumLink.Ask(world);
        }

        /// <summary>Reads the map again for the character played in this world: for <c>/scry map</c> and the button.</summary>
        public static string Again(CompendiumBook book)
        {
            var world = World;
            if (book == null || world == null) return CompendiumWords.MapNoWorld;
            book.Unmap(world);
            _failedFor = null;
            _biomesFor = null;
            return CompendiumWords.MapAgain;
        }

        /// <summary>Takes in the kinds of place a server told lie in the explored map, and how many of each.</summary>
        public static void Told(CompendiumBook book, string world, List<(string Kind, int Count)> counted)
        {
            if (book == null || world == null || world != World || book.Mapped(world)) return;
            Counts.Took(WorldSource.Explored, counted, whole: true);
            var raised = MapCatchUp.Apply(book, world, Kinds(counted));
            CompendiumKeeper.Noted(PlayEvent.MapRead);
            Log.Report(CompendiumWords.MapRead(counted.Count, raised, fromServer: true));
        }

        /// <summary>Takes in the kinds of place lying in the ground explored since the last ask, and how many, what is new noted as the first reading's is.</summary>
        public static void ToldMore(CompendiumBook book, string world, List<(string Kind, int Count)> counted)
        {
            if (book == null || world == null || world != World) return;
            Counts.Took(WorldSource.Explored, counted, whole: false);
            var raised = MapCatchUp.Apply(book, world, Kinds(counted));
            if (raised == 0) return;
            CompendiumKeeper.Noted(PlayEvent.MapRead);
            Log.Report(CompendiumWords.MapRead(counted.Count, raised, fromServer: !ZNet.instance.IsServer()));
        }

        /// <summary>Takes in the server's census: how many of each kind of place the world holds, every kind kept as the whole world's (<see cref="WorldSource.WholeWorld"/>).</summary>
        public static void ToldCensus(CompendiumBook book, string world, IReadOnlyDictionary<string, int> census)
        {
            if (world == null || world != World) return;
            Counts.Census(census);
            book?.FoundIn(world, WorldSource.WholeWorld, Counts.HeldKinds);
        }

        private static List<string> Kinds(List<(string Kind, int Count)> counted) => counted.ConvertAll(c => c.Kind);

        /// <summary>
        /// The world's census once a session while *The whole world* shows it, as the world's server
        /// knows it: here where this game runs the world, else asked of the server's Scry.
        /// </summary>
        private static void UpdateCensus(CompendiumBook book, string world, bool settled)
        {
            if (book == null || world == null || !settled || book.Shows(WorldSource.WholeWorld) != SourceLevel.Counted || Counts.CensusKnown) return;
            if (!ZNet.instance.IsServer())
            {
                CompendiumLink.AskCensus(world);
                return;
            }
            if (_censusFor == world) return;
            _censusFor = world;
            ToldCensus(book, world, MapCatchUp.Census(Places()));
        }

        /// <summary>The explored map the server is asked about, kept for the ground new since.</summary>
        public static void Asked(string world, ExploredGrid grid)
        {
            _askedGrid = grid;
            _askedGridFor = world;
            _grownAt = Time.unscaledTime;
        }

        /// <summary>
        /// Once the map was caught up with this session: once a minute the map is looked at on a
        /// worker thread for the ground new since the last ask, and where enough is (<see cref="MapGrowth.Due"/>)
        /// its kinds of place are worked out here where this game is the world's server, else asked
        /// of the server's Scry with the new ground alone; a server without Scry is not asked.
        /// </summary>
        private static void UpdateGrowth(CompendiumBook book, string world, bool settled)
        {
            if (_growingKinds != null)
            {
                if (!_growingKinds.IsCompleted) return;
                var done = _growingKinds;
                var doneFor = _growingKindsFor;
                _growingKinds = null;
                _growingKindsFor = null;
                if (done.Status != TaskStatus.RanToCompletion) Faults.Tell(Feature.CompendiumMap, "reading the ground explored since", done.Exception?.GetBaseException());
                else ToldMore(book, doneFor, done.Result);
                return;
            }
            if (_growing != null)
            {
                if (!_growing.IsCompleted) return;
                var done = _growing;
                var doneFor = _growingFor;
                _growing = null;
                _growingFor = null;
                if (done.Status != TaskStatus.RanToCompletion)
                {
                    Faults.Tell(Feature.CompendiumMap, "looking at the ground explored since", done.Exception?.GetBaseException());
                    return;
                }
                var (now, fresh, packed) = done.Result;
                if (doneFor != world || doneFor != _askedGridFor || !MapGrowth.Due(fresh.Count, MapGrowth.Every)) return;
                Asked(world, now);
                AskedMore++;
                if (ZNet.instance.IsServer())
                {
                    var places = Places();
                    _growingKindsFor = world;
                    _growingKinds = Task.Run(() => MapCatchUp.Counted(fresh, places));
                }
                else CompendiumLink.AskMore(world, packed);
                return;
            }
            if (book == null || world == null || !settled || !book.Mapped(world) || world != _askedGridFor || _askedGrid == null) return;
            if (Time.unscaledTime - _grownAt < MapGrowth.Every || CompendiumLink.AskingMore) return;
            if (!ZNet.instance.IsServer() && !ServerScries.Answers(EventLink.Server)) return;
            var (size, pixel, words) = Copy();
            if (words == null) return;
            var before = _askedGrid;
            _grownAt = Time.unscaledTime;
            _growingFor = world;
            _growing = Task.Run(() =>
            {
                var grid = Grid(size, pixel, words);
                var since = grid.NewSince(before);
                return (grid, since, since.Count >= MapGrowth.FewestNew ? Utils.Compress(since.Pack()) : null);
            });
        }

        /// <summary>Takes in the kinds of place a server told lie in the ground from shared maps, and how many of each.</summary>
        public static void ToldShared(CompendiumBook book, string world, List<(string Kind, int Count)> counted)
        {
            if (book == null || world == null || world != World) return;
            Counts.Took(WorldSource.SharedMaps, counted, whole: true);
            book.FoundIn(world, WorldSource.SharedMaps, Kinds(counted));
        }

        /// <summary>A map read at a cartography table: the shared ground is asked about again.</summary>
        public static void MapTableRead() => _sharedFor = null;

        /// <summary>The ground from shared maps of the character played, packed on a worker thread from a copy taken now; none where it holds none.</summary>
        public static Task<byte[]> PackSharedOnWorker()
        {
            var (size, pixel, words) = Copy(shared: true);
            return words == null ? null : Task.Run(() => Grid(size, pixel, words).Pack());
        }

        /// <summary>
        /// The ground from shared maps, once a session and again after a map is read at a table,
        /// once the character's own map is caught up: worked out here where this game is the
        /// world's server, else asked of the server's Scry. Ground none was shared of asks nothing.
        /// </summary>
        private static void UpdateShared(CompendiumBook book, string world, bool settled)
        {
            if (_sharing != null)
            {
                if (!_sharing.IsCompleted) return;
                var done = _sharing;
                var doneFor = _sharingFor;
                _sharing = null;
                _sharingFor = null;
                if (done.Status != TaskStatus.RanToCompletion)
                {
                    Faults.Tell(Feature.CompendiumMap, "reading the ground from shared maps", done.Exception?.GetBaseException());
                    return;
                }
                ToldShared(book, doneFor, done.Result);
                return;
            }
            if (book == null || world == null || !settled || world == _sharedFor || !book.Mapped(world)) return;
            if (ZNet.instance.IsServer())
            {
                var (size, pixel, words) = Copy(shared: true);
                _sharedFor = world;
                if (words == null) return;
                var places = Places();
                _sharingFor = world;
                _sharing = Task.Run(() => MapCatchUp.Counted(Grid(size, pixel, words), places));
            }
            else if (CompendiumLink.AskShared(world)) _sharedFor = world;
        }

        /// <summary>The places with a map icon, as the game tells every client of them, kept as the places on every map; every half minute, as more are placed while the world is explored.</summary>
        private static void UpdateIcons(CompendiumBook book, string world, bool settled)
        {
            var zones = ZoneSystem.instance;
            if (book == null || world == null || !settled || zones == null || Time.unscaledTime - _iconsAt < IconsEvery) return;
            _iconsAt = Time.unscaledTime;
            Icons.Clear();
            zones.GetLocationIcons(Icons);
            book.FoundIn(world, WorldSource.OnEveryMap, Icons.Values);
        }

        /// <summary>Gives up on this world for the session, as when its server has no Scry to ask.</summary>
        public static void GiveUp(string world) => _failedFor = world;

        /// <summary>The explored map of the character played, read and packed for the server on a worker thread from a copy taken now; none before it is in.</summary>
        public static Task<(ExploredGrid Grid, byte[] Packed)> GridOnWorker()
        {
            var (size, pixel, words) = Copy();
            if (words == null) return null;
            return Task.Run(() =>
            {
                var grid = Grid(size, pixel, words);
                return (grid, Utils.Compress(grid.Pack()));
            });
        }

        /// <summary>The explored map of the character played, packed on a worker thread from a copy taken now; none before it is in.</summary>
        public static Task<byte[]> PackOnWorker()
        {
            var (size, pixel, words) = Copy();
            return words == null ? null : Task.Run(() => Grid(size, pixel, words).Pack());
        }

        /// <summary>The explored map of the character played, packed in cells about as wide as given, the places' own by default; none before it is in.</summary>
        public static byte[] PackedMap(float cellMetres = ExploredGrid.PlaceCellMetres)
        {
            var (size, pixel, words) = Copy();
            return words == null ? null : Grid(size, pixel, words, cellMetres).Pack();
        }

        /// <summary>Where every place this game holds is, by its kind: only a world's server holds them all.</summary>
        public static List<(string Kind, float X, float Z)> Places()
        {
            var places = new List<(string, float, float)>();
            var zones = ZoneSystem.instance;
            if (zones == null) return places;
            foreach (var instance in zones.m_locationInstances.Values)
            {
                var location = instance.m_location;
                if (location == null) continue;
                places.Add((NameOf(location), instance.m_position.x, instance.m_position.z));
            }
            return places;
        }

        /// <summary>A placed location's prefab name, as its kind goes by.</summary>
        public static string NameOf(ZoneSystem.ZoneLocation location) => string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefab.Name : location.m_prefabName;

        /// <summary>Works out on a worker thread which kinds of place lie in the explored map.</summary>
        private static void Start(string world, List<(string Kind, float X, float Z)> places)
        {
            var (size, pixel, words) = Copy();
            if (words == null) return;
            _readingFor = world;
            _reading = Task.Run(() =>
            {
                var grid = Grid(size, pixel, words);
                return (grid, MapCatchUp.Counted(grid, places));
            });
        }

        private static void Take(CompendiumBook book)
        {
            var reading = _reading;
            var world = _readingFor;
            _reading = null;
            _readingFor = null;
            if (reading.Status != TaskStatus.RanToCompletion)
            {
                _failedFor = world;
                Faults.Tell(Feature.CompendiumMap, "reading the explored map", reading.Exception?.GetBaseException());
                return;
            }
            if (book == null || world != World || book.Mapped(world)) return;
            Asked(world, reading.Result.Grid);
            Counts.Took(WorldSource.Explored, reading.Result.Counted, whole: true);
            var raised = MapCatchUp.Apply(book, world, Kinds(reading.Result.Counted));
            CompendiumKeeper.Noted(PlayEvent.MapRead);
            Log.Report(CompendiumWords.MapRead(reading.Result.Counted.Count, raised, fromServer: false));
        }

        /// <summary>
        /// The biomes under the ground the character explored are seen (<see cref="MapCatchUp.Biomes"/>),
        /// worked out once per world each session on a worker thread, as the game's own heightmap
        /// builder asks the world generator from its thread; client or server alike, the world
        /// generator being the same everywhere.
        /// </summary>
        private static void UpdateBiomes(CompendiumBook book, string world, bool settled)
        {
            if (_biomes != null)
            {
                if (!_biomes.IsCompleted) return;
                var done = _biomes;
                _biomes = null;
                if (done.Status != TaskStatus.RanToCompletion)
                {
                    Faults.Tell(Feature.CompendiumMap, "reading the biomes of the explored map", done.Exception?.GetBaseException());
                    return;
                }
                if (book != null && world == _biomesFor) MapCatchUp.ApplyBiomes(book, done.Result);
                return;
            }
            var generator = WorldGenerator.instance;
            if (book == null || world == null || world == _biomesFor || !settled || generator == null) return;
            var (size, pixel, words) = Copy();
            if (words == null) return;
            _biomesFor = world;
            // Coarser cells for the biomes, which need no finer and ask the generator once a cell.
            _biomes = Task.Run(() => MapCatchUp.Biomes(Grid(size, pixel, words, ExploredGrid.BiomeCellMetres), (x, z) => generator.GetBiome(x, z).ToString()));
        }

        /// <summary>
        /// The map's size, its pixels' width and a copy of its explored bits, the character's own or
        /// those from shared maps; no bits before the character's map is in (it always has the
        /// ground it stands on explored), nor where none were shared.
        /// </summary>
        private static (int Size, float Pixel, int[] Words) Copy(bool shared = false)
        {
            var map = Minimap.instance;
            if (map == null) return (0, 0f, null);
            if (_exploredBits == null) _exploredBits = AccessTools.FieldRefAccess<Minimap, BitArray>("m_explored");
            if (_sharedBits == null) _sharedBits = AccessTools.FieldRefAccess<Minimap, BitArray>("m_exploredOthers");
            var bits = shared ? _sharedBits(map) : _exploredBits(map);
            var size = map.m_textureSize;
            if (bits == null || size <= 0 || bits.Length < size * size) return (0, 0f, null);
            var words = new int[(bits.Length + 31) / 32];
            bits.CopyTo(words, 0);
            return Array.TrueForAll(words, w => w == 0) ? (0, 0f, null) : (size, map.m_pixelSize, words);
        }

        private static ExploredGrid Grid(int size, float pixel, int[] words, float cellMetres = ExploredGrid.PlaceCellMetres) =>
            ExploredGrid.FromMap(size, pixel, i => (words[i >> 5] & (1 << (i & 31))) != 0, cellMetres);

        private static void Forget()
        {
            _sharedFor = null;
            _sharing = null;
            _sharingFor = null;
            _iconsAt = float.NegativeInfinity;
            Icons.Clear();
            _reading = null;
            _readingFor = null;
            _askedGrid = null;
            _askedGridFor = null;
            _grownAt = float.NegativeInfinity;
            _growing = null;
            _growingFor = null;
            _growingKinds = null;
            _growingKindsFor = null;
            _censusFor = null;
            Counts.Forget();
            _held = null;
            _heldStamp = default;
            _heldVersion++;
            _failedFor = null;
            _player = null;
            _biomes = null;
            _biomesFor = null;
        }
    }

    /// <summary>A map read at a cartography table brings shared ground to ask about (<see cref="CompendiumMap.MapTableRead"/>).</summary>
    [HarmonyPatch(typeof(MapTable), "OnRead", typeof(Switch), typeof(Humanoid), typeof(ItemDrop.ItemData), typeof(bool))]
    internal static class MapTableRead
    {
        private static void Postfix(bool __result)
        {
            if (__result) Guard.Run(Feature.CompendiumMap, "noting a shared map read", CompendiumMap.MapTableRead);
        }
    }
}
