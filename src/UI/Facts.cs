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
    /// resistances and drops, a piece's cost and comfort, what a status effect changes, and
    /// where things spawn, grow or come from. Read from the prefab, so mods' changes show.
    /// </summary>
    internal sealed class Facts
    {
        /// <summary>A row of items with amounts, such as a recipe or a creature's drops.</summary>
        public sealed class Row
        {
            public string Title = "";
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
        public readonly List<string> Where = new List<string>();
        public string WhereTitle = "Where it comes from";

        public bool IsEmpty => Description.Length == 0 && Pairs.Count == 0 && Rows.Count == 0 && Where.Count == 0;

        private void Add(string label, string value)
        {
            if (!string.IsNullOrEmpty(value)) Pairs.Add(new KeyValuePair<string, string>(label, value));
        }

        private static readonly Dictionary<Entry, Facts> Cache = new Dictionary<Entry, Facts>();

        public static Facts For(Entry entry)
        {
            if (entry == null) return new Facts();
            if (Cache.TryGetValue(entry, out var known)) return known;

            var facts = new Facts();
            try
            {
                if (entry.Source is StatusEffect effect)
                {
                    facts.StatusEffect(effect);
                    facts.WhereTitle = "What gives it";
                    facts.Where.AddRange(Knowledge.GiverLines(effect.name));
                }
                else if (entry.Source is GameObject prefab)
                {
                    facts.Prefab(prefab);
                    if (entry.Kind == Kind.Creature) facts.WhereTitle = "Where it lives";
                    facts.Where.AddRange(Knowledge.WhereLines(entry.Name));
                    facts.Where.AddRange(Knowledge.SourceLines(entry.Name));

                    // An item nothing makes, drops or sells here comes from somewhere Scry cannot see.
                    if (entry.Kind == Kind.Item && facts.Where.Count == 0 && !facts.Rows.Any(r => r.Title.StartsWith("Made")))
                    {
                        facts.Where.Add("Nothing loaded makes, drops or sells it. It may come from a location, a dungeon, an event or a mod.");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Scry could not read the facts of {entry.Name}: {ex.Message}");
            }

            Cache[entry] = facts;
            return facts;
        }

        /// <summary>Forgets everything read, for a new world.</summary>
        public static void Forget() => Cache.Clear();

        private void Prefab(GameObject prefab)
        {
            var drop = prefab.GetComponent<ItemDrop>();
            if (drop?.m_itemData?.m_shared != null) Item(prefab, drop.m_itemData.m_shared);

            var character = prefab.GetComponent<Character>();
            if (character != null) Creature(prefab, character);

            var piece = prefab.GetComponent<Piece>();
            if (piece != null) Piece(piece, prefab.GetComponent<WearNTear>());
        }

        // ----- Items -----

        private void Item(GameObject prefab, ItemDrop.ItemData.SharedData shared)
        {
            Description = CatalogBuilder.Localize(shared.m_description);

            Add("Type", Word(shared.m_itemType));
            Add("Weight", Number(shared.m_weight));
            if (shared.m_value > 0) Add("Worth", $"{shared.m_value} coins");
            if (shared.m_maxStackSize > 1) Add("Stacks to", shared.m_maxStackSize.ToString(CultureInfo.InvariantCulture));
            if (shared.m_maxQuality > 1) Add("Quality", $"up to {shared.m_maxQuality}");
            if (!shared.m_teleportable) Add("Portals", "cannot go through");

            var damage = Damages(shared.m_damages);
            if (damage.Length > 0)
            {
                Add("Damage", damage);
                Add("Per quality", Damages(shared.m_damagesPerLevel));
            }

            var type = shared.m_itemType;
            var worn = type == ItemDrop.ItemData.ItemType.Helmet || type == ItemDrop.ItemData.ItemType.Chest
                       || type == ItemDrop.ItemData.ItemType.Legs || type == ItemDrop.ItemData.ItemType.Shoulder;
            if (worn && shared.m_armor > 0f) Add("Armour", $"{Number(shared.m_armor)}, +{Number(shared.m_armorPerLevel)} per quality");
            if (type == ItemDrop.ItemData.ItemType.Shield && shared.m_blockPower > 0f) Add("Block", $"{Number(shared.m_blockPower)}, +{Number(shared.m_blockPowerPerLevel)} per quality");

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
            if (!string.IsNullOrEmpty(shared.m_setName)) Add("Set", shared.m_setName);
            if (shared.m_equipStatusEffect != null) Add("When worn", EffectName(shared.m_equipStatusEffect));
            if (shared.m_consumeStatusEffect != null) Add("When used", EffectName(shared.m_consumeStatusEffect));
            if (shared.m_attackStatusEffect != null) Add("On hit", EffectName(shared.m_attackStatusEffect));

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
                Rows.Add(Requirements(title, recipe.m_resources));
            }
        }

        private static string Damages(HitData.DamageTypes damages)
        {
            var parts = new List<string>();
            foreach (var field in typeof(HitData.DamageTypes).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(float) || field.Name == "m_nonPlayer") continue;
                var value = (float)field.GetValue(damages);
                if (value > 0f) parts.Add($"{Naming.FieldLabel(field.Name).ToLowerInvariant()} {Number(value)}");
            }
            return string.Join(", ", parts);
        }

        // ----- Creatures -----

        private void Creature(GameObject prefab, Character character)
        {
            Add("Health", Number(character.m_health));
            Add("Faction", Word(character.m_faction));
            if (character.m_boss) Add("Boss", "yes");

            var groups = new SortedDictionary<string, List<string>>();
            var mods = character.m_damageModifiers;
            foreach (var field in typeof(HitData.DamageModifiers).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(HitData.DamageModifier) || field.Name == "m_nonPlayer") continue;
                var modifier = (HitData.DamageModifier)field.GetValue(mods);
                if (modifier == HitData.DamageModifier.Normal) continue;

                var group = ModifierWords(modifier);
                if (!groups.TryGetValue(group, out var list)) groups[group] = list = new List<string>();
                list.Add(Naming.FieldLabel(field.Name).ToLowerInvariant());
            }
            foreach (var group in groups) Add(group.Key, string.Join(", ", group.Value));

            if (prefab.GetComponent<Tameable>() != null)
            {
                var ai = prefab.GetComponent<MonsterAI>();
                var food = ai?.m_consumeItems?.Where(i => i != null).Select(i => ItemName(i.gameObject)).ToList() ?? new List<string>();
                Add("Tameable", food.Count > 0 ? "yes, eats " + string.Join(", ", food) : "yes");
            }

            var drops = prefab.GetComponent<CharacterDrop>();
            if (drops != null && drops.m_drops.Count > 0)
            {
                var row = new Row { Title = "Drops" };
                foreach (var drop in drops.m_drops)
                {
                    if (drop?.m_prefab == null) continue;
                    var amount = drop.m_amountMin == drop.m_amountMax ? $"{drop.m_amountMin}" : $"{drop.m_amountMin}-{drop.m_amountMax}";
                    if (drop.m_chance < 1f) amount += $" ({Mathf.RoundToInt(drop.m_chance * 100f)}%)";
                    row.Items.Add(new Ingredient { Icon = Icon(drop.m_prefab), Name = ItemName(drop.m_prefab), Amount = amount, Prefab = drop.m_prefab.name });
                }
                if (row.Items.Count > 0) Rows.Add(row);
            }
        }

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
            Add("Category", Word(piece.m_category));
            if (piece.m_comfort > 0) Add("Comfort", piece.m_comfort.ToString(CultureInfo.InvariantCulture));
            if (wear != null)
            {
                Add("Health", Number(wear.m_health));
                Add("Material", Word(wear.m_materialType));
            }

            if (piece.m_resources != null && piece.m_resources.Length > 0)
            {
                var station = piece.m_craftingStation != null ? CatalogBuilder.Localize(piece.m_craftingStation.m_name) : "";
                Rows.Add(Requirements(station.Length > 0 ? "Built near " + station : "Build cost", piece.m_resources));
            }
        }

        private static Row Requirements(string title, Piece.Requirement[] requirements)
        {
            var row = new Row { Title = title };
            if (requirements == null) return row;
            foreach (var need in requirements)
            {
                if (need?.m_resItem == null) continue;
                var amount = need.m_amount.ToString(CultureInfo.InvariantCulture);
                if (need.m_amountPerLevel > 0) amount += $" (+{need.m_amountPerLevel})";
                row.Items.Add(new Ingredient
                {
                    Icon = Icon(need.m_resItem.gameObject), Name = ItemName(need.m_resItem.gameObject), Amount = amount, Prefab = need.m_resItem.gameObject.name,
                });
            }
            return row;
        }

        // ----- Status effects -----

        /// <summary>
        /// Every setting of the status effect that differs from a fresh one of the same kind, which
        /// is what it actually changes. Works for mods' own kinds of status effect too.
        /// </summary>
        private void StatusEffect(StatusEffect effect)
        {
            Description = CatalogBuilder.Localize(effect.m_tooltip);
            ScriptableObject blank = null;
            try
            {
                blank = ScriptableObject.CreateInstance(effect.GetType());
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                foreach (var field in effect.GetType().GetFields(flags))
                {
                    if (Skipped.Contains(field.Name)) continue;
                    var type = field.FieldType;
                    if (type != typeof(float) && type != typeof(int) && type != typeof(bool) && !type.IsEnum) continue;

                    var value = field.GetValue(effect);
                    if (Equals(value, field.GetValue(blank))) continue;
                    var shown = Shown(value);
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
            return seconds >= 120f ? $"{Mathf.RoundToInt(seconds / 60f)} min" : $"{Mathf.RoundToInt(seconds)} s";
        }
    }
}
