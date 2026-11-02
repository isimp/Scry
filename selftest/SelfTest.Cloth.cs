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
        /// runs, rather than the copy hanging stiff in its resting shape; each is pictured. A
        /// Charred mage's worn hood, cloth the game binds to its wearer, keeps none.
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

            // Worn cloth the game binds to its wearer as it puts it on (VisEquipment.SetupCloth);
            // a copy skinned on without that keeps none, its hood hanging as the body poses it.
            var mage = X.Catalog.FirstOrDefault(e => e.Source is GameObject prefab && prefab != null && prefab.name == "Charred_Mage");
            if (mage == null)
            {
                p.Note("there is no Charred mage to show");
                yield break;
            }
            Select(mage);
            yield return Until(() => CopyOf(mage) != null, 20);
            yield return new Wait(2f);
            var dressed = CopyOf(mage);
            var hood = dressed != null && dressed.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.name == "Hood" && r.enabled);
            // Only cloth counts: the shapes the hood would rest against sit on the mage's own legs and stay.
            var worn = dressed != null ? dressed.GetComponentsInChildren<MonoBehaviour>(true).Count(c => c != null && c.GetType().FullName == "MagicaCloth2.MagicaCloth") : -1;
            p.Check(hood && worn == 0, "a Charred mage wears its hood, which keeps no cloth of its own",
                $"hood drawn {hood}, {Numbers.Count(worn)} cloths on the mage");
            yield return Picture(p, "a Charred mage in its hood");
        }

        /// <summary>Whether a cloth has built and runs (<c>MagicaCloth.IsValid</c>), asked by name so the self-test needs no reference to the cloth's library.</summary>
        private static bool Runs(MonoBehaviour cloth)
        {
            var valid = cloth.GetType().GetMethod("IsValid", System.Type.EmptyTypes);
            return cloth.enabled && valid != null && valid.Invoke(cloth, null) is bool ok && ok;
        }
    }
}
