using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Scry
{
    /// <summary>
    /// The game's own item tooltip, without any mod's hooks: Harmony copies
    /// <c>ItemData.GetTooltip</c> as the game ships it into <see cref="Of"/> (a reverse patch), so
    /// the lines mods add to an item's tooltip can be told from the game's. Null where the copy
    /// could not be made.
    /// </summary>
    [HarmonyPatch]
    internal static class UnmoddedTooltip
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
            new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Of(ItemDrop.ItemData item, int qualityLevel, bool crafting, float worldLevel, int stackOverride, bool appending) => null;
    }
}
