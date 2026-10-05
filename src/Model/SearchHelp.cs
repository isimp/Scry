using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>A way to finish the word being typed: a key ("biome:") or a key with a value ("biome:swamp"), with how many it finds.</summary>
    internal struct Suggestion
    {
        /// <summary>What it is called in the list: the key, or the value as the game names it ("Black forest").</summary>
        public string Label;

        /// <summary>The word it puts in the search in place of the one being typed.</summary>
        public string Insert;

        /// <summary>For a key, what it looks for.</summary>
        public string Note;

        /// <summary>How many entries it finds (for a chip, how many have the value).</summary>
        public int Count;

        public bool IsKey;
    }

    /// <summary>A word of the search: where it starts and ends, and the word itself.</summary>
    internal struct WordSpan
    {
        public int Start;
        public int End;
        public string Word;
    }

    /// <summary>
    /// Every value each search key can take in the catalog, read in one pass: the kinds, the
    /// component types, biomes, mods, the players of effects, stations and places. Each value is
    /// kept as the one word the search takes ("blackforest", "byhand") and as its name, with how
    /// many entries have it.
    /// </summary>
    internal sealed class TermIndex
    {
        internal sealed class Value
        {
            public string Token;
            public string Label;
            public int Count;

            /// <summary>The entries that have it, by place in the catalog, each once.</summary>
            public readonly List<int> Entries = new List<int>();
        }

        /// <summary>How many entries each suggested term finds, worked out once a world.</summary>
        private readonly Dictionary<string, int> _finds = new Dictionary<string, int>(StringComparer.Ordinal);

        // Marks an entry already counted, by the round it was counted in, so no set is made per count.
        private int[] _counted;
        private int _round;

        private readonly Dictionary<string, Dictionary<string, Value>> _byKey = new Dictionary<string, Dictionary<string, Value>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Value>> _lists = new Dictionary<string, List<Value>>(StringComparer.Ordinal);

        internal readonly IReadOnlyList<Entry> Catalog;

        public TermIndex(IEnumerable<Entry> catalog)
        {
            Catalog = catalog as IReadOnlyList<Entry> ?? catalog.ToList();
            foreach (var key in Search.Keys) _byKey[key] = new Dictionary<string, Value>(StringComparer.Ordinal);

            // The kinds are a fixed set, offered whether any are listed or not.
            foreach (Kind kind in Enum.GetValues(typeof(Kind))) Intern("kind", kind.ToString(), Kinds.Label(kind));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var at = 0; at < Catalog.Count; at++)
            {
                var entry = Catalog[at];
                seen.Clear();
                void Add(string key, string word, string label)
                {
                    if (string.IsNullOrEmpty(word)) return;
                    var value = Intern(key, word, label);
                    if (!seen.Add(key + "|" + value.Token)) return;
                    value.Count++;
                    value.Entries.Add(at);
                }

                Add("kind", entry.Kind.ToString(), Kinds.Label(entry.Kind));
                foreach (var part in entry.Components ?? Array.Empty<string>()) Add("has", part, part);
                foreach (var biome in entry.Biomes ?? Array.Empty<string>()) Add("biome", biome, Naming.FieldLabel(biome));
                Add("mod", entry.ModName, entry.ModName);
                foreach (var user in entry.UsedBy) Add("playedby", user, user);
                foreach (var station in entry.Stations ?? Array.Empty<StationUse>()) Add("station", station.Shown.Length > 0 ? station.Shown : station.Name, station.Shown.Length > 0 ? station.Shown : station.Name);
                foreach (var place in entry.FoundIn ?? Array.Empty<string>()) Add("in", Places.NameOf(place), Places.NameOf(place));
                foreach (var key in Search.WordKeys)
                {
                    foreach (var word in entry.TermWords(key)) Add(key, word, Naming.Capital(word));
                }
                foreach (var key in Search.LinkKeys)
                {
                    foreach (var other in entry.TermLinks(key)) Add(key, other.ShownName, other.ShownName);
                }
            }
        }

        private Value Intern(string key, string word, string label)
        {
            var token = Token(word);
            var values = _byKey[key];
            if (!values.TryGetValue(token, out var value))
            {
                value = new Value { Token = token, Label = label };
                values[token] = value;
                _lists.Remove(key);
            }
            return value;
        }

        /// <summary>A value as one word of the search: without its spaces, in small letters.</summary>
        internal static string Token(string word) => (word ?? "").Replace(" ", "").ToLowerInvariant();

        /// <summary>
        /// How many entries a term of the index's values finds, as the search finds them: every
        /// entry with a value of that key holding the term's word, or any of its words where
        /// commas part it, each once. Read from the
        /// index rather than the catalog, as suggestions count up to eight terms a keystroke. The
        /// kinds and stations, whose words the search reads in ways of their own, are counted by
        /// searching; every count is kept once found.
        /// </summary>
        internal int Finds(string key, string token)
        {
            var term = SearchHelp.Term(key, token);
            if (_finds.TryGetValue(term, out var known)) return known;

            int count;
            if (key == "kind" || key == "station")
            {
                var parsed = Search.Parse(term);
                count = 0;
                foreach (var entry in Catalog) if (Search.Matches(entry, parsed)) count++;
            }
            else
            {
                if (_counted == null) _counted = new int[Catalog.Count];
                if (++_round == int.MaxValue)
                {
                    System.Array.Clear(_counted, 0, _counted.Length);
                    _round = 1;
                }
                count = 0;
                var words = Search.Values(token);
                foreach (var value in ValuesOf(key))
                {
                    if (!Array.Exists(words, w => value.Token.IndexOf(w, StringComparison.Ordinal) >= 0)) continue;
                    foreach (var at in value.Entries)
                    {
                        if (_counted[at] == _round) continue;
                        _counted[at] = _round;
                        count++;
                    }
                }
            }
            _finds[term] = count;
            return count;
        }

        internal List<Value> ValuesOf(string key)
        {
            if (_lists.TryGetValue(key, out var list)) return list;
            list = _byKey.TryGetValue(key, out var values) ? values.Values.ToList() : new List<Value>();
            _lists[key] = list;
            return list;
        }
    }

    /// <summary>
    /// Tab through what a search can be finished with: the first Tab puts in the first suggestion
    /// for the word being typed, each Tab after it the next (Shift+Tab the one before), as long as
    /// nothing else was typed. A word with only one way to finish it is finished at once, so the
    /// next Tab goes on from there: "bi", Tab, "biome:", Tab, "biome:swamp".
    /// </summary>
    internal sealed class TabCycle
    {
        /// <summary>Which suggestion is in the search now, while cycling.</summary>
        public int Index { get; private set; }

        /// <summary>What is being cycled through, while cycling, and where the word it replaces starts.</summary>
        public IReadOnlyList<Suggestion> Suggestions => _list;

        public int Start => _start;

        private List<Suggestion> _list;
        private string _text;
        private int _caret;
        private int _start;

        public void Reset() => _list = null;

        /// <summary>Whether the search is still as the last Tab left it, so the next Tab goes on cycling.</summary>
        public bool IsAt(string text, int caret) => _list != null && text == _text && caret == _caret;

        public (string Text, int Caret) Next(string text, int caret, Func<string, List<Suggestion>> suggest, bool back)
        {
            text = text ?? "";
            int end;
            if (_list == null || text != _text || caret != _caret)
            {
                var span = SearchHelp.WordAt(text, caret);
                var list = suggest(text.Substring(span.Start, Math.Max(0, Math.Min(caret, text.Length) - span.Start)));
                if (list == null || list.Count == 0)
                {
                    _list = null;
                    return (text, caret);
                }
                _list = list;
                _start = span.Start;
                end = span.End;
                Index = back ? list.Count - 1 : 0;
            }
            else
            {
                end = _caret;
                Index = (Index + (back ? -1 : 1) + _list.Count) % _list.Count;
            }

            var insert = _list[Index].Insert;
            var result = text.Substring(0, _start) + insert + text.Substring(end);
            var at = _start + insert.Length;
            if (_list.Count == 1) _list = null;
            else
            {
                _text = result;
                _caret = at;
            }
            return (result, at);
        }
    }

    /// <summary>
    /// Help with typing a search: the word being typed, what could finish it and how many each
    /// would find in the whole catalog, and the rest of the best one shown after it.
    /// </summary>
    internal static class SearchHelp
    {
        /// <summary>A term the search reads: its key, and a value as one word (<see cref="TermIndex.Token"/>), "mod:coolstatues".</summary>
        public static string Term(string key, string value) => key + ":" + TermIndex.Token(value);

        /// <summary>Whether a search asks by a key, whatever its letters' size; a term left out with a minus ("-in:") asks for the rest, so it does not.</summary>
        public static bool Asks(string text, string key)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var word in text.Split(' '))
            {
                if (word.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>What each key looks for, shown beside it.</summary>
        private static readonly Dictionary<string, string> KeyNotes = new Dictionary<string, string>
        {
            ["kind"] = "what kind it is",
            ["has"] = "a part of that type",
            ["biome"] = "where it spawns or grows",
            ["mod"] = "the mod that added it",
            ["playedby"] = "what plays it",
            ["station"] = "where it is made",
            ["in"] = "a location or dungeon it is found in",
            ["is"] = "what it is: a boss, food, a weapon",
            ["weak"] = "a damage type it takes more of",
            ["resists"] = "a damage type it takes less of",
            ["immune"] = "a damage type it takes none of",
            ["damage"] = "a damage type it deals",
            ["skill"] = "the skill it trains",
            ["drops"] = "something it drops or gives",
            ["from"] = "what drops or gives it",
            ["needs"] = "an item it is made or built with",
            ["gives"] = "a status effect it gives",
            ["spawns"] = "something it spawns or brings",
        };

        /// <summary>The word the caret is in (or at the end of), from space to space.</summary>
        public static WordSpan WordAt(string text, int caret)
        {
            text = text ?? "";
            caret = Math.Max(0, Math.Min(caret, text.Length));
            var start = caret == 0 ? 0 : text.LastIndexOf(' ', caret - 1) + 1;
            var end = text.IndexOf(' ', caret);
            if (end < 0) end = text.Length;
            return new WordSpan { Start = start, End = end, Word = text.Substring(start, end - start) };
        }

        /// <summary>The text with one word replaced, and where the caret goes: right after it.</summary>
        public static string Replace(string text, WordSpan span, string insert, out int caret)
        {
            text = text ?? "";
            caret = span.Start + insert.Length;
            return text.Substring(0, span.Start) + insert + text.Substring(span.End);
        }

        /// <summary>
        /// What could finish the word being typed. Nothing typed yet gets every key, to show what
        /// the search can do, however many there are; a word without a colon that starts a key gets the key; after a known key's colon, the values in the catalog that start with (or else
        /// hold) what is typed after it, those most entries have first, whichever tab is open;
        /// after a comma, the same for the value being typed, keeping those before it and leaving
        /// them out of what is offered. A minus in front stays in front. Each finds as many as
        /// its count says, the values before the comma with it.
        /// </summary>
        public static List<Suggestion> Suggest(string word, TermIndex index, int max = 8)
        {
            var found = new List<Suggestion>();
            if (index == null) return found;
            word = word ?? "";
            var minus = word.Length > 1 && word[0] == '-' ? "-" : "";
            var typed = word.Substring(minus.Length);

            var colon = typed.IndexOf(':');
            if (colon < 0)
            {
                var start = typed.ToLowerInvariant();
                foreach (var key in Search.Keys)
                {
                    if (!key.StartsWith(start, StringComparison.Ordinal)) continue;
                    found.Add(new Suggestion { Label = key + ":", Insert = minus + key + ":", Note = KeyNotes[key], IsKey = true });
                }
                return found;
            }

            var name = typed.Substring(0, colon).ToLowerInvariant();
            if (Array.IndexOf(Search.Keys, name) < 0) return found;
            var after = typed.Substring(colon + 1);
            var comma = after.LastIndexOf(',');
            var before = comma >= 0 ? TermIndex.Token(after.Substring(0, comma + 1)) : "";
            var taken = Search.Values(before);
            var partial = TermIndex.Token(after.Substring(comma + 1));

            var ranked = new List<(int Rank, TermIndex.Value Value)>();
            foreach (var value in index.ValuesOf(name))
            {
                if (Array.IndexOf(taken, value.Token) >= 0) continue;
                var rank = Rank(value, partial);
                if (rank >= 0) ranked.Add((rank, value));
            }
            ranked.Sort((a, b) =>
            {
                var byRank = a.Rank.CompareTo(b.Rank);
                if (byRank != 0) return byRank;
                var byCount = b.Value.Count.CompareTo(a.Value.Count);
                return byCount != 0 ? byCount : string.Compare(a.Value.Label, b.Value.Label, StringComparison.OrdinalIgnoreCase);
            });

            foreach (var (_, value) in ranked.Take(max))
            {
                var term = Term(name, before + value.Token);
                found.Add(new Suggestion
                {
                    Label = value.Label,
                    Insert = minus + term,
                    Count = index.Finds(name, before + value.Token),
                });
            }
            return found;
        }

        /// <summary>0 when the value starts with what is typed, 1 when a word of its name does, 2 when it only holds it, -1 when it does not.</summary>
        private static int Rank(TermIndex.Value value, string partial)
        {
            if (partial.Length == 0 || value.Token.StartsWith(partial, StringComparison.Ordinal)) return 0;
            foreach (var word in value.Label.Split(' ', '_'))
            {
                if (word.StartsWith(partial, StringComparison.OrdinalIgnoreCase)) return 1;
            }
            return value.Token.IndexOf(partial, StringComparison.Ordinal) >= 0 ? 2 : -1;
        }

        /// <summary>
        /// Whether Enter takes the marked suggestion rather than playing the list's selection: only
        /// while suggestions show for a word being typed, or while Tab cycles through them. The
        /// keys listed for nothing typed yet leave Enter to the list.
        /// </summary>
        public static bool EnterTakesSuggestion(string typed, bool cycling, int shown)
        {
            return shown > 0 && (cycling || !string.IsNullOrEmpty(typed));
        }

        /// <summary>The rest of the best suggestion, when it begins with the word typed; nothing otherwise.</summary>
        public static string Ghost(string word, List<Suggestion> suggestions)
        {
            if (string.IsNullOrEmpty(word) || suggestions == null || suggestions.Count == 0) return "";
            var best = suggestions[0].Insert;
            return best.Length > word.Length && best.StartsWith(word, StringComparison.OrdinalIgnoreCase) ? best.Substring(word.Length) : "";
        }
    }
}
