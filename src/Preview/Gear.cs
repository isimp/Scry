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

            Wear(prefab, copy, items);
        }

        private static readonly int ChestTex = Shader.PropertyToID("_ChestTex");
        private static readonly int ChestBumpMap = Shader.PropertyToID("_ChestBumpMap");
        private static readonly int ChestMetal = Shader.PropertyToID("_ChestMetal");
        private static readonly int LegsTex = Shader.PropertyToID("_LegsTex");
        private static readonly int LegsBumpMap = Shader.PropertyToID("_LegsBumpMap");
        private static readonly int LegsMetal = Shader.PropertyToID("_LegsMetal");

        /// <summary>Whether an item can be shown worn: something held, worn on the head or the body.</summary>
        public static bool IsWearable(GameObject item)
        {
            var shared = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
            if (shared == null) return false;
            if (AttachPart(item, out _) != null) return true;
            var type = shared.m_itemType;
            return shared.m_armorMaterial != null
                   && (type == ItemDrop.ItemData.ItemType.Chest || type == ItemDrop.ItemData.ItemType.Legs);
        }

        /// <summary>
        /// Puts items on a copy of a character, as <c>VisEquipment</c> does: held and head items
        /// on their bones, body items bound to the skeleton, and chest and leg armour also
        /// painted onto the body the way the game swaps its textures. A style, when given, is
        /// set on each worn part as <c>ItemStyle</c> sets it.
        /// </summary>
        public static void Wear(GameObject prefab, GameObject copy, IList<GameObject> items, int style = -1)
        {
            var vis = prefab.GetComponentInChildren<VisEquipment>(true);
            if (vis == null) return;

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

                if (body != null && shared.m_armorMaterial != null) Paint(body, shared);

                var part = AttachPart(item, out var skin);
                if (part == null) continue;

                GameObject worn = null;
                if (skin)
                {
                    if (body != null) worn = Skin(part, body, copy.layer);
                }
                else
                {
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

                    worn = Ghost.Make(part, joint, joint.position, joint.rotation, copy.layer);
                    if (worn == null) continue;
                    worn.SetActive(true);
                    worn.transform.localPosition = Vector3.zero;
                    worn.transform.localRotation = Quaternion.identity;
                }

                if (worn != null && style >= 0 && MaterialMan.instance != null)
                {
                    foreach (var styled in part.GetComponentsInChildren<ItemStyle>(true))
                    {
                        var twin = Looks.Twin(part.transform, worn.transform, styled.transform);
                        if (twin != null) MaterialMan.instance.SetValue(twin.gameObject, ShaderProps._Style, style, true);
                    }
                }
            }
        }

        /// <summary>Chest and leg armour change the body's own textures as well, as the game does.</summary>
        private static void Paint(SkinnedMeshRenderer body, ItemDrop.ItemData.SharedData shared)
        {
            var armour = shared.m_armorMaterial;
            var material = body.material;
            if (shared.m_itemType == ItemDrop.ItemData.ItemType.Chest)
            {
                if (armour.HasProperty(ChestTex)) material.SetTexture(ChestTex, armour.GetTexture(ChestTex));
                if (armour.HasProperty(ChestBumpMap)) material.SetTexture(ChestBumpMap, armour.GetTexture(ChestBumpMap));
                if (armour.HasProperty(ChestMetal)) material.SetTexture(ChestMetal, armour.GetTexture(ChestMetal));
            }
            else if (shared.m_itemType == ItemDrop.ItemData.ItemType.Legs)
            {
                if (armour.HasProperty(LegsTex)) material.SetTexture(LegsTex, armour.GetTexture(LegsTex));
                if (armour.HasProperty(LegsBumpMap)) material.SetTexture(LegsBumpMap, armour.GetTexture(LegsBumpMap));
                if (armour.HasProperty(LegsMetal)) material.SetTexture(LegsMetal, armour.GetTexture(LegsMetal));
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
        private static GameObject Skin(GameObject part, SkinnedMeshRenderer body, int layer)
        {
            var parent = body.transform.parent;
            var worn = Ghost.Make(part, parent, parent.position, parent.rotation, layer);
            if (worn == null) return null;
            worn.SetActive(true);
            worn.transform.localPosition = Vector3.zero;
            worn.transform.localRotation = Quaternion.identity;

            var bones = body.bones;
            foreach (var renderer in worn.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Armour made for another skeleton cannot be bound to this one; it is left off
                // rather than drawn twisted.
                var mesh = renderer.sharedMesh;
                if (mesh != null && mesh.bindposes.Length != bones.Length)
                {
                    Plugin.Log.LogDebug($"Scry left {part.name} off: made for {mesh.bindposes.Length} bones, the body has {bones.Length}.");
                    renderer.enabled = false;
                    continue;
                }

                renderer.rootBone = body.rootBone;
                renderer.bones = bones;
                renderer.updateWhenOffscreen = true;
            }
            return worn;
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
