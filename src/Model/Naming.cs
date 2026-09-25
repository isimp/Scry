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
            if (name.StartsWith("m_")) name = name.Substring(2);
            return Words(name);
        }

        /// <summary>The name an effect list field goes by on a button, e.g. "m_startEffects" as "Start".</summary>
        public static string EffectListLabel(string field)
        {
            var name = field ?? "";
            if (name.StartsWith("m_")) name = name.Substring(2);
            if (name.EndsWith("Effects")) name = name.Substring(0, name.Length - "Effects".Length);
            else if (name.EndsWith("Effect")) name = name.Substring(0, name.Length - "Effect".Length);
            if (name.Length == 0 || name == "effects" || name == "effect") return "Effect";
            return Words(name);
        }

        private static readonly System.Text.RegularExpressions.Regex Markup = new System.Text.RegularExpressions.Regex(
            @"</?(color|b|i|size|material|quad|sprite|u|s|sup|sub|mark|font|align|alpha|cspace|indent|line-height|lowercase|uppercase|smallcaps|noparse|nobr|space|voffset|width|link|style|rotate|pos)(=[^>]*)?>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>A name without the rich-text tags some mods colour or size their names with.</summary>
        public static string Plain(string text)
        {
            return string.IsNullOrEmpty(text) ? text ?? "" : Markup.Replace(text, "");
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
