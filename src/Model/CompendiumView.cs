using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>Which ruleset the compendium follows: none, a known entry shown whole, or of how it connects only what the character witnessed.</summary>
    internal enum CompendiumRule
    {
        Off,
        Loose,
        Strict,
    }

    /// <summary>Going through the rulesets in their order, as the panel's mode does.</summary>
    internal static class CompendiumRules
    {
        /// <summary>The ruleset a step after or before (a negative step) another; the first or last stays where it is.</summary>
        public static CompendiumRule Step(CompendiumRule rule, int by)
        {
            var at = Math.Max((int)CompendiumRule.Off, Math.Min((int)CompendiumRule.Strict, (int)rule + Math.Sign(by)));
            return (CompendiumRule)at;
        }
    }

    /// <summary>
    /// What each entry shows as, from what the character knows (<see cref="CompendiumBook"/>) and
    /// the ruleset. Off, everything is known. Under <see cref="CompendiumRule.Loose"/> an entry is
    /// what the book holds of it, mods are always known, and what a known entry owns is
    /// known with it: the effects and sounds it plays (<see cref="Entry.UsedBy"/>), what it leaves
    /// behind, and the status effects, effects, sounds and projectiles it links to, never what
    /// links to it (<see cref="Link.Back"/>: knowing Poison does not make known all that gives
    /// it), and a boss stone does not own its guardian power, which it gives only for the boss's
    /// trophy. A dungeon known owns its rooms. Under <see cref="CompendiumRule.Strict"/> only what is
    /// known brings its parts along, and a known page keeps of how it connects only what is
    /// <see cref="Related"/> to it. With a milestone floor (<see cref="Milestones"/>) everything of
    /// a tier at or below it is known too, with what it owns that is no higher, told apart
    /// (<see cref="ByFloor"/>), and shows whole
    /// under either rules (<see cref="BelowFloor"/>). Whatever else a known entry's page names,
    /// by its links, its link terms, its stations or the places it is found in, is heard of.
    /// Told again only when the book's levels, the rule, the floor or the catalog's groups change
    /// (the places read, which tell where things are found); a pair seen changes no level
    /// (<see cref="CompendiumBook.PairsVersion"/>); while it stays off, only when the rule does.
    /// </summary>
    internal sealed class CompendiumView
    {
        private static readonly string[] TermKeys = { "drops", "from", "needs", "gives", "spawns" };

        private readonly EntryCatalog _catalog;
        private readonly Dictionary<string, List<Entry>> _played = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Entry>> _leftovers = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Entry>> _places = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Entry>> _rooms = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        private readonly Dictionary<Entry, KnownLevel> _levels = new Dictionary<Entry, KnownLevel>();
        private readonly Dictionary<Entry, Entry> _because = new Dictionary<Entry, Entry>();
        private readonly HashSet<(Entry, Entry)> _owns = new HashSet<(Entry, Entry)>();
        private readonly HashSet<Entry> _byFloor = new HashSet<Entry>();

        private CompendiumBook _book;
        private int _bookVersion = -1;
        private CompendiumRule _rule;
        private int _indexedAt = -1;
        private IReadOnlyDictionary<string, int> _tiers;
        private int _floor;
        private bool _floorMoved;

        public CompendiumView(EntryCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>Indexes what plays, leaves behind and goes by each name, and the rooms by their dungeons' labels, as the catalog's groups are now.</summary>
        private void Index()
        {
            _indexedAt = _catalog.Regroups;
            _played.Clear();
            _leftovers.Clear();
            _places.Clear();
            _rooms.Clear();
            foreach (var entry in _catalog.All)
            {
                foreach (var owner in entry.UsedBy) Add(_played, owner, entry);
                foreach (var owner in entry.LeftBy) Add(_leftovers, owner, entry);
                Add(_places, Places.LabelOf(entry), entry);
                if (entry.Kind == Kind.Location && entry.Components != null && Array.IndexOf(entry.Components, "Room") >= 0)
                {
                    foreach (var label in entry.FoundIn) Add(_rooms, label, entry);
                }
            }
        }

        /// <summary>Counts up each time the levels are told anew.</summary>
        public int Version { get; private set; }

        /// <summary>The catalog it tells of.</summary>
        public EntryCatalog Catalog => _catalog;

        /// <summary>The book told last; none before.</summary>
        public CompendiumBook Book => _book;

        /// <summary>What the book told last learned lately, newest first; none before it was told.</summary>
        public IEnumerable<string> Lately => _book?.Lately ?? Array.Empty<string>();

        /// <summary>The ruleset last told; with it off, everything is known.</summary>
        public CompendiumRule Rule => _rule;

        /// <summary>Whether a kind's tab counts what was found of all there is; the others count only what was found.</summary>
        public static bool Counted(Kind kind) =>
            kind == Kind.Creature || kind == Kind.Item || kind == Kind.Piece || kind == Kind.Resource || kind == Kind.Spawner
            || kind == Kind.StatusEffect || kind == Kind.Location || kind == Kind.Raid || kind == Kind.Biome;

        /// <summary>How much the character knows of an entry under the rule last told.</summary>
        public KnownLevel Of(Entry entry)
        {
            if (entry == null) return KnownLevel.Unknown;
            if (_rule == CompendiumRule.Off) return KnownLevel.Known;
            return _levels.TryGetValue(entry, out var level) ? level : KnownLevel.Unknown;
        }

        /// <summary>What made an entry known or heard of along with it: the entry owning it or naming it; none for what the book holds itself.</summary>
        public Entry Because(Entry entry) => entry != null && _rule != CompendiumRule.Off && _because.TryGetValue(entry, out var owner) ? owner : null;

        /// <summary>What made an entry known, then what made that known, back to what the book holds itself.</summary>
        public IEnumerable<Entry> Chain(Entry entry)
        {
            var seen = new HashSet<Entry>();
            for (var owner = Because(entry); owner != null && seen.Add(owner); owner = Because(owner)) yield return owner;
        }

        /// <summary>
        /// The milestone floor to show by: each entry's tier (<see cref="TierPass"/>) and the floor
        /// reached, none at 0. Everything of a tier at or below it is known; told on the next
        /// <see cref="Tell"/> when either changed.
        /// </summary>
        public void Milestones(IReadOnlyDictionary<string, int> tiers, int floor)
        {
            if (ReferenceEquals(tiers, _tiers) && floor == _floor) return;
            _tiers = tiers;
            _floor = floor;
            _floorMoved = true;
        }

        /// <summary>Whether an entry is one of a known owner's own parts: what it plays, leaves behind or links to as its own.</summary>
        public bool Owns(Entry owner, Entry owned) => owner != null && owned != null && _owns.Contains((owner, owned));

        /// <summary>Whether two entries go together for the strict rules: seen together, or one a part of the other.</summary>
        public bool Related(Entry a, Entry b) => Witnessed(a, b) || Owns(a, b) || Owns(b, a);

        /// <summary>The locations going by a label (<see cref="Places.LabelOf"/>), as the catalog was last indexed.</summary>
        public IReadOnlyList<Entry> PlacesLabelled(string label) =>
            label != null && _places.TryGetValue(label, out var places) ? places : (IReadOnlyList<Entry>)Array.Empty<Entry>();

        /// <summary>Whether the character saw two entries go together, either way round (<see cref="CompendiumBook.Witness"/>).</summary>
        public bool Witnessed(Entry a, Entry b) => a != null && b != null && _book != null && _book.Witnessed(a.Key, b.Key);

        /// <summary>Whether the game told the character how a creature takes a damage type, on a hit of it (<see cref="HitTold"/>).</summary>
        public bool HitWith(Entry creature, string type) => creature != null && _book != null && _book.Witnessed(creature.Key, EntryKeys.Damage(type));

        /// <summary>Whether an entry's tier is at or below the floor, so it shows whatever the book holds of it; never one placed only to be glimpsed (<see cref="OnlyGlimpsed"/>).</summary>
        public bool BelowFloor(Entry entry) => entry != null && _floor > 0 && !OnlyGlimpsed(entry) && _tiers != null && _tiers.TryGetValue(entry.Key, out var tier) && tier <= _floor;

        /// <summary>The game's behaviour of a thing placed only to be glimpsed: it stands far off and is gone as a player comes near, or after a while.</summary>
        private const string GlimpsedBehaviour = "Odin";

        /// <summary>
        /// Whether the game places an entry only to be glimpsed (<see cref="GlimpsedBehaviour"/>):
        /// the hooded figure standing at the edge of sight. However low its tier, no floor shows it
        /// or brings it along; it is known once the character has seen it.
        /// </summary>
        public static bool OnlyGlimpsed(Entry entry) => Array.IndexOf(entry.Components, GlimpsedBehaviour) >= 0;

        /// <summary>Whether an entry's own tier is above the floor, so the floor does not bring it along; none is for one of no tier.</summary>
        private bool AboveFloor(Entry entry) => _tiers != null && _tiers.TryGetValue(entry.Key, out var tier) && tier > _floor;

        /// <summary>The floor shown by, the last tier it opens; none at 0.</summary>
        public int Floor => _floor;

        /// <summary>Whether an entry shows as known only because of the floor: neither the character knows it nor anything it knows brings it along.</summary>
        public bool ByFloor(Entry entry) => entry != null && _byFloor.Contains(entry);

        /// <summary>How many times every entry's level was worked out afresh, the catalog gone through whole.</summary>
        public int WholeCounts { get; private set; }

        /// <summary>
        /// Whether all that changed since the levels were last told is more learned in the same
        /// book: the rules, the floor and the catalog's groups as they were. Told on top
        /// (<see cref="Tell"/>), and with the panel open at most once a second.
        /// </summary>
        public bool OnlyLearnedSince(CompendiumBook book, CompendiumRule rule) =>
            book != null && ReferenceEquals(book, _book) && book.Version != _bookVersion && rule == _rule && !_floorMoved && _indexedAt == _catalog.Regroups;

        /// <summary>
        /// Works out every entry's level for this book and rule, unless nothing changed since last
        /// time. Where only more was learned in the same book (<see cref="OnlyLearnedSince"/>),
        /// as levels only rise, just what was raised since and what it brings along are told on
        /// top, as a count afresh would have them; otherwise the whole catalog is gone through.
        /// </summary>
        public void Tell(CompendiumBook book, CompendiumRule rule)
        {
            if (book == null) book = new CompendiumBook();
            var regrouped = _indexedAt != _catalog.Regroups;
            if (ReferenceEquals(book, _book) && book.Version == _bookVersion && rule == _rule && !regrouped && !_floorMoved) return;
            if (rule != CompendiumRule.Off && OnlyLearnedSince(book, rule) && book.RaisedSince(_bookVersion, Raised))
            {
                _bookVersion = book.Version;
                TellRaised();
                Raised.Clear();
                Version++;
                return;
            }
            _floorMoved = false;
            // Off, everything is known whatever the book holds: nothing to tell again.
            var stillOff = rule == CompendiumRule.Off && _rule == CompendiumRule.Off;
            _book = book;
            _bookVersion = book.Version;
            _rule = rule;
            if (stillOff) return;
            if (regrouped) Index();
            Version++;
            WholeCounts++;
            _levels.Clear();
            _because.Clear();
            _owns.Clear();
            _byFloor.Clear();
            if (rule == CompendiumRule.Off) return;

            // What the character knows itself, with all it brings along; then what the floor adds,
            // and all that brings, told apart as the floor's (ByFloor).
            var met = new Queue<Entry>();
            var floored = new List<Entry>();
            foreach (var entry in _catalog.All)
            {
                // A mod and a skill of the character's own are always known.
                var level = entry.Kind == Kind.Mod || entry.Kind == Kind.Skill ? KnownLevel.Known : book.Level(entry.Key);
                if (level < KnownLevel.Known && BelowFloor(entry)) floored.Add(entry);
                if (level == KnownLevel.Unknown) continue;
                _levels[entry] = level;
                // Only what is known brings its parts along, not what was only seen, as a creature seen from afar.
                if (level >= KnownLevel.Known) met.Enqueue(entry);
            }
            Spread(met, false);
            foreach (var entry in floored)
            {
                if (Of(entry) == KnownLevel.Known) continue;
                _levels[entry] = KnownLevel.Known;
                _byFloor.Add(entry);
                met.Enqueue(entry);
            }
            Spread(met, true);

            var heard = new List<(Entry Named, Entry By)>();
            foreach (var pair in _levels)
            {
                if (pair.Value == KnownLevel.Known) foreach (var named in Named(pair.Key)) heard.Add((named, pair.Key));
            }
            foreach (var (named, by) in heard)
            {
                if (Of(named) != KnownLevel.Unknown) continue;
                _levels[named] = KnownLevel.HeardOf;
                _because[named] = by;
            }
        }

        private readonly List<string> Raised = new List<string>();
        private readonly Queue<Entry> Bringing = new Queue<Entry>();
        private readonly List<Entry> NewlyKnown = new List<Entry>();

        /// <summary>
        /// Tells on top what the book raised since it was last told (<see cref="Raised"/>), as the
        /// whole count would have it. Raised to known, an entry is the book's own, no longer the
        /// floor's nor anything's part, and brings along what it owns; what showed only by the
        /// floor among that is the book's too now, and brings its own along again. Raised to less,
        /// it shows what the book holds, unless something known already brings it along. What
        /// becomes known names what was unknown, heard of.
        /// </summary>
        private void TellRaised()
        {
            foreach (var key in Raised)
            {
                var entry = _catalog.Find(key);
                if (entry == null || entry.Kind == Kind.Mod || entry.Kind == Kind.Skill) continue;
                var level = _book.Level(key);
                var now = Of(entry);
                if (level == KnownLevel.Known)
                {
                    _because.Remove(entry);
                    if (now == KnownLevel.Known && !_byFloor.Remove(entry)) continue;
                    _levels[entry] = KnownLevel.Known;
                    if (now != KnownLevel.Known) NewlyKnown.Add(entry);
                    Bringing.Enqueue(entry);
                }
                else if (now != KnownLevel.Known)
                {
                    _levels[entry] = level;
                    _because.Remove(entry);
                }
            }
            while (Bringing.Count > 0)
            {
                var owner = Bringing.Dequeue();
                foreach (var owned in Owned(owner))
                {
                    _owns.Add((owner, owned));
                    var was = Of(owned);
                    if (was == KnownLevel.Known && !_byFloor.Remove(owned)) continue;
                    _levels[owned] = KnownLevel.Known;
                    _because[owned] = owner;
                    if (was != KnownLevel.Known) NewlyKnown.Add(owned);
                    Bringing.Enqueue(owned);
                }
            }
            foreach (var known in NewlyKnown)
            {
                foreach (var named in Named(known))
                {
                    if (Of(named) != KnownLevel.Unknown) continue;
                    _levels[named] = KnownLevel.HeardOf;
                    _because[named] = known;
                }
            }
            NewlyKnown.Clear();
        }

        /// <summary>
        /// What a known entry owns is known, and owns in turn (an effect's own sounds):
        /// each owner waiting in the queue, and what it brings along; by the floor, told so, and
        /// only what is of no higher a tier than the floor.
        /// </summary>
        private void Spread(Queue<Entry> met, bool byFloor)
        {
            while (met.Count > 0)
            {
                var owner = met.Dequeue();
                foreach (var owned in Owned(owner))
                {
                    if (byFloor && (AboveFloor(owned) || OnlyGlimpsed(owned))) continue;
                    _owns.Add((owner, owned));
                    if (Of(owned) == KnownLevel.Known) continue;
                    _levels[owned] = KnownLevel.Known;
                    _because[owned] = owner;
                    if (byFloor) _byFloor.Add(owned);
                    met.Enqueue(owned);
                }
            }
        }

        /// <summary>What an entry owns: what it plays, what it leaves behind, a known dungeon's rooms, and what it links to that is not a thing of its own, by its own links, not those back from what links to it.</summary>
        private IEnumerable<Entry> Owned(Entry owner)
        {
            if (_played.TryGetValue(owner.Name, out var played)) foreach (var entry in played) yield return entry;
            if (_leftovers.TryGetValue(owner.Name, out var left)) foreach (var entry in left) yield return entry;
            foreach (var name in owner.LeavesBehind)
            {
                var entry = _catalog.Find(name);
                if (entry != null) yield return entry;
            }
            // A dungeon known: every one of its rooms.
            var label = Places.LabelOf(owner);
            if (label != null && Of(owner) == KnownLevel.Known && _rooms.TryGetValue(label, out var rooms))
            {
                foreach (var room in rooms) yield return room;
            }
            foreach (var link in owner.Links)
            {
                if (link.Back || GivesGuardianPower(link)) continue;
                var entry = _catalog.Find(link.Target);
                if (entry != null && Belongs(entry.Kind)) yield return entry;
            }
        }

        /// <summary>Whether a link is a boss stone's to its guardian power, which the stone gives only for the boss's trophy.</summary>
        private static bool GivesGuardianPower(Link link)
        {
            foreach (var note in link.Notes) if (Giver.IsGuardianPower(note)) return true;
            return false;
        }

        /// <summary>What a known entry's page names; what it owns is among them, already known.</summary>
        internal IEnumerable<Entry> Named(Entry entry)
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
