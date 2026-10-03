using System;
using System.Text;

namespace Scry
{
    /// <summary>Plain names for things the game only names in code.</summary>
    public static class Naming
    {
        /// <summary>The name a field goes by in the panel, e.g. "m_staminaRegenMultiplier" as "Stamina regen multiplier".</summary>
        public static string FieldLabel(string field)
        {
            var name = field ?? "";
            if (name.StartsWith("m_", StringComparison.Ordinal)) name = name.Substring(2);
            return Words(name);
        }

        /// <summary>The name an effect list field goes by on a button, e.g. "m_startEffects" as "Start".</summary>
        public static string EffectListLabel(string field)
        {
            var name = field ?? "";
            if (name.StartsWith("m_", StringComparison.Ordinal)) name = name.Substring(2);
            if (name.EndsWith("Effects", StringComparison.Ordinal)) name = name.Substring(0, name.Length - "Effects".Length);
            else if (name.EndsWith("Effect", StringComparison.Ordinal)) name = name.Substring(0, name.Length - "Effect".Length);
            if (name.Length == 0 || name == "effects" || name == "effect") return "Effect";
            return Words(name);
        }

        /// <summary>
        /// The name an effect list inside a field's game data goes by (a fire pit's fireworks, one
        /// list per firework): its own, unless it is only called "effect", then the field's. One of
        /// several says what it is for, as a part does ("Blue fireworks: fireworks").
        /// </summary>
        public static string NestedListLabel(string outer, string inner, string of = null)
        {
            var own = EffectListLabel(inner);
            var label = own == "Effect" ? EffectListLabel(outer) : own;
            return string.IsNullOrEmpty(of) ? label : of + ": " + label.ToLowerInvariant();
        }

        private static readonly System.Text.RegularExpressions.Regex Markup = new System.Text.RegularExpressions.Regex(
            @"</?(color|b|i|size|material|quad|sprite|u|s|sup|sub|mark|font|align|alpha|cspace|indent|line-height|lowercase|uppercase|smallcaps|noparse|nobr|space|voffset|width|link|style|rotate|pos)(=[^>]*)?>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>
        /// A number as every fact writes it: up to two decimals, none for a whole number. Times
        /// are the one exception, written by <see cref="Duration"/> to one decimal of their unit.
        /// </summary>
        public static string Number(float value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>An amount with up to two decimals and its thousands by commas: "1,234.5".</summary>
        public static string Amount(float value) => value.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A length in metres, as an amount: "1,500 m".</summary>
        public static string Metres(float value) => Amount(value) + " m";

        /// <summary>A list of names as a sentence: "A", "A and B", "A, B and C"; nothing for none.</summary>
        public static string Joined(System.Collections.Generic.IReadOnlyList<string> names)
        {
            if (names.Count <= 1) return names.Count == 1 ? names[0] : "";
            var most = new string[names.Count - 1];
            for (var i = 0; i < most.Length; i++) most[i] = names[i];
            return string.Join(", ", most) + " and " + names[names.Count - 1];
        }

        /// <summary>A count with its thousands by commas, whatever language the PC is set to: "1,234".</summary>
        public static string Count(int value) => value.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A length of time in the largest unit that reads well: "40 s", "25 min", "2.5 h".</summary>
        public static string Duration(float seconds)
        {
            string Number(float value) => value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            if (seconds < 120f) return Number(seconds) + " s";
            if (seconds < 7200f) return Number(seconds / 60f) + " min";
            return Number(seconds / 3600f) + " h";
        }

        /// <summary>From one time to another, the unit said once where both share it: "50–60 min", "90 s to 3 min".</summary>
        public static string DurationRange(float least, float most)
        {
            var low = Duration(least);
            var high = Duration(most);
            if (low == high) return low;
            var lowUnit = low.Substring(low.LastIndexOf(' ') + 1);
            var highUnit = high.Substring(high.LastIndexOf(' ') + 1);
            return lowUnit == highUnit ? $"{low.Substring(0, low.LastIndexOf(' '))}–{high}" : $"{low} to {high}";
        }

        /// <summary>A name without the rich-text tags some mods colour or size their names with.</summary>
        public static string Plain(string text)
        {
            // Every tag starts with "<"; most names have none, and are left without the regex.
            if (string.IsNullOrEmpty(text)) return text ?? "";
            return text.IndexOf('<') < 0 ? text : Markup.Replace(text, "");
        }

        /// <summary>"healthUpgrade" as "Health upgrade".</summary>
        private static string Words(string name)
        {
            var words = new StringBuilder();
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (i == 0) words.Append(char.ToUpperInvariant(c));
                else if (char.IsUpper(c)) words.Append(' ').Append(char.ToLowerInvariant(c));
                else words.Append(c);
            }
            return words.ToString();
        }
    }
}
