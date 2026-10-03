using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Puts a copy in one of the looks its prefab offers (<see cref="PrefabLooks"/>), the way the
    /// game's own script switches it: a plant grown, a fire lit, a portal open, a door open, a
    /// machine at work, a creature in its gear or saddle, an item in a style.
    /// </summary>
    internal static class Variants
    {
        /// <summary>What to copy for a look: a grown plant is a prefab of its own.</summary>
        public static GameObject SourceFor(GameObject prefab, int look)
        {
            var found = PrefabLooks.Describe(prefab);
            if (found.Sort != PrefabLooks.Sort.Growth || look <= 0) return prefab;

            var plant = prefab.GetComponentInChildren<Plant>(true);
            var grown = plant != null && look - 1 < plant.m_grownPrefabs.Length ? plant.m_grownPrefabs[look - 1] : null;
            return grown != null ? grown : prefab;
        }

        /// <summary>Puts a copy in a look.</summary>
        public static void Apply(GameObject prefab, GameObject copy, int look)
        {
            if (prefab == null || copy == null) return;
            var found = PrefabLooks.Describe(prefab);

            switch (found.Sort)
            {
                case PrefabLooks.Sort.Fire:
                    Fire(prefab, copy, look);
                    break;
                case PrefabLooks.Sort.Portal:
                    Portal(prefab, copy, look);
                    break;
                case PrefabLooks.Sort.Door:
                    Door(prefab, copy, look);
                    break;
                case PrefabLooks.Sort.Chest:
                    var chest = prefab.GetComponentInChildren<Container>(true);
                    Show(prefab, copy, chest.m_open, look == 1);
                    Show(prefab, copy, chest.m_closed, look == 0);
                    break;
                case PrefabLooks.Sort.Windmill:
                    Windmill(prefab, copy, look == 1);
                    break;
                case PrefabLooks.Sort.Smelter:
                    Smelter(prefab, copy, look);
                    break;
                case PrefabLooks.Sort.Fermenter:
                    var fermenter = prefab.GetComponentInChildren<Fermenter>(true);
                    Show(prefab, copy, fermenter.m_topObject, look != 0);
                    Show(prefab, copy, fermenter.m_fermentingObject, look == 1);
                    Show(prefab, copy, fermenter.m_readyObject, look == 2);
                    break;
                case PrefabLooks.Sort.Sap:
                    var sap = prefab.GetComponentInChildren<SapCollector>(true);
                    Show(prefab, copy, sap.m_workingEffect, look == 1);
                    Show(prefab, copy, sap.m_notEmptyEffect, look != 0);
                    break;
                case PrefabLooks.Sort.Station:
                    Station(prefab, copy, look);
                    break;
                case PrefabLooks.Sort.Saddle:
                    var saddle = prefab.GetComponentInChildren<Tameable>(true).m_saddle;
                    Show(prefab, copy, saddle != null ? saddle.gameObject : null, look == 1);
                    break;
                case PrefabLooks.Sort.Picked:
                    var pickable = prefab.GetComponentInChildren<Pickable>(true);
                    var shown = Looks.Twin(prefab.transform, copy.transform, pickable.m_hideWhenPicked.transform);
                    if (shown != null) shown.gameObject.SetActive(look == 0);
                    break;
                case PrefabLooks.Sort.Gear:
                    if (look > 0) Gear.Dress(prefab, copy, look);
                    break;
                case PrefabLooks.Sort.Style:
                    Style(prefab, copy, look);
                    break;
            }
        }

        /// <summary>What a creature wears in a look; nothing when the look is not about gear.</summary>
        public static List<GameObject> GearOf(GameObject prefab, int look)
        {
            return PrefabLooks.Describe(prefab).Sort == PrefabLooks.Sort.Gear ? Gear.DressItems(prefab, look) : new List<GameObject>();
        }
        /// <summary>As <c>Fireplace.UpdateState</c> shows a fire by whether it burns and how brightly.</summary>
        private static void Fire(GameObject prefab, GameObject copy, int look)
        {
            var fire = prefab.GetComponentInChildren<Fireplace>(true);
            var hasLow = fire.m_enabledObjectLow != null && fire.m_enabledObjectHigh != null;
            var burning = look > 0;
            var low = hasLow && look == 1;

            void Set(GameObject part, bool on)
            {
                if (part == null) return;
                var twin = Looks.Twin(prefab.transform, copy.transform, part.transform);
                if (twin != null) twin.gameObject.SetActive(on);
            }

            Set(fire.m_enabledObject, burning);
            if (hasLow)
            {
                Set(fire.m_enabledObjectHigh, burning && !low);
                Set(fire.m_enabledObjectLow, burning && low);
            }
            Set(fire.m_fullObject, burning && !low);
            Set(fire.m_halfObject, low);
            Set(fire.m_emptyObject, !burning);
        }

        /// <summary>
        /// As <c>TeleportWorld</c> shows a portal: its runes glow in one colour until it is
        /// connected and in another after, and its swirl fades in while a player who may travel
        /// stands near a connected one.
        /// </summary>
        private static void Portal(GameObject prefab, GameObject copy, int look)
        {
            var portal = prefab.GetComponentInChildren<TeleportWorld>(true);
            if (portal.m_model != null)
            {
                // The runes' colour is set on a material of the model's own, which goes with the copy.
                var model = Looks.Twin(prefab.transform, copy.transform, portal.m_model.transform).OrNull()?.GetComponent<Renderer>();
                var material = model != null ? Owned.MaterialOf(model, copy) : null;
                if (material != null) material.SetColor("_EmissionColor", look == 0 ? portal.m_colorUnconnected : portal.m_colorTargetfound);
            }
            if (portal.m_target_found != null) Fade(prefab, copy, portal.m_target_found, look == 2);
        }

        /// <summary>A part of the prefab shown or hidden on the copy, as a script's <c>SetActive</c> does.</summary>
        private static void Show(GameObject prefab, GameObject copy, GameObject part, bool on)
        {
            if (part == null) return;
            var twin = Looks.Twin(prefab.transform, copy.transform, part.transform);
            if (twin != null) twin.gameObject.SetActive(on);
        }

        /// <summary>As <c>Door.SetState</c>: the animator's state (1 open, -1 open the other way) and the part shown while open.</summary>
        private static void Door(GameObject prefab, GameObject copy, int look)
        {
            var door = prefab.GetComponentInChildren<global::Door>(true);
            var state = look == 1 ? 1 : look == 2 ? -1 : 0;
            var animator = ClipPlayer.AnimatorOf(copy);
            if (animator != null) animator.SetInteger("state", state);
            Show(prefab, copy, door.m_openEnable, state != 0);
        }

        /// <summary>
        /// As <c>Windmill.Update</c> turns them, at full wind: the blades about their axis, the
        /// millstone about its own, and a smelter it drives at work.
        /// </summary>
        private static void Windmill(GameObject prefab, GameObject copy, bool turning)
        {
            var mill = prefab.GetComponentInChildren<global::Windmill>(true);
            if (mill.m_propellerAOE != null) Show(prefab, copy, mill.m_propellerAOE, false);
            if (prefab.GetComponentInChildren<global::Smelter>(true) != null) Smelter(prefab, copy, turning ? 2 : 1);
            if (!turning) return;

            var propeller = Looks.Twin(prefab.transform, copy.transform, mill.m_propeller);
            var stone = mill.m_grindstone != null ? Looks.Twin(prefab.transform, copy.transform, mill.m_grindstone) : null;
            var turn = copy.AddComponent<Turn>();
            turn.Propeller = propeller;
            turn.PropellerSpeed = mill.m_propellerRotationSpeed;
            turn.Stone = stone;
            turn.StoneSpeed = mill.m_grindstoneRotationSpeed;
        }

        /// <summary>
        /// As <c>Smelter.UpdateState</c> and <c>SetAnimation</c>: cold (empty, unlit), loaded
        /// (fuel and ore in, not burning) or working (burning, its bellows and wheels going).
        /// </summary>
        private static void Smelter(GameObject prefab, GameObject copy, int look)
        {
            var smelter = prefab.GetComponentInChildren<global::Smelter>(true);
            var working = look == 2;
            var loaded = look >= 1;
            Show(prefab, copy, smelter.m_enabledObject, working);
            Show(prefab, copy, smelter.m_disabledObject, !working);
            Show(prefab, copy, smelter.m_haveFuelObject, loaded);
            Show(prefab, copy, smelter.m_haveOreObject, loaded);
            Show(prefab, copy, smelter.m_noOreObject, !loaded);
            if (smelter.m_animators == null) return;
            foreach (var animator in smelter.m_animators)
            {
                var twin = animator != null ? Looks.Twin(prefab.transform, copy.transform, animator.transform).OrNull()?.GetComponent<Animator>() : null;
                if (twin == null) continue;
                twin.SetBool("active", working);
                twin.SetFloat("activef", working ? 1f : 0f);
            }
        }

        /// <summary>As <c>CraftingStation</c> shows it: its fire lit when one burns near, and its in-use part while someone crafts.</summary>
        private static void Station(GameObject prefab, GameObject copy, int look)
        {
            var station = prefab.GetComponentInChildren<CraftingStation>(true);
            var hasFire = station.m_haveFireObject != null;
            var inUse = look == (hasFire ? 2 : 1);
            Show(prefab, copy, station.m_inUseObject, inUse);
            if (hasFire) Show(prefab, copy, station.m_haveFireObject, look >= 1);
        }

        /// <summary>
        /// Puts every <c>EffectFade</c> part of a copy out, as the script does when it wakes: the
        /// game only fades them in when something calls for it, as a portal does.
        /// </summary>
        public static void FadeOut(GameObject prefab, GameObject copy)
        {
            foreach (var fade in prefab.GetComponentsInChildren<EffectFade>(true)) Fade(prefab, copy, fade, false);
        }

        /// <summary>An <c>EffectFade</c> part of the copy faded in or out: its particles, light and sound.</summary>
        private static void Fade(GameObject prefab, GameObject copy, EffectFade fade, bool on)
        {
            var twin = Looks.Twin(prefab.transform, copy.transform, fade.transform);
            if (twin == null) return;

            foreach (var particles in twin.GetComponentsInChildren<ParticleSystem>(true))
            {
                var emission = particles.emission;
                emission.enabled = on;
                if (on && !particles.isPlaying) particles.Play();
            }

            var light = twin.GetComponentInChildren<Light>(true);
            var original = fade.GetComponentInChildren<Light>(true);
            if (light != null)
            {
                light.enabled = on;
                if (original != null) light.intensity = original.intensity;
            }

            var source = twin.GetComponentInChildren<AudioSource>(true);
            var sound = fade.GetComponentInChildren<AudioSource>(true);
            if (source != null)
            {
                source.volume = on && sound != null ? sound.volume : 0f;
                if (on && source.loop && !source.isPlaying) source.Play();
            }
        }

        /// <summary>As <c>ItemStyle.Setup</c> picks an item's style through the game's material manager.</summary>
        private static void Style(GameObject prefab, GameObject copy, int look)
        {
            if (MaterialMan.instance == null) return;
            foreach (var style in prefab.GetComponentsInChildren<ItemStyle>(true))
            {
                var twin = Looks.Twin(prefab.transform, copy.transform, style.transform);
                if (twin != null) MaterialMan.instance.SetValue(twin.gameObject, ShaderProps._Style, look, true);
            }
        }
    }
}
