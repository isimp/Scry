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
    /// those not read yet are loaded one at a time in the background, each read
    /// (<see cref="PlaceReader"/>, which fills their own entries too) and let go of; once all are
    /// in, the example is laid out, and laid out anew on asking for another.
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

        /// <summary>Whether a room's bundle is held while it loads, for the self-test to see nothing is left held.</summary>
        public static bool Holding => _loading != null;

        /// <summary>Whether the example shown is of this entry.</summary>
        public static bool Of(Entry entry) => entry != null && entry == _entry;

        /// <summary>Lets go of any room held, and forgets the example.</summary>
        public static void Forget()
        {
            LetGo();
            _entry = null;
            Rooms.Clear();
            _next = 0;
            Failed = 0;
            Example = null;
        }

        /// <summary>Lets go of the room being loaded, which is loaded again when the panel opens; the example stays.</summary>
        public static void Pause() => LetGo();

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
            if (Example != null) return;

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
                    Plugin.Log.LogWarning($"Scry could not load the dungeon room {_loading.Prefab} for an example layout.");
                }
                else
                {
                    try
                    {
                        _loading.Contents = PlaceReader.Read(asset, true);
                        Facts.Forget(Rooms[_next]);
                    }
                    catch (Exception ex)
                    {
                        Failed++;
                        Faults.Skip("dungeon rooms", _loading.Prefab, ex);
                    }
                }
                LetGo();
                _next++;
            }
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

            // The kinds of room it is built of are listed by what each is, known now.
            if (wasNone) Facts.Forget(_entry);
        }

        private static void LetGo()
        {
            if (_loading == null) return;
            _loading.Reference.Release();
            _loading = null;
        }
    }
}
