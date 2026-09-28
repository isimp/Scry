using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SoftReferenceableAssets;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Where things are found in the world's locations and dungeon rooms, read when asked (the
    /// panel's "Find in locations" buttons, or <c>/scry locations</c>). The game keeps them
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
            Summoned.Clear();
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
            Plugin.Log.LogInfo($"Scry stopped reading the locations after {done} of them; nothing read is kept.");
            return "stopped reading the locations and dungeons; nothing read so far is kept.";
        }

        /// <summary>Starts reading, and says what it does.</summary>
        public static string Start()
        {
            if (Now == State.Reading) return $"already reading the locations and dungeons, {Done} of {Total} so far.";
            if (Now == State.Read) return "the locations and dungeons of this world are read already.";
            var zones = ZoneSystem.instance;
            if (zones == null || Session.Explorer == null) return "the catalog of this world is not read yet.";

            Forget();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var location in zones.m_locations)
            {
                if (location == null || !location.m_enable || !location.m_prefab.IsValid) continue;
                var name = string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefab.Name : location.m_prefabName;
                if (seen.Add("location|" + name)) Queue.Add(new Asset { Name = name, Reference = location.m_prefab });
            }
            var rooms = DungeonDB.instance != null ? DungeonDB.GetRooms() : null;
            if (rooms != null)
            {
                foreach (var room in rooms)
                {
                    if (room == null || !room.m_prefab.IsValid) continue;
                    var name = room.m_prefab.Name;
                    if (seen.Add("room|" + name)) Queue.Add(new Asset { Name = name, Room = true, Reference = room.m_prefab });
                }
            }
            if (Queue.Count == 0) return "this world has no locations or dungeon rooms to read.";

            Now = State.Reading;
            _next = 0;
            _failed = 0;
            _workMs = 0.0;
            _frames = 0;
            _clock = Stopwatch.StartNew();
            return $"reading {Queue.Count} locations and dungeon rooms in the background.";
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

        /// <summary>The places the one being read stands for: a location by its name, a room by its kinds of dungeon.</summary>
        private static void PlacesOf(GameObject prefab)
        {
            Here.Clear();
            if (!_current.Room)
            {
                Here.Add(Places.LocationLabel(_current.Name));
                return;
            }
            var room = prefab.GetComponent<Room>();
            var themes = room != null ? room.m_theme.ToString() : "";
            foreach (var theme in themes.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (theme != "None") Here.Add(Places.RoomLabel(theme));
            }
            if (Here.Count == 0) Here.Add(Places.RoomLabel("Dungeon"));
        }

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

            foreach (var field in DropTablesOf(component.GetType()))
            {
                if (!(field.GetValue(component) is DropTable table) || table.m_drops == null) continue;
                foreach (var drop in table.m_drops) if (drop.m_item != null) Add(drop.m_item.name);
            }
        }

        private static readonly Dictionary<Type, System.Reflection.FieldInfo[]> DropTableFields = new Dictionary<Type, System.Reflection.FieldInfo[]>();

        /// <summary>The drop table fields of a type and its bases, found once per type.</summary>
        private static System.Reflection.FieldInfo[] DropTablesOf(Type type)
        {
            if (DropTableFields.TryGetValue(type, out var known)) return known;
            var found = new List<System.Reflection.FieldInfo>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            try
            {
                for (var t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(Component) && t != typeof(object); t = t.BaseType)
                {
                    foreach (var field in t.GetFields(flags)) if (field.FieldType == typeof(DropTable)) found.Add(field);
                }
            }
            catch (Exception ex)
            {
                Faults.Skip("reading of a type's fields", type.Name, ex);
                found.Clear();
            }
            known = found.ToArray();
            DropTableFields[type] = known;
            return known;
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
                explorer.Regrouped();
            }
            Facts.Forget();
            Plugin.Log.LogInfo(
                $"Scry read {Total} locations and dungeon rooms in {_clock.Elapsed.TotalSeconds:0.0} s ({_workMs:0} ms of its own work over {_frames} frames, {_failed} could not be loaded): " +
                $"they name {Found.Count} prefabs, {inCatalog} of them in the catalog; {moved} effects, sounds and projectiles nothing else plays or fires went under \"In locations\".");
            Found.Clear();
            Named.Clear();
            Faults.TellSkipped();
        }
    }
}
