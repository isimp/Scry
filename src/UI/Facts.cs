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
        }

        public readonly List<string> Lines = new List<string>();
        public readonly List<Row> Rows = new List<Row>();
        public readonly List<string> Where = new List<string>();

        public bool IsEmpty => Lines.Count == 0 && Rows.Count == 0 && Where.Count == 0;

        private static readonly Dictionary<Entry, Facts> Cache = new Dictionary<Entry, Facts>();

        public static Facts For(Entry entry)
        {
            if (entry == null) return new Facts();
            if (Cache.TryGetValue(entry, out var known)) return known;

            var facts = new Facts();
            try
            {
                if (entry.Source is StatusEffect effect) facts.StatusEffect(effect);
                else if (entry.Source is GameObject prefab) facts.Prefab(prefab);

                facts.Where.AddRange(Knowledge.WhereLines(entry.Name));
                facts.Where.AddRange(Knowledge.SourceLines(entry.Name));
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
            var description = CatalogBuilder.Localize(shared.m_description);
            if (description.Length > 0) Lines.Add(description);

            var basics = new List<string> { Naming.FieldLabel(shared.m_itemType.ToString()) };
            basics.Add("weight " + Number(shared.m_weight));
            if (shared.m_value > 0) basics.Add($"worth {shared.m_value}");
            if (shared.m_maxStackSize > 1) basics.Add($"stacks to {shared.m_maxStackSize}");
            if (shared.m_maxQuality > 1) basics.Add($"quality up to {shared.m_maxQuality}");
            if (!shared.m_teleportable) basics.Add("cannot go through portals");
            Lines.Add(string.Join(", ", basics));

            var damage = Damages(shared.m_damages);
            if (damage.Length > 0)
            {
                var more = Damages(shared.m_damagesPerLevel);
                Lines.Add("Damage: " + damage + (more.Length > 0 ? $" (per quality: {more})" : ""));
            }

            var type = shared.m_itemType;
            var worn = type == ItemDrop.ItemData.ItemType.Helmet || type == ItemDrop.ItemData.ItemType.Chest
                       || type == ItemDrop.ItemData.ItemType.Legs || type == ItemDrop.ItemData.ItemType.Shoulder;
            if (worn && shared.m_armor > 0f) Lines.Add($"Armour {Number(shared.m_armor)} (per quality {Number(shared.m_armorPerLevel)})");
            if (type == ItemDrop.ItemData.ItemType.Shield && shared.m_blockPower > 0f) Lines.Add($"Block {Number(shared.m_blockPower)} (per quality {Number(shared.m_blockPowerPerLevel)})");

            if (shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f)
            {
                var food = new List<string>();
                if (shared.m_food > 0f) food.Add($"{Number(shared.m_food)} health");
                if (shared.m_foodStamina > 0f) food.Add($"{Number(shared.m_foodStamina)} stamina");
                if (shared.m_foodEitr > 0f) food.Add($"{Number(shared.m_foodEitr)} eitr");
                if (shared.m_foodRegen > 0f) food.Add($"heals {Number(shared.m_foodRegen)} a tick");
                if (shared.m_foodBurnTime > 0f) food.Add($"lasts {Minutes(shared.m_foodBurnTime)}");
                Lines.Add("Food: " + string.Join(", ", food));
            }

            if (shared.m_toolTier > 0) Lines.Add($"Tool tier {shared.m_toolTier}");
            if (Math.Abs(shared.m_movementModifier) > 0.001f) Lines.Add($"Movement {Percent(shared.m_movementModifier)}");
            if (!string.IsNullOrEmpty(shared.m_setName)) Lines.Add("Part of the set " + shared.m_setName);
            if (shared.m_equipStatusEffect != null)
            {
                var effect = CatalogBuilder.Localize(shared.m_equipStatusEffect.m_name);
                Lines.Add("When worn: " + (effect.Length > 0 ? effect : shared.m_equipStatusEffect.name));
            }

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
            var basics = new List<string> { $"Health {Number(character.m_health)}" };
            basics.Add(Naming.FieldLabel(character.m_faction.ToString()).ToLowerInvariant());
            if (character.m_boss) basics.Add("a boss");
            Lines.Add(string.Join(", ", basics));

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
            foreach (var group in groups) Lines.Add($"{group.Key} {string.Join(", ", group.Value)}");

            if (prefab.GetComponent<Tameable>() != null)
            {
                var ai = prefab.GetComponent<MonsterAI>();
                var food = ai?.m_consumeItems?.Where(i => i != null).Select(i => ItemName(i.gameObject)).ToList() ?? new List<string>();
                Lines.Add(food.Count > 0 ? "Can be tamed, eats " + string.Join(", ", food) : "Can be tamed");
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
                    row.Items.Add(new Ingredient { Icon = Icon(drop.m_prefab), Name = ItemName(drop.m_prefab), Amount = amount });
                }
                if (row.Items.Count > 0) Rows.Add(row);
            }
        }

        private static string ModifierWords(HitData.DamageModifier modifier)
        {
            switch (modifier)
            {
                case HitData.DamageModifier.VeryWeak: return "Very weak to";
                case HitData.DamageModifier.Weak: return "Weak to";
                case HitData.DamageModifier.SlightlyWeak: return "Slightly weak to";
                case HitData.DamageModifier.SlightlyResistant: return "Slightly resistant to";
                case HitData.DamageModifier.Resistant: return "Resistant to";
                case HitData.DamageModifier.VeryResistant: return "Very resistant to";
                case HitData.DamageModifier.Immune: return "Immune to";
                case HitData.DamageModifier.Ignore: return "Unaffected by";
                default: return modifier.ToString();
            }
        }

        // ----- Pieces -----

        private void Piece(Piece piece, WearNTear wear)
        {
            var basics = new List<string> { Naming.FieldLabel(piece.m_category.ToString()) };
            if (piece.m_comfort > 0) basics.Add($"comfort {piece.m_comfort}");
            if (wear != null)
            {
                basics.Add($"health {Number(wear.m_health)}");
                basics.Add(Naming.FieldLabel(wear.m_materialType.ToString()).ToLowerInvariant());
            }
            Lines.Add(string.Join(", ", basics));

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
                row.Items.Add(new Ingredient { Icon = Icon(need.m_resItem.gameObject), Name = ItemName(need.m_resItem.gameObject), Amount = amount });
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
                    Lines.Add($"{Naming.FieldLabel(field.Name)}: {Shown(value)}");
                }

                if (effect is SE_Stats stats && stats.m_mods != null)
                {
                    foreach (var pair in stats.m_mods)
                    {
                        Lines.Add($"{ModifierWords(pair.m_modifier)} {pair.m_type.ToString().ToLowerInvariant()}");
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

        private static string Shown(object value)
        {
            if (value is float f) return Number(f);
            if (value is bool b) return b ? "yes" : "no";
            if (value != null && value.GetType().IsEnum) return Naming.FieldLabel(value.ToString()).ToLowerInvariant();
            return Convert.ToString(value, CultureInfo.InvariantCulture);
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
