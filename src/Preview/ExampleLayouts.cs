using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// An example layout of the dungeon or camp in the location selected (<see cref="DungeonLayout"/>).
    /// The rooms of its kinds are kept by the game only as soft references to asset bundles, so
    /// those not read yet are loaded one at a time in the background and read
    /// (<see cref="PlaceReader"/>, which fills their own entries too); once all are in, the
    /// example is laid out, and laid out anew on asking for another. The example is then built
    /// on the stage from copies of its rooms (<see cref="Stage.StepExample"/>), so the bundles of
    /// the kinds it uses are held while it is shown, and the rest let go of a frame after the
    /// stage has let go of their copies. Closing the panel or leaving the location lets go of all.
    /// </summary>
    internal static class ExampleLayouts
    {
        /// <summary>Leaving a world lets go of what is held of it (<see cref="WorldCaches"/>).</summary>
        static ExampleLayouts() => WorldCaches.Register(nameof(ExampleLayouts), Forget);

        /// <summary>A few milliseconds a frame for reading rooms that have loaded.</summary>
        private const double BudgetMs = 3.0;

        private static readonly System.Random Seeds = new System.Random();

        /// <summary>The location the example is of, and its rooms in the order the game lists them.</summary>
        private static Entry _entry;
        private static readonly List<Entry> Rooms = new List<Entry>();
        private static int _next;
        private static PlaceSource _loading;

        public static DungeonExample Example { get; private set; }

        /// <summary>How many of its rooms could not be loaded, left out of the example.</summary>
        public static int Failed { get; private set; }

        /// <summary>How many kinds of room it is built of, and how many have been read.</summary>
        public static int Total => Rooms.Count;
        public static int Read => Rooms.Count(e => ((PlaceSource)e.Source).Contents?.Room != null);

        /// <summary>How many rooms' bundles are held, the one loading among them, for the resource monitor.</summary>
        public static int HeldCount => Held.Count + (_loading != null ? 1 : 0);

        /// <summary>Whether a room's bundle is held while it loads, for the self-test to see nothing is left held.</summary>
        public static bool Holding => _loading != null || Held.Count > 0;

        /// <summary>The bundles held for the stage's copies, by room prefab.</summary>
        private static readonly Dictionary<string, PlaceSource> Held = new Dictionary<string, PlaceSource>(StringComparer.Ordinal);

        /// <summary>The example whose kinds of room are held, and the frame from which the others may be let go of.</summary>
        private static DungeonExample _heldFor;
        private static int _releaseFrom = -1;

        /// <summary>Whether the example shown is of this entry.</summary>
        public static bool Of(Entry entry) => entry != null && entry == _entry;

        /// <summary>Lets go of any room held, and forgets the example.</summary>
        public static void Forget()
        {
            LetGoOfAll();
            _entry = null;
            Rooms.Clear();
            _next = 0;
            Failed = 0;
            Example = null;
        }

        /// <summary>Lets go of every room held, loaded again when the panel opens; the example stays.</summary>
        public static void Pause() => LetGoOfAll();

        /// <summary>Lays out another example of the same.</summary>
        public static void Another()
        {
            if (_entry == null || Example == null) return;
            Build();
        }

        /// <summary>Keeps to the selected location, reading its rooms a little each frame until the example is laid out.</summary>
        public static void Update(Explorer explorer)
        {
            var entry = explorer.Selected;
            var place = entry?.Source as PlaceSource;
            var plan = place != null && !place.IsRoom ? place.Contents?.Dungeon : null;
            if (plan == null)
            {
                if (_entry != null) Forget();
                return;
            }
            if (entry != _entry) Start(explorer, entry, plan);
            if (Example == null) ReadRooms();
            if (Example == null) return;

            // Its rooms' copies on the stage, from the bundles of the kinds it uses.
            HoldFor(Example);
            Stage.StepExample(_entry, Example, plan, RoomModel);
            if (_releaseFrom >= 0 && Time.frameCount >= _releaseFrom) LetGoOfUnused();
        }

        /// <summary>Loads and reads the rooms not read yet, a few milliseconds a frame, one bundle at a time, and lays out the example once all are in.</summary>
        private static void ReadRooms()
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalMilliseconds < BudgetMs)
            {
                if (_loading == null)
                {
                    while (_next < Rooms.Count && ((PlaceSource)Rooms[_next].Source).Contents?.Room != null) _next++;
                    if (_next >= Rooms.Count)
                    {
                        Build();
                        return;
                    }
                    _loading = (PlaceSource)Rooms[_next].Source;
                    // Holds a reference until let go of, as Load does; loads over the next frames.
                    _loading.Reference.LoadAsync();
                    return;
                }

                if (!_loading.Reference.IsLoaded && _loading.Reference.IsLoading) return;
                var asset = _loading.Reference.IsLoaded ? _loading.Reference.Asset : null;
                if (asset == null)
                {
                    Failed++;
                    Faults.Skip("example layouts", _loading.Prefab, "its room did not load");
                }
                else
                {
                    var reading = _loading;
                    var room = Rooms[_next];
                    if (!Guard.Each("dungeon rooms", reading.Prefab, () =>
                    {
                        reading.Contents = PlaceReader.Read(asset, true);
                        Learned.About(room);
                    })) Failed++;
                }

                // Kept for the stage, which is likely to want it next; let go of if it did not load.
                if (asset != null && !Held.ContainsKey(_loading.Prefab)) Held[_loading.Prefab] = _loading;
                else _loading.Reference.Release();
                _loading = null;
                _next++;
            }
        }

        /// <summary>Holds the bundle of every kind of room the example uses, asking for those not held; the rest are let go of shortly.</summary>
        private static void HoldFor(DungeonExample example)
        {
            if (ReferenceEquals(_heldFor, example)) return;
            _heldFor = example;
            foreach (var room in example.Rooms)
            {
                var name = room.Room.Name;
                if (Held.ContainsKey(name)) continue;
                var source = Rooms.Select(e => (PlaceSource)e.Source).FirstOrDefault(s => s.Prefab == name);
                if (source == null) continue;
                // Holds a reference until let go of; loads over the next frames.
                source.Reference.LoadAsync();
                Held[name] = source;
            }
            _releaseFrom = Time.frameCount + 2;
        }

        /// <summary>A room's model for the stage: true once it has loaded, with null when it could not be.</summary>
        private static bool RoomModel(string prefab, out GameObject model)
        {
            model = null;
            if (!Held.TryGetValue(prefab, out var source)) return true;
            if (source.Reference.IsLoaded)
            {
                model = source.Reference.Asset;
                return true;
            }
            return !source.Reference.IsLoading;
        }

        /// <summary>Lets go of the bundles the example shown does not use.</summary>
        private static void LetGoOfUnused()
        {
            _releaseFrom = -1;
            var used = new HashSet<string>(Example?.Rooms.Select(r => r.Room.Name) ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            foreach (var name in Held.Keys.Where(n => !used.Contains(n)).ToList())
            {
                // Let go of once no copy is being made from it.
                var reference = Held[name].Reference;
                Ghost.Building.WhenIdle(() => reference.Release());
                Held.Remove(name);
            }
        }

        private static void LetGoOfAll()
        {
            LetGo();
            // Let go of once no copy is being made from them.
            foreach (var source in Held.Values)
            {
                var reference = source.Reference;
                Ghost.Building.WhenIdle(() => reference.Release());
            }
            Held.Clear();
            _heldFor = null;
            _releaseFrom = -1;
        }

        /// <summary>The rooms of a dungeon's kinds, enabled, in the order the game lists them (<c>DungeonGenerator.SetupAvailableRooms</c>).</summary>
        private static void Start(Explorer explorer, Entry entry, DungeonPlan plan)
        {
            Forget();
            _entry = entry;
            var byPrefab = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var e in explorer.Catalog)
            {
                if (e.Source is PlaceSource p && p.IsRoom && !byPrefab.ContainsKey(p.Prefab)) byPrefab[p.Prefab] = e;
            }
            var rooms = DungeonDB.instance != null ? DungeonDB.GetRooms() : null;
            if (rooms == null) return;
            foreach (var room in rooms)
            {
                if (room == null || !room.m_enabled || ((int)room.m_theme & plan.Themes) == 0 || !room.m_prefab.IsValid) continue;
                if (byPrefab.TryGetValue(room.m_prefab.Name, out var found)) Rooms.Add(found);
            }
        }

        /// <summary>Lays out an example with dice of its own, in a zone of its own.</summary>
        private static void Build()
        {
            var place = (PlaceSource)_entry.Source;
            var plan = place.Contents.Dungeon;
            var shapes = Rooms.Select(e => ((PlaceSource)e.Source).Contents?.Room).Where(r => r != null).ToList();
            var rules = place.Rules.FirstOrDefault();
            var radius = rules != null ? Mathf.Max(rules.m_exteriorRadius, rules.m_interiorRadius) : 0f;
            var turned = rules == null || rules.m_randomRotation || rules.m_slopeRotation;
            var dice = new Dice(Seeds.Next());
            var wasNone = Example == null;
            Example = DungeonLayout.Build(plan, shapes, DungeonLayout.Site(plan, radius, turned, dice), dice);

            // The kinds of room it is built of are listed by what each is, known now, and ranked
            // under their dungeon as entrances, rooms and end caps.
            if (wasNone)
            {
                Learned.About(_entry);
                var world = WorldCatalog.Current;
                if (world != null)
                {
                    PlaceEntries.Arrange(world.All);
                    world.Regroup();
                }
            }
        }

        private static void LetGo()
        {
            if (_loading == null) return;
            _loading.Reference.Release();
            _loading = null;
        }
    }
}
