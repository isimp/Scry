using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What was made for one copy alone, destroyed when the copy is. Asking a renderer for its
    /// <c>material</c> makes it a material of its own, and Unity does not destroy that with the
    /// renderer: it would stay in memory until the game next unloads unused assets, which it does
    /// when a world loads and once an hour.
    /// </summary>
    internal sealed class Owned : MonoBehaviour
    {
        /// <summary>
        /// How long what is owned outlives the copy. The parts a piece breaks into draw with the
        /// copy's materials and are gone within four seconds (<see cref="Falling.Break"/>), and a
        /// copy in the world can be made again while they still fly.
        /// </summary>
        private const float Grace = 5f;

        private readonly List<Object> _things = new List<Object>();

        /// <summary>
        /// A renderer's material of its own, as <c>Renderer.material</c> gives it, owned by the
        /// copy when it is made now. One the renderer already had is not taken twice.
        /// </summary>
        public static Material MaterialOf(Renderer renderer, GameObject copy)
        {
            var before = renderer.sharedMaterial;
            var material = renderer.material;
            if (material != null && material != before) Add(copy, material);
            return material;
        }

        /// <summary>Has the copy destroy something with it.</summary>
        public static void Add(GameObject copy, Object thing)
        {
            if (copy == null || thing == null) return;
            var owned = copy.GetComponent<Owned>();
            if (owned == null) owned = copy.AddComponent<Owned>();
            if (!owned._things.Contains(thing)) owned._things.Add(thing);
        }

        private void OnDestroy()
        {
            foreach (var thing in _things) if (thing != null) Destroy(thing, Grace);
            _things.Clear();
        }
    }
}
