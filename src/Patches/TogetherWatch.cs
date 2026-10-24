using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the character does that ties two things together, for the compendium's strict rules:
    /// an item picked up with the biome and place it lay in, a piece placed with the tool that
    /// built it, a creature petted, which makes it known, and one seen eat with what it
    /// ate; what the character put into a thing it used (ore, fuel, food, an offering) with it, and
    /// an item coming to be near a thing it used or hit lately with that thing, where it gives it
    /// (<see cref="ActedOn"/>); a boss called at its altar with the altar, its offering and its
    /// place; a fish caught with its bait and where it swam. Picking up, placing and petting run
    /// on the client of the character doing it; eating only on the client that owns the creature,
    /// where its mind runs, told there as an event the character sees near by
    /// (<see cref="WorldEvents"/>). A taming is seen otherwise (<see cref="CompendiumKeeper"/>).
    /// </summary>
    internal static class TogetherWatch
    {
        /// <summary>How near the character a boss is called for the character to see it, as far as a creature in view is seen.</summary>
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

        /// <summary>
        /// A creature was hit, by the character or, on a creature this game runs, by anyone near:
        /// the damage types the game's popup tells how it takes (<see cref="HitTold"/>), which it
        /// shows every player within its text distance, worked out as the game works it out on
        /// receiving the hit, by its own <c>HitData.ApplyResistance</c> on a copy; paired with the
        /// creature. A hit on a weak spot is passed over, as it lands by the weak spot's own
        /// modifiers, and found only by the character's own hit.
        /// </summary>
        public static void Hit(Character target, HitData hit, bool mine = true)
        {
            if (target == null || hit == null || target.IsPlayer() || Player.m_localPlayer == null) return;
            if (mine ? hit.GetAttacker() != Player.m_localPlayer : hit.GetAttacker() == Player.m_localPlayer || !PopupSeen(target)) return;
            // A weak spot hit shows how that spot takes it, not the creature: the spot is found, no more.
            if (hit.m_weakSpot >= 0)
            {
                if (!mine) return;
                Guard.Run(Feature.CompendiumTogether, "noting a weak spot found", () =>
                {
                    var spots = target.m_weakSpots;
                    var spot = spots != null && hit.m_weakSpot < spots.Length ? spots[hit.m_weakSpot] : null;
                    if (spot != null) CompendiumKeeper.FoundWeakSpot(target, spot.gameObject.name);
                });
                return;
            }
            Guard.Run(Feature.CompendiumTogether, "noting how a creature took a hit", () => Told(Utils.GetPrefabName(target.gameObject), hit, target.GetDamageModifiers()));
        }

        /// <summary>
        /// The damage types the game's popup tells of a hit of the character's on what takes it by
        /// these modifiers (<see cref="HitTold"/>), worked out as the game works it out on receiving
        /// the hit, by its own <c>HitData.ApplyResistance</c> on a copy, and found out of it.
        /// </summary>
        public static void Told(string key, HitData hit, HitData.DamageModifiers mods)
        {
            var copy = hit.Clone();
            copy.ApplyResistance(mods, out var shown);
            var d = hit.m_damage;
            var parts = new[]
            {
                (ResistWords.Types[0], d.m_blunt, (Degree)(int)mods.m_blunt), (ResistWords.Types[1], d.m_slash, (Degree)(int)mods.m_slash),
                (ResistWords.Types[2], d.m_pierce, (Degree)(int)mods.m_pierce), (ResistWords.Types[3], d.m_chop, (Degree)(int)mods.m_chop),
                (ResistWords.Types[4], d.m_pickaxe, (Degree)(int)mods.m_pickaxe), (ResistWords.Types[5], d.m_fire, (Degree)(int)mods.m_fire),
                (ResistWords.Types[6], d.m_frost, (Degree)(int)mods.m_frost), (ResistWords.Types[7], d.m_lightning, (Degree)(int)mods.m_lightning),
                (ResistWords.Types[8], d.m_poison, (Degree)(int)mods.m_poison), (ResistWords.Types[9], d.m_spirit, (Degree)(int)mods.m_spirit),
            };
            foreach (var type in HitTold.Types(parts, (Degree)(int)shown)) CompendiumKeeper.FoundOut(key, EntryKeys.Damage(type));
            CompendiumKeeper.Noted(PlayEvent.HitTold);
        }

        /// <summary>Whether the game shows the character a hit's popup on a creature: within the popup's distance (<c>DamageText.m_maxTextDistance</c>, 30 m).</summary>
        private static bool PopupSeen(Character target)
        {
            var reach = DamageText.instance != null ? DamageText.instance.m_maxTextDistance : 30f;
            return Vector3.Distance(Player.m_localPlayer.transform.position, target.transform.position) <= reach;
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
                if (!string.IsNullOrEmpty(bait)) CompendiumKeeper.Saw(key, bait, Tie.Eats);
                CompendiumKeeper.MetIn(key, fish.transform.position);
            });
        }

        public static void Placed(Player who, Piece piece, Vector3 at)
        {
            if (who == null || who != Player.m_localPlayer || piece == null) return;
            Guard.Run(Feature.CompendiumTogether, "noting what built a piece", () =>
                CompendiumKeeper.Placed(piece, at, who.GetCurrentWeapon()?.m_dropPrefab.OrNull()?.name));
        }

        public static void Petted(Tameable tameable, Humanoid user)
        {
            var creature = tameable != null ? tameable.GetComponent<Character>() : null;
            if (user == null || user != Player.m_localPlayer || creature == null || !creature.IsTamed()) return;
            Guard.Run(Feature.CompendiumTogether, "noting a creature petted", () => CompendiumKeeper.Tamed(creature, tamedNow: false));
        }

        public static void Ate(Tameable tameable, ItemDrop food)
        {
            var creature = tameable != null ? tameable.GetComponent<Character>() : null;
            if (food == null || creature == null) return;
            Guard.Run(Feature.CompendiumTogether, "noting what a creature ate", () => WorldEvents.Tell(Happened.Ate, creature, Utils.GetPrefabName(food.gameObject)));
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

    /// <summary>A hit anyone lands on a creature this game runs (<c>Character.RPC_Damage</c> runs on the creature's owner): how it took it, where the character saw the popup.</summary>
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class HitOnOwnedWatched
    {
        private static void Postfix(Character __instance, HitData hit) => TogetherWatch.Hit(__instance, hit, mine: false);
    }

    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class HitToldWatched
    {
        // After the game found which weak spot was hit, before the hit is sent on.
        private static void Postfix(Character __instance, HitData hit) => TogetherWatch.Hit(__instance, hit);
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
        private static void Postfix(Player __instance, Piece piece, Vector3 pos) => TogetherWatch.Placed(__instance, piece, pos);
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
