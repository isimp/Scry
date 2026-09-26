using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Copies that fall under physics: a creature's ragdoll when it dies, and the parts a piece
    /// breaks into, each made as the game makes them.
    ///
    /// Their colliders sit on Scry's own layer, which is set to meet only the ground (terrain,
    /// rocks, buildings) and itself, so they land and tumble while players, creatures, arrows and
    /// every check the game makes pass through them. Nothing of them is networked or saved.
    /// </summary>
    internal static class Falling
    {
        /// <summary>The layers a falling copy lands on in the world.</summary>
        private static readonly string[] Ground = { "Default", "terrain", "static_solid", "piece" };

        private static int _readyLayer = -1;

        /// <summary>Sets the layer to meet only the ground and itself. Once is enough.</summary>
        public static bool Ready(int layer)
        {
            if (layer < 0) return false;
            if (_readyLayer == layer) return true;

            var ground = new HashSet<int> { layer };
            foreach (var name in Ground)
            {
                var index = LayerMask.NameToLayer(name);
                if (index >= 0) ground.Add(index);
            }
            for (var i = 0; i < 32; i++) Physics.IgnoreLayerCollision(layer, i, !ground.Contains(i));

            _readyLayer = layer;
            return true;
        }

        /// <summary>The ragdoll a creature's death list leaves behind, if it has one.</summary>
        public static global::Ragdoll RagdollIn(EffectList list)
        {
            if (list?.m_effectPrefabs == null) return null;
            foreach (var data in list.m_effectPrefabs)
            {
                if (data == null || !data.m_enabled || data.m_prefab == null) continue;
                var ragdoll = data.m_prefab.GetComponent<global::Ragdoll>();
                if (ragdoll != null) return ragdoll;
            }
            return null;
        }

        /// <summary>
        /// Whether this is the list the prefab plays when it is destroyed: a piece broken, a tree
        /// felled, a rock or log smashed. The prefab is gone from the world after it plays.
        /// </summary>
        public static bool IsDestroyedList(GameObject prefab, EffectList list)
        {
            if (prefab == null || list == null) return false;
            foreach (var component in prefab.GetComponents<Component>())
            {
                switch (component)
                {
                    case WearNTear piece when piece.m_destroyedEffect == list: return true;
                    case Destructible destructible when destructible.m_destroyedEffect == list: return true;
                    case MineRock rock when rock.m_destroyedEffect == list: return true;
                    case MineRock5 rock5 when rock5.m_destroyedEffect == list: return true;
                    case TreeBase tree when tree.m_destroyedEffect == list: return true;
                    case TreeLog log when log.m_destroyedEffect == list: return true;
                }
            }
            return false;
        }

        /// <summary>
        /// How long what a destroyed list leaves behind lasts: the longest timer of the debris it
        /// throws, or a few seconds when it throws none.
        /// </summary>
        public static float DebrisSeconds(EffectList list)
        {
            var longest = 0f;
            if (list?.m_effectPrefabs != null)
            {
                foreach (var data in list.m_effectPrefabs)
                {
                    if (data?.m_prefab == null || !Ghost.IsDebris(data.m_prefab)) continue;
                    foreach (var timer in data.m_prefab.GetComponentsInChildren<TimedDestruction>(true)) longest = Mathf.Max(longest, timer.m_timeout);
                    if (longest <= 0f) longest = 5f;
                }
            }
            return longest > 0f ? Mathf.Clamp(longest, 3f, 10f) : 4.5f;
        }

        /// <summary>
        /// A tree copy felled as <c>TreeBase.SpawnLog</c> fells it: its log, the size of the tree,
        /// tipped over from high up so it topples away from you, and its stump left standing.
        /// </summary>
        public static GameObject Fell(GameObject prefab, GameObject copy, Transform parent, int layer, int physicsLayer, Vector3 away)
        {
            var tree = prefab.GetComponent<TreeBase>();
            if (tree == null || tree.m_logPrefab == null) return null;

            var point = tree.m_logSpawnPoint != null ? Looks.Twin(prefab.transform, copy.transform, tree.m_logSpawnPoint) : null;
            if (point == null) point = copy.transform;
            var scale = copy.transform.lossyScale.x;

            var holder = new GameObject("Scry felled tree");
            holder.transform.SetParent(parent, false);

            var log = Ghost.Make(tree.m_logPrefab, holder.transform, point.position, point.rotation, layer, falling: true);
            if (log == null)
            {
                Object.Destroy(holder);
                return null;
            }
            log.transform.localScale = tree.m_logPrefab.transform.localScale * scale;
            if (layer < 0) Solidify(log, physicsLayer);

            var body = log.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.mass *= scale;
                body.ResetInertiaTensor();
                body.AddForceAtPosition(away * 0.2f * body.mass, log.transform.position + Vector3.up * 4f * scale, ForceMode.Impulse);
            }

            if (tree.m_stubPrefab != null)
            {
                var stump = Ghost.Make(tree.m_stubPrefab, holder.transform, copy.transform.position, copy.transform.rotation, layer);
                if (stump != null) stump.transform.localScale = tree.m_stubPrefab.transform.localScale * scale;
            }
            return holder;
        }

        /// <summary>
        /// A copy of debris an effect list throws (planks, splinters, stones), falling under its
        /// own physics where the list puts it.
        /// </summary>
        public static GameObject Debris(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, int layer, int physicsLayer)
        {
            var copy = Ghost.Make(prefab, parent, position, rotation, layer, falling: true);
            if (copy != null && layer < 0) Solidify(copy, physicsLayer);
            return copy;
        }

        /// <summary>
        /// Whether playing this list on the prefab breaks it apart: it is the list a piece, tree or
        /// rock plays when destroyed, and the game breaks that one into parts as well.
        /// </summary>
        public static bool Breaks(GameObject prefab, EffectList list)
        {
            if (prefab == null || list == null) return false;
            var piece = prefab.GetComponent<WearNTear>();
            if (piece != null && piece.m_autoCreateFragments && piece.m_destroyedEffect == list) return true;
            var destructible = prefab.GetComponent<Destructible>();
            return destructible != null && destructible.m_autoCreateFragments && destructible.m_destroyedEffect == list;
        }

        /// <summary>
        /// A copy of a creature's ragdoll where its copy stands, as <c>Character.OnDeath</c> leaves
        /// it: in the creature's colours for its level, wearing its armour but not what it held,
        /// knocked back a little as by the blow that killed it.
        /// </summary>
        public static GameObject Ragdoll(global::Ragdoll ragdoll, GameObject creature, GameObject copy, int level, float scale,
            IList<GameObject> gear, Transform parent, int layer, int physicsLayer)
        {
            var prefab = ragdoll.gameObject;
            var fallen = Ghost.Make(prefab, parent, copy.transform.position, copy.transform.rotation, layer, falling: true);
            if (fallen == null) return null;
            fallen.transform.localScale = prefab.transform.localScale * scale;

            Gear.Body(prefab, fallen);
            if (prefab.GetComponentInChildren<LevelEffects>(true) != null) Looks.ApplyLevel(prefab, fallen, level);
            else if (ragdoll.m_mainModel != null)
            {
                var main = Looks.Twin(prefab.transform, fallen.transform, ragdoll.m_mainModel.transform)?.GetComponent<Renderer>();
                if (main != null) Looks.Tint(creature, main, level);
            }

            var worn = new List<GameObject>();
            foreach (var item in gear)
            {
                var slot = Gear.SlotOf(item);
                if (slot != Slot.RightHand && slot != Slot.LeftHand && slot != Slot.BothHands) worn.Add(item);
            }
            if (worn.Count > 0) Gear.Wear(prefab, fallen, worn);

            if (layer < 0) Solidify(fallen, physicsLayer);

            var push = -copy.transform.forward * 2f;
            push.x *= ragdoll.m_velMultiplier;
            push.z *= ragdoll.m_velMultiplier;
            push.y = 1.5f;
            foreach (var body in fallen.GetComponentsInChildren<Rigidbody>())
            {
                body.linearVelocity = push * Random.value;
            }
            return fallen;
        }

        /// <summary>
        /// The parts a piece copy breaks into, as <c>Destructible.CreateFragments</c> makes them:
        /// each of its meshes on its own, a little smaller, flung out from the middle, gone after
        /// two to four seconds. A piece made to break along set lines breaks along them.
        /// </summary>
        public static GameObject Break(GameObject prefab, GameObject copy, Transform parent, int layer, int physicsLayer)
        {
            var roots = new List<Transform>();
            var piece = prefab.GetComponent<WearNTear>();
            if (piece?.m_fragmentRoots != null)
            {
                foreach (var root in piece.m_fragmentRoots)
                {
                    var twin = root != null ? Looks.Twin(prefab.transform, copy.transform, root.transform) : null;
                    if (twin == null) continue;
                    twin.gameObject.SetActive(true);
                    roots.Add(twin);
                }
            }
            if (roots.Count == 0) roots.Add(copy.transform);

            var holder = new GameObject("Scry pieces");
            holder.transform.SetParent(parent, false);

            var bodies = new List<Rigidbody>();
            foreach (var root in roots)
            {
                var skipped = LowerDetail(root);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.enabled || skipped.Contains(renderer)) continue;
                    var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    bodies.Add(Fragment(renderer, mesh, holder.transform, layer >= 0 ? layer : physicsLayer, layer >= 0 ? layer : renderer.gameObject.layer));
                }
            }

            foreach (var root in roots) if (root != copy.transform) root.gameObject.SetActive(false);

            if (bodies.Count == 0)
            {
                Object.Destroy(holder);
                return null;
            }

            var middle = Vector3.zero;
            foreach (var body in bodies) middle += body.worldCenterOfMass;
            middle /= bodies.Count;
            foreach (var body in bodies)
            {
                var force = (body.worldCenterOfMass - middle).normalized * 4f + Random.onUnitSphere;
                body.AddForce(force, ForceMode.VelocityChange);
                Object.Destroy(body.gameObject, Random.Range(2f, 4f));
            }
            return holder;
        }

        private static Rigidbody Fragment(MeshRenderer source, Mesh mesh, Transform holder, int physicsLayer, int drawLayer)
        {
            var part = new GameObject("Scry piece");
            part.layer = physicsLayer;
            part.transform.SetParent(holder, true);
            part.transform.position = source.transform.position;
            part.transform.rotation = source.transform.rotation;
            part.transform.localScale = source.transform.lossyScale * 0.9f;

            var body = part.AddComponent<Rigidbody>();
            var box = part.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center;
            box.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.02f);

            // Drawn by a child, so in the world the look is on a layer the camera sees while the
            // collider stays on the layer that only meets the ground.
            var look = new GameObject("look");
            look.layer = drawLayer;
            look.transform.SetParent(part.transform, false);
            look.AddComponent<MeshFilter>().sharedMesh = mesh;
            look.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
            if (MaterialMan.instance != null)
            {
                MaterialMan.instance.SetValue(look, ShaderProps._RippleDistance, 0f);
                MaterialMan.instance.SetValue(look, ShaderProps._ValueNoise, 0f);
            }
            return body;
        }

        /// <summary>The renderers of the less detailed versions of a model, which would break into doubles.</summary>
        private static HashSet<Renderer> LowerDetail(Transform root)
        {
            var skipped = new HashSet<Renderer>();
            foreach (var group in root.GetComponentsInChildren<LODGroup>())
            {
                var lods = group.GetLODs();
                for (var i = 1; i < lods.Length; i++)
                {
                    foreach (var renderer in lods[i].renderers) if (renderer != null) skipped.Add(renderer);
                }
            }
            return skipped;
        }

        /// <summary>
        /// Puts a world copy's colliders on the layer that only meets the ground. A collider that
        /// shares its object with something drawn moves to a child of its own, so the drawing
        /// stays on a layer the camera sees.
        /// </summary>
        private static void Solidify(GameObject copy, int layer)
        {
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
            {
                var owner = collider.gameObject;
                if (owner.GetComponent<Renderer>() == null)
                {
                    owner.layer = layer;
                    continue;
                }

                var holder = new GameObject("Scry collider") { layer = layer };
                holder.transform.SetParent(owner.transform, false);
                if (Clone(collider, holder)) Object.Destroy(collider);
                else Object.Destroy(holder);
            }
        }

        private static bool Clone(Collider from, GameObject to)
        {
            switch (from)
            {
                case BoxCollider box:
                    var b = to.AddComponent<BoxCollider>();
                    b.center = box.center;
                    b.size = box.size;
                    return true;
                case SphereCollider sphere:
                    var s = to.AddComponent<SphereCollider>();
                    s.center = sphere.center;
                    s.radius = sphere.radius;
                    return true;
                case CapsuleCollider capsule:
                    var c = to.AddComponent<CapsuleCollider>();
                    c.center = capsule.center;
                    c.radius = capsule.radius;
                    c.height = capsule.height;
                    c.direction = capsule.direction;
                    return true;
                case MeshCollider mesh:
                    var m = to.AddComponent<MeshCollider>();
                    m.sharedMesh = mesh.sharedMesh;
                    m.convex = mesh.convex;
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Stands in for a copy while it lies fallen or broken: the copy is not drawn until the
    /// stand-in goes, by its own time running out or by being cleared, and then it is again.
    /// A ragdoll's parting effect plays where it lay, as the game plays it.
    /// </summary>
    internal sealed class Standin : MonoBehaviour
    {
        private static readonly HashSet<GameObject> Down = new HashSet<GameObject>();

        private readonly List<Renderer> _hidden = new List<Renderer>();
        private GameObject _copy;
        private float _until;
        private EffectList _parting;
        private bool _onStage;

        public static Standin For(GameObject fallen, GameObject copy, float seconds, EffectList parting, bool onStage)
        {
            var standin = fallen.AddComponent<Standin>();
            standin._copy = copy;
            Down.Add(copy);
            All.Add(standin);
            standin._until = Time.unscaledTime + seconds;
            standin._parting = parting;
            standin._onStage = onStage;
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                renderer.enabled = false;
                standin._hidden.Add(renderer);
            }
            return standin;
        }

        private void Update()
        {
            if (Time.unscaledTime < _until) return;

            if (_parting != null)
            {
                var body = GetComponentInChildren<Rigidbody>();
                var at = body != null ? body.transform : transform;
                if (_onStage) Stage.PlayList(_parting, at);
                else Previews.PlayList(_parting, at.position, Quaternion.identity);
            }
            Destroy(gameObject);
        }

        private static readonly List<Standin> All = new List<Standin>();

        /// <summary>
        /// Takes away whatever stands in for a copy, which then stands again: when the copy is
        /// moved, made again or taken away, its ragdoll or parts should not be left lying about.
        /// </summary>
        public static void ClearFor(GameObject copy)
        {
            if (copy == null) return;
            foreach (var standin in All.ToArray())
            {
                if (standin != null && standin._copy == copy) Destroy(standin.gameObject);
            }
        }

        /// <summary>Whether a copy is lying fallen or broken right now, so it cannot fall again until it stands.</summary>
        public static bool IsDown(GameObject copy)
        {
            Down.RemoveWhere(c => c == null);
            return copy != null && Down.Contains(copy);
        }

        private void OnDestroy()
        {
            All.Remove(this);
            Down.Remove(_copy);
            foreach (var renderer in _hidden) if (renderer != null) renderer.enabled = true;
            _hidden.Clear();
        }
    }
}
