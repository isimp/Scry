using System;
using System.Collections.Generic;
using System.Text;

namespace Scry
{
    /// <summary>
    /// Forgiving a slip of the keys: when a search finds nothing at all, each typed word no
    /// name holds is read as the word of the catalog's names it is one slip away from (a letter
    /// wrong, missing or extra, or two swapped), or whose start it is one slip away from while
    /// it is still being typed; words of three letters or fewer are left as they are, being too
    /// short to tell a slip from another word.
    /// </summary>
    internal static class SearchSlip
    {
        private const int Shortest = 4;

        /// <summary>Whether two words are at most one slip apart: equal, a letter changed, added or left out, or two side by side swapped.</summary>
        public static bool OneSlip(string a, string b) => OneSlip(a, b, b.Length);

        /// <summary>The same against the start of <paramref name="b"/>, its first so many letters, compared in place.</summary>
        private static bool OneSlip(string a, string b, int lengthB)
        {
            if (Math.Abs(a.Length - lengthB) > 1) return false;
            var start = 0;
            while (start < a.Length && start < lengthB && a[start] == b[start]) start++;
            if (start == a.Length && start == lengthB) return true;
            int endA = a.Length, endB = lengthB;
            while (endA > start && endB > start && a[endA - 1] == b[endB - 1])
            {
                endA--;
                endB--;
            }
            var restA = endA - start;
            var restB = endB - start;
            if (restA <= 1 && restB <= 1) return true;
            return restA == 2 && restB == 2 && a[start] == b[start + 1] && a[start + 1] == b[start];
        }

        /// <summary>Every word of the catalog's names, the game's and the prefab's, in small letters: parted at spaces, marks and digits, and where a capital follows a small letter.</summary>
        public static HashSet<string> Words(IEnumerable<Entry> catalog)
        {
            var words = new HashSet<string>(StringComparer.Ordinal);
            var word = new StringBuilder();
            void Take()
            {
                if (word.Length > 1) words.Add(word.ToString().ToLowerInvariant());
                word.Clear();
            }
            foreach (var entry in catalog)
            {
                foreach (var name in new[] { entry.Name, entry.DisplayName })
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    for (var i = 0; i < name.Length; i++)
                    {
                        var c = name[i];
                        if (!char.IsLetter(c))
                        {
                            Take();
                            continue;
                        }
                        if (char.IsUpper(c) && i > 0 && char.IsLower(name[i - 1])) Take();
                        word.Append(c);
                    }
                    Take();
                }
            }
            return words;
        }

        /// <summary>
        /// The word a typed word was meant to be, or null: one a slip away from it, else one whose
        /// start is a slip away from it, the shortest first, then in order of their letters.
        /// </summary>
        public static string Nearest(string typed, ICollection<string> words)
        {
            if (typed == null || typed.Length < Shortest) return null;
            typed = typed.ToLowerInvariant();
            string whole = null, started = null;
            foreach (var word in words)
            {
                if (OneSlip(typed, word))
                {
                    if (Better(word, whole)) whole = word;
                }
                else if (word.Length > typed.Length && StartsWithSlip(typed, word) && Better(word, started))
                {
                    started = word;
                }
            }
            return whole ?? started;
        }

        private static bool StartsWithSlip(string typed, string word)
        {
            for (var length = typed.Length - 1; length <= typed.Length + 1 && length <= word.Length; length++)
            {
                if (OneSlip(typed, word, length)) return true;
            }
            return false;
        }

        private static bool Better(string word, string than) => than == null || word.Length < than.Length || (word.Length == than.Length && string.CompareOrdinal(word, than) < 0);

        /// <summary>Whether any name, the game's or the prefab's, holds the word, whatever the case.</summary>
        public static bool AnyNameHolds(IEnumerable<Entry> catalog, string word)
        {
            foreach (var entry in catalog)
            {
                if ((entry.Name ?? "").IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 || (entry.DisplayName ?? "").IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>
        /// The search with each plain word no name holds read as the word it was meant to be,
        /// noted in <paramref name="mended"/> (cleared first); null where no word could be.
        /// </summary>
        public static string Mend(string text, IReadOnlyList<Entry> catalog, ICollection<string> words, List<(string Typed, string Used)> mended)
        {
            mended.Clear();
            foreach (var typed in Search.Parse(text).Words)
            {
                if (AnyNameHolds(catalog, typed)) continue;
                var meant = Nearest(typed, words);
                if (meant != null) mended.Add((typed, meant));
            }
            if (mended.Count == 0) return null;
            var parts = text.Split(' ');
            for (var i = 0; i < parts.Length; i++)
            {
                foreach (var (typed, used) in mended) if (parts[i] == typed) parts[i] = used;
            }
            return string.Join(" ", parts);
        }
    }
}
