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
    /// or a pickable is picked (<c>Pickable.RPC_Pick</c>), is counted for it, a time where it gave anything. Only what breaks on this machine is
    /// seen, as for the thing's owner. The hooks only watch.
    /// </summary>
    internal static class YieldWatch
    {
        /// <summary>
        /// What is breaking now, the frame it began in and the items come to be as it does, the
        /// innermost last, as one breaking may break another. One from an earlier frame never
        /// ended, its game method having failed, and is let go.
        /// </summary>
        private static readonly List<(string Thing, List<ItemDrop> Made, int Frame, bool Picked)> Breaking = new List<(string, List<ItemDrop>, int, bool)>();

        public static void Begin(Component thing)
        {
            Breaking.RemoveAll(b => b.Frame != Time.frameCount);
            if (thing == null) return;
            Breaking.Add((Utils.GetPrefabName(thing.gameObject), new List<ItemDrop>(), Time.frameCount, thing is Pickable));
        }

        /// <summary>An item come to be: kept for what is breaking now, if anything is.</summary>
        public static void ItemMade(ItemDrop item)
        {
            if (Breaking.Count > 0 && item != null && Breaking[Breaking.Count - 1].Frame == Time.frameCount) Breaking[Breaking.Count - 1].Made.Add(item);
        }

        /// <summary>What broke gave what came to be meanwhile, as whole stacks.</summary>
        public static void End()
        {
            if (Breaking.Count == 0 || Breaking[Breaking.Count - 1].Frame != Time.frameCount) return;
            var (thing, made, _, picked) = Breaking[Breaking.Count - 1];
            Breaking.RemoveAt(Breaking.Count - 1);
            if (made.Count == 0) return;
            var loot = new List<(string, int)>();
            foreach (var item in made)
            {
                if (item != null) loot.Add((Utils.GetPrefabName(item.gameObject), Mathf.Max(1, item.m_itemData?.m_stack ?? 1)));
            }
            CompendiumKeeper.Yielded(thing, loot, picked);
        }
    }

    [HarmonyPatch(typeof(TreeBase), "RPC_Damage")]
    internal static class TreeYield
    {
        private static void Prefix(TreeBase __instance) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Begin(__instance));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.End);
    }

    [HarmonyPatch(typeof(TreeLog), "Destroy")]
    internal static class LogYield
    {
        private static void Prefix(TreeLog __instance) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Begin(__instance));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.End);
    }

    [HarmonyPatch(typeof(MineRock), "RPC_Hit")]
    internal static class RockYield
    {
        private static void Prefix(MineRock __instance) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Begin(__instance));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.End);
    }

    [HarmonyPatch(typeof(MineRock5), "DamageArea")]
    internal static class VeinYield
    {
        private static void Prefix(MineRock5 __instance) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Begin(__instance));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.End);
    }

    [HarmonyPatch(typeof(DropOnDestroyed), "OnDestroyed")]
    internal static class BrokenYield
    {
        private static void Prefix(DropOnDestroyed __instance) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Begin(__instance));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.End);
    }

    [HarmonyPatch(typeof(Pickable), "RPC_Pick")]
    internal static class PickYield
    {
        private static void Prefix(Pickable __instance) => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", () => YieldWatch.Begin(__instance));

        private static void Postfix() => Guard.Run(Feature.CompendiumTogether, "watching what breaks give", YieldWatch.End);
    }

    [HarmonyPatch(typeof(ItemDrop), "Awake")]
    internal static class YieldItemMade
    {
        private static void Postfix(ItemDrop __instance) => YieldWatch.ItemMade(__instance);
    }
}
