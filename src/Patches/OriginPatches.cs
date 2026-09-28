using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
            // Runs inside the game's own Awake: whatever goes wrong here must not stop it. The work
            // is in a method of its own, so that a list an update renamed, which fails the method
            // naming it before it runs, is caught here too.
            try
            {
                Record(__instance);
            }
            catch (System.Exception ex)
            {
                Faults.Tell("telling the game's prefabs from those mods add", ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Record(ZNetScene scene)
        {
            var names = new List<string>();
            foreach (var prefab in scene.m_prefabs) if (prefab != null) names.Add(prefab.name);
            foreach (var prefab in scene.m_nonNetViewPrefabs) if (prefab != null) names.Add(prefab.name);
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
            // Runs inside the game's own Awake: as above, nothing here may stop it.
            try
            {
                Record(__instance);
            }
            catch (System.Exception ex)
            {
                Faults.Tell("telling the game's status effects from those mods add", ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Record(ObjectDB db)
        {
            var names = new List<string>();
            foreach (var effect in db.m_StatusEffects) if (effect != null) names.Add(effect.name);
            Origins.StatusEffects.RecordOriginal(names);
        }
    }
}
