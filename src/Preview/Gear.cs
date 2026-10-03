using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static Gear() => WorldCaches.Register(nameof(Gear), Forget);

        /// <summary>
        /// The gear choices a creature has: "Gear" when it always carries the same things, or one
        /// entry per set when it is given one of several.
        /// </summary>
        public static bool Sets(GameObject prefab, out List<string> sets)
        {
            sets = new List<string>();
            var humanoid = prefab.GetComponent<Humanoid>();
            if (humanoid == null || prefab.GetComponentInChildren<VisEquipment>(true) == null) return false;

            // Every set it may roll, also one with nothing drawn: a troll given the set without a
            // log fights with its hands, and is a look of its own.
            var rolled = RolledSets(humanoid);
            if (rolled.Count > 0)
            {
                foreach (var set in rolled) sets.Add(SetName(set, rolled.IndexOf(set)));
                Tell(prefab, rolled);
                return true;
            }

            if (AnyVisible(humanoid.m_defaultItems) || AnyVisible(humanoid.m_randomWeapon)
                || AnyVisible(humanoid.m_randomArmor) || AnyVisible(humanoid.m_randomShield)
                || LoadoutOf(prefab).HasChoices)
            {
                sets.Add("Gear");
            }
            return sets.Count > 0;
        }

        /// <summary>The sets a humanoid rolls one of when it spawns, those with anything in them.</summary>
        private static List<Humanoid.ItemSet> RolledSets(Humanoid humanoid)
        {
            // A set listed twice (to be rolled more often) is one look.
            var sets = new List<Humanoid.ItemSet>();
            var seen = new HashSet<string>();
            if (humanoid.m_randomSets == null) return sets;
            foreach (var set in humanoid.m_randomSets)
            {
                if (set?.m_items == null || !set.m_items.Any(i => i != null)) continue;
                var key = string.Join(",", set.m_items.Where(i => i != null).Select(i => i.name).OrderBy(n => n, StringComparer.Ordinal));
                if (seen.Add(key)) sets.Add(set);
            }
            return sets;
        }

        /// <summary>The weapons of the set a look shows, those that show in the hand first.</summary>
        public static List<string> SetWeapons(GameObject prefab, int look)
        {
            var humanoid = prefab != null ? prefab.GetComponent<Humanoid>() : null;
            var sets = humanoid != null ? RolledSets(humanoid) : new List<Humanoid.ItemSet>();
            if (sets.Count == 0 || look <= 0) return new List<string>();
            return HoldChoices(sets[Mathf.Clamp(look - 1, 0, sets.Count - 1)].m_items, prefab.name);
        }

        /// <summary>
        /// Everything a creature carries in a look, as the game gives it at spawn: what it always
        /// has, the set it rolled, and the weapon, shield, armour and extras it rolled. All of it,
        /// not only what is in its hand, since its AI takes any of its weapons in a fight.
        /// </summary>
        public static List<GameObject> Inventory(GameObject prefab, int look)
        {
            var items = new List<GameObject>();
            var humanoid = prefab != null ? prefab.GetComponent<Humanoid>() : null;
            if (humanoid == null) return items;
            if (humanoid.m_defaultItems != null) items.AddRange(humanoid.m_defaultItems.Where(i => i != null));
            var sets = RolledSets(humanoid);
            if (sets.Count > 0) items.AddRange(sets[Mathf.Clamp(Mathf.Max(1, look) - 1, 0, sets.Count - 1)].m_items.Where(i => i != null));
            var loadout = LoadoutOf(prefab);
            foreach (var row in new[] { Loadout.Row.Weapon, Loadout.Row.Shield, Loadout.Row.Armour })
            {
                if (loadout.Options(row).Count == 0 || !loadout.Held(row)) continue;
                var item = Looks.Prefab(loadout.Options(row)[loadout.Chosen(row)]);
                if (item != null) items.Add(item);
            }
            for (var i = 0; i < loadout.Extras.Count; i++)
            {
                var extra = loadout.ExtraOn(i) ? Looks.Prefab(loadout.Extras[i].Name) : null;
                if (extra != null) items.Add(extra);
            }
            return items.Distinct().ToList();
        }

        /// <summary>A set by its own name, else by what of it is drawn, else by its number.</summary>
        private static string SetName(Humanoid.ItemSet set, int index)
        {
            if (!string.IsNullOrEmpty(set.m_name)) return Naming.FieldLabel(set.m_name);
            var drawn = set.m_items.Where(i => i != null && AttachPart(i, out _) != null).Select(CatalogBuilder.AttackName).Distinct().ToList();
            return drawn.Count > 0 ? string.Join(" + ", drawn) : $"Set {Numbers.Count(index + 1)}, nothing drawn";
        }

        private static readonly HashSet<string> ToldSets = new HashSet<string>();

        /// <summary>Says once per creature which sets it rolls from and what is in each, drawn or not.</summary>
        private static void Tell(GameObject prefab, List<Humanoid.ItemSet> sets)
        {
            if (!ToldSets.Add(prefab.name)) return;
            var told = sets.Select((set, i) => $"{SetName(set, i)} ({string.Join(", ", set.m_items.Where(x => x != null).Select(x => x.name + (AttachPart(x, out _) != null ? "" : " not drawn")))})");
            Plugin.Note($"Scry: {prefab.name} rolls one of {Numbers.Count(sets.Count)} gear sets: {string.Join("; ", told)}.");
        }

        /// <summary>Puts the gear of a look on the copy: 1 is the first set, or the only one.</summary>
        public static void Dress(GameObject prefab, GameObject copy, int look)
        {
            var items = DressItems(prefab, look);
            if (items.Count > 0) Wear(prefab, copy, items);
        }

        /// <summary>
        /// What a creature wears in a look, as <c>Humanoid.GiveDefaultItems</c> hands it out: what
        /// it always has, the set of the look, and the weapon, shield, armour and extras chosen in
        /// its <see cref="LoadoutOf">loadout</see>, which the game would roll.
        /// </summary>
        public static List<GameObject> DressItems(GameObject prefab, int look)
        {
            var items = new List<GameObject>();
            var humanoid = prefab != null ? prefab.GetComponent<Humanoid>() : null;
            if (humanoid == null || look <= 0 || prefab.GetComponentInChildren<VisEquipment>(true) == null) return items;

            // Of the weapons it always carries it holds one, the one chosen; the rest are put away.
            var loadout = LoadoutOf(prefab);
            loadout.Carrying(SetWeapons(prefab, look));
            var holding = loadout.Options(Loadout.Row.Holding);
            var held = holding.Count > 0 ? holding[loadout.Chosen(Loadout.Row.Holding)] : null;
            if (humanoid.m_defaultItems != null)
            {
                foreach (var item in humanoid.m_defaultItems)
                {
                    if (item != null && (!holding.Contains(item.name) || item.name == held)) items.Add(item);
                }
            }
            var chosen = loadout.Worn();

            var visibleSets = RolledSets(humanoid);

            // In the order GiveDefaultItems hands them out: shield, weapon, armour, set, extras.
            void Add(string name)
            {
                var item = string.IsNullOrEmpty(name) ? null : Looks.Prefab(name);
                if (item != null) items.Add(item);
            }
            foreach (var row in new[] { Loadout.Row.Shield, Loadout.Row.Weapon, Loadout.Row.Armour })
            {
                // The rolled weapon only when it is the one in hand.
                if (loadout.Options(row).Count == 0) continue;
                var choice = loadout.Options(row)[loadout.Chosen(row)];
                if (chosen.Contains(choice)) Add(choice);
            }
            if (visibleSets.Count > 0)
            {
                // Of the set's weapons only the one in hand; the rest of it is worn.
                foreach (var item in visibleSets[Mathf.Clamp(look - 1, 0, visibleSets.Count - 1)].m_items)
                {
                    if (item != null && (!holding.Contains(item.name) || item.name == held)) items.Add(item);
                }
            }
            for (var i = 0; i < loadout.Extras.Count; i++)
            {
                if (loadout.ExtraOn(i)) Add(loadout.Extras[i].Name);
            }
            // What draws nothing (a creature's unseen attacks) has no place on the copy, and must
            // not take the hand from a weapon that shows.
            items.RemoveAll(i => i == null || !Draws(i));

            // Each item is equipped as it is handed out, taking the place of one worn in the same
            // slot, so what comes later is what shows.
            var outfit = new Outfit();
            var slotless = new List<GameObject>();
            foreach (var item in items)
            {
                var slot = SlotOf(item);
                if (slot == Slot.None) slotless.Add(item);
                else outfit.Keep(item.name, slot);
            }
            var worn = new List<GameObject>(slotless);
            foreach (var name in outfit.Keys)
            {
                var item = items.Find(i => i.name == name);
                if (item != null) worn.Add(item);
            }
            return worn;
        }

        private static readonly Dictionary<GameObject, Loadout> Loadouts = new Dictionary<GameObject, Loadout>();

        /// <summary>
        /// The weapon, shield and armour a creature rolls from its lists when it spawns, and the
        /// extras it may be given by chance, as choices. Only items that show when worn are
        /// offered. The choices are kept for each creature until reset.
        /// </summary>
        public static Loadout LoadoutOf(GameObject prefab)
        {
            if (prefab == null) return new Loadout(null, null, null, null);
            if (Loadouts.TryGetValue(prefab, out var known)) return known;

            var humanoid = prefab.GetComponent<Humanoid>();
            var extras = new List<Loadout.Extra>();
            if (humanoid?.m_randomItems != null)
            {
                foreach (var random in humanoid.m_randomItems)
                {
                    var item = random?.m_prefab;
                    var shared = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                    if (shared != null && Shows(item)) extras.Add(new Loadout.Extra(item.name, (int)shared.m_itemType));
                }
            }

            var loadout = humanoid == null
                ? new Loadout(null, null, null, null)
                : new Loadout(Choices(humanoid.m_randomWeapon), Choices(humanoid.m_randomShield), Choices(humanoid.m_randomArmor), extras,
                    (humanoid.m_randomWeapon ?? Array.Empty<GameObject>()).Concat(humanoid.m_defaultItems ?? Array.Empty<GameObject>())
                        .Where(w => w != null && SlotOf(w) == Slot.BothHands).Select(w => w.name),
                    Holdable(humanoid));
            Loadouts[prefab] = loadout;
            return loadout;
        }

        /// <summary>
        /// The weapons a creature always carries: with the one it rolls, it holds one at a time,
        /// as its AI picks them in a fight. One that shows in the hand comes first, as it is the
        /// one to see.
        /// </summary>
        private static List<string> Holdable(Humanoid humanoid) => HoldChoices(humanoid.m_defaultItems, humanoid.gameObject.name);

        /// <summary>
        /// Of some items, the weapons that are choices to hold (<see cref="WeaponChoices.Holdable"/>):
        /// only those that show in the hand, one of each that shows alike.
        /// </summary>
        private static List<string> HoldChoices(IEnumerable<GameObject> items, string creature)
        {
            var weapons = (items ?? Array.Empty<GameObject>())
                .Where(i => i != null && i.GetComponent<ItemDrop>()?.m_itemData?.IsWeapon() == true)
                .Distinct()
                .Select(i => (i.name, Drawn(i) ?? CatalogBuilder.GameName(i) ?? WeaponChoices.Readable(i.name, creature), AttachPart(i, out _) != null));
            return WeaponChoices.Holdable(weapons);
        }

        /// <summary>
        /// What an item draws in the hand, by the meshes of its part: two items drawing the same
        /// (a troll's log for either swing) are the same weapon to see. Null when it draws none.
        /// </summary>
        private static string Drawn(GameObject item)
        {
            var part = AttachPart(item, out _);
            if (part == null) return null;
            var meshes = part.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh != null ? m.sharedMesh.name : "")
                .Concat(part.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(m => m.sharedMesh != null ? m.sharedMesh.name : ""))
                .Where(n => n.Length > 0).OrderBy(n => n, StringComparer.Ordinal).ToList();
            return meshes.Count > 0 ? string.Join(",", meshes) : null;
        }

        /// <summary>Whether a creature's loadout differs from the first of each.</summary>
        public static bool LoadoutChanged(GameObject prefab)
        {
            return prefab != null && Loadouts.TryGetValue(prefab, out var loadout) && loadout.Changed;
        }

        /// <summary>Puts a creature's loadout back to the first of each.</summary>
        public static void ResetLoadout(GameObject prefab)
        {
            if (prefab != null) Loadouts.Remove(prefab);
        }

        /// <summary>
        /// Lets go of the loadouts of prefabs that are gone, for a world that was left. The
        /// choices made for prefabs that are still there are kept, as they were before.
        /// </summary>
        public static void Forget()
        {
            foreach (var prefab in Loadouts.Keys.Where(p => p == null).ToList()) Loadouts.Remove(prefab);
        }

        /// <summary>A list's items by name, an empty entry kept as nothing, items that do not show left out.</summary>
        private static List<string> Choices(GameObject[] items)
        {
            var names = new List<string>();
            if (items == null) return names;
            foreach (var item in items)
            {
                if (item == null) names.Add(null);
                else if (Shows(item)) names.Add(item.name);
            }
            return names;
        }

        private static bool Shows(GameObject item)
        {
            var shared = item.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            return AttachPart(item, out _) != null || shared?.m_armorMaterial != null;
        }

        private static readonly int ChestTex = Shader.PropertyToID("_ChestTex");
        private static readonly int ChestBumpMap = Shader.PropertyToID("_ChestBumpMap");
        private static readonly int ChestMetal = Shader.PropertyToID("_ChestMetal");
        private static readonly int LegsTex = Shader.PropertyToID("_LegsTex");
        private static readonly int LegsBumpMap = Shader.PropertyToID("_LegsBumpMap");
        private static readonly int LegsMetal = Shader.PropertyToID("_LegsMetal");

        /// <summary>
        /// Whether an item can be shown worn: one the game equips (held, or worn on the head or
        /// body) that has something to show when it is. Materials and trophies also carry an
        /// attach part, for item stands, but have no slot.
        /// </summary>
        public static bool IsWearable(GameObject item)
        {
            if (SlotOf(item) == Slot.None) return false;
            var shared = item.GetComponent<ItemDrop>().m_itemData.m_shared;
            if (AttachPart(item, out _) != null) return true;
            var type = shared.m_itemType;
            return shared.m_armorMaterial != null
                   && (type == ItemDrop.ItemData.ItemType.Chest || type == ItemDrop.ItemData.ItemType.Legs);
        }

        /// <summary>Where the game equips an item, by its type.</summary>
        public static Slot SlotOf(GameObject item)
        {
            var shared = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
            if (shared == null) return Slot.None;
            switch (shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.Tool:
                    return Slot.RightHand;
                case ItemDrop.ItemData.ItemType.Shield:
                    return Slot.LeftHand;
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                    return Slot.BothHands;
                case ItemDrop.ItemData.ItemType.Helmet: return Slot.Head;
                case ItemDrop.ItemData.ItemType.Chest: return Slot.Chest;
                case ItemDrop.ItemData.ItemType.Legs: return Slot.Legs;
                case ItemDrop.ItemData.ItemType.Shoulder: return Slot.Shoulders;
                case ItemDrop.ItemData.ItemType.Utility: return Slot.Utility;
                default: return Slot.None;
            }
        }

        /// <summary>
        /// Puts items on a copy of a character, as <c>VisEquipment</c> does: held and head items
        /// on their bones, body items bound to the skeleton, and chest and leg armour also
        /// painted onto the body the way the game swaps its textures. A style, when given, is
        /// set on each worn part as <c>ItemStyle</c> sets it.
        /// </summary>
        public static void Wear(GameObject prefab, GameObject copy, IList<GameObject> items, int style = -1, GameObject styled = null)
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

                if (body != null && shared.m_armorMaterial != null) Paint(body, shared, copy);

                var part = AttachPart(item, out var skin);
                if (part == null) continue;

                GameObject worn = null;
                if (skin)
                {
                    if (body != null) worn = Skin(item, part, body, copy.layer);
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

                    worn = Hang(item, part, joint, copy.layer);
                }

                if (worn != null && style >= 0 && (styled == null || styled == item) && MaterialMan.instance != null)
                {
                    foreach (var itemStyle in part.GetComponentsInChildren<ItemStyle>(true))
                    {
                        var twin = Looks.Twin(part.transform, worn.transform, itemStyle.transform);
                        if (twin != null) MaterialMan.instance.SetValue(twin.gameObject, ShaderProps._Style, style, true);
                    }
                }
            }

            Stance(prefab, copy, items);
        }

        /// <summary>
        /// Sets the stance the items in hand call for on the copy's animator, as
        /// <c>Humanoid.SetupAnimationState</c> does: the left hand's item decides (a torch there
        /// is held as one), else the right hand's, else the bare hands'. A draugr stands with its
        /// bow as with a bow, and a person tried on with an axe holds it as an axe.
        /// </summary>
        private static readonly HashSet<string> Told = new HashSet<string>();

        private static void Stance(GameObject prefab, GameObject copy, IList<GameObject> items)
        {
            var animator = ClipPlayer.AnimatorOf(copy);
            if (animator == null) return;

            ItemDrop.ItemData.SharedData left = null, right = null;
            foreach (var item in items)
            {
                var shared = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                if (shared == null) continue;
                var type = shared.m_itemType;
                if (type == ItemDrop.ItemData.ItemType.Shield || type == ItemDrop.ItemData.ItemType.Bow || type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft) left = shared;
                else if (SlotOf(item) == Slot.RightHand || SlotOf(item) == Slot.BothHands) right = shared;
            }

            var unarmed = prefab.GetComponent<Humanoid>()?.m_unarmedWeapon?.m_itemData?.m_shared;
            var state = left != null ? (left.m_itemType == ItemDrop.ItemData.ItemType.Torch ? ItemDrop.ItemData.AnimationState.LeftTorch : left.m_animationState)
                : right != null ? right.m_animationState
                : unarmed != null ? unarmed.m_animationState
                : ItemDrop.ItemData.AnimationState.Unarmed;

            var stood = false;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == "statei" && parameter.type == AnimatorControllerParameterType.Int) { animator.SetInteger("statei", (int)state); stood = true; }
                if (parameter.name == "statef" && parameter.type == AnimatorControllerParameterType.Float) { animator.SetFloat("statef", (float)state); stood = true; }
            }

            var told = $"{prefab.name}|{string.Join(",", items.Where(i => i != null).Select(i => i.name))}";
            if (Told.Add(told))
            {
                var hands = string.Join(", ", items.Where(i => i != null && SlotOf(i) != Slot.None && SlotOf(i) != Slot.Head && SlotOf(i) != Slot.Chest && SlotOf(i) != Slot.Legs).Select(i => i.name + (AttachPart(i, out _) != null ? "" : " (not drawn)")));
                Plugin.Note($"Scry dressed {prefab.name}: in hand {(hands.Length > 0 ? hands : "nothing")}; stance {state}{(stood ? "" : ", which its animator does not take")}.");
            }
        }

        /// <summary>
        /// Chest and leg armour change the body's own textures as well, as the game does. The body
        /// gets a material of its own for it, which goes with the copy.
        /// </summary>
        private static void Paint(SkinnedMeshRenderer body, ItemDrop.ItemData.SharedData shared, GameObject copy)
        {
            var armour = shared.m_armorMaterial;
            var material = Owned.MaterialOf(body, copy);
            if (material == null) return;
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
        private static GameObject Skin(GameObject item, GameObject part, SkinnedMeshRenderer body, int layer)
        {
            var worn = Hang(item, part, body.transform.parent, layer);
            if (worn == null) return null;

            var bones = body.bones;
            foreach (var renderer in worn.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Armour made for another skeleton cannot be bound to this one; it is left off
                // rather than drawn twisted.
                var mesh = renderer.sharedMesh;
                if (mesh != null && mesh.bindposes.Length != bones.Length)
                {
                    Plugin.Note($"Scry left {part.name} off: made for {Numbers.Count(mesh.bindposes.Length)} bones, the body has {Numbers.Count(bones.Length)}.");
                    renderer.enabled = false;
                    continue;
                }

                renderer.rootBone = body.rootBone;
                renderer.bones = bones;
                renderer.updateWhenOffscreen = true;
            }
            return worn;
        }

        /// <summary>
        /// Hangs a copy of an item's part on a bone the way <c>VisEquipment.AttachItem</c> does:
        /// made on its own and then parented keeping its size in the world, so it is as big on a
        /// creature whose bones are scaled up as in a hand. The item's <c>equipoffset</c> shifts it,
        /// and its <c>equiped</c> child, the glow or flame it has while held, is switched on.
        /// </summary>
        private static GameObject Hang(GameObject item, GameObject part, Transform parent, int layer)
        {
            var worn = Ghost.MakeOn(part, parent, parent.position, parent.rotation, layer);
            if (worn == null) return null;

            var t = worn.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            var offset = item.transform.Find("equipoffset");
            if (offset != null)
            {
                t.localPosition += offset.position;
                t.localRotation *= offset.rotation;
            }

            var equipped = t.Find("equiped");
            if (equipped != null) equipped.gameObject.SetActive(true);
            worn.AddComponent<Hung>();
            worn.SetActive(true);
            return worn;
        }

        /// <summary>Whether an item shows when worn: it has a part to hang, or armour that paints the body.</summary>
        private static bool Draws(GameObject item)
        {
            if (AttachPart(item, out _) != null) return true;
            return item.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_armorMaterial != null;
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

    }
}
