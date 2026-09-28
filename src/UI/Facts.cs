using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What an entry is in the game, in words: an item's stats and recipe, a creature's health,
    /// resistances and drops, a piece's cost and comfort, a resource's drops, a chest's contents,
    /// what a status effect changes, and where things spawn, grow or come from. Read from the
    /// prefab, so mods' changes show; a world whose settings change them says so.
    /// </summary>
    internal sealed partial class Facts
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static Facts() => WorldCaches.Register(nameof(Facts), Forget);

        /// <summary>A row of items with amounts, such as a recipe or a creature's drops.</summary>
        public sealed class Row
        {
            public string Title = "";

            /// <summary>The prefab the title names, such as the crafting station, so it can be gone to.</summary>
            public string TitleLink;
            public readonly List<Ingredient> Items = new List<Ingredient>();
        }

        public struct Ingredient
        {
            public Sprite Icon;
            public string Name;
            public string Amount;

            /// <summary>The prefab, so the panel can jump to it.</summary>
            public string Prefab;
        }

        /// <summary>The text the game describes it with, if any.</summary>
        public string Description = "";

        /// <summary>Named values, shown as a two-column table.</summary>
        public readonly List<KeyValuePair<string, string>> Pairs = new List<KeyValuePair<string, string>>();

        public readonly List<Row> Rows = new List<Row>();

        /// <summary>Where it lives, comes from, or what gives it, under <see cref="WhereTitle"/>.</summary>
        public readonly List<Source> Where = new List<Source>();
        public string WhereTitle = "Where it comes from";

        /// <summary>What it is used for, a row for each kind of use and place.</summary>
        public readonly List<Row> UseRows = new List<Row>();

        public bool IsEmpty => Description.Length == 0 && Pairs.Count == 0 && Rows.Count == 0 && Where.Count == 0 && UseRows.Count == 0;

        /// <summary>Values that name something in the catalog, by their label: a prefab name, or "se:" and a status effect's.</summary>
        public readonly Dictionary<string, string> Links = new Dictionary<string, string>();

        private void Add(string label, string value, string link = null)
        {
            if (string.IsNullOrEmpty(value)) return;
            Pairs.Add(new KeyValuePair<string, string>(label, value));
            if (link != null) Links[label] = link;
        }

        private static readonly Dictionary<Entry, Facts> Cache = new Dictionary<Entry, Facts>();

        public static Facts For(Entry entry)
        {
            if (entry == null) return new Facts();
            if (Cache.TryGetValue(entry, out var known)) return known;

            var facts = new Facts();
            if (entry.Source is StatusEffect effect)
            {
                facts.Part("status effect", () => facts.StatusEffect(effect));
            }
            else if (entry.Source is GameObject prefab)
            {
                // The game spawns creatures up to two stars; some mods go higher, and show it.
                facts._stars = Math.Max(2, entry.ExtraLevels);
                facts._entry = entry;
                facts.Prefab(prefab);
                if (entry.Kind == Kind.Creature) facts.WhereTitle = "Where it lives";
                facts.Part("where it comes from", () =>
                {
                    facts.Where.AddRange(Knowledge.WhereLines(entry.Name));
                    facts.Where.AddRange(Knowledge.SourceLines(entry.Name));

                    // An item nothing makes, drops, sells or spawns here comes from somewhere Scry cannot
                    // see, unless the locations, once read, show where it is found. Something that
                    // spawns it (an Asksvin its egg) is told under LINKED.
                    if (entry.Kind == Kind.Item && facts.Where.Count == 0 && entry.FoundIn.Length == 0 && !facts.Rows.Any(r => r.Title.StartsWith("Made"))
                        && !entry.Links.Any(l => l.Group == Relations.SpawnedBy))
                    {
                        facts.Where.Add(new Source("Nothing loaded makes, drops or sells it. It may come from a location, a dungeon, an event or a mod.", null));
                    }
                });
                facts.Part("uses", () => facts.Uses(entry.Name));
            }
            facts.TellMissing();

            Cache[entry] = facts;
            return facts;
        }

        /// <summary>The parts of these facts that could not be read, told at their end.</summary>
        private readonly List<string> _missing = new List<string>();

        /// <summary>
        /// Reads one part of the facts on its own. One that fails, on a mod's odd prefab or because
        /// an update changed what it reads, is told once in the log and named at the end of the
        /// facts, and the other parts still show.
        /// </summary>
        private void Part(string part, Action read)
        {
            try
            {
                read();
            }
            catch (Exception ex)
            {
                Faults.Tell("the " + part + " details", ex);
                if (!_missing.Contains(part)) _missing.Add(part);
            }
        }

        private void TellMissing()
        {
            if (_missing.Count == 0) return;
            Add("Not shown", $"the {string.Join(", ", _missing)} details could not be read (the log says why)");
        }

        /// <summary>Forgets everything read, for a new world.</summary>
        public static void Forget()
        {
            Cache.Clear();
            _comesFrom = null;
        }

        private void Prefab(GameObject prefab)
        {
            var drop = prefab.GetComponent<ItemDrop>();
            var shared = drop?.m_itemData?.m_shared;
            if (shared != null) Part("item", () => Item(prefab, shared));

            var character = prefab.GetComponent<Character>();
            if (character != null) Part("creature", () => Creature(prefab, character));

            var piece = prefab.GetComponent<Piece>();
            if (piece != null && piece.enabled) Part("piece", () => Piece(piece, prefab.GetComponent<WearNTear>()));

            Part("resource", () => Resource(prefab));
            if (piece != null && !piece.enabled) Part("build cost", () => MadeBuildable(piece));
            Part("station", () => Station(prefab));

            var projectile = prefab.GetComponent<Projectile>();
            if (projectile != null) Part("projectile", () => Flight(projectile));

            // What a chest is filled with when the game first opens it (Container.AddDefaultItems);
            // one players build has nothing.
            var container = prefab.GetComponent<Container>();
            if (container != null) Part("chest", () => Drops(container.m_defaultItems, null, holds: true));

            // The prefab's own numbers are shown; a world that changes them says by how much.
            Part("world settings", () =>
            {
                var game = Game.instance;
                var enemy = character != null && !(character is Player);
                var note = WorldWords.Note(Game.m_worldLevel, game != null ? game.m_worldLevelEnemyHPMultiplier : 1f, Game.m_resourceRate, enemy, _drops);
                if (note != null) Add("In this world", char.ToUpperInvariant(note[0]) + note.Substring(1));
            });
        }

        /// <summary>The entry told of, for what the list already says of it (its group).</summary>
        private Entry _entry;

        /// <summary>Whether any drops were told, which a world's resource rate scales.</summary>
        private bool _drops;

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

            if (shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f)
            {
                var food = new List<string>();
                if (shared.m_food > 0f) food.Add($"{Number(shared.m_food)} health");
                if (shared.m_foodStamina > 0f) food.Add($"{Number(shared.m_foodStamina)} stamina");
                if (shared.m_foodEitr > 0f) food.Add($"{Number(shared.m_foodEitr)} eitr");
                Add("Food", string.Join(", ", food));
                if (shared.m_foodRegen > 0f) Add("Heals", $"{Number(shared.m_foodRegen)} a tick");
                if (shared.m_foodBurnTime > 0f) Add("Lasts", Minutes(shared.m_foodBurnTime));
            }

            if (shared.m_toolTier > 0) Add("Tool tier", shared.m_toolTier.ToString(CultureInfo.InvariantCulture));
            if (Math.Abs(shared.m_movementModifier) > 0.001f) Add("Movement", Percent(shared.m_movementModifier));
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

            Part("recipe", () => Recipes(prefab, shared));
            Part("made at stations", () => MadeIn(prefab));
        }

        /// <summary>Each enabled recipe that makes the item, with its upgrade kits.</summary>
        private void Recipes(GameObject prefab, ItemDrop.ItemData.SharedData shared)
        {
            var db = ObjectDB.instance;
            if (db == null) return;
            foreach (var recipe in db.m_recipes)
            {
                if (recipe == null || recipe.m_item == null || recipe.m_item.gameObject.name != prefab.name || !recipe.m_enabled) continue;

                var station = recipe.m_craftingStation != null ? CatalogBuilder.Localize(recipe.m_craftingStation.m_name) : "";
                var title = station.Length > 0
                    ? $"Made at {station}{(recipe.m_minStationLevel > 1 ? $" level {recipe.m_minStationLevel}" : "")}"
                    : "Made by hand";
                if (recipe.m_amount > 1) title += $", makes {recipe.m_amount}";
                if (recipe.m_requireOnlyOneIngredient) title += ", from any one of these";
                var row = Requirements(title, recipe.m_resources, shared.m_maxQuality > 1);
                row.TitleLink = recipe.m_craftingStation != null ? recipe.m_craftingStation.gameObject.name : null;
                Rows.Add(row);

                var kits = UpgradeKits(recipe, shared.m_maxQuality);
                if (kits.Items.Count > 0) Rows.Add(kits);
            }
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
                Add("Skill", Word(shared.m_skillType));
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
                var costs = new List<string>();
                if (attack.m_attackStamina > 0f) costs.Add($"{Number(attack.m_attackStamina)} stamina");
                if (attack.m_attackEitr > 0f) costs.Add($"{Number(attack.m_attackEitr)} eitr");
                if (attack.m_attackHealth > 0f) costs.Add($"{Number(attack.m_attackHealth)} health");
                if (attack.m_attackHealthPercentage > 0f) costs.Add($"{Number(attack.m_attackHealthPercentage)}% health");
                if (costs.Count > 0) Add("Each attack costs", string.Join(", ", costs));
                if (attack.m_drawStaminaDrain > 0f) Add("Drawing costs", $"{Number(attack.m_drawStaminaDrain)} stamina a second");
            }

            var recipe = ObjectDB.instance != null ? ObjectDB.instance.m_recipes.FirstOrDefault(r => r != null && r.m_enabled && r.m_item != null && r.m_item.gameObject.name == prefab.name) : null;
            if (shared.m_useDurability)
            {
                Add("Durability", Number(shared.m_maxDurability) + PerQuality(shared.m_durabilityPerLevel));
                // Repaired where it is made or at its repair station, from the recipe's station level (InventoryGui.CanRepair).
                var at = recipe != null ? recipe.m_repairStation ?? recipe.m_craftingStation : null;
                if (shared.m_canBeReparied && at != null)
                {
                    var level = recipe.m_minStationLevel > 1 ? $" level {recipe.m_minStationLevel}" : "";
                    Add("Repaired at", CatalogBuilder.Localize(at.m_name) + level, at.gameObject.name);
                }
            }

            var station = recipe != null ? recipe.m_craftingStation ?? recipe.m_repairStation : null;
            if (upgradable && station != null)
            {
                var first = recipe.GetRequiredStationLevel(2);
                var last = recipe.GetRequiredStationLevel(shared.m_maxQuality);
                Add("Upgrades need", $"{CatalogBuilder.Localize(station.m_name)} level {DropWords.Range(first, last)}, one more for each quality", station.gameObject.name);
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
            var row = new Row { Title = "Past its top quality, at an upgrade station" };
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

        // ----- Creatures -----

        /// <summary>How many stars a creature can have, for what they add.</summary>
        private int _stars = 2;

        private void Creature(GameObject prefab, Character character)
        {
            Add("Health", Number(character.m_health));
            Add("Faction", Groups.FactionName(character.m_faction.ToString()));
            if (character.m_boss) Add("Boss", "yes");

            // What stars add: its health once more for each, and half as much again to each hit.
            if (!character.m_boss)
            {
                var health = CombatWords.StarHealth(character.m_health, _stars);
                if (health != null) Add("Health with stars", health);
                var hits = CombatWords.StarDamage(_stars);
                if (hits != null) Add("Hits with stars", hits);
            }

            Part("resistances", () => Resists(character.m_damageModifiers));
            Part("attacks", () => Attacks(prefab));
            Part("behaviour", () => Behaviour(prefab, character));
            if (character.m_boss) Part("summoning", () => SummonedBy(prefab));

            if (prefab.GetComponent<Tameable>() != null)
            {
                Add("Tameable", "yes");
                var ai = prefab.GetComponent<MonsterAI>();
                var food = ai?.m_consumeItems?.Where(i => i != null).ToList();
                if (food != null && food.Count > 0)
                {
                    var eats = new Row { Title = "Eats" };
                    foreach (var item in food) eats.Items.Add(new Ingredient { Icon = Icon(item.gameObject), Name = ItemName(item.gameObject), Amount = "", Prefab = item.gameObject.name });
                    Rows.Add(eats);
                }
            }

            var drops = prefab.GetComponent<CharacterDrop>();
            if (drops != null && drops.m_drops.Count > 0)
            {
                var row = new Row { Title = "Drops" };
                foreach (var drop in drops.m_drops)
                {
                    if (drop?.m_prefab == null) continue;
                    var amount = DropWords.CreatureAmount(drop.m_amountMin, drop.m_amountMax, drop.m_onePerPlayer);
                    if (drop.m_chance < 1f) amount += $" ({Mathf.RoundToInt(drop.m_chance * 100f)}%)";
                    row.Items.Add(new Ingredient { Icon = Icon(drop.m_prefab), Name = ItemName(drop.m_prefab), Amount = amount, Prefab = drop.m_prefab.name });
                }
                if (row.Items.Count > 0)
                {
                    Rows.Add(row);
                    _drops = true;
                    // A boss is not spawned with stars; any other creature's drops grow with them.
                    if (!character.m_boss && drops.m_drops.Any(d => d?.m_prefab != null && d.m_levelMultiplier && !d.m_onePerPlayer))
                    {
                        var same = drops.m_drops.Where(d => d?.m_prefab != null && !d.m_levelMultiplier).Select(d => ItemName(d.m_prefab)).Distinct().ToList();
                        Add("Drops with stars", DropWords.StarDrops(_stars, same));
                    }
                }
            }
        }

        /// <summary>
        /// Each attack it has, from the items it may carry (a creature fights with items of its
        /// own, as a person does): what a hit does, how, and from how near and far and how often
        /// its AI uses it (<c>m_aiAttackRange</c>, <c>m_aiAttackRangeMin</c>, <c>m_aiAttackInterval</c>).
        /// Each goes to its item.
        /// </summary>
        private void Attacks(GameObject prefab)
        {
            foreach (var item in Relations.CarriedItems(prefab))
            {
                var shared = item.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                var attack = shared?.m_attack;
                if (attack == null) continue;
                var name = ItemName(item);
                var damage = Damages(shared.m_damages);
                var key = "Attack: " + name;
                if (Pairs.Any(p => p.Key == key)) continue;
                Add(key, CombatWords.Attack(damage, attack.m_attackType.ToString(), shared.m_aiAttackRangeMin, shared.m_aiAttackRange, shared.m_aiAttackInterval));
                Links[key] = item.name;
            }
        }

        /// <summary>How it moves, sees and hears, what it fears, when it flees, and how long it takes to tame.</summary>
        private void Behaviour(GameObject prefab, Character character)
        {
            var moves = new List<string>();
            if (character.m_flying) moves.Add($"flies {Number(character.m_flySlowSpeed)}–{Number(character.m_flyFastSpeed)} m/s");
            else moves.Add($"walks {Number(character.m_walkSpeed)} m/s, runs {Number(character.m_runSpeed)} m/s");
            if (character.m_canSwim) moves.Add($"swims {Number(character.m_swimSpeed)} m/s");
            Add("Moves", string.Join(", ", moves));

            var ai = prefab.GetComponent<BaseAI>();
            if (ai != null)
            {
                Add("Sees", CombatWords.Sight(ai.m_viewRange, ai.m_viewAngle));
                if (ai.m_hearRange < 9000f) Add("Hears", $"{Number(ai.m_hearRange)} m");
                if (ai.m_afraidOfFire) Add("Fire", "afraid of it");
                else if (ai.m_avoidFire) Add("Fire", "keeps away from it");
                if (ai.m_passiveAggresive) Add("Fights", "only once attacked");
                if (ai is MonsterAI monster)
                {
                    if (monster.m_fleeIfLowHealth > 0f) Add("Flees", $"below {Mathf.RoundToInt(monster.m_fleeIfLowHealth * 100f)}% health, right after being hurt");
                    if (!monster.m_attackPlayerObjects) Add("Leaves alone", "what players build");
                }
            }

            var tame = prefab.GetComponent<Tameable>();
            if (tame != null)
            {
                Add("Takes to tame", Minutes(tame.m_tamingTime));
                Add("Stays fed", Minutes(tame.m_fedDuration));
            }
        }

        /// <summary>What it resists or is weak to, a row for each degree, as a creature's or a resource's are told.</summary>
        private void Resists(HitData.DamageModifiers mods)
        {
            // By degree, from very weak to immune, as the game orders them, not by the words' spelling.
            var groups = new SortedDictionary<int, (string Words, List<string> Types)>();
            foreach (var field in typeof(HitData.DamageModifiers).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(HitData.DamageModifier) || field.Name == "m_nonPlayer") continue;
                var modifier = (HitData.DamageModifier)field.GetValue(mods);
                if (modifier == HitData.DamageModifier.Normal) continue;

                var order = Array.IndexOf(Degrees, modifier);
                if (order < 0) order = Degrees.Length + (int)modifier;
                if (!groups.TryGetValue(order, out var group)) groups[order] = group = (ModifierWords(modifier), new List<string>());
                group.Types.Add(Naming.FieldLabel(field.Name).ToLowerInvariant());
            }
            foreach (var group in groups.Values) Add(group.Words, string.Join(", ", group.Types));
        }

        // ----- Resources -----

        /// <summary>
        /// What is chopped, mined, broken, picked or grown: how much it takes and with what tool,
        /// what it resists, and what it gives, as the game's own scripts hold it. A prefab can be
        /// several of these (a stump breaks and drops wood), so each part adds its own.
        /// </summary>
        private void Resource(GameObject prefab)
        {
            // Where it comes from and what it becomes: a tree falls as its log and leaves a stump,
            // a log splits into halves, a vein's shell breaks into the vein (TreeBase.SpawnLog,
            // TreeLog.Destroy, Destructible.Destroy).
            Leads("Comes from", ComesFrom(prefab.name).Select(p => (p, 1)));

            var tree = prefab.GetComponent<TreeBase>();
            if (tree != null)
            {
                Hits(tree.m_health, false, tree.m_minToolTier, tree.m_damageModifiers);
                Drops(tree.m_dropWhenDestroyed, "When felled, ");
                Leads("Felled, falls as", new[] { (tree.m_logPrefab, 1) });
                Leads("Leaves", new[] { (tree.m_stubPrefab, 1) });
            }

            var log = prefab.GetComponent<TreeLog>();
            if (log != null)
            {
                Hits(log.m_health, false, log.m_minToolTier, log.m_damages);
                Drops(log.m_dropWhenDestroyed, null);
                Leads("Splits into", new[] { (log.m_subLogPrefab, log.m_subLogPoints?.Count(p => p != null) ?? 0) });
            }

            // A rock or ore vein is mined a piece at a time; each piece has this much health.
            var rock = prefab.GetComponent<MineRock>();
            if (rock != null)
            {
                Hits(rock.m_health, true, rock.m_minToolTier, rock.m_damageModifiers);
                Drops(rock.m_dropItems, "Each piece ");
            }
            var vein = prefab.GetComponent<MineRock5>();
            if (vein != null)
            {
                Hits(vein.m_health, true, vein.m_minToolTier, vein.m_damageModifiers);
                Drops(vein.m_dropItems, "Each piece ");
            }

            var breaks = prefab.GetComponent<Destructible>();
            var inside = breaks != null ? MinedInside(breaks.m_spawnWhenDestroyed) : null;
            if (inside != null)
            {
                // A shell that, struck once, turns into what is mined (a silver vein, a copper
                // deposit): what that takes and gives is what the vein takes and gives.
                Add("Breaks into", AnyName(inside, inside.name) + ", mined a piece at a time", inside.name);
                var innerRock = inside.GetComponent<MineRock>();
                var innerVein = inside.GetComponent<MineRock5>();
                if (innerVein != null)
                {
                    Hits(innerVein.m_health, true, Math.Max(breaks.m_minToolTier, innerVein.m_minToolTier), innerVein.m_damageModifiers);
                    Drops(innerVein.m_dropItems, "Each piece ");
                }
                else if (innerRock != null)
                {
                    Hits(innerRock.m_health, true, Math.Max(breaks.m_minToolTier, innerRock.m_minToolTier), innerRock.m_damageModifiers);
                    Drops(innerRock.m_dropItems, "Each piece ");
                }
            }
            else if (breaks != null && tree == null && log == null)
            {
                Hits(breaks.m_health, false, breaks.m_minToolTier, breaks.m_damages);
                var dropping = prefab.GetComponent<DropOnDestroyed>();
                if (dropping != null) Drops(dropping.m_dropWhenDestroyed, "When broken, ");
            }

            var pickable = prefab.GetComponent<Pickable>();
            if (pickable != null && pickable.m_itemPrefab != null)
            {
                var picked = new Row { Title = "Picked" };
                picked.Items.Add(new Ingredient
                {
                    Icon = Icon(pickable.m_itemPrefab), Name = ItemName(pickable.m_itemPrefab),
                    Amount = pickable.m_amount.ToString(CultureInfo.InvariantCulture), Prefab = pickable.m_itemPrefab.name,
                });
                Rows.Add(picked);
                _drops = true;
                if (pickable.m_respawnTimeMinutes > 0f) Add("Grows back in", Minutes(pickable.m_respawnTimeMinutes * 60f));
                var day = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1200L;
                var yields = Yield.PerDay(pickable.m_amount, pickable.m_respawnTimeMinutes, day);
                if (yields != null) Add("Gives", yields + $" (a day is {Minutes(day)})");
                Drops(pickable.m_extraDrops, "Also ");
            }

            var found = prefab.GetComponent<PickableItem>();
            if (found != null)
            {
                var row = new Row { Title = found.m_randomItemPrefabs != null && found.m_randomItemPrefabs.Length > 1 ? "Picked, one of" : "Picked" };
                if (found.m_randomItemPrefabs != null && found.m_randomItemPrefabs.Length > 0)
                {
                    foreach (var random in found.m_randomItemPrefabs)
                    {
                        if (random.m_itemPrefab == null) continue;
                        var item = random.m_itemPrefab.gameObject;
                        row.Items.Add(new Ingredient { Icon = Icon(item), Name = ItemName(item), Amount = DropWords.Range(random.m_stackMin, random.m_stackMax), Prefab = item.name });
                    }
                }
                else if (found.m_itemPrefab != null)
                {
                    var item = found.m_itemPrefab.gameObject;
                    row.Items.Add(new Ingredient { Icon = Icon(item), Name = ItemName(item), Amount = Math.Max(1, found.m_stack).ToString(CultureInfo.InvariantCulture), Prefab = item.name });
                }
                if (row.Items.Count > 0)
                {
                    Rows.Add(row);
                    _drops = true;
                }
            }

            var plant = prefab.GetComponent<Plant>();
            if (plant != null)
            {
                Add("Takes to grow", Naming.DurationRange(plant.m_growTime, Math.Max(plant.m_growTime, plant.m_growTimeMax)));
                if (plant.m_biome != 0) Add("Grows in", Knowledge.BiomeNames(plant.m_biome));
                if (plant.m_needCultivatedGround) Add("Needs", "cultivated ground");
                var tolerates = new List<string>();
                if (plant.m_tolerateHeat) tolerates.Add("heat");
                if (plant.m_tolerateCold) tolerates.Add("cold");
                if (tolerates.Count > 0) Add("Tolerates", string.Join(", ", tolerates));
                if (plant.m_grownPrefabs != null && plant.m_grownPrefabs.Length > 0)
                {
                    var grows = new Row { Title = plant.m_grownPrefabs.Length > 1 ? "Grows into one of" : "Grows into" };
                    foreach (var grown in plant.m_grownPrefabs.Where(g => g != null).GroupBy(g => g.name).Select(g => g.First()))
                    {
                        grows.Items.Add(new Ingredient { Icon = AnyIcon(grown), Name = AnyName(grown, grown.name), Amount = "", Prefab = grown.name });
                    }
                    if (grows.Items.Count > 0) Rows.Add(grows);
                }
            }
        }

        /// <summary>A row of what a resource leads to or comes from, each part going to it; nothing when there is none.</summary>
        private void Leads(string title, IEnumerable<(GameObject Prefab, int Count)> parts)
        {
            var row = new Row { Title = title };
            foreach (var (part, count) in parts)
            {
                if (part == null || count <= 0) continue;
                row.Items.Add(new Ingredient { Icon = AnyIcon(part), Name = AnyName(part, part.name), Amount = count > 1 ? count.ToString(CultureInfo.InvariantCulture) : "", Prefab = part.name });
            }
            if (row.Items.Count > 0) Rows.Add(row);
        }

        private static Dictionary<string, List<GameObject>> _comesFrom;

        /// <summary>
        /// What turns into a prefab when felled, split or broken open: the trees that fall as a
        /// log, the logs that split into a half, the shells that break into a vein. Worked out once
        /// a world, from its prefabs.
        /// </summary>
        private static List<GameObject> ComesFrom(string name)
        {
            if (_comesFrom == null)
            {
                _comesFrom = new Dictionary<string, List<GameObject>>();
                void Note(GameObject into, GameObject from)
                {
                    if (into == null || from == null) return;
                    if (!_comesFrom.TryGetValue(into.name, out var list)) _comesFrom[into.name] = list = new List<GameObject>();
                    if (!list.Contains(from)) list.Add(from);
                }
                var prefabs = ZNetScene.instance != null ? ZNetScene.instance.m_prefabs : null;
                if (prefabs != null)
                {
                    foreach (var prefab in prefabs)
                    {
                        if (prefab == null) continue;
                        var tree = prefab.GetComponent<TreeBase>();
                        if (tree != null) Note(tree.m_logPrefab, prefab);
                        var log = prefab.GetComponent<TreeLog>();
                        if (log != null) Note(log.m_subLogPrefab, prefab);
                        var breaks = prefab.GetComponent<Destructible>();
                        if (breaks != null) Note(MinedInside(breaks.m_spawnWhenDestroyed), prefab);
                    }
                }
            }
            return _comesFrom.TryGetValue(name, out var from) ? from : new List<GameObject>();
        }

        /// <summary>What a prefab turns into when broken, if that is mined (a vein, a rock), or null.</summary>
        public static GameObject MinedInside(GameObject broken)
        {
            if (broken == null) return null;
            return broken.GetComponent<MineRock5>() != null || broken.GetComponent<MineRock>() != null ? broken : null;
        }

        /// <summary>How much it takes to break, with what tool, and what it resists.</summary>
        private void Hits(float health, bool perPiece, int toolTier, HitData.DamageModifiers resists)
        {
            if (health > 0f) Add("Health", Number(health) + (perPiece ? " a piece" : ""));
            if (toolTier > 0) Add("Needs tool tier", toolTier.ToString(CultureInfo.InvariantCulture));
            Resists(resists);
        }

        /// <summary>
        /// A drop table as a row: its title says how often and how many times, each chip how many
        /// and its share. A chest's is told as what it holds.
        /// </summary>
        private void Drops(DropTable table, string lead, bool holds = false)
        {
            if (table?.m_drops == null) return;
            var info = new DropTableInfo { Min = table.m_dropMin, Max = table.m_dropMax, Chance = table.m_dropChance, OneOfEach = table.m_oneOfEach };
            var items = new List<GameObject>();
            foreach (var drop in table.m_drops)
            {
                if (drop.m_item == null) continue;
                info.Drops.Add(new DropInfo(drop.m_item.name, drop.m_stackMin, drop.m_stackMax, drop.m_weight));
                items.Add(drop.m_item);
            }
            if (DropWords.IsEmpty(info)) return;

            var title = holds ? DropWords.HoldsTitle(info) : DropWords.Title(info);
            if (lead != null) title = lead + char.ToLowerInvariant(title[0]) + title.Substring(1);
            var row = new Row { Title = title };
            for (var i = 0; i < items.Count; i++)
            {
                row.Items.Add(new Ingredient { Icon = Icon(items[i]), Name = ItemName(items[i]), Amount = DropWords.Amount(info, info.Drops[i]), Prefab = items[i].name });
            }
            Rows.Add(row);
            _drops = true;
        }

        /// <summary>The degrees of a damage modifier, from the most harm taken to the least.</summary>
        private static readonly HitData.DamageModifier[] Degrees =
        {
            HitData.DamageModifier.VeryWeak, HitData.DamageModifier.Weak, HitData.DamageModifier.SlightlyWeak, HitData.DamageModifier.SlightlyResistant,
            HitData.DamageModifier.Resistant, HitData.DamageModifier.VeryResistant, HitData.DamageModifier.Immune, HitData.DamageModifier.Ignore,
        };

        /// <summary>How a damage modifier reads as a label, e.g. "Weak to".</summary>
        private static string ModifierWords(HitData.DamageModifier modifier)
        {
            switch (modifier)
            {
                case HitData.DamageModifier.VeryWeak: return "Very weak to";
                case HitData.DamageModifier.Weak: return "Weak to";
                case HitData.DamageModifier.SlightlyWeak: return "Slightly weak to";
                case HitData.DamageModifier.SlightlyResistant: return "Slightly resists";
                case HitData.DamageModifier.Resistant: return "Resists";
                case HitData.DamageModifier.VeryResistant: return "Strongly resists";
                case HitData.DamageModifier.Immune: return "Immune to";
                case HitData.DamageModifier.Ignore: return "Unaffected by";
                default: return Naming.FieldLabel(modifier.ToString());
            }
        }

        // ----- Pieces -----

        private void Piece(Piece piece, WearNTear wear)
        {
            Description = CatalogBuilder.Localize(piece.m_description);
            // The build menu tab it is under, named as the list's group names it (not the game's category enum).
            if (_entry != null && _entry.Kind == Kind.Piece) Add("Build menu", _entry.Group == Groups.InNoMenu.Name ? "none" : _entry.Group);
            if (piece.m_comfort > 0)
            {
                // SE_Rested.CalculateComfortLevel counts, within 10 m, only the best of each comfort
                // group, and a piece of the same name once.
                Add("Comfort", piece.m_comfort.ToString(CultureInfo.InvariantCulture));
                Add("Comfort group", piece.m_comfortGroup != global::Piece.ComfortGroup.None
                    ? $"{Word(piece.m_comfortGroup)}: only the best of these within 10 m counts"
                    : "none: a second one within 10 m adds nothing");
            }
            if (wear != null)
            {
                Add("Health", Number(wear.m_health));
                Add("Material", Word(wear.m_materialType));
            }

            if (piece.m_resources != null && piece.m_resources.Length > 0)
            {
                var station = piece.m_craftingStation != null ? CatalogBuilder.Localize(piece.m_craftingStation.m_name) : "";
                var row = Requirements(station.Length > 0 ? "Built near " + station : "Build cost", piece.m_resources, false);
                row.TitleLink = piece.m_craftingStation != null ? piece.m_craftingStation.gameObject.name : null;
                Rows.Add(row);
            }
        }

        /// <summary>
        /// One of the game's own prefabs a mod made buildable, by adding a piece to it that it
        /// leaves switched off on the prefab (MoreVanillaBuildPrefabs does so): what it costs.
        /// </summary>
        private void MadeBuildable(Piece piece)
        {
            var by = BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(BuildPrefabsGuid, out var mod) && mod?.Metadata != null ? mod.Metadata.Name : "a mod";
            Add("Buildable", "through " + by);
            if (piece.m_resources == null || piece.m_resources.Length == 0) return;
            var station = piece.m_craftingStation != null ? CatalogBuilder.Localize(piece.m_craftingStation.m_name) : "";
            var row = Requirements(station.Length > 0 ? $"Built through {by} near {station}" : $"Built through {by} with", piece.m_resources, false);
            row.TitleLink = piece.m_craftingStation != null ? piece.m_craftingStation.gameObject.name : null;
            if (row.Items.Count > 0) Rows.Add(row);
        }

        /// <summary>MoreVanillaBuildPrefabs' plugin id, as it loads ("Loading [MoreVanillaBuildPrefabs 1.5.0] (Searica.Valheim.MoreVanillaBuildPrefabs)").</summary>
        private const string BuildPrefabsGuid = "Searica.Valheim.MoreVanillaBuildPrefabs";

        /// <summary>
        /// What something costs. Each ingredient also says how many more each upgrade needs, but
        /// only for items that can be upgraded: the game fills that number in everywhere, pieces
        /// and single-quality items included, where it means nothing. Upgrade kits are left out, as
        /// the game leaves them out (<see cref="UpgradeKits"/>).
        /// </summary>
        private static Row Requirements(string title, Piece.Requirement[] requirements, bool upgradable)
        {
            var row = new Row { Title = title };
            if (requirements == null) return row;
            foreach (var need in requirements)
            {
                if (need?.m_resItem == null || need.m_upgraderResource) continue;
                var amount = need.m_amount.ToString(CultureInfo.InvariantCulture);
                if (upgradable && need.m_amountPerLevel > 0) amount += $", +{need.m_amountPerLevel} per quality";
                row.Items.Add(new Ingredient
                {
                    Icon = Icon(need.m_resItem.gameObject), Name = ItemName(need.m_resItem.gameObject), Amount = amount, Prefab = need.m_resItem.gameObject.name,
                });
            }
            return row;
        }

        // ----- What it is used for -----

        /// <summary>What an item is used for, a row for each kind of use and place, each thing it goes into a chip.</summary>
        private void Uses(string item)
        {
            foreach (var group in Knowledge.UsesOf(item))
            {
                var row = new Row { Title = UseTitle(group), TitleLink = group.Place != null && group.Place != "hand" ? group.Place : null };
                foreach (var (target, amount) in group.Targets)
                {
                    var prefab = Looks.Prefab(target);
                    // A recipe that takes none of it at first needs it only to upgrade what it makes.
                    var name = AnyName(prefab, target);
                    if (amount <= 0 && group.Kind == UseKind.Crafts) name += " (upgrades)";
                    var shown = amount > 0 ? amount.ToString(CultureInfo.InvariantCulture) : "";
                    row.Items.Add(new Ingredient { Icon = AnyIcon(prefab), Name = name, Amount = shown, Prefab = target });
                }
                if (row.Items.Count > 0) UseRows.Add(row);
            }
            OfferedFor(item);
        }

        private static string UseTitle(UseGroup group)
        {
            var place = group.Place != null && group.Place != "hand" ? AnyName(Looks.Prefab(group.Place), group.Place) : null;
            switch (group.Kind)
            {
                case UseKind.Crafts: return place != null ? $"Used to make at {place}" : "Used to make by hand";
                case UseKind.UpgradesPastTop: return "Takes these past their top quality, at an upgrade station";
                case UseKind.Builds: return place != null ? $"Used to build near {place}" : "Used to build";
                case UseKind.TurnsInto: return place != null ? $"{place} turns it into" : "Turned into";
                case UseKind.Fuels: return "Burnt as fuel by";
                default: return "Eaten by";
            }
        }

        /// <summary>A prefab's name as the game shows it: an item's, a piece's or a creature's, else the prefab's own.</summary>
        private static string AnyName(GameObject prefab, string fallback)
        {
            if (prefab == null) return fallback;
            var token = prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name
                        ?? prefab.GetComponent<Piece>()?.m_name
                        ?? prefab.GetComponent<Character>()?.m_name;
            var shown = CatalogBuilder.Localize(token);
            return shown.Length > 0 ? shown : prefab.name;
        }

        private static Sprite AnyIcon(GameObject prefab)
        {
            if (prefab == null) return null;
            var icons = prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
            if (icons != null && icons.Length > 0) return icons[0];
            var piece = prefab.GetComponent<Piece>();
            return piece != null ? piece.m_icon : null;
        }

        // ----- Status effects -----

        /// <summary>
        /// Every setting of the status effect that differs from a fresh one of the same kind, which
        /// is what it actually changes. Works for mods' own kinds of status effect too.
        /// </summary>
        private void StatusEffect(StatusEffect effect)
        {
            Description = CatalogBuilder.Localize(effect.m_tooltip);

            // Its category is an id the game never shows; what it means is that nothing giving
            // another effect of the same category can be eaten or drunk while it lasts
            // (Player.CanConsumeItem, SEMan.HaveStatusEffectCategory).
            if (!string.IsNullOrEmpty(effect.m_category) && ObjectDB.instance != null)
            {
                var kin = ObjectDB.instance.m_StatusEffects
                    .Where(other => other != null && other != effect && other.m_category == effect.m_category)
                    .Select(EffectName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
                if (kin.Count > 0) Add("While it lasts, cannot take", string.Join(", ", kin));
            }

            // What the game's own tooltip says of its stats, in its words and units, where it can
            // be read (SE_Stats.GetTooltipString); the fields it tells are then not told again.
            var told = effect is SE_Stats && GameWords(effect);

            ScriptableObject blank = null;
            try
            {
                blank = ScriptableObject.CreateInstance(effect.GetType());
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                foreach (var field in effect.GetType().GetFields(flags))
                {
                    if (Skipped.Contains(field.Name)) continue;
                    if (told && ToldByTheGame.Contains(field.Name)) continue;
                    var type = field.FieldType;
                    if (type != typeof(float) && type != typeof(int) && type != typeof(bool) && !type.IsEnum) continue;

                    var value = field.GetValue(effect);
                    if (Equals(value, field.GetValue(blank))) continue;
                    // A time is told with its unit ("Cooldown 20 min", not 1200).
                    var shown = value is float seconds && IsTime(field.Name) ? Naming.Duration(seconds) : Shown(value);
                    if (shown != null) Add(Naming.FieldLabel(field.Name), shown);
                }

                if (effect is SE_Stats stats && stats.m_mods != null)
                {
                    foreach (var pair in stats.m_mods)
                    {
                        var damage = Word(pair.m_type);
                        if (damage != null) Add(ModifierWords(pair.m_modifier), damage.ToLowerInvariant());
                    }
                }
            }
            finally
            {
                if (blank != null) UnityEngine.Object.Destroy(blank);
            }
        }

        /// <summary>
        /// The game's own tooltip lines, each "Label: value" as a pair, and a skill's "Swords +15"
        /// by the skill; the description it starts with is left out, as the card shows it, and so
        /// is its duration. False where the tooltip cannot be read or tells nothing more.
        /// </summary>
        private bool GameWords(StatusEffect effect)
        {
            string text;
            try
            {
                text = Localization.instance != null ? Localization.instance.Localize(effect.GetTooltipString()) : null;
            }
            catch
            {
                // A mod's effect that needs a character to describe itself is told by its fields.
                return false;
            }
            if (string.IsNullOrEmpty(text)) return false;
            text = Naming.Plain(text).Replace("\r", "");
            // The tooltip starts with the description, which may hold blank lines of its own.
            var intro = Naming.Plain(Localization.instance.Localize(effect.m_tooltip ?? "")).Replace("\r", "");
            if (intro.Length > 0 && text.StartsWith(intro, StringComparison.Ordinal)) text = text.Substring(intro.Length);
            var lines = text.Split('\n').ToList();
            var duration = Localization.instance.Localize("$se_ttl");
            // Resistances are told as a creature's are, in the rows below.
            var modifier = Localization.instance.Localize("$inventory_dmgmod");
            var added = 0;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                var colon = line.IndexOf(':');
                var space = line.LastIndexOf(' ');
                string label, value;
                if (colon > 0)
                {
                    label = line.Substring(0, colon).Trim();
                    value = line.Substring(colon + 1).Trim();
                }
                else if (space > 0)
                {
                    label = line.Substring(0, space).Trim();
                    value = line.Substring(space + 1).Trim();
                }
                else continue;
                if (label.Length == 0 || value.Length == 0 || label == duration || label == modifier) continue;
                Add(label, value);
                added++;
            }
            return added > 0;
        }

        /// <summary>The fields <c>SE_Stats.GetTooltipString</c> tells.</summary>
        private static readonly HashSet<string> ToldByTheGame = new HashSet<string>
        {
            "m_addArmor", "m_addMaxCarryWeight", "m_adrenalineModifier", "m_adrenalineUpFront", "m_armorMultiplier", "m_attackStaminaUseModifier",
            "m_blockStaminaUseFlatValue", "m_blockStaminaUseModifier", "m_dodgeStaminaUseModifier", "m_eitrOverTime", "m_eitrRegenMultiplier",
            "m_eitrUpFront", "m_fallDamageModifier", "m_healthOverTime", "m_healthRegenMultiplier", "m_healthUpFront", "m_homeItemStaminaUseModifier",
            "m_jumpModifier", "m_jumpStaminaUseModifier", "m_maxMaxFallSpeed", "m_noiseModifier", "m_percentigeDamageModifiers",
            "m_runStaminaDrainModifier", "m_runStaminaUseModifier", "m_skillLevel", "m_skillLevelModifier", "m_skillLevel2", "m_skillLevelModifier2",
            "m_sneakStaminaUseModifier", "m_speedModifier", "m_staggerModifier", "m_staminaOverTime", "m_staminaRegenMultiplier", "m_staminaUpFront",
            "m_stealthModifier", "m_swimSpeedModifier", "m_swimStaminaUseModifier", "m_timedBlockBonus",
        };

        /// <summary>A field that holds a time in seconds, by its name.</summary>
        private static bool IsTime(string field)
        {
            var name = field.ToLowerInvariant();
            return (name.Contains("cooldown") || name.Contains("duration") || name.EndsWith("time") || name.Contains("interval")) && !name.Contains("multiplier") && !name.Contains("modifier");
        }

        /// <summary>Settings the card already shows, or that say nothing about what the effect does.</summary>
        private static readonly HashSet<string> Skipped = new HashSet<string>
        {
            "m_ttl", "m_name", "m_category", "m_tooltip", "m_icon", "m_iconText", "m_startMessage", "m_stopMessage",
            "m_repeatMessage", "m_startMessageType", "m_stopMessageType", "m_repeatMessageType", "m_flashIcon",
            "m_cooldownIcon", "m_nameHash", "m_activationAnimation", "m_repeatInterval",
        };

        // ----- Helpers -----

        private static string ItemName(GameObject item)
        {
            var shared = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
            var name = shared != null ? CatalogBuilder.Localize(shared.m_name) : "";
            return name.Length > 0 ? name : item != null ? item.name : "";
        }

        private static Sprite Icon(GameObject item)
        {
            var icons = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons : null;
            return icons != null && icons.Length > 0 ? icons[0] : null;
        }

        /// <summary>A value as text, or null for a choice the game has no name for (a mod's own numbered one).</summary>
        private static string Shown(object value)
        {
            if (value is float f) return Number(f);
            if (value is bool b) return b ? "yes" : "no";
            if (value is Enum e) return Word(e)?.ToLowerInvariant();
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A choice by its name, "OneHandedWeapon" as "One handed weapon". Null when the value has
        /// no name, which is how a mod's own categories and factions show up, as bare numbers.
        /// Flags that combine several names are joined.
        /// </summary>
        private static string Word(Enum value)
        {
            var text = value.ToString();
            if (text.Length > 0 && (char.IsDigit(text[0]) || text[0] == '-')) return null;
            return string.Join(", ", text.Split(new[] { ", " }, StringSplitOptions.None).Select(Naming.FieldLabel));
        }

        /// <summary>The status effect each kind of damage puts on what it hits, as <c>Character</c> adds them.</summary>
        private static readonly (string Damage, string Effect)[] DamageEffects =
        {
            ("fire", "Burning"), ("frost", "Frost"), ("lightning", "Lightning"), ("poison", "Poison"), ("spirit", "Spirit"),
        };

        private static string EffectName(StatusEffect effect)
        {
            var name = CatalogBuilder.Localize(effect.m_name);
            return name.Length > 0 ? name : effect.name;
        }

        private static string Number(float value)
        {
            return value.ToString(Math.Abs(value - Mathf.Round(value)) < 0.001f ? "0" : "0.##", CultureInfo.InvariantCulture);
        }

        private static string Percent(float value)
        {
            var percent = Mathf.RoundToInt(value * 100f);
            return (percent > 0 ? "+" : "") + percent + "%";
        }

        private static string Minutes(float seconds)
        {
            return Naming.Duration(seconds);
        }
    }
}
