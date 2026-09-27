using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SoftReferenceableAssets;
using UnityEngine;
using UnityEngine.Profiling;

namespace Scry
{
    /// <summary>
    /// A measurement, started by hand with <c>/scry locations</c>: what reading the world's
    /// locations and dungeon rooms would cost, and what it would tell. The game keeps them only as
    /// soft references to asset bundles (<c>ZoneSystem.m_locations</c>, <c>DungeonDB</c>'s rooms),
    /// loaded while it builds a zone. Each is loaded, read and released again within one frame, one
    /// a frame: a release unloads the bundle with everything in it once nothing else holds it, so
    /// only names and numbers are kept. The log gets the timings, the memory a load takes, and which
    /// effects, sounds and projectiles the catalog finds nothing for are named in them.
    /// </summary>
    internal static class LocationSurvey
    {
        static LocationSurvey() => WorldCaches.Register(nameof(LocationSurvey), Forget);

        private struct Asset
        {
            public string Name;
            public string What;
            public SoftReference<GameObject> Reference;
        }

        private struct Measured
        {
            public string Name;
            public double LoadMs;
            public double ReadMs;
            public long Bytes;
            public int Parts;
            public bool WasLoaded;
            public bool Failed;
        }

        private static readonly List<Asset> Queue = new List<Asset>();
        private static readonly List<Measured> Results = new List<Measured>();
        private static readonly Dictionary<string, HashSet<string>> NamedIn = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private static int _next;
        private static int _locations;
        private static int _rooms;
        private static int _skipped;
        private static Stopwatch _clock;

        public static bool Running => _clock != null;

        /// <summary>Stops a measurement of a world that was left; nothing is held between frames.</summary>
        public static void Forget()
        {
            Queue.Clear();
            Results.Clear();
            NamedIn.Clear();
            _clock = null;
        }

        /// <summary>Starts measuring, and says how many there are to go through.</summary>
        public static string Start()
        {
            if (Running) return "already measuring, " + (Queue.Count - _next) + " to go.";
            var zones = ZoneSystem.instance;
            if (zones == null) return "no world is loaded.";

            Forget();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            _locations = _rooms = _skipped = 0;
            foreach (var location in zones.m_locations)
            {
                if (location == null || !location.m_enable) continue;
                if (!location.m_prefab.IsValid) { _skipped++; continue; }
                var name = string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefab.Name : location.m_prefabName;
                if (!seen.Add("location|" + name)) continue;
                Queue.Add(new Asset { Name = name, What = "location", Reference = location.m_prefab });
                _locations++;
            }
            var rooms = DungeonDB.instance != null ? DungeonDB.GetRooms() : null;
            if (rooms != null)
            {
                foreach (var room in rooms)
                {
                    if (room == null) continue;
                    if (!room.m_prefab.IsValid) { _skipped++; continue; }
                    var name = room.m_prefab.Name;
                    if (!seen.Add("room|" + name)) continue;
                    Queue.Add(new Asset { Name = name, What = "room", Reference = room.m_prefab });
                    _rooms++;
                }
            }
            _next = 0;
            _clock = Stopwatch.StartNew();
            return $"measuring {_locations} locations and {_rooms} dungeon rooms, one a frame; the result goes to the log.";
        }

        /// <summary>Measures the next one; called every frame.</summary>
        public static void Update()
        {
            if (!Running) return;
            if (ZoneSystem.instance == null) { Forget(); return; }
            if (_next >= Queue.Count)
            {
                Report();
                Forget();
                return;
            }
            Measure(Queue[_next++]);
        }

        private static void Measure(Asset asset)
        {
            var measured = new Measured { Name = asset.What + " " + asset.Name, WasLoaded = asset.Reference.IsLoaded };
            var before = Profiler.GetTotalAllocatedMemoryLong();
            var watch = Stopwatch.StartNew();
            LoadResult result;
            try
            {
                result = asset.Reference.Load();
            }
            catch (Exception ex)
            {
                // A failed load may or may not hold its reference; it is not released, to be safe.
                measured.Failed = true;
                Results.Add(measured);
                Plugin.Log.LogWarning($"Scry could not load the {asset.What} {asset.Name}: {ex.Message}");
                return;
            }
            measured.LoadMs = watch.Elapsed.TotalMilliseconds;
            measured.Bytes = Profiler.GetTotalAllocatedMemoryLong() - before;
            try
            {
                watch.Restart();
                var prefab = result == LoadResult.Succeeded ? asset.Reference.Asset : null;
                if (prefab == null) measured.Failed = true;
                else Read(prefab, measured.Name, ref measured);
                measured.ReadMs = watch.Elapsed.TotalMilliseconds;
            }
            catch (Exception ex)
            {
                measured.Failed = true;
                Plugin.Log.LogWarning($"Scry could not read the {asset.What} {asset.Name}: {ex.Message}");
            }
            finally
            {
                asset.Reference.Release();
            }
            Results.Add(measured);
        }

        /// <summary>The prefabs one names: in its effect lists, in its fields, and placed in it as networked parts.</summary>
        private static void Read(GameObject prefab, string where, ref Measured measured)
        {
            var components = prefab.GetComponentsInChildren<Component>(true);
            measured.Parts = components.Length;
            foreach (var component in components)
            {
                if (component == null) continue;
                if (component is ZNetView && component.transform != prefab.transform) Named(PrefabName(component.gameObject.name), where);
                foreach (var on in CatalogBuilder.ListsOn(component))
                {
                    var members = on.List?.m_effectPrefabs;
                    if (members == null) continue;
                    foreach (var data in members) if (data?.m_prefab != null) Named(data.m_prefab.name, where);
                }
                foreach (var field in component.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                {
                    object value;
                    try { value = field.GetValue(component); }
                    catch { continue; }
                    if (value is GameObject one && one != null) Named(one.name, where);
                    else if (value is GameObject[] many) foreach (var each in many) if (each != null) Named(each.name, where);
                    else if (value is List<GameObject> list) foreach (var item in list) if (item != null) Named(item.name, where);
                }
            }
        }

        private static void Named(string name, string where)
        {
            if (!NamedIn.TryGetValue(name, out var places)) NamedIn[name] = places = new HashSet<string>(StringComparer.Ordinal);
            places.Add(where);
        }

        /// <summary>A placed copy's prefab name: without Unity's " (1)" and "(Clone)".</summary>
        private static string PrefabName(string name)
        {
            var cut = name.IndexOf(" (", StringComparison.Ordinal);
            if (cut < 0) cut = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return cut > 0 ? name.Substring(0, cut) : name;
        }

        private static void Report()
        {
            var done = Results.Where(r => !r.Failed).ToList();
            var fresh = done.Where(r => !r.WasLoaded).ToList();
            var loads = fresh.Select(r => r.LoadMs).OrderBy(v => v).ToList();
            double Median(List<double> values) => values.Count == 0 ? 0 : values[values.Count / 2];
            var slowest = fresh.OrderByDescending(r => r.LoadMs + r.ReadMs).Take(8).Select(r => $"{r.Name} {r.LoadMs + r.ReadMs:0} ms, {r.Bytes / 1048576.0:0.#} MB");
            var biggest = fresh.OrderByDescending(r => r.Bytes).Take(5).Select(r => $"{r.Name} {r.Bytes / 1048576.0:0.#} MB");

            Plugin.Log.LogInfo(
                $"Scry measured {_locations} locations and {_rooms} dungeon rooms in {_clock.Elapsed.TotalSeconds:0.0} s ({_skipped} without a valid reference, {Results.Count - done.Count} failed, {done.Count - fresh.Count} already loaded by the game). " +
                $"Loading the rest took {loads.Sum():0} ms in all (median {Median(loads):0.0} ms, slowest {(loads.Count > 0 ? loads[loads.Count - 1] : 0):0} ms), reading them {done.Sum(r => r.ReadMs):0} ms over {done.Sum(r => r.Parts):N0} parts. " +
                $"Slowest: {string.Join("; ", slowest)}. Most memory while loaded: {string.Join("; ", biggest)}.");

            // What the catalog finds nothing for, that locations and rooms name.
            var catalog = Session.Explorer?.Catalog;
            if (catalog == null)
            {
                Plugin.Log.LogInfo($"Scry's locations and rooms name {NamedIn.Count} prefabs; the catalog was not read yet to compare.");
                return;
            }
            var unplayed = Groups.Purpose(new string[0], false, false).Order;
            var unfired = Groups.Projectile(new Shooter[0]).Order;
            var found = new List<string>();
            var registered = 0;
            foreach (var entry in catalog)
            {
                if (!NamedIn.TryGetValue(entry.Name, out var places)) continue;
                registered++;
                var lost = ((entry.Kind == Kind.Effect || entry.Kind == Kind.Sound) && entry.GroupOrder == unplayed)
                           || (entry.Kind == Kind.Projectile && entry.GroupOrder == unfired);
                if (lost) found.Add($"{entry.Name} (in {string.Join(", ", places.OrderBy(p => p).Take(3))}{(places.Count > 3 ? $" and {places.Count - 3} more" : "")})");
            }
            Plugin.Log.LogInfo(
                $"Scry's locations and rooms name {NamedIn.Count} prefabs, {registered} of them in the catalog. " +
                $"Of what it finds nothing to play or fire, they name {found.Count}: {string.Join(", ", found.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))}.");
        }
    }
}
