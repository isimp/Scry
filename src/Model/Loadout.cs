using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The gear a creature rolls when it spawns, laid out for choosing. The game gives such a
    /// creature one weapon, one shield and one armour, each picked at random from its own list,
    /// and each of a few extras by chance, one per kind of item. Here each list is a row to pick
    /// from, starting on its first entry, and each extra can be put on or taken off.
    ///
    /// Names stand for item prefabs. An empty entry in a list is the game's way of letting the
    /// creature roll nothing for that row, and is offered as nothing; an item listed more than once
    /// to make it commoner is offered once.
    /// </summary>
    public sealed class Loadout
    {
        public enum Row { Weapon, Shield, Armour }

        /// <summary>An item a creature may be given by chance, and the kind of item it is.</summary>
        public struct Extra
        {
            public string Name;
            public int Kind;

            public Extra(string name, int kind)
            {
                Name = name;
                Kind = kind;
            }
        }

        private readonly List<string>[] _options = new List<string>[3];
        private readonly int[] _chosen = new int[3];
        private readonly Extra[] _extras;
        private readonly bool[] _on;

        public Loadout(IEnumerable<string> weapons, IEnumerable<string> shields, IEnumerable<string> armours, IEnumerable<Extra> extras)
        {
            _options[(int)Row.Weapon] = Distinct(weapons);
            _options[(int)Row.Shield] = Distinct(shields);
            _options[(int)Row.Armour] = Distinct(armours);

            _extras = new List<Extra>(extras ?? new Extra[0]).ToArray();
            _on = new bool[_extras.Length];
            var kinds = new HashSet<int>();
            for (var i = 0; i < _extras.Length; i++) _on[i] = kinds.Add(_extras[i].Kind);
        }

        /// <summary>What a row offers; an empty name stands for nothing.</summary>
        public IReadOnlyList<string> Options(Row row) => _options[(int)row];

        /// <summary>Whether a row has more than one thing to choose from.</summary>
        public bool Offered(Row row) => _options[(int)row].Count > 1;

        /// <summary>Whether there is anything to choose at all.</summary>
        public bool HasChoices => Offered(Row.Weapon) || Offered(Row.Shield) || Offered(Row.Armour) || _extras.Length > 0;

        public int Chosen(Row row) => _chosen[(int)row];

        public void Choose(Row row, int index)
        {
            if (index < 0 || index >= _options[(int)row].Count) return;
            _chosen[(int)row] = index;
        }

        public IReadOnlyList<Extra> Extras => _extras;

        public bool ExtraOn(int index) => index >= 0 && index < _on.Length && _on[index];

        /// <summary>Puts an extra on, taking off any other of its kind, or takes it off.</summary>
        public void ToggleExtra(int index)
        {
            if (index < 0 || index >= _on.Length) return;
            if (_on[index])
            {
                _on[index] = false;
                return;
            }
            for (var i = 0; i < _extras.Length; i++)
            {
                if (_extras[i].Kind == _extras[index].Kind) _on[i] = false;
            }
            _on[index] = true;
        }

        /// <summary>What the creature wears: the chosen weapon, shield and armour, then the extras put on.</summary>
        public List<string> Worn()
        {
            var worn = new List<string>();
            for (var row = 0; row < _options.Length; row++)
            {
                var options = _options[row];
                if (options.Count == 0) continue;
                var name = options[_chosen[row]];
                if (name.Length > 0) worn.Add(name);
            }
            for (var i = 0; i < _extras.Length; i++)
            {
                if (_on[i] && !string.IsNullOrEmpty(_extras[i].Name)) worn.Add(_extras[i].Name);
            }
            return worn;
        }

        private static List<string> Distinct(IEnumerable<string> names)
        {
            var list = new List<string>();
            if (names == null) return list;
            foreach (var name in names)
            {
                var n = name ?? "";
                if (!list.Contains(n)) list.Add(n);
            }
            return list;
        }
    }
}
