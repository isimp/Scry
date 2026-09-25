using System.Collections.Generic;
using HarmonyLib;

namespace Scry
{
    /// <summary>The records of which prefabs and status effects are the game's own.</summary>
    internal static class Origins
    {
        public static readonly Provenance Prefabs = new Provenance();
        public static readonly Provenance StatusEffects = new Provenance();
    }

    /// <summary>
    /// The scene's prefab lists as they come out of the game's files, read before anything else
    /// gets to run in <c>ZNetScene.Awake</c>. Mods add theirs to the same lists, in this method or
    /// later.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class SceneOrigins
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(ZNetScene __instance)
        {
            var names = new List<string>();
            foreach (var prefab in __instance.m_prefabs) if (prefab != null) names.Add(prefab.name);
            foreach (var prefab in __instance.m_nonNetViewPrefabs) if (prefab != null) names.Add(prefab.name);
            Origins.Prefabs.RecordOriginal(names);
        }
    }

    /// <summary>The same for status effects, which live in <c>ObjectDB</c>.</summary>
    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    internal static class StatusEffectOrigins
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(ObjectDB __instance)
        {
            var names = new List<string>();
            foreach (var effect in __instance.m_StatusEffects) if (effect != null) names.Add(effect.name);
            Origins.StatusEffects.RecordOriginal(names);
        }
    }
}
