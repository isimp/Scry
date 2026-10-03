using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using SoftReferenceableAssets;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Where things are found in the world's locations and dungeon rooms, read when asked (the
    /// panel's "Read all locations" buttons, or <c>/scry locations</c>). The game keeps them
    /// only as soft references to asset bundles (<c>ZoneSystem.m_locations</c>, <c>DungeonDB</c>'s
    /// rooms), loaded while it builds a zone; loading all of them at once stalls the game for
    /// seconds, so one at a time is loaded in the background, read a few milliseconds a frame, and
    /// released. A release unloads the bundle with everything in it once nothing else holds it, so
    /// only names are kept. What each names (placed there, in its fields, in its effect lists) is put
    /// on the catalog's entries (<see cref="Places"/>), for the world being played.
    /// </summary>
    internal static class Locations
    {
        static Locations() => WorldCaches.Register(nameof(Locations), Forget);

        public enum State
        {
            NotRead,
            Reading,
            Read,
        }

        public static State Now { get; private set; }

        /// <summary>How many of the locations and rooms are read so far, and how many there are.</summary>
        public static int Done { get; private set; }

        public static int Total => Queue.Count;

        /// <summary>A few milliseconds a frame, as the catalog is read in the background.</summary>
        private const double BudgetMs = 3.0;

        private struct Asset
        {
            public string Name;
            public bool Room;
            public SoftReference<GameObject> Reference;

            /// <summary>The biomes the world's list places a location in, all of its entries together.</summary>
            public Heightmap.Biome Biome;
        }

        private static readonly List<Asset> Queue = new List<Asset>();

        /// <summary>The altars found in the locations: which boss each summons, with what, and where it stands.</summary>
        private static readonly List<Summon> Summoned = new List<Summon>();

        /// <summary>The bosses the locations' altars summon, once all are read; none while reading.</summary>
        public static IReadOnlyList<Summon> Summons => Now == State.Read ? Summoned : (IReadOnlyList<Summon>)NoSummons;

        private static readonly List<Summon> NoSummons = new List<Summon>();
        private static readonly Dictionary<string, HashSet<string>> Found = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private static readonly List<GameObject> Named = new List<GameObject>();
        private static readonly List<string> Here = new List<string>();

        /// <summary>The dungeons read so far: the kinds of room each is built with, and its place's name.</summary>
        private static readonly List<KeyValuePair<int, string>> Dungeons = new List<KeyValuePair<int, string>>();

        /// <summary>What each location and room was read to hold, and the places each stands for, by its prefab name, for their entries.</summary>
        private static readonly Dictionary<string, PlaceContents> ReadLocations = new Dictionary<string, PlaceContents>(StringComparer.Ordinal);
        private static readonly Dictionary<string, PlaceContents> ReadRooms = new Dictionary<string, PlaceContents>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string[]> PlaceLabels = new Dictionary<string, string[]>(StringComparer.Ordinal);

        /// <summary>Creatures' shown names by prefab name, for the words of places named after them.</summary>
        private static Dictionary<string, string> _creatures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static int _next;
        private static int _failed;

        // The one being read: held from its load until it is released.
        private static bool _holding;
        private static Asset _current;
        private static Component[] _parts;
        private static Transform _root;
        private static int _part;

        private static Stopwatch _clock;
        private static double _workMs;
        private static int _frames;

        /// <summary>Lets go of the one being read, and forgets what was found: the world was left.</summary>
        public static void Forget()
        {
            ReleaseCurrent();
            Queue.Clear();
            Found.Clear();
            Named.Clear();
            Here.Clear();
            Dungeons.Clear();
            Summoned.Clear();
            ReadLocations.Clear();
            ReadRooms.Clear();
            PlaceLabels.Clear();
            Now = State.NotRead;
            Done = 0;
            _clock = null;
        }

        /// <summary>
        /// Stops reading and says so. Nothing read so far is kept: what the locations hold shows
        /// only once all are read, so a reading stopped leaves the world as it was before it.
        /// </summary>
        public static string Stop()
        {
            if (Now != State.Reading) return "not reading the locations and dungeons.";
            var done = Done;
            Forget();
            Plugin.Log.LogInfo($"Scry stopped reading the locations after {Numbers.Count(done)} of them; nothing read is kept.");
            return "stopped reading the locations and dungeons; nothing read so far is kept.";
        }

        /// <summary>Starts reading, and says what it does.</summary>
        public static string Start()
        {
            if (Now == State.Reading) return $"already reading the locations and dungeons, {Numbers.Count(Done)} of {Numbers.Count(Total)} so far.";
            if (Now == State.Read) return "the locations and dungeons of this world are read already.";
            var zones = ZoneSystem.instance;
            if (zones == null || Session.Explorer == null) return "the catalog of this world is not read yet.";

            Forget();
            _creatures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Session.Explorer.Catalog)
            {
                if (entry.Kind == Kind.Creature && !string.IsNullOrEmpty(entry.DisplayName) && !_creatures.ContainsKey(entry.Name)) _creatures[entry.Name] = entry.DisplayName;
            }

            // Locations first, so each dungeon is named before the rooms it is built with.
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var location in zones.m_locations)
            {
                if (location == null || !location.m_enable || !location.m_prefab.IsValid) continue;
                var name = string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefab.Name : location.m_prefabName;
                if (seen.TryGetValue(name, out var at))
                {
                    var known = Queue[at];
                    known.Biome |= location.m_biome;
                    Queue[at] = known;
                    continue;
                }
                seen[name] = Queue.Count;
                Queue.Add(new Asset { Name = name, Reference = location.m_prefab, Biome = location.m_biome });
            }
            var roomsSeen = new HashSet<string>(StringComparer.Ordinal);
            var rooms = DungeonDB.instance != null ? DungeonDB.GetRooms() : null;
            if (rooms != null)
            {
                foreach (var room in rooms)
                {
                    if (room == null || !room.m_prefab.IsValid) continue;
                    var name = room.m_prefab.Name;
                    if (roomsSeen.Add(name)) Queue.Add(new Asset { Name = name, Room = true, Reference = room.m_prefab });
                }
            }
            if (Queue.Count == 0) return "this world has no locations or dungeon rooms to read.";

            Now = State.Reading;
            _next = 0;
            _failed = 0;
            _workMs = 0.0;
            _frames = 0;
            _clock = Stopwatch.StartNew();
            return $"reading {Numbers.Count(Queue.Count)} locations and dungeon rooms in the background.";
        }

        /// <summary>Reads on for a few milliseconds; called every frame.</summary>
        public static void Update()
        {
            if (Now != State.Reading) return;
            if (ZoneSystem.instance == null)
            {
                Forget();
                return;
            }

            var watch = Stopwatch.StartNew();
            _frames++;
            try
            {
                while (watch.Elapsed.TotalMilliseconds < BudgetMs)
                {
                    if (!_holding)
                    {
                        if (_next >= Queue.Count)
                        {
                            Finish();
                            return;
                        }
                        _current = Queue[_next++];
                        _parts = null;
                        _part = 0;
                        // Holds a reference until released, as Load does; loads over the next frames.
                        _holding = true;
                        _current.Reference.LoadAsync();
                        return;
                    }

                    if (_parts == null)
                    {
                        if (!_current.Reference.IsLoaded)
                        {
                            if (_current.Reference.IsLoading) return;
                            _failed++;
                            Next();
                            continue;
                        }
                        var prefab = _current.Reference.Asset;
                        if (prefab == null)
                        {
                            _failed++;
                            Next();
                            continue;
                        }
                        PlacesOf(prefab);
                        _root = prefab.transform;
                        _parts = prefab.GetComponentsInChildren<Component>(true);
                        continue;
                    }

                    var end = Math.Min(_parts.Length, _part + 32);
                    while (_part < end) Read(_parts[_part++]);
                    if (_part >= _parts.Length) Next();
                }
            }
            catch (Exception ex)
            {
                // One odd location costs only itself.
                Faults.Skip("locations", _current.Name, ex);
                _failed++;
                Next();
            }
            finally
            {
                _workMs += watch.Elapsed.TotalMilliseconds;
            }
        }

        /// <summary>
        /// The places the one being read stands for: a location by its name (<see cref="Places"/>),
        /// noting the kinds of room any dungeon in it is built with; a room by those dungeons.
        /// </summary>
        private static void PlacesOf(GameObject prefab)
        {
            Here.Clear();
            PlaceContents contents = null;
            try { contents = PlaceReader.Read(prefab, _current.Room); }
            catch (Exception ex) { Faults.Skip("locations", _current.Name, ex); }

            if (_current.Room)
            {
                var theme = 0;
                try { theme = ThemeOf(prefab); }
                catch (Exception ex) { Faults.Skip("names of dungeon rooms", _current.Name, ex); }
                Here.AddRange(Places.RoomLabels(theme, Dungeons));
                if (contents != null) ReadRooms[_current.Name] = contents;
                PlaceLabels["room:" + _current.Name] = Here.ToArray();
                return;
            }

            var facts = new PlaceFacts { Prefab = _current.Name, Biome = BiomeOf(_current.Biome) };
            if (contents != null)
            {
                facts.GameName = contents.GameName;
                facts.Boss = contents.Boss;
                facts.Trader = contents.Trader;
            }
            var themes = new List<int>();
            try { DungeonThemes(prefab, themes); }
            catch (Exception ex) { Faults.Skip("names of locations", _current.Name, ex); }
            var label = Places.LocationLabel(facts, _creatures);
            Here.Add(label);
            foreach (var kinds in themes) Dungeons.Add(new KeyValuePair<int, string>(kinds, label));
            if (contents != null) ReadLocations[_current.Name] = contents;
            PlaceLabels[_current.Name] = new[] { label };
        }

        /// <summary>The kinds of room a dungeon room is (<c>Room.m_theme</c>, flags).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ThemeOf(GameObject prefab)
        {
            var room = prefab.GetComponent<Room>();
            return room != null ? (int)room.m_theme : 0;
        }

        /// <summary>
        /// The kinds of room each dungeon in a location is built with (<c>DungeonGenerator.m_themes</c>).
        /// What names it is read with the rest of it (<see cref="PlaceReader"/>): the name shown on
        /// entering its dungeon (<c>Teleport.m_enterText</c>) or on discovering it
        /// (<c>Location.m_discoverLabel</c>), the boss its altar summons and the trader there.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DungeonThemes(GameObject prefab, List<int> themes)
        {
            foreach (var generator in prefab.GetComponentsInChildren<DungeonGenerator>(true))
            {
                if (generator != null && generator.m_themes != 0) themes.Add((int)generator.m_themes);
            }
        }

        /// <summary>The biomes a location is placed in as the game shows them, or none when that is every one or none.</summary>
        private static string BiomeOf(Heightmap.Biome biome) => PlaceEntries.BiomeWords(biome);

        /// <summary>
        /// What one part names: itself, when it is a networked prefab placed there; what its fields
        /// name; what its drop tables hold (a chest's loot, what a pickable or a rock gives); what
        /// its effect lists play.
        /// </summary>
        private static void Read(Component component)
        {
            if (component == null || component is Transform) return;
            if (component is ZNetView && component.transform != _root) Add(PrefabName(component.gameObject.name));

            if (component is OfferingBowl bowl)
            {
                var summon = Summon.Of(bowl, Here.Count > 0 ? Here[0] : _current.Name, null);
                if (summon.Boss != null && !Summoned.Exists(s => s.Boss == summon.Boss && s.Item == summon.Item && s.Place == summon.Place)) Summoned.Add(summon);
            }

            foreach (var on in CatalogBuilder.ListsOn(component))
            {
                var members = on.List?.m_effectPrefabs;
                if (members == null) continue;
                foreach (var data in members) if (data?.m_prefab != null) Add(data.m_prefab.name);
            }

            Named.Clear();
            Relations.PrefabsNamedBy(component, Named);
            foreach (var thing in Named) if (thing != null) Add(thing.name);

            foreach (var field in Knowledge.DropTables(component.GetType()))
            {
                if (!(field.GetValue(component) is DropTable table) || table.m_drops == null) continue;
                foreach (var drop in table.m_drops) if (drop.m_item != null) Add(drop.m_item.name);
            }
        }

        private static void Add(string name)
        {
            if (!Found.TryGetValue(name, out var places)) Found[name] = places = new HashSet<string>(StringComparer.Ordinal);
            foreach (var place in Here) places.Add(place);
        }

        /// <summary>A placed copy's prefab name: without Unity's " (1)" and "(Clone)".</summary>
        private static string PrefabName(string name)
        {
            var cut = name.IndexOf(" (", StringComparison.Ordinal);
            if (cut < 0) cut = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return cut > 0 ? name.Substring(0, cut) : name;
        }

        private static void Next()
        {
            ReleaseCurrent();
            Done++;
        }

        private static void ReleaseCurrent()
        {
            _parts = null;
            _root = null;
            if (!_holding) return;
            _holding = false;
            _current.Reference.Release();
        }

        private static void Finish()
        {
            Now = State.Read;
            var explorer = Session.Explorer;
            var moved = 0;
            var items = 0;
            var inCatalog = 0;
            if (explorer != null)
            {
                var before = explorer.Catalog.Select(e => e.Group).ToList();
                Places.Apply(explorer.Catalog, Found);
                for (var i = 0; i < before.Count; i++)
                {
                    var entry = explorer.Catalog[i];
                    if (entry.FoundIn.Length > 0) inCatalog++;
                    if (before[i] != entry.Group) moved++;
                }
                items = Grouping.FoundInLocations(explorer.Catalog);
                foreach (var entry in explorer.Catalog)
                {
                    if (!(entry.Source is PlaceSource place)) continue;
                    var read = place.IsRoom ? ReadRooms : ReadLocations;
                    if (place.Contents == null && read.TryGetValue(place.Prefab, out var contents)) place.Contents = contents;
                    if (PlaceLabels.TryGetValue(place.IsRoom ? "room:" + place.Prefab : place.Prefab, out var labels)) PlaceEntries.Named(entry, labels);
                }
                PlaceEntries.Arrange(explorer.Catalog);
                explorer.Regrouped();
            }
            Learned.AboutAll();
            Plugin.Log.LogInfo(
                $"Scry read {Numbers.Count(Total)} locations and dungeon rooms in {Numbers.Fixed(_clock.Elapsed.TotalSeconds, 1)} s ({Numbers.Amount(_workMs, 0)} ms of its own work over {Numbers.Count(_frames)} frames, {Numbers.Count(_failed)} could not be loaded): " +
                $"they name {Numbers.Count(Found.Count)} prefabs, {Numbers.Count(inCatalog)} of them in the catalog; {Numbers.Count(moved)} effects, sounds and projectiles nothing else plays or fires went under \"In locations\", and {Numbers.Count(items)} items only creatures seemed to have went back to their kind of item.");
            Found.Clear();
            Named.Clear();
            ReadLocations.Clear();
            ReadRooms.Clear();
            PlaceLabels.Clear();
            Faults.TellSkipped();
        }
    }
}
