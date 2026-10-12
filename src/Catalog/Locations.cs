using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
    /// kept from one session to the next in BepInEx's cache folder (<see cref="KeptPlaces"/>). On
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

        /// <summary>Whether places are being read, or what was kept of them found, in the background.</summary>
        public static bool Busy => _reading || _loading != null;

        /// <summary>Whether what was read is still being written to the cache folder.</summary>
        public static bool Saving => _saving != null;

        /// <summary>How often what the places hold was put on the catalog's entries in this session, for what is worked out from it to know when to work it out again.</summary>
        public static int Applied { get; private set; }

        /// <summary>How many of the places being read are read so far, and how many there are to read.</summary>
        public static int Done { get; private set; }

        public static int Total => ToRead.Count;

        /// <summary>On entering the world, how many of its places were kept holding the same, kept but read again, and never kept.</summary>
        public static (int Fresh, int Stale, int Missing) Found { get; private set; }

        /// <summary>A few milliseconds a frame, as the catalog is read in the background.</summary>
        private const double BudgetMs = 3.0;

        private const string KeptFile = "places.dat";

        private struct Asset
        {
            public string Name;
            public bool Room;
            public SoftReference<GameObject> Reference;

            /// <summary>The biomes the world's list places a location in, all of its entries together.</summary>
            public Heightmap.Biome Biome;

            /// <summary>The build of Scry, the mods and the bundle it is read from (<see cref="PlaceStamps"/>); empty while not known.</summary>
            public string Stamp;
        }

        /// <summary>The world's places, each once, its locations first so each dungeon is named before the rooms it is built with.</summary>
        private static readonly List<Asset> World = new List<Asset>();

        /// <summary>Those being read, by their place in <see cref="World"/>.</summary>
        private static readonly List<int> ToRead = new List<int>();

        /// <summary>Every place kept, this world's and others', from the cache folder and the readings since.</summary>
        private static KeptPlaces _kept = new KeptPlaces();

        /// <summary>What the reading under way has read so far, kept once it is done.</summary>
        private static readonly List<PlaceRecord> Fresh = new List<PlaceRecord>();

        /// <summary>Places whose contents could not be read whole: told this session, kept for none, so they are read again.</summary>
        private static readonly HashSet<(string Prefab, bool Room)> Unkept = new HashSet<(string, bool)>();

        /// <summary>What was kept, and each of the world's places' stamps, found on a worker thread on entering it.</summary>
        private static Task<(KeptPlaces Kept, string[] Stamps)> _loading;

        private static bool _automatically;
        private static Task _saving;

        /// <summary>The stamp of each of the world's places, by prefab and whether a room, once found on entering it.</summary>
        private static readonly Dictionary<(string Prefab, bool Room), string> StampsOf = new Dictionary<(string, bool), string>();

        /// <summary>A place's stamp (<see cref="PlaceStamps"/>), for what else is kept of it; empty while not known.</summary>
        public static string StampOf(string prefab, bool room) => StampsOf.TryGetValue((prefab, room), out var stamp) ? stamp : "";

        /// <summary>The altars found in the locations: which boss each summons, with what, and where it stands.</summary>
        private static readonly List<Summon> Summoned = new List<Summon>();

        /// <summary>The bosses the locations' altars summon, once known; none before.</summary>
        public static IReadOnlyList<Summon> Summons => Now == State.Read ? Summoned : (IReadOnlyList<Summon>)NoSummons;

        private static readonly List<Summon> NoSummons = new List<Summon>();

        /// <summary>The groups of the entries what the places hold moved to another (effects only places play, items only creatures seemed to have), as they were, to be put back before it is told anew.</summary>
        private static readonly Dictionary<Entry, (string Group, int Order)> GroupsBefore = new Dictionary<Entry, (string, int)>();

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
            _loading = null;
            World.Clear();
            StampsOf.Clear();
            Summoned.Clear();
            GroupsBefore.Clear();
            CatalogNames.Clear();
            Unkept.Clear();
            Named.Clear();
            _kept = new KeptPlaces();
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
        /// worker thread (<see cref="Update"/> takes it), then shown, and what may have changed read
        /// again in the background; with nothing kept, they wait to be asked for, or are read by
        /// themselves when <paramref name="automatically"/>.
        /// </summary>
        public static void Begin(bool automatically)
        {
            StopReading();
            _loading = null;
            _automatically = automatically;
            if (!List()) return;

            var path = Path.Combine(Settings.CacheFolder, KeptFile);
            var manifest = Path.Combine(SoftReferenceableAssets.Shared.GetBuildDirectory(), "manifest");
            var ids = World.Select(a => a.Reference.m_assetID).ToArray();
            var build = typeof(Locations).Assembly.ManifestModule.ModuleVersionId.ToString("N");
            var mods = BepInEx.Bootstrap.Chainloader.PluginInfos.Values
                .Where(i => i?.Metadata != null).Select(i => (i.Metadata.GUID ?? "", i.Metadata.Version?.ToString() ?? "")).ToList();
            _loading = Task.Run(() => (ReadKept(path), Stamps(manifest, ids, build, mods)));
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

        /// <summary>What was kept in the cache folder; nothing for a file of another kind or none. On a worker thread.</summary>
        private static KeptPlaces ReadKept(string path)
        {
            var file = FileSwap.ToRead(path);
            return KeptPlaces.Load(file != null ? File.ReadAllText(file) : null);
        }

        /// <summary>
        /// Each place's stamp: the bundle the game's own list of them names for it (its <c>SoftRef</c>
        /// manifest, read as <c>AssetBundleManifest.DeserializeFromDisk</c> reads it), by its file's
        /// size and when it was written, or "mod" for one the list does not have, which a mod adds;
        /// none where the list or the file cannot be read. On a worker thread.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string[] Stamps(string manifestPath, AssetID[] ids, string build, List<(string, string)> mods)
        {
            var stamps = new string[ids.Length];
            var manifest = File.Exists(manifestPath) ? SoftReferenceableAssets.AssetBundleManifest.DeserializeFromDisk(manifestPath) : null;
            var folder = manifest?.GetAbsoluteBundlesDirectory();
            for (var i = 0; i < ids.Length; i++)
            {
                var source = "";
                if (manifest != null)
                {
                    if (!manifest.m_assetToLocationMap.TryGetValue(ids[i], out var at)) source = "mod";
                    else
                    {
                        var file = new FileInfo(Path.Combine(folder, at.m_bundleName));
                        if (file.Exists) source = PlaceStamps.Bundle(at.m_bundleName, file.Length, file.LastWriteTimeUtc.Ticks);
                    }
                }
                stamps[i] = PlaceStamps.Of(build, mods, source);
            }
            return stamps;
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

        /// <summary>Starts reading every place, or reads every one again where they are known, and says what it does.</summary>
        public static string Start()
        {
            if (_reading) return LocationWords.AlreadyReading(Done, Total);
            if (_loading != null) return "still finding what is kept of this world's locations from before; ask again in a moment.";
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
            if (_saving != null && _saving.IsCompleted)
            {
                if (_saving.IsFaulted) Faults.Tell(Feature.PlacesKept, "saving the places read", _saving.Exception?.GetBaseException());
                _saving = null;
            }

            if (_loading != null)
            {
                if (!_loading.IsCompleted) return;
                var loading = _loading;
                _loading = null;
                Guard.Run(Feature.PlacesKept, "showing the places kept", () => Loaded(loading));
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
        private static void Loaded(Task<(KeptPlaces Kept, string[] Stamps)> loading)
        {
            if (loading.Status == TaskStatus.RanToCompletion)
            {
                _kept = loading.Result.Kept;
                for (var i = 0; i < World.Count; i++)
                {
                    var asset = World[i];
                    asset.Stamp = loading.Result.Stamps[i];
                    World[i] = asset;
                    StampsOf[(asset.Name, asset.Room)] = asset.Stamp;
                }
            }
            else
            {
                Faults.Tell(Feature.PlacesKept, "finding the places kept", loading.Exception?.GetBaseException());
                _kept = new KeptPlaces();
            }

            var plan = PlacePlan.Of(_kept, World.Select(a => (a.Name, a.Room, a.Stamp)).ToList(), CatalogNames.Contains);
            Found = (plan.Fresh, plan.Stale, plan.Missing);
            if (plan.Shown.Count > 0)
            {
                Apply(Records(), new HashSet<(string, bool)>());
                Now = State.Read;
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
        /// on the catalog's entries with everything else kept of the world's places, and is written
        /// to the cache folder on a worker thread.
        /// </summary>
        private static void Finish()
        {
            _reading = false;
            var read = new HashSet<(string, bool)>();
            foreach (var record in Fresh)
            {
                read.Add((record.Prefab, record.Room));
                if (!Unkept.Contains((record.Prefab, record.Room))) _kept.Put(record);
            }
            var named = Apply(Records(), read);
            Now = State.Read;

            // Written after any saving still under way, so the newest is the one left in place.
            var text = _kept.Save();
            var path = Path.Combine(Settings.CacheFolder, KeptFile);
            var before = _saving;
            _saving = Task.Run(() =>
            {
                ((IAsyncResult)before)?.AsyncWaitHandle.WaitOne();
                FileSwap.Write(path, aside => File.WriteAllText(aside, text));
            });

            Log.Report(
                $"Scry read {Numbers.Count(Total)} locations and dungeon rooms in {Numbers.Fixed(_clock.Elapsed.TotalSeconds, 1)} s ({Numbers.Amount(_workMs, 0)} ms of its own work over {Numbers.Count(_frames)} frames, {Numbers.Count(_failed)} could not be loaded) and keeps them for the next time: " +
                $"the world's places name {Numbers.Count(named.Prefabs)} prefabs, {Numbers.Count(named.InCatalog)} entries of the catalog are found in them; {Numbers.Count(named.Moved)} effects, sounds and projectiles nothing else plays or fires went under \"In locations\", and {Numbers.Count(named.Items)} items only creatures seemed to have went back to their kind of item.");
            Fresh.Clear();
            Faults.TellSkipped();
        }

        /// <summary>What is known of the world's places, in its order: each one read in this reading, else as kept.</summary>
        private static List<PlaceRecord> Records()
        {
            var fresh = Fresh.ToDictionary(r => (r.Prefab, r.Room));
            var records = new List<PlaceRecord>();
            foreach (var asset in World)
            {
                var record = fresh.TryGetValue((asset.Name, asset.Room), out var now) ? now : _kept.Find(asset.Name, asset.Room);
                if (record != null) records.Add(record);
            }
            return records;
        }

        /// <summary>
        /// Tells what the places hold on the catalog's entries, from their records in the world's
        /// order, each told in this session's terms (<see cref="PlaceContents.Tell"/>): a location by
        /// its name (<see cref="Places.LocationLabel"/>), noting the kinds of room its dungeons are
        /// built with, then a room by those dungeons; where each thing they name is found; its
        /// altars; each place's contents on its entry, where it has none or was read just now
        /// (<paramref name="read"/>). What was told before is put back first, so telling anew
        /// leaves nothing of it.
        /// </summary>
        private static (int Prefabs, int InCatalog, int Moved, int Items) Apply(List<PlaceRecord> records, HashSet<(string, bool)> read)
        {
            var world = WorldCatalog.Current;
            if (world == null) return (0, 0, 0, 0);
            var catalog = world.All;
            var creatures = PlaceEntries.CreatureNames(catalog);
            var terms = PlaceReader.TermsNow();
            var biomes = World.Where(a => !a.Room).GroupBy(a => a.Name).ToDictionary(g => g.Key, g => g.First().Biome);

            var dungeons = new List<KeyValuePair<int, string>>();
            var found = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var labels = new Dictionary<(string, bool), string[]>();
            Summoned.Clear();
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
                        var summon = new Summon { Boss = altar.Boss, Item = altar.Item.Length > 0 ? altar.Item : null, Count = altar.Count, OnStands = altar.OnStands, Place = label };
                        if (!Summoned.Exists(s => s.Boss == summon.Boss && s.Item == summon.Item && s.Place == summon.Place)) Summoned.Add(summon);
                    }
                }
                labels[(record.Prefab, record.Room)] = here;
                foreach (var name in record.Names)
                {
                    if (!found.TryGetValue(name, out var places)) found[name] = places = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var place in here) places.Add(place);
                }
            }

            // What was told before goes back to how it was.
            foreach (var pair in GroupsBefore)
            {
                pair.Key.Group = pair.Value.Group;
                pair.Key.GroupOrder = pair.Value.Order;
            }
            GroupsBefore.Clear();
            foreach (var entry in catalog) if (!(entry.Source is PlaceSource)) entry.FoundIn = Array.Empty<string>();

            // Each place goes by its name first: where things are found is ordered by the biome of the location going by each.
            foreach (var entry in catalog)
            {
                if (entry.Source is PlaceSource place && labels.TryGetValue((place.Prefab, place.IsRoom), out var named)) PlaceEntries.Named(entry, named);
            }

            var before = catalog.Select(e => (e.Group, e.GroupOrder)).ToList();
            Places.Apply(catalog, found);
            var moved = 0;
            var inCatalog = 0;
            for (var i = 0; i < before.Count; i++)
            {
                if (catalog[i].FoundIn.Length > 0) inCatalog++;
                if (before[i].Group != catalog[i].Group) moved++;
            }
            var items = Grouping.FoundInLocations(catalog);
            for (var i = 0; i < before.Count; i++)
            {
                var entry = catalog[i];
                if (before[i].Group != entry.Group || before[i].GroupOrder != entry.GroupOrder) GroupsBefore[entry] = before[i];
            }

            var byPlace = records.ToDictionary(r => (r.Prefab, r.Room));
            foreach (var entry in catalog)
            {
                if (!(entry.Source is PlaceSource place)) continue;
                var key = (place.Prefab, place.IsRoom);
                if (byPlace.TryGetValue(key, out var record) && !Unkept.Contains(key) && (place.Contents == null || read.Contains(key))) place.Contents = record.Contents;
            }
            PlaceEntries.Arrange(catalog);
            world.Regroup();
            Applied++;
            Learned.AboutAll();
            return (found.Count, inCatalog, moved, items);
        }
    }
}
