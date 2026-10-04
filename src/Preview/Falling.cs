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
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static Falling() => WorldCaches.Register(nameof(Falling), Release);

        /// <summary>The layers a falling copy lands on in the world.</summary>
        private static readonly string[] Ground = { "Default", "terrain", "static_solid", "piece" };

        private static int _readyLayer = -1;

        /// <summary>Which layers the stage's layer ignored before Scry set it, to give back.</summary>
        private static readonly bool[] Before = new bool[32];

        /// <summary>
        /// Sets the layer to meet only the ground and itself. Once is enough, until the world is
        /// left, when it is given back as it was (<see cref="Release"/>): it is a layer the game
        /// does not name, and another mod may take it up too.
        /// </summary>
        public static bool Ready(int layer)
        {
            if (layer < 0) return false;
            if (_readyLayer == layer) return true;
            Release();

            var ground = new HashSet<int> { layer };
            foreach (var name in Ground)
            {
                var index = LayerMask.NameToLayer(name);
                if (index >= 0) ground.Add(index);
            }
            for (var i = 0; i < 32; i++)
            {
                Before[i] = Physics.GetIgnoreLayerCollision(layer, i);
                Physics.IgnoreLayerCollision(layer, i, !ground.Contains(i));
            }

            _readyLayer = layer;
            return true;
        }

        /// <summary>Gives the layer's collisions back as they were before Scry set them.</summary>
        public static void Release()
        {
            if (_readyLayer < 0) return;
            for (var i = 0; i < 32; i++) Physics.IgnoreLayerCollision(_readyLayer, i, Before[i]);
            _readyLayer = -1;
        }

        /// <summary>The ragdoll a creature's death list leaves behind, if it has one.</summary>
        public static global::Ragdoll RagdollIn(EffectList list)
        {
            foreach (var slot in EffectSlots.Of(list))
            {
                if (!EffectSlots.Plays(slot)) continue;
                var ragdoll = slot.m_prefab.GetComponent<global::Ragdoll>();
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
            foreach (var slot in EffectSlots.Of(list))
            {
                if (!EffectSlots.Plays(slot) || !PrefabShapes.IsDebris(slot.m_prefab)) continue;
                foreach (var timer in slot.m_prefab.GetComponentsInChildren<TimedDestruction>(true)) longest = Mathf.Max(longest, timer.m_timeout);
                if (longest <= 0f) longest = 5f;
            }
            return longest > 0f ? Mathf.Clamp(longest, 3f, 10f) : 4.5f;
        }

        /// <summary>
        /// A tree copy felled as <c>TreeBase.SpawnLog</c> fells it: its log, the size of the tree,
        /// tipped over from high up so it topples away from you, and its stump left standing,
        /// solid, for the log to topple off.
        /// </summary>
        public static GameObject Fell(GameObject prefab, GameObject copy, Transform parent, int layer, int physicsLayer, Vector3 away)
        {
            var tree = prefab.GetComponent<TreeBase>();
            if (tree == null || tree.m_logPrefab == null) return null;

            var point = tree.m_logSpawnPoint != null ? Looks.Twin(prefab.transform, copy.transform, tree.m_logSpawnPoint) : null;
            if (point == null) point = copy.transform;
            var scale = copy.transform.lossyScale.x;

            var holder = NewHolder("Scry felled tree", parent, copy.transform, layer);

            var log = Ghost.Make(tree.m_logPrefab, holder.transform, point.position, point.rotation, layer, falling: true);
            if (log == null)
            {
                Object.Destroy(holder);
                return null;
            }
            log.transform.localScale = tree.m_logPrefab.transform.localScale * scale;
            if (layer < 0) Solidify(log, physicsLayer);
            Physics.SyncTransforms();
            FallWatch.Start(log, copy, layer >= 0);

            var body = log.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.mass *= scale;
                body.ResetInertiaTensor();
                body.AddForceAtPosition(away * 0.2f * body.mass, log.transform.position + Vector3.up * 4f * scale, ForceMode.Impulse);
            }

            // The stump stands solid, as the game's does: the log rests on it and topples off onto
            // the ground, a contact of its own, which is what makes the log strike
            // (ImpactEffect.OnCollisionEnter); on bare ground the log's foot would touch it all along.
            if (tree.m_stubPrefab != null)
            {
                var stump = Ghost.Make(tree.m_stubPrefab, holder.transform, copy.transform.position, copy.transform.rotation, layer, falling: true);
                if (stump != null)
                {
                    stump.transform.localScale = tree.m_stubPrefab.transform.localScale * scale;
                    foreach (var held in stump.GetComponentsInChildren<Rigidbody>(true)) held.isKinematic = true;
                    if (layer < 0) Solidify(stump, physicsLayer);
                }
            }
            return holder;
        }

        /// <summary>
        /// What holds a fall's pieces: where the copy stood, and on the stage on its layer, so the
        /// log says where the fall is rather than where the world or the stage begins.
        /// </summary>
        private static GameObject NewHolder(string name, Transform parent, Transform at, int layer)
        {
            var holder = new GameObject(name);
            if (layer >= 0) holder.layer = layer;
            holder.transform.SetParent(parent, false);
            holder.transform.position = at.position;
            return holder;
        }

        /// <summary>
        /// A copy of debris an effect list throws (planks, splinters, stones), falling under its
        /// own physics where the list puts it.
        /// </summary>
        public static GameObject Debris(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, int layer, int physicsLayer)
        {
            var copy = Ghost.Make(prefab, parent, position, rotation, layer, falling: true);
            Gib(prefab, copy, Vector3.zero);
            if (copy != null && layer < 0) Solidify(copy, physicsLayer);
            return copy;
        }

        /// <summary>
        /// What else the game leaves when it destroys the prefab, under the parent, at the copy's
        /// size: a log's halves at its split points (<c>TreeLog.Destroy</c>), what a destructible
        /// leaves in its place (<c>Destructible.Destroy</c>'s spawn, its gibs flung as
        /// <c>Gibber</c> flings them), and what its drop table rolls, falling out where the game
        /// puts it (<c>TreeBase</c>, <c>TreeLog</c>, <c>DropOnDestroyed</c>). True when anything
        /// was left.
        /// </summary>
        public static bool Leave(GameObject prefab, GameObject copy, Transform parent, int layer, int physicsLayer, Vector3 away)
        {
            var left = false;
            var t = copy.transform;
            var size = t.lossyScale;
            var shown = t.lossyScale.x / Mathf.Max(0.001f, prefab.transform.localScale.x);

            var log = prefab.GetComponent<TreeLog>();
            if (log != null && log.m_subLogPrefab != null)
            {
                foreach (var point in log.m_subLogPoints)
                {
                    if (point == null) continue;
                    var at = Looks.Twin(prefab.transform, t, point).OrNull() ?? t;
                    var half = Ghost.Make(log.m_subLogPrefab, parent, at.position, log.m_useSubLogPointRotation ? at.rotation : t.rotation, layer, falling: true);
                    if (half == null) continue;
                    half.transform.localScale = size;
                    if (layer < 0) Solidify(half, physicsLayer);
                    FallWatch.Start(half, copy, layer >= 0);
                    left = true;
                }
            }

            var destructible = prefab.GetComponent<Destructible>();
            var spawn = destructible != null ? destructible.m_spawnWhenDestroyed : null;
            if (spawn != null && spawn.GetComponentInChildren<Character>(true) == null)
            {
                var broken = Ghost.Make(spawn, parent, t.position, t.rotation, layer, falling: true);
                if (broken != null)
                {
                    broken.transform.localScale = size;
                    Gib(spawn, broken, away);
                    if (layer < 0) Solidify(broken, physicsLayer);
                    FallWatch.Start(broken, copy, layer >= 0);
                    left = true;
                }
            }

            // What its drop table rolls: along a log, and above anything else, a step higher each.
            DropTable table = null;
            float spread = 0f, offset = 0.5f, step = 0.3f;
            if (log != null) { table = log.m_dropWhenDestroyed; spread = log.m_spawnDistance; offset = 0f; }
            else if (prefab.GetComponent<TreeBase>() is TreeBase tree) { table = tree.m_dropWhenDestroyed; offset = tree.m_spawnYOffset; step = tree.m_spawnYStep; }
            else if (prefab.GetComponent<DropOnDestroyed>() is DropOnDestroyed drops) { table = drops.m_dropWhenDestroyed; offset = drops.m_spawnYOffset; step = drops.m_spawnYStep; }
            if (table != null)
            {
                var items = table.GetDropList();
                for (var i = 0; i < items.Count && i < 24; i++)
                {
                    if (items[i] == null) continue;
                    Vector3 position;
                    if (log != null) position = t.position + t.up * Random.Range(-spread, spread) * shown + Vector3.up * 0.3f * i * shown;
                    else
                    {
                        var circle = Random.insideUnitCircle * 0.5f * shown;
                        position = t.position + Vector3.up * offset * shown + new Vector3(circle.x, step * i * shown, circle.y);
                    }
                    var item = Ghost.Make(items[i], parent, position, Quaternion.Euler(0f, Random.Range(0, 360), 0f), layer, falling: true);
                    if (item == null) continue;
                    item.transform.localScale = items[i].transform.localScale * shown;
                    if (layer < 0) Solidify(item, physicsLayer);
                    FallWatch.Start(item, copy, layer >= 0);
                    left = true;
                }
            }
            if (left) Physics.SyncTransforms();
            return left;
        }

        /// <summary>
        /// Flings a copy's pieces as the game's <c>Gibber</c> does when it appears: each drawn part
        /// without a body gets one, and every body flies out from their middle, turned towards the
        /// hit's direction by as much as it says, at a speed between its least and most, spinning.
        /// Nothing for a copy of a prefab without one.
        /// </summary>
        public static void Gib(GameObject prefab, GameObject copy, Vector3 hitDir)
        {
            var gibber = prefab != null ? prefab.GetComponent<Gibber>() : null;
            if (gibber == null || copy == null) return;
            foreach (var part in copy.GetComponentsInChildren<MeshRenderer>())
            {
                if (part.GetComponent<Rigidbody>() != null) continue;
                part.gameObject.AddComponent<BoxCollider>();
                part.gameObject.AddComponent<Rigidbody>().maxDepenetrationVelocity = 2f;
            }
            var bodies = copy.GetComponentsInChildren<Rigidbody>();
            if (bodies.Length == 0) return;
            var middle = Vector3.zero;
            foreach (var body in bodies) middle += body.worldCenterOfMass;
            middle /= bodies.Length;
            var mix = hitDir.magnitude > 0.01f ? gibber.m_impactDirectionMix : 0f;
            foreach (var body in bodies)
            {
                var direction = Vector3.Lerp(Vector3.Normalize(body.worldCenterOfMass - middle), hitDir, mix);
                body.linearVelocity = direction * Random.Range(gibber.m_minVel, gibber.m_maxVel);
                var spin = gibber.m_maxRotVel;
                body.angularVelocity = new Vector3(Random.Range(-spin, spin), Random.Range(-spin, spin), Random.Range(-spin, spin));
            }
        }

        /// <summary>
        /// A loose copy of something the game leaves to physics (a log, an item), dropped a little
        /// above where the copy stands and let fall: a log pushed across its length, so it rolls,
        /// anything else set tumbling.
        /// </summary>
        public static GameObject Loose(GameObject prefab, GameObject copy, Transform parent, int layer, int physicsLayer, Vector3 away)
        {
            var t = copy.transform;
            var holder = NewHolder("Scry let fall", parent, t, layer);
            var size = t.lossyScale.x;
            var loose = Ghost.Make(prefab, holder.transform, t.position + Vector3.up * 0.5f * size, t.rotation, layer, falling: true);
            if (loose == null)
            {
                Object.Destroy(holder);
                return null;
            }
            loose.transform.localScale = t.lossyScale;
            if (layer < 0) Solidify(loose, physicsLayer);

            var body = loose.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = false;
                if (prefab.GetComponent<TreeLog>() != null)
                {
                    // Across its longest side, and turning about it.
                    var along = LongestSide(loose);
                    var across = Vector3.ProjectOnPlane(away, along).normalized;
                    if (across.sqrMagnitude < 0.01f) across = Vector3.Cross(along, Vector3.up).normalized;
                    body.linearVelocity = across * 2.5f * size;
                    body.angularVelocity = Vector3.Cross(Vector3.up, across) * 3f;
                }
                else
                {
                    body.linearVelocity = (away * 1.5f + Vector3.up * 2f) * size;
                    body.angularVelocity = Random.onUnitSphere * 6f;
                }
            }
            Physics.SyncTransforms();
            FallWatch.Start(loose, copy, layer >= 0);
            return holder;
        }

        /// <summary>The world direction of a copy's longest side, by what it draws.</summary>
        private static Vector3 LongestSide(GameObject copy)
        {
            var mesh = copy.GetComponentInChildren<MeshFilter>();
            if (mesh == null || mesh.sharedMesh == null) return copy.transform.forward;
            var extents = mesh.sharedMesh.bounds.extents;
            var local = extents.x >= extents.y && extents.x >= extents.z ? Vector3.right : extents.y >= extents.z ? Vector3.up : Vector3.forward;
            return mesh.transform.TransformDirection(local).normalized;
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
                var main = Looks.Twin(prefab.transform, fallen.transform, ragdoll.m_mainModel.transform).OrNull()?.GetComponent<Renderer>();
                if (main != null) Looks.Tint(creature, main, level);
            }

            var worn = new List<GameObject>();
            foreach (var item in gear)
            {
                var slot = PrefabGear.SlotOf(item);
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
            if (piece.OrNull()?.m_fragmentRoots != null)
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

            var holder = NewHolder("Scry pieces", parent, copy.transform, layer);

            var bodies = new List<Rigidbody>();
            foreach (var root in roots)
            {
                var skipped = LowerDetail(root);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.enabled || skipped.Contains(renderer)) continue;
                    var mesh = renderer.GetComponent<MeshFilter>().OrNull()?.sharedMesh;
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
                if (Clone(collider, holder)) Object.DestroyImmediate(collider);
                else Object.DestroyImmediate(holder);
            }
        }

        /// <summary>
        /// A collider copied onto another object, as it was: its shape, where it is, and whether
        /// it is a trigger, switched on, and of what material. A box, sphere or capsule is copied
        /// in its real size onto an object of no scale of its own, placed where it was: made the
        /// child of a part scaled unevenly (a log is 12 by 12 by 6.5), its copy came out stretched
        /// (the log's 13.8 m capsule 25.4 m, reaching 5 m into the ground). A mesh keeps its part.
        /// </summary>
        private static bool Clone(Collider from, GameObject to)
        {
            var owner = from.transform;
            if (from is MeshCollider)
            {
                to.transform.SetParent(owner, false);
            }
            else
            {
                // Under the body, which moves it, at no scale: its size is given in metres.
                var body = from.attachedRigidbody != null ? from.attachedRigidbody.transform : owner;
                to.transform.SetParent(body, false);
                var bodyScale = body.lossyScale;
                to.transform.localScale = new Vector3(1f / Mathf.Max(1e-6f, Mathf.Abs(bodyScale.x)), 1f / Mathf.Max(1e-6f, Mathf.Abs(bodyScale.y)), 1f / Mathf.Max(1e-6f, Mathf.Abs(bodyScale.z)));
                to.transform.rotation = owner.rotation;
            }
            if (!Shape(from, to)) return false;
            var made = to.GetComponent<Collider>();
            made.isTrigger = from.isTrigger;
            made.enabled = from.enabled;
            made.sharedMaterial = from.sharedMaterial;
            return true;
        }

        private static bool Shape(Collider from, GameObject to)
        {
            // The part's own scale, by its axes, which the copy takes into its size in metres.
            var owner = from.transform;
            var scale = owner.lossyScale;
            var axes = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            switch (from)
            {
                case BoxCollider box:
                    to.transform.position = owner.TransformPoint(box.center);
                    var b = to.AddComponent<BoxCollider>();
                    b.center = Vector3.zero;
                    b.size = Vector3.Scale(box.size, axes);
                    return true;
                case SphereCollider sphere:
                    to.transform.position = owner.TransformPoint(sphere.center);
                    var s = to.AddComponent<SphereCollider>();
                    s.center = Vector3.zero;
                    s.radius = sphere.radius * Mathf.Max(axes.x, Mathf.Max(axes.y, axes.z));
                    return true;
                case CapsuleCollider capsule:
                    // As Unity sizes one: its length by the scale along it, its radius by the
                    // larger of the other two.
                    to.transform.position = owner.TransformPoint(capsule.center);
                    var along = capsule.direction == 0 ? axes.x : capsule.direction == 1 ? axes.y : axes.z;
                    var across = capsule.direction == 0 ? Mathf.Max(axes.y, axes.z) : capsule.direction == 1 ? Mathf.Max(axes.x, axes.z) : Mathf.Max(axes.x, axes.y);
                    var c = to.AddComponent<CapsuleCollider>();
                    c.center = Vector3.zero;
                    c.direction = capsule.direction;
                    c.radius = capsule.radius * across;
                    c.height = capsule.height * along;
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

            // Gone at the frame's end whatever its parting effect does, so one that fails leaves
            // no piece lying on and failing again each frame.
            Destroy(gameObject);
            if (_parting == null) return;
            Guard.Run(Feature.LetFall, "playing a falling piece's parting effect", () =>
            {
                var body = GetComponentInChildren<Rigidbody>();
                var at = body != null ? body.transform : transform;
                if (_onStage) Stage.PlayList(_parting, at);
                else Previews.PlayList(_parting, at.position, Quaternion.identity);
            });
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
