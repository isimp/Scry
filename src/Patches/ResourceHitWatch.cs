using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A resource the character hits (a tree, a log, a rock, a vein, anything that breaks) becomes
    /// known to the compendium: each kind's <c>Damage</c> is called on the client that deals the
    /// hit, before the hit is sent to the resource's owner.
    /// </summary>
    internal static class ResourceHitWatch
    {
        public static void Hit(Component resource, HitData hit)
        {
            if (hit == null || Player.m_localPlayer == null) return;
            Guard.Run(Feature.CompendiumHit, "noting a resource hit", () =>
            {
                if (hit.GetAttacker() == Player.m_localPlayer) CompendiumKeeper.Hit(resource);
            });
        }
    }

    [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
    internal static class DestructibleHitWatched
    {
        private static void Prefix(Destructible __instance, HitData hit) => ResourceHitWatch.Hit(__instance, hit);
    }

    [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Damage))]
    internal static class TreeHitWatched
    {
        private static void Prefix(TreeBase __instance, HitData hit) => ResourceHitWatch.Hit(__instance, hit);
    }

    [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Damage))]
    internal static class LogHitWatched
    {
        private static void Prefix(TreeLog __instance, HitData hit) => ResourceHitWatch.Hit(__instance, hit);
    }

    [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
    internal static class RockHitWatched
    {
        private static void Prefix(MineRock __instance, HitData hit) => ResourceHitWatch.Hit(__instance, hit);
    }

    [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
    internal static class VeinHitWatched
    {
        private static void Prefix(MineRock5 __instance, HitData hit) => ResourceHitWatch.Hit(__instance, hit);
    }
}
