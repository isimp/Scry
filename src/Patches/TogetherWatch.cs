using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the character does that ties two things together, for the compendium's strict rules:
    /// an item picked up with the biome and place it lay in, a piece placed with the tool that
    /// built it, a creature tamed or petted, which makes it known, and one seen eat with what it
    /// ate; what the character put into a thing it used (ore, fuel, food, an offering) with it, and
    /// an item coming to be near a thing it used or hit lately with that thing, where it gives it
    /// (<see cref="ActedOn"/>); a boss called at its altar with the altar, its offering and its
    /// place; a fish caught with its bait and where it swam. Picking up and placing run on the
    /// client of the character doing it; taming and eating on the client that owns the creature,
    /// so they count only near the character.
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
            Guard.Run(Feature.CompendiumTogether, "noting where an item was picked up", () =>
            {
                CompendiumKeeper.MetIn(item.Key, item.At);
                CompendiumKeeper.Arrived(item.Key, item.At);
            });
        }

        /// <summary>An item came to be: from a thing the character acted on lately near it, if that gives it.</summary>
        public static void Arrived(ItemDrop item)
        {
            if (item == null || Player.m_localPlayer == null) return;
            Guard.Run(Feature.CompendiumTogether, "noting where an item came from", () => CompendiumKeeper.Arrived(Utils.GetPrefabName(item.gameObject), item.transform.position));
        }

        /// <summary>What the character carries now, by prefab and count, before using something; none for anyone else.</summary>
        public static Dictionary<string, int> Carried(Humanoid who)
        {
            if (who == null || who != Player.m_localPlayer) return null;
            var counts = new Dictionary<string, int>();
            foreach (var held in who.GetInventory().GetAllItems())
            {
                var name = held?.m_dropPrefab.OrNull()?.name;
                if (name == null) continue;
                counts.TryGetValue(name, out var count);
                counts[name] = count + held.m_stack;
            }
            return counts;
        }

        /// <summary>After using a thing: what left the inventory was put into it.</summary>
        public static void UsedWith(Humanoid who, GameObject target, Dictionary<string, int> before)
        {
            if (before == null || target == null) return;
            var thing = target.GetComponentInParent<ZNetView>();
            if (thing == null || thing.gameObject == who.gameObject) return;
            Guard.Run(Feature.CompendiumTogether, "noting what was put into a thing used", () =>
            {
                var taken = ActedOn.Taken(before, Carried(who));
                if (taken.Count > 0) CompendiumKeeper.PutInto(Utils.GetPrefabName(thing.gameObject), taken);
            });
        }

        /// <summary>A boss called at its altar: with the altar, the offering and the place it stands in.</summary>
        public static void Called(OfferingBowl altar)
        {
            var player = Player.m_localPlayer;
            if (altar == null || altar.m_bossPrefab == null || player == null || Vector3.Distance(player.transform.position, altar.transform.position) > Near) return;
            Guard.Run(Feature.CompendiumTogether, "noting a boss called at its altar", () =>
            {
                var boss = altar.m_bossPrefab.name;
                var stone = altar.GetComponentInParent<ZNetView>();
                var at = stone != null ? Utils.GetPrefabName(stone.gameObject) : null;
                var offering = altar.m_bossItem.OrNull()?.gameObject.name;
                if (at != null) CompendiumKeeper.Saw(at, boss);
                if (offering != null) CompendiumKeeper.Saw(offering, boss);
                if (at != null && offering != null) CompendiumKeeper.Saw(at, offering);
                var place = Location.GetLocation(altar.transform.position);
                if (place != null) CompendiumKeeper.Saw(boss, EntryKeys.For(Kind.Location, Utils.GetPrefabName(place.gameObject)));
            });
        }

        /// <summary>The bait on the line a fish is caught with, before the catch takes the float away.</summary>
        public static string Bait(Fish fish) => fish != null ? FishingFloat.FindFloat(fish).OrNull()?.GetBait() : null;

        /// <summary>A fish caught by the character: with its bait, and met where it swam.</summary>
        public static void Caught(Fish fish, Character owner, string bait)
        {
            if (fish == null || owner == null || owner != Player.m_localPlayer) return;
            Guard.Run(Feature.CompendiumTogether, "noting a fish caught", () =>
            {
                var key = Utils.GetPrefabName(fish.gameObject);
                if (!string.IsNullOrEmpty(bait)) CompendiumKeeper.Saw(key, bait);
                CompendiumKeeper.MetIn(key, fish.transform.position);
            });
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

    [HarmonyPatch(typeof(ItemDrop), "Awake")]
    internal static class ItemArrived
    {
        private static void Postfix(ItemDrop __instance) => TogetherWatch.Arrived(__instance);
    }

    [HarmonyPatch(typeof(Player), "Interact")]
    internal static class InteractingWatched
    {
        private static void Prefix(Player __instance, out Dictionary<string, int> __state) => __state = TogetherWatch.Carried(__instance);

        private static void Postfix(Player __instance, GameObject go, Dictionary<string, int> __state) => TogetherWatch.UsedWith(__instance, go, __state);
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
    internal static class UsingItemWatched
    {
        private static void Prefix(Humanoid __instance, bool fromInventoryGui, out (Dictionary<string, int> Before, GameObject Target) __state) =>
            __state = fromInventoryGui ? (null, null) : (TogetherWatch.Carried(__instance), __instance.GetHoverObject());

        private static void Postfix(Humanoid __instance, (Dictionary<string, int> Before, GameObject Target) __state) => TogetherWatch.UsedWith(__instance, __state.Target, __state.Before);
    }

    [HarmonyPatch(typeof(OfferingBowl), "InitiateSpawnBoss")]
    internal static class BossCalled
    {
        private static void Postfix(OfferingBowl __instance) => TogetherWatch.Called(__instance);
    }

    [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.Catch))]
    internal static class FishCaught
    {
        private static void Prefix(Fish fish, out string __state) => __state = TogetherWatch.Bait(fish);

        private static void Postfix(Fish fish, Character owner, string __state) => TogetherWatch.Caught(fish, owner, __state);
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
