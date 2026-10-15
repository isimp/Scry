using HarmonyLib;

namespace Scry
{
    /// <summary>
    /// Keeps everything the game keeps in the world for the compendium's sight
    /// (<see cref="CompendiumSight"/>), each noted as it wakes: trees, rocks, buildings,
    /// pickables, nests, items on the ground and the rest alike.
    /// </summary>
    [HarmonyPatch(typeof(ZNetView), "Awake")]
    internal static class ThingWoke
    {
        private static void Postfix(ZNetView __instance) => CompendiumSight.Woke(__instance);
    }
}
