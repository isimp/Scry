using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the character sees done that shows a number the game never does, for the strict
    /// rules (<see cref="Deed"/>): a tamed creature giving birth (<c>Procreation.Procreate</c>,
    /// its pregnancy ending), a young one growing up (<c>Growup.GrowUpdate</c>, the young one
    /// gone as the grown one comes), a pickable growing back (<c>Pickable.SetPicked</c> to not
    /// picked), a crop growing (<c>Plant.Grow</c>), a station handing out what it made
    /// (<c>Smelter.Spawn</c>, <c>Fermenter.DelayedTap</c>, <c>CookingStation.SpawnItem</c>). Each is
    /// told as an event (<see cref="WorldEvents"/>), counted where the character saw it, and only
    /// where the game does it on this machine, as for the creature's or pickable's owner. A
    /// producer the character collected from is the character's own doing. The hooks only watch.
    /// </summary>
    internal static class DeedWatch
    {
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
            if (!wasPregnant || Pregnant(procreation)) return;
            Guard.Run(Feature.CompendiumTogether, "noting a birth seen", () => WorldEvents.Tell(Happened.Born, procreation));
        }

        /// <summary>Whether a young creature is still in the world, before it may grow up.</summary>
        public static bool Here(Growup young) =>
            Guard.Run(Feature.CompendiumTogether, "noting one grown up", () => young != null && young.GetComponent<ZNetView>().OrNull()?.IsValid() == true, out var here) && here;

        /// <summary>A young creature gone in this call: grown up.</summary>
        public static void Grew(Growup young, bool wasHere)
        {
            if (!wasHere || Here(young)) return;
            Guard.Run(Feature.CompendiumTogether, "noting one grown up", () => WorldEvents.Tell(Happened.GrewUp, young));
        }

        /// <summary>A spawner that spawned in this call.</summary>
        public static void Spawned(SpawnArea area, bool spawned)
        {
            if (!spawned) return;
            Guard.Run(Feature.CompendiumTogether, "noting a spawn seen", () => WorldEvents.Tell(Happened.Spawned, area));
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
            Guard.Run(Feature.CompendiumTogether, "noting a producer collected from", () => CompendiumKeeper.CollectedFrom(producer.gameObject));
        }

        /// <summary>A crop that grew in this call: grown.</summary>
        public static void Grown(Plant plant, bool grew)
        {
            if (!grew) return;
            Guard.Run(Feature.CompendiumTogether, "noting a crop grown", () => WorldEvents.Tell(Happened.Grew, plant));
        }

        /// <summary>A station that handed out what it made in this call: its work finished.</summary>
        public static void Finished(Component station)
        {
            if (station == null) return;
            Guard.Run(Feature.CompendiumTogether, "noting a station's work finished", () => WorldEvents.Tell(Happened.Finished, station));
        }

        /// <summary>A picked pickable set back to not picked: grown back.</summary>
        public static void SetPicked(Pickable pickable, bool wasPicked, bool picked)
        {
            if (!wasPicked || picked) return;
            Guard.Run(Feature.CompendiumTogether, "noting one grown back", () => WorldEvents.Tell(Happened.GrewBack, pickable));
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

    /// <summary>A crop grown within sight shows how long it takes (<c>Plant.Grow</c>, the grown prefab made in its place).</summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.Grow))]
    internal static class CropGrown
    {
        private static void Postfix(Plant __instance, GameObject __result) => DeedWatch.Grown(__instance, __result != null);
    }

    /// <summary>A smelter, kiln, windmill or the like handing out what it made within sight shows how long each takes and what it burns (<c>Smelter.Spawn</c>).</summary>
    [HarmonyPatch(typeof(Smelter), "Spawn")]
    internal static class SmeltingFinished
    {
        private static void Postfix(Smelter __instance) => DeedWatch.Finished(__instance);
    }

    /// <summary>A fermenter tapped within sight shows how long it ferments (<c>Fermenter.DelayedTap</c>).</summary>
    [HarmonyPatch(typeof(Fermenter), "DelayedTap")]
    internal static class FermentingFinished
    {
        private static void Postfix(Fermenter __instance) => DeedWatch.Finished(__instance);
    }

    /// <summary>A cooking station handing out what it cooked within sight shows how long each takes and what it burns (<c>CookingStation.SpawnItem</c>).</summary>
    [HarmonyPatch(typeof(CookingStation), "SpawnItem")]
    internal static class CookingFinished
    {
        private static void Postfix(CookingStation __instance) => DeedWatch.Finished(__instance);
    }
}
