using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>An item's facts: its stats as the game's tooltip tells them, its recipe and its upgrades.</summary>
    internal sealed partial class Facts
    {
        // ----- Items -----

        private void Item(GameObject prefab, ItemDrop.ItemData.SharedData shared)
        {
            Description = CatalogBuilder.Localize(shared.m_description);

            Add("Type", Groups.ItemTypeName(shared.m_itemType.ToString()));
            Add("Weight", Numbers.Amount(shared.m_weight));
            if (shared.m_value > 0) Add("Worth", ItemWords.Coins(shared.m_value));
            if (shared.m_maxStackSize > 1) Add("Stacks to", Numbers.Count(shared.m_maxStackSize));
            // Gear tells how far it upgrades even when it cannot; anything else only when it can.
            var gear = IsGear(shared.m_itemType);
            if (gear || shared.m_maxQuality > 1) Add("Quality", ItemWords.Quality(shared.m_maxQuality));
            Add("Portals", ItemWords.Portals(shared.m_teleportable));

            // The game fills in per-quality numbers everywhere; they mean something only for what can be upgraded.
            var upgradable = shared.m_maxQuality > 1;
            var damage = Damages(shared.m_damages);
            if (damage.Length > 0)
            {
                Add("Damage", damage);
                if (upgradable) Add("Per quality", Damages(shared.m_damagesPerLevel));
            }

            var type = shared.m_itemType;
            // The slots whose armour counts (Player.GetBodyArmor); gloves' does not.
            var worn = type == ItemDrop.ItemData.ItemType.Helmet || type == ItemDrop.ItemData.ItemType.Chest
                       || type == ItemDrop.ItemData.ItemType.Legs || type == ItemDrop.ItemData.ItemType.Shoulder;
            if (worn) Add("Armour", ItemWords.Armour(shared.m_armor, upgradable ? shared.m_armorPerLevel : 0f));
            Part("item stats", () => Combat(prefab, shared));
            Part("resistances", () => GearResists(shared, worn));
            if (damage.Length > 0 || (worn && shared.m_armor > 0f) || shared.m_blockPower > 1f) Hooked(HookedRule.ItemStats);

            if (shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f)
            {
                Add("Food", ItemWords.Food(shared.m_food, shared.m_foodStamina, shared.m_foodEitr));
                if (shared.m_foodRegen > 0f) Add("Heals", ItemWords.Heals(shared.m_foodRegen));
                if (shared.m_foodBurnTime > 0f) Add("Lasts", Numbers.Duration(shared.m_foodBurnTime));
                Hooked(HookedRule.Food);
            }

            if (shared.m_toolTier > 0) Add("Tool tier", Numbers.Count(shared.m_toolTier));
            // What gear changes while worn; armour, a shield and what is worn for its effects say so when nothing.
            var wornFor = worn || type == ItemDrop.ItemData.ItemType.Shield || type == ItemDrop.ItemData.ItemType.Utility || type == ItemDrop.ItemData.ItemType.Trinket;
            if (Math.Abs(shared.m_movementModifier) > 0.001f) Add("Movement", Numbers.Percent(shared.m_movementModifier, 0, signed: true));
            else if (wornFor) Add("Movement", "no change");
            Part("gear", () =>
            {
                var lines = GearWords.Lines(GearValues(shared));
                foreach (var (label, value) in lines) Add(label, value);
                if (shared.m_fullAdrenalineSE != null) Add("At full adrenaline", EffectName(shared.m_fullAdrenalineSE), EntryKeys.For(Kind.StatusEffect, shared.m_fullAdrenalineSE.name));
                if (wornFor && lines.Count == 0 && shared.m_fullAdrenalineSE == null && shared.m_equipStatusEffect == null) Add("Other changes while worn", "none");
            });
            // The set's own name is an id the game never shows; the rest of the set is linked under LINKED.
            if (shared.m_setStatusEffect != null)
            {
                Add("Set bonus", ItemWords.SetBonus(EffectName(shared.m_setStatusEffect), shared.m_setSize), EntryKeys.For(Kind.StatusEffect, shared.m_setStatusEffect.name));
            }
            else if (worn) Add("Set bonus", "none");
            foreach (var (damageType, name) in CombatWords.DamageEffects)
            {
                var amount = damageType == "fire" ? shared.m_damages.m_fire : damageType == "frost" ? shared.m_damages.m_frost : damageType == "lightning" ? shared.m_damages.m_lightning
                    : damageType == "poison" ? shared.m_damages.m_poison : shared.m_damages.m_spirit;
                var effect = amount > 0f && ObjectDB.instance != null ? ObjectDB.instance.GetStatusEffect(name.GetStableHashCode()) : null;
                if (effect != null) Add(CombatWords.DamageCauses(damageType), EffectName(effect), EntryKeys.For(Kind.StatusEffect, effect.name));
            }
            if (shared.m_equipStatusEffect != null) Add("When worn", EffectName(shared.m_equipStatusEffect), EntryKeys.For(Kind.StatusEffect, shared.m_equipStatusEffect.name));
            if (shared.m_consumeStatusEffect != null) Add("When used", EffectName(shared.m_consumeStatusEffect), EntryKeys.For(Kind.StatusEffect, shared.m_consumeStatusEffect.name));
            if (shared.m_attackStatusEffect != null) Add("On hit", EffectName(shared.m_attackStatusEffect), EntryKeys.For(Kind.StatusEffect, shared.m_attackStatusEffect.name));

            // An egg hatches where it is kept right (EggGrow.CanGrow).
            var egg = prefab.GetComponent<EggGrow>();
            if (egg != null && egg.m_grownPrefab != null)
            {
                Add("Hatches into", BreedWords.HatchesInto(AnyName(egg.m_grownPrefab, egg.m_grownPrefab.name), egg.m_tamed), egg.m_grownPrefab.name);
                Add("Hatches in", Numbers.Duration(egg.m_growTime));
                Add("Hatches when", BreedWords.Hatches(egg.m_requireNearbyFire, egg.m_requireUnderRoof, egg.m_requireCoverPercentige));
            }

            Part("recipe", () => Recipes(prefab, shared));
            Part("made at stations", () => MadeIn(prefab));
        }

        /// <summary>Each enabled recipe that makes the item, with its upgrade kits.</summary>
        private void Recipes(GameObject prefab, ItemDrop.ItemData.SharedData shared)
        {
            var db = ObjectDB.instance;
            if (db == null) return;
            var made = false;
            foreach (var recipe in db.m_recipes)
            {
                if (recipe == null || recipe.m_item == null || recipe.m_item.gameObject.name != prefab.name || !recipe.m_enabled) continue;

                var station = recipe.m_craftingStation != null ? CatalogBuilder.Localize(recipe.m_craftingStation.m_name) : "";
                var title = ModWords.AddedBy(ItemWords.RecipeTitle(station, recipe.m_minStationLevel, recipe.m_amount, recipe.m_requireOnlyOneIngredient),
                    Knowledge.RecipeMod(recipe.name), Knowledge.ModName(prefab.name));
                var row = Requirements(title, recipe.m_resources, shared.m_maxQuality > 1);
                row.TitleLink = recipe.m_craftingStation != null ? recipe.m_craftingStation.gameObject.name : null;
                Rows.Add(row);

                var kits = UpgradeKits(recipe, shared.m_maxQuality);
                if (kits.Items.Count > 0) Rows.Add(kits);
                made = true;
            }
            if (made) Hooked(HookedRule.Crafting);
        }

        /// <summary>
        /// What the game's own tooltip tells of a weapon, shield, tool or ammo besides its damage
        /// (<c>ItemDrop.ItemData.GetTooltip</c>, <c>AddBlockTooltip</c>): the skill it trains,
        /// durability and where it is repaired, blocking and parrying, knockback, backstab, and
        /// what an attack costs; and for what can be upgraded, the station level each quality needs
        /// (<c>Recipe.GetRequiredStationLevel</c>: one more for each quality).
        /// </summary>
        private void Combat(GameObject prefab, ItemDrop.ItemData.SharedData shared)
        {
            var type = shared.m_itemType;
            var weapon = type == ItemDrop.ItemData.ItemType.OneHandedWeapon || type == ItemDrop.ItemData.ItemType.TwoHandedWeapon
                         || type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || type == ItemDrop.ItemData.ItemType.Bow || type == ItemDrop.ItemData.ItemType.Torch;
            var ammo = type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable;
            var shield = type == ItemDrop.ItemData.ItemType.Shield;
            var upgradable = shared.m_maxQuality > 1;
            float PerQuality(float perLevel) => upgradable ? perLevel : 0f;

            if ((weapon || ammo || shield || type == ItemDrop.ItemData.ItemType.Tool) && shared.m_skillType != Skills.SkillType.None)
            {
                var skill = SkillWords.Name(shared.m_skillType.ToString(), CatalogBuilder.Localize);
                if (skill == SkillWords.ModSkill) AddUnsure("Skill", skill, UnsureWords.ModSkill);
                else Add("Skill", skill);
            }

            if (weapon || shield)
            {
                // The game tells blocking only above 1, as AddBlockTooltip does; below that it cannot block.
                Add("Block", ItemWords.Block(shared.m_blockPower, PerQuality(shared.m_blockPowerPerLevel)));
                if (shared.m_deflectionForce > 1f) Add("Block force", ItemWords.PerQuality(shared.m_deflectionForce, PerQuality(shared.m_deflectionForcePerLevel)));
                if (shared.m_timedBlockBonus > 1f) Add("Parry bonus", Numbers.Times(shared.m_timedBlockBonus));
            }

            if ((weapon || ammo) && shared.m_attackForce > 0f) Add("Knockback", Numbers.Amount(shared.m_attackForce));
            if (weapon && shared.m_backstabBonus > 1f) Add("Backstab", Numbers.Times(shared.m_backstabBonus));

            var attack = shared.m_attack;
            if (weapon && attack != null)
            {
                var costs = CombatWords.Costs(attack.m_attackStamina, attack.m_attackEitr, attack.m_attackHealth, attack.m_attackHealthPercentage);
                Add("Each attack costs", costs);
                if (attack.m_drawStaminaDrain > 0f) Add("Drawing costs", CombatWords.DrawCost(attack.m_drawStaminaDrain));

                // A second attack the game offers only with an animation of its own (ItemData.HaveSecondaryAttack), told against the first.
                var second = shared.m_secondaryAttack;
                if (second != null && !string.IsNullOrEmpty(second.m_attackAnimation))
                {
                    float Against(float mine, float first) => mine / Math.Max(0.001f, first);
                    Add("Secondary attack", CombatWords.SecondaryAttack(Against(second.m_damageMultiplier, attack.m_damageMultiplier), Against(second.m_forceMultiplier, attack.m_forceMultiplier),
                        Against(second.m_staggerMultiplier, attack.m_staggerMultiplier), CombatWords.Costs(second.m_attackStamina, second.m_attackEitr, second.m_attackHealth, second.m_attackHealthPercentage)));
                }
                else Add("Secondary attack", "none");
            }

            var recipe = ObjectDB.instance != null ? ObjectDB.instance.m_recipes.FirstOrDefault(r => r != null && r.m_enabled && r.m_item != null && r.m_item.gameObject.name == prefab.name) : null;
            if (shared.m_useDurability)
            {
                Add("Durability", ItemWords.PerQuality(shared.m_maxDurability, PerQuality(shared.m_durabilityPerLevel)));
                // Repaired where it is made or at its repair station, from the recipe's station level (InventoryGui.CanRepair);
                // where it cannot be, that is said too. Unity's own null check, not ??, which a destroyed reference would pass.
                var at = recipe == null ? null : recipe.m_repairStation != null ? recipe.m_repairStation : recipe.m_craftingStation;
                Add("Repaired at", ItemWords.Repair(shared.m_canBeReparied, at != null ? CatalogBuilder.Localize(at.m_name) : null, recipe != null ? recipe.m_minStationLevel : 1),
                    shared.m_canBeReparied && at != null ? at.gameObject.name : null);
            }
            else if (IsGear(shared.m_itemType)) Add("Durability", ItemWords.NoWear);

            var station = recipe == null ? null : recipe.m_craftingStation != null ? recipe.m_craftingStation : recipe.m_repairStation;
            if (upgradable && station != null)
            {
                var first = recipe.GetRequiredStationLevel(2);
                var last = recipe.GetRequiredStationLevel(shared.m_maxQuality);
                Add("Upgrades need", ItemWords.UpgradesNeed(CatalogBuilder.Localize(station.m_name), first, last), station.gameObject.name);
            }
        }

        /// <summary>
        /// What armour resists while worn (<c>Player.ApplyArmorDamageMods</c>: chest, legs, helmet
        /// and cape) and what a shield or weapon resists while blocking with it
        /// (<c>Humanoid.BlockAttack</c>); anything else's own resistances the game never uses.
        /// </summary>
        private void GearResists(ItemDrop.ItemData.SharedData shared, bool worn)
        {
            var type = shared.m_itemType;
            if (shared.m_damageModifiers == null) return;
            var blocks = type == ItemDrop.ItemData.ItemType.Shield || type == ItemDrop.ItemData.ItemType.OneHandedWeapon || type == ItemDrop.ItemData.ItemType.TwoHandedWeapon
                         || type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft;
            if (!worn && !blocks) return;
            var mods = default(HitData.DamageModifiers);
            mods.Apply(shared.m_damageModifiers);
            if (ByDegree(mods).Count > 0) Resists(mods, ItemWords.DamageTaken(worn));
            else if (worn || type == ItemDrop.ItemData.ItemType.Shield) Add(ItemWords.ResistsNothing(worn), "nothing");
        }

        /// <summary>What is worn or wielded: weapons, shields, tools, torches, armour, belts and trinkets.</summary>
        private static bool IsGear(ItemDrop.ItemData.ItemType type)
        {
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Hands:
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Trinket:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>What gear changes while worn, by its field names, for <see cref="GearWords"/>.</summary>
        private static Dictionary<string, float> GearValues(ItemDrop.ItemData.SharedData shared) => new Dictionary<string, float>
        {
            ["m_eitrRegenModifier"] = shared.m_eitrRegenModifier,
            ["m_homeItemsStaminaModifier"] = shared.m_homeItemsStaminaModifier,
            ["m_heatResistanceModifier"] = shared.m_heatResistanceModifier,
            ["m_jumpStaminaModifier"] = shared.m_jumpStaminaModifier,
            ["m_attackStaminaModifier"] = shared.m_attackStaminaModifier,
            ["m_blockStaminaModifier"] = shared.m_blockStaminaModifier,
            ["m_dodgeStaminaModifier"] = shared.m_dodgeStaminaModifier,
            ["m_swimStaminaModifier"] = shared.m_swimStaminaModifier,
            ["m_sneakStaminaModifier"] = shared.m_sneakStaminaModifier,
            ["m_runStaminaModifier"] = shared.m_runStaminaModifier,
            ["m_maxAdrenaline"] = shared.m_maxAdrenaline,
        };

        /// <summary>
        /// The upgrade kits a recipe names. The game asks for them only at a station marked as an
        /// upgrader, which takes an item past its top quality with the kits alone, and leaves them
        /// out everywhere else (<c>Player.HaveRequirements</c>, <c>InventoryGui.SetupRequirementList</c>).
        /// Each shows how many the first step past the top takes.
        /// </summary>
        private static Row UpgradeKits(Recipe recipe, int maxQuality)
        {
            var row = new Row { Title = ItemWords.PastTop(Knowledge.UpgradeStationName), TitleLink = Knowledge.UpgradeStation };
            if (recipe.m_resources == null || maxQuality <= 1) return row;
            foreach (var need in recipe.m_resources)
            {
                if (need?.m_resItem == null || !need.m_upgraderResource) continue;
                row.Items.Add(new Ingredient
                {
                    Icon = Icon(need.m_resItem.gameObject), Name = ItemName(need.m_resItem.gameObject),
                    Amount = Numbers.Count(need.GetAmount(maxQuality + 1)), Prefab = need.m_resItem.gameObject.name,
                });
            }
            return row;
        }

        /// <summary>
        /// Damage by type, the biggest first, in the same words everywhere: an item's, a creature's
        /// attack's. The game's plain <c>m_damage</c>, which no resistance lessens, is "true".
        /// </summary>
        private static string Damages(HitData.DamageTypes d) => CombatWords.Damage(DamageFigures(d)) ?? "";

        /// <summary>A hit's damage by type, as the game keeps it, named as the panel names the types; the search reads the same figures.</summary>
        internal static (string Type, float Amount)[] DamageFigures(HitData.DamageTypes d) => new[]
        {
            ("true", d.m_damage), ("blunt", d.m_blunt), ("slash", d.m_slash), ("pierce", d.m_pierce), ("chop", d.m_chop), ("pickaxe", d.m_pickaxe),
            ("fire", d.m_fire), ("frost", d.m_frost), ("lightning", d.m_lightning), ("poison", d.m_poison), ("spirit", d.m_spirit),
        };
    }
}
