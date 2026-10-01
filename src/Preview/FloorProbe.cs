using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Finds where a place's floors are (<see cref="FloorFinder"/>): its copy is made with its
    /// colliders kept, rays are cast straight down over it onto them, and where they land on what
    /// faces up is noted, as heights in the space given. The colliders are taken away after, so
    /// the copy is like any other.
    /// </summary>
    internal static class FloorProbe
    {
        /// <summary>How upright a surface must face to stand on (about 45 degrees at the steepest).</summary>
        private const float Upright = 0.7f;

        private static readonly List<Collider> Colliders = new List<Collider>();

        /// <summary>Casts the rays over the copy and adds where they land to <paramref name="hits"/>; how many rays were cast, 0 for a copy with nothing to land on.</summary>
        public static int Read(GameObject copy, Transform space, int layer, List<float> hits)
        {
            if (copy == null) return 0;
            copy.GetComponentsInChildren(false, Colliders);
            var rays = 0;
            try
            {
                // Made and moved this frame: physics learns where its colliders are before they are measured.
                Physics.SyncTransforms();
                Bounds? around = null;
                foreach (var collider in Colliders)
                {
                    if (collider == null || !collider.enabled || collider.isTrigger) continue;
                    if (around == null) around = collider.bounds;
                    else
                    {
                        var grown = around.Value;
                        grown.Encapsulate(collider.bounds);
                        around = grown;
                    }
                }
                if (around == null) return 0;

                var bounds = around.Value;
                var top = bounds.max.y + 1f;
                var length = bounds.size.y + 2f;
                var mask = layer >= 0 ? 1 << layer : Physics.DefaultRaycastLayers;
                var root = copy.transform;
                foreach (var (x, z) in FloorFinder.Grid(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z))
                {
                    rays++;
                    foreach (var hit in Physics.RaycastAll(new Vector3(x, top, z), Vector3.down, length, mask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.normal.y < Upright || hit.collider == null || !hit.collider.transform.IsChildOf(root)) continue;
                        hits.Add(space.InverseTransformPoint(hit.point).y);
                    }
                }
                return rays;
            }
            finally
            {
                // Nothing else of the copy should meet anything; its colliders go once read.
                copy.GetComponentsInChildren(true, Colliders);
                foreach (var collider in Colliders) if (collider != null) Object.Destroy(collider);
                Colliders.Clear();
            }
        }
    }
}
