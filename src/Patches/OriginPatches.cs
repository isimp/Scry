using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Scry
{
    /// <summary>The records of which prefabs and status effects are the game's own.</summary>
    internal static class Origins
    {
        public static readonly Provenance Prefabs = new Provenance();
        public static readonly Provenance StatusEffects = new Provenance();
        public static readonly Provenance Raids = new Provenance();
        public static readonly Provenance Locations = new Provenance();
        public static readonly Provenance Rooms = new Provenance();
    }

    /// <summary>
    /// The scene's prefab lists as they come out of the game's files, read before anything else
    /// gets to run in <c>ZNetScene.Awake</c>. Mods add theirs to the same lists, in this method or
    /// later.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class SceneOrigins
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(ZNetScene __instance)
        {
            // Runs inside the game's own Awake: whatever goes wrong here must not stop it. The work
            // is in a method of its own, so that a list an update renamed, which fails the method
            // naming it before it runs, is caught here too.
            try
            {
                Record(__instance);
            }
            catch (System.Exception ex)
            {
                Faults.Tell("telling the game's prefabs from those mods add", ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Record(ZNetScene scene)
        {
            var names = new List<string>();
            foreach (var prefab in scene.m_prefabs) if (prefab != null) names.Add(prefab.name);
            foreach (var prefab in scene.m_nonNetViewPrefabs) if (prefab != null) names.Add(prefab.name);
            Origins.Prefabs.RecordOriginal(names);
        }
    }

    /// <summary>The same for status effects, which live in <c>ObjectDB</c>.</summary>
    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    internal static class StatusEffectOrigins
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(ObjectDB __instance)
        {
            // Runs inside the game's own Awake: as above, nothing here may stop it.
            try
            {
                Record(__instance);
            }
            catch (System.Exception ex)
            {
                Faults.Tell("telling the game's status effects from those mods add", ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Record(ObjectDB db)
        {
            var names = new List<string>();
            foreach (var effect in db.m_StatusEffects) if (effect != null) names.Add(effect.name);
            Origins.StatusEffects.RecordOriginal(names);
        }
    }

    /// <summary>
    /// The same for raids, which live in <c>RandEventSystem</c>. The game adds more a step later
    /// from its location lists (<see cref="LocationOrigins"/>).
    /// </summary>
    [HarmonyPatch(typeof(RandEventSystem), "Awake")]
    internal static class RaidOrigins
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(RandEventSystem __instance)
        {
            // Runs inside the game's own Awake: as above, nothing here may stop it.
            try
            {
                Record(__instance);
            }
            catch (System.Exception ex)
            {
                Faults.Tell("telling the game's raids from those mods add", ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Record(RandEventSystem events)
        {
            var names = new List<string>();
            foreach (var raid in events.m_events) if (raid != null) names.Add(raid.m_name);
            Origins.Raids.RecordOriginal(names);
        }
    }

    /// <summary>
    /// The locations as the game's own location lists give them, read after
    /// <c>ZoneSystem.SetupLocations</c> gathers them and before any mod's later step adds its own.
    /// The same step adds the raids those lists hold, which are the game's too.
    /// </summary>
    [HarmonyPatch(typeof(ZoneSystem), "SetupLocations")]
    internal static class LocationOrigins
    {
        private static HashSet<string> _raidsBefore;

        [HarmonyPriority(Priority.First)]
        private static void Prefix()
        {
            // Runs inside the game's own setup: as above, nothing here may stop it.
            try
            {
                _raidsBefore = RaidNames();
            }
            catch (System.Exception ex)
            {
                Faults.Tell("telling the game's raids from those mods add", ex);
            }
        }

        [HarmonyPriority(Priority.First)]
        private static void Postfix(ZoneSystem __instance)
        {
            try
            {
                Record(__instance);
            }
            catch (System.Exception ex)
            {
                Faults.Tell("telling the game's locations from those mods add", ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static HashSet<string> RaidNames()
        {
            var names = new HashSet<string>();
            var events = RandEventSystem.instance?.m_events;
            if (events != null) foreach (var raid in events) if (raid != null) names.Add(raid.m_name);
            return names;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Record(ZoneSystem zones)
        {
            var locations = new List<string>();
            foreach (var location in zones.m_locations) if (location != null && location.m_prefab.IsValid) locations.Add(location.m_prefab.Name);
            Origins.Locations.RecordOriginal(locations);

            var before = _raidsBefore ?? new HashSet<string>();
            var added = new List<string>();
            foreach (var name in RaidNames()) if (!before.Contains(name)) added.Add(name);
            Origins.Raids.AddOriginal(added);
            _raidsBefore = null;
        }
    }

    /// <summary>The same for dungeon rooms, gathered by <c>DungeonDB.SetupRooms</c> from the game's room lists.</summary>
    [HarmonyPatch(typeof(DungeonDB), "SetupRooms")]
    internal static class RoomOrigins
    {
        [HarmonyPriority(Priority.First)]
        private static void Postfix()
        {
            try
            {
                Record();
            }
            catch (System.Exception ex)
            {
                Faults.Tell("telling the game's dungeon rooms from those mods add", ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Record()
        {
            var names = new List<string>();
            foreach (var room in DungeonDB.GetRooms()) if (room != null && room.m_prefab.IsValid) names.Add(room.m_prefab.Name);
            Origins.Rooms.RecordOriginal(names);
        }
    }
}
