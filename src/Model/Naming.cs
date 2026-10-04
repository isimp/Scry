using System;
using System.Text;

namespace Scry
{
    /// <summary>Plain names for things the game only names in code.</summary>
    internal static class Naming
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

        /// <summary>A list of names as a sentence: "A", "A and B", "A, B and C"; nothing for none.</summary>
        /// <summary>A row's title with how many it holds: "Lives here (12)".</summary>
        public static string Counted(string title, int count) => $"{title} ({Numbers.Count(count)})";

        public static string Joined(System.Collections.Generic.IReadOnlyList<string> names)
        {
            if (names.Count <= 1) return names.Count == 1 ? names[0] : "";
            var most = new string[names.Count - 1];
            for (var i = 0; i < most.Length; i++) most[i] = names[i];
            return string.Join(", ", most) + " and " + names[names.Count - 1];
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
