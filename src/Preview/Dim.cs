using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A copy drawn darker, or as it was again, in the way in use (<see cref="DimWay"/>): each of
    /// its materials' tint and glow at a share of their own, or every colour its shaders take so,
    /// through a property block, so no material is copied or changed; nothing of its own where the
    /// stage's lights on it are made weaker; or one plain dark material in place of its own, which
    /// it gets back undimmed. A part whose shader takes no tint is drawn as it was by its tint.
    /// Rooms below a dungeon's opened floor are dimmed so.
    /// </summary>
    internal static class Dim
    {
        /// <summary>The share of its own brightness a dimmed part keeps.</summary>
        public const float Share = 0.3f;

        /// <summary>The colour of the plain dark material, near the stage's own backdrop.</summary>
        private static readonly Color PlainColour = new Color(0.16f, 0.16f, 0.18f, 1f);

        private static readonly int Tint = Shader.PropertyToID("_Color");
        private static readonly int Glow = Shader.PropertyToID("_EmissionColor");
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        private static readonly List<Renderer> Renderers = new List<Renderer>();

        /// <summary>Each renderer's own materials while it is drawn with the plain one.</summary>
        private static readonly Dictionary<Renderer, Material[]> OwnMaterials = new Dictionary<Renderer, Material[]>();

        private static Material _plain;

        static Dim() => WorldCaches.Register(nameof(Dim), Forget);

        /// <summary>Lets go of the materials kept for copies of a world left; the copies go with it.</summary>
        private static void Forget() => OwnMaterials.Clear();

        /// <summary>The way copies are dimmed from now on.</summary>
        public static DimWay Way { get; private set; } = DimWay.Tint;

        /// <summary>Dims copies another way from now on, for the self-test to compare the ways; those dimmed already keep theirs until taken off.</summary>
        [Diagnostic]
        public static void UseWay(DimWay way) => Way = way;

        /// <summary>Whether dimming by tint darkens a material: its shader takes a tint or a glow.</summary>
        public static bool Reaches(Material material) => material != null && (material.HasProperty(Tint) || material.HasProperty(Glow));

        /// <summary>Dims a copy, or takes the dimming off, whichever way it was dimmed.</summary>
        public static void Set(GameObject copy, bool dim)
        {
            if (copy == null) return;
            copy.GetComponentsInChildren(true, Renderers);
            try
            {
                foreach (var renderer in Renderers)
                {
                    if (renderer == null) continue;
                    if (!dim)
                    {
                        Undim(renderer);
                        continue;
                    }
                    switch (Way)
                    {
                        case DimWay.Tint:
                        case DimWay.Colours:
                            Darken(renderer, Way == DimWay.Colours);
                            break;
                        case DimWay.Plain:
                            MakePlain(renderer);
                            break;
                    }
                }
            }
            finally
            {
                Renderers.Clear();
            }
        }

        private static void Undim(Renderer renderer)
        {
            if (OwnMaterials.TryGetValue(renderer, out var own))
            {
                renderer.sharedMaterials = own;
                OwnMaterials.Remove(renderer);
            }
            var count = renderer.sharedMaterials.Length;
            for (var i = 0; i < count; i++) renderer.SetPropertyBlock(null, i);
        }

        /// <summary>Each material's tint and glow, or every colour its shader takes, at <see cref="Share"/> of its own, its alpha kept.</summary>
        private static void Darken(Renderer renderer, bool everyColour)
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null) continue;
                Block.Clear();
                var darkened = false;
                if (everyColour)
                {
                    var shader = material.shader;
                    for (var p = 0; shader != null && p < shader.GetPropertyCount(); p++)
                    {
                        if (shader.GetPropertyType(p) != UnityEngine.Rendering.ShaderPropertyType.Color) continue;
                        var id = shader.GetPropertyNameId(p);
                        Block.SetColor(id, Darker(material.GetColor(id)));
                        darkened = true;
                    }
                }
                else if (Reaches(material))
                {
                    if (material.HasProperty(Tint)) Block.SetColor(Tint, Darker(material.GetColor(Tint)));
                    if (material.HasProperty(Glow)) Block.SetColor(Glow, material.GetColor(Glow) * Share);
                    darkened = true;
                }
                if (darkened) renderer.SetPropertyBlock(Block, i);
            }
        }

        private static Color Darker(Color own) => new Color(own.r * Share, own.g * Share, own.b * Share, own.a);

        /// <summary>A mesh drawn with the plain dark material in place of each of its own; particles and lines are left as they are.</summary>
        private static void MakePlain(Renderer renderer)
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) return;
            if (OwnMaterials.ContainsKey(renderer)) return;
            var plain = Plain();
            if (plain == null) return;
            var own = renderer.sharedMaterials;
            OwnMaterials[renderer] = own;
            var swapped = new Material[own.Length];
            for (var i = 0; i < swapped.Length; i++) swapped[i] = plain;
            renderer.sharedMaterials = swapped;
        }

        private static Material Plain()
        {
            if (_plain != null) return _plain;
            var shader = Shader.Find("Standard");
            if (shader == null) return null;
            _plain = Kept.Add(new Material(shader) { name = "Scry dimmed", color = PlainColour });
            return _plain;
        }
    }
}
