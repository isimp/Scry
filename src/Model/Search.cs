using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>Which prefabs to show, by origin.</summary>
    internal enum OriginFilter
    {
        All,
        Vanilla,
        Mods,
    }

    /// <summary>Everything that narrows the list: the typed text, a kind, favourites, origin.</summary>
    internal sealed class Query
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
    /// <c>kind:</c>, <c>has:</c> (a component), <c>biome:</c>, <c>mod:</c>, <c>playedby:</c> (the prefabs
    /// that play an effect; <c>used:</c> is its older spelling), <c>station:</c> (where it is made) and <c>in:</c> (a location or dungeon
    /// it is found in, once they are read) and <c>is:</c> (what it is, <see cref="SearchFlags"/>, by the
    /// start of the word). A comma in a term's value reads as or: <c>biome:swamp,plains</c>.
    /// A minus in front of a word or a term leaves out what matches it.
    /// </summary>
    internal sealed class ParsedSearch
    {
        public readonly List<string> Words = new List<string>();
        public readonly List<string> NotWords = new List<string>();
        public readonly List<KeyValuePair<string, string>> Terms = new List<KeyValuePair<string, string>>();
        public readonly List<KeyValuePair<string, string>> NotTerms = new List<KeyValuePair<string, string>>();

        public bool IsEmpty => Words.Count == 0 && NotWords.Count == 0 && Terms.Count == 0 && NotTerms.Count == 0;

        private Matcher _matcher;

        /// <summary>The search made ready to be matched against many entries, worked out when first used.</summary>
        internal Matcher Matcher => _matcher ?? (_matcher = new Matcher(this));
    }

    /// <summary>
    /// A parsed search as it is matched: the words in capitals and each term's value worked out,
    /// once for the whole list rather than again for every entry.
    /// </summary>
    internal sealed class Matcher
    {
        public readonly List<string> Words = new List<string>();
        public readonly List<string> NotWords = new List<string>();
        public readonly List<Term> Terms = new List<Term>();
        public readonly List<Term> NotTerms = new List<Term>();

        public Matcher(ParsedSearch search)
        {
            foreach (var word in search.Words) Words.Add(word.ToUpperInvariant());
            foreach (var word in search.NotWords) NotWords.Add(word.ToUpperInvariant());
            foreach (var term in search.Terms) Terms.Add(new Term(term.Key, term.Value));
            foreach (var term in search.NotTerms) NotTerms.Add(new Term(term.Key, term.Value));
        }
    }

    /// <summary>
    /// One term, with what its value stands for: the values its commas part it into, any of
    /// which may match; the kinds they name; or each one's station name and level.
    /// </summary>
    internal sealed class Term
    {
        public readonly string Key;
        public readonly string[] Values;
        public readonly bool[] Kinds;
        public readonly string[] Stations;
        public readonly int[] Levels;

        public Term(string key, string value)
        {
            Key = key;
            Values = Search.Values(value);
            if (key == "kind")
            {
                var kinds = (Kind[])Enum.GetValues(typeof(Kind));
                Kinds = new bool[kinds.Length];
                foreach (var kind in kinds)
                {
                    foreach (var one in Values) Kinds[(int)kind] |= Search.KindMatches(kind, one);
                }
            }
            else if (key == "station")
            {
                Stations = new string[Values.Length];
                Levels = new int[Values.Length];
                for (var i = 0; i < Values.Length; i++) Search.StationParts(Values[i], out Stations[i], out Levels[i]);
            }
        }
    }

    internal static class Search
    {
        /// <summary>The keys a term can have, as typed before the colon.</summary>
        public static readonly string[] Keys = { "kind", "has", "biome", "mod", "playedby", "station", "in", "is" };

        /// <summary>The keys whose values are words the catalog reads into each entry (<see cref="Entry.TermWords"/>), each matched by its start.</summary>
        public static readonly string[] WordKeys = { "is" };

        /// <summary>Older spellings of a key, still read: "used:" was taken for what an item is used for.</summary>
        private static readonly Dictionary<string, string> OldKeys = new Dictionary<string, string> { ["used"] = "playedby" };

        private static readonly char[] Separators = { ' ', '\t' };

        private static readonly char[] Commas = { ',' };

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
                if (key != null && OldKeys.TryGetValue(key, out var current)) key = current;
                if (key != null && Array.IndexOf(Keys, key) >= 0)
                {
                    // A term still being typed, its value not yet there, is left out until it has one.
                    var value = word.Substring(colon + 1);
                    if (Values(value).Length == 0) continue;
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
            return Score(entry, search.Matcher) != Miss;
        }

        /// <summary>The entries that match, best match first.</summary>
        public static List<Entry> Run(IReadOnlyList<Entry> all, Query query, ICollection<string> favourites)
        {
            return Run(all, query, favourites, null, null);
        }

        /// <summary>
        /// The entries that match, best match first, going through the list once. Only the keys in
        /// <paramref name="only"/> can match when it is given. <paramref name="kindCounts"/>, when
        /// given, is added to: for each kind, how many would match with any kind let through, so
        /// the kind chips count across every kind while one is picked.
        /// </summary>
        public static List<Entry> Run(IReadOnlyList<Entry> all, Query query, ICollection<string> favourites,
            ICollection<string> only, int[] kindCounts)
        {
            var search = Parse(query.Text).Matcher;
            var found = new List<Entry>();
            var scores = new List<int>();
            var unordered = false;

            foreach (var entry in all)
            {
                if (entry.NameOrder < 0) unordered = true;
                if (!PassesAnyKind(entry, query, favourites)) continue;
                if (only != null && !only.Contains(entry.Key)) continue;

                var score = Score(entry, search);
                if (score == Miss) continue;

                if (kindCounts != null) kindCounts[(int)entry.Kind]++;
                if (query.Kind.HasValue && entry.Kind != query.Kind.Value) continue;
                found.Add(entry);
                scores.Add(score);
            }

            // Names are put in order once, and again only after one changes (a leftover renamed
            // after its owner), so sorting compares numbers rather than names: a search matching
            // most of the catalog sorts thousands of entries on every keystroke.
            if (unordered) OrderNames(all);

            // Best score first; among equal matches the shorter name is the closer one ("Troll"
            // before "Troll_Summoned"); then name order. With no words typed every score is equal
            // and this is name order. The three are one number for each entry, so the sort
            // compares plain numbers, with no call made for each comparison.
            var ranked = search.Words.Count > 0;
            var keys = new long[found.Count];
            var entries = found.ToArray();
            for (var i = 0; i < entries.Length; i++) keys[i] = SortKey(scores[i], ranked ? entries[i].Name.Length : 0, entries[i].NameOrder);
            Array.Sort(keys, entries);
            return new List<Entry>(entries);
        }

        /// <summary>
        /// A result's place as one number: its score, then its name's length, then its name's
        /// order, each in bits of its own, the first counting most. Each is held to its bits, so an
        /// odd score or a name longer than any the game has only ties, never wraps around.
        /// </summary>
        public static long SortKey(int score, int length, int nameOrder)
        {
            long Bits(int value, int bits) => Math.Max(0, Math.Min(value, (1 << bits) - 1));
            return (Bits(score, 20) << 43) | (Bits(length, 16) << 27) | Bits(nameOrder, 27);
        }

        /// <summary>
        /// Numbers every entry by the name the list shows first (the game's, else the prefab's),
        /// then by its prefab name, ignoring case, with ties broken by the exact name and then by
        /// place in the catalog, so the order never depends on how a sort happens to run.
        /// </summary>
        public static void OrderNames(IReadOnlyList<Entry> all)
        {
            var order = new List<KeyValuePair<Entry, int>>(all.Count);
            for (var i = 0; i < all.Count; i++) order.Add(new KeyValuePair<Entry, int>(all[i], i));
            // By the name the list shows first (the game's, else the prefab's), then the prefab's.
            order.Sort((a, b) =>
            {
                var byShown = string.Compare(a.Key.ShownName, b.Key.ShownName, StringComparison.OrdinalIgnoreCase);
                if (byShown != 0) return byShown;
                var byName = string.Compare(a.Key.Name, b.Key.Name, StringComparison.OrdinalIgnoreCase);
                if (byName != 0) return byName;
                var exact = string.CompareOrdinal(a.Key.Name, b.Key.Name);
                return exact != 0 ? exact : a.Value.CompareTo(b.Value);
            });
            for (var i = 0; i < order.Count; i++) order[i].Key.NameOrder = i;
        }

        /// <summary>The filters other than the text and the kind: favourites and origin.</summary>
        private static bool PassesAnyKind(Entry entry, Query query, ICollection<string> favourites)
        {
            if (query.FavouritesOnly && (favourites == null || !favourites.Contains(entry.Key))) return false;
            if (query.Origin == OriginFilter.Vanilla && entry.Origin == Origin.Mod) return false;
            if (query.Origin == OriginFilter.Mods && entry.Origin != Origin.Mod) return false;
            return true;
        }

        /// <summary>A term's values, parted by its commas, each read as one way to match; what stands empty between them is passed over.</summary>
        public static string[] Values(string value) => value.Split(Commas, StringSplitOptions.RemoveEmptyEntries);

        public static string[] Words(string text)
        {
            return string.IsNullOrEmpty(text)
                ? Array.Empty<string>()
                : text.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// The sum of each word's best match over both names, or <see cref="Miss"/> when any word
        /// matches neither, a term fails, or something left out matches. Lower is better.
        /// </summary>
        private static int Score(Entry entry, Matcher search)
        {
            foreach (var term in search.Terms) if (!TermMatches(entry, term)) return Miss;
            foreach (var term in search.NotTerms) if (TermMatches(entry, term)) return Miss;
            foreach (var word in search.NotWords)
            {
                if (Rank(entry.Name, entry.NameUpper, word) != Miss || Rank(entry.DisplayName, entry.DisplayNameUpper, word) != Miss) return Miss;
            }

            var total = 0;
            foreach (var word in search.Words)
            {
                var best = Math.Min(Rank(entry.Name, entry.NameUpper, word), Rank(entry.DisplayName, entry.DisplayNameUpper, word));
                if (best == Miss) return Miss;
                total += best;
            }
            return total;
        }

        /// <summary>Whether any of a term's values matches the entry.</summary>
        private static bool TermMatches(Entry entry, Term term)
        {
            if (term.Key == "kind")
            {
                var kind = (int)entry.Kind;
                return kind >= 0 && kind < term.Kinds.Length && term.Kinds[kind];
            }
            for (var i = 0; i < term.Values.Length; i++)
            {
                if (term.Key == "station" ? StationMatches(entry.Stations, term.Stations[i], term.Levels[i]) : ValueMatches(entry, term.Key, term.Values[i])) return true;
            }
            return false;
        }

        private static bool ValueMatches(Entry entry, string key, string value)
        {
            switch (key)
            {
                case "has": return AnyContains(entry.Components, value);
                case "biome": return AnyContains(entry.Biomes, value);
                case "in": return AnyPlaceNamed(entry.FoundIn, value);
                case "mod": return ContainsLeavingOutSpaces(entry.ModName, value);
                case "playedby": return AnyContainsLeavingOutSpaces(entry.UsedBy, value);
                case "is": return AnyStartsWith(entry.TermWords(key), value);
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

        /// <summary>
        /// Made at a station by that name, either the game's or the prefab's. Digits at the end
        /// are a station level: "forge3" is what a forge at level 3 can make, needing 3 or less.
        /// </summary>
        public static bool StationMatches(StationUse[] stations, string value)
        {
            StationParts(value, out var name, out var level);
            return StationMatches(stations, name, level);
        }

        private static bool StationMatches(StationUse[] stations, string name, int level)
        {
            if (stations == null || stations.Length == 0) return false;

            foreach (var use in stations)
            {
                var named = name.Length == 0 || Contains(use.Name, name) || ContainsLeavingOutSpaces(use.Shown, name);
                if (named && (level == 0 || use.Level <= level)) return true;
            }
            return false;
        }

        /// <summary>
        /// A station term taken apart: its name, and the level its digits at the end give, or 0
        /// for none. A level too large to count stands for any level.
        /// </summary>
        internal static void StationParts(string value, out string name, out int level)
        {
            var digits = value.Length;
            while (digits > 0 && value[digits - 1] >= '0' && value[digits - 1] <= '9') digits--;
            name = value.Substring(0, digits);

            level = 0;
            for (var i = digits; i < value.Length; i++)
            {
                var digit = value[i] - '0';
                level = level > (int.MaxValue - digit) / 10 ? int.MaxValue : level * 10 + digit;
            }
        }

        private static bool AnyStartsWith(string[] words, string value)
        {
            foreach (var word in words) if (word.StartsWith(value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool AnyContains(string[] values, string value)
        {
            if (values == null) return false;
            foreach (var candidate in values) if (Contains(candidate, value)) return true;
            return false;
        }

        /// <summary>A place whose name, not its biome, holds the word, as the suggestions for places offer them.</summary>
        private static bool AnyPlaceNamed(string[] places, string value)
        {
            if (places == null) return false;
            foreach (var place in places) if (ContainsLeavingOutSpaces(Places.NameOf(place), value)) return true;
            return false;
        }

        private static bool AnyContainsLeavingOutSpaces(string[] values, string value)
        {
            if (values == null) return false;
            foreach (var candidate in values) if (ContainsLeavingOutSpaces(candidate, value)) return true;
            return false;
        }

        private static bool AnyContainsLeavingOutSpaces(List<string> values, string value)
        {
            if (values == null) return false;
            foreach (var candidate in values) if (ContainsLeavingOutSpaces(candidate, value)) return true;
            return false;
        }

        private static bool AnyContains(List<string> values, string value)
        {
            if (values == null) return false;
            foreach (var candidate in values) if (Contains(candidate, value)) return true;
            return false;
        }

        private static bool Contains(string text, string value)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Whether the text holds the value, whatever the case, once the text's spaces are left
        /// out: "Artisan table" holds "antab". Compared in place, without making the shorter text.
        /// </summary>
        private static bool ContainsLeavingOutSpaces(string text, string value)
        {
            if (string.IsNullOrEmpty(text)) return false;

            for (var start = 0; start < text.Length; start++)
            {
                if (text[start] == ' ') continue;

                var at = start;
                var matched = 0;
                while (matched < value.Length && at < text.Length)
                {
                    if (text[at] == ' ')
                    {
                        at++;
                        continue;
                    }
                    if (char.ToUpperInvariant(text[at]) != char.ToUpperInvariant(value[matched])) break;
                    at++;
                    matched++;
                }
                if (matched == value.Length) return true;
            }
            return false;
        }

        /// <summary>
        /// How well a word matches a name, given the name in capitals and the word already in
        /// capitals, so no case has to be ignored while comparing.
        /// </summary>
        private static int Rank(string name, string upperName, string upperWord)
        {
            if (string.IsNullOrEmpty(name)) return Miss;

            var at = upperName.IndexOf(upperWord, StringComparison.Ordinal);
            if (at < 0) return Miss;
            if (at == 0) return name.Length == upperWord.Length ? Exact : Prefix;

            // A word starting after a separator or at a capital letter ("vfx_troll", "MountainTroll")
            // reads as a word of its own, so it beats a match in the middle of one. Capitals change
            // no letter's place, so the name as written tells where its words start.
            var before = name[at - 1];
            if (before == '_' || before == ' ' || before == '-') return WordStart;
            if (char.IsUpper(name[at]) && char.IsLower(before)) return WordStart;
            return Inside;
        }
    }
}
