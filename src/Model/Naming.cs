using System;
using System.Collections.Generic;
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
            return string.IsNullOrEmpty(of) ? label : OfPart(of, label);
        }

        private static readonly System.Text.RegularExpressions.Regex Markup = new System.Text.RegularExpressions.Regex(
            @"</?(color|b|i|size|material|quad|sprite|u|s|sup|sub|mark|font|align|alpha|cspace|indent|line-height|lowercase|uppercase|smallcaps|noparse|nobr|space|voffset|width|link|style|rotate|pos)(=[^>]*)?>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>A text begun with a capital: "health ×2" as "Health ×2".</summary>
        public static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        /// <summary>A member by the type or field it is in: "m_attack.m_hitEffect".</summary>
        public static string MemberPath(string owner, string member) => owner + "." + member;

        /// <summary>A count with its noun in the number it takes: "1 star", "1,234 stars".</summary>
        public static string Count(int count, string one, string many) => Numbers.Count(count) + " " + Noun(count, one, many);

        /// <summary>The word that goes with a count: one's for one, many's otherwise ("stays", "stay").</summary>
        public static string Noun(int count, string one, string many) => count == 1 ? one : many;

        /// <summary>Lines one under another, leaving out those with nothing: a tip's.</summary>
        public static string Lines(params string[] lines) => Lines((System.Collections.Generic.IEnumerable<string>)lines);

        /// <summary>Lines one under another, leaving out those with nothing.</summary>
        public static string Lines(System.Collections.Generic.IEnumerable<string> lines)
        {
            var text = new StringBuilder();
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                if (text.Length > 0) text.Append('\n');
                text.Append(line);
            }
            return text.ToString();
        }

        /// <summary>A text as a sentence: begun with a capital, ended with a full stop.</summary>
        public static string Sentence(string text) => Capital(text) + ".";

        /// <summary>Names one after another with commas: "A, B, C".</summary>
        public static string Commas(System.Collections.Generic.IEnumerable<string> names) => string.Join(", ", names);

        /// <summary>An effect list's label led by the part it is on, where the part must be said: "Troll club: hit".</summary>
        public static string OfPart(string part, string label) => part + ": " + label.ToLowerInvariant();

        /// <summary>An effect list's label followed by the part it is on, where two lists share a label: "Hit (item drop)".</summary>
        public static string WithPart(string label, string part) => $"{label} ({FieldLabel(part).ToLowerInvariant()})";

        /// <summary>A name as the game shows it, with its prefab's where they differ: "Greydwarf nest (Spawner_GreydwarfNest)"; the prefab's alone without one.</summary>
        public static string WithPrefab(string shown, string prefab) =>
            !string.IsNullOrEmpty(shown) && shown != prefab ? $"{shown} ({prefab})" : prefab;

        /// <summary>Names shown side by side, each a thing's name with its prefab: one whose name another of them shares, with a different prefab, says its prefab too ("bjorn bite (bjorn_slam)").</summary>
        public static List<string> TellApart(IReadOnlyList<(string Shown, string Prefab)> named)
        {
            var told = new List<string>(named.Count);
            for (var i = 0; i < named.Count; i++)
            {
                var shared = false;
                for (var j = 0; j < named.Count && !shared; j++)
                    shared = named[j].Shown == named[i].Shown && named[j].Prefab != named[i].Prefab;
                told.Add(shared ? WithPrefab(named[i].Shown, named[i].Prefab) : named[i].Shown);
            }
            return told;
        }

        /// <summary>A row's title with how many it holds: "Lives here (12)".</summary>
        public static string Counted(string title, int count) => $"{title} ({Numbers.Count(count)})";

        /// <summary>Something found more than once, with how many times: "Collider ×3"; once, its name alone.</summary>
        public static string Repeated(string name, int count) => count > 1 ? name + " " + Numbers.Times(count) : name;

        /// <summary>A list of names as a sentence: "A", "A and B", "A, B and C"; nothing for none.</summary>
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

        /// <summary>The tags in a prefab's name that tell how it is built, not what it is: left out of its readable name.</summary>
        private static readonly HashSet<string> BuildTags = new HashSet<string>(StringComparer.Ordinal) { "frac", "clone", "pickable", "test", "prop" };

        /// <summary>The biomes' words as prefabs carry them, "heath" for the Plains and "coast" for the shore among them.</summary>
        private static readonly HashSet<string> BiomeTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "meadows", "blackforest", "forest", "swamp", "mountain", "plains", "heath", "mistlands", "ashlands", "ashland", "deepnorth", "coast", "ocean",
        };

        /// <summary>
        /// A name a player would say for something the game gives no name, or names in its own
        /// shorthand: its words in sentence case, without numbers, its copy number, "(Clone)" or
        /// the tags telling how it is built: "rock4_heath_frac" as "Rock heath",
        /// "Pickable_MountainCaveRandom" as "Mountain cave random", "fireballattack" as "Fireball
        /// attack". A name of nothing else stays as it is.
        /// </summary>
        public static string Readable(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var words = new List<string>();
            foreach (var word in PrefabWords(name))
            {
                if (BuildTags.Contains(word) || IsNumber(word)) continue;
                // The game names some attacks in one word: "fireballattack".
                if (word.Length > 6 && word.EndsWith("attack", StringComparison.Ordinal))
                {
                    words.Add(word.Substring(0, word.Length - 6));
                    words.Add("attack");
                }
                else words.Add(word);
            }
            if (words.Count == 0) return name.Trim();
            words[0] = char.ToUpperInvariant(words[0][0]) + words[0].Substring(1);
            return string.Join(" ", words.ToArray());
        }

        /// <summary>A readable name without the biome it tells (<see cref="Readable"/>), so things alike but for their biome go by one: "Rock heath" as "Rock"; one of nothing but a biome stays as it is.</summary>
        public static string WithoutBiome(string named)
        {
            if (string.IsNullOrEmpty(named)) return named ?? "";
            var words = named.Split(' ');
            var kept = new List<string>();
            foreach (var word in words) if (!BiomeTags.Contains(word.ToLowerInvariant())) kept.Add(word);
            if (kept.Count == 0) return named;
            kept[0] = char.ToUpperInvariant(kept[0][0]) + kept[0].Substring(1);
            return string.Join(" ", kept.ToArray());
        }

        /// <summary>
        /// A name without the numbers at its end telling one variant from the next, as a mod
        /// numbers its pieces: "Rock (4)" and "Rock 1 (3)" as "Rock", "Cliff (Creep) (1)" as
        /// "Cliff (Creep)"; a size such as "4x2" is part of the name, and one of nothing but a
        /// number stays as it is.
        /// </summary>
        public static string WithoutVariant(string named)
        {
            if (string.IsNullOrEmpty(named)) return named ?? "";
            var kept = named.TrimEnd();
            for (var cut = kept.LastIndexOf(' '); cut > 0 && IsVariantNumber(kept.Substring(cut + 1)); cut = kept.LastIndexOf(' '))
            {
                kept = kept.Substring(0, cut).TrimEnd();
            }
            return kept.Length > 0 ? kept : named;
        }

        /// <summary>A number telling a variant from the next, bare or in brackets: "1", "(01)".</summary>
        private static bool IsVariantNumber(string word) =>
            IsNumber(word.Length > 2 && word[0] == '(' && word[word.Length - 1] == ')' ? word.Substring(1, word.Length - 2) : word);

        /// <summary>A prefab's name in lower-case words: split at underscores, spaces, dashes and brackets, where a capital follows a small letter, and between letters and digits, so a copy number or "(Clone)" is a word of its own.</summary>
        private static List<string> PrefabWords(string name)
        {
            var text = name.Trim();
            var words = new List<string>();
            var word = new StringBuilder();
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '_' || c == ' ' || c == '-' || c == '(' || c == ')')
                {
                    Flush(word, words);
                    continue;
                }
                var previous = i > 0 ? text[i - 1] : ' ';
                if (word.Length > 0 && ((char.IsUpper(c) && char.IsLower(previous)) || (char.IsDigit(c) && char.IsLetter(previous)) || (char.IsLetter(c) && char.IsDigit(previous)))) Flush(word, words);
                word.Append(char.ToLowerInvariant(c));
            }
            Flush(word, words);
            return words;
        }

        private static void Flush(StringBuilder word, List<string> words)
        {
            if (word.Length > 0) words.Add(word.ToString());
            word.Length = 0;
        }

        private static bool IsNumber(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            foreach (var c in word) if (!char.IsDigit(c)) return false;
            return true;
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
