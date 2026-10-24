using System;
using System.Linq;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What the panel shows and what the player has picked: the search, the kind chips, the list,
    /// the selection and its modifiers. The panel draws this and hands it the player's input.
    /// </summary>
    internal sealed partial class Explorer
    {
        private readonly EntryCatalog _entries;
        private readonly IReadOnlyList<Entry> _catalog;
        private readonly Favourites _favourites;
        private readonly Modifiers _modifiers = new Modifiers();
        private readonly Query _query = new Query();
        private readonly int[] _counts = new int[Enum.GetValues(typeof(Kind)).Length];
        private readonly int[] _found = new int[Enum.GetValues(typeof(Kind)).Length];
        private readonly int[] _total = new int[Enum.GetValues(typeof(Kind)).Length];

        /// <summary>What the character knows of each entry, for the compendium; none while it is off.</summary>
        private CompendiumView _view;
        private int _viewVersion = -1;
        private int _pairsVersion = -1;

        /// <summary>The entries the character has seen or knows, while the compendium is on.</summary>
        private List<Entry> _visible;

        private List<Entry> _results = new List<Entry>();

        /// <summary>The results moved right under what leaves them behind, for the list to indent (<see cref="Leftovers.UnderOwners"/>).</summary>
        private readonly HashSet<Entry> _underOwner = new HashSet<Entry>();

        private int _countAll;
        private int _learnedCount;

        /// <summary>What was learned lately, and how many of each kind of it the search finds; worked out again with the list.</summary>
        private readonly List<Entry> _learned = new List<Entry>();
        private readonly int[] _learnedCounts = new int[Enum.GetValues(typeof(Kind)).Length];

        /// <summary>The catalog's name words, for mending slips (<see cref="SearchSlip"/>), read when first needed.</summary>
        private HashSet<string> _slipWords;

        private readonly List<(string Typed, string Used)> _mended = new List<(string, string)>();
        private string _mendedText;
        private readonly Query _mendedQuery = new Query();

        private Entry _selected;
        private int _selectedIndex = -1;
        private int _selectionVersion;

        public Explorer(IReadOnlyList<Entry> catalog, Favourites favourites)
        {
            _entries = new EntryCatalog(catalog);
            _catalog = catalog;
            _favourites = favourites;
            _entries.Regrouped += Refresh;
            Refresh();
        }

        /// <summary>The catalog the explorer shows, which what reads the world shares (it lists again when the catalog's groups change).</summary>
        public EntryCatalog Entries => _entries;

        /// <summary>The entry kept under a key (<see cref="EntryKeys"/>), the first in the catalog where several are; null for none.</summary>
        public Entry Find(string key) => _entries.Find(key);

        public IReadOnlyList<Entry> Catalog => _catalog;
        public Favourites Favourites => _favourites;
        public Modifiers Modifiers => _modifiers;

        public string Text
        {
            get => _query.Text;
            set
            {
                value = value ?? "";
                if (value == _query.Text) return;
                _query.Text = value;
                Refresh();
            }
        }

        public Kind? KindFilter
        {
            get => _query.Kind;
            set
            {
                if (value == _query.Kind) return;
                _query.Kind = value;
                Refresh();
            }
        }

        public bool FavouritesOnly
        {
            get => _query.FavouritesOnly;
            set
            {
                if (value == _query.FavouritesOnly) return;
                _query.FavouritesOnly = value;
                Refresh();
            }
        }

        /// <summary>The most recently looked-at entries that may be remembered.</summary>
        public const int MostRecent = 500;

        private int _recentLimit = 30;

        /// <summary>How many recently looked-at entries are remembered, from one to <see cref="MostRecent"/>.</summary>
        public int RecentLimit
        {
            get => _recentLimit;
            set => _recentLimit = Math.Max(1, Math.Min(MostRecent, value));
        }

        private readonly List<string> _recent = new List<string>();
        private bool _recentOnly;

        private bool _learnedOnly;

        /// <summary>Only what the character learned lately (<see cref="CompendiumBook.Lately"/>), newest first; not with the recent list.</summary>
        public bool LearnedOnly
        {
            get => _learnedOnly;
            set
            {
                if (value == _learnedOnly) return;
                _learnedOnly = value;
                if (value) _recentOnly = false;
                Refresh();
            }
        }

        /// <summary>Only what was looked at recently, newest first.</summary>
        public bool RecentOnly
        {
            get => _recentOnly;
            set
            {
                if (value == _recentOnly) return;
                _recentOnly = value;
                if (value) _learnedOnly = false;
                Refresh();
            }
        }

        /// <summary>The keys of recently looked-at entries, newest first.</summary>
        public IReadOnlyList<string> RecentKeys => _recent;

        public OriginFilter Origin
        {
            get => _query.Origin;
            set
            {
                if (value == _query.Origin) return;
                _query.Origin = value;
                Refresh();
            }
        }

        /// <summary>
        /// Searches for the text with every other filter off, as a chip that searches does, going
        /// through the catalog once rather than once for each filter it clears.
        /// </summary>
        public void SearchEverything(string text)
        {
            _query.Kind = null;
            _query.FavouritesOnly = false;
            _recentOnly = false;
            _learnedOnly = false;
            _query.Origin = OriginFilter.All;
            _query.Text = text ?? "";
            Refresh();
        }

        /// <summary>What the list shows now, best match first.</summary>
        public IReadOnlyList<Entry> Results => _results;

        /// <summary>Whether a result sits right under what leaves it behind, and is indented there.</summary>
        public bool UnderOwner(Entry entry) => _underOwner.Contains(entry);

        /// <summary>How many of what the text and the other filters let through are of this kind.</summary>
        public int CountOf(Kind kind) => _counts[(int)kind];

        /// <summary>How many the text and the other filters let through, all kinds together.</summary>
        public int CountAll => _countAll;

        /// <summary>How many the learned list holds, whatever is shown now: what the character learned lately and has seen or come to know; none while the compendium is off.</summary>
        public int LearnedCount => _learnedCount;

        /// <summary>The view of what the character knows the explorer was last handed; none before.</summary>
        public CompendiumView Compendium => _view;

        /// <summary>Counts up each time what the character knows is handed over anew, so what is made from it can tell it is out of date.</summary>
        public int KnowStamp { get; private set; }

        /// <summary>Whether the compendium is on for the page shown: it shows only what the character has seen or knows. Not while peeking, when each page shows whole and its links lead anywhere; the list goes with <see cref="CompendiumSet"/>.</summary>
        public bool CompendiumOn => CompendiumSet && !_peeking;

        /// <summary>Whether the compendium is set on, peeking or not: what the list, its counts and the search go with, and Peek and the list of what was learned lately.</summary>
        public bool CompendiumSet => _view != null && _view.Rule != CompendiumRule.Off;

        /// <summary>How much the character knows of an entry; all of it while the compendium is off.</summary>
        public KnownLevel LevelOf(Entry entry) => CompendiumOn ? _view.Of(entry) : KnownLevel.Known;

        /// <summary>
        /// Whether a place's inside shows: one only seen from outside, by sight or on the explored
        /// map, shows its outside alone, its inside once entered and known; every one with the
        /// compendium off or peeking. None for no place.
        /// </summary>
        public bool InsideKnown(Entry place) => place != null && LevelOf(place) == KnownLevel.Known;

        /// <summary>Whether an entry shows as known only because of the compendium's floor (<see cref="CompendiumView.ByFloor"/>).</summary>
        public bool ByFloor(Entry entry) => CompendiumOn && _view.ByFloor(entry);

        /// <summary>The lowest tier the compendium's floor shows an entry by (<see cref="CompendiumView.FloorOf"/>); none (0) for one it does not show, or with the compendium off.</summary>
        public int FloorOf(Entry entry) => CompendiumOn ? _view.FloorOf(entry) : 0;

        /// <summary>Whether an entry's latest news is what a reading of the explored map brought (<see cref="EntryKeys.OnMap"/>), counted rather than named in the notice.</summary>
        public bool FromMap(Entry entry) => CompendiumOn && entry != null && _view.Book?.FoundOutOf(entry.Key) == EntryKeys.OnMap;

        /// <summary>An entry's latest news in the book (<see cref="CompendiumBook.NewsOf"/>); none with the compendium off or none to tell.</summary>
        private BookNews? NewsOf(Entry entry) => CompendiumOn && entry != null ? _view.NewsOf(entry.Key) : null;

        /// <summary>The game's day an entry's latest news came on, for its day in the learned list; -1 for none, caught up or kept before days were.</summary>
        public int LearnedDay(Entry entry) => NewsOf(entry)?.Day ?? -1;

        /// <summary>How an entry's latest news came, in a few words for its row in the learned list (<see cref="JournalWords.LearnedNote"/>); none with the compendium off.</summary>
        public string LearnedNote(Entry entry) => NewsOf(entry) is BookNews news ? JournalWords.LearnedNote(news) : null;

        /// <summary>Where an entry's row in the learned list opens its page (<see cref="NewsArrival"/>); none with the compendium off or no news.</summary>
        public NewsArrival ArrivalOf(Entry entry) => NewsOf(entry) is BookNews news ? NewsArrival.Of(news) : null;

        /// <summary>An entry's latest news as a line of the notice, by the name it is shown by; seen for one with none to tell.</summary>
        public NewsLine NewsLineOf(Entry entry)
        {
            var news = NewsOf(entry);
            return new NewsLine(entry?.ShownName ?? "", news?.How ?? NewsHow.Seen, news?.Proof, news?.Day ?? -1);
        }

        /// <summary>The source of the world an entry shows from only, as the player chose (<see cref="CompendiumView.ByWorld"/>); none with the compendium off.</summary>
        public WorldSource? ByWorld(Entry entry) => CompendiumOn ? _view.ByWorld(entry) : null;

        /// <summary>Whether an entry shows only by the player's choice, the floor or a source of the world, so it is marked.</summary>
        public bool ByChoice(Entry entry) => ByFloor(entry) || ByWorld(entry) != null;

        /// <summary>Whether what a link or chip goes to, by its key, shows only by the player's choice (<see cref="ByChoice"/>), to be marked as its row is.</summary>
        public bool ShownByChoice(string key) => ByChoice(Find(key));

        /// <summary>A link's or chip's tip, with what shows only by the player's choice saying so first: the floor's tier that shows it, or the source and how it shows.</summary>
        public string ChoiceTip(string key, string tip)
        {
            var entry = Find(key);
            if (ByFloor(entry)) return CompendiumWords.FloorTip(FloorOf(entry), tip);
            var source = ByWorld(entry);
            return source != null ? CompendiumWords.WorldTip(source.Value, _view.Of(entry), tip) : tip;
        }

        /// <summary>Whether what a link or chip on a page goes to, by its key, shows as known only because of the floor (<see cref="ByFloor"/>), to be marked as its row is.</summary>
        public bool ShownByFloor(string key) => ByFloor(Find(key));

        /// <summary>
        /// Whether a page may go to the entry a key names (<see cref="EntryKeys"/>): one in the
        /// catalog the character has seen or knows and the list could show (<see cref="Kinds.ForPlayers(Entry)"/>),
        /// any of them while the compendium is off or peeking. A name it may not go to is told as plain text.
        /// </summary>
        public bool Reaches(string key)
        {
            var entry = Find(key);
            return entry != null && LevelOf(entry) >= KnownLevel.Seen && (!CompendiumOn || Kinds.ForPlayers(entry));
        }

        /// <summary>
        /// Whether the page shown leaves out a fact naming the entry a key names: on a known page
        /// under either rules, what the character has not met (<see cref="NotMet"/>), unless the
        /// page is a place shown with all it holds (<see cref="CompendiumView.HoldsNamed"/>); under
        /// the strict rules above the milestone floor also what it never saw go together with the
        /// page, is no part of it (its sounds, effects, projectiles) and owns it not. Never a mod,
        /// always known, nor a skill the character has learned; always one it has not.
        /// </summary>
        public bool LeavesOut(string key) => LeavesOut(key, false);


        /// <summary>
        /// As <see cref="LeavesOut(string)"/>, but a fact the game shows the moment a thing is known
        /// (<paramref name="gameShown"/>: a recipe's materials and station, a piece's cost and tool,
        /// what a known thing is made into, a station's recipes, a store's wares) leaves out only
        /// what the character has not met, under either rules: the game's menus told it already.
        /// </summary>
        public bool LeavesOut(string key, bool gameShown)
        {
            if (!NamesOnlyMet) return false;
            var other = Find(key);
            if (other == null || other == _selected || other.Kind == Kind.Mod) return false;
            if (_view.Of(other) < KnownLevel.Seen) return !NamesAllHeld(other);
            return !gameShown && other.Kind != Kind.Skill && LeavingOut && !_view.Related(_selected, other);
        }

        /// <summary>
        /// Whether the page shown names only what the character has met, seen or come to know
        /// (<see cref="LeavesOut(string)"/>): a known page with the compendium on, under either rules and
        /// whatever the floor; never a mod's page, which lists only what was seen of what it adds.
        /// </summary>
        public bool NamesOnlyMet =>
            CompendiumOn && _selected != null && _selected.Kind != Kind.Mod && _view.Of(_selected) == KnownLevel.Known;

        /// <summary>
        /// Whether what hinges on the entry a key names is left out whole, a topic of a page or a
        /// look on the stage that exists only through it, as a lox's riding and saddled look through
        /// its saddle: with the compendium on, until the character met it, seen counting, so no
        /// ??? where it would be named tells it is there. None for no key.
        /// </summary>
        public bool HidesHinged(string key) => !string.IsNullOrEmpty(key) && NotMet(key);

        /// <summary>
        /// The looks of a creature's gear the stage offers, by their place among all its looks: each
        /// whose items, by their keys (<paramref name="itemsOf"/>), were all met, as a set's look
        /// shows them and its name tells their attacks; under the strict rules each the creature
        /// shown was seen carrying every item of (<see cref="WearsShown"/>); every look with the
        /// compendium off.
        /// </summary>
        public List<int> LooksShown(int looks, Func<int, IReadOnlyList<string>> itemsOf)
        {
            var shown = new List<int>();
            for (var look = 0; look < looks; look++)
            {
                var hidden = false;
                foreach (var item in itemsOf(look)) hidden |= HidesHinged(item) || (Find(item) != null && !WearsShown(_selected?.Key, item));
                if (!hidden) shown.Add(look);
            }
            return shown;
        }

        /// <summary>
        /// Whether a key names an entry of the catalog the character has not met, seen or come to
        /// know, with the compendium on: a creature so is left off a place's example on the stage,
        /// and out of a room's card, unless the page shown is a place shown with all it holds.
        /// Never while peeking or with the compendium off.
        /// </summary>
        public bool NotMet(string key)
        {
            if (!CompendiumOn) return false;
            var entry = Find(key);
            return entry != null && _view.Of(entry) < KnownLevel.Seen && !NamesAllHeld(entry);
        }

        /// <summary>
        /// Whether the page shown names an entry the character never met as one of all it holds: a
        /// room or place shown with all it holds (<see cref="CompendiumView.HoldsNamed"/>), or a
        /// place naming what its rooms walked on this server hold (<see cref="CompendiumView.HeldByWalks"/>);
        /// never a skill not learned, named nowhere.
        /// </summary>
        private bool NamesAllHeld(Entry other) => other.Kind != Kind.Skill && (_view.HoldsNamed(_selected) || _view.HeldByWalks(_selected, other));

        /// <summary>
        /// The faint line at a section's end telling how many things its page left out, in the
        /// words of the rules leaving them out: what the character has not met, or under the
        /// strict rules what it has not seen together (<see cref="CompendiumWords.LeftOutOf"/>);
        /// without "more" where the section lists nothing else (<paramref name="alone"/>). Only
        /// for a creature's loot and where it lives, counts the player can act on; none elsewhere.
        /// </summary>
        public string LeftOutLine(string topic, int count, bool alone = false)
        {
            // Only a count the player can act on is said: drops not yet seen fall, places it lives
            // not met in, and what a dungeon room's card holds not yet met.
            if (topic != "loot" && topic != "lives" && topic != "room") return null;
            return LeavingOut ? CompendiumWords.LeftOutOf(topic, count, alone) : CompendiumWords.NotYetMet(topic, count, alone);
        }

        /// <summary>
        /// Whether the page shown keeps back how it takes a damage type, where a hit's popup tells
        /// it (a creature, or anything that breaks): under the strict rules, on a known page above
        /// the milestone floor, a type the game never told the character on a hit of it (<see cref="HitTold"/>).
        /// </summary>
        public bool HidesDamage(string type) => LeavingOut && !_view.HitWith(_selected, type);

        /// <summary>
        /// Whether a creature's page shown keeps back the damage of one of its attacks (a line of
        /// its attacks table, <see cref="EntryKeys.PlayAttack"/>): under the strict rules, on a
        /// known page above the milestone floor, one that never hit the character.
        /// </summary>
        public bool HidesAttack(string link) =>
            LeavingOut && _selected.Kind == Kind.Creature && EntryKeys.PlaysAttack(link, out var item, out _) && !_view.Book.Proved(_selected.Key, EntryKeys.Attack(item));

        /// <summary>
        /// Whether the page shown keeps back what only something seen done tells (a deed,
        /// <see cref="EntryKeys.Did"/>, or a weak spot a hit found, <see cref="EntryKeys.WeakSpot"/>):
        /// under the strict rules, on a known page above the milestone floor, until it was seen.
        /// </summary>
        public bool KeepsBack(string proof) => LeavingOut && proof != null && !_view.Book.Proved(_selected.Key, proof) && !AlsoShownBy(proof);

        /// <summary>The game prints the health of the creature ridden (<c>EnemyHud</c>, its health text for the mount alone): riding one proves its health as a kill does.</summary>
        private bool AlsoShownBy(string proof) => proof == EntryKeys.Did(Deed.Killed) && _view.Book.Proved(_selected.Key, EntryKeys.Did(Deed.Ridden));

        /// <summary>
        /// What shows a table every line of which the page shown keeps back (<see cref="KeepsBack"/>),
        /// as a creature's stars table before one was killed: what shows its first line, the first
        /// of it to show; none where any line shows or waits for nothing, or with no lines.
        /// </summary>
        public string KeepsBackWhole(IReadOnlyDictionary<int, string> lineProofs, int lines)
        {
            if (lines <= 0 || lineProofs == null) return null;
            for (var line = 0; line < lines; line++)
            {
                if (!lineProofs.TryGetValue(line, out var proof) || !KeepsBack(proof)) return null;
            }
            return lineProofs[0];
        }

        /// <summary>Whether a page offers the game's commands that spawn or give its thing: with the compendium off or while peeking, never as part of what a character knows.</summary>
        public bool ShowsCommands => !CompendiumOn;

        /// <summary>Whether the page shown keeps back what any one of several things seen would show (<see cref="KeepsBack"/>), as a thing to bring against a creature; never for none.</summary>
        public bool KeepsBackAll(IReadOnlyList<string> proofs)
        {
            if (proofs == null || proofs.Count == 0 || !LeavingOut) return false;
            foreach (var proof in proofs) if (_view.Book.Proved(_selected.Key, proof)) return false;
            return true;
        }

        /// <summary>
        /// An item's amount in a row counted from what the character saw (<see cref="Tally"/>) of
        /// a creature or resource: under the strict rules, on a known page above the milestone
        /// floor, how many came and how often, ??? where it was never counted; none otherwise,
        /// when the game's own amount shows.
        /// </summary>
        public string StrictAmount(Tally tally, string of, string item)
        {
            if (tally == Tally.None || !LeavingOut) return null;
            var counted = _view.Book.Tallied(tally);
            var drop = counted.Of(of).FirstOrDefault(d => d.Item == item);
            if (drop == null) return CompendiumWords.Hidden;
            return tally == Tally.Weather ? TallyWords.Seen(drop, counted.Kills(of)) : TallyWords.Chip(drop, counted.Kills(of));
        }

        /// <summary>
        /// A line of where the item shown comes from under the strict rules, on a known page above
        /// the milestone floor: a way with a chance in it told with the character's count in its
        /// place (<see cref="SourceWords.CountedBy"/>), ??? where nothing was counted or nothing
        /// counts; none otherwise, when the game's own line shows.
        /// </summary>
        public string StrictSourceLine(SourceFacts source)
        {
            var amount = StrictSourceAmount(source);
            return amount != null ? SourceWords.Counted(source, amount) : null;
        }

        /// <summary>
        /// The amount a line of where the item shown comes from tells under the strict rules
        /// (<see cref="StrictSourceLine"/>), alone, as its giver's chip says it: the character's
        /// count, or ???; none where the game's own amount shows.
        /// </summary>
        public string StrictSourceAmount(SourceFacts source)
        {
            if (!LeavingOut) return null;
            // A producer's pace: as the game tells it once collected from, ??? before.
            var shownBy = SourceWords.ShownBy(source);
            if (shownBy != null) return _view.Book.Proved(source.Giver, shownBy) ? null : CompendiumWords.Hidden;
            var by = SourceWords.CountedBy(source);
            if (by == null) return null;
            // A way nothing counts reads an empty count (CompendiumBook.Tallied), so ???.
            var counted = _view.Book.Tallied(by.Value);
            var drop = counted.Of(source.Giver).FirstOrDefault(d => d.Item == _selected.Name);
            return drop != null ? TallyWords.Line(by.Value, drop, counted.Kills(source.Giver)) : CompendiumWords.Hidden;
        }

        /// <summary>A faint line saying what shows a line of a table kept back (<see cref="CompendiumWords.ShowsOnce(string, string)"/>).</summary>
        public string ShowsOnce(string what, string proof) => CompendiumWords.ShowsOnce(what, proof);

        /// <summary>The faint line under a counted row where an amount is not yet known (<see cref="TallyWords.NotYetCounted"/>).</summary>
        public string NotYetCounted(Tally tally) => TallyWords.NotYetCounted(tally);

        /// <summary>Such a row's title, after how it is given (<paramref name="lead"/>): how many times it was seen; none where the game's own title shows (<see cref="StrictAmount"/>).</summary>
        public string StrictTitle(Tally tally, string of, string lead) =>
            tally == Tally.None || !LeavingOut ? null : TallyWords.Title(tally, lead, _view.Book.Tallied(tally).Kills(of));

        /// <summary>
        /// Whether the page shown leaves out a line naming entries all of which it leaves out
        /// (<see cref="LeavesOut(string)"/>), as a creature's spawn line for biomes it was never met in;
        /// never one naming none.
        /// </summary>
        public bool LeavesOutAll(IReadOnlyList<string> keys, bool gameShown = false)
        {
            if (keys == null || keys.Count == 0) return false;
            foreach (var key in keys) if (!LeavesOut(key, gameShown)) return false;
            return true;
        }

        /// <summary>
        /// Whether the page shown leaves anything out (<see cref="LeavesOut(string)"/>): under the strict
        /// rules, a known page above the milestone floor; never a mod's page, which lists only
        /// what was seen of what it adds.
        /// </summary>
        public bool LeavingOut =>
            CompendiumOn && _view.Rule == CompendiumRule.Strict && _selected != null && _selected.Kind != Kind.Mod && _selected.Kind != Kind.Skill
            && _view.Of(_selected) == KnownLevel.Known && !_view.Whole(_selected);

        /// <summary>
        /// Whether the page shown, known under the strict rules, shows whole by the player's choice
        /// rather than keeping back as the rules say: below the floor (<paramref name="source"/>
        /// none), or by a source of the world.
        /// </summary>
        public bool ShownWhole(out WorldSource? source)
        {
            source = null;
            if (!CompendiumOn || _view.Rule != CompendiumRule.Strict || _selected == null || _selected.Kind == Kind.Mod || _selected.Kind == Kind.Skill
                || _view.Of(_selected) != KnownLevel.Known || !_view.Whole(_selected)) return false;
            if (!_view.BelowFloor(_selected)) source = _view.ByWorld(_selected);
            return true;
        }

        /// <summary>How many of a kind the character has met (seen or known), whatever is typed.</summary>
        public int FoundOf(Kind kind) => _found[(int)kind];

        /// <summary>How many of a kind there are in all, whatever is typed: of places, those the world holds where its census is shown (<see cref="WorldHolds"/>).</summary>
        public int TotalOf(Kind kind) => _total[(int)kind];

        /// <summary>How many of a kind the catalog holds, whatever the world holds.</summary>
        public int AllOf(Kind kind) => _all[(int)kind];

        private readonly int[] _all = new int[Enum.GetValues(typeof(Kind)).Length];

        /// <summary>The places the world holds, and their rooms, by the census shown; none counts every one.</summary>
        private HashSet<Entry> _held;
        private int _heldVersion = -1;

        /// <summary>The places the world holds by its census (<see cref="PlaceCensus.Held"/>), and a count moving each time they change, for the Locations tab to count only those; none counts every place again.</summary>
        public void WorldHolds(HashSet<Entry> held, int version)
        {
            if (version == _heldVersion && ReferenceEquals(held, _held)) return;
            _held = held;
            _heldVersion = version;
            if (CompendiumSet) Retell();
        }

        /// <summary>
        /// Shows what the character knows (<see cref="CompendiumView"/>): the list again, with what
        /// was found of each kind counted, and the steps back and forward without what it has not
        /// seen, whenever the view or what it holds changed. A pair seen go together, which makes
        /// nothing more or less known, only lists again under the strict rules.
        /// </summary>
        public void Know(CompendiumView view)
        {
            var version = view?.Version ?? -1;
            var pairs = view?.Book?.PairsVersion ?? -1;
            if (ReferenceEquals(view, _view) && version == _viewVersion)
            {
                if (pairs == _pairsVersion) return;
                _pairsVersion = pairs;
                // A pair seen changes no level, only what the strict rules leave out of the page and
                // the search, and, found out, the order of the learned list.
                if (CompendiumSet && (_view.Rule == CompendiumRule.Strict || _learnedOnly))
                {
                    KnowStamp++;
                    Refresh();
                }
                return;
            }
            _view = view;
            _viewVersion = version;
            _pairsVersion = pairs;
            // A peek ends with the compendium turned off.
            if (!CompendiumSet) Peeking = false;
            Retell();
        }

        /// <summary>Counts what was found of each kind, drops the steps to what was not seen and lists again, for the view as it is now.</summary>
        private void Retell()
        {
            KnowStamp++;
            _slipWords = null;
            _visible = null;
            Array.Clear(_found, 0, _found.Length);
            Array.Clear(_total, 0, _total.Length);
            Array.Clear(_all, 0, _all.Length);
            if (CompendiumSet)
            {
                _visible = new List<Entry>();
                foreach (var entry in _catalog)
                {
                    // A creature's body is its leftover, counted and listed nowhere.
                    if (Kinds.IsBody(entry)) continue;
                    var level = _view.Of(entry);
                    _all[(int)entry.Kind]++;
                    // With the census shown, places count only where the world holds them.
                    var counted = entry.Kind != Kind.Location || _held == null || _held.Contains(entry);
                    if (counted) _total[(int)entry.Kind]++;
                    if (counted && level >= KnownLevel.Seen) _found[(int)entry.Kind]++;
                    if (level >= KnownLevel.Seen) _visible.Add(entry);
                }
                // Steps back and forward lead only to what the character has seen.
                _back.RemoveAll(Unmet);
                _forward.RemoveAll(Unmet);
            }
            Refresh();
        }

        private bool Unmet(Place place) => place.Selected != null && LevelOf(place.Selected) < KnownLevel.Seen;

        /// <summary>
        /// Which of the entries of its kind going by its name an entry is, as its list row tells it
        /// (<see cref="Entry.Alike"/>); with the compendium set, told among what the list shows
        /// only, so it never tells how many alike the character has not met.
        /// </summary>
        public string AlikeOf(Entry entry)
        {
            if (entry == null) return null;
            if (!CompendiumSet || _visible == null) return entry.Alike;
            if (_alikeStamp != KnowStamp)
            {
                _alikeStamp = KnowStamp;
                _alikeShown.Clear();
                var shown = new List<Entry>(_visible);
                shown.Sort((a, b) => a.NameOrder != b.NameOrder ? a.NameOrder.CompareTo(b.NameOrder) : string.Compare(a.ShownName, b.ShownName, StringComparison.OrdinalIgnoreCase));
                Search.TellAlike(shown, (e, alike) => { if (alike != null) _alikeShown[e] = alike; });
            }
            return _alikeShown.TryGetValue(entry, out var told) ? told : null;
        }

        private readonly Dictionary<Entry, string> _alikeShown = new Dictionary<Entry, string>();
        private int _alikeStamp = -1;

        /// <summary>The selection as the stage may show it: none while the character has not seen it.</summary>
        public Entry Shown => _selected != null && LevelOf(_selected) >= KnownLevel.Seen ? _selected : null;

        /// <summary>
        /// The picked kind has nothing the text and the other filters let through while other
        /// kinds do, so the list shows those, as the All tab would; the kind stays picked and
        /// the list goes back to it once it has matches again.
        /// </summary>
        public bool ShowingEveryKind => _showingEveryKind;

        /// <summary>The words of a search that found nothing at all that were read as others one slip away, and those others; none for most searches.</summary>
        public IReadOnlyList<(string Typed, string Used)> Mended => _mended;

        /// <summary>The search as the list shows it when slips were mended; null when they were not.</summary>
        public string MendedText => _mendedText;

        private bool _showingEveryKind;

        public Entry Selected => _selected;

        /// <summary>Position of the selection in <see cref="Results"/>, or -1.</summary>
        public int SelectedIndex => _selectedIndex;

        /// <summary>Changes every time a different entry is selected, so a view can tell.</summary>
        public int SelectionVersion => _selectionVersion;

        /// <summary>Selects an entry; picking another than the one shown is a step to go back from.</summary>
        public void Select(Entry entry) => Select(entry, true);

        private void Select(Entry entry, bool record)
        {
            if (entry == _selected) return;
            if (record && entry != null && _here != null && _here.Selected != entry)
            {
                Remember(_back);
                _forward.Clear();
            }
            // A peek at the page shown alone ends on going to another, but for one the character has
            // not seen, which only that page's links reach: that one is peeked at in turn.
            var endsPeek = PeekEndsGoingTo(entry);

            _selected = entry;
            _selectedIndex = entry == null ? -1 : _results.IndexOf(entry);
            _selectionVersion++;
            _modifiers.ResetFor(entry);
            if (endsPeek) EndPeek();

            // The recent list is not reordered under the cursor; it catches up on the next refresh.
            if (entry == null) return;
            _here = Here(entry);
            _recent.Remove(entry.Key);
            _recent.Insert(0, entry.Key);
            if (_recent.Count > RecentLimit) _recent.RemoveRange(RecentLimit, _recent.Count - RecentLimit);
        }

        /// <summary>Moves the selection up or down the list, stopping at either end.</summary>
        public void Move(int rows)
        {
            if (_results.Count == 0) return;

            // With nothing selected, down starts from above the first row and up from below the last.
            var from = _selectedIndex >= 0 ? _selectedIndex : (rows > 0 ? -1 : _results.Count);
            var index = Math.Max(0, Math.Min(_results.Count - 1, from + rows));
            Select(_results[index]);
        }

        /// <summary>Selects the status effect of that name, or the prefab when it is not one (<see cref="Jump(string)"/>).</summary>
        public bool Jump(string prefabName, bool statusEffect) => Jump(statusEffect ? EntryKeys.For(Kind.StatusEffect, prefabName) : prefabName);

        /// <summary>
        /// Selects the entry a key names (<see cref="EntryKeys"/>): a prefab by its name, as when an
        /// ingredient or a drop is clicked, or a status effect or raid in its namespace. When the
        /// search or the filters hide it, they are cleared so it shows. False when there is none.
        /// </summary>
        public bool Jump(string key)
        {
            var target = Find(key);
            // What the character has not seen is listed nowhere, a heard-of name only on its page.
            if (target == null || LevelOf(target) < KnownLevel.Seen) return false;
            if (target != _selected && _here != null)
            {
                Remember(_back);
                _forward.Clear();
            }

            if (!_results.Contains(target))
            {
                _query.Text = "";
                _query.Kind = null;
                _query.FavouritesOnly = false;
                _query.Origin = OriginFilter.All;
                _recentOnly = false;
                _learnedOnly = false;
                Refresh();
            }

            Select(target, false);
            return true;
        }

        // ----- Back and forward -----

        /// <summary>How many steps back are kept.</summary>
        public const int HistoryLimit = 50;

        /// <summary>Where the player was: the search, the filters and the selection.</summary>
        private sealed class Place
        {
            public string Text;
            public Kind? Kind;
            public bool FavouritesOnly;
            public bool RecentOnly;
            public bool LearnedOnly;
            public OriginFilter Origin;
            public Entry Selected;

            /// <summary>Whether the page was peeked at alone when it was left, to be peeked at again on a step back or forward to it.</summary>
            public bool Peeked;
        }

        /// <summary>The entry shown last, with the search and filters it was picked from.</summary>
        private Place _here;

        private readonly List<Place> _back = new List<Place>();
        private readonly List<Place> _forward = new List<Place>();

        public bool CanGoBack => _back.Count > 0;
        public bool CanGoForward => _forward.Count > 0;

        /// <summary>Goes back to the entry shown before, with its search and filters.</summary>
        public bool Back() => Step(_back, _forward);

        /// <summary>Goes forward again to where going back came from.</summary>
        public bool Forward() => Step(_forward, _back);

        private bool Step(List<Place> from, List<Place> to)
        {
            if (from.Count == 0) return false;
            Remember(to);
            var place = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);

            _query.Text = place.Text;
            _query.Kind = place.Kind;
            _query.FavouritesOnly = place.FavouritesOnly;
            _query.Origin = place.Origin;
            _recentOnly = place.RecentOnly;
            _learnedOnly = place.LearnedOnly;
            // A page left while peeked at is peeked at again, the peek moving with the step.
            if (place.Peeked) PeekAgain(place.Selected);
            Refresh();
            Select(place.Selected, false);
            return true;
        }

        /// <summary>
        /// Keeps the entry shown last: with the search as it is now when that still shows it,
        /// otherwise with the search it was picked from.
        /// </summary>
        private void Remember(List<Place> into)
        {
            if (_here == null) return;
            var place = _results.Contains(_here.Selected) ? Here(_here.Selected) : _here;
            place.Peeked = _peeking && _peekedPage != null && _peekedPage == _here.Selected;
            into.Add(place);
            if (into.Count > HistoryLimit) into.RemoveAt(0);
        }

        private Place Here(Entry selected) => new Place
        {
            Text = _query.Text,
            Kind = _query.Kind,
            FavouritesOnly = _query.FavouritesOnly,
            RecentOnly = _recentOnly,
            LearnedOnly = _learnedOnly,
            Origin = _query.Origin,
            Selected = selected,
        };

        // ----- Keeping and putting back -----

        /// <summary>Keeps where the explorer is whole, to be put back with <see cref="Restore"/>.</summary>
        [ForTest]
        public Kept Keep() => new Kept(this);

        /// <summary>Puts the explorer back where it was kept: search, filters, selection, recent entries and history.</summary>
        [ForTest]
        public void Restore(Kept kept) => kept?.Into(this);

        /// <summary>
        /// Where the explorer was: the search and filters, the selection, the recent entries and
        /// the steps back and forward, so something that looks around through it (the self-test)
        /// leaves it as the player had it.
        /// </summary>
        public sealed class Kept
        {
            private readonly string _text;
            private readonly Kind? _kind;
            private readonly bool _favouritesOnly, _recentOnly, _learnedOnly;
            private readonly OriginFilter _origin;
            private readonly Entry _selected;
            private readonly Place _here;
            private readonly List<string> _recent;
            private readonly List<Place> _back, _forward;

            internal Kept(Explorer explorer)
            {
                _text = explorer._query.Text;
                _kind = explorer._query.Kind;
                _favouritesOnly = explorer._query.FavouritesOnly;
                _origin = explorer._query.Origin;
                _recentOnly = explorer._recentOnly;
                _learnedOnly = explorer._learnedOnly;
                _selected = explorer._selected;
                _here = explorer._here;
                _recent = new List<string>(explorer._recent);
                _back = new List<Place>(explorer._back);
                _forward = new List<Place>(explorer._forward);
            }

            internal void Into(Explorer explorer)
            {
                explorer._query.Text = _text;
                explorer._query.Kind = _kind;
                explorer._query.FavouritesOnly = _favouritesOnly;
                explorer._query.Origin = _origin;
                explorer._recentOnly = _recentOnly;
                explorer._learnedOnly = _learnedOnly;
                explorer._recent.Clear();
                explorer._recent.AddRange(_recent);
                explorer.Refresh();

                explorer._selected = _selected;
                explorer._selectedIndex = _selected == null ? -1 : explorer._results.IndexOf(_selected);
                explorer._selectionVersion++;
                explorer._modifiers.ResetFor(_selected);
                explorer._here = _here;
                explorer._back.Clear();
                explorer._back.AddRange(_back);
                explorer._forward.Clear();
                explorer._forward.AddRange(_forward);
            }
        }

        public void ToggleFavourite(Entry entry)
        {
            _favourites.Toggle(entry);
            if (_query.FavouritesOnly) Refresh();
        }

        /// <summary>
        /// The results by their groups, the groups in their order and then by name, within a group
        /// by rank (<see cref="Entry.GroupRank"/>) and then in the order the search gave them.
        /// Sorted as numbers: a group's place, a rank and each result's place in one, so a tab of
        /// thousands is sorted with no call per comparison.
        /// </summary>
        public static List<Entry> ByGroup(List<Entry> results)
        {
            var orders = results.Select(e => e.GroupOrder).Distinct().OrderBy(o => o).ToList();
            var names = results.Select(e => e.Group ?? "").Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
            var orderRank = new Dictionary<int, int>();
            for (var i = 0; i < orders.Count; i++) orderRank[orders[i]] = i;
            var nameRank = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < names.Count; i++) nameRank[names[i]] = i;

            var keys = new long[results.Count];
            var entries = results.ToArray();
            for (var i = 0; i < entries.Length; i++)
            {
                var rank = Math.Max(0, Math.Min(entries[i].GroupRank, 63));
                keys[i] = ((long)orderRank[entries[i].GroupOrder] << 48) | ((long)nameRank[entries[i].Group ?? ""] << 27) | ((long)rank << 21) | (long)i;
            }
            Array.Sort(keys, entries);
            return new List<Entry>(entries);
        }

        private void Refresh()
        {
            Dictionary<string, int> order = null;
            if (_recentOnly)
            {
                order = new Dictionary<string, int>();
                for (var i = 0; i < _recent.Count; i++) order[_recent[i]] = i;
            }
            else if (_learnedOnly && _view != null)
            {
                // Newest first, what has no day (caught up, or kept before days were) after the rest.
                order = new Dictionary<string, int>();
                var book = _view.Book;
                foreach (var key in _view.Lately) if (!order.ContainsKey(key) && (_view.NewsOf(key)?.Day ?? -1) >= 0) order[key] = order.Count;
                foreach (var key in _view.Lately) if (!order.ContainsKey(key)) order[key] = order.Count;
            }

            // One pass over the catalog lists what matches and counts it by kind for the chips,
            // across every kind, so picking one still shows what the others hold.
            // Peeking, the list stays as the character knows it; only a page shows whole.
            var view = CompendiumSet ? _view : null;
            Array.Clear(_counts, 0, _counts.Length);
            _results = Search.Run(_catalog, _query, _favourites.Keys, order?.Keys, _learnedOnly ? null : _counts, view, PeekingEvery);
            // The learned list is a tab of its own: the others count what each would show.
            if (_learnedOnly) Search.Run(_catalog, _query, _favourites.Keys, null, _counts, view, PeekingEvery);
            _countAll = 0;
            foreach (var count in _counts) _countAll += count;
            // The learned tab counts what the search finds among what was learned, as each kind's tab
            // counts what it finds of its kind, whichever tab is shown.
            _learnedCount = 0;
            if (CompendiumSet)
            {
                _learned.Clear();
                foreach (var key in _view.Lately)
                {
                    var entry = Find(key);
                    if (entry != null) _learned.Add(entry);
                }
                Array.Clear(_learnedCounts, 0, _learnedCounts.Length);
                Search.Run(_learned, _query, _favourites.Keys, null, _learnedCounts, view, PeekingEvery);
                foreach (var count in _learnedCounts) _learnedCount += count;
            }

            // Nothing found at all: a word one slip from a name's word is read as that word, if
            // that finds anything; the list says so.
            var query = _query;
            _mendedText = null;
            _mended.Clear();
            if (_countAll == 0 && !string.IsNullOrEmpty(_query.Text))
            {
                // Only names the character knows are offered for a slip.
                var named = (IReadOnlyList<Entry>)_visible ?? _catalog;
                if (_slipWords == null) _slipWords = SearchSlip.Words(named);
                var mended = SearchSlip.Mend(_query.Text, named, _slipWords, _mended);
                if (mended != null)
                {
                    _mendedQuery.Text = mended;
                    _mendedQuery.Kind = _query.Kind;
                    _mendedQuery.FavouritesOnly = _query.FavouritesOnly;
                    _mendedQuery.Origin = _query.Origin;
                    _results = Search.Run(_catalog, _mendedQuery, _favourites.Keys, order?.Keys, _counts, view);
                    foreach (var count in _counts) _countAll += count;
                    if (_countAll > 0)
                    {
                        query = _mendedQuery;
                        _mendedText = mended;
                    }
                    else _mended.Clear();
                }
            }

            // A picked kind with nothing to show while others have matches lists those instead,
            // so typing never ends on an empty list with the matches a tab away.
            _showingEveryKind = _results.Count == 0 && query.Kind != null && _countAll > 0;
            if (_showingEveryKind)
            {
                var kind = query.Kind;
                query.Kind = null;
                _results = Search.Run(_catalog, query, _favourites.Keys, order?.Keys, null, view);
                query.Kind = kind;
            }

            _underOwner.Clear();

            // Newest first, whatever the search ranking would be.
            if (order != null) _results.Sort((a, b) => order[a.Key].CompareTo(order[b.Key]));

            // Within a kind's tab, by its groups (resources by how they are gathered), each
            // group in the order the search gave it.
            else if (_query.Kind != null && !_showingEveryKind) _results = Leftovers.UnderOwners(ByGroup(_results), _underOwner);

            if (_selected == null) return;

            _selectedIndex = _results.IndexOf(_selected);
            // While peeking, a page reached by a link stays, listed or not.
            if (_selectedIndex < 0 && !_peeking) Select(null, false);
        }
    }
}
