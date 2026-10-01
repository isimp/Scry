using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A copy drawn darker, or as it was again: each of its materials' tint and glow at a share of
    /// their own, through a property block, so no material is copied or changed. A part whose
    /// shader takes no tint is drawn as it was. Rooms below a dungeon's opened floor are dimmed so.
    /// </summary>
    internal static class Dim
    {
        /// <summary>The share of its own brightness a dimmed part keeps.</summary>
        public const float Share = 0.3f;

        private static readonly int Tint = Shader.PropertyToID("_Color");
        private static readonly int Glow = Shader.PropertyToID("_EmissionColor");
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        private static readonly List<Renderer> Renderers = new List<Renderer>();

        /// <summary>Dims a copy, or takes the dimming off.</summary>
        public static void Set(GameObject copy, bool dim)
        {
            if (copy == null) return;
            copy.GetComponentsInChildren(true, Renderers);
            try
            {
                foreach (var renderer in Renderers)
                {
                    if (renderer == null) continue;
                    var materials = renderer.sharedMaterials;
                    for (var i = 0; i < materials.Length; i++)
                    {
                        if (!dim)
                        {
                            renderer.SetPropertyBlock(null, i);
                            continue;
                        }
                        var material = materials[i];
                        if (material == null) continue;
                        Block.Clear();
                        var tinted = false;
                        if (material.HasProperty(Tint))
                        {
                            var own = material.GetColor(Tint);
                            Block.SetColor(Tint, new Color(own.r * Share, own.g * Share, own.b * Share, own.a));
                            tinted = true;
                        }
                        if (material.HasProperty(Glow))
                        {
                            Block.SetColor(Glow, material.GetColor(Glow) * Share);
                            tinted = true;
                        }
                        if (tinted) renderer.SetPropertyBlock(Block, i);
                    }
                }
            }
            finally
            {
                Renderers.Clear();
            }
        }
    }
}
