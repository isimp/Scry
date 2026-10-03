using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What each prefab is made of (<see cref="PrefabShape"/>), read from its components once
    /// and kept for the world: debris and whole models are told apart while the catalog is read
    /// and again by every preview of an effect list.
    /// </summary>
    internal static class PrefabShapes
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static PrefabShapes() => WorldCaches.Register(nameof(PrefabShapes), Forget);

        private static readonly Dictionary<GameObject, PrefabShape> Known = new Dictionary<GameObject, PrefabShape>();

        private static void Forget() => Known.Clear();

        public static PrefabShape Of(GameObject prefab)
        {
            if (prefab == null) return default;
            if (!Known.TryGetValue(prefab, out var shape))
            {
                shape = new PrefabShape
                {
                    FreeBody = HasFreeBody(prefab),
                    Ragdoll = prefab.GetComponentInChildren<Ragdoll>(true) != null,
                    Character = prefab.GetComponentInChildren<Character>(true) != null,
                    Item = prefab.GetComponent<ItemDrop>() != null,
                    Skinned = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null,
                };
                Known[prefab] = shape;
            }
            return shape;
        }

        /// <summary>Whether an effect list's prefab is debris (<see cref="PrefabShape.IsDebris"/>).</summary>
        public static bool IsDebris(GameObject prefab) => prefab != null && Of(prefab).IsDebris;

        /// <summary>Whether an effect list's prefab is a whole model rather than an effect (<see cref="PrefabShape.IsWholeModel"/>).</summary>
        public static bool IsWholeModel(GameObject prefab) => prefab != null && Of(prefab).IsWholeModel;

        private static bool HasFreeBody(GameObject prefab)
        {
            foreach (var body in prefab.GetComponentsInChildren<Rigidbody>(true))
            {
                if (!body.isKinematic) return true;
            }
            return false;
        }
    }
}
