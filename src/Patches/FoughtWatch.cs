using HarmonyLib;

namespace Scry
{
    /// <summary>
    /// A creature the character hits becomes known to the compendium: <c>Character.Damage</c> is
    /// called on the client that deals the hit, for whatever deals it, before the hit is sent to
    /// the creature's owner.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class HitWatched
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            if (hit == null || Player.m_localPlayer == null || __instance == Player.m_localPlayer) return;
            Guard.Run(Feature.CompendiumFought, "noting a creature fought", () =>
            {
                if (hit.GetAttacker() != Player.m_localPlayer) return;
                CompendiumKeeper.Fought(__instance);
                CompendiumKeeper.Struck(__instance);
            });
        }
    }

    /// <summary>
    /// A creature that hits the character becomes known to the compendium: <c>Character.RPC_Damage</c>
    /// runs for the character on its own client, whoever dealt the hit.
    /// </summary>
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class HitByWatched
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            if (hit == null || Player.m_localPlayer == null || __instance != Player.m_localPlayer) return;
            Guard.Run(Feature.CompendiumFought, "noting a creature fought", () =>
            {
                CompendiumKeeper.Fought(hit.GetAttacker());
                CompendiumKeeper.HitBy(hit.GetAttacker());
            });
        }
    }
}
