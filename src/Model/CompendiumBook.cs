using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>How much a character knows of an entry, each level above the one before.</summary>
    internal enum KnownLevel
    {
        /// <summary>Never met nor named anywhere the character could read.</summary>
        Unknown = 0,

        /// <summary>Named on a page the character can read, or by someone else's map: its name, not its page.</summary>
        HeardOf = 1,

        /// <summary>Met in the world: in sight, looked at, offered by a trader.</summary>
        Seen = 2,

        /// <summary>Dealt with: fought, held, built, used, entered.</summary>
        Known = 3,
    }

    /// <summary>
    /// What one character has come to know, entry by entry (by <see cref="Entry.Key"/>), each
    /// only ever rising; the latest learned, newest first; the worlds whose explored map was
    /// caught up with; and, for each server world, whether the player let the server be asked
    /// where its places are. Kept from session to session as text (<see cref="Save"/>,
    /// <see cref="Load"/>).
    /// </summary>
    internal sealed class CompendiumBook
    {
        /// <summary>How many of the latest learned are kept.</summary>
        public const int LatelyKept = 50;

        private readonly Dictionary<string, KnownLevel> _levels = new Dictionary<string, KnownLevel>(StringComparer.Ordinal);
        private readonly List<string> _lately = new List<string>();
        private readonly HashSet<string> _mapped = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Counts up with every entry learned more of, so what tells the compendium tells it again.</summary>
        public int Version { get; private set; }

        /// <summary>Whether anything changed since it was last saved.</summary>
        public bool Changed { get; private set; }

        /// <summary>How much is known of an entry; unknown for one never met or none.</summary>
        public KnownLevel Level(string key) => !string.IsNullOrEmpty(key) && _levels.TryGetValue(key, out var level) ? level : KnownLevel.Unknown;

        /// <summary>
        /// Learns of an entry up to a level; true when that is more than was known. Quietly, it is
        /// not counted among the latest learned: it was known before, only not yet written down.
        /// </summary>
        public bool Raise(string key, KnownLevel level, bool quietly = false)
        {
            if (string.IsNullOrEmpty(key) || level <= Level(key)) return false;
            _levels[key] = level;
            if (!quietly)
            {
                _learnedAt[key] = ++Learned;
                _lately.Remove(key);
                _lately.Insert(0, key);
                if (_lately.Count > LatelyKept) _lately.RemoveRange(LatelyKept, _lately.Count - LatelyKept);
            }
            Version++;
            Changed = true;
            return true;
        }

        /// <summary>The keys learned more of lately, newest first.</summary>
        public IEnumerable<string> Lately => _lately;

        private readonly Dictionary<string, int> _learnedAt = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Counts what was learned since the book was made or read, not quietly: a count taken now marks what is new after it. Not kept.</summary>
        public int Learned { get; private set; }

        /// <summary>The keys learned more of after a count of <see cref="Learned"/> taken before, newest first, as far as <see cref="Lately"/> keeps them.</summary>
        public IEnumerable<string> LearnedSince(int mark) => _lately.Where(key => _learnedAt.TryGetValue(key, out var at) && at > mark);

        /// <summary>Every entry known of at all, with its level.</summary>
        public IEnumerable<KeyValuePair<string, KnownLevel>> All => _levels;

        /// <summary>How many entries are known at least to a level.</summary>
        public int Count(KnownLevel atLeast) => _levels.Values.Count(l => l >= atLeast);

        /// <summary>Whether a world's explored map was caught up with.</summary>
        public bool Mapped(string world) => world != null && _mapped.Contains(world);

        public void MarkMapped(string world)
        {
            if (string.IsNullOrEmpty(world) || !_mapped.Add(world)) return;
            Changed = true;
        }

        /// <summary>Forgets that a world's map was caught up with, so the catch-up runs again.</summary>
        public void Unmap(string world)
        {
            if (world != null && _mapped.Remove(world)) Changed = true;
        }

        /// <summary>Everything known, a line an entry, then the latest learned in order and the worlds mapped.</summary>
        public string Save()
        {
            var text = new StringBuilder();
            foreach (var pair in _levels.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                text.Append("know\t").Append(Stored.Escaped(pair.Key)).Append('\t').Append(Stored.Count((int)pair.Value)).Append('\n');
            }
            foreach (var key in _lately) text.Append("lately\t").Append(Stored.Escaped(key)).Append('\n');
            foreach (var world in _mapped.OrderBy(w => w, StringComparer.Ordinal)) text.Append("mapped\t").Append(Stored.Escaped(world)).Append('\n');
            Changed = false;
            return text.ToString();
        }

        /// <summary>The file a character's book is kept in: by the character's own file and where that lives (on this PC or in the cloud), so two characters never share one.</summary>
        public static string FileName(string characterFile, string source) => $"{characterFile}-{source}.dat";

        /// <summary>What was saved; a line that cannot be read is passed over, as are those of a question no longer asked ("asked").</summary>
        public static CompendiumBook Load(string text)
        {
            var book = new CompendiumBook();
            if (string.IsNullOrEmpty(text)) return book;
            foreach (var line in text.Split('\n'))
            {
                var parts = line.TrimEnd('\r').Split('\t');
                if (parts.Length < 2 || !Stored.TryUnescaped(parts[1], out var name) || name.Length == 0) continue;
                switch (parts[0])
                {
                    case "know" when parts.Length == 3 && Stored.TryCount(parts[2], out var level) && level > (int)KnownLevel.Unknown && level <= (int)KnownLevel.Known:
                        book._levels[name] = (KnownLevel)level;
                        break;
                    case "lately" when parts.Length == 2 && !book._lately.Contains(name) && book._lately.Count < LatelyKept:
                        book._lately.Add(name);
                        break;
                    case "mapped" when parts.Length == 2:
                        book._mapped.Add(name);
                        break;
                }
            }
            return book;
        }
    }
}
