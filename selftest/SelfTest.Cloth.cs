using System.Collections;
using System.Collections.Generic;
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
        /// Charred mage's worn hood, cloth the game binds to its wearer, keeps its cloth, built on
        /// the mage's bones and resting against the shapes on its body, and it runs.
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

            // Worn cloth the game binds to its wearer as it puts it on (VisEquipment.SetupCloth):
            // built on the body's bones, not the hood's own, which no longer move, resting against
            // the shapes on the mage's legs, and run.
            var mage = X.Catalog.FirstOrDefault(e => e.Source is GameObject prefab && prefab != null && prefab.name == "Charred_Mage");
            if (mage == null)
            {
                p.Note("there is no Charred mage to show");
                yield break;
            }
            BigStage();
            // Held still, so its two pictures show it from one side.
            _spinWas = Stage.Spin;
            Stage.Spin = false;
            Select(mage);
            yield return Until(() => CopyOf(mage) != null, 20);
            yield return new Wait(2f);
            var dressed = CopyOf(mage);
            var hood = dressed != null ? dressed.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Hood" && r.enabled) : null;
            var part = hood != null ? hood.GetComponentInParent<Hung>(true) : null;
            var worn = part != null ? part.GetComponentsInChildren<MonoBehaviour>(true).Where(IsCloth).ToList() : new List<MonoBehaviour>();
            var bones = new HashSet<Transform>(hood != null ? hood.bones.Where(b => b != null) : Enumerable.Empty<Transform>());
            var boneNames = new HashSet<string>(bones.Select(b => b.name));
            int wornRunning = 0, onBody = 0, onOwn = 0;
            foreach (var cloth in worn)
            {
                if (Runs(cloth)) wornRunning++;
                foreach (var used in UsedBy(cloth))
                {
                    if (bones.Contains(used)) onBody++;
                    else if (boneNames.Contains(used.name)) onOwn++;
                }
            }
            p.Check(hood != null && worn.Count > 0 && wornRunning == worn.Count && onBody > 0 && onOwn == 0,
                "a Charred mage's hood keeps its cloth, built on the mage's bones, and it runs",
                $"hood drawn {hood != null}, {Numbers.Count(worn.Count)} cloths on it, {Numbers.Count(wornRunning)} running; of the bones they follow {Numbers.Count(onBody)} are the mage's, {Numbers.Count(onOwn)} the hood's own");

            var named = ShapesNamed(mage.Source as GameObject);
            var resting = worn.Select(c => RestsOn(c).Count(s => s != null && dressed != null && s.transform.IsChildOf(dressed.transform) && !s.transform.IsChildOf(part.transform))).ToList();
            p.Check(worn.Count > 0 && resting.All(n => n >= named), "and the cloth rests against the shapes on the mage's body",
                $"the mage's prefab names {Numbers.Count(named)}; each cloth rests against {string.Join(", ", resting.Select(n => Numbers.Count(n)))} of the body's");

            if (hood != null && Stage.PictureOf(hood.bounds.center) is Vector2 at)
            {
                for (var i = 0; i < 3; i++) Stage.ZoomBy(-3f, at);
            }
            yield return Settled();

            // As copies drew it while worn cloth was not set up: the cloth switched off, the hood posed by the body alone.
            foreach (var cloth in worn) cloth.enabled = false;
            yield return new Wait(0.5f);
            yield return Picture(p, "a Charred mage's hood, its cloth switched off");
            foreach (var cloth in worn) cloth.enabled = true;
            yield return new Wait(1.5f);
            p.Check(worn.Count > 0 && worn.All(Runs), "switched on again, the hood's cloth runs", $"{Numbers.Count(worn.Count(Runs))} of {Numbers.Count(worn.Count)} running");
            yield return Picture(p, "a Charred mage's hood with its cloth moving");
            ClothAsItWas();
        }

        private static bool? _spinWas;

        /// <summary>Puts the stage's turning back as it was before the hood's pictures, and its size (<see cref="StageAsItWas"/>).</summary>
        private static void ClothAsItWas()
        {
            if (_spinWas is bool spin) Stage.Spin = spin;
            _spinWas = null;
            StageAsItWas();
        }

        private static bool IsCloth(MonoBehaviour script) => script != null && script.GetType().FullName == "MagicaCloth2.MagicaCloth";

        /// <summary>The transforms a cloth follows (<c>MagicaCloth.GetUsedTransform</c>), asked by name.</summary>
        private static IEnumerable<Transform> UsedBy(MonoBehaviour cloth) =>
            cloth.GetType().GetMethod("GetUsedTransform", System.Type.EmptyTypes)?.Invoke(cloth, null) as IEnumerable<Transform> ?? Enumerable.Empty<Transform>();

        /// <summary>The shapes a cloth rests against (<c>colliderCollisionConstraint.colliderList</c>).</summary>
        private static IEnumerable<Component> RestsOn(MonoBehaviour cloth) =>
            ClothLibrary.Of(cloth.GetType()).CollidersOf(cloth)?.OfType<Component>() ?? Enumerable.Empty<Component>();

        /// <summary>How many shapes a prefab's worn cloth rests against (<c>VisEquipment.m_clothColliders</c>).</summary>
        private static int ShapesNamed(GameObject prefab)
        {
            var vis = prefab != null ? prefab.GetComponentInChildren<VisEquipment>(true) : null;
            var list = vis != null ? TypeFields.Value(TypeFields.Named(typeof(VisEquipment), "m_clothColliders"), vis) as IEnumerable : null;
            return list?.Cast<object>().Count(c => c != null) ?? 0;
        }

        /// <summary>Whether a cloth has built and runs (<c>MagicaCloth.IsValid</c>), asked by name so the self-test needs no reference to the cloth's library.</summary>
        private static bool Runs(MonoBehaviour cloth)
        {
            var valid = cloth.GetType().GetMethod("IsValid", System.Type.EmptyTypes);
            return cloth.enabled && valid != null && valid.Invoke(cloth, null) is bool ok && ok;
        }
    }
}
