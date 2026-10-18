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
        };

        /// <summary>A setting's label: in players' words where it has some, else its name in words.</summary>
        public static string Label(string field) => Labels.TryGetValue(field, out var label) ? label : Naming.FieldLabel(field);

        /// <summary>
        /// A setting's value as what it does: a damage share times as much, a training share more,
        /// a skill "All" as every skill; else as it is shown (<paramref name="shown"/>).
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
                default:
                    return shown;
            }
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
