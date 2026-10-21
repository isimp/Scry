using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scry
{
    /// <summary>
    /// A copy drawn darker, or as it was again: every colour each of its materials' shaders take
    /// at a share of its own (<see cref="DimColour"/>), through a property block, so no material
    /// is copied or changed. A part whose shader takes no colour is drawn as it was. Rooms below
    /// a dungeon's opened floor are dimmed so.
    /// </summary>
    internal static class Dim
    {
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        private static readonly List<Renderer> Renderers = new List<Renderer>();

        /// <summary>The colours each shader takes, by property id, found once a shader.</summary>
        private static readonly Dictionary<Shader, int[]> ColoursOf = new Dictionary<Shader, int[]>();

        static Dim() => WorldCaches.Register(nameof(Dim), Forget);

        /// <summary>Lets go of the shaders of a world left, which the next may not have.</summary>
        private static void Forget() => ColoursOf.Clear();

        /// <summary>The share of their colours the self-test holds dimmed parts to; null for the dimming's own (<see cref="DimColour.Share"/>).</summary>
        private static float? _heldShare;

        /// <summary>Has parts dimmed from now on keep another share of their colours, none for black, for the self-test to film what no colour lights; null for the dimming's own.</summary>
        [Diagnostic, ForTest]
        public static void HoldShare(float? share) => _heldShare = share;

        /// <summary>Whether dimming darkens a material: its shader takes a colour.</summary>
        [ForTest]
        public static bool Reaches(Material material) => material != null && material.shader != null && Colours(material.shader).Length > 0;

        /// <summary>The share of its colours a room's copy keeps as it is dimmed: half on the level of the room gone into, less below.</summary>
        public static float ShareOf(RoomLook look) => look == RoomLook.Near ? DimColour.NearShare : DimColour.Share;

        /// <summary>Dims a copy to keep a share of its colours (<see cref="ShareOf"/>), or with none takes the dimming off.</summary>
        public static void Set(GameObject copy, float? keep)
        {
            if (copy == null) return;
            var dim = keep.HasValue;
            var share = _heldShare ?? keep ?? 1f;
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
                        if (material == null || material.shader == null) continue;
                        var colours = Colours(material.shader);
                        if (colours.Length == 0) continue;
                        Block.Clear();
                        foreach (var id in colours)
                        {
                            var own = material.GetColor(id);
                            var (r, g, b, a) = DimColour.Darker(own.r, own.g, own.b, own.a, share);
                            Block.SetColor(id, new Color(r, g, b, a));
                        }
                        renderer.SetPropertyBlock(Block, i);
                    }
                }
            }
            finally
            {
                Renderers.Clear();
            }
        }

        private static int[] Colours(Shader shader)
        {
            if (ColoursOf.TryGetValue(shader, out var known)) return known;
            var ids = new List<int>();
            for (var i = 0; i < shader.GetPropertyCount(); i++)
            {
                if (shader.GetPropertyType(i) == ShaderPropertyType.Color) ids.Add(shader.GetPropertyNameId(i));
            }
            return ColoursOf[shader] = ids.ToArray();
        }
    }
}
