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

    /// <summary>
    /// The typed search, taken apart. Plain words are matched against both names and rank the
    /// results. A word with a known key and a colon narrows the list by something else:
    /// <c>kind:</c>, <c>has:</c> (a component), <c>biome:</c>, <c>mod:</c> and <c>used:</c> (the prefabs
    /// that play an effect). A minus in front of a word or a term leaves out what matches it.
    /// </summary>
    public sealed class ParsedSearch
    {
        public readonly List<string> Words = new List<string>();
        public readonly List<string> NotWords = new List<string>();
        public readonly List<KeyValuePair<string, string>> Terms = new List<KeyValuePair<string, string>>();
        public readonly List<KeyValuePair<string, string>> NotTerms = new List<KeyValuePair<string, string>>();

        /// <summary>A term with nothing after its colon, which nothing can match.</summary>
        public bool Unanswerable;

        public bool IsEmpty => Words.Count == 0 && NotWords.Count == 0 && Terms.Count == 0 && NotTerms.Count == 0 && !Unanswerable;
    }

    public static class Search
    {
        /// <summary>The keys a term can have, as typed before the colon.</summary>
        public static readonly string[] Keys = { "kind", "has", "biome", "mod", "used" };

        private static readonly char[] Separators = { ' ', '\t' };

        // How well one word matches one name, best first.
        private const int Exact = 0;
        private const int Prefix = 1;
        private const int WordStart = 2;
        private const int Inside = 3;
        private const int Miss = int.MaxValue;

        public static ParsedSearch Parse(string text)
        {
            var parsed = new ParsedSearch();
            foreach (var raw in Words(text))
            {
                var word = raw;
                var not = word.Length > 1 && word[0] == '-';
                if (not) word = word.Substring(1);
                if (word == "-") continue;

                var colon = word.IndexOf(':');
                var key = colon > 0 ? word.Substring(0, colon).ToLowerInvariant() : null;
                if (key != null && Array.IndexOf(Keys, key) >= 0)
                {
                    var value = word.Substring(colon + 1);
                    if (value.Length == 0)
                    {
                        if (!not) parsed.Unanswerable = true;
                        continue;
                    }
                    (not ? parsed.NotTerms : parsed.Terms).Add(new KeyValuePair<string, string>(key, value));
                    continue;
                }

                (not ? parsed.NotWords : parsed.Words).Add(word);
            }
            return parsed;
        }

        /// <summary>Whether an entry matches the typed text, words and terms alike.</summary>
        public static bool Matches(Entry entry, string text)
        {
            return Matches(entry, Parse(text));
        }

        public static bool Matches(Entry entry, ParsedSearch search)
        {
            return Score(entry, search) != Miss;
        }

        /// <summary>The entries that match, best match first.</summary>
        public static List<Entry> Run(IReadOnlyList<Entry> all, Query query, ICollection<string> favourites)
        {
            var search = Parse(query.Text);
            var found = new List<KeyValuePair<int, Entry>>();

            foreach (var entry in all)
            {
                if (!Passes(entry, query, favourites)) continue;

                var score = Score(entry, search);
                if (score != Miss) found.Add(new KeyValuePair<int, Entry>(score, entry));
            }

            var ranked = search.Words.Count > 0;
            found.Sort((a, b) =>
            {
                var byScore = a.Key.CompareTo(b.Key);
                if (byScore != 0) return byScore;

                // Among equal matches the shorter name is the closer one: "Troll" before
                // "Troll_Summoned". With no words typed every score is equal and this is name order.
                if (ranked)
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
        /// matches neither, a term fails, or something left out matches. Lower is better.
        /// </summary>
        private static int Score(Entry entry, ParsedSearch search)
        {
            if (search.Unanswerable) return Miss;

            foreach (var term in search.Terms) if (!TermMatches(entry, term.Key, term.Value)) return Miss;
            foreach (var term in search.NotTerms) if (TermMatches(entry, term.Key, term.Value)) return Miss;
            foreach (var word in search.NotWords)
            {
                if (Rank(entry.Name, word) != Miss || Rank(entry.DisplayName, word) != Miss) return Miss;
            }

            var total = 0;
            foreach (var word in search.Words)
            {
                var best = Math.Min(Rank(entry.Name, word), Rank(entry.DisplayName, word));
                if (best == Miss) return Miss;
                total += best;
            }
            return total;
        }

        private static bool TermMatches(Entry entry, string key, string value)
        {
            switch (key)
            {
                case "kind": return KindMatches(entry.Kind, value);
                case "has": return AnyContains(entry.Components, value);
                case "biome": return AnyContains(entry.Biomes, value);
                case "mod": return Contains(entry.ModName, value);
                case "used": return AnyContains(entry.UsedBy, value);
                default: return false;
            }
        }

        /// <summary>A kind by its name or the start of it, singular or plural; "se" is a status effect.</summary>
        public static bool KindMatches(Kind kind, string value)
        {
            var typed = value.Replace(" ", "").ToLowerInvariant();
            if (typed == "se") return kind == Kind.StatusEffect;

            var label = Kinds.Label(kind).Replace(" ", "").ToLowerInvariant();
            var name = kind.ToString().ToLowerInvariant();
            return label.StartsWith(typed, StringComparison.Ordinal) || name.StartsWith(typed, StringComparison.Ordinal);
        }

        private static bool AnyContains(IEnumerable<string> values, string value)
        {
            if (values == null) return false;
            foreach (var candidate in values) if (Contains(candidate, value)) return true;
            return false;
        }

        private static bool Contains(string text, string value)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
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
