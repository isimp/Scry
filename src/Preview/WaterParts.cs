using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Whether a copy holds the game's water: a part drawn with its water shader, which reads the
    /// camera's depth to show what lies under the surface and soften its edges, and without it
    /// stands as a flat, lit slab, as the flood in a sunken crypt's rooms does.
    /// </summary>
    internal static class WaterParts
    {
        /// <summary>The game's water shader, by name.</summary>
        public const string Shader = "Custom/Water";

        private static readonly List<Renderer> Found = new List<Renderer>();

        /// <summary>Whether any part of a copy, shown or not, is drawn with the game's water shader.</summary>
        public static bool In(GameObject copy)
        {
            if (copy == null) return false;
            copy.GetComponentsInChildren(true, Found);
            try
            {
                foreach (var renderer in Found)
                {
                    if (renderer == null) continue;
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null && material.shader != null && material.shader.name == Shader) return true;
                    }
                }
                return false;
            }
            finally
            {
                Found.Clear();
            }
        }
    }
}
