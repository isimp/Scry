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
            Add("Lasts", effect.m_ttl > 0f ? Naming.Duration(effect.m_ttl) : "no time limit of its own");

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

        /// <summary>Settings told in their own words already (how long it lasts, its name, icon and tooltip), or that say nothing about what the effect does.</summary>
        private static readonly HashSet<string> Skipped = new HashSet<string>
        {
            "m_ttl", "m_name", "m_category", "m_tooltip", "m_icon", "m_iconText", "m_startMessage", "m_stopMessage",
            "m_repeatMessage", "m_startMessageType", "m_stopMessageType", "m_repeatMessageType", "m_flashIcon",
            "m_cooldownIcon", "m_nameHash", "m_activationAnimation", "m_repeatInterval",
        };
    }
}
