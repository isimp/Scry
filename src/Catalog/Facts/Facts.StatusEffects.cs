using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Scry
{
    /// <summary>A status effect's facts, in the game's own words where it has them.</summary>
    internal sealed partial class Facts
    {
        // ----- Status effects -----

        /// <summary>
        /// Every setting of the status effect that differs from a fresh one of the same kind, which
        /// is what it actually changes. Works for mods' own kinds of status effect too.
        /// </summary>
        private void StatusEffect(StatusEffect effect)
        {
            Description = CatalogBuilder.Localize(effect.m_tooltip);

            // Told here as well as on the card, since an effect seen on a person has no card.
            Add("Lasts", Lasting(effect));

            // Its category is an id the game never shows; what it means is that nothing giving
            // another effect of the same category can be eaten or drunk while it lasts
            // (Player.CanConsumeItem, SEMan.HaveStatusEffectCategory).
            if (!string.IsNullOrEmpty(effect.m_category) && ObjectDB.instance != null)
            {
                var kin = ObjectDB.instance.m_StatusEffects
                    .Where(other => other != null && other != effect && other.m_category == effect.m_category)
                    .GroupBy(EffectName).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
                if (kin.Count > 0)
                {
                    var row = new Row { Title = "While it lasts, cannot take" };
                    foreach (var named in kin)
                    {
                        var page = EntryOf(EntryKeys.For(Kind.StatusEffect, named.First().name));
                        row.Items.Add(page != null ? EntryChip(page) : new Ingredient { Name = named.Key, Amount = "" });
                    }
                    Rows.Add(row);
                }
            }

            // What the game's own tooltip says of its stats, in its words and units, where it can
            // be read (SE_Stats.GetTooltipString); the fields it tells are then not told again.
            var told = effect is SE_Stats && GameWords(effect);
            // What a damage type's effect really does, in words; its settings are then not told again.
            var damageTold = DamageEffect(effect);

            ScriptableObject blank = null;
            try
            {
                blank = ScriptableObject.CreateInstance(effect.GetType());
                foreach (var field in TypeFields.Matching(StatFields, effect.GetType(), f =>
                    f.IsPublic && (f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(bool) || f.FieldType.IsEnum)))
                {
                    if (Skipped.Contains(field.Name)) continue;
                    if (told && ToldByTheGame.Contains(field.Name)) continue;
                    if (damageTold && ToldAsDamage.Contains(field.Name)) continue;

                    var value = TypeFields.Value(field, effect);
                    if (Equals(value, TypeFields.Value(field, blank))) continue;
                    // A time is told with its unit ("Cooldown 20 min", not 1,200).
                    var shown = value is float seconds && StatusEffectWords.IsTime(field.Name) ? Numbers.Duration(seconds) : FactWords.Value(value);
                    if (shown != null) Add(Naming.FieldLabel(field.Name), shown);
                }

                if (effect is SE_Stats stats && stats.m_mods != null)
                {
                    foreach (var pair in stats.m_mods)
                    {
                        var damage = FactWords.Choice(pair.m_type);
                        if (damage != null) Add(ResistWords.Label(DegreeOf(pair.m_modifier)), damage.ToLowerInvariant());
                    }
                }
            }
            finally
            {
                if (blank != null) UnityEngine.Object.Destroy(blank);
            }
        }

        /// <summary>
        /// The game's own tooltip lines as lines of the page (<see cref="StatusEffectWords.TooltipPairs"/>);
        /// its duration and resistances are told apart, resistances as a creature's are, in the rows
        /// below. False where the tooltip cannot be read or tells nothing more.
        /// </summary>
        private bool GameWords(StatusEffect effect)
        {
            // A mod's effect that needs a character to describe itself is told by its fields instead.
            if (Steps.Run(() => Localization.instance != null ? Localization.instance.Localize(effect.GetTooltipString()) : null, out var text, null) != null) return false;
            if (string.IsNullOrEmpty(text)) return false;
            var pairs = StatusEffectWords.TooltipPairs(Naming.Plain(text), Naming.Plain(Localization.instance.Localize(effect.m_tooltip ?? "")),
                Localization.instance.Localize("$se_ttl"), Localization.instance.Localize("$inventory_dmgmod"));
            AddAll(pairs);
            return pairs.Count > 0;
        }

        /// <summary>How long a status effect lasts in words: a poison's or frost's as each hit sets it, others by their own time.</summary>
        public static string Lasting(StatusEffect effect)
        {
            if (effect is SE_Poison) return DamageEffectWords.PoisonLasts;
            if (effect is SE_Frost) return DamageEffectWords.FrostLasts;
            return StatusEffectWords.Lasts(effect.m_ttl);
        }

        /// <summary>
        /// What the effect a damage type puts on whatever it hits does, from its own settings
        /// (<see cref="DamageEffectWords"/>), as the game deals it (Character.RPC_Damage): fire or
        /// spirit over its time (SE_Burning.AddFireDamage, SE_Burning.AddSpiritDamage,
        /// SE_Burning.UpdateStatusEffect, with Wet and resting by SE_Wet.UpdateStatusEffect and
        /// Player.UpdateEnvStatusEffects), poison over a time growing with it (SE_Poison.AddDamage,
        /// SE_Poison.UpdateStatusEffect), frost's slowing (SE_Frost.AddDamage, SE_Frost.ModifySpeed),
        /// lightning's mark (Character.AddLightningDamage). False for any other effect.
        /// </summary>
        private bool DamageEffect(StatusEffect effect)
        {
            switch (effect)
            {
                case SE_Burning burning:
                    // The one class burns and haunts: the game adds "Spirit" for spirit damage.
                    AddAll(DamageEffectWords.Burns(burning.NameHash() == SEMan.s_statusEffectSpirit, burning.m_ttl, burning.m_damageInterval));
                    return true;

                case SE_Poison poison:
                    AddAll(DamageEffectWords.Poison(poison.m_baseTTL, poison.m_TTLPerDamagePlayer, poison.m_TTLPerDamage, poison.m_TTLPower, poison.m_damageInterval));
                    Table(DamageEffectWords.PoisonTitle, DamageEffectWords.PoisonColumns,
                        DamageEffectWords.PoisonLines(poison.m_baseTTL, poison.m_TTLPerDamagePlayer, poison.m_TTLPerDamage, poison.m_TTLPower, poison.m_damageInterval));
                    return true;

                case SE_Frost frost:
                    AddAll(DamageEffectWords.Frost(frost.m_freezeTimePlayer, frost.m_freezeTimeEnemy, frost.m_minSpeedFactor));
                    var multipliers = (frost.m_frostSlowMultipliers ?? new List<ResistanceMultiplier>()).Select(m => (DegreeOf(m.m_modifier), m.m_multiplier));
                    Table(DamageEffectWords.FrostSlowTitle, DamageEffectWords.FrostSlowColumns, DamageEffectWords.FrostSlowLines(frost.m_minSpeedFactor, multipliers));
                    Table(DamageEffectWords.FrostTimeTitle, DamageEffectWords.FrostTimeColumns, DamageEffectWords.FrostTimeLines(frost.m_freezeTimePlayer, frost.m_freezeTimeEnemy));
                    return true;

                default:
                    // Lightning's own effect is a plain one, read nowhere but for its icon and marks.
                    if (effect.GetType() != typeof(StatusEffect) || effect.NameHash() != SEMan.s_statusEffectLightning) return false;
                    AddAll(DamageEffectWords.Lightning(effect.m_ttl));
                    return true;
            }
        }

        /// <summary>A table row of the page: its title, columns and lines, none going anywhere.</summary>
        private void Table(string title, string[] columns, IEnumerable<string[]> lines)
        {
            var row = new Row { Title = title, Columns = columns };
            foreach (var line in lines) row.Lines.Add((line, null));
            if (row.Lines.Count > 0) Rows.Add(row);
        }

        /// <summary>The settings <see cref="DamageEffect"/> tells in words.</summary>
        private static readonly HashSet<string> ToldAsDamage = new HashSet<string>
        {
            "m_damageInterval", "m_baseTTL", "m_TTLPerDamagePlayer", "m_TTLPerDamage", "m_TTLPower", "m_freezeTimeEnemy", "m_freezeTimePlayer", "m_minSpeedFactor",
        };

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

        /// <summary>The public stats of each kind of status effect, found once per type.</summary>
        private static readonly Dictionary<Type, FieldInfo[]> StatFields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>Settings told in their own words already (how long it lasts, its name, icon and tooltip), or that say nothing about what the effect does.</summary>
        private static readonly HashSet<string> Skipped = new HashSet<string>
        {
            "m_ttl", "m_name", "m_category", "m_tooltip", "m_icon", "m_iconText", "m_startMessage", "m_stopMessage",
            "m_repeatMessage", "m_startMessageType", "m_stopMessageType", "m_repeatMessageType", "m_flashIcon",
            "m_cooldownIcon", "m_nameHash", "m_activationAnimation", "m_repeatInterval",
        };
    }
}
