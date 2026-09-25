using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>Which prefabs to show, by origin.</summary>
    public enum OriginFilter
    {
        All,
        Vanilla,
        Mods,
    }

    /// <summary>Everything that narrows the list: the typed text, a kind, favourites, origin.</summary>
    public sealed class Query
    {
        public string Text = "";

        /// <summary>Only this kind, or every kind when null.</summary>
        public Kind? Kind;

        public bool FavouritesOnly;
        public OriginFilter Origin = OriginFilter.All;
    }

    public static class Search
    {
        private static readonly char[] Separators = { ' ', '\t' };

        // How well one word matches one name, best first.
        private const int Exact = 0;
        private const int Prefix = 1;
        private const int WordStart = 2;
        private const int Inside = 3;
        private const int Miss = int.MaxValue;

        /// <summary>Whether an entry matches the typed text, looking at both its names.</summary>
        public static bool Matches(Entry entry, string text)
        {
            return Score(entry, Words(text)) != Miss;
        }

        /// <summary>The entries that match, best match first.</summary>
        public static List<Entry> Run(IReadOnlyList<Entry> all, Query query, ICollection<string> favourites)
        {
            var words = Words(query.Text);
            var found = new List<KeyValuePair<int, Entry>>();

            foreach (var entry in all)
            {
                if (!Passes(entry, query, favourites)) continue;

                var score = Score(entry, words);
                if (score != Miss) found.Add(new KeyValuePair<int, Entry>(score, entry));
            }

            found.Sort((a, b) =>
            {
                var byScore = a.Key.CompareTo(b.Key);
                if (byScore != 0) return byScore;

                // Among equal matches the shorter name is the closer one: "Troll" before
                // "Troll_Summoned". With nothing typed every score is equal and this is name order.
                if (words.Length > 0)
                {
                    var byLength = a.Value.Name.Length.CompareTo(b.Value.Name.Length);
                    if (byLength != 0) return byLength;
                }

                return string.Compare(a.Value.Name, b.Value.Name, StringComparison.OrdinalIgnoreCase);
            });

            var result = new List<Entry>(found.Count);
            foreach (var pair in found) result.Add(pair.Value);
            return result;
        }

        /// <summary>The filters other than the text: kind, favourites and origin.</summary>
        public static bool Passes(Entry entry, Query query, ICollection<string> favourites)
        {
            if (query.Kind.HasValue && entry.Kind != query.Kind.Value) return false;
            if (query.FavouritesOnly && (favourites == null || !favourites.Contains(entry.Key))) return false;
            if (query.Origin == OriginFilter.Vanilla && entry.Origin == Origin.Mod) return false;
            if (query.Origin == OriginFilter.Mods && entry.Origin != Origin.Mod) return false;
            return true;
        }

        public static string[] Words(string text)
        {
            return string.IsNullOrEmpty(text)
                ? new string[0]
                : text.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// The sum of each word's best match over both names, or <see cref="Miss"/> when any word
        /// matches neither. Lower is better.
        /// </summary>
        private static int Score(Entry entry, string[] words)
        {
            var total = 0;
            foreach (var word in words)
            {
                var best = Math.Min(Rank(entry.Name, word), Rank(entry.DisplayName, word));
                if (best == Miss) return Miss;
                total += best;
            }
            return total;
        }

        private static int Rank(string name, string word)
        {
            if (string.IsNullOrEmpty(name)) return Miss;

            var at = name.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return Miss;
            if (at == 0) return name.Length == word.Length ? Exact : Prefix;

            // A word starting after a separator or at a capital letter ("vfx_troll", "MountainTroll")
            // reads as a word of its own, so it beats a match in the middle of one.
            var before = name[at - 1];
            if (before == '_' || before == ' ' || before == '-') return WordStart;
            if (char.IsUpper(name[at]) && char.IsLower(before)) return WordStart;
            return Inside;
        }
    }
}
