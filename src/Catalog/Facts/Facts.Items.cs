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
            if (damage.Length > 0) Add("Damage", damage);
            // What a hit deals at the character's skill, rolled between two shares of it (Skills.GetRandomSkillRange).
            if (damage.Length > 0 && SkillReading.Trains(shared.m_itemType))
            {
                YoursAt("Damage", shared.m_skillType, level => SkillWords.DamageRange(DamageFigures(shared.m_damages), SkillMath.HitShare(level).Least, SkillMath.HitShare(level).Most));
            }

            var type = shared.m_itemType;
            // The slots whose armour counts (Player.GetBodyArmor); gloves' does not.
            var worn = type == ItemDrop.ItemData.ItemType.Helmet || type == ItemDrop.ItemData.ItemType.Chest
                       || type == ItemDrop.ItemData.ItemType.Legs || type == ItemDrop.ItemData.ItemType.Shoulder;
            // What grows with quality is told in its table, each figure here at the first level.
            if (worn) Add("Armour", ItemWords.Armour(shared.m_armor));
            if (upgradable) Part(worn ? "gear" : "item stats", () => QualityTable(prefab, shared, worn));
            Part("item stats", () => Combat(prefab, shared));
            Part("item stats", () => SkillUses(shared));
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
                var lines = GearWords.Lines(GearModifiers.Of(shared));
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
                Proofs["Hatches in"] = EntryKeys.Did(Deed.Hatched);
                Add("Hatches when", BreedWords.Hatches(egg.m_requireNearbyFire, egg.m_requireUnderRoof, egg.m_requireCoverPercentige));
            }

            Part("recipe", () => Recipes(prefab, shared));
            Part("made at stations", () => MadeIn(prefab));
            Part("mod settings", () => ModSettings(prefab, shared));
            Part("seen equipped", () => SeenEquipped(prefab, shared));
        }

        /// <summary>
        /// What equipping it was seen to change on you in your play (<see cref="WornWatch"/>),
        /// whichever mod made it so: a table with a column for each quality seen.
        /// </summary>
        private void SeenEquipped(GameObject prefab, ItemDrop.ItemData.SharedData shared)
        {
            var seen = WornWatch.Seen.Of(prefab.name);
            if (seen.Count == 0) return;
            var (columns, lines) = WornWords.Table(seen, shared.m_maxQuality);
            // Counted over every character's play on this machine: under the strict rules it stands faint, uncounted, as the drops seen do.
            var row = new Row { Title = WornWords.Title, Columns = columns, Unsure = UnsureWords.SeenWorn, AllPlay = true };
            foreach (var line in lines) row.Lines.Add((line, null));
            Rows.Add(row);
        }

        /// <summary>
        /// The mods' settings for an item, by sections of their configs named after it
        /// (<see cref="ModConfigs"/>): each a table of its settings as the config holds them now,
        /// under the mod's name, which goes to its page. Only the mod knows what it does with them.
        /// </summary>
        private void ModSettings(GameObject prefab, ItemDrop.ItemData.SharedData shared)
        {
            foreach (var (mod, section, settings) in ModConfigs.About(CatalogBuilder.Localize(shared.m_name), prefab.name))
            {
                var page = EntryKeys.For(Kind.Mod, mod);
                var row = new Row
                {
                    Title = ModSettingWords.Title(mod, section), TitleLink = EntryOf(page) != null ? page : null,
                    Columns = ModSettingWords.Columns, Unsure = UnsureWords.ModSettings,
                };
                foreach (var (key, value) in settings) row.Lines.Add((new[] { key, ModSettingWords.Value(value) }, null));
                if (row.Lines.Count > 0) Rows.Add(row);
            }
        }

        /// <summary>Each enabled recipe that makes the item, with its upgrade kits; one the game holds again alike, once (<see cref="RecipeAlike"/>).</summary>
        private void Recipes(GameObject prefab, ItemDrop.ItemData.SharedData shared)
        {
            var db = ObjectDB.instance;
            if (db == null) return;
            var made = false;
            var shown = new RecipeAlike();
            foreach (var recipe in db.m_recipes)
            {
                if (recipe == null || recipe.m_item == null || recipe.m_item.gameObject.name != prefab.name || !recipe.m_enabled) continue;

                var station = recipe.m_craftingStation != null ? CatalogBuilder.Localize(recipe.m_craftingStation.m_name) : "";
                var title = ModWords.AddedBy(ItemWords.RecipeTitle(station, recipe.m_minStationLevel, recipe.m_amount, recipe.m_requireOnlyOneIngredient),
                    Knowledge.RecipeMod(recipe.name), Knowledge.ModName(prefab.name));
                var row = Requirements(title, recipe.m_resources, shared.m_maxQuality > 1);
                row.Recipe = RecipeToken(prefab);
                row.TitleLink = recipe.m_craftingStation != null ? recipe.m_craftingStation.gameObject.name : null;
                var kits = UpgradeKits(recipe, shared.m_maxQuality);
                made = true;
                if (!shown.First(RecipeAlike.Key(row.Title, row.TitleLink, row.Items.Select(i => (i.Prefab, i.Amount)), kits.Items.Select(i => (i.Prefab, i.Amount))))) continue;
                Rows.Add(row);
                if (kits.Items.Count > 0) Rows.Add(kits);
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

            if ((weapon || ammo || shield || type == ItemDrop.ItemData.ItemType.Tool) && shared.m_skillType != Skills.SkillType.None) SkillLine(shared);
            if (weapon || shield) Blocking(shared);

            var attack = shared.m_attack;
            // A second attack the game offers only with an animation of its own (ItemData.HaveSecondaryAttack).
            var second = weapon && attack != null ? shared.m_secondaryAttack : null;
            var twoAttacks = second != null && !string.IsNullOrEmpty(second.m_attackAnimation);
            // A weapon's knockback is in its attacks table.
            if ((ammo || weapon && attack == null) && shared.m_attackForce > 0f)
            {
                Add("Knockback", Numbers.Amount(shared.m_attackForce));
                YoursAt("Knockback", shared.m_skillType, level => SkillWords.Range(shared.m_attackForce * SkillMath.HitShare(level).Least, shared.m_attackForce * SkillMath.HitShare(level).Most));
            }
            if (weapon && shared.m_backstabBonus > 1f) Add("Backstab", Numbers.Times(shared.m_backstabBonus));

            if (weapon && attack != null)
            {
                Drawing(shared, attack);
                WeaponAttacks(prefab, shared, attack, second, twoAttacks);
            }
            Durability(prefab, shared);
        }

        /// <summary>The skill an item trains, going to its page, named as the page names it; one a mod adds marked unsure.</summary>
        private void SkillLine(ItemDrop.ItemData.SharedData shared)
        {
            var skill = SkillName(shared.m_skillType);
            var page = EntryKeys.For(Kind.Skill, shared.m_skillType.ToString());
            var link = EntryOf(page) != null ? page : null;
            if (skill == SkillWords.ModSkill) AddUnsure("Skill", skill, UnsureWords.ModSkill, link);
            else Add("Skill", skill, link);
        }

        /// <summary>
        /// Blocking, at the first quality, what each level adds being in the table by quality: told
        /// only above 1, as AddBlockTooltip does, below which it cannot block; its force, its parry
        /// bonus, and what a parry holds back at the character's skill.
        /// </summary>
        private void Blocking(ItemDrop.ItemData.SharedData shared)
        {
            Add("Block", ItemWords.Block(shared.m_blockPower));
            if (shared.m_blockPower > 1f) YoursAt("Block", Skills.SkillType.Blocking, level => ItemWords.Block(SkillMath.Block(shared.m_blockPower, level)));
            if (shared.m_deflectionForce > 1f) Add("Block force", Numbers.Amount(shared.m_deflectionForce));
            if (shared.m_timedBlockBonus > 1f) Add("Parry bonus", Numbers.Times(shared.m_timedBlockBonus));
            // A parry holds back the block times its bonus (Humanoid.BlockAttack).
            if (shared.m_timedBlockBonus > 1f && shared.m_blockPower > 1f)
            {
                YoursAt("Parry bonus", Skills.SkillType.Blocking, level => SkillWords.Parry(SkillMath.Block(shared.m_blockPower, level) * shared.m_timedBlockBonus));
            }
        }

        /// <summary>
        /// A bow's draw and a crossbow's reload, each faster at the character's skill
        /// (Humanoid.GetAttackDrawPercentage, ItemDrop.ItemData.GetWeaponLoadingTime), and the drain
        /// while drawing, less (ItemDrop.ItemData.GetDrawStaminaDrain).
        /// </summary>
        private void Drawing(ItemDrop.ItemData.SharedData shared, Attack attack)
        {
            if (attack.m_drawStaminaDrain > 0f)
            {
                Add("Drawing costs", CombatWords.DrawCost(attack.m_drawStaminaDrain));
                YoursAt("Drawing costs", shared.m_skillType, level => CombatWords.DrawCost(attack.m_drawStaminaDrain * SkillMath.CostShare(level), 1));
            }
            if (attack.m_bowDraw && attack.m_drawDurationMin > 0f)
            {
                Add(CombatWords.FullDraw, Numbers.Duration(attack.m_drawDurationMin));
                YoursAt(CombatWords.FullDraw, shared.m_skillType, level => Numbers.Duration(SkillMath.DrawTime(attack.m_drawDurationMin, level)));
            }
            if (attack.m_requiresReload && attack.m_reloadTime > 0f)
            {
                Add(CombatWords.Reloads, Numbers.Duration(attack.m_reloadTime));
                YoursAt(CombatWords.Reloads, shared.m_skillType, level => Numbers.Duration(SkillMath.ReloadTime(attack.m_reloadTime, level)));
            }
        }

        /// <summary>
        /// A weapon's attacks table, each attack a line side by side: what it deals at the first
        /// quality, knocks back, staggers and costs, one with no second attack having its line too,
        /// so it can be played; a chained attack a line for each step. Under each, faint, the same at
        /// the character's skill (Attack.DoMeleeAttack, Attack.FireProjectileBurst,
        /// Attack.GetAttackStamina), and an axe's hit on a tree where trees take another skill.
        /// </summary>
        private void WeaponAttacks(GameObject prefab, ItemDrop.ItemData.SharedData shared, Attack attack, Attack second, bool twoAttacks)
        {
            var table = new Row { Title = CombatWords.WeaponAttacksTitle, Columns = CombatWords.WeaponAttackColumns };
            var you = YourAt(shared.m_skillType);
            foreach (var (each, isSecond) in twoAttacks ? new[] { (attack, false), (second, true) } : new[] { (attack, false) })
            {
                var figures = DamageFigures(shared.m_damages).Select(d => (d.Type, d.Amount * each.m_damageMultiplier)).ToList();
                var costs = CombatWords.Costs(each.m_attackStamina, each.m_attackEitr, each.m_attackHealth, each.m_attackHealthPercentage);
                var knockback = shared.m_attackForce * each.m_forceMultiplier;
                var plays = !string.IsNullOrEmpty(each.m_attackAnimation);
                // A chained attack a line for each step, the last as the game deals it (Attack.DoMeleeAttack,
                // DoAreaAttack); each step's name plays that step on the person wearing it.
                if (each.m_attackChainLevels > 1)
                {
                    var melee = each.m_attackType == Attack.AttackType.Horizontal || each.m_attackType == Attack.AttackType.Vertical;
                    var area = each.m_attackType == Attack.AttackType.Area;
                    var last = CombatWords.LastSwingMultiplier(melee, area, each.m_lastChainDamageMultiplier);
                    var steps = CombatWords.WeaponCombo(each.m_attackChainLevels, isSecond, figures, knockback, each.m_staggerMultiplier, last, costs);
                    var first = table.Lines.Count;
                    for (var step = 0; step < steps.Count; step++) table.Lines.Add((steps[step], plays ? EntryKeys.PlayAttack(prefab.name, isSecond, step) : null));
                    YoursUnder(table, first, each, figures, knockback, last, you);
                    Add(CombatWords.ComboTitle(isSecond), CombatWords.Combo(each.m_attackChainLevels, melee || area));
                    foreach (var (label, value) in CombatWords.ComboResets((int)each.m_resetChainIfHit)) Add(label, value);
                    continue;
                }
                // Its name plays it on the person wearing it, as a creature's attack plays from its table; one with no animation plays nothing.
                table.Lines.Add((CombatWords.WeaponAttack(isSecond, CombatWords.Damage(figures), knockback, each.m_staggerMultiplier, costs),
                    plays ? EntryKeys.PlayAttack(prefab.name, isSecond) : null));
                YoursUnder(table, table.Lines.Count - 1, each, figures, knockback, 1f, you);
            }
            Rows.Add(table);
            if (!twoAttacks) Add("Secondary attack", "none");
        }

        /// <summary>
        /// Durability, and where an item is repaired: where it is made or at its repair station, from
        /// the recipe's station level (InventoryGui.CanRepair); where it cannot be, that is said too.
        /// Gear that does not wear says so.
        /// </summary>
        private void Durability(GameObject prefab, ItemDrop.ItemData.SharedData shared)
        {
            var recipe = RecipeOf(prefab);
            if (shared.m_useDurability)
            {
                Add("Durability", Numbers.Amount(shared.m_maxDurability));
                // Repaired where it is made or at its repair station, from the recipe's station level (InventoryGui.CanRepair);
                // where it cannot be, that is said too. Unity's own null check, not ??, which a destroyed reference would pass.
                var at = recipe == null ? null : recipe.m_repairStation != null ? recipe.m_repairStation : recipe.m_craftingStation;
                Add("Repaired at", ItemWords.Repair(shared.m_canBeReparied, at != null ? CatalogBuilder.Localize(at.m_name) : null, recipe != null ? recipe.m_minStationLevel : 1),
                    shared.m_canBeReparied && at != null ? at.gameObject.name : null);
            }
            else if (IsGear(shared.m_itemType)) Add("Durability", ItemWords.NoWear);
        }

        /// <summary>
        /// The faint lines under an attack's lines in the attacks table, from its first: what each
        /// step deals and knocks back at the character's skill and what it costs, and where the
        /// attack hits trees by another skill (<c>Attack.m_specialHitSkill</c>, an axe's Wood
        /// Cutting), what each step deals a tree at that one.
        /// </summary>
        private void YoursUnder(Row table, int first, Attack attack, IList<(string Type, float Amount)> figures, float knockback, float last, (string At, float Level, string Name, string Skill)? you)
        {
            if (you == null) return;
            var steps = Math.Max(1, attack.m_attackChainLevels);
            var cost = SkillMath.CostShare(you.Value.Level);
            var costs = CombatWords.Costs(attack.m_attackStamina * cost, attack.m_attackEitr * cost, attack.m_attackHealth * cost, attack.m_attackHealthPercentage * cost, 1);
            var lines = SkillWords.AttackLines(you.Value.At, steps, figures, knockback, last, SkillMath.HitShare(you.Value.Level), costs);
            var trees = attack.m_specialHitSkill != Skills.SkillType.None && (attack.m_specialHitType & DestructibleType.Tree) != 0 ? YourAt(attack.m_specialHitSkill) : null;
            var treeLines = trees != null ? SkillWords.TreeLines(SkillWords.OnTrees(trees.Value.At), steps, figures, last, SkillMath.HitShare(trees.Value.Level)) : null;
            for (var step = 0; step < lines.Count && first + step < table.Lines.Count; step++)
            {
                var under = new List<(string[], string, string)> { YourLine(lines[step], you.Value) };
                if (treeLines != null) under.Add(YourLine(treeLines[step], trees.Value));
                table.Yours[first + step] = under;
            }
        }

        /// <summary>
        /// What an item does by a skill besides hitting: a build tool's cost and wear for each
        /// use (<c>Player.GetBuildStamina</c>, <c>Player.GetPlaceDurability</c>), a scythe's reach
        /// (<c>Attack.DoMeleeAttack</c>), a fishing float's drain and reeling
        /// (<c>FishingFloat.FixedUpdate</c>), a staff's summons (<c>SpawnAbility.Spawn</c>); each with
        /// what it comes to at the character's skill.
        /// </summary>
        private void SkillUses(ItemDrop.ItemData.SharedData shared)
        {
            var table = shared.m_buildPieces;
            if (table != null && shared.m_attack != null && shared.m_attack.m_attackStamina > 0f)
            {
                var stamina = shared.m_attack.m_attackStamina;
                Add("Each use costs", CombatWords.Costs(stamina, 0f, 0f, 0f));
                YoursAt("Each use costs", table.m_skill, level => CombatWords.Costs(stamina * SkillMath.BuildStaminaShare(level), 0f, 0f, 0f, 1));
            }
            if (table != null && shared.m_useDurability && shared.m_useDurabilityDrain > 0f)
            {
                var wear = shared.m_useDurabilityDrain;
                Add("Each use wears", Numbers.Amount(wear));
                if (shared.m_placementDurabilityMax > 0f) YoursAt("Each use wears", shared.m_placementDurabilitySkill, level => Numbers.Amount(wear * SkillMath.WearShare(level, shared.m_placementDurabilityMax)));
            }
            var told = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attack in SkillReading.Attacks(shared))
            {
                if (attack.m_harvest && attack.m_harvestRadiusMaxLevel > 0f && told.Add("Harvests within"))
                {
                    var least = attack.m_harvestRadius;
                    var most = attack.m_harvestRadiusMaxLevel;
                    Add("Harvests within", SkillWords.Span(Numbers.Metres(least, 1), Numbers.Metres(most, 1)));
                    YoursAt("Harvests within", Skills.SkillType.Farming, level => Numbers.Metres(SkillMath.Between(least, most, level), 1));
                }
                var fishing = SkillReading.FloatOf(attack);
                if (fishing != null && told.Add("With a fish on"))
                {
                    Add("With a fish on", SkillWords.Span(SkillWords.PerSecond(fishing.m_hookedStaminaPerSec), SkillWords.PerSecond(fishing.m_hookedStaminaPerSecMaxSkill)));
                    YoursAt("With a fish on", Skills.SkillType.Fishing, level => SkillWords.PerSecond(SkillMath.Between(fishing.m_hookedStaminaPerSec, fishing.m_hookedStaminaPerSecMaxSkill, level)));
                    Add("Reeling in", SkillWords.Span(SkillWords.Speed(fishing.m_pullLineSpeed), SkillWords.Speed(fishing.m_pullLineSpeedMaxSkill)));
                    YoursAt("Reeling in", Skills.SkillType.Fishing, level => SkillWords.Speed(SkillMath.Between(fishing.m_pullLineSpeed, fishing.m_pullLineSpeedMaxSkill, level)));
                }
                var spawn = attack.m_attackProjectile != null ? attack.m_attackProjectile.GetComponent<SpawnAbility>() : null;
                if (spawn != null && spawn.m_levelUpSettings != null && spawn.m_levelUpSettings.Count > 0 && !spawn.m_setMaxInstancesFromWeaponLevel && told.Add("Summons at once"))
                {
                    var skill = spawn.m_levelUpSettings[0].m_skill;
                    var levels = spawn.m_levelUpSettings.Select(s => s.m_skillLevel).ToArray();
                    var counts = spawn.m_levelUpSettings.Select(s => s.m_maxSpawns).ToArray();
                    Add("Summons at once", SkillWords.SummonSteps(levels, counts));
                    YoursAt("Summons at once", skill, level => SkillWords.SummonsAt(levels, counts, level));
                    if (spawn.m_copySkill != Skills.SkillType.None && spawn.m_copySkillToRandomFactor > 0f)
                    {
                        var perLevel = spawn.m_copySkillToRandomFactor;
                        Add("Summons hit", SkillWords.SummonsHit(perLevel));
                        YoursAt("Summons hit", spawn.m_copySkill, level => Numbers.Times(SkillMath.SummonStrength(level, perLevel)));
                    }
                }
            }
        }

        /// <summary>The recipe that makes an item, the first enabled one, or null.</summary>
        private static Recipe RecipeOf(GameObject prefab) =>
            ObjectDB.instance != null ? ObjectDB.instance.m_recipes.FirstOrDefault(r => r != null && r.m_enabled && r.m_item != null && r.m_item.gameObject.name == prefab.name) : null;

        /// <summary>
        /// What grows with quality, a line each: its damage (<c>ItemData.GetDamage</c>), armour,
        /// block and block force, durability, each its base and what each level adds; and the
        /// station level each quality needs (<c>Recipe.GetRequiredStationLevel</c>).
        /// </summary>
        private void QualityTable(GameObject prefab, ItemDrop.ItemData.SharedData shared, bool worn)
        {
            var most = shared.m_maxQuality;
            var type = shared.m_itemType;
            var blocks = type == ItemDrop.ItemData.ItemType.Shield || type == ItemDrop.ItemData.ItemType.OneHandedWeapon || type == ItemDrop.ItemData.ItemType.TwoHandedWeapon
                         || type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || type == ItemDrop.ItemData.ItemType.Bow || type == ItemDrop.ItemData.ItemType.Torch;
            var perLevel = DamageFigures(shared.m_damagesPerLevel);
            var lines = new List<(string[] Cells, string Link)>
            {
                (ItemWords.DamageByQuality(DamageFigures(shared.m_damages).Select((d, i) => (d.Type, d.Amount, perLevel[i].Amount)).ToList(), most), null),
                (worn ? ItemWords.ByQuality("Armour", shared.m_armor, shared.m_armorPerLevel, most) : null, null),
                (blocks && shared.m_blockPower > 1f ? ItemWords.ByQuality("Block", shared.m_blockPower, shared.m_blockPowerPerLevel, most) : null, null),
                (blocks && shared.m_deflectionForce > 1f ? ItemWords.ByQuality("Block force", shared.m_deflectionForce, shared.m_deflectionForcePerLevel, most) : null, null),
                (shared.m_useDurability ? ItemWords.ByQuality("Durability", shared.m_maxDurability, shared.m_durabilityPerLevel, most) : null, null),
            };
            var recipe = RecipeOf(prefab);
            var station = recipe == null ? null : recipe.m_craftingStation != null ? recipe.m_craftingStation : recipe.m_repairStation;
            if (station != null)
            {
                var levels = Enumerable.Range(1, most).Select(q => recipe.GetRequiredStationLevel(q)).ToList();
                lines.Add((ItemWords.StationLevels(CatalogBuilder.Localize(station.m_name), levels), station.gameObject.name));
            }
            var table = new Row { Title = ItemWords.ByQualityTitle, Columns = ItemWords.QualityColumns(most) };
            // Faint under damage and block, the same at the character's skill at each quality.
            var damageAt = SkillReading.Trains(type) ? YourAt(shared.m_skillType) : null;
            var blockAt = blocks && shared.m_blockPower > 1f ? YourAt(Skills.SkillType.Blocking) : null;
            var yours = new[]
            {
                damageAt != null ? SkillWords.DamageByQuality(damageAt.Value.At, DamageFigures(shared.m_damages).Select((d, i) => (d.Type, d.Amount, perLevel[i].Amount)).ToList(), most, SkillMath.HitShare(damageAt.Value.Level)) : null,
                null,
                blockAt != null ? SkillWords.BlockByQuality(blockAt.Value.At, shared.m_blockPower, shared.m_blockPowerPerLevel, most, blockAt.Value.Level) : null,
            };
            var yoursAt = new[] { damageAt, null, blockAt };
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Cells == null) continue;
                table.Lines.Add(lines[i]);
                if (i < yours.Length && yours[i] != null) table.Yours[table.Lines.Count - 1] = new List<(string[], string, string)> { YourLine(yours[i], yoursAt[i].Value) };
            }
            if (table.Lines.Count > 0) Rows.Add(table);
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

        /// <summary>
        /// The upgrade kits a recipe names. The game asks for them only at a station marked as an
        /// upgrader, which takes an item past its top quality with the kits alone, and leaves them
        /// out everywhere else (<c>Player.HaveRequirements</c>, <c>InventoryGui.SetupRequirementList</c>).
        /// Each shows how many the first step past the top takes.
        /// </summary>
        private static Row UpgradeKits(Recipe recipe, int maxQuality)
        {
            var row = new Row { Title = ItemWords.PastTop(Knowledge.UpgradeStationName), TitleLink = Knowledge.UpgradeStation, GameShown = true, Recipe = recipe.m_item != null ? RecipeToken(recipe.m_item.gameObject) : null };
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
