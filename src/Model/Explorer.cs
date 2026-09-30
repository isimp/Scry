using System;
using System.Linq;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What the panel shows and what the player has picked: the search, the kind chips, the list,
    /// the selection and its modifiers. The panel draws this and hands it the player's input.
    /// </summary>
    public sealed class Explorer
    {
        private readonly IReadOnlyList<Entry> _catalog;
        private readonly Favourites _favourites;
        private readonly Modifiers _modifiers = new Modifiers();
        private readonly Query _query = new Query();
        private readonly int[] _counts = new int[Enum.GetValues(typeof(Kind)).Length];

        private List<Entry> _results = new List<Entry>();
        private int _countAll;
        private Entry _selected;
        private int _selectedIndex = -1;
        private int _selectionVersion;

        public Explorer(IReadOnlyList<Entry> catalog, Favourites favourites)
        {
            _catalog = catalog;
            _favourites = favourites;
            Refresh();
        }

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

        /// <summary>Only what was looked at recently, newest first.</summary>
        public bool RecentOnly
        {
            get => _recentOnly;
            set
            {
                if (value == _recentOnly) return;
                _recentOnly = value;
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
            _query.Origin = OriginFilter.All;
            _query.Text = text ?? "";
            Refresh();
        }

        /// <summary>What the list shows now, best match first.</summary>
        public IReadOnlyList<Entry> Results => _results;

        /// <summary>How many of what the text and the other filters let through are of this kind.</summary>
        public int CountOf(Kind kind) => _counts[(int)kind];

        /// <summary>How many the text and the other filters let through, all kinds together.</summary>
        public int CountAll => _countAll;

        /// <summary>
        /// The picked kind has nothing the text and the other filters let through while other
        /// kinds do, so the list shows those, as the All tab would; the kind stays picked and
        /// the list goes back to it once it has matches again.
        /// </summary>
        public bool ShowingEveryKind => _showingEveryKind;

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

            _selected = entry;
            _selectedIndex = entry == null ? -1 : _results.IndexOf(entry);
            _selectionVersion++;
            _modifiers.ResetFor(entry);

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
            var name = EntryKeys.Split(key, out var kind);
            Entry target = null;
            foreach (var entry in _catalog)
            {
                var matches = kind != null ? entry.Kind == kind : !EntryKeys.HasOwnNamespace(entry.Kind);
                if (matches && entry.Name == name)
                {
                    target = entry;
                    break;
                }
            }
            if (target == null) return false;
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
            public OriginFilter Origin;
            public Entry Selected;
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
            into.Add(_results.Contains(_here.Selected) ? Here(_here.Selected) : _here);
            if (into.Count > HistoryLimit) into.RemoveAt(0);
        }

        private Place Here(Entry selected) => new Place
        {
            Text = _query.Text,
            Kind = _query.Kind,
            FavouritesOnly = _query.FavouritesOnly,
            RecentOnly = _recentOnly,
            Origin = _query.Origin,
            Selected = selected,
        };

        // ----- Keeping and putting back -----

        /// <summary>Keeps where the explorer is whole, to be put back with <see cref="Restore"/>.</summary>
        public Kept Keep() => new Kept(this);

        /// <summary>Puts the explorer back where it was kept: search, filters, selection, recent entries and history.</summary>
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
            private readonly bool _favouritesOnly, _recentOnly;
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

        /// <summary>Lists the results again after entries changed their groups (what the locations were found to hold).</summary>
        public void Regrouped() => Refresh();

        /// <summary>
        /// The results by their groups, the groups in their order and then by name, each group
        /// keeping the order the search gave it. Sorted as numbers: a group's place and each
        /// result's place in one, so a tab of thousands is sorted with no call per comparison.
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
                keys[i] = ((long)orderRank[entries[i].GroupOrder] << 42) | ((long)nameRank[entries[i].Group ?? ""] << 21) | (long)i;
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

            // One pass over the catalog lists what matches and counts it by kind for the chips,
            // across every kind, so picking one still shows what the others hold.
            Array.Clear(_counts, 0, _counts.Length);
            _results = Search.Run(_catalog, _query, _favourites.Keys, order?.Keys, _counts);
            _countAll = 0;
            foreach (var count in _counts) _countAll += count;

            // A picked kind with nothing to show while others have matches lists those instead,
            // so typing never ends on an empty list with the matches a tab away.
            _showingEveryKind = _results.Count == 0 && _query.Kind != null && _countAll > 0;
            if (_showingEveryKind)
            {
                var kind = _query.Kind;
                _query.Kind = null;
                _results = Search.Run(_catalog, _query, _favourites.Keys, order?.Keys, null);
                _query.Kind = kind;
            }

            // Newest first, whatever the search ranking would be.
            if (order != null) _results.Sort((a, b) => order[a.Key].CompareTo(order[b.Key]));

            // Within a kind's tab, by its groups (resources by how they are gathered), each
            // group in the order the search gave it.
            else if (_query.Kind != null && !_showingEveryKind) _results = ByGroup(_results);

            if (_selected == null) return;

            _selectedIndex = _results.IndexOf(_selected);
            if (_selectedIndex < 0) Select(null, false);
        }
    }
}
