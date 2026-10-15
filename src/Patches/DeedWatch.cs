using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the character sees done that shows a number the game never does, for the strict
    /// rules (<see cref="Deed"/>): a tamed creature giving birth (<c>Procreation.Procreate</c>,
    /// its pregnancy ending), a young one growing up (<c>Growup.GrowUpdate</c>, the young one
    /// gone as the grown one comes), a pickable growing back (<c>Pickable.SetPicked</c> to not
    /// picked). Each counts within sight of the character, and only where the game does it on
    /// this machine, as for the creature's or pickable's owner. The hooks only watch.
    /// </summary>
    internal static class DeedWatch
    {
        /// <summary>Whether a thing is within sight of the character, as far as one of a person's size is seen.</summary>
        private static bool InSight(Component thing)
        {
            var player = Player.m_localPlayer;
            return thing != null && player != null && Vector3.Distance(player.transform.position, thing.transform.position) <= Sighting.PersonReach;
        }

        /// <summary>Whether a creature is pregnant now, as the game keeps it on its object.</summary>
        public static bool Pregnant(Procreation procreation) =>
            Guard.Run(Feature.CompendiumTogether, "noting a birth seen", () =>
            {
                var zdo = procreation != null ? procreation.GetComponent<ZNetView>().OrNull()?.GetZDO() : null;
                return zdo != null && zdo.GetLong(ZDOVars.s_pregnant) != 0L;
            }, out var pregnant) && pregnant;

        /// <summary>A pregnancy that ended in this call: a birth.</summary>
        public static void Procreated(Procreation procreation, bool wasPregnant)
        {
            if (!wasPregnant || Pregnant(procreation) || !InSight(procreation)) return;
            Guard.Run(Feature.CompendiumTogether, "noting a birth seen", () => CompendiumKeeper.SawDone(procreation.gameObject, Deed.Born));
        }

        /// <summary>Whether a young creature is still in the world, before it may grow up.</summary>
        public static bool Here(Growup young) =>
            Guard.Run(Feature.CompendiumTogether, "noting one grown up", () => young != null && young.GetComponent<ZNetView>().OrNull()?.IsValid() == true, out var here) && here;

        /// <summary>A young creature gone in this call: grown up.</summary>
        public static void Grew(Growup young, bool wasHere)
        {
            if (!wasHere || Here(young) || !InSight(young)) return;
            Guard.Run(Feature.CompendiumTogether, "noting one grown up", () => CompendiumKeeper.SawDone(young.gameObject, Deed.GrewUp));
        }

        /// <summary>A spawner that spawned in this call, within sight.</summary>
        public static void Spawned(SpawnArea area, bool spawned)
        {
            if (!spawned || !InSight(area)) return;
            Guard.Run(Feature.CompendiumTogether, "noting a spawn seen", () => CompendiumKeeper.SawDone(area.gameObject, Deed.Spawned));
        }

        /// <summary>Whether a producer holds anything to collect, as the game keeps it on its object.</summary>
        public static bool Holding(Component producer) =>
            Guard.Run(Feature.CompendiumTogether, "noting a producer collected from", () =>
            {
                var zdo = producer != null ? producer.GetComponent<ZNetView>().OrNull()?.GetZDO() : null;
                return zdo != null && zdo.GetInt(ZDOVars.s_level) > 0;
            }, out var holding) && holding;

        /// <summary>A producer that held something used by the character: collected from.</summary>
        public static void Collected(Component producer, Humanoid user, bool held)
        {
            if (!held || user == null || user != Player.m_localPlayer) return;
            Guard.Run(Feature.CompendiumTogether, "noting a producer collected from", () => CompendiumKeeper.SawDone(producer.gameObject, Deed.Collected));
        }

        /// <summary>A picked pickable set back to not picked: grown back.</summary>
        public static void SetPicked(Pickable pickable, bool wasPicked, bool picked)
        {
            if (!wasPicked || picked || !InSight(pickable)) return;
            Guard.Run(Feature.CompendiumTogether, "noting one grown back", () => CompendiumKeeper.SawDone(pickable.gameObject, Deed.GrewBack));
        }
    }

    /// <summary>A spawner that spawned within sight shows its pool, star chance and pace (<c>SpawnArea.SpawnOne</c>).</summary>
    [HarmonyPatch(typeof(SpawnArea), "SpawnOne")]
    internal static class SpawnSeen
    {
        private static void Postfix(SpawnArea __instance, bool __result) => DeedWatch.Spawned(__instance, __result);
    }

    /// <summary>A producer collected from by the character shows its pace: one holding anything, used not held (<c>Beehive.Interact</c>).</summary>
    [HarmonyPatch(typeof(Beehive), nameof(Beehive.Interact))]
    internal static class HiveCollected
    {
        private static void Prefix(Beehive __instance, bool repeat, out bool __state) => __state = !repeat && DeedWatch.Holding(__instance);

        private static void Postfix(Beehive __instance, Humanoid character, bool __state) => DeedWatch.Collected(__instance, character, __state);
    }

    /// <summary>The same for a sap collector (<c>SapCollector.Interact</c>).</summary>
    [HarmonyPatch(typeof(SapCollector), nameof(SapCollector.Interact))]
    internal static class TapCollected
    {
        private static void Prefix(SapCollector __instance, bool repeat, out bool __state) => __state = !repeat && DeedWatch.Holding(__instance);

        private static void Postfix(SapCollector __instance, Humanoid character, bool __state) => DeedWatch.Collected(__instance, character, __state);
    }

    /// <summary>A chest the character opens is counted the first time (<c>InventoryGui.Show</c>).</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    internal static class ChestOpened
    {
        private static void Postfix(Container container) => Guard.Run(Feature.CompendiumTogether, "counting what a chest held", () => CompendiumKeeper.Opened(container));
    }

    [HarmonyPatch(typeof(Procreation), "Procreate")]
    internal static class BirthSeen
    {
        private static void Prefix(Procreation __instance, out bool __state) => __state = DeedWatch.Pregnant(__instance);

        private static void Postfix(Procreation __instance, bool __state) => DeedWatch.Procreated(__instance, __state);
    }

    [HarmonyPatch(typeof(Growup), "GrowUpdate")]
    internal static class GrowingUpSeen
    {
        private static void Prefix(Growup __instance, out bool __state) => __state = DeedWatch.Here(__instance);

        private static void Postfix(Growup __instance, bool __state) => DeedWatch.Grew(__instance, __state);
    }

    [HarmonyPatch(typeof(Pickable), nameof(Pickable.SetPicked))]
    internal static class GrowingBackSeen
    {
        private static void Prefix(Pickable __instance, out bool __state) => __state = __instance != null && __instance.GetPicked();

        private static void Postfix(Pickable __instance, bool picked, bool __state) => DeedWatch.SetPicked(__instance, __state, picked);
    }
}
