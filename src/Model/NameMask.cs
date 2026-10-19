using System;
using System.Collections.Generic;
using System.Text;

namespace Scry
{
    /// <summary>
    /// The names a known page's text keeps back with the compendium on: each whole name of what
    /// the character has not seen reads <see cref="CompendiumWords.Hidden"/>, in any case and as
    /// a plain plural with an s, as the game's texts name things, the longest name first, never a
    /// part of a word. A name also borne by something it has seen stays, as do names too short
    /// to tell from a word.
    /// </summary>
    internal sealed class NameMask
    {
        private const int ShortestName = 3;

        private readonly HashSet<string> _hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly int _mostWords;

        /// <summary>A mask of the names not seen, less any also among the names seen.</summary>
        public NameMask(IEnumerable<string> unseen, IEnumerable<string> seen)
        {
            var keep = new HashSet<string>(seen ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var name in unseen ?? Array.Empty<string>())
            {
                if (name == null || name.Length < ShortestName || keep.Contains(name) || !_hidden.Add(name)) continue;
                _mostWords = Math.Max(_mostWords, Spans(name).Count);
            }
        }

        /// <summary>
        /// Whether a thing's shown name may be kept back in a text: a name the game gives a thing
        /// of the world a player meets. One Scry made of its prefab's words (<paramref name="byWords"/>:
        /// a dungeon room, a place the game names nothing, or anything the game gives no name) is
        /// no name a text holds, and kept back it would hide a plain word ("Well" in "feel so well");
        /// so is a status effect's, mostly a word of what it does to you ("Fire" in "fires").
        /// </summary>
        public static bool Keeps(Entry entry, bool byWords) =>
            entry != null && !byWords && Kinds.ForPlayers(entry) && entry.Kind != Kind.StatusEffect && !string.IsNullOrEmpty(entry.DisplayName);

        /// <summary>The text with each whole name kept back.</summary>
        public string Apply(string text)
        {
            if (string.IsNullOrEmpty(text) || _hidden.Count == 0) return text;
            var words = Spans(text);
            StringBuilder masked = null;
            var copied = 0;
            for (var w = 0; w < words.Count; w++)
            {
                for (var n = Math.Min(_mostWords, words.Count - w); n >= 1; n--)
                {
                    var start = words[w].Start;
                    var end = words[w + n - 1].End;
                    if (!Hidden(text.Substring(start, end - start))) continue;
                    if (masked == null) masked = new StringBuilder(text.Length);
                    masked.Append(text, copied, start - copied).Append(CompendiumWords.Hidden);
                    copied = end;
                    w += n - 1;
                    break;
                }
            }
            if (masked == null) return text;
            return masked.Append(text, copied, text.Length - copied).ToString();
        }

        /// <summary>Whether words name what is kept back: as they are, or many of it with a plain s.</summary>
        private bool Hidden(string words) =>
            _hidden.Contains(words) || (words.Length > ShortestName && (words[words.Length - 1] == 's' || words[words.Length - 1] == 'S') && _hidden.Contains(words.Substring(0, words.Length - 1)));

        /// <summary>Where each word of a text starts and ends: a run of letters and digits.</summary>
        private static List<(int Start, int End)> Spans(string text)
        {
            var spans = new List<(int, int)>();
            for (var i = 0; i < text.Length;)
            {
                if (!char.IsLetterOrDigit(text[i]))
                {
                    i++;
                    continue;
                }
                var start = i;
                while (i < text.Length && char.IsLetterOrDigit(text[i])) i++;
                spans.Add((start, i));
            }
            return spans;
        }
    }
}
