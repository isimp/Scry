using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What each skill works on in this world, read once from the catalog's prefabs and the
    /// game's own figures (<see cref="SkillInputs"/>): the items that train it and how they
    /// attack, draw and reload; axes hitting trees by another skill; the build tools whose
    /// stamina or wear it cuts; the stations it speeds and the cooking stations and plants it
    /// gives bonuses at; the player's own figures for moving; the fishing float's; the shields
    /// and summons its casting gives. Each skill also keeps what its page lists of these.
    /// </summary>
    internal static class SkillReading
    {
        /// <summary>Leaving a world forgets what was read of it (<see cref="WorldCaches"/>).</summary>
        static SkillReading() => WorldCaches.Register(nameof(SkillReading), Forget);

        /// <summary>The prefabs a skill's page lists: what trains it, the tools it eases, the stations it speeds, what cooks or grows with a bonus by it.</summary>
        internal sealed class Related
        {
            public readonly List<string> Trains = new List<string>();
            public readonly List<string> Tools = new List<string>();
            public readonly List<string> Stations = new List<string>();
            public readonly List<string> Cooking = new List<string>();
            public readonly List<string> Picked = new List<string>();
        }

        private static Dictionary<int, (SkillInputs Inputs, Related Related)> _read;

        /// <summary>What each summon's strength grows with, by its prefab's name: its summoner's skill and what each level adds.</summary>
        private static readonly Dictionary<string, (Skills.SkillType Skill, float PerLevel)> Summoned = new Dictionary<string, (Skills.SkillType, float)>(StringComparer.Ordinal);

        /// <summary>The skill a summon's hits grow with and what each level adds (<c>SpawnAbility.Spawn</c>); none for what nothing summons so.</summary>
        public static (Skills.SkillType Skill, float PerLevel)? SummonedBy(string prefab)
        {
            Of(Skills.SkillType.None);
            return Summoned.TryGetValue(prefab ?? "", out var by) ? by : ((Skills.SkillType, float)?)null;
        }

        /// <summary>The skill each shield's absorbing grows with, by the shield effect's name.</summary>
        private static readonly Dictionary<string, Skills.SkillType> ShieldSkills = new Dictionary<string, Skills.SkillType>(StringComparer.Ordinal);

        /// <summary>The skill a shield's absorbing grows with (<c>SE_Shield.SetLevel</c>), by the area putting it on; none where none does.</summary>
        public static Skills.SkillType ShieldSkill(string effect)
        {
            Of(Skills.SkillType.None);
            return ShieldSkills.TryGetValue(effect ?? "", out var skill) ? skill : Skills.SkillType.None;
        }

        /// <summary>What a skill works on, read once a world; a skill nothing uses has only its name.</summary>
        public static SkillInputs Inputs(Skills.SkillType skill) => Of(skill).Inputs;

        /// <summary>What a skill's page lists, read once a world.</summary>
        public static Related RelatedTo(Skills.SkillType skill) => Of(skill).Related;

        private static (SkillInputs Inputs, Related Related) Of(Skills.SkillType skill)
        {
            if (_read == null) Read(WorldCatalog.Current?.All ?? Enumerable.Empty<Entry>());
            return For(skill);
        }

        /// <summary>
        /// Reads what every skill works on from the catalog's entries, once a world: with the
        /// catalog, so no page drawn pays for it; again only after the world is left.
        /// </summary>
        public static void Read(IEnumerable<Entry> entries)
        {
            _read = new Dictionary<int, (SkillInputs, Related)>();
            ShieldSkills.Clear();
            Summoned.Clear();
            Guard.Run(Feature.SkillPages, "what the skills work on", ReadAll, entries);
        }

        private static (SkillInputs Inputs, Related Related) For(Skills.SkillType skill)
        {
            if (!_read.TryGetValue((int)skill, out var read)) _read[(int)skill] = read = (new SkillInputs { Name = skill.ToString() }, new Related());
            return read;
        }

        private static void ReadAll(IEnumerable<Entry> entries)
        {
            Moving();
            var gui = InventoryGui.instance;
            foreach (var entry in entries)
            {
                // Only items, pieces (stations) and resources (what is picked) work by a skill.
                if (entry.Kind != Kind.Item && entry.Kind != Kind.Piece && entry.Kind != Kind.Resource) continue;
                if (!(entry.Source is GameObject prefab) || prefab == null) continue;
                Guard.Each(Feature.SkillPages, "what the skills work on", prefab.name, () =>
                {
                    if (entry.Kind == Kind.Item) Item(entry, prefab);
                    var station = prefab.GetComponent<CraftingStation>();
                    if (station != null && station.m_craftingSkill != Skills.SkillType.None && gui != null)
                    {
                        var (inputs, related) = For(station.m_craftingSkill);
                        inputs.Crafts = true;
                        inputs.CraftMostSaved = gui.m_craftDurationSkillMaxDecrease;
                        inputs.CraftBonus = gui.m_craftBonusChance;
                        related.Stations.Add(prefab.name);
                    }
                    var cooking = prefab.GetComponent<CookingStation>();
                    if (cooking != null && cooking.m_skill != Skills.SkillType.None && cooking.m_canGiveBonusYield && gui != null)
                    {
                        var (inputs, related) = For(cooking.m_skill);
                        inputs.CookBonus = gui.m_craftBonusChance;
                        related.Cooking.Add(prefab.name);
                    }
                    var pickable = prefab.GetComponent<Pickable>();
                    if (pickable != null && pickable.m_pickRaiseSkill != Skills.SkillType.None)
                    {
                        var (inputs, related) = For(pickable.m_pickRaiseSkill);
                        inputs.PickBonus = Math.Max(inputs.PickBonus, pickable.m_maxLevelBonusChance);
                        related.Picked.Add(prefab.name);
                    }
                });
            }
            Shields();
        }

        /// <summary>The player's own figures for moving, from the character in the world or else the player's prefab.</summary>
        private static void Moving()
        {
            var player = Player.m_localPlayer != null ? Player.m_localPlayer : GamePrefabs.Person.OrNull()?.GetComponent<Player>();
            if (player == null) return;
            For(Skills.SkillType.Run).Inputs.RunSpeed = player.m_runSpeed;
            For(Skills.SkillType.Run).Inputs.RunDrain = player.m_runStaminaDrain;
            For(Skills.SkillType.Swim).Inputs.SwimAtNone = player.m_swimStaminaDrainMinSkill;
            For(Skills.SkillType.Swim).Inputs.SwimAtTop = player.m_swimStaminaDrainMaxSkill;
            For(Skills.SkillType.Dodge).Inputs.Dodge = player.m_dodgeStaminaUsage;
            For(Skills.SkillType.Sneak).Inputs.Sneak = player.m_sneakStaminaDrain;
        }

        /// <summary>An item that trains a skill, or whose attacks, building or placing work by one.</summary>
        private static void Item(Entry entry, GameObject prefab)
        {
            var shared = prefab.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
            if (shared == null || !Grouping.Obtainable(entry)) return;
            if (shared.m_skillType != Skills.SkillType.None && Trains(shared.m_itemType))
            {
                var (inputs, related) = For(shared.m_skillType);
                related.Trains.Add(prefab.name);
                if (shared.m_attack != null) inputs.HitRises[prefab.name] = SkillMath.RisePerHit(shared.m_attack.m_attackType.ToString(), shared.m_attack.m_raiseSkillAmount);
                var deals = Facts.DamageFigures(shared.m_damages).Any(d => d.Amount > 0f);
                foreach (var attack in Attacks(shared))
                {
                    if (deals || attack.m_attackProjectile != null) inputs.Hits = true;
                    if (attack.m_attackStamina > 0f || attack.m_attackEitr > 0f || attack.m_attackHealth > 0f || attack.m_attackHealthPercentage > 0f) inputs.Costs = true;
                    if (attack.m_bowDraw && attack.m_drawDurationMin > 0f) inputs.DrawTimes.Add(attack.m_drawDurationMin);
                    if (attack.m_bowDraw && (attack.m_drawStaminaDrain > 0f || attack.m_drawEitrDrain > 0f)) inputs.DrawDrains = true;
                    if (attack.m_requiresReload && attack.m_reloadTime > 0f) inputs.ReloadTimes.Add(attack.m_reloadTime);
                    if (attack.m_specialHitSkill != Skills.SkillType.None && (attack.m_specialHitType & DestructibleType.Tree) != 0) For(attack.m_specialHitSkill).Inputs.OnTrees = true;
                    if (attack.m_harvest && attack.m_harvestRadiusMaxLevel > 0f)
                    {
                        var farming = For(Skills.SkillType.Farming);
                        farming.Inputs.ScytheAtNone = attack.m_harvestRadius;
                        farming.Inputs.ScytheAtTop = attack.m_harvestRadiusMaxLevel;
                        if (!farming.Related.Tools.Contains(prefab.name)) farming.Related.Tools.Add(prefab.name);
                    }
                    Summons(shared, attack);
                    Fishing(attack);
                }
            }
            var table = shared.m_buildPieces;
            if (table != null && table.m_skill != Skills.SkillType.None)
            {
                var (inputs, related) = For(table.m_skill);
                inputs.BuildStamina = true;
                if (!related.Tools.Contains(prefab.name)) related.Tools.Add(prefab.name);
            }
            if (shared.m_placementDurabilitySkill != Skills.SkillType.None && shared.m_placementDurabilityMax > 0f)
            {
                var (inputs, related) = For(shared.m_placementDurabilitySkill);
                inputs.WearSaved = Math.Max(inputs.WearSaved, shared.m_placementDurabilityMax);
                if (!related.Tools.Contains(prefab.name)) related.Tools.Add(prefab.name);
            }
        }

        /// <summary>An item's attacks: its first, and its second where it has an animation of its own (<c>ItemData.HaveSecondaryAttack</c>).</summary>
        internal static IEnumerable<Attack> Attacks(ItemDrop.ItemData.SharedData shared)
        {
            if (shared.m_attack != null) yield return shared.m_attack;
            if (shared.m_secondaryAttack != null && !string.IsNullOrEmpty(shared.m_secondaryAttack.m_attackAnimation)) yield return shared.m_secondaryAttack;
        }

        /// <summary>A staff's summons, by the skill whose level sets how many it keeps at once, and what each level adds to their hits (<c>SpawnAbility.Spawn</c>).</summary>
        private static void Summons(ItemDrop.ItemData.SharedData shared, Attack attack)
        {
            var spawn = attack.m_attackProjectile != null ? attack.m_attackProjectile.GetComponent<SpawnAbility>() : null;
            if (spawn != null && spawn.m_copySkill != Skills.SkillType.None && spawn.m_copySkillToRandomFactor > 0f && spawn.m_spawnPrefab != null)
            {
                foreach (var spawned in spawn.m_spawnPrefab) if (spawned != null) Summoned[spawned.name] = (spawn.m_copySkill, spawn.m_copySkillToRandomFactor);
            }
            if (spawn == null || spawn.m_levelUpSettings == null || spawn.m_levelUpSettings.Count == 0 || spawn.m_setMaxInstancesFromWeaponLevel) return;
            var skill = spawn.m_levelUpSettings[0].m_skill;
            if (skill == Skills.SkillType.None || spawn.m_levelUpSettings.Any(s => s.m_skill != skill)) return;
            var inputs = For(skill).Inputs;
            var name = CatalogBuilder.Localize(shared.m_name);
            if (inputs.Summons.Any(s => s.Staff == name)) return;
            inputs.Summons.Add((name, spawn.m_levelUpSettings.Select(s => s.m_skillLevel).ToArray(), spawn.m_levelUpSettings.Select(s => s.m_maxSpawns).ToArray()));
            if (spawn.m_copySkill == skill && spawn.m_copySkillToRandomFactor > 0f) inputs.SummonPerLevel = spawn.m_copySkillToRandomFactor;
        }

        /// <summary>A fishing rod's float, its drains and reeling at no skill and at the top (<c>FishingFloat.FixedUpdate</c>).</summary>
        private static void Fishing(Attack attack)
        {
            var fishing = attack.m_attackProjectile != null ? attack.m_attackProjectile.GetComponent<FishingFloat>() : null;
            if (fishing == null) return;
            var inputs = For(Skills.SkillType.Fishing).Inputs;
            inputs.HookedAtNone = fishing.m_hookedStaminaPerSec;
            inputs.HookedAtTop = fishing.m_hookedStaminaPerSecMaxSkill;
            inputs.PullAtTop = fishing.m_pullStaminaUseMaxSkillMultiplier;
            inputs.LineAtNone = fishing.m_pullLineSpeed;
            inputs.LineAtTop = fishing.m_pullLineSpeedMaxSkill;
        }

        /// <summary>
        /// Each shield whose absorbing grows with a skill (<c>SE_Shield.SetLevel</c>), by the skill
        /// of the area that puts it on (<c>Aoe.m_skill</c>, the level it hands the shield).
        /// </summary>
        private static void Shields()
        {
            var effects = ObjectDB.instance != null ? ObjectDB.instance.m_StatusEffects : null;
            if (effects == null) return;
            foreach (var effect in effects)
            {
                if (!(effect is SE_Shield shield) || shield.m_absorbDamagePerSkillLevel <= 0f) continue;
                foreach (var (giver, given, _) in Knowledge.Givers())
                {
                    if (given != shield.name) continue;
                    var aoe = GamePrefabs.Named(giver).OrNull()?.GetComponentInChildren<Aoe>(true);
                    if (aoe == null || aoe.m_skill == Skills.SkillType.None || aoe.m_skill == Skills.SkillType.All) continue;
                    ShieldSkills[shield.name] = aoe.m_skill;
                    var inputs = For(aoe.m_skill).Inputs;
                    inputs.ShieldListed = shield.m_absorbDamage;
                    inputs.ShieldPerLevel = shield.m_absorbDamagePerSkillLevel;
                    break;
                }
            }
        }

        /// <summary>The items whose skill the game trains with them: weapons, torches, ammo, shields and tools.</summary>
        internal static bool Trains(ItemDrop.ItemData.ItemType type)
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

        /// <summary>Forgets what was read, for another world.</summary>
        public static void Forget()
        {
            _read = null;
            ShieldSkills.Clear();
            Summoned.Clear();
        }
    }
}
