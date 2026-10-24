using HarmonyLib;

namespace Scry
{
    /// <summary>
    /// A creature's attack begun near the character, for the compendium: the animation trigger
    /// the game sends every player's game (<c>ZSyncAnimation.RPC_SetTrigger</c>), so a creature a
    /// friend's game runs counts as one this game runs (<see cref="CompendiumSight.SwingSeen"/>).
    /// </summary>
    [HarmonyPatch(typeof(ZSyncAnimation), "RPC_SetTrigger")]
    internal static class SwingWatched
    {
        private static void Postfix(ZSyncAnimation __instance, string name)
        {
            if (__instance == null || Player.m_localPlayer == null) return;
            Guard.Run(Feature.CompendiumSeen, "noting an attack seen begun", Note, (__instance, name));
        }

        private static void Note((ZSyncAnimation Animation, string Trigger) swing) =>
            CompendiumSight.SwingSeen(swing.Animation.GetComponent<Character>(), swing.Trigger);
    }
}
