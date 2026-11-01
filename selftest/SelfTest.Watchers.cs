using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// Every patch class Scry registers sits on a game method (<see cref="PatchesInPlace"/>):
        /// read from what Harmony holds as Scry's, so a hook moved or renamed is checked where it
        /// is now. One whose feature the game turned off is named, not looked for.
        /// </summary>
        private static void WatchersInPlace(Probe p)
        {
            var sitting = new HashSet<string>(StringComparer.Ordinal);
            foreach (var method in Harmony.GetAllPatchedMethods())
            {
                var info = Harmony.GetPatchInfo(method);
                if (info == null) continue;
                foreach (var patch in info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers))
                {
                    if (patch.owner == About.Guid && patch.PatchMethod?.DeclaringType != null) sitting.Add(patch.PatchMethod.DeclaringType.FullName);
                }
            }
            var off = new HashSet<Feature>(Faults.ChangedFeatures);
            var registered = About.OwnTypes().Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
                .Select(t => (t.FullName, off.Contains(Compatibility.FeatureOfPatch(t)), CopiesOnly(t))).ToList();
            var missing = PatchesInPlace.Missing(registered, sitting);
            p.Check(missing.Count == 0, $"each of the {Numbers.Count(registered.Count)} patches Scry registers sits on its game method", string.Join(", ", missing));
            foreach (var (patch, isOff, _) in registered) if (isOff) p.Note($"{patch}: its feature went off for a game change, so it is not looked for");
        }

        /// <summary>Whether a patch class only copies a game method's body as it was (a reverse patch): it hooks nothing, so sits on no method.</summary>
        private static bool CopiesOnly(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            bool Hooks(MethodInfo m) => m.Name == "Prefix" || m.Name == "Postfix" || m.Name == "Transpiler" || m.Name == "Finalizer"
                || m.IsDefined(typeof(HarmonyPrefix), false) || m.IsDefined(typeof(HarmonyPostfix), false) || m.IsDefined(typeof(HarmonyTranspiler), false) || m.IsDefined(typeof(HarmonyFinalizer), false);
            return methods.Any(m => m.IsDefined(typeof(HarmonyReversePatch), false)) && !methods.Any(Hooks);
        }
    }
}
