using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A creature's gear and an item's wearing, as read from the game: the sets a humanoid rolls
    /// one of, the weapons, shield, armour and extras it may be given, where the game equips an
    /// item and whether it shows when worn (its "attach" part, as <c>VisEquipment.AttachItem</c>
    /// finds it, or armour that paints the body). The previews put it on copies (<see cref="Gear"/>).
    /// </summary>
    internal static class PrefabGear
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
                || ReadLoadout(prefab).HasChoices)
            {
                sets.Add("Gear");
            }
            return sets.Count > 0;
        }

        /// <summary>The sets a humanoid rolls one of when it spawns, those with anything in them.</summary>
        public static List<Humanoid.ItemSet> RolledSets(Humanoid humanoid)
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

        /// <summary>A set by its own name, else by what of it is drawn, else by its number.</summary>
        public static string SetName(Humanoid.ItemSet set, int index)
        {
            if (!string.IsNullOrEmpty(set.m_name)) return Naming.FieldLabel(set.m_name);
            var drawn = set.m_items.Where(i => i != null && AttachPart(i, out _) != null).Select(CatalogBuilder.AttackName).Distinct().ToList();
            return drawn.Count > 0 ? string.Join(" + ", drawn) : $"Set {Numbers.Count(index + 1)}, nothing drawn";
        }

        private static readonly HashSet<string> ToldSets = new HashSet<string>();

        /// <summary>Says once per creature which sets it rolls from and what is in each, drawn or not.</summary>
        [Diagnostic]
        private static void Tell(GameObject prefab, List<Humanoid.ItemSet> sets)
        {
            if (!ToldSets.Add(prefab.name)) return;
            var told = sets.Select((set, i) => $"{SetName(set, i)} ({string.Join(", ", set.m_items.Where(x => x != null).Select(x => x.name + (AttachPart(x, out _) != null ? "" : " not drawn")))})");
            Log.Note($"Scry: {prefab.name} rolls one of {Numbers.Count(sets.Count)} gear sets: {string.Join("; ", told)}.");
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
                .Where(i => i != null && i.GetComponent<ItemDrop>().OrNull()?.m_itemData?.IsWeapon() == true)
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

        /// <summary>Whether an item shows when worn: it has a part to hang, or armour that paints the body.</summary>
        public static bool Shows(GameObject item)
        {
            var shared = item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
            return AttachPart(item, out _) != null || shared?.m_armorMaterial != null;
        }

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
            var shared = item != null ? item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared : null;
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

        /// <summary>The part of an item worn on the body, found as <c>VisEquipment.AttachItem</c> finds it.</summary>
        public static GameObject AttachPart(GameObject item, out bool skin)
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

        /// <summary>
        /// The weapon, shield and armour a creature rolls from its lists when it spawns, and the
        /// extras it may be given by chance, as choices: only items that show when worn are
        /// offered. Read fresh from the prefab; the choices made in the panel are kept with the
        /// previews.
        /// </summary>
        public static Loadout ReadLoadout(GameObject prefab)
        {
            if (prefab == null) return new Loadout(null, null, null, null);
            var humanoid = prefab.GetComponent<Humanoid>();
            var extras = new List<Loadout.Extra>();
            if (humanoid.OrNull()?.m_randomItems != null)
            {
                foreach (var random in humanoid.m_randomItems)
                {
                    var item = random?.m_prefab;
                    var shared = item != null ? item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared : null;
                    if (shared != null && Shows(item)) extras.Add(new Loadout.Extra(item.name, (int)shared.m_itemType));
                }
            }

            return humanoid == null
                ? new Loadout(null, null, null, null)
                : new Loadout(Choices(humanoid.m_randomWeapon), Choices(humanoid.m_randomShield), Choices(humanoid.m_randomArmor), extras,
                    (humanoid.m_randomWeapon ?? Array.Empty<GameObject>()).Concat(humanoid.m_defaultItems ?? Array.Empty<GameObject>())
                        .Where(w => w != null && SlotOf(w) == Slot.BothHands).Select(w => w.name),
                    Holdable(humanoid));
        }
    }
}
