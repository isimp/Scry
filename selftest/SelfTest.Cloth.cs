using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for cloth physics moving on copies as their prefabs carry it.</summary>
    internal static partial class SelfTest
    {
        /// <summary>Prefabs whose own body moves by the cloth physics the game ships: tentacles, a body, a tail, a skirt and a sail.</summary>
        private static readonly string[] ClothPrefabs = { "Gjall", "Wolf", "Ulv", "Hildir", "Karve" };

        /// <summary>
        /// Each of a few prefabs whose body moves by the game's cloth physics (MagicaCloth 2) is
        /// shown on the stage: its copy keeps the cloth and, once woken, the cloth has built and
        /// runs, rather than the copy hanging stiff in its resting shape; each is pictured.
        /// </summary>
        private static IEnumerator ClothMoves(Probe p)
        {
            var shown = ClothPrefabs.Select(name => X.Catalog.FirstOrDefault(e => e.Source is GameObject prefab && prefab != null && prefab.name == name)).Where(e => e != null).ToList();
            if (shown.Count == 0) p.Skip("none of the prefabs carrying cloth is in the catalog");
            foreach (var entry in shown)
            {
                Select(entry);
                yield return Until(() => CopyOf(entry) != null, 20);
                // The cloth builds over a few frames after the copy wakes.
                yield return new Wait(2f);
                var copy = CopyOf(entry);
                var cloths = copy != null ? copy.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null && c.GetType().FullName == "MagicaCloth2.MagicaCloth").ToList() : null;
                var running = cloths?.Count(Runs) ?? 0;
                p.Check(cloths != null && cloths.Count > 0 && running == cloths.Count, $"{entry.Name} keeps its cloth, and it runs",
                    cloths == null ? "no copy" : $"{Numbers.Count(cloths.Count)} cloths kept, {Numbers.Count(running)} running");
                yield return Picture(p, entry.Name + " with its cloth moving");
            }
        }

        /// <summary>Whether a cloth has built and runs (<c>MagicaCloth.IsValid</c>), asked by name so the self-test needs no reference to the cloth's library.</summary>
        private static bool Runs(MonoBehaviour cloth)
        {
            var valid = cloth.GetType().GetMethod("IsValid", System.Type.EmptyTypes);
            return cloth.enabled && valid != null && valid.Invoke(cloth, null) is bool ok && ok;
        }
    }
}
