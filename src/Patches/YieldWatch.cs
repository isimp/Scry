using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What trees, logs, rocks and other things that break are seen to give, for the strict
    /// rules' amounts (<see cref="CompendiumBook.Yielded"/>): every item that comes to be while
    /// one breaks or loses a piece (<c>TreeBase.RPC_Damage</c>, <c>TreeLog.Destroy</c>,
    /// <c>MineRock.RPC_Hit</c>, <c>MineRock5.DamageArea</c>, <c>DropOnDestroyed.OnDestroyed</c>),
    /// or a pickable is picked (<c>Pickable.RPC_Pick</c>), is told as its break or pick, a time
    /// where it gave anything (<see cref="WorldEvents"/>), with the player who did it: the one
    /// whose hit it took, for a thing dropping what it holds as it goes the one whose hit
    /// destroyed it (<c>Destructible.Destroy</c>, <c>WearNTear.Destroy</c>), for a pick the one
    /// whose game asked for it. Only what breaks on this machine is seen, as for the thing's
    /// owner. The hooks only watch.
    /// </summary>
    internal static class YieldWatch
    {
        /// <summary>
        /// What is breaking now, the frame it began in and the items come to be as it does, the
        /// innermost last, as one breaking may break another. One from an earlier frame never
        /// ended, its game method having failed, and is let go.
        /// </summary>
        private static readonly List<(string Thing, List<ItemDrop> Made, int Frame, bool Picked, IReadOnlyList<long> Doers, Vector3 At)> Breaking =
            new List<(string, List<ItemDrop>, int, bool, IReadOnlyList<long>, Vector3)>();

        /// <summary>A thing breaking or being picked by the players given, where it stands.</summary>
        public static void Begin(Component thing, IReadOnlyList<long> doers)
        {
            Breaking.RemoveAll(b => b.Frame != Time.frameCount);
            if (thing == null) return;
            Breaking.Add((Utils.GetPrefabName(thing.gameObject), new List<ItemDrop>(), Time.frameCount, thing is Pickable, doers, thing.transform.position));
        }

        /// <summary>The players whose hits are destroying things now, the innermost last, and the frame each began in, as <see cref="Breaking"/>.</summary>
        private static readonly List<(IReadOnlyList<long> Doers, int Frame)> Destroyers = new List<(IReadOnlyList<long>, int)>();

        /// <summary>A thing being destroyed by a hit, before what it holds drops.</summary>
        public static void Destroying(HitData hit)
        {
            Destroyers.RemoveAll(d => d.Frame != Time.frameCount);
            Destroyers.Add((WorldEvents.HitBy(hit), Time.frameCount));
        }

        /// <summary>The thing destroyed last is gone.</summary>
        public static void Destroyed()
        {
            if (Destroyers.Count > 0 && Destroyers[Destroyers.Count - 1].Frame == Time.frameCount) Destroyers.RemoveAt(Destroyers.Count - 1);
        }

        /// <summary>The players whose hit is destroying a thing now; no one where nothing is.</summary>
        public static IReadOnlyList<long> DestroyedBy() =>
            Destroyers.Count > 0 && Destroyers[Destroyers.Count - 1].Frame == Time.frameCount ? Destroyers[Destroyers.Count - 1].Doers : null;

        /// <summary>An item come to be: kept for what is breaking now, if anything is.</summary>
        public static void ItemMade(ItemDrop item)
        {
            if (Breaking.Count > 0 && item != null && Breaking[Breaking.Count - 1].Frame == Time.frameCount) Breaking[Breaking.Count - 1].Made.Add(item);
        }

        /// <summary>What broke gave what came to be meanwhile, as whole stacks, told as its break or pick; a hit that gave nothing is neither.</summary>
        public static void End()
        {
            if (Breaking.Count == 0 || Breaking[Breaking.Count - 1].Frame != Time.frameCount) return;
            var (thing, made, _, picked, doers, at) = Breaking[Breaking.Count - 1];
            Breaking.RemoveAt(Breaking.Count - 1);
            if (made.Count == 0) return;
            var loot = new List<(string, int)>();
            foreach (var item in made)
            {
                if (item != null) loot.Add((Utils.GetPrefabName(item.gameObject), Mathf.Max(1, item.m_itemData?.m_stack ?? 1)));
            }
            WorldEvents.Tell(picked ? Happened.Picked : Happened.Broke, thing, at, loot, doers: doers);
        }
    }

    [HarmonyPatch(typeof(TreeBase), "RPC_Damage")]
    internal static class TreeYield
    {
        private static void Prefix(TreeBase __instance, HitData hit) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Begin(__instance, WorldEvents.HitBy(hit)));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.End);
    }

    [HarmonyPatch(typeof(TreeLog), "Destroy")]
    internal static class LogYield
    {
        private static void Prefix(TreeLog __instance, HitData hitData) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Begin(__instance, WorldEvents.HitBy(hitData)));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.End);
    }

    [HarmonyPatch(typeof(MineRock), "RPC_Hit")]
    internal static class RockYield
    {
        private static void Prefix(MineRock __instance, HitData hit) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Begin(__instance, WorldEvents.HitBy(hit)));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.End);
    }

    [HarmonyPatch(typeof(MineRock5), "DamageArea")]
    internal static class VeinYield
    {
        private static void Prefix(MineRock5 __instance, HitData hit) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Begin(__instance, WorldEvents.HitBy(hit)));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.End);
    }

    [HarmonyPatch(typeof(DropOnDestroyed), "OnDestroyed")]
    internal static class BrokenYield
    {
        private static void Prefix(DropOnDestroyed __instance) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Begin(__instance, YieldWatch.DestroyedBy()));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.End);
    }

    [HarmonyPatch(typeof(Pickable), "RPC_Pick")]
    internal static class PickYield
    {
        private static void Prefix(Pickable __instance, long sender) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Begin(__instance, WorldEvents.AskedBy(sender)));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.End);
    }

    /// <summary>The hit a destructible thing is destroyed by, for what it holds as it goes.</summary>
    [HarmonyPatch(typeof(Destructible), nameof(Destructible.Destroy))]
    internal static class DestroyingWatched
    {
        private static void Prefix(HitData hit) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Destroying(hit));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.Destroyed);
    }

    /// <summary>The hit a building piece is destroyed by, for what it holds as it goes.</summary>
    [HarmonyPatch(typeof(WearNTear), "Destroy")]
    internal static class PieceDestroyingWatched
    {
        private static void Prefix(HitData hitData) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Destroying(hitData));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.Destroyed);
    }

    [HarmonyPatch(typeof(ItemDrop), "Awake")]
    internal static class YieldItemMade
    {
        private static void Postfix(ItemDrop __instance) => YieldWatch.ItemMade(__instance);
    }
}
