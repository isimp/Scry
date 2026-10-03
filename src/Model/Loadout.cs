using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The gear a creature rolls when it spawns, laid out for choosing. The game gives such a
    /// creature one weapon, one shield and one armour, each picked at random from its own list,
    /// and each of a few extras by chance, one per kind of item. Here each list is a row to pick
    /// from, starting on its first entry, and each extra can be put on or taken off. A creature
    /// carrying several weapons of its own (a troll's log, slap and throw) holds one at a time,
    /// as its AI picks them in a fight; which one is a row too.
    ///
    /// Names stand for item prefabs. An empty entry in a list is the game's way of letting the
    /// creature roll nothing for that row, and is offered as nothing; an item listed more than once
    /// to make it commoner is offered once.
    /// </summary>
    internal sealed class Loadout
    {
        /// <summary>What can be chosen: a weapon, shield and armour rolled from lists, and which of the weapons it always carries it holds.</summary>
        public enum Row { Weapon, Shield, Armour, Holding }

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

        private readonly List<string>[] _options = new List<string>[4];
        private readonly int[] _chosen = new int[4];
        private readonly Extra[] _extras;
        private readonly bool[] _on;
        private readonly bool[] _onFirst;
        private readonly HashSet<string> _bothHands;
        private List<string> _setWeapons = new List<string>();

        /// <summary>
        /// The weapons of the gear set the creature rolled, shown in its look: they are among what
        /// it may hold, as those it always carries are. Another set's take their place.
        /// </summary>
        public void Carrying(IEnumerable<string> setWeapons)
        {
            var weapons = Distinct(setWeapons).Where(w => w.Length > 0).ToList();
            if (weapons.SequenceEqual(_setWeapons)) return;
            _setWeapons = weapons;
            _chosen[(int)Row.Holding] = 0;
        }

        /// <summary>
        /// A loadout from the lists a creature rolls from. Weapons held in both hands (bows,
        /// two-handed weapons) leave no hand for a shield, as equipping one takes the shield off.
        /// </summary>
        public Loadout(IEnumerable<string> weapons, IEnumerable<string> shields, IEnumerable<string> armours, IEnumerable<Extra> extras,
            IEnumerable<string> bothHands = null, IEnumerable<string> holding = null)
        {
            _bothHands = new HashSet<string>(bothHands ?? System.Array.Empty<string>());
            _options[(int)Row.Weapon] = Distinct(weapons);
            _options[(int)Row.Shield] = Distinct(shields);
            _options[(int)Row.Armour] = Distinct(armours);
            _options[(int)Row.Holding] = Distinct(holding);

            _extras = new List<Extra>(extras ?? System.Array.Empty<Extra>()).ToArray();
            _on = new bool[_extras.Length];
            var kinds = new HashSet<int>();
            for (var i = 0; i < _extras.Length; i++) _on[i] = kinds.Add(_extras[i].Kind);
            _onFirst = (bool[])_on.Clone();
        }

        /// <summary>What a row offers; an empty name stands for nothing.</summary>
        public IReadOnlyList<string> Options(Row row) => row == Row.Holding ? Holdable() : _options[(int)row];

        /// <summary>
        /// The weapons it may hold: the one it rolled, first, and those it always carries. Any of
        /// them is in its hand at a time, as its AI picks it.
        /// </summary>
        private List<string> Holdable()
        {
            var weapons = new List<string>();
            var rolled = _options[(int)Row.Weapon];
            if (rolled.Count > 0 && rolled[_chosen[(int)Row.Weapon]].Length > 0) weapons.Add(rolled[_chosen[(int)Row.Weapon]]);
            foreach (var own in _options[(int)Row.Holding]) if (!weapons.Contains(own)) weapons.Add(own);
            foreach (var set in _setWeapons) if (!weapons.Contains(set)) weapons.Add(set);
            return weapons;
        }

        /// <summary>Whether a row has more than one thing to choose from.</summary>
        public bool Offered(Row row) => Options(row).Count > 1;

        /// <summary>Whether there is anything to choose at all.</summary>
        public bool HasChoices => Offered(Row.Weapon) || Offered(Row.Shield) || Offered(Row.Armour) || Offered(Row.Holding) || _extras.Length > 0;

        public int Chosen(Row row) => row == Row.Holding ? System.Math.Min(_chosen[(int)row], System.Math.Max(0, Holdable().Count - 1)) : _chosen[(int)row];

        /// <summary>The weapon in its hand, or empty when it holds none.</summary>
        private string InHand()
        {
            var weapons = Holdable();
            return weapons.Count > 0 ? weapons[Chosen(Row.Holding)] : "";
        }

        /// <summary>
        /// Whether what is chosen in a row is worn: a shield is not while the chosen weapon takes
        /// both hands. The choice is kept for when a hand is free again.
        /// </summary>
        public bool Held(Row row)
        {
            return row != Row.Shield || !_bothHands.Contains(InHand());
        }

        public void Choose(Row row, int index)
        {
            if (index < 0 || index >= Options(row).Count) return;
            _chosen[(int)row] = index;
        }

        /// <summary>Whether anything differs from what the creature rolls first, so there is something to reset.</summary>
        public bool Changed => _chosen.Any(c => c != 0) || !_on.SequenceEqual(_onFirst);

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
            var hand = InHand();
            if (hand.Length > 0) worn.Add(hand);
            foreach (var row in new[] { Row.Shield, Row.Armour })
            {
                var options = _options[(int)row];
                if (options.Count == 0 || !Held(row)) continue;
                var name = options[_chosen[(int)row]];
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
