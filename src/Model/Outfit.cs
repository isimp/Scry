using System.Collections.Generic;

namespace Scry
{
    /// <summary>Where on a character an item is worn.</summary>
    public enum Slot
    {
        None,
        RightHand,
        LeftHand,
        BothHands,
        Head,
        Chest,
        Legs,
        Shoulders,
        Utility,
    }

    /// <summary>
    /// Items kept on the try-on person while browsing, one per slot. The selected item is tried
    /// on over them, taking the place of whatever shares its slot, without changing the outfit.
    /// A two-handed weapon takes both hands, so it and anything held in either hand replace
    /// each other.
    /// </summary>
    public sealed class Outfit
    {
        private readonly List<KeyValuePair<string, Slot>> _items = new List<KeyValuePair<string, Slot>>();

        public IReadOnlyList<string> Keys => _items.ConvertAll(i => i.Key);

        public bool Contains(string key) => _items.Exists(i => i.Key == key);

        public void Keep(string key, Slot slot)
        {
            if (slot == Slot.None || string.IsNullOrEmpty(key)) return;
            _items.RemoveAll(i => i.Key == key || Clash(i.Value, slot));
            _items.Add(new KeyValuePair<string, Slot>(key, slot));
        }

        public void TakeOff(string key)
        {
            _items.RemoveAll(i => i.Key == key);
        }

        /// <summary>What the person wears with this item tried on: the outfit, less what it replaces, and the item.</summary>
        public IEnumerable<string> With(string key, Slot slot)
        {
            foreach (var item in _items)
            {
                if (item.Key == key || (slot != Slot.None && Clash(item.Value, slot))) continue;
                yield return item.Key;
            }
            if (!string.IsNullOrEmpty(key)) yield return key;
        }

        private static bool Clash(Slot a, Slot b)
        {
            if (a == b) return true;
            bool Hand(Slot s) => s == Slot.RightHand || s == Slot.LeftHand || s == Slot.BothHands;
            return (a == Slot.BothHands && Hand(b)) || (b == Slot.BothHands && Hand(a));
        }
    }
}
