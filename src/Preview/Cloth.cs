using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>
    /// Cloth worn on a copy's body, set up as the game sets it up as it puts it on
    /// (<c>VisEquipment.SetupCloth</c>), through what <see cref="ClothLibrary"/> finds of the
    /// cloth physics the game ships.
    /// </summary>
    internal static class Cloth
    {
        /// <summary>The cloths on a copy that wait for their wearer (<see cref="StripPolicy.WaitsForWearer"/>).</summary>
        private static IEnumerable<MonoBehaviour> Waiting(GameObject copy) =>
            copy.GetComponentsInChildren<MonoBehaviour>(true).Where(s => s != null && StripPolicy.WaitsForWearer(new ComponentFacts(s.GetType().FullName, isScript: true), skinned: true));

        /// <summary>
        /// Holds each cloth of a sleeping copy, made to be skinned onto a body, from building itself
        /// as it wakes, for <see cref="Wear"/> to set up. One that cannot be held goes, and its
        /// part hangs as the body poses it.
        /// </summary>
        public static void Hold(GameObject copy)
        {
            foreach (var cloth in Waiting(copy).ToList())
            {
                var hold = ClothLibrary.Of(cloth.GetType()).DisableAutoBuild;
                if (hold == null || !Guard.Run(Feature.WornClothMoving, "worn cloth held", () => hold.Invoke(cloth, null))) Object.DestroyImmediate(cloth);
            }
        }

        /// <summary>
        /// The shapes a body's worn cloth rests against (<c>VisEquipment.m_clothColliders</c>), as
        /// their twins on the copy; empty where the prefab names none.
        /// </summary>
        public static List<Component> WearerColliders(VisEquipment vis, Transform prefabRoot, Transform copyRoot)
        {
            var twins = new List<Component>();
            if (!(TypeFields.Value(ClothColliders, vis) is IEnumerable colliders)) return twins;
            foreach (var collider in colliders.OfType<Component>())
            {
                if (collider == null) continue;
                var twin = Looks.Twin(prefabRoot, copyRoot, collider.transform);
                if (twin == null) continue;
                // The same one of its kind on the part, should a part carry two.
                var index = Array.IndexOf(collider.GetComponents(collider.GetType()), collider);
                var mates = twin.GetComponents(collider.GetType());
                if (index >= 0 && index < mates.Length) twins.Add(mates[index]);
            }
            return twins;
        }

        private static readonly FieldInfo ClothColliders = TypeFields.Named(typeof(VisEquipment), "m_clothColliders");

        /// <summary>
        /// Sets up the held cloth of a part just skinned onto <paramref name="body"/>, as the game
        /// does as it puts a part on: initialised with its renderers bound to the body, kept from
        /// building itself, resting against the wearer's shapes before its own, its bones swapped
        /// for the body's of the same name, then built and run.
        /// </summary>
        public static void Wear(GameObject worn, SkinnedMeshRenderer body, List<Component> wearerColliders)
        {
            Dictionary<string, Transform> bones = null;
            foreach (var cloth in Waiting(worn))
            {
                var library = ClothLibrary.Of(cloth.GetType());
                Guard.Each(Feature.WornClothMoving, "worn cloth", worn.name, () =>
                {
                    library.Initialize.Invoke(cloth, null);
                    library.DisableAutoBuild.Invoke(cloth, null);
                    if (wearerColliders.Count > 0)
                    {
                        // The cloth's own list, in the order it rests against them.
                        var list = library.CollidersOf(cloth);
                        var all = WornCloth.Colliders(wearerColliders, list.Cast<Component>());
                        list.Clear();
                        foreach (var collider in all) list.Add(collider);
                    }
                    if (bones == null) bones = WornCloth.BoneMap(body.bones, b => b.name);
                    library.ReplaceTransform.Invoke(cloth, new object[] { bones });
                    library.BuildAndRun.Invoke(cloth, null);
                });
            }
        }
    }
}
