using HarmonyLib;

namespace Scry
{
    /// <summary>
    /// A creature that kills the character is counted in the compendium: <c>Player.OnDeath</c>
    /// runs for the character on its own client, its last hit (<c>Character.m_lastHit</c>) naming
    /// what dealt it.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    internal static class DeathWatched
    {
        private static AccessTools.FieldRef<Character, HitData> _lastHit;

        private static void Prefix(Player __instance)
        {
            if (__instance == null || __instance != Player.m_localPlayer) return;
            Guard.Run(Feature.CompendiumDeaths, "noting what killed the character", () =>
            {
                if (_lastHit == null) _lastHit = AccessTools.FieldRefAccess<Character, HitData>("m_lastHit");
                CompendiumKeeper.KilledBy(_lastHit(__instance)?.GetAttacker());
            });
        }
    }
}
