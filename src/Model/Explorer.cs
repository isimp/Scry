using System;
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

        /// <summary>What the list shows now, best match first.</summary>
        public IReadOnlyList<Entry> Results => _results;

        /// <summary>How many of what the text and the other filters let through are of this kind.</summary>
        public int CountOf(Kind kind) => _counts[(int)kind];

        /// <summary>How many the text and the other filters let through, all kinds together.</summary>
        public int CountAll => _countAll;

        public Entry Selected => _selected;

        /// <summary>Position of the selection in <see cref="Results"/>, or -1.</summary>
        public int SelectedIndex => _selectedIndex;

        /// <summary>Changes every time a different entry is selected, so a view can tell.</summary>
        public int SelectionVersion => _selectionVersion;

        public void Select(Entry entry)
        {
            if (entry == _selected) return;

            _selected = entry;
            _selectedIndex = entry == null ? -1 : _results.IndexOf(entry);
            _selectionVersion++;
            _modifiers.ResetFor(entry);
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

        public void ToggleFavourite(Entry entry)
        {
            _favourites.Toggle(entry);
            if (_query.FavouritesOnly) Refresh();
        }

        private void Refresh()
        {
            _results = Search.Run(_catalog, _query, _favourites.Keys);

            // The chips count across every kind, so picking one still shows what the others hold.
            Array.Clear(_counts, 0, _counts.Length);
            _countAll = 0;
            var words = Search.Words(_query.Text);
            var anyKind = new Query { Text = _query.Text, FavouritesOnly = _query.FavouritesOnly, Origin = _query.Origin };
            foreach (var entry in _catalog)
            {
                if (!Search.Passes(entry, anyKind, _favourites.Keys)) continue;
                if (words.Length > 0 && !Search.Matches(entry, _query.Text)) continue;
                _counts[(int)entry.Kind]++;
                _countAll++;
            }

            if (_selected == null) return;

            _selectedIndex = _results.IndexOf(_selected);
            if (_selectedIndex < 0) Select(null);
        }
    }
}
