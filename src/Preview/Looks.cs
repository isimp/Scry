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

        /// <summary>What went wrong dressing a copy, each told once.</summary>
        private static readonly HashSet<string> Failed = new HashSet<string>();

        /// <summary>
        /// A copy of an entry in the look its modifiers ask for: grown or not, its body, its
        /// level, its wear, and its fire, picked state, gear or style. With <paramref name="timing"/>,
        /// making and dressing it are added to <see cref="Timing"/> as its "copy" and "dress" parts.
        /// </summary>
        public static GameObject Copy(Entry entry, Modifiers modifiers, Transform parent, Vector3 position, Quaternion rotation, int layer = -1, string timing = null)
        {
            if (entry?.Source is StatusEffect effect) return Affected(effect, parent, position, rotation, layer, timing);
            if (!(entry?.Source is GameObject prefab)) return null;

            if (IsWorn(entry)) return Worn(prefab, modifiers, parent, position, rotation, layer, timing);

            var started = Timing.Start();
            var source = Variants.SourceFor(prefab, modifiers.Look);
            var copy = Ghost.Make(source, parent, position, rotation, layer);
            if (timing != null) Timing.Add(timing + " copy", started);
            if (copy == null) return null;

            // The copy is already in the scene, so whatever goes wrong dressing it, it is handed
            // back to be kept track of and cleared like any other.
            started = Timing.Start();
            Step(prefab, "its body", () => Gear.Body(source, copy));
            Step(prefab, "its effects faded out", () => Variants.FadeOut(source, copy));
            if (entry.Kind == Kind.Creature) Step(prefab, "its level", () => ApplyLevel(source, copy, modifiers.Level));
            if (modifiers.WearAvailable) Step(prefab, "its wear", () => ApplyWear(source, copy, modifiers.Wear));
            if (source == prefab) Step(prefab, "its look", () => Variants.Apply(prefab, copy, modifiers.Look));
            Step(prefab, "its animation events", () => AnimationEars.Attach(source, copy));
            if (timing != null) Timing.Add(timing + " dress", started);
            return copy;
        }

        /// <summary>
        /// One step of dressing a copy. A step that fails, as one can on a mod's prefab laid out
        /// in a way it does not expect, is left out, and each distinct failure is told once.
        /// </summary>
        private static void Step(GameObject prefab, string what, System.Action step)
        {
            var started = Timing.Start();
            try
            {
                step();
                if (Plugin.LogPreviews) Timing.Add("dress " + what, started);
            }
            catch (System.Exception ex)
            {
                if (Failed.Add(prefab.name + "|" + what + "|" + ex.GetType().Name + "|" + ex.Message))
                {
                    Plugin.Log.LogWarning($"Scry shows its copy of {prefab.name} without {what}, which failed: {ex}");
                }
            }
        }

        /// <summary>Show wearable items worn by a person rather than on their own.</summary>
        public static bool OnPerson;

        /// <summary>What the person keeps on while other items are tried on.</summary>
        public static readonly Outfit Outfit = new Outfit();

        /// <summary>Whether this entry is shown worn by a person right now.</summary>
        public static bool IsWorn(Entry entry)
        {
            return OnPerson && entry != null && entry.Kind == Kind.Item && entry.Source is GameObject prefab && Gear.IsWearable(prefab);
        }

        /// <summary>A person, the game's own player model, wearing the item in its chosen style.</summary>
        private static GameObject Worn(GameObject item, Modifiers modifiers, Transform parent, Vector3 position, Quaternion rotation, int layer, string timing)
        {
            var person = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Player") : null;
            if (person == null) return null;

            var started = Timing.Start();
            var copy = Ghost.Make(person, parent, position, rotation, layer);
            if (timing != null) Timing.Add(timing + " copy", started);
            if (copy == null) return null;

            started = Timing.Start();
            Step(item, "the person's body", () => Gear.Body(person, copy));
            Step(item, "being worn", () => Gear.Wear(person, copy, WornWith(item), modifiers.LookAvailable ? modifiers.Look : -1, item));
            Step(item, "the person's animation events", () => AnimationEars.Attach(person, copy));
            if (timing != null) Timing.Add(timing + " dress", started);
            return copy;
        }

        /// <summary>Whether a status effect shows on a person: whether its start effects have anything to see or hear.</summary>
        public static bool ShowsOnPerson(Entry entry)
        {
            if (!(entry?.Source is StatusEffect effect) || effect.m_startEffects?.m_effectPrefabs == null) return false;
            foreach (var data in effect.m_startEffects.m_effectPrefabs)
            {
                if (data != null && data.m_enabled && data.m_prefab != null && !Ghost.IsWholeModel(data.m_prefab)) return true;
            }
            return false;
        }

        /// <summary>
        /// A person with a status effect's look on it: its start effects put where the game puts
        /// them on a character (<c>EffectList.Create</c> with the character as base: on the named
        /// part, following it when attached), as "Show it on you" puts them on you. Only the look;
        /// the effect itself is not there.
        /// </summary>
        private static GameObject Affected(StatusEffect effect, Transform parent, Vector3 position, Quaternion rotation, int layer, string timing)
        {
            var person = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Player") : null;
            if (person == null) return null;

            var started = Timing.Start();
            var copy = Ghost.Make(person, parent, position, rotation, layer);
            if (timing != null) Timing.Add(timing + " copy", started);
            if (copy == null) return null;

            started = Timing.Start();
            Step(person, "the person's body", () => Gear.Body(person, copy));
            Step(person, "the person's animation events", () => AnimationEars.Attach(person, copy));
            Step(person, "the look of " + effect.name, () =>
            {
                foreach (var data in effect.m_startEffects.m_effectPrefabs)
                {
                    if (data == null || !data.m_enabled || data.m_prefab == null || Ghost.IsWholeModel(data.m_prefab)) continue;
                    var anchor = copy.transform;
                    if (!string.IsNullOrEmpty(data.m_childTransform))
                    {
                        var child = Utils.FindChild(anchor, data.m_childTransform);
                        if (child != null) anchor = child;
                    }
                    Ghost.Make(data.m_prefab, anchor, anchor.position, anchor.rotation, layer);
                }
            });
            if (timing != null) Timing.Add(timing + " dress", started);
            return copy;
        }

        /// <summary>What the person wears when trying an item on: the item and what it keeps on, the item first.</summary>
        public static List<GameObject> WornWith(GameObject item)
        {
            var items = new List<GameObject>();
            foreach (var name in Outfit.With(item.name, Gear.SlotOf(item)))
            {
                var worn = name == item.name ? item : Prefab(name);
                if (worn != null) items.Add(worn);
            }
            items.Sort((a, b) => (b == item).CompareTo(a == item));
            return items;
        }

        /// <summary>An item prefab by name.</summary>
        public static GameObject Prefab(string name)
        {
            var item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(name) : null;
            if (item == null && ZNetScene.instance != null) item = ZNetScene.instance.GetPrefab(name);
            return item;
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

        /// <summary>
        /// Gives a renderer the colours of a creature's level, as <c>Ragdoll.Setup</c> gives its
        /// body the colours of the creature that died.
        /// </summary>
        public static void Tint(GameObject creature, Renderer renderer, int level)
        {
            if (level <= 1) return;
            var effects = creature.GetComponentInChildren<LevelEffects>(true);
            if (effects == null || effects.m_levelSetups == null || effects.m_levelSetups.Count < level - 1) return;
            renderer.sharedMaterials = Tinted(creature.name + " fallen", level, renderer.sharedMaterials, effects.m_levelSetups[level - 2]);
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

        /// <summary>
        /// Destroys the levels' materials and forgets what failed, for a world that was left.
        /// Called once the copies that draw with them are gone.
        /// </summary>
        public static void Forget()
        {
            foreach (var material in LevelMaterials.Values) if (material != null) Object.Destroy(material);
            LevelMaterials.Clear();
            Failed.Clear();
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
