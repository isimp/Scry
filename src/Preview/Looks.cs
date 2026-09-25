using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The looks a prefab can take on that the game switches by script: a creature's star levels
    /// and a piece's wear. The scripts that do it are gone from the copy, so the settings are read
    /// from the untouched prefab and applied to the copy's matching parts, found by their place in
    /// the hierarchy, which the copy shares with the prefab.
    /// </summary>
    internal static class Looks
    {
        private static readonly Dictionary<string, Material> LevelMaterials = new Dictionary<string, Material>();

        /// <summary>
        /// A copy of an entry in the look its modifiers ask for: grown or not, its body, its
        /// level, its wear, and its fire, picked state, gear or style.
        /// </summary>
        public static GameObject Copy(Entry entry, Modifiers modifiers, Transform parent, Vector3 position, Quaternion rotation, int layer = -1)
        {
            if (!(entry?.Source is GameObject prefab)) return null;

            var source = Variants.SourceFor(prefab, modifiers.Look);
            var copy = Ghost.Make(source, parent, position, rotation, layer);
            if (copy == null) return null;

            Gear.Body(source, copy);
            if (entry.Kind == Kind.Creature) ApplyLevel(source, copy, modifiers.Level);
            if (modifiers.WearAvailable) ApplyWear(source, copy, modifiers.Wear);
            if (source == prefab) Variants.Apply(prefab, copy, modifiers.Look);
            return copy;
        }

        /// <summary>Gives a creature copy the look of a level, as <c>LevelEffects</c> would.</summary>
        public static void ApplyLevel(GameObject prefab, GameObject copy, int level)
        {
            if (level <= 1) return;

            var effects = prefab.GetComponentInChildren<LevelEffects>(true);
            if (effects == null || effects.m_levelSetups == null || effects.m_levelSetups.Count < level - 1) return;

            var setup = effects.m_levelSetups[level - 2];
            var target = Twin(prefab.transform, copy.transform, effects.transform);
            if (target != null) target.localScale = new Vector3(setup.m_scale, setup.m_scale, setup.m_scale);

            if (effects.m_mainRender != null)
            {
                var renderer = Twin(prefab.transform, copy.transform, effects.m_mainRender.transform)?.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterials = Tinted(prefab.name, level, effects.m_mainRender.sharedMaterials, setup);
            }

            if (effects.m_baseEnableObject != null)
            {
                var off = Twin(prefab.transform, copy.transform, effects.m_baseEnableObject.transform);
                if (off != null) off.gameObject.SetActive(false);
            }

            if (setup.m_enableObject != null)
            {
                var on = Twin(prefab.transform, copy.transform, setup.m_enableObject.transform);
                if (on != null) on.gameObject.SetActive(true);
            }
        }

        /// <summary>Shows a piece copy new, worn or broken, as <c>WearNTear</c> would.</summary>
        public static void ApplyWear(GameObject prefab, GameObject copy, Wear wear)
        {
            var source = prefab.GetComponentInChildren<WearNTear>(true);
            if (source == null) return;

            var shown = wear == Wear.Broken ? source.m_broken : wear == Wear.Worn ? source.m_worn : source.m_new;
            if (shown == null) shown = source.m_new;
            if (shown == null) return;

            foreach (var state in new[] { source.m_new, source.m_worn, source.m_broken })
            {
                if (state == null) continue;
                var twin = Twin(prefab.transform, copy.transform, state.transform);
                if (twin != null) twin.gameObject.SetActive(state == shown);
            }
        }

        /// <summary>The level's material, made once per creature and level and reused after.</summary>
        private static Material[] Tinted(string prefabName, int level, Material[] original, LevelEffects.LevelSetup setup)
        {
            var materials = (Material[])original.Clone();
            if (materials.Length == 0 || materials[0] == null) return materials;

            var key = prefabName + "#" + level;
            if (!LevelMaterials.TryGetValue(key, out var tinted) || tinted == null)
            {
                tinted = new Material(materials[0]);
                tinted.SetFloat("_Hue", setup.m_hue);
                tinted.SetFloat("_Saturation", setup.m_saturation);
                tinted.SetFloat("_Value", setup.m_value);
                if (setup.m_setEmissiveColor) tinted.SetColor("_EmissionColor", setup.m_emissiveColor);
                LevelMaterials[key] = tinted;
            }

            materials[0] = tinted;
            return materials;
        }

        /// <summary>The part of the copy at the same place as <paramref name="part"/> in the prefab.</summary>
        public static Transform Twin(Transform prefabRoot, Transform copyRoot, Transform part)
        {
            var path = new List<int>();
            var t = part;
            for (; t != null && t != prefabRoot; t = t.parent) path.Add(t.GetSiblingIndex());

            // A reference to something outside the prefab has no twin.
            if (t == null) return null;

            var result = copyRoot;
            for (var i = path.Count - 1; i >= 0; i--)
            {
                if (path[i] >= result.childCount) return null;
                result = result.GetChild(path[i]);
            }
            return result;
        }
    }
}
