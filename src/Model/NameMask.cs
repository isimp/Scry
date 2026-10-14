using System;
using System.Collections.Generic;
using System.Text;

namespace Scry
{
    /// <summary>
    /// The names a known page's text keeps back under the strict rules: each whole name of what
    /// the character has not seen reads <see cref="CompendiumWords.Hidden"/>, the longest name
    /// first, never a part of a word. A name also borne by something it has seen stays, as do
    /// names too short to tell from a word.
    /// </summary>
    internal sealed class NameMask
    {
        private const int ShortestName = 3;

        private readonly HashSet<string> _hidden = new HashSet<string>(StringComparer.Ordinal);
        private readonly int _mostWords;

        /// <summary>A mask of the names not seen, less any also among the names seen.</summary>
        public NameMask(IEnumerable<string> unseen, IEnumerable<string> seen)
        {
            var keep = new HashSet<string>(seen ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (var name in unseen ?? Array.Empty<string>())
            {
                if (name == null || name.Length < ShortestName || keep.Contains(name) || !_hidden.Add(name)) continue;
                _mostWords = Math.Max(_mostWords, Spans(name).Count);
            }
        }

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
                    if (!_hidden.Contains(text.Substring(start, end - start))) continue;
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
