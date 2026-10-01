using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Finds where a place's floors are (<see cref="FloorFinder"/>): its copy is made with its
    /// colliders kept, rays are cast straight down over it onto them on a grid, and where they
    /// land on what is flat is noted, as heights in the space given, with each ray's place on the
    /// grid and the ground it stands for. A roof (its pieces lean 26 or 45 degrees) or a ramp is
    /// not flat. The colliders are taken away after, so the copy is like any other.
    /// </summary>
    internal static class FloorProbe
    {
        /// <summary>How flat a surface must be to count as floor: about 18 degrees at the steepest.</summary>
        private const float Flat = 0.95f;

        private static readonly List<Collider> Colliders = new List<Collider>();

        /// <summary>
        /// Casts the rays over the copy and adds where they land to <paramref name="hits"/>, as
        /// patch <paramref name="patch"/>; the ground the rays were cast over, 0 for a copy with
        /// nothing to land on.
        /// </summary>
        public static float Read(GameObject copy, Transform space, int layer, List<FloorHit> hits, int patch)
        {
            if (copy == null) return 0f;
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
                if (around == null) return 0f;

                var bounds = around.Value;
                var top = bounds.max.y + 1f;
                var length = bounds.size.y + 2f;
                var mask = layer >= 0 ? 1 << layer : Physics.DefaultRaycastLayers;
                var root = copy.transform;
                var grid = FloorFinder.Grid(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z);
                var cell = FloorFinder.CellArea(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z, grid.Count);
                foreach (var (i, j, x, z) in grid)
                {
                    rays++;
                    foreach (var hit in Physics.RaycastAll(new Vector3(x, top, z), Vector3.down, length, mask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.normal.y < Flat || hit.collider == null || !hit.collider.transform.IsChildOf(root)) continue;
                        hits.Add(new FloorHit { Patch = patch, I = i, J = j, Height = space.InverseTransformPoint(hit.point).y, Area = cell });
                    }
                }
                return rays * cell;
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
