using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>Which ruleset the compendium follows: none, or a known entry shown whole.</summary>
    internal enum CompendiumRule
    {
        Off,
        Loose,
    }

    /// <summary>
    /// What each entry shows as, from what the character knows (<see cref="CompendiumBook"/>) and
    /// the ruleset. Off, everything is known. Under <see cref="CompendiumRule.Loose"/> an entry is
    /// what the book holds of it, mods are always known, and what an entry seen or known owns is
    /// known with it: the effects and sounds it plays (<see cref="Entry.UsedBy"/>), what it leaves
    /// behind, and the status effects, effects, sounds and projectiles it links to. Whatever else
    /// a known entry's page names, by its links, its link terms, its stations or the places it is
    /// found in, is heard of. Told again only when the book, its version or the rule changes; while
    /// it stays off, only when the rule does.
    /// </summary>
    internal sealed class CompendiumView
    {
        private static readonly string[] TermKeys = { "drops", "from", "needs", "gives", "spawns" };

        private readonly EntryCatalog _catalog;
        private readonly Dictionary<string, List<Entry>> _played = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Entry>> _leftovers = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Entry>> _places = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        private readonly Dictionary<Entry, KnownLevel> _levels = new Dictionary<Entry, KnownLevel>();

        private CompendiumBook _book;
        private int _bookVersion = -1;
        private CompendiumRule _rule;

        public CompendiumView(EntryCatalog catalog)
        {
            _catalog = catalog;
            foreach (var entry in catalog.All)
            {
                foreach (var owner in entry.UsedBy) Add(_played, owner, entry);
                foreach (var owner in entry.LeftBy) Add(_leftovers, owner, entry);
                Add(_places, Places.LabelOf(entry), entry);
            }
        }

        /// <summary>Counts up each time the levels are told anew.</summary>
        public int Version { get; private set; }

        /// <summary>The catalog it tells of.</summary>
        public EntryCatalog Catalog => _catalog;

        /// <summary>The ruleset last told; with it off, everything is known.</summary>
        public CompendiumRule Rule => _rule;

        /// <summary>Whether a kind shows an entry not known as a silhouette in its place; the others leave it out.</summary>
        public static bool Silhouetted(Kind kind) =>
            kind == Kind.Creature || kind == Kind.Item || kind == Kind.Piece || kind == Kind.Resource || kind == Kind.Spawner
            || kind == Kind.StatusEffect || kind == Kind.Location || kind == Kind.Raid || kind == Kind.Biome;

        /// <summary>How much the character knows of an entry under the rule last told.</summary>
        public KnownLevel Of(Entry entry)
        {
            if (entry == null) return KnownLevel.Unknown;
            if (_rule == CompendiumRule.Off) return KnownLevel.Known;
            return _levels.TryGetValue(entry, out var level) ? level : KnownLevel.Unknown;
        }

        /// <summary>Works out every entry's level for this book and rule, unless nothing changed since last time.</summary>
        public void Tell(CompendiumBook book, CompendiumRule rule)
        {
            if (book == null) book = new CompendiumBook();
            if (ReferenceEquals(book, _book) && book.Version == _bookVersion && rule == _rule) return;
            // Off, everything is known whatever the book holds: nothing to tell again.
            var stillOff = rule == CompendiumRule.Off && _rule == CompendiumRule.Off;
            _book = book;
            _bookVersion = book.Version;
            _rule = rule;
            if (stillOff) return;
            Version++;
            _levels.Clear();
            if (rule == CompendiumRule.Off) return;

            var met = new Queue<Entry>();
            foreach (var entry in _catalog.All)
            {
                var level = entry.Kind == Kind.Mod ? KnownLevel.Known : book.Level(entry.Key);
                if (level == KnownLevel.Unknown) continue;
                _levels[entry] = level;
                if (level >= KnownLevel.Seen) met.Enqueue(entry);
            }

            // What a seen or known entry owns is known, and owns in turn: an effect's own sounds.
            while (met.Count > 0)
            {
                foreach (var owned in Owned(met.Dequeue()))
                {
                    if (Of(owned) == KnownLevel.Known) continue;
                    _levels[owned] = KnownLevel.Known;
                    met.Enqueue(owned);
                }
            }

            var heard = new List<Entry>();
            foreach (var pair in _levels)
            {
                if (pair.Value == KnownLevel.Known) heard.AddRange(Named(pair.Key));
            }
            foreach (var entry in heard)
            {
                if (Of(entry) == KnownLevel.Unknown) _levels[entry] = KnownLevel.HeardOf;
            }
        }

        /// <summary>What an entry owns: what it plays, what it leaves behind, and what it links to that is not a thing of its own.</summary>
        private IEnumerable<Entry> Owned(Entry owner)
        {
            if (_played.TryGetValue(owner.Name, out var played)) foreach (var entry in played) yield return entry;
            if (_leftovers.TryGetValue(owner.Name, out var left)) foreach (var entry in left) yield return entry;
            foreach (var name in owner.LeavesBehind)
            {
                var entry = _catalog.Find(name);
                if (entry != null) yield return entry;
            }
            foreach (var link in owner.Links)
            {
                var entry = _catalog.Find(link.Target);
                if (entry != null && Belongs(entry.Kind)) yield return entry;
            }
        }

        /// <summary>What a known entry's page names; what it owns is among them, already known.</summary>
        private IEnumerable<Entry> Named(Entry entry)
        {
            foreach (var link in entry.Links)
            {
                var other = _catalog.Find(link.Target);
                if (other != null) yield return other;
            }
            foreach (var key in TermKeys) foreach (var other in entry.TermLinks(key)) yield return other;
            foreach (var station in entry.Stations)
            {
                var other = _catalog.Find(station.Name);
                if (other != null) yield return other;
            }
            foreach (var label in entry.FoundIn)
            {
                if (label != null && _places.TryGetValue(label, out var places)) foreach (var place in places) yield return place;
            }
        }

        /// <summary>Whether a thing of this kind linked to belongs to what links to it: a status effect it gives, an effect, a sound, a projectile.</summary>
        private static bool Belongs(Kind kind) =>
            kind == Kind.StatusEffect || kind == Kind.Effect || kind == Kind.Sound || kind == Kind.Projectile || kind == Kind.Other;

        private static void Add(Dictionary<string, List<Entry>> index, string name, Entry entry)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!index.TryGetValue(name, out var list)) index[name] = list = new List<Entry>();
            list.Add(entry);
        }
    }
}
