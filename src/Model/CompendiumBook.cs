using System;
using System.Collections.Generic;
using System.IO;
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

    /// <summary>How a character came to know a thing in play, told on its page (<see cref="CompendiumWords.CameToKnow"/>).</summary>
    internal enum CameBy
    {
        Fought,
        Killed,
        Tamed,
        Held,
        Made,
        Eaten,
        Recipe,
        Card,
        Used,
        Built,
        Hit,
        Picked,
        Trophy,
        StoodIn,
        WentInside,
        LivedThrough,
        Traded,
        OnYou,
        Given,
        Gives,
        Chosen,
    }

    /// <summary>
    /// What one character has come to know, entry by entry (by <see cref="Entry.Key"/>), each
    /// only ever rising; everything learned, newest first, with the day of its latest news; the
    /// worlds whose explored map was caught up with; the rules the player chose for the
    /// compendium and the floor they raised it to; the pairs seen together; the moment each
    /// thing was first seen and came to be known in play (<see cref="Moment"/>); and the deaths
    /// each creature dealt the character. Kept from session to session as text
    /// (<see cref="Save"/>, <see cref="Load"/>).
    /// </summary>
    internal sealed class CompendiumBook
    {
        private readonly Dictionary<string, KnownLevel> _levels = new Dictionary<string, KnownLevel>(StringComparer.Ordinal);
        private readonly List<string> _lately = new List<string>();
        private readonly HashSet<string> _mapped = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The kinds of place each source found in each world (<see cref="FoundIn"/>).</summary>
        private readonly Dictionary<(string World, WorldSource Source), HashSet<string>> _foundIn = new Dictionary<(string, WorldSource), HashSet<string>>();

        /// <summary>How each source's places show, where the player chose other than it starts at (<see cref="Shows"/>).</summary>
        private readonly Dictionary<WorldSource, SourceLevel> _shows = new Dictionary<WorldSource, SourceLevel>();

        private static readonly string[] NoKinds = Array.Empty<string>();

        /// <summary>Counts up whenever a source finds another kind of place or the player sets how a source shows, for what is told from them to tell again.</summary>
        public int SourcesVersion { get; private set; }

        /// <summary>
        /// Kinds of place a source found in a world (<see cref="WorldSource"/>), kept with the book
        /// apart from what the character knows: what they show as is the player's choice
        /// (<see cref="Shows"/>), laid over the book, never written into it.
        /// </summary>
        public void FoundIn(string world, WorldSource source, IEnumerable<string> kinds)
        {
            if (string.IsNullOrEmpty(world) || kinds == null) return;
            foreach (var kind in kinds)
            {
                if (string.IsNullOrEmpty(kind)) continue;
                if (!_foundIn.TryGetValue((world, source), out var found)) _foundIn[(world, source)] = found = new HashSet<string>(StringComparer.Ordinal);
                if (!found.Add(kind)) continue;
                SourcesVersion++;
                Changed = true;
            }
        }

        /// <summary>The kinds of place a source found in a world; none for a world it found none in.</summary>
        public IReadOnlyCollection<string> Found(string world, WorldSource source) =>
            world != null && _foundIn.TryGetValue((world, source), out var found) ? found : (IReadOnlyCollection<string>)NoKinds;

        /// <summary>How a source's places show: as the player chose, else as it starts (<see cref="WorldSources.Least"/>).</summary>
        public SourceLevel Shows(WorldSource source) => _shows.TryGetValue(source, out var level) ? level : WorldSources.Least(source);

        /// <summary>The sources of ground whose pin is on, their dungeons marked on Valheim's big map (<see cref="DungeonMarks"/>).</summary>
        private readonly HashSet<WorldSource> _marks = new HashSet<WorldSource>();

        /// <summary>Whether a source of ground has its pin on.</summary>
        public bool MarksDungeons(WorldSource source) => _marks.Contains(source);

        /// <summary>Turns a source of ground's pin on or off: a choice of the player's; a source without ground has none.</summary>
        public void MarkDungeons(WorldSource source, bool on)
        {
            if (!WorldSources.HasGround(source) || MarksDungeons(source) == on) return;
            if (on) _marks.Add(source);
            else _marks.Remove(source);
            SourcesVersion++;
            Changed = true;
        }

        /// <summary>Whether a source's dungeons are marked now: its pin on and its row showing anything.</summary>
        public bool MarksDungeonsNow(WorldSource source) => MarksDungeons(source) && Shows(source) > SourceLevel.Off;

        /// <summary>Sets how a source's places show, no lower than the least it allows.</summary>
        public void Show(WorldSource source, SourceLevel level)
        {
            var least = WorldSources.Least(source);
            if (level < least) level = least;
            if (!WorldSources.Allows(source, level) || Shows(source) == level) return;
            _shows[source] = level;
            SourcesVersion++;
            Changed = true;
        }
        private int _floor;
        private CompendiumRule _rule;
        private readonly HashSet<(string, string)> _pairs = new HashSet<(string, string)>();
        private readonly Dictionary<string, (CameBy Way, Moment At)> _came = new Dictionary<string, (CameBy, Moment)>(StringComparer.Ordinal);
        private readonly Dictionary<string, (Moment At, int Stars)> _seen = new Dictionary<string, (Moment, int)>(StringComparer.Ordinal);
        private readonly Dictionary<string, (int Count, int LastDay)> _deaths = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _newsDay = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>The moment the character is in now, for what it sees or comes to know; none outside a game. Not kept.</summary>
        public Func<Moment> Now { get; set; }

        /// <summary>
        /// Whether what is learned now is caught up from the game's own records rather than met in
        /// play: they do not say when, so nothing is kept of the moment and the news has no day.
        /// Not kept.
        /// </summary>
        public bool CatchingUp { get; set; }

        /// <summary>How and at which moment the character came to know a thing in play; none for one known before the book, or not by a way of its own.</summary>
        public (CameBy Way, Moment At)? Came(string key) => !string.IsNullOrEmpty(key) && _came.TryGetValue(key, out var came) ? came : ((CameBy, Moment)?)null;

        /// <summary>The moment the character first saw a thing in play, and a creature's stars then (-1 for none told); none for one known at once, off the map or caught up.</summary>
        public (Moment At, int Stars)? FirstSeen(string key) => !string.IsNullOrEmpty(key) && _seen.TryGetValue(key, out var seen) ? seen : ((Moment, int)?)null;

        /// <summary>How often a creature killed the character, and the game's day of the last (-1 where none was known); none for one that never did.</summary>
        public (int Count, int LastDay)? DeathsBy(string key) => !string.IsNullOrEmpty(key) && _deaths.TryGetValue(key, out var deaths) ? deaths : ((int, int)?)null;

        /// <summary>A creature killed the character: counted, with the day.</summary>
        public void Died(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _deaths.TryGetValue(key, out var deaths);
            _deaths[key] = (deaths.Count + 1, Now?.Invoke().Day ?? -1);
            Changed = true;
        }

        /// <summary>
        /// An entry's latest news (<see cref="BookNews"/>): what was found out of it or the map it
        /// came off, else known with the way it came, else seen; none for one only heard of or not
        /// in the book.
        /// </summary>
        public BookNews? NewsOf(string key)
        {
            var level = Level(key);
            if (level < KnownLevel.Seen) return null;
            var day = _newsDay.TryGetValue(key, out var told) ? told : -1;
            var way = _came.TryGetValue(key, out var came) ? came.Way : (CameBy?)null;
            var proof = FoundOutOf(key);
            if (proof == EntryKeys.OnMap) return new BookNews(NewsHow.Map, proof, way, day);
            if (proof != null) return new BookNews(NewsHow.FoundOut, proof, way, day);
            return new BookNews(level == KnownLevel.Known ? NewsHow.Known : NewsHow.Seen, null, way, day);
        }

        /// <summary>Counts up with every entry learned more of, so what tells the compendium tells it again.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// Counts up with every pair seen go together (<see cref="Witness"/>). Kept apart from
        /// <see cref="Version"/> because a pair makes nothing more or less known: only what the
        /// strict rules leave out of a page or a search changes.
        /// </summary>
        public int PairsVersion { get; private set; }

        /// <summary>Whether anything changed since it was last saved.</summary>
        public bool Changed { get; private set; }

        /// <summary>How much is known of an entry; unknown for one never met or none.</summary>
        public KnownLevel Level(string key) => !string.IsNullOrEmpty(key) && _levels.TryGetValue(key, out var level) ? level : KnownLevel.Unknown;

        /// <summary>
        /// Learns of an entry up to a level; true when that is more than was known. Quietly, it is
        /// not counted among the latest learned: it was known before, only not yet written down.
        /// Come to know in play by a way of its own, that way is kept with the moment
        /// (<see cref="Now"/>), once, as a thing is come to know once; first seen in play by sight
        /// (<paramref name="sighted"/>, not off the map), the moment is kept with a creature's
        /// <paramref name="stars"/> (-1 for none told). Caught up (<see cref="CatchingUp"/>), no moment is kept.
        /// </summary>
        public bool Raise(string key, KnownLevel level, bool quietly = false, CameBy? way = null, int stars = -1, bool sighted = true)
        {
            if (string.IsNullOrEmpty(key) || level <= Level(key)) return false;
            _levels[key] = level;
            var inPlay = !quietly && !CatchingUp;
            if (inPlay && level == KnownLevel.Known && way is CameBy by) _came[key] = (by, Now?.Invoke() ?? Moment.None);
            // A thing is raised to seen once, so this is its first sighting.
            if (inPlay && sighted && level == KnownLevel.Seen) _seen[key] = (Now?.Invoke() ?? Moment.None, stars);
            if (!quietly)
            {
                Newest(key);
                _found.Remove(key);
            }
            Version++;
            _raised.Add(key);
            if (_raised.Count > RaisesKept)
            {
                var dropped = _raised.Count / 2;
                _raised.RemoveRange(0, dropped);
                _raisedFrom += dropped;
            }
            Changed = true;
            return true;
        }

        /// <summary>How many of the latest raises the book remembers, for what is counted on top (<see cref="RaisedSince"/>); the older half is forgotten past it.</summary>
        public const int RaisesKept = 4096;

        /// <summary>What creatures were seen to drop, kill by kill, and what trees, rocks and other resources were seen to give, for the strict rules' amounts.</summary>
        private readonly SeenDrops _fell = new SeenDrops();
        private readonly SeenDrops _gave = new SeenDrops();

        private readonly SeenDrops _held = new SeenDrops();
        private readonly SeenDrops _weathers = new SeenDrops();

        /// <summary>What was counted of a kind (<see cref="Tally"/>); an empty count for none.</summary>
        public SeenDrops Tallied(Tally tally)
        {
            switch (tally)
            {
                case Tally.Fell: return _fell;
                case Tally.Gave: return _gave;
                case Tally.Held: return _held;
                case Tally.Weather: return _weathers;
                default: return NoTally;
            }
        }

        /// <summary>The chests the character opened, by an id of each in its world, so each is counted only the first time.</summary>
        private readonly HashSet<string> _opened = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>A chest's id for <see cref="FirstOpen"/>: its world's and its own in that world.</summary>
        public static string ChestId(string world, string chest) => world + "/" + chest;

        /// <summary>Whether this is the first time the character opens a chest, by its id; it is the last time it is.</summary>
        public bool FirstOpen(string id)
        {
            if (string.IsNullOrEmpty(id) || !_opened.Add(id)) return false;
            Changed = true;
            return true;
        }

        /// <summary>What a chest held the first time the character opened it, counted as one opening.</summary>
        public void Held(string chest, IEnumerable<(string Item, int Amount)> loot)
        {
            if (string.IsNullOrEmpty(chest)) return;
            _held.Record(chest, loot);
            Changed = true;
        }

        /// <summary>A weather seen come in a biome, counted as one of the weathers seen there.</summary>
        public void Weathered(string biome, string weather)
        {
            if (string.IsNullOrEmpty(biome) || string.IsNullOrEmpty(weather)) return;
            _weathers.Record(biome, new[] { (weather, 1) });
            Changed = true;
        }

        private static readonly SeenDrops NoTally = new SeenDrops();

        /// <summary>A creature's loot seen to fall as it died, counted as one kill.</summary>
        public void Dropped(string creature, IEnumerable<(string Item, int Amount)> loot)
        {
            if (string.IsNullOrEmpty(creature)) return;
            _fell.Record(creature, loot);
            Changed = true;
        }

        /// <summary>What a resource was seen to give as it broke, counted as one time where it gave anything.</summary>
        public void Yielded(string resource, IEnumerable<(string Item, int Amount)> loot)
        {
            var given = (loot ?? Enumerable.Empty<(string, int)>()).Where(l => !string.IsNullOrEmpty(l.Item) && l.Amount > 0).ToList();
            if (string.IsNullOrEmpty(resource) || given.Count == 0) return;
            _gave.Record(resource, given);
            Changed = true;
        }

        /// <summary>The keys raised, oldest first: the one at i raised to <see cref="Version"/> <c>_raisedFrom + i + 1</c>. Not kept.</summary>
        private readonly List<string> _raised = new List<string>();
        private int _raisedFrom;

        /// <summary>
        /// Adds the keys raised after a version to a list, oldest first, for what was counted at
        /// that version to have them counted on top; false, adding none, where the book no longer
        /// remembers that far back, or the version is still to come.
        /// </summary>
        public bool RaisedSince(int version, List<string> into)
        {
            if (version < _raisedFrom || version > Version) return false;
            for (var i = version - _raisedFrom; i < _raised.Count; i++) into.Add(_raised[i]);
            return true;
        }

        /// <summary>A key learned more of now: counted, first among what was learned, with the day (none while caught up).</summary>
        private void Newest(string key)
        {
            _learnedAt[key] = ++Learned;
            _lately.Remove(key);
            _lately.Insert(0, key);
            _newsDay[key] = CatchingUp ? -1 : Now?.Invoke().Day ?? -1;
        }

        /// <summary>
        /// Something the strict rules kept back of an entry was found out in play (a deed seen,
        /// a weak spot found, how a damage type lands, an attack's damage), as news: paired with
        /// it, the entry first among the latest learned again, saying what (<see cref="FoundOutOf"/>).
        /// True when it was not found out before.
        /// </summary>
        public bool FoundOut(string key, string proof)
        {
            if (!Witness(key, proof)) return false;
            Newest(key);
            _found[key] = proof;
            Changed = true;
            return true;
        }

        /// <summary>
        /// Takes back an entry that only a record no longer telling of it made known, as a kill
        /// once counted for a creature it never stood for: its level, what it was found out by
        /// (<paramref name="proof"/>), and its news. A count on top only raises levels, so what was
        /// counted before this is counted whole again (<see cref="RaisedSince"/>). True when the
        /// book held it.
        /// </summary>
        public bool TakeBack(string key, string proof)
        {
            if (string.IsNullOrEmpty(key) || !_levels.Remove(key)) return false;
            if (!string.IsNullOrEmpty(proof) && _pairs.Remove(Pair(key, proof))) PairsVersion++;
            _came.Remove(key);
            _seen.Remove(key);
            _newsDay.Remove(key);
            _found.Remove(key);
            _learnedAt.Remove(key);
            _lately.Remove(key);
            Version++;
            _raised.Clear();
            _raisedFrom = Version;
            Changed = true;
            return true;
        }

        /// <summary>Notes where the latest news of an entry came from, as off the explored map (<see cref="EntryKeys.OnMap"/>), told beside it in the learned list.</summary>
        public void NoteNews(string key, string note)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(note)) return;
            _found[key] = note;
            Changed = true;
        }

        /// <summary>What was last found out of an entry (<see cref="FoundOut"/>), or where its news came from (<see cref="NoteNews"/>), while that is the latest news of it; none otherwise.</summary>
        public string FoundOutOf(string key) => key != null && _found.TryGetValue(key, out var proof) ? proof : null;

        private readonly Dictionary<string, string> _found = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The keys learned more of, all of them, newest first.</summary>
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

        /// <summary>
        /// The milestone floor the player raised it to: everything of a tier at or below it shows
        /// as known (none at 0). It changes what is shown, never what is known.
        /// </summary>
        public int Floor
        {
            get => _floor;
            set
            {
                var floor = Math.Max(0, Math.Min(Tiers.Last, value));
                if (floor == _floor) return;
                _floor = floor;
                Changed = true;
            }
        }

        /// <summary>
        /// The rules the player chose in the panel for this character's compendium; off, the
        /// panel shows everything. It changes what is shown, never what is known.
        /// </summary>
        public CompendiumRule Rule
        {
            get => _rule;
            set
            {
                if (value == _rule || !Enum.IsDefined(typeof(CompendiumRule), value)) return;
                _rule = value;
                Changed = true;
            }
        }

        /// <summary>
        /// Notes that the character saw two entries go together, either way round: a drop fall
        /// from a creature, a creature in a biome or a place, an item made at a station, a chest's
        /// loot, a creature's attack hit it. True when it was not noted before. What the strict
        /// rules show of how a known entry connects.
        /// </summary>
        public bool Witness(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b || !_pairs.Add(Pair(a, b))) return false;
            PairsVersion++;
            Changed = true;
            return true;
        }

        /// <summary>Whether the character saw two entries go together, either way round.</summary>
        public bool Witnessed(string a, string b) => !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && _pairs.Contains(Pair(a, b));

        /// <summary>How many pairs the character saw go together.</summary>
        public int Pairs => _pairs.Count;

        private static (string, string) Pair(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

        /// <summary>Forgets that a world's map was caught up with, so the catch-up runs again.</summary>
        public void Unmap(string world)
        {
            if (world != null && _mapped.Remove(world)) Changed = true;
        }

        /// <summary>Everything known, a line an entry, then the latest learned in order, the worlds mapped, the rules, the floor, the pairs seen together and how each thing came to be known.</summary>
        public string Save() => ToSave()();

        /// <summary>
        /// The book's text (<see cref="Save"/>) as it stands now, worked out when called: what it
        /// holds is copied now and sorted and written out then, on a worker thread while more is
        /// learned. The book counts as saved from now until it changes again.
        /// </summary>
        public Func<string> ToSave()
        {
            var levels = _levels.ToArray();
            var lately = _lately.ToArray();
            var mapped = _mapped.ToArray();
            var foundIn = _foundIn.SelectMany(p => p.Value.Select(kind => (p.Key.World, p.Key.Source, Kind: kind))).ToArray();
            var shows = _shows.Where(p => p.Value != WorldSources.Least(p.Key)).ToArray();
            var marks = _marks.ToArray();
            var rule = _rule;
            var floor = _floor;
            var pairs = _pairs.ToArray();
            var came = _came.ToArray();
            var seen = _seen.ToArray();
            var deaths = _deaths.ToArray();
            var newsDay = new Dictionary<string, int>(_newsDay, StringComparer.Ordinal);
            var found = _found.ToArray();
            var fell = _fell.Save();
            var gave = _gave.Save();
            var held = _held.Save();
            var weathers = _weathers.Save();
            var opened = _opened.ToArray();
            Changed = false;
            return () =>
            {
                var text = new StringBuilder();
                foreach (var pair in levels.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    text.Append("know\t").Append(Stored.Escaped(pair.Key)).Append('\t').Append(Stored.Count((int)pair.Value)).Append('\n');
                }
                foreach (var key in lately)
                {
                    text.Append("lately\t").Append(Stored.Escaped(key));
                    if (newsDay.TryGetValue(key, out var day) && day >= 0) text.Append('\t').Append(Stored.Count(day));
                    text.Append('\n');
                }
                foreach (var world in mapped.OrderBy(w => w, StringComparer.Ordinal)) text.Append("mapped\t").Append(Stored.Escaped(world)).Append('\n');
                foreach (var (world, source, kind) in foundIn.OrderBy(f => f.World, StringComparer.Ordinal).ThenBy(f => f.Source).ThenBy(f => f.Kind, StringComparer.Ordinal))
                {
                    text.Append("place\t").Append(Stored.Escaped(world)).Append('\t').Append(source).Append('\t').Append(Stored.Escaped(kind)).Append('\n');
                }
                foreach (var pair in shows.OrderBy(p => p.Key)) text.Append("shows\t").Append(pair.Key).Append('\t').Append(pair.Value).Append('\n');
                foreach (var source in marks.OrderBy(s => s)) text.Append("marks\t").Append(source).Append('\n');
                if (rule != CompendiumRule.Off) text.Append("rule\t").Append(rule).Append('\n');
                if (floor > 0) text.Append("floor\t").Append(Stored.Count(floor)).Append('\n');
                foreach (var (a, b) in pairs.OrderBy(p => p.Item1, StringComparer.Ordinal).ThenBy(p => p.Item2, StringComparer.Ordinal))
                {
                    text.Append("saw\t").Append(Stored.Escaped(a)).Append('\t').Append(Stored.Escaped(b)).Append('\n');
                }
                foreach (var pair in came.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    text.Append("came\t").Append(Stored.Escaped(pair.Key)).Append('\t').Append(pair.Value.Way).Append('\t');
                    Written(text, pair.Value.At);
                    text.Append('\n');
                }
                foreach (var pair in seen.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    text.Append("seen\t").Append(Stored.Escaped(pair.Key)).Append('\t').Append(Stored.Count(pair.Value.Stars)).Append('\t');
                    Written(text, pair.Value.At);
                    text.Append('\n');
                }
                foreach (var pair in deaths.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    text.Append("died\t").Append(Stored.Escaped(pair.Key)).Append('\t').Append(Stored.Count(pair.Value.Count)).Append('\t').Append(Stored.Count(pair.Value.LastDay)).Append('\n');
                }
                foreach (var pair in found.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    text.Append("found\t").Append(Stored.Escaped(pair.Key)).Append('\t').Append(Stored.Escaped(pair.Value)).Append('\n');
                }
                foreach (var line in fell.Split('\n')) if (line.Length > 0) text.Append("fell\t").Append(line).Append('\n');
                foreach (var line in gave.Split('\n')) if (line.Length > 0) text.Append("gave\t").Append(line).Append('\n');
                foreach (var line in held.Split('\n')) if (line.Length > 0) text.Append("held\t").Append(line).Append('\n');
                foreach (var line in weathers.Split('\n')) if (line.Length > 0) text.Append("weather\t").Append(line).Append('\n');
                foreach (var id in opened.OrderBy(o => o, StringComparer.Ordinal)) text.Append("opened\t").Append(Stored.Escaped(id)).Append('\n');
                return text.ToString();
            };
        }

        /// <summary>
        /// The file a character's book is kept in: by the character's own file, where that lives
        /// (on this PC or in the cloud) and its id, which the game draws anew for each character
        /// made, so two characters never share one, not even one made under the name of one deleted.
        /// </summary>
        public static string FileName(string characterFile, string source, long id) => $"{characterFile}-{source}-{Stored.Count(id)}.dat";


        /// <summary>A moment as a line's last fields: day, biome, time of day, weather, what was in hand.</summary>
        private static void Written(StringBuilder text, Moment at)
        {
            text.Append(Stored.Count(at.Day)).Append('\t').Append(Stored.Escaped(at.Biome ?? "")).Append('\t').Append(at.Time)
                .Append('\t').Append(Stored.Escaped(at.Weather ?? "")).Append('\t').Append(Stored.Escaped(at.InHand ?? ""));
        }

        /// <summary>
        /// A moment read from a line's fields from <paramref name="from"/> on: the day and biome
        /// alone as kept before moments were, else all five; false where they cannot be read.
        /// </summary>
        private static bool Read(string[] parts, int from, out Moment at)
        {
            at = Moment.None;
            if (parts.Length != from + 2 && parts.Length != from + 5) return false;
            if (!Stored.TryCount(parts[from], out var day) || !Stored.TryUnescaped(parts[from + 1], out var biome)) return false;
            if (parts.Length == from + 2)
            {
                at = new Moment(day, biome);
                return true;
            }
            if (!Enum.TryParse(parts[from + 2], out DayPart time) || !Enum.IsDefined(typeof(DayPart), time)) return false;
            if (!Stored.TryUnescaped(parts[from + 3], out var weather) || !Stored.TryUnescaped(parts[from + 4], out var inHand)) return false;
            at = new Moment(day, biome, time, weather, inHand);
            return true;
        }

        /// <summary>
        /// What was saved; a line that cannot be read is passed over, as are those of a question no
        /// longer asked ("asked") and those of guesses at what ran in another player's game, which
        /// events crossing between games replaced ("elsewhere", "played").
        /// </summary>
        public static CompendiumBook Load(string text)
        {
            var book = new CompendiumBook();
            if (string.IsNullOrEmpty(text)) return book;
            var lately = new HashSet<string>(StringComparer.Ordinal);
            // The counts of what fell and was given, each its own text inside the book's.
            var fell = new StringBuilder();
            var gave = new StringBuilder();
            var held = new StringBuilder();
            var weathers = new StringBuilder();
            foreach (var line in text.Split('\n'))
            {
                if (line.StartsWith("fell\t", StringComparison.Ordinal)) fell.Append(line, 5, line.Length - 5).Append('\n');
                else if (line.StartsWith("gave\t", StringComparison.Ordinal)) gave.Append(line, 5, line.Length - 5).Append('\n');
                else if (line.StartsWith("held\t", StringComparison.Ordinal)) held.Append(line, 5, line.Length - 5).Append('\n');
                else if (line.StartsWith("weather\t", StringComparison.Ordinal)) weathers.Append(line, 8, line.Length - 8).Append('\n');
            }
            book._fell.Take(SeenDrops.Load(fell.ToString()));
            book._gave.Take(SeenDrops.Load(gave.ToString()));
            book._held.Take(SeenDrops.Load(held.ToString()));
            book._weathers.Take(SeenDrops.Load(weathers.ToString()));
            foreach (var line in text.Split('\n'))
            {
                var parts = line.TrimEnd('\r').Split('\t');
                if (parts.Length < 2 || !Stored.TryUnescaped(parts[1], out var name) || name.Length == 0) continue;
                switch (parts[0])
                {
                    case "opened" when parts.Length == 2:
                        book._opened.Add(name);
                        break;
                    case "found" when parts.Length == 3 && Stored.TryUnescaped(parts[2], out var proof) && proof.Length > 0:
                        book._found[name] = proof;
                        break;
                    case "know" when parts.Length == 3 && Stored.TryCount(parts[2], out var level) && level > (int)KnownLevel.Unknown && level <= (int)KnownLevel.Known:
                        book._levels[name] = (KnownLevel)level;
                        break;
                    case "lately" when (parts.Length == 2 || parts.Length == 3) && lately.Add(name):
                        book._lately.Add(name);
                        if (parts.Length == 3 && Stored.TryCount(parts[2], out var newsDay)) book._newsDay[name] = newsDay;
                        break;
                    case "mapped" when parts.Length == 2:
                        book._mapped.Add(name);
                        break;
                    case "place" when parts.Length == 4 && Enum.TryParse(parts[2], out WorldSource source) && Enum.IsDefined(typeof(WorldSource), source)
                                      && Stored.TryUnescaped(parts[3], out var kind) && kind.Length > 0:
                        if (!book._foundIn.TryGetValue((name, source), out var found)) book._foundIn[(name, source)] = found = new HashSet<string>(StringComparer.Ordinal);
                        found.Add(kind);
                        break;
                    case "shows" when parts.Length == 3 && Enum.TryParse(name, out WorldSource shown) && Enum.IsDefined(typeof(WorldSource), shown)
                                      && Enum.TryParse(parts[2], out SourceLevel level) && Enum.IsDefined(typeof(SourceLevel), level) && WorldSources.Allows(shown, level):
                        book._shows[shown] = level;
                        break;
                    case "marks" when parts.Length == 2 && Enum.TryParse(name, out WorldSource marked) && Enum.IsDefined(typeof(WorldSource), marked) && WorldSources.HasGround(marked):
                        book._marks.Add(marked);
                        break;
                    case "rule" when parts.Length == 2 && (name == nameof(CompendiumRule.Loose) || name == nameof(CompendiumRule.Strict)):
                        book._rule = name == nameof(CompendiumRule.Loose) ? CompendiumRule.Loose : CompendiumRule.Strict;
                        break;
                    case "floor" when parts.Length == 2 && Stored.TryCount(parts[1], out var floor):
                        book._floor = Math.Max(0, Math.Min(Tiers.Last, floor));
                        break;
                    case "saw" when parts.Length == 3 && Stored.TryUnescaped(parts[2], out var other) && other.Length > 0 && other != name:
                        book._pairs.Add(Pair(name, other));
                        break;
                    case "came" when parts.Length >= 5 && Enum.TryParse(parts[2], out CameBy way) && Enum.IsDefined(typeof(CameBy), way) && Read(parts, 3, out var cameAt):
                        book._came[name] = (way, cameAt);
                        break;
                    case "seen" when parts.Length == 8 && Stored.TryCount(parts[2], out var stars) && Read(parts, 3, out var seenAt):
                        book._seen[name] = (seenAt, stars);
                        break;
                    case "died" when parts.Length == 4 && Stored.TryCount(parts[2], out var deaths) && deaths > 0 && Stored.TryCount(parts[3], out var lastDay):
                        book._deaths[name] = (deaths, lastDay);
                        break;
                }
            }
            return book;
        }
    }
}
