using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>
    /// A copy of a prefab that can only be looked at.
    ///
    /// The copy is made under an inactive holder, so none of its scripts wake up. While it is
    /// still asleep everything that could be hit, picked up, fought, interacted with or saved is
    /// taken off it (<see cref="StripPolicy"/>), and only then is it moved to where it is shown
    /// and woken. It has no network view, so no other player sees it and nothing of it is saved.
    /// </summary>
    internal static class Ghost
    {
        private static GameObject _holder;
        private static readonly Dictionary<Type, Type[]> RequiredByType = new Dictionary<Type, Type[]>();

        /// <summary>
        /// Makes the copy, or returns null if the prefab could not be copied. A falling copy keeps
        /// its bodies, colliders and joints, for <see cref="Falling"/> to put where they only meet
        /// the ground.
        /// </summary>
        public static GameObject Make(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, int layer = -1, bool falling = false)
        {
            if (prefab == null) return null;

            // A network view left on the copy for any reason destroys itself on waking.
            var was = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            GameObject copy = null;
            try
            {
                copy = Object.Instantiate(prefab, Holder().transform, false);
                copy.name = prefab.name;

                Strip(copy, falling);
                Settle(copy, falling);
                if (layer >= 0) SetLayer(copy.transform, layer);

                var t = copy.transform;
                t.SetParent(parent, false);
                t.position = position;
                t.rotation = rotation;
                return copy;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Scry could not make a preview of {prefab.name}: {ex.Message}");
                if (copy != null) Object.Destroy(copy);
                return null;
            }
            finally
            {
                ZNetView.m_forceDisableInit = was;
            }
        }

        /// <summary>
        /// Makes a copy so many times its size, as what a creature shown bigger or smaller plays
        /// is shown with it: its particles take the size of everything above them (a particle
        /// system otherwise heeds only its own scale), and its trails and lights reach as far again.
        /// </summary>
        public static void Magnify(GameObject copy, float times)
        {
            if (copy == null || Mathf.Abs(times - 1f) < 0.01f) return;
            copy.transform.localScale *= times;
            foreach (var particles in copy.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            foreach (var trail in copy.GetComponentsInChildren<TrailRenderer>(true)) trail.widthMultiplier *= times;
            foreach (var line in copy.GetComponentsInChildren<LineRenderer>(true)) line.widthMultiplier *= times;
            foreach (var light in copy.GetComponentsInChildren<Light>(true)) light.range *= times;
        }

        /// <summary>
        /// Makes the copy and hangs it on a parent keeping its size in the world, as the game
        /// hangs effects and held items on bones. Bones are often scaled, a creature's by a
        /// hundred, so a copy that kept its own scale under one would be drawn that much bigger.
        /// </summary>
        public static GameObject MakeOn(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, int layer = -1)
        {
            var copy = Make(prefab, null, position, rotation, layer);
            if (copy != null && parent != null) copy.transform.SetParent(parent, true);
            return copy;
        }

        /// <summary>
        /// Whether a prefab an effect list points at is a model rather than an effect: a ragdoll
        /// that takes a creature's place when it dies, a creature it splits into, an item, a
        /// skinned body, or pieces that fly apart under physics. A copy keeps none of its physics,
        /// so such a thing would only stand frozen beside the preview, and effects leave it out.
        /// </summary>
        public static bool IsWholeModel(GameObject prefab)
        {
            if (prefab == null) return false;
            if (!Known.TryGetValue(prefab, out var model))
            {
                model = prefab.GetComponentInChildren<Ragdoll>(true) != null
                        || prefab.GetComponentInChildren<Character>(true) != null
                        || prefab.GetComponent<ItemDrop>() != null
                        || prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null
                        || HasFreeBody(prefab);
                Known[prefab] = model;
            }
            return model;
        }

        private static readonly Dictionary<GameObject, bool> Known = new Dictionary<GameObject, bool>();

        /// <summary>
        /// Whether a prefab an effect list points at is debris: loose parts that fly apart under
        /// physics (planks, splinters, stones), which a falling copy can show as the game does.
        /// </summary>
        public static bool IsDebris(GameObject prefab)
        {
            return prefab != null
                   && HasFreeBody(prefab)
                   && prefab.GetComponentInChildren<Ragdoll>(true) == null
                   && prefab.GetComponentInChildren<Character>(true) == null
                   && prefab.GetComponent<ItemDrop>() == null;
        }

        private static bool HasFreeBody(GameObject prefab)
        {
            foreach (var body in prefab.GetComponentsInChildren<Rigidbody>(true))
            {
                if (!body.isKinematic) return true;
            }
            return false;
        }

        public static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (var i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        private static GameObject Holder()
        {
            if (_holder != null) return _holder;

            _holder = new GameObject("Scry holder");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);
            return _holder;
        }

        /// <summary>
        /// Takes off everything the policy does not keep. A component another one requires can
        /// only go after that one, so removal repeats until nothing more can be taken off.
        /// </summary>
        private static void Strip(GameObject copy, bool falling)
        {
            var all = copy.GetComponentsInChildren<Component>(true);
            var remaining = new List<Component>(all.Length);
            var doomed = new List<KeyValuePair<int, Component>>();

            foreach (var component in all)
            {
                if (component == null) continue;
                remaining.Add(component);

                var facts = new ComponentFacts(component.GetType().FullName, component is MonoBehaviour, component is Joint);
                var pass = StripPolicy.PassFor(facts, falling);
                if (pass != StripPolicy.Keep) doomed.Add(new KeyValuePair<int, Component>(pass, component));
            }

            doomed.Sort((a, b) => a.Key.CompareTo(b.Key));

            var progress = true;
            while (doomed.Count > 0 && progress)
            {
                progress = false;
                for (var i = 0; i < doomed.Count; i++)
                {
                    var component = doomed[i].Value;
                    if (IsRequired(component, remaining)) continue;

                    remaining.Remove(component);
                    Object.DestroyImmediate(component);
                    doomed.RemoveAt(i);
                    i--;
                    progress = true;
                }
            }

            if (doomed.Count > 0)
            {
                Plugin.Log.LogDebug($"Scry left {doomed.Count} part(s) on the preview of {copy.name} that something else needs.");
            }
        }

        private static bool IsRequired(Component component, List<Component> remaining)
        {
            var type = component.GetType();
            var owner = component.gameObject;

            foreach (var other in remaining)
            {
                if (other == component || other.gameObject != owner) continue;
                foreach (var required in Requires(other.GetType()))
                {
                    if (required != null && required.IsAssignableFrom(type)) return true;
                }
            }
            return false;
        }

        private static Type[] Requires(Type type)
        {
            if (RequiredByType.TryGetValue(type, out var known)) return known;

            var list = new List<Type>();
            foreach (RequireComponent attribute in type.GetCustomAttributes(typeof(RequireComponent), true))
            {
                list.Add(attribute.m_Type0);
                list.Add(attribute.m_Type1);
                list.Add(attribute.m_Type2);
            }

            known = list.ToArray();
            RequiredByType[type] = known;
            return known;
        }

        /// <summary>
        /// Keeps what stayed from wandering off or complaining. Animations play in place, and the
        /// events they would send to the scripts just removed are not sent. The copy is drawn
        /// wherever it stands, since the preview stage is far from the player. A falling copy is
        /// moved by physics alone.
        /// </summary>
        private static void Settle(GameObject copy, bool falling)
        {
            foreach (var animator in copy.GetComponentsInChildren<Animator>(true))
            {
                if (falling) animator.enabled = false;
                animator.applyRootMotion = false;
                animator.fireEvents = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            foreach (var skinned in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skinned.updateWhenOffscreen = true;
            }
        }
    }
}
