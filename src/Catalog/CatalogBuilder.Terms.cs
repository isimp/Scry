using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the search's own terms read of each entry, once every entry is made, linked and
    /// grouped: what it is (is:, <see cref="SearchFlags"/>) and how it fights (weak:, resists:,
    /// immune:, damage:, skill:, <see cref="SearchFight"/>), as its details tell the same.
    /// </summary>
    internal static partial class CatalogBuilder
    {
        /// <summary>Reads the terms' words into every entry, a slice of entries a step.</summary>
        private static IEnumerable<int> SearchTerms(List<Entry> entries, int slice)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                Guard.Each(Feature.SearchTerms, "search terms", entry.Name, () =>
                {
                    entry.SetTermWords("is", SearchFlags.Of(FlagFactsOf(entry)));
                    if (entry.Source is GameObject prefab && prefab != null && !EntryKeys.HasOwnNamespace(entry.Kind)) Fight(entry, prefab);
                });
                if ((i + 1) % slice == 0) yield return i + 1;
            }
        }

        /// <summary>
        /// The fight terms' words: a creature's resistances as its grid shows them and the damage
        /// its attacks deal; worn armour's resistances while worn (<c>Player.ApplyArmorDamageMods</c>);
        /// an item's damage and the skill a weapon, ammo, shield or tool trains; a projectile's damage.
        /// </summary>
        private static void Fight(Entry entry, GameObject prefab)
        {
            var character = prefab.GetComponent<Character>();
            if (character != null)
            {
                SetTaking(entry, ResistWords.ForCreature(Facts.Cells(character.m_damageModifiers)));
                var dealt = new List<(string, float)>();
                foreach (var item in Relations.CarriedItems(prefab))
                {
                    var carried = item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
                    if (carried?.m_attack != null) dealt.AddRange(Facts.DamageFigures(carried.m_damages));
                }
                entry.SetTermWords("damage", SearchFight.Dealt(dealt));
            }

            var shared = prefab.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
            if (shared != null)
            {
                entry.SetTermWords("damage", SearchFight.Dealt(Facts.DamageFigures(shared.m_damages)));
                var type = shared.m_itemType;
                if (type == ItemDrop.ItemData.ItemType.Helmet || type == ItemDrop.ItemData.ItemType.Chest || type == ItemDrop.ItemData.ItemType.Legs || type == ItemDrop.ItemData.ItemType.Shoulder)
                {
                    var mods = default(HitData.DamageModifiers);
                    if (shared.m_damageModifiers != null) mods.Apply(shared.m_damageModifiers);
                    SetTaking(entry, Facts.Cells(mods));
                }
                if (TrainsSkill(type) && shared.m_skillType != Skills.SkillType.None)
                {
                    entry.SetTermWords("skill", SearchFight.Skill(SkillWords.Name(shared.m_skillType.ToString(), Localize)));
                }
            }

            var projectile = prefab.GetComponent<Projectile>();
            if (projectile != null) entry.SetTermWords("damage", SearchFight.Dealt(Facts.DamageFigures(projectile.m_damage)));
        }

        private static void SetTaking(Entry entry, List<ResistCell> cells)
        {
            entry.SetTermWords("weak", SearchFight.Taking(cells, Tone.Weak));
            entry.SetTermWords("resists", SearchFight.Taking(cells, Tone.Resists));
            entry.SetTermWords("immune", SearchFight.Taking(cells, Tone.Immune));
        }

        /// <summary>The items whose skill the details tell: weapons, torches, ammo, shields and tools.</summary>
        private static bool TrainsSkill(ItemDrop.ItemData.ItemType type)
        {
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Tool:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>What is: asks of an entry, read from its prefab as its details read it.</summary>
        private static FlagFacts FlagFactsOf(Entry entry)
        {
            var facts = new FlagFacts { Silent = entry.Empty, Unsure = entry.Origin == Origin.Mod && !UnsureWords.IsSureClue(entry.ModClue) };
            if (!(entry.Source is GameObject prefab) || prefab == null || EntryKeys.HasOwnNamespace(entry.Kind)) return facts;

            var character = prefab.GetComponent<Character>();
            if (character != null)
            {
                facts.Boss = character.m_boss;
                facts.Flying = character.m_flying;
                facts.Tameable = prefab.GetComponent<Tameable>() != null;
            }

            var shared = prefab.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
            if (shared != null)
            {
                facts.ItemType = shared.m_itemType.ToString();
                facts.Food = shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f;
                facts.Craftable = _recipes != null && _recipes.ContainsKey(prefab.name);
            }

            if (entry.Kind == Kind.Piece) facts.Buildable = Knowledge.Tools.ToolsOf(prefab.name).Count > 0;
            return facts;
        }
    }
}
