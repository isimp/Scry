using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the character does that ties two things together, for the compendium's strict rules:
    /// an item picked up with the biome and place it lay in, a piece placed with the tool that
    /// built it, a creature tamed or petted, which makes it known, and one seen eat with what it
    /// ate. Picking up and placing run on the client of the character doing it; taming and eating
    /// on the client that owns the creature, so they count only near the character.
    /// </summary>
    internal static class TogetherWatch
    {
        /// <summary>How near the character a creature tames or eats for the character to see it, as far as a creature in view is seen.</summary>
        private const float Near = 30f;

        public static (string Key, Vector3 At) PickingUp(Humanoid who, GameObject go) =>
            who != null && who == Player.m_localPlayer && go != null ? (Utils.GetPrefabName(go), go.transform.position) : (null, Vector3.zero);

        public static void PickedUp(bool done, (string Key, Vector3 At) item)
        {
            if (!done || item.Key == null) return;
            Guard.Run(Feature.CompendiumTogether, "noting where an item was picked up", () => CompendiumKeeper.MetIn(item.Key, item.At));
        }

        public static void Placed(Player who, Piece piece)
        {
            if (who == null || who != Player.m_localPlayer || piece == null) return;
            Guard.Run(Feature.CompendiumTogether, "noting what built a piece", () =>
                CompendiumKeeper.Placed(Utils.GetPrefabName(piece.gameObject), who.GetCurrentWeapon()?.m_dropPrefab.OrNull()?.name));
        }

        public static void Tamed(Tameable tameable)
        {
            var creature = tameable != null ? tameable.GetComponent<Character>() : null;
            if (!NearBy(creature)) return;
            Guard.Run(Feature.CompendiumTogether, "noting a creature tamed", () => CompendiumKeeper.Tamed(creature));
        }

        public static void Petted(Tameable tameable, Humanoid user)
        {
            var creature = tameable != null ? tameable.GetComponent<Character>() : null;
            if (user == null || user != Player.m_localPlayer || creature == null || !creature.IsTamed()) return;
            Guard.Run(Feature.CompendiumTogether, "noting a creature petted", () => CompendiumKeeper.Tamed(creature));
        }

        public static void Ate(Tameable tameable, ItemDrop food)
        {
            var creature = tameable != null ? tameable.GetComponent<Character>() : null;
            if (food == null || !NearBy(creature)) return;
            Guard.Run(Feature.CompendiumTogether, "noting what a creature ate", () => CompendiumKeeper.Fed(creature, Utils.GetPrefabName(food.gameObject)));
        }

        private static bool NearBy(Character creature)
        {
            var player = Player.m_localPlayer;
            return creature != null && player != null && Vector3.Distance(player.transform.position, creature.transform.position) <= Near;
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup))]
    internal static class PickupWatched
    {
        private static void Prefix(Humanoid __instance, GameObject go, out (string Key, Vector3 At) __state) => __state = TogetherWatch.PickingUp(__instance, go);

        private static void Postfix(bool __result, (string Key, Vector3 At) __state) => TogetherWatch.PickedUp(__result, __state);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
    internal static class PlacingWatched
    {
        private static void Postfix(Player __instance, Piece piece) => TogetherWatch.Placed(__instance, piece);
    }

    [HarmonyPatch(typeof(Tameable), "Tame")]
    internal static class TamingWatched
    {
        private static void Postfix(Tameable __instance) => TogetherWatch.Tamed(__instance);
    }

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
    internal static class PettingWatched
    {
        private static void Postfix(Tameable __instance, Humanoid user) => TogetherWatch.Petted(__instance, user);
    }

    [HarmonyPatch(typeof(Tameable), "OnConsumedItem")]
    internal static class EatingWatched
    {
        private static void Prefix(Tameable __instance, ItemDrop item) => TogetherWatch.Ate(__instance, item);
    }
}
