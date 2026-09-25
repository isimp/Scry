using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Weapons and armour on a creature copy. The game puts them on through <c>VisEquipment</c>,
    /// which the copy no longer has, so this does what its <c>AttachItem</c> does: each item's
    /// "attach" part is hung on the matching hand or head bone, and an "attach_skin" part (chest,
    /// legs, capes) is bound to the body's own bones.
    /// </summary>
    internal static class Gear
    {
        /// <summary>
        /// The gear choices a creature has: "Gear" when it always carries the same things, or one
        /// entry per set when it is given one of several.
        /// </summary>
        public static bool Sets(GameObject prefab, out List<string> sets)
        {
            sets = new List<string>();
            var humanoid = prefab.GetComponent<Humanoid>();
            if (humanoid == null || prefab.GetComponentInChildren<VisEquipment>(true) == null) return false;

            if (humanoid.m_randomSets != null && humanoid.m_randomSets.Length > 0)
            {
                for (var i = 0; i < humanoid.m_randomSets.Length; i++)
                {
                    var set = humanoid.m_randomSets[i];
                    if (set == null || !AnyVisible(set.m_items)) continue;
                    sets.Add(string.IsNullOrEmpty(set.m_name) ? $"Set {i + 1}" : Naming.FieldLabel(set.m_name));
                }
                if (sets.Count > 0) return true;
            }

            if (AnyVisible(humanoid.m_defaultItems) || AnyVisible(humanoid.m_randomWeapon)
                || AnyVisible(humanoid.m_randomArmor) || AnyVisible(humanoid.m_randomShield))
            {
                sets.Add("Gear");
            }
            return sets.Count > 0;
        }

        /// <summary>Puts the gear of a look on the copy: 1 is the first set, or the only one.</summary>
        public static void Dress(GameObject prefab, GameObject copy, int look)
        {
            var humanoid = prefab.GetComponent<Humanoid>();
            var vis = prefab.GetComponentInChildren<VisEquipment>(true);
            if (humanoid == null || vis == null) return;

            var items = new List<GameObject>();
            if (humanoid.m_defaultItems != null) items.AddRange(humanoid.m_defaultItems);

            var visibleSets = new List<Humanoid.ItemSet>();
            if (humanoid.m_randomSets != null)
            {
                foreach (var set in humanoid.m_randomSets) if (set != null && AnyVisible(set.m_items)) visibleSets.Add(set);
            }

            if (visibleSets.Count > 0)
            {
                items.AddRange(visibleSets[Mathf.Clamp(look - 1, 0, visibleSets.Count - 1)].m_items);
            }
            else
            {
                // A creature given one of several weapons, armours or shields shows the first of each.
                AddFirst(items, humanoid.m_randomWeapon);
                AddFirst(items, humanoid.m_randomArmor);
                AddFirst(items, humanoid.m_randomShield);
            }

            var root = prefab.transform;
            var right = vis.m_rightHand != null ? Looks.Twin(root, copy.transform, vis.m_rightHand) : null;
            var left = vis.m_leftHand != null ? Looks.Twin(root, copy.transform, vis.m_leftHand) : null;
            var head = vis.m_helmet != null ? Looks.Twin(root, copy.transform, vis.m_helmet) : null;
            var body = vis.m_bodyModel != null ? Looks.Twin(root, copy.transform, vis.m_bodyModel.transform)?.GetComponent<SkinnedMeshRenderer>() : null;

            var usedRight = false;
            var usedLeft = false;
            foreach (var item in items)
            {
                var drop = item != null ? item.GetComponent<ItemDrop>() : null;
                var shared = drop?.m_itemData?.m_shared;
                if (shared == null) continue;

                var part = AttachPart(item, out var skin);
                if (part == null) continue;

                if (skin)
                {
                    if (body != null) Skin(part, body, copy.layer);
                    continue;
                }

                Transform joint = null;
                switch (shared.m_itemType)
                {
                    case ItemDrop.ItemData.ItemType.Shield:
                    case ItemDrop.ItemData.ItemType.Bow:
                    case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                        if (!usedLeft) { joint = left; usedLeft = true; }
                        break;
                    case ItemDrop.ItemData.ItemType.Helmet:
                        joint = head;
                        break;
                    case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                    case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                    case ItemDrop.ItemData.ItemType.Torch:
                    case ItemDrop.ItemData.ItemType.Tool:
                        if (!usedRight) { joint = right; usedRight = true; }
                        break;
                }
                if (joint == null) continue;

                var worn = Ghost.Make(part, joint, joint.position, joint.rotation, copy.layer);
                if (worn == null) continue;
                worn.SetActive(true);
                worn.transform.localPosition = Vector3.zero;
                worn.transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// The body a creature is drawn with when <c>VisEquipment</c> picks it at run time, as it
        /// does for the player: without it the copy of such a prefab has no body mesh at all.
        /// </summary>
        public static void Body(GameObject prefab, GameObject copy)
        {
            var vis = prefab.GetComponentInChildren<VisEquipment>(true);
            if (vis == null || vis.m_bodyModel == null || vis.m_models == null || vis.m_models.Length == 0) return;

            var body = Looks.Twin(prefab.transform, copy.transform, vis.m_bodyModel.transform)?.GetComponent<SkinnedMeshRenderer>();
            var model = vis.m_models[0];
            if (body == null || model == null || model.m_mesh == null) return;

            if (body.sharedMesh == null) body.sharedMesh = model.m_mesh;
            if (model.m_baseMaterial != null && (body.sharedMaterial == null || body.sharedMaterials.Length == 0)) body.sharedMaterial = model.m_baseMaterial;
        }

        /// <summary>Hangs a skinned part (armour, cape) on the body's own bones.</summary>
        private static void Skin(GameObject part, SkinnedMeshRenderer body, int layer)
        {
            var parent = body.transform.parent;
            var worn = Ghost.Make(part, parent, parent.position, parent.rotation, layer);
            if (worn == null) return;
            worn.SetActive(true);
            worn.transform.localPosition = Vector3.zero;
            worn.transform.localRotation = Quaternion.identity;

            foreach (var renderer in worn.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.rootBone = body.rootBone;
                renderer.bones = body.bones;
                renderer.updateWhenOffscreen = true;
            }
        }

        /// <summary>The part of an item worn on the body, found as <c>VisEquipment.AttachItem</c> finds it.</summary>
        private static GameObject AttachPart(GameObject item, out bool skin)
        {
            skin = false;
            var t = item.transform;
            for (var i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (child.name == "attach") return child.gameObject;
                if (child.name == "attach_skin")
                {
                    skin = true;
                    return child.gameObject;
                }
            }
            return null;
        }

        private static bool AnyVisible(GameObject[] items)
        {
            if (items == null) return false;
            foreach (var item in items)
            {
                if (item != null && AttachPart(item, out _) != null) return true;
            }
            return false;
        }

        private static void AddFirst(List<GameObject> items, GameObject[] choices)
        {
            if (choices == null) return;
            foreach (var choice in choices)
            {
                if (choice == null) continue;
                items.Add(choice);
                return;
            }
        }
    }
}
