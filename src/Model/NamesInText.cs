using System;
using System.Collections.Generic;
using System.Text;

namespace Scry
{
    /// <summary>
    /// The names of entries a line of text holds, for the self-test to find what is told as text
    /// where a chip could go to it: whole words, the first capitalised as the game names things,
    /// its later words in any case, never across punctuation, the longest name at a place taken,
    /// up to <see cref="MostWords"/> words long. A possessive names its owner.
    /// </summary>
    public static class NamesInText
    {
        public const int MostWords = 4;

        public static List<string> Find(string text, ICollection<string> names)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(text)) return found;
            foreach (var run in Runs(text))
            {
                for (var i = 0; i < run.Count; i++)
                {
                    if (!char.IsUpper(run[i][0])) continue;
                    for (var n = Math.Min(MostWords, run.Count - i); n >= 1; n--)
                    {
                        var name = string.Join(" ", run.GetRange(i, n));
                        if (!names.Contains(name)) continue;
                        found.Add(name);
                        i += n - 1;
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>The text's words, in runs that punctuation ends; a word keeps its apostrophes and hyphens, less a possessive's.</summary>
        private static List<List<string>> Runs(string text)
        {
            var runs = new List<List<string>>();
            var run = new List<string>();
            var word = new StringBuilder();
            void EndWord()
            {
                if (word.Length == 0) return;
                var w = word.ToString();
                if (w.EndsWith("'s", StringComparison.Ordinal) || w.EndsWith("\u2019s", StringComparison.Ordinal)) w = w.Substring(0, w.Length - 2);
                if (w.Length > 0) run.Add(w);
                word.Clear();
            }
            void EndRun()
            {
                EndWord();
                if (run.Count > 0) runs.Add(run);
                run = new List<string>();
            }
            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c) || (word.Length > 0 && (c == '\'' || c == '\u2019' || c == '-'))) word.Append(c);
                else if (c == ' ') EndWord();
                else EndRun();
            }
            EndRun();
            return runs;
        }
    }
}
