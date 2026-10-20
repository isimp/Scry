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
    /// Where things are found in the world's locations and dungeon rooms, and what each holds. The
    /// game keeps them only as soft references to asset bundles (<c>ZoneSystem.m_locations</c>,
    /// <c>DungeonDB</c>'s rooms), loaded while it builds a zone; loading all of them at once stalls
    /// the game for seconds, so one at a time is loaded in the background, read a few milliseconds
    /// a frame, and released. A release unloads the bundle with everything in it once nothing else
    /// holds it, so only names and figures are kept: a record of each place (<see cref="PlaceRecord"/>),
    /// kept from one session to the next in BepInEx's cache folder (<see cref="PlaceRecords"/>). On
    /// entering a world what was kept is shown at once and only what may have changed is read again
    /// (<see cref="PlacePlan"/>); with nothing kept, the places are read when asked (the panel's
    /// "Read all locations" buttons, or <c>/scry locations</c>) or by themselves with
    /// <c>ReadLocationsAutomatically</c>. What each names (placed there, in its fields, in its
    /// effect lists) is put on the catalog's entries (<see cref="Places"/>), for the world being played.
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

        /// <summary>What is known of what the places hold: nothing yet, nothing yet while they are read, or known (kept from before, or read), some perhaps being read again meanwhile.</summary>
        public static State Now { get; private set; }

        /// <summary>Whether places are being read, what was kept of them found, or what they hold told on the catalog, in the background.</summary>
        public static bool Busy => _reading || PlaceRecords.Finding || _telling != null;

        /// <summary>Told as what was kept of the places is forgotten (<see cref="ForgetKept"/>), for what else is kept of them, their floors, to go with it.</summary>
        public static event Action Forgetting;

        /// <summary>Whether places are being read, the first time or again.</summary>
        public static bool Reading => _reading;

        /// <summary>Whether what was read is still being written to the cache folder.</summary>
        public static bool Saving => PlaceRecords.Busy;

        /// <summary>How often what the places hold was put on the catalog's entries in this session, for what is worked out from it to know when to work it out again.</summary>
        public static int Applied { get; private set; }

        /// <summary>How many of the places being read are read so far, and how many there are to read.</summary>
        public static int Done { get; private set; }

        public static int Total => ToRead.Count;

        /// <summary>On entering the world, how many of its places were kept holding the same, kept but read again, and never kept.</summary>
        public static (int Fresh, int Stale, int Missing) Found { get; private set; }

        /// <summary>A few milliseconds a frame, as the catalog is read in the background.</summary>
        private const double BudgetMs = 3.0;

        private struct Asset
        {
            public string Name;
            public bool Room;
            public SoftReference<GameObject> Reference;

            /// <summary>The biomes the world's list places a location in, all of its entries together.</summary>
            public Heightmap.Biome Biome;

            /// <summary>The build of Scry, the mods and the bundle it is read from (<see cref="PlaceStamps"/>), once found (<see cref="PlaceRecords"/>); empty while not known.</summary>
            public string Stamp;
        }

        /// <summary>The world's places, each once, its locations first so each dungeon is named before the rooms it is built with.</summary>
        private static readonly List<Asset> World = new List<Asset>();

        /// <summary>Those being read, by their place in <see cref="World"/>.</summary>
        private static readonly List<int> ToRead = new List<int>();

        /// <summary>What the reading under way has read so far, kept once it is done.</summary>
        private static readonly List<PlaceRecord> Fresh = new List<PlaceRecord>();

        /// <summary>Places whose contents could not be read whole: told this session, kept for none, so they are read again.</summary>
        private static readonly HashSet<(string Prefab, bool Room)> Unkept = new HashSet<(string, bool)>();

        private static bool _automatically;

        /// <summary>The altars found in the locations: which boss each summons, with what, and where it stands.</summary>
        private static readonly List<Summon> Summoned = new List<Summon>();

        /// <summary>The bosses the locations' altars summon, once known; none before.</summary>
        public static IReadOnlyList<Summon> Summons => Now == State.Read ? Summoned : (IReadOnlyList<Summon>)NoSummons;

        private static readonly List<Summon> NoSummons = new List<Summon>();

        /// <summary>What the places hold, told on this world's catalog each time from the catalog as it was made.</summary>
        private static PlaceFinds _finds = new PlaceFinds();

        /// <summary>What the places hold being told on the catalog, a few milliseconds a frame (<see cref="Telling"/>); null while nothing is.</summary>
        private static IEnumerator<bool> _telling;

        /// <summary>The names of the catalog's prefabs, for how many of what a place names the catalog holds.</summary>
        private static readonly HashSet<string> CatalogNames = new HashSet<string>(StringComparer.Ordinal);

        private static readonly List<GameObject> Named = new List<GameObject>();

        private static int _next;
        private static int _failed;
        private static bool _reading;

        // The one being read: held from its load until it is released.
        private static bool _holding;
        private static Asset _current;
        private static Transform _root;
        private static PlaceRecord _record;
        private static readonly HashSet<string> RecordNames = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Its contents being read a part of the reading at a time, then its parts walked a few at a time.</summary>
        private static IEnumerator<bool> _steps;
        private static PreOrderWalk<Transform> _walk;
        private static readonly List<Component> Components = new List<Component>();

        private static Stopwatch _clock;
        private static double _workMs;
        private static int _frames;

        /// <summary>Lets go of the one being read, and forgets what was found: the world was left.</summary>
        public static void Forget()
        {
            StopReading();
            _telling = null;
            World.Clear();
            Summoned.Clear();
            _finds = new PlaceFinds();
            CatalogNames.Clear();
            Unkept.Clear();
            Named.Clear();
            Now = State.NotRead;
            Found = (0, 0, 0);
        }

        /// <summary>Ends a reading, keeping nothing of it.</summary>
        private static void StopReading()
        {
            ReleaseCurrent();
            _reading = false;
            ToRead.Clear();
            Fresh.Clear();
            Done = 0;
            _clock = null;
        }

        /// <summary>
        /// On entering a world, once its catalog is read: what was kept of its places is found on a
        /// worker thread (<see cref="PlaceRecords"/>; <see cref="Update"/> takes it), then shown, and what may have changed read
        /// again in the background; with nothing kept, they wait to be asked for, or are read by
        /// themselves when <paramref name="automatically"/>.
        /// </summary>
        public static void Begin(bool automatically)
        {
            StopReading();
            _telling = null;
            _automatically = automatically;
            if (!List())
            {
                PlaceRecords.Forget();
                return;
            }
            PlaceRecords.Begin(World.Select(a => (a.Name, a.Room, a.Reference.m_assetID)).ToList());
        }

        /// <summary>Lists the world's places, each once, its locations first; false for a world with none or no catalog yet.</summary>
        private static bool List()
        {
            World.Clear();
            var zones = ZoneSystem.instance;
            var catalog = WorldCatalog.Current;
            if (zones == null || catalog == null) return false;

            CatalogNames.Clear();
            foreach (var entry in catalog.All) if (!EntryKeys.HasOwnNamespace(entry.Kind)) CatalogNames.Add(entry.Name);

            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var location in zones.m_locations)
            {
                if (location == null || !location.m_enable || !location.m_prefab.IsValid) continue;
                var name = string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefab.Name : location.m_prefabName;
                if (seen.TryGetValue(name, out var at))
                {
                    var known = World[at];
                    known.Biome |= location.m_biome;
                    World[at] = known;
                    continue;
                }
                seen[name] = World.Count;
                World.Add(new Asset { Name = name, Reference = location.m_prefab, Biome = location.m_biome, Stamp = "" });
            }
            var roomsSeen = new HashSet<string>(StringComparer.Ordinal);
            var rooms = DungeonDB.instance != null ? DungeonDB.GetRooms() : null;
            if (rooms != null)
            {
                foreach (var room in rooms)
                {
                    if (room == null || !room.m_prefab.IsValid) continue;
                    var name = room.m_prefab.Name;
                    if (roomsSeen.Add(name)) World.Add(new Asset { Name = name, Room = true, Reference = room.m_prefab, Stamp = "" });
                }
            }
            return World.Count > 0;
        }

        /// <summary>
        /// Stops reading and says so. Nothing read in it is kept: what it reads shows only once all is
        /// read, so a reading stopped leaves the world as it was before it.
        /// </summary>
        public static string Stop()
        {
            if (!_reading) return "not reading the locations and dungeons.";
            var done = Done;
            StopReading();
            Log.Report($"Scry stopped reading the locations after {Numbers.Count(done)} of them; nothing read is kept.");
            if (Now == State.Read) return "stopped reading the locations and dungeons again; what was known of them stays shown.";
            Now = State.NotRead;
            return "stopped reading the locations and dungeons; nothing read so far is kept.";
        }

        /// <summary>
        /// Forgets everything kept of the places, what each holds and where its floors are, in this
        /// session and in the cache folder, for when what is kept seems wrong: the world's places are
        /// told as never read and every one is read afresh in the background; says what it does.
        /// </summary>
        public static string ForgetKept()
        {
            if (PlaceRecords.Finding) return "still finding what is kept of this world's locations from before; ask again in a moment.";
            if (ZoneSystem.instance == null || WorldCatalog.Current == null) return "the catalog of this world is not read yet.";
            PlaceRecords.Clear();
            Forgetting?.Invoke();
            StopReading();
            Unkept.Clear();
            if (World.Count == 0) List();
            Now = State.Reading;
            _telling = Telling(new List<PlaceRecord>(), new HashSet<(string, bool)>(), null, forgetting: true).GetEnumerator();
            Read(Enumerable.Range(0, World.Count));
            if (Total == 0) Now = State.NotRead;
            Log.Report($"Scry forgot what it kept of the locations and their floors, and reads {Numbers.Count(Total)} of them afresh.");
            return LocationWords.ReadingAfresh(Total);
        }

        /// <summary>Starts reading every place, or reads every one again where they are known, and says what it does.</summary>
        public static string Start()
        {
            if (_reading) return LocationWords.AlreadyReading(Done, Total);
            if (PlaceRecords.Finding) return "still finding what is kept of this world's locations from before; ask again in a moment.";
            if (ZoneSystem.instance == null || WorldCatalog.Current == null) return "the catalog of this world is not read yet.";
            if (World.Count == 0 && !List()) return "this world has no locations or dungeon rooms to read.";

            var again = Now == State.Read;
            Read(Enumerable.Range(0, World.Count));
            return again ? LocationWords.ReadingAgain(Total) : LocationWords.ReadingInBackground(Total);
        }

        private static void Read(IEnumerable<int> places)
        {
            StopReading();
            ToRead.AddRange(places);
            if (ToRead.Count == 0) return;
            _reading = true;
            if (Now == State.NotRead) Now = State.Reading;
            _next = 0;
            _failed = 0;
            _workMs = 0.0;
            _frames = 0;
            _clock = Stopwatch.StartNew();
        }

        /// <summary>Takes what was kept once it is found, and reads on for a few milliseconds; called every frame.</summary>
        public static void Update()
        {
            PlaceRecords.Update();
            if (PlaceRecords.Finding)
            {
                var stamps = PlaceRecords.Take();
                if (stamps == null) return;
                Guard.Run(Feature.PlacesKept, "showing the places kept", () => Loaded(stamps));
            }

            // A reading waits while what the places hold is told.
            if (_telling != null)
            {
                if (!Guard.Run(Feature.Locations, "telling what the places hold", TellOn)) _telling = null;
                return;
            }
            if (!_reading) return;
            if (ZoneSystem.instance == null)
            {
                Forget();
                return;
            }

            var watch = Stopwatch.StartNew();
            _frames++;
            // One odd location costs only itself.
            if (!Guard.Each(Feature.Locations, "locations", () => _current.Name, () => Step(watch)))
            {
                _failed++;
                Next();
            }
            _workMs += watch.Elapsed.TotalMilliseconds;
        }

        /// <summary>What was kept, found: shown, and what may have changed read again; with nothing kept, read only by itself or when asked.</summary>
        private static void Loaded(string[] stamps)
        {
            for (var i = 0; i < World.Count; i++)
            {
                var asset = World[i];
                asset.Stamp = stamps[i];
                World[i] = asset;
            }

            var plan = PlacePlan.Of(PlaceRecords.Kept, World.Select(a => (a.Name, a.Room, a.Stamp)).ToList(), CatalogNames.Contains);
            Found = (plan.Fresh, plan.Stale, plan.Missing);
            if (plan.Shown.Count > 0)
            {
                _telling = Telling(Records(), new HashSet<(string, bool)>(), null).GetEnumerator();
                Log.Report($"Scry knows what {Numbers.Count(plan.Shown.Count)} locations and dungeon rooms hold from before ({Numbers.Count(plan.Fresh)} the same, {Numbers.Count(plan.Stale)} to read again, {Numbers.Count(plan.Missing)} never read).");
            }
            if (plan.ToRead.Count > 0 && (plan.Shown.Count > 0 || _automatically))
            {
                Read(plan.ToRead);
                Log.Report("Scry: " + (Now == State.Read ? LocationWords.ReadingAgain(Total) : LocationWords.ReadingInBackground(Total)));
            }
        }

        /// <summary>Reads on, within the frame's share, a location's parts a few at a time and the next once one is done.</summary>
        private static void Step(Stopwatch watch)
        {
            while (watch.Elapsed.TotalMilliseconds < BudgetMs)
            {
                if (!_holding)
                {
                    if (_next >= ToRead.Count)
                    {
                        Finish();
                        return;
                    }
                    _current = World[ToRead[_next++]];
                    _steps = null;
                    _walk = null;
                    // Holds a reference until released, as Load does; loads over the next frames.
                    _holding = true;
                    _current.Reference.LoadAsync();
                    return;
                }

                if (_walk == null && _steps == null)
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
                    _root = prefab.transform;
                    _steps = Begun(prefab).GetEnumerator();
                    continue;
                }

                // What it holds, a part of the reading at a time; each part of it walks the whole
                // location for one kind of part, so it is timed on its own.
                if (_steps != null)
                {
                    var step = Timing.Start();
                    var more = _steps.MoveNext();
                    Timing.Add("locations contents", step);
                    if (more) continue;
                    _steps = null;
                    _walk = new PreOrderWalk<Transform>(_root, t => t.childCount, (t, i) => t.GetChild(i));
                    continue;
                }

                // Then each of its parts, a few at a time, in the order the prefab holds them.
                for (var n = 0; n < 8 && _walk.Next(out var part); n++)
                {
                    part.GetComponents(Components);
                    foreach (var component in Components) Read(component);
                }
                Components.Clear();
                if (_walk.Done) Next();
            }
        }

        /// <summary>
        /// Reads the one being read into its record (<see cref="PlaceReader"/>): its contents, and
        /// the kinds of room any dungeon in it is built with, or the kind of room it is.
        /// </summary>
        private static IEnumerable<bool> Begun(GameObject prefab)
        {
            _record = new PlaceRecord { Prefab = _current.Name, Room = _current.Room, Stamp = _current.Stamp ?? "" };
            RecordNames.Clear();
            var steps = PlaceReader.Steps(prefab, _current.Room, _record.Contents).GetEnumerator();
            var whole = true;
            while (true)
            {
                var more = false;
                if (!Guard.Each(Feature.Locations, "locations", _current.Name, () => more = steps.MoveNext()))
                {
                    whole = false;
                    break;
                }
                if (!more) break;
                yield return true;
            }
            if (!whole) Unkept.Add((_record.Prefab, _record.Room));
            else Unkept.Remove((_record.Prefab, _record.Room));

            if (_current.Room)
            {
                var theme = 0;
                Guard.Each(Feature.Locations, "names of dungeon rooms", _current.Name, () => theme = ThemeOf(prefab));
                _record.Themes.Add(theme);
                yield break;
            }
            Guard.Each(Feature.Locations, "names of locations", _current.Name, () => DungeonThemes(prefab, _record.Themes));
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
        /// its effect lists play. An altar is kept with the boss it summons.
        /// </summary>
        private static void Read(Component component)
        {
            if (component == null || component is Transform) return;
            if (component is ZNetView && component.transform != _root) Add(PlaceReader.PrefabName(component.gameObject.name));

            if (component is OfferingBowl bowl)
            {
                var summon = Summon.Of(bowl, "", null);
                if (summon.Boss != null) _record.Altars.Add(new PlaceAltar { Boss = summon.Boss, Item = summon.Item ?? "", Count = summon.Count, OnStands = summon.OnStands });
            }

            foreach (var on in CatalogBuilder.ListsOn(component))
            {
                foreach (var slot in EffectSlots.Of(on.List)) if (EffectSlots.Plays(slot)) Add(slot.m_prefab.name);
            }

            Named.Clear();
            Relations.PrefabsNamedBy(component, Named);
            foreach (var thing in Named) if (thing != null) Add(thing.name);

            foreach (var field in Knowledge.DropTables(component.GetType()))
            {
                if (!(TypeFields.Value(field, component) is DropTable table) || table.m_drops == null) continue;
                foreach (var drop in table.m_drops) if (drop.m_item != null) Add(drop.m_item.name);
            }
        }

        private static void Add(string name)
        {
            if (!RecordNames.Add(name)) return;
            _record.Names.Add(name);
            if (CatalogNames.Contains(name)) _record.Known++;
        }

        private static void Next()
        {
            if (_record != null && _walk != null && _walk.Done) Fresh.Add(_record);
            _record = null;
            ReleaseCurrent();
            Done++;
        }

        private static void ReleaseCurrent()
        {
            _steps = null;
            _walk = null;
            _root = null;
            _record = null;
            if (!_holding) return;
            _holding = false;
            _current.Reference.Release();
        }

        /// <summary>
        /// The reading is done: what it read takes the place of what was kept of the same, is told
        /// on the catalog's entries with everything else kept of the world's places over the next
        /// frames (<see cref="Telling"/>), and is written to the cache folder on a worker thread
        /// (<see cref="PlaceRecords.Write"/>).
        /// </summary>
        private static void Finish()
        {
            _reading = false;
            var read = new HashSet<(string, bool)>();
            foreach (var record in Fresh)
            {
                read.Add((record.Prefab, record.Room));
                if (!Unkept.Contains((record.Prefab, record.Room))) PlaceRecords.Put(record);
            }
            var records = Records();
            Fresh.Clear();
            PlaceRecords.Write();

            var reading = $"Scry read {Numbers.Count(Total)} locations and dungeon rooms in {Numbers.Fixed(_clock.Elapsed.TotalSeconds, 1)} s ({Numbers.Amount(_workMs, 0)} ms of its own work over {Numbers.Count(_frames)} frames, {Numbers.Count(_failed)} could not be loaded) and keeps them for the next time: ";
            _telling = Telling(records, read, named =>
            {
                Log.Report(reading +
                    $"the world's places name {Numbers.Count(named.Prefabs)} prefabs, {Numbers.Count(named.InCatalog)} entries of the catalog are found in them; {Numbers.Count(named.Moved)} effects, sounds and projectiles nothing else plays or fires went under \"In locations\", and {Numbers.Count(named.Items)} items only creatures seemed to have went back to their kind of item.");
                Faults.TellSkipped();
            }).GetEnumerator();
        }

        /// <summary>Tells on within the frame's share; a step asking for a frame of its own starts the next.</summary>
        private static void TellOn()
        {
            var watch = Stopwatch.StartNew();
            var timed = Timing.Start();
            while (watch.Elapsed.TotalMilliseconds < BudgetMs)
            {
                if (!_telling.MoveNext())
                {
                    _telling = null;
                    break;
                }
                if (!_telling.Current) break;
            }
            Timing.Add("locations told", timed);
        }

        /// <summary>What is known of the world's places, in its order: each one read in this reading, else as kept.</summary>
        private static List<PlaceRecord> Records()
        {
            var fresh = Fresh.ToDictionary(r => (r.Prefab, r.Room));
            var records = new List<PlaceRecord>();
            foreach (var asset in World)
            {
                var record = fresh.TryGetValue((asset.Name, asset.Room), out var now) ? now : PlaceRecords.Kept.Find(asset.Name, asset.Room);
                if (record != null) records.Add(record);
            }
            return records;
        }

        /// <summary>
        /// Tells what the places hold on the catalog's entries, a step at a time (<see cref="TellOn"/>),
        /// from their records in the world's order, each told in this session's terms
        /// (<see cref="PlaceContents.Tell"/>): a location by its name (<see cref="Places.LocationLabel"/>),
        /// noting the kinds of room its dungeons are built with, then a room by those dungeons; its
        /// altars; on the catalog (<see cref="PlaceFinds"/>), each place's name and where each thing
        /// they name is found, from the catalog as it was made, so telling anew leaves nothing of what
        /// was told before; each place's contents on its entry, where it has none or was read just now
        /// (<paramref name="read"/>), or, <paramref name="forgetting"/> what was kept, on none. Nothing
        /// of it shows until its last step, which puts it all on the catalog at once in a frame of its
        /// own, and gives <paramref name="told"/> how many prefabs the places name, entries are found
        /// in them, went under "In locations" and items went back to their kind.
        /// </summary>
        private static IEnumerable<bool> Telling(List<PlaceRecord> records, HashSet<(string, bool)> read, Action<(int Prefabs, int InCatalog, int Moved, int Items)> told, bool forgetting = false)
        {
            var world = WorldCatalog.Current;
            if (world == null) yield break;
            var catalog = world.All;
            var creatures = PlaceEntries.CreatureNames(catalog);
            var terms = PlaceReader.TermsNow();
            var biomes = World.Where(a => !a.Room).GroupBy(a => a.Name).ToDictionary(g => g.Key, g => g.First().Biome);
            yield return true;

            var dungeons = new List<KeyValuePair<int, string>>();
            var found = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var labels = new Dictionary<(string, bool), string[]>();
            var summons = new List<Summon>();
            foreach (var record in records)
            {
                Guard.Each(Feature.Locations, "telling places", record.Prefab, () => record.Contents.Tell(terms));
                string[] here;
                if (record.Room)
                {
                    here = Places.RoomLabels(record.Themes.FirstOrDefault(), dungeons).ToArray();
                }
                else
                {
                    var c = record.Contents;
                    var facts = new PlaceFacts { Prefab = record.Prefab, Biome = BiomeOf(biomes.TryGetValue(record.Prefab, out var biome) ? biome : Heightmap.Biome.None), GameName = c.GameName, Boss = c.Boss, Trader = c.Trader };
                    var label = Places.LocationLabel(facts, creatures);
                    foreach (var kinds in record.Themes) dungeons.Add(new KeyValuePair<int, string>(kinds, label));
                    here = new[] { label };
                    foreach (var altar in record.Altars)
                    {
                        var summon = new Summon { Boss = altar.Boss, Item = altar.Item.Length > 0 ? altar.Item : null, Count = altar.Count, OnStands = altar.OnStands, Place = label, PlacePrefab = EntryKeys.For(Kind.Location, record.Prefab) };
                        if (!summons.Exists(s => s.Boss == summon.Boss && s.Item == summon.Item && s.Place == summon.Place)) summons.Add(summon);
                    }
                }
                labels[(record.Prefab, record.Room)] = here;
                foreach (var name in record.Names)
                {
                    if (!found.TryGetValue(name, out var places)) found[name] = places = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var place in here) places.Add(place);
                }
                yield return true;
            }

            foreach (var _ in _finds.Steps(catalog, found, labels, PlaceEntries.PlaceOf)) yield return true;

            // All of it put on the catalog at once, in a frame of its own.
            yield return false;
            var published = Timing.Start();
            var (inCatalog, moved) = _finds.Publish();
            var items = Grouping.FoundInLocations(catalog);
            var byPlace = records.ToDictionary(r => (r.Prefab, r.Room));
            foreach (var entry in catalog)
            {
                if (!(entry.Source is PlaceSource place)) continue;
                var key = (place.Prefab, place.IsRoom);
                if (forgetting) place.Contents = null;
                else if (byPlace.TryGetValue(key, out var record) && !Unkept.Contains(key) && (place.Contents == null || read.Contains(key))) place.Contents = record.Contents;
            }
            Summoned.Clear();
            Summoned.AddRange(summons);
            Timing.Add("locations published", published);

            var arranged = Timing.Start();
            PlaceEntries.Arrange(catalog);
            world.Regroup();
            Timing.Add("locations arranged", arranged);
            Applied++;
            if (!forgetting) Now = State.Read;
            var learned = Timing.Start();
            Learned.AboutAll();
            Timing.Add("locations learned", learned);
            told?.Invoke((found.Count, inCatalog, moved, items));
        }
    }
}
