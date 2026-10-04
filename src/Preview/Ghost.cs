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
    /// A copy too large to make at once is made over several frames (<see cref="Building"/>).
    /// </summary>
    internal static partial class Ghost
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static Ghost() => WorldCaches.Register(nameof(Ghost), Forget);

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
                // One that cannot be copied, as a mod's odd prefab may be, shows nothing; what was made of it goes.
                if (Guard.Each(Feature.Previews, "previews", prefab.name, () =>
                {
                    // A large prefab (the person, with hundreds of parts) is stripped once and kept
                    // stripped under the sleeping holder; later copies are made from that.
                    var key = new KeyValuePair<GameObject, bool>(prefab, falling);
                    if (Templates.TryGetValue(key, out var template) && template != null)
                    {
                        copy = Object.Instantiate(template, Holder().transform, false);
                        Used.Remove(key);
                        Used.Add(key);
                    }
                    else
                    {
                        copy = Object.Instantiate(prefab, Holder().transform, false);
                        if (Strip(copy, falling) >= TemplateFrom) KeepTemplate(key, copy);
                    }
                    copy.name = prefab.name;

                    var limit = falling ? StripPolicy.PushApartLimit(ScriptNames(prefab)) : null;
                    Settle(copy, falling);
                    var body = limit != null ? copy.GetComponent<Rigidbody>() : null;
                    if (body != null) body.maxDepenetrationVelocity = limit.Value;
                    if (layer >= 0) SetLayer(copy.transform, layer);
                    // A falling copy strikes the ground as its prefab does, heard and seen (Thud).
                    if (falling) Thud.Add(prefab, copy, onStage: layer >= 0);

                    // Its sounds at the loudness the player chose; one that cannot take it plays as the game would.
                    Guard.Run(Feature.PreviewsHeardAsIfBesideYou, "preview loudness", Loudness.Add, copy);
                    Place(copy, parent, position, rotation);
                    Awake(prefab, copy);

                    // Made under a holder that outlives worlds; one standing on its own belongs to the
                    // world, and goes with it.
                    if (parent == null) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                    return copy;
                }, out var made)) return made;
                if (copy != null) Object.Destroy(copy);
                return null;
            }
            finally
            {
                ZNetView.m_forceDisableInit = was;
            }
        }

        /// <summary>
        /// Poses the copy while it still sleeps, then wakes it where it stands. A body wakes where
        /// its copy is at that moment, and one the game interpolates (a log, whose parts sit some
        /// 50 m from its root) does not follow a move made after. A pose given in the parent's own
        /// space (<paramref name="local"/>) is kept as it is.
        /// </summary>
        private static void Place(GameObject copy, Transform parent, Vector3 position, Quaternion rotation, bool local = false)
        {
            var t = copy.transform;
            if (parent != null && !local)
            {
                t.localPosition = parent.InverseTransformPoint(position);
                t.localRotation = Quaternion.Inverse(parent.rotation) * rotation;
            }
            else
            {
                t.localPosition = position;
                t.localRotation = rotation;
            }
            t.SetParent(parent, false);
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

        /// <summary>Lets go of what was found out about the prefabs of a world that was left.</summary>
        public static void Forget()
        {
            Building.Flush();
            foreach (var template in Templates.Values) if (template != null) Object.Destroy(template);
            Templates.Clear();
            Used.Clear();
        }

        /// <summary>Stripped copies kept to copy again, by prefab and way of copying, and the order they were last used in.</summary>
        private static readonly Dictionary<KeyValuePair<GameObject, bool>, GameObject> Templates = new Dictionary<KeyValuePair<GameObject, bool>, GameObject>();
        private static readonly List<KeyValuePair<GameObject, bool>> Used = new List<KeyValuePair<GameObject, bool>>();

        /// <summary>How many parts a prefab has before its stripped copy is kept, and how many are kept at most.</summary>
        private const int TemplateFrom = 150;
        private const int TemplatesKept = 4;

        /// <summary>Keeps a stripped copy of a large prefab asleep under the holder, the one used longest ago going when too many are.</summary>
        private static void KeepTemplate(KeyValuePair<GameObject, bool> key, GameObject stripped)
        {
            var template = Object.Instantiate(stripped, Holder().transform, false);
            template.name = stripped.name + " (Scry template)";
            Templates[key] = template;
            Used.Remove(key);
            Used.Add(key);
            while (Used.Count > TemplatesKept)
            {
                var oldest = Used[0];
                Used.RemoveAt(0);
                if (Templates.TryGetValue(oldest, out var gone) && gone != null) Object.Destroy(gone);
                Templates.Remove(oldest);
            }
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

        /// <summary>Takes off everything the policy does not keep (<see cref="Stripping"/>); how many components the copy had.</summary>
        private static int Strip(GameObject copy, bool falling)
        {
            var stripping = new Stripping(copy, falling, keepColliders: false);
            stripping.Go(null, double.PositiveInfinity);
            return stripping.Parts;
        }

        /// <summary>Whether another component on the same object requires this one, so it can only go after that one.</summary>
        private static bool IsRequired(Component component)
        {
            var type = component.GetType();
            component.GetComponents(Siblings);
            try
            {
                foreach (var other in Siblings)
                {
                    if (other == null || other == component) continue;
                    foreach (var required in Requires(other.GetType()))
                    {
                        if (required != null && required.IsAssignableFrom(type)) return true;
                    }
                }
                return false;
            }
            finally
            {
                Siblings.Clear();
            }
        }

        private static readonly List<Component> Siblings = new List<Component>();

        /// <summary>What the policy says of a component, worked out once for each type and way of copying.</summary>
        private static int PassFor(Component component, bool falling)
        {
            var type = component.GetType();
            var key = new KeyValuePair<Type, bool>(type, falling);
            if (!PassByType.TryGetValue(key, out var pass))
            {
                pass = StripPolicy.PassFor(new ComponentFacts(type.FullName, component is MonoBehaviour, component is Joint), falling);
                PassByType[key] = pass;
            }
            return pass;
        }

        private static readonly Dictionary<KeyValuePair<Type, bool>, int> PassByType = new Dictionary<KeyValuePair<Type, bool>, int>();

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

        /// <summary>The type names of the scripts on a prefab's root, where the game keeps the ones that set up its body.</summary>
        private static IEnumerable<string> ScriptNames(GameObject prefab)
        {
            foreach (var script in prefab.GetComponents<MonoBehaviour>())
            {
                if (script != null) yield return script.GetType().Name;
            }
        }

        /// <summary>
        /// A person starts standing, as the game starts one that has no place in the world (the
        /// one on the character screen): <c>Player.SetupAwake</c> switches its animator's
        /// "wakeup" off then. Only a player waking in the world gets up from sitting first.
        /// Switched once the copy is awake, before its animator first moves.
        /// </summary>
        private static void Awake(GameObject prefab, GameObject copy)
        {
            if (prefab.GetComponent<Player>() == null) return;
            foreach (var animator in copy.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) continue;
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.name == "wakeup" && parameter.type == AnimatorControllerParameterType.Bool) animator.SetBool("wakeup", false);
                }
            }
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
