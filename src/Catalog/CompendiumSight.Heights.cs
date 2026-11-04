using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// How tall each kind of place stands over its own ground, for how far it is seen
    /// (<see cref="Sighting.PlaceReach"/>): the highest of what it shows round its middle, within
    /// its radius, measured once per kind from the first of it loaded. A place's radius tells only
    /// how far it reaches across the ground, as a rock spire's 4 m, so a landmark standing tall is
    /// seen by its height. What stands far above, as a dungeon's rooms built high over the world,
    /// is left out.
    /// </summary>
    internal static partial class CompendiumSight
    {
        /// <summary>How high over a place's middle what it shows is counted, in metres, so a dungeon's rooms built far above take no part.</summary>
        private const float PlaceHighest = 300f;

        /// <summary>Each kind of place's height, by prefab, as first measured.</summary>
        private static readonly Dictionary<string, float> PlaceHeights = new Dictionary<string, float>(StringComparer.Ordinal);

        private static readonly List<Renderer> PlaceRenderers = new List<Renderer>();

        /// <summary>Lets the heights measured go, with the world.</summary>
        private static void ForgetHeights()
        {
            PlaceHeights.Clear();
            PlaceRenderers.Clear();
        }

        /// <summary>How tall a place stands over its middle, measured once per kind.</summary>
        private static float PlaceHeight(Location place)
        {
            var key = Utils.GetPrefabName(place.gameObject);
            if (PlaceHeights.TryGetValue(key, out var height)) return height;
            var centre = place.transform.position;
            var round = place.m_exteriorRadius;
            var top = centre.y;
            place.GetComponentsInChildren(false, PlaceRenderers);
            try
            {
                foreach (var shown in PlaceRenderers)
                {
                    if (shown == null || shown is ParticleSystemRenderer) continue;
                    var box = shown.bounds;
                    var dx = box.center.x - centre.x;
                    var dz = box.center.z - centre.z;
                    if (dx * dx + dz * dz > round * round || box.center.y > centre.y + PlaceHighest) continue;
                    top = Mathf.Max(top, Mathf.Min(box.max.y, centre.y + PlaceHighest));
                }
            }
            finally
            {
                PlaceRenderers.Clear();
            }
            return PlaceHeights[key] = top - centre.y;
        }
    }
}
