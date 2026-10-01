using System;
using System.Collections.Generic;
using System.Globalization;
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
            Add("Weight", Number(shared.m_weight));
            if (shared.m_value > 0) Add("Worth", $"{shared.m_value} coins");
            if (shared.m_maxStackSize > 1) Add("Stacks to", shared.m_maxStackSize.ToString(CultureInfo.InvariantCulture));
            if (shared.m_maxQuality > 1) Add("Quality", $"up to {shared.m_maxQuality}");
            if (!shared.m_teleportable) Add("Portals", "cannot go through");

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
            if (worn && shared.m_armor > 0f) Add("Armour", Number(shared.m_armor) + (upgradable && shared.m_armorPerLevel > 0f ? $", +{Number(shared.m_armorPerLevel)} per quality" : ""));
            Part("item stats", () => Combat(prefab, shared));
            Part("resistances", () => GearResists(shared, worn));
            if (damage.Length > 0 || (worn && shared.m_armor > 0f) || shared.m_blockPower > 1f) Hooked(HookedRule.ItemStats);

            if (shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f)
            {
                var food = new List<string>();
                if (shared.m_food > 0f) food.Add($"{Number(shared.m_food)} health");
                if (shared.m_foodStamina > 0f) food.Add($"{Number(shared.m_foodStamina)} stamina");
                if (shared.m_foodEitr > 0f) food.Add($"{Number(shared.m_foodEitr)} eitr");
                Add("Food", string.Join(", ", food));
                if (shared.m_foodRegen > 0f) Add("Heals", $"{Number(shared.m_foodRegen)} a tick");
                if (shared.m_foodBurnTime > 0f) Add("Lasts", Minutes(shared.m_foodBurnTime));
                Hooked(HookedRule.Food);
            }

            if (shared.m_toolTier > 0) Add("Tool tier", shared.m_toolTier.ToString(CultureInfo.InvariantCulture));
            if (Math.Abs(shared.m_movementModifier) > 0.001f) Add("Movement", Percent(shared.m_movementModifier));
            Part("gear", () =>
            {
                foreach (var (label, value) in GearWords.Lines(GearValues(shared))) Add(label, value);
                if (shared.m_fullAdrenalineSE != null) Add("At full adrenaline", EffectName(shared.m_fullAdrenalineSE), "se:" + shared.m_fullAdrenalineSE.name);
            });
            // The set's own name is an id the game never shows; the rest of the set is linked under LINKED.
            if (shared.m_setStatusEffect != null)
            {
                var pieces = shared.m_setSize > 0 ? $" ({shared.m_setSize} pieces)" : "";
                Add("Set bonus", EffectName(shared.m_setStatusEffect) + pieces, "se:" + shared.m_setStatusEffect.name);
            }
            foreach (var (damageType, name) in DamageEffects)
            {
                var amount = damageType == "fire" ? shared.m_damages.m_fire : damageType == "frost" ? shared.m_damages.m_frost : damageType == "lightning" ? shared.m_damages.m_lightning
                    : damageType == "poison" ? shared.m_damages.m_poison : shared.m_damages.m_spirit;
                var effect = amount > 0f && ObjectDB.instance != null ? ObjectDB.instance.GetStatusEffect(name.GetStableHashCode()) : null;
                if (effect != null) Add(Naming.FieldLabel(damageType) + " damage causes", EffectName(effect), "se:" + effect.name);
            }
            if (shared.m_equipStatusEffect != null) Add("When worn", EffectName(shared.m_equipStatusEffect), "se:" + shared.m_equipStatusEffect.name);
            if (shared.m_consumeStatusEffect != null) Add("When used", EffectName(shared.m_consumeStatusEffect), "se:" + shared.m_consumeStatusEffect.name);
            if (shared.m_attackStatusEffect != null) Add("On hit", EffectName(shared.m_attackStatusEffect), "se:" + shared.m_attackStatusEffect.name);

            // An egg hatches where it is kept right (EggGrow.CanGrow).
            var egg = prefab.GetComponent<EggGrow>();
            if (egg != null && egg.m_grownPrefab != null)
            {
                Add("Hatches into", AnyName(egg.m_grownPrefab, egg.m_grownPrefab.name) + (egg.m_tamed ? ", tame" : ""), egg.m_grownPrefab.name);
                Add("Hatches in", Naming.Duration(egg.m_growTime));
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
                var title = station.Length > 0
                    ? $"Made at {station}{(recipe.m_minStationLevel > 1 ? $" level {recipe.m_minStationLevel}" : "")}"
                    : "Made by hand";
                if (recipe.m_amount > 1) title += $", makes {recipe.m_amount}";
                if (recipe.m_requireOnlyOneIngredient) title += ", from any one of these";
                title = ModWords.AddedBy(title, Knowledge.RecipeMod(recipe.name), Knowledge.ModName(prefab.name));
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
            string PerQuality(float perLevel) => upgradable && perLevel > 0f ? $", +{Number(perLevel)} per quality" : "";

            if ((weapon || ammo || shield || type == ItemDrop.ItemData.ItemType.Tool) && shared.m_skillType != Skills.SkillType.None)
            {
                var skill = SkillWords.Name(shared.m_skillType.ToString(), CatalogBuilder.Localize);
                if (skill == SkillWords.ModSkill) AddUnsure("Skill", skill, UnsureWords.ModSkill);
                else Add("Skill", skill);
            }

            if (weapon || shield)
            {
                // The game tells blocking only above 1, as AddBlockTooltip does.
                if (shared.m_blockPower > 1f) Add("Block", Number(shared.m_blockPower) + PerQuality(shared.m_blockPowerPerLevel));
                if (shared.m_deflectionForce > 1f) Add("Block force", Number(shared.m_deflectionForce) + PerQuality(shared.m_deflectionForcePerLevel));
                if (shared.m_timedBlockBonus > 1f) Add("Parry bonus", $"\u00d7{Number(shared.m_timedBlockBonus)}");
            }

            if ((weapon || ammo) && shared.m_attackForce > 0f) Add("Knockback", Number(shared.m_attackForce));
            if (weapon && shared.m_backstabBonus > 1f) Add("Backstab", $"\u00d7{Number(shared.m_backstabBonus)}");

            var attack = shared.m_attack;
            if (weapon && attack != null)
            {
                var costs = CombatWords.Costs(attack.m_attackStamina, attack.m_attackEitr, attack.m_attackHealth, attack.m_attackHealthPercentage);
                if (costs.Count > 0) Add("Each attack costs", string.Join(", ", costs));
                if (attack.m_drawStaminaDrain > 0f) Add("Drawing costs", $"{Number(attack.m_drawStaminaDrain)} stamina a second");

                // A second attack the game offers only with an animation of its own (ItemData.HaveSecondaryAttack), told against the first.
                var second = shared.m_secondaryAttack;
                if (second != null && !string.IsNullOrEmpty(second.m_attackAnimation))
                {
                    float Against(float mine, float first) => mine / Math.Max(0.001f, first);
                    Add("Secondary attack", CombatWords.SecondaryAttack(Against(second.m_damageMultiplier, attack.m_damageMultiplier), Against(second.m_forceMultiplier, attack.m_forceMultiplier),
                        Against(second.m_staggerMultiplier, attack.m_staggerMultiplier), CombatWords.Costs(second.m_attackStamina, second.m_attackEitr, second.m_attackHealth, second.m_attackHealthPercentage)));
                }
            }

            var recipe = ObjectDB.instance != null ? ObjectDB.instance.m_recipes.FirstOrDefault(r => r != null && r.m_enabled && r.m_item != null && r.m_item.gameObject.name == prefab.name) : null;
            if (shared.m_useDurability)
            {
                Add("Durability", Number(shared.m_maxDurability) + PerQuality(shared.m_durabilityPerLevel));
                // Repaired where it is made or at its repair station, from the recipe's station level (InventoryGui.CanRepair).
                // Unity's own null check, not ??, which a destroyed reference would pass.
                var at = recipe == null ? null : recipe.m_repairStation != null ? recipe.m_repairStation : recipe.m_craftingStation;
                if (shared.m_canBeReparied && at != null)
                {
                    var level = recipe.m_minStationLevel > 1 ? $" level {recipe.m_minStationLevel}" : "";
                    Add("Repaired at", CatalogBuilder.Localize(at.m_name) + level, at.gameObject.name);
                }
            }

            var station = recipe == null ? null : recipe.m_craftingStation != null ? recipe.m_craftingStation : recipe.m_repairStation;
            if (upgradable && station != null)
            {
                var first = recipe.GetRequiredStationLevel(2);
                var last = recipe.GetRequiredStationLevel(shared.m_maxQuality);
                Add("Upgrades need", $"{CatalogBuilder.Localize(station.m_name)} level {DropWords.Range(first, last)}, one more for each quality", station.gameObject.name);
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
            if (ByDegree(mods).Count > 0) Resists(mods, worn ? "Damage it takes while worn" : "Damage it takes while blocking");
            else if (worn || type == ItemDrop.ItemData.ItemType.Shield) Add(worn ? "Resists while worn" : "Resists while blocking", "nothing");
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
            var row = new Row { Title = "Past its top quality, at " + UpgradeStationName, TitleLink = Knowledge.UpgradeStation };
            if (recipe.m_resources == null || maxQuality <= 1) return row;
            foreach (var need in recipe.m_resources)
            {
                if (need?.m_resItem == null || !need.m_upgraderResource) continue;
                row.Items.Add(new Ingredient
                {
                    Icon = Icon(need.m_resItem.gameObject), Name = ItemName(need.m_resItem.gameObject),
                    Amount = need.GetAmount(maxQuality + 1).ToString(CultureInfo.InvariantCulture), Prefab = need.m_resItem.gameObject.name,
                });
            }
            return row;
        }

        /// <summary>
        /// Damage by type, the biggest first, in the same words everywhere: an item's, a creature's
        /// attack's. The game's plain <c>m_damage</c>, which no resistance lessens, is "true".
        /// </summary>
        private static string Damages(HitData.DamageTypes d)
        {
            return CombatWords.Damage(new[]
            {
                ("true", d.m_damage), ("blunt", d.m_blunt), ("slash", d.m_slash), ("pierce", d.m_pierce), ("chop", d.m_chop), ("pickaxe", d.m_pickaxe),
                ("fire", d.m_fire), ("frost", d.m_frost), ("lightning", d.m_lightning), ("poison", d.m_poison), ("spirit", d.m_spirit),
            }) ?? "";
        }
    }
}
