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
    /// Where the player is in or at the dungeon, its rooms are laid out as the world has them
    /// (<see cref="WorldDungeons"/>, <see cref="RealDungeon"/>) in place of an example, until an
    /// example is asked for; with the compendium on, as far as the player has walked it
    /// (<see cref="DungeonFog"/>, <see cref="DungeonWalks"/>), laid out again as that changes.
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

        /// <summary>Whether what is shown is the world's own dungeon, not an example.</summary>
        public static bool ShowsWorlds { get; private set; }

        /// <summary>How many of the world's rooms are of kinds Scry has not read, left out.</summary>
        public static int Unread { get; private set; }

        /// <summary>Whether the world's own dungeon is at hand, the player in or at it.</summary>
        public static bool WorldsAtHand => _world != null;

        /// <summary>The world's own dungeon where the player is in or at it, looked for once a second.</summary>
        private static WorldDungeon _world;
        private static float _lookAgain;

        /// <summary>Whether an example was asked for in place of the world's own dungeon.</summary>
        private static bool _wantExample;

        /// <summary>A dungeon the self-test hands in as the world's own, looked for in place of it.</summary>
        private static WorldDungeon _standIn;

        /// <summary>The map opened from the location's page, shown though its dungeon is not at hand, and the dungeon read from it.</summary>
        private static string _opened;
        private static WorldDungeon _openedWorld;

        /// <summary>The id of the map of the world's own dungeon shown; none for an example.</summary>
        public static string ShownMap => ShowsWorlds ? _wholeId : null;

        /// <summary>How many of a kept map's rooms were walked, of how many, by the kinds of room read for the location shown.</summary>
        public static (int Walked, int Rooms) Count(DungeonMap map)
        {
            var byHash = RoomsByHash();
            return DungeonMaps.Walked(map, hash => byHash.TryGetValue(hash, out var shape) ? shape : null);
        }

        /// <summary>The world's own dungeon whole, before the fog, and the id of its map.</summary>
        private static DungeonExample _whole;
        private static string _wholeId;

        /// <summary>Whether the compendium is set on, and peeking, as the explorer last said.</summary>
        private static bool _fogged, _peeking;

        /// <summary>What the fog was last laid by: the maps' stamp, the compendium set on, and peeking.</summary>
        private static (int Stamp, bool Fogged, bool Peeking) _fogOf;

        /// <summary>The rooms shown only as peeked, by their place in the saved rooms.</summary>
        private static readonly HashSet<int> PeekedAt = new HashSet<int>();

        /// <summary>Whether the compendium hides the rooms of the world's own dungeon not walked.</summary>
        public static bool Fogged { get; private set; }

        /// <summary>The ways on from rooms walked to rooms not, as the world has them (<see cref="DungeonFog.OpenWays"/>); none without the fog.</summary>
        public static List<OpenWay> OpenWays { get; private set; } = new List<OpenWay>();

        /// <summary>How many of the world's own dungeon's rooms were walked, of how many.</summary>
        public static (int Walked, int Rooms) Walked { get; private set; }

        /// <summary>How many rooms are shown only as peeked, end caps and dividers left out.</summary>
        public static int PeekedRooms { get; private set; }

        /// <summary>Whether a room shown is shown only as peeked.</summary>
        public static bool IsPeeked(PlacedRoom room) => ShowsWorlds && room.SavedAt >= 0 && PeekedAt.Contains(room.SavedAt);

        /// <summary>Whether the player stands in the world's own dungeon shown.</summary>
        public static bool YouAreIn => ShowsWorlds && _wholeId != null && DungeonWalks.YouIn == _wholeId;

        /// <summary>Whether a room shown is the one the player stands in.</summary>
        public static bool IsYours(PlacedRoom room) => YouAreIn && room.SavedAt >= 0 && room.SavedAt == DungeonWalks.YouAt;

        /// <summary>How often the world's own dungeon is looked for, in seconds.</summary>
        private const float LookEvery = 1f;

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
            ShowsWorlds = false;
            Unread = 0;
            _world = null;
            _lookAgain = 0f;
            _wantExample = false;
            _standIn = null;
            _opened = null;
            _openedWorld = null;
            _byHash = null;
            _byHashOf = -1;
            ForgetWhole();
        }

        /// <summary>Shows a kept map of the location shown, as far as it was walked, wherever the player is; the dungeon at hand comes first.</summary>
        public static void OpenMap(string id)
        {
            _opened = id;
            _openedWorld = null;
            _wantExample = false;
            _lookAgain = 0f;
        }

        /// <summary>The map opened as a dungeon of the world, the same each time; none where it is not of the location shown or was forgotten.</summary>
        private static WorldDungeon Opened(string location)
        {
            var map = DungeonWalks.Maps.Of(_opened);
            if (map == null || map.Location != location) return null;
            return _openedWorld ?? (_openedWorld = new WorldDungeon { Saved = map.Rooms, Generator = map.Generator, Turn = map.Turn, Id = _opened, Location = map.Location, Entrance = map.Entrance });
        }

        private static void ForgetWhole()
        {
            _whole = null;
            _wholeId = null;
            _fogOf = default;
            PeekedAt.Clear();
            PeekedRooms = 0;
            Fogged = false;
            OpenWays = new List<OpenWay>();
            Walked = default;
        }

        /// <summary>Hands in a dungeon as the world's own, as if the player stood in it, looked for at once; null ends it.</summary>
        [Diagnostic]
        public static void StandInForTest(WorldDungeon world)
        {
            _standIn = world;
            _lookAgain = 0f;
        }

        /// <summary>Lets go of every room held, loaded again when the panel opens; the example stays.</summary>
        public static void Pause() => LetGoOfAll();

        /// <summary>Lays out another example of the same, in place of the world's own dungeon too.</summary>
        public static void Another()
        {
            if (_entry == null || Example == null) return;
            _wantExample = true;
            Build();
        }

        /// <summary>Lays out an example in place of the world's own dungeon.</summary>
        public static void ShowExample()
        {
            if (_entry == null || Example == null || !ShowsWorlds) return;
            _wantExample = true;
            Build();
        }

        /// <summary>Shows the world's own dungeon again in place of the example, where it is at hand.</summary>
        public static void ShowThisDungeon()
        {
            if (_entry == null || Example == null || ShowsWorlds || _world == null) return;
            _wantExample = false;
            Build();
        }

        /// <summary>Keeps to the selected location, reading its rooms a little each frame until the example is laid out.</summary>
        public static void Update(Explorer explorer)
        {
            var entry = explorer.Shown;
            var place = entry?.Source as PlaceSource;
            var plan = place != null && !place.IsRoom ? place.Contents?.Dungeon : null;
            if (plan == null)
            {
                Stage.InsideUnknown = false;
                if (_entry != null) Forget();
                return;
            }
            if (entry != _entry) Start(explorer, entry, plan);
            if (plan.Algorithm == "Dungeon") LookForWorlds(place);
            _fogged = explorer.CompendiumSet;
            _peeking = explorer.Peeking;
            if (ShowsWorlds && _whole != null && _fogOf != (DungeonWalks.Maps.Stamp, _fogged, _peeking)) Refog();
            // A dungeon only seen from outside shows its outside alone; a camp's example is its outside.
            Stage.InsideUnknown = plan.Algorithm == "Dungeon" && !explorer.InsideKnown(entry);
            if (Example == null) ReadRooms();
            if (Example == null) return;

            // Its rooms' copies on the stage, from the bundles of the kinds it uses.
            HoldFor(Example);
            Stage.StepExample(_entry, Example, plan, RoomModel);
            if (_releaseFrom >= 0 && Time.frameCount >= _releaseFrom) LetGoOfUnused();
        }

        /// <summary>
        /// Looks for the world's own dungeon once a second; found while an example is shown that was
        /// not asked for, it is shown in its place.
        /// </summary>
        private static void LookForWorlds(PlaceSource place)
        {
            if (Time.unscaledTime < _lookAgain) return;
            _lookAgain = Time.unscaledTime + LookEvery;
            if (!Guard.Run(Feature.DungeonMaps, "reading the dungeon the player is in", () => _standIn ?? WorldDungeons.Here(place.Prefab) ?? Opened(place.Prefab), out var found)) found = null;
            if (ReferenceEquals(found, _world)) return;
            _world = found;
            if (Example != null && found != null && (!ShowsWorlds || _wholeId != found.Id) && !_wantExample) Build();
            // Its map forgotten and the dungeon not at hand, an example stands in its place.
            else if (Example != null && found == null && ShowsWorlds && DungeonWalks.Maps.Of(_wholeId) == null) Build();
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
                    Faults.Skip(Feature.ExampleDungeonAndCampLayouts, "example layouts", _loading.Prefab, "its room did not load");
                }
                else
                {
                    var reading = _loading;
                    var room = Rooms[_next];
                    if (!Guard.Each(Feature.ExampleDungeonAndCampLayouts, "dungeon rooms", reading.Prefab, () =>
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

        /// <summary>
        /// Lays out the world's own dungeon where it is at hand and no example was asked for, each
        /// room by its name's hash among the rooms read; else an example with dice of its own, in a
        /// zone of its own.
        /// </summary>
        private static void Build()
        {
            var place = (PlaceSource)_entry.Source;
            var plan = place.Contents.Dungeon;
            var shapes = Rooms.Select(e => ((PlaceSource)e.Source).Contents?.Room).Where(r => r != null).ToList();
            var wasNone = Example == null;
            if (_world != null && !_wantExample)
            {
                var byHash = RoomsByHash();
                _whole = RealDungeon.Of(_world.Saved, hash => byHash.TryGetValue(hash, out var shape) ? shape : null, _world.Generator, _world.Turn, out var unread);
                _wholeId = _world.Id;
                ShowsWorlds = true;
                Unread = unread;
                Refog();
            }
            else
            {
                var rules = place.Rules.FirstOrDefault();
                var radius = rules != null ? Mathf.Max(rules.m_exteriorRadius, rules.m_interiorRadius) : 0f;
                var turned = rules == null || rules.m_randomRotation || rules.m_slopeRotation;
                var dice = new Dice(Seeds.Next());
                Example = DungeonLayout.Build(plan, shapes, DungeonLayout.Site(plan, radius, turned, dice), dice);
                ShowsWorlds = false;
                Unread = 0;
                ForgetWhole();
            }

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

        /// <summary>The kinds of room read for the location shown, by their names' hashes, as a world saves its rooms.</summary>
        private static Dictionary<int, RoomShape> RoomsByHash()
        {
            var read = Read;
            if (_byHashOf == read && _byHash != null) return _byHash;
            _byHash = new Dictionary<int, RoomShape>();
            foreach (var e in Rooms)
            {
                var shape = ((PlaceSource)e.Source).Contents?.Room;
                if (shape != null) _byHash[shape.Name.GetStableHashCode()] = shape;
            }
            _byHashOf = read;
            return _byHash;
        }

        private static Dictionary<int, RoomShape> _byHash;
        private static int _byHashOf = -1;

        /// <summary>The world's own dungeon as the fog shows it, the stage building it afresh: its rooms walked, the rest only while peeking; every one with the compendium off.</summary>
        private static void Refog()
        {
            _fogOf = (DungeonWalks.Maps.Stamp, _fogged, _peeking);
            var walked = DungeonWalks.Maps.Of(_wholeId)?.Walked;
            bool IsWalked(PlacedRoom room) => walked != null && walked.Contains(room.SavedAt);
            Example = DungeonFog.Shown(_whole, IsWalked, _fogged, _peeking, out var peeked);
            PeekedAt.Clear();
            foreach (var room in peeked) PeekedAt.Add(room.SavedAt);
            PeekedRooms = peeked.Count(r => !r.Room.EndCap && !r.Room.Divider);
            Fogged = _fogged;
            OpenWays = _fogged ? DungeonFog.OpenWays(DungeonWays.Of(_whole.Rooms), IsWalked) : new List<OpenWay>();
            Walked = DungeonFog.Walked(_whole, IsWalked);
        }

        private static void LetGo()
        {
            if (_loading == null) return;
            _loading.Reference.Release();
            _loading = null;
        }
    }
}
