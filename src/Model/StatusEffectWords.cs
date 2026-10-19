using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// A status effect in words: how long it lasts, the game's own tooltip told line by line
    /// (<c>SE_Stats.GetTooltipString</c>), and which of its settings hold a time, told with a unit.
    /// </summary>
    internal static class StatusEffectWords
    {
        /// <summary>The faint caption beside a status effect's button: it shows on you its look alone.</summary>
        public const string OnlyTheLook = "only the look, never the effect";

        /// <summary>The faint caption where a status effect's button would be, for one with nothing to show.</summary>
        public const string NoLook = "nothing of its own to see or hear";

        public static string Lasts(float seconds) => seconds > 0f ? Numbers.Duration(seconds) : "no time limit of its own";

        /// <summary>What its card says of how long it lasts.</summary>
        public static string Card(float seconds) => seconds > 0f ? PanelWords.Lasts(seconds) : Naming.Capital(Lasts(seconds));

        /// <summary>
        /// The tooltip's lines as lines of the page: each "Label: value", or a skill's "Swords +15"
        /// by the skill. The description it starts with is left out, as the page shows it, and so
        /// are its duration and its resistances, told apart; a line with no label or value tells
        /// nothing.
        /// </summary>
        public static List<FactPair> TooltipPairs(string tooltip, string intro, string durationLabel, string modifierLabel)
        {
            var pairs = new List<FactPair>();
            var text = (tooltip ?? "").Replace("\r", "");
            intro = (intro ?? "").Replace("\r", "");
            if (intro.Length > 0 && text.StartsWith(intro, StringComparison.Ordinal)) text = text.Substring(intro.Length);
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                var colon = line.IndexOf(':');
                var space = line.LastIndexOf(' ');
                string label, value;
                if (colon >= 0)
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
                if (label.Length == 0 || value.Length == 0 || label == durationLabel || label == modifierLabel) continue;
                pairs.Add(new FactPair(label, value));
            }
            return pairs;
        }

        /// <summary>The settings the game's tooltip does not tell, in players' words, by field (<c>SE_Stats</c>).</summary>
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["m_healthOverTimeInterval"] = "Heals every",
            ["m_healthOverTimeDuration"] = "Heals over",
            ["m_staminaOverTimeDuration"] = "Gives stamina over",
            ["m_modifyAttackSkill"] = "Stronger attacks with",
            ["m_damageModifier"] = "Their damage",
            ["m_raiseSkill"] = "Trains faster",
            ["m_raiseSkillModifier"] = "Trains faster by",
            ["m_attributes"] = "Also",
            ["m_effectsOnlyOnPlayer"] = "Shows on",
            ["m_staminaDrainPerSec"] = "Drains stamina",
        };

        /// <summary>
        /// What each mark an effect puts on a character does, where the game asks for it
        /// (<c>StatusEffect.StatusAttribute</c>): the wind on a ship one sails
        /// (<c>Ship.IsWindControllActive</c>), taming near one (<c>Tameable</c>), objects crashing
        /// into one (<c>ImpactEffect</c>); cold resistance the game asks for nowhere.
        /// </summary>
        private static readonly Dictionary<string, string> Marks = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SailingPower"] = "the wind blows the way your ship sails",
            ["TamingBoost"] = "tames animals near you faster",
            ["DoubleImpactDamage"] = "falling and rolling things hit twice as hard",
            ["ColdResistance"] = "a mark of cold resistance the game itself never reads",
        };

        /// <summary>A setting's label: in players' words where it has some, else its name in words.</summary>
        public static string Label(string field) => Labels.TryGetValue(field, out var label) ? label : Naming.FieldLabel(field);

        /// <summary>
        /// A setting's value as what it does: a damage share times as much, a training share more,
        /// a skill "All" as every skill, the marks it puts on a character by what the game does
        /// for each, a drain by the second; else as it is shown (<paramref name="shown"/>).
        /// </summary>
        public static string Shown(string field, object value, string shown)
        {
            switch (field)
            {
                case "m_damageModifier":
                    return value is float times ? Numbers.Times(times, 2) : shown;
                case "m_raiseSkillModifier":
                    return value is float share ? Numbers.Percent(share, 0, signed: true) : shown;
                case "m_raiseSkill":
                case "m_modifyAttackSkill":
                    return value?.ToString() == "All" ? "every skill" : shown;
                case "m_attributes":
                    return MarksOf(value?.ToString()) ?? shown;
                case "m_effectsOnlyOnPlayer":
                    return value is bool only && only ? "players only" : shown;
                case "m_staminaDrainPerSec":
                    return value is float drain ? Numbers.Amount(drain) + " a second" : shown;
                default:
                    return shown;
            }
        }

        /// <summary>The marks of a flags value ("TamingBoost, DoubleImpactDamage") in words, one a mod adds as its name in words; none for none.</summary>
        private static string MarksOf(string flags)
        {
            if (string.IsNullOrEmpty(flags)) return null;
            var said = new List<string>();
            foreach (var flag in flags.Split(','))
            {
                var name = flag.Trim();
                if (name.Length == 0) continue;
                said.Add(Marks.TryGetValue(name, out var words) ? words : Naming.Readable(name));
            }
            return said.Count > 0 ? string.Join("; ", said.ToArray()) : null;
        }

        /// <summary>Whether a setting holds a time in seconds, by its name: a cooldown, a duration, an interval, a time, but no multiplier or modifier of one.</summary>
        public static bool IsTime(string field)
        {
            var name = field.ToLowerInvariant();
            return (name.Contains("cooldown") || name.Contains("duration") || name.EndsWith("time", StringComparison.Ordinal) || name.Contains("interval"))
                   && !name.Contains("multiplier") && !name.Contains("modifier");
        }
    }
}
