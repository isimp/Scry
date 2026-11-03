using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A resource the character hits (a tree, a log, a rock, a vein, anything that breaks) becomes
    /// known to the compendium, with how it took the hit as the game's popup tells it, by its own
    /// modifiers, where the tool was strong enough for the popup to tell more than that it is too
    /// hard (<c>HitData.CheckToolTier</c>): each kind's <c>Damage</c> is called on the client that
    /// deals the hit, before the hit is sent to the resource's owner.
    /// </summary>
    internal static class ResourceHitWatch
    {
        public static void Hit(Component resource, HitData hit, HitData.DamageModifiers mods, int minToolTier)
        {
            if (hit == null || Player.m_localPlayer == null) return;
            Guard.Run(Feature.CompendiumHit, "noting a resource hit", () =>
            {
                if (hit.GetAttacker() != Player.m_localPlayer) return;
                // The tool in hand broke it only where the hit was its own swing, by its skill, and strong enough.
                var weapon = Player.m_localPlayer.GetCurrentWeapon();
                CompendiumKeeper.Hit(resource, weapon != null && hit.m_skill == weapon.m_shared.m_skillType && hit.CheckToolTier(minToolTier));
                // How a type lands is told by the popup, which shows no farther on a tree than on a creature.
                if (hit.CheckToolTier(minToolTier) && TogetherWatch.PopupSeen(resource)) TogetherWatch.Told(Utils.GetPrefabName(resource.gameObject), hit, mods);
            });
        }
    }

    /// <summary>
    /// A building piece the character hits tells how it took the hit, as the popup shows it, by its
    /// own modifiers, where the tool was strong enough (<c>HitData.CheckToolTier</c>, a piece always
    /// letting the lowest tier through, as the game checks a hit on one); <c>WearNTear.Damage</c>
    /// is called on the client dealing the hit, before it is sent to the piece's owner. One the
    /// world placed (<see cref="WorldPiece"/>) becomes known by the hit, as a resource does, with
    /// what broke it and where it stands.
    /// </summary>
    internal static class PieceHitWatch
    {
        public static void Hit(WearNTear piece, HitData hit)
        {
            if (piece == null || hit == null || Player.m_localPlayer == null) return;
            Guard.Run(Feature.CompendiumHit, "noting a piece hit", () =>
            {
                if (hit.GetAttacker() != Player.m_localPlayer) return;
                var strongEnough = hit.CheckToolTier(piece.m_minToolTier, alwaysAllowTierZero: true);
                var built = piece.GetComponent<Piece>();
                if (WorldPiece.LearnedByHit(built != null ? built.GetCreator() : 0L))
                {
                    // The tool in hand broke it only where the hit was its own swing, by its skill, and strong enough.
                    var weapon = Player.m_localPlayer.GetCurrentWeapon();
                    CompendiumKeeper.Hit(piece, weapon != null && hit.m_skill == weapon.m_shared.m_skillType && strongEnough);
                }
                if (strongEnough && TogetherWatch.PopupSeen(piece)) TogetherWatch.Told(Utils.GetPrefabName(piece.gameObject), hit, piece.m_damages);
            });
        }
    }

    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
    internal static class PieceHitWatched
    {
        private static void Prefix(WearNTear __instance, HitData hit) => PieceHitWatch.Hit(__instance, hit);
    }

    [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
    internal static class DestructibleHitWatched
    {
        private static void Prefix(Destructible __instance, HitData hit) => ResourceHitWatch.Hit(__instance, hit, __instance.m_damages, __instance.m_minToolTier);
    }

    [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Damage))]
    internal static class TreeHitWatched
    {
        private static void Prefix(TreeBase __instance, HitData hit) => ResourceHitWatch.Hit(__instance, hit, __instance.m_damageModifiers, __instance.m_minToolTier);
    }

    [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Damage))]
    internal static class LogHitWatched
    {
        private static void Prefix(TreeLog __instance, HitData hit) => ResourceHitWatch.Hit(__instance, hit, __instance.m_damages, __instance.m_minToolTier);
    }

    [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
    internal static class RockHitWatched
    {
        private static void Prefix(MineRock __instance, HitData hit) => ResourceHitWatch.Hit(__instance, hit, __instance.m_damageModifiers, __instance.m_minToolTier);
    }

    [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
    internal static class VeinHitWatched
    {
        private static void Prefix(MineRock5 __instance, HitData hit) => ResourceHitWatch.Hit(__instance, hit, __instance.m_damageModifiers, __instance.m_minToolTier);
    }
}
