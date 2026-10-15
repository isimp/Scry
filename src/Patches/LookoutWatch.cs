using HarmonyLib;

namespace Scry
{
    /// <summary>
    /// Keeps the pickables and nests loaded for the compendium's sight (<see cref="CompendiumSight"/>):
    /// the game keeps no list of either, so each is noted as it wakes.
    /// </summary>
    [HarmonyPatch(typeof(Pickable), "Awake")]
    internal static class PickableWoke
    {
        private static void Postfix(Pickable __instance) => CompendiumSight.Woke(__instance);
    }

    [HarmonyPatch(typeof(SpawnArea), "Awake")]
    internal static class NestWoke
    {
        private static void Postfix(SpawnArea __instance) => CompendiumSight.Woke(__instance);
    }
}
