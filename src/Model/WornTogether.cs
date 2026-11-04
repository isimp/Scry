using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What one of a creature's sets surely puts on it, and the items of it whose sight tells that set was rolled (<see cref="WornTogether"/>).</summary>
    internal sealed class WornSet
    {
        public WornSet(IReadOnlyList<string> worn, IReadOnlyList<string> tells)
        {
            Worn = worn;
            Tells = tells;
        }

        /// <summary>The items the set surely puts on the creature's body, by their prefabs.</summary>
        public IReadOnlyList<string> Worn { get; }

        /// <summary>The items of <see cref="Worn"/> that nothing else the creature can be given holds: one seen worn tells this set.</summary>
        public IReadOnlyList<string> Tells { get; }
    }

    /// <summary>
    /// What a creature that rolled one of its sets wears with whatever of it was seen, as
    /// <c>Humanoid.GiveDefaultItems</c> hands out the set and equips every item of it but the
    /// weapons, which its AI picks in a fight, each taking the place of the one before it in its
    /// slot, and nothing takes off again: a Dvergr mage seen in its fire robe held its fire staff, a
    /// utility item. Only the body's slots count, as weapons may take the hands, and a slot the
    /// creature's chance items, handed out after the set, may fill is not sure. An item tells
    /// the set only where no other set and none of the creature's other lists hold it.
    /// </summary>
    internal static class WornTogether
    {
        /// <summary>Whether what a set puts in a slot stays there: the head, chest, legs, shoulders and utility, not the hands a weapon may take.</summary>
        public static bool IsSure(Slot slot) => slot == Slot.Head || slot == Slot.Chest || slot == Slot.Legs || slot == Slot.Shoulders || slot == Slot.Utility;

        /// <summary>
        /// The creature's sets as worn: each set's items in the order the game hands them out with
        /// their slots, the items its other lists hold (<paramref name="elsewhere"/>: what it
        /// always carries and its random weapon, armour, shield and chance items), and the slots its
        /// chance items may fill after the set. None for no sets.
        /// </summary>
        public static List<WornSet> Of(IReadOnlyList<IReadOnlyList<(string Item, Slot Slot)>> sets, IEnumerable<string> elsewhere, IEnumerable<Slot> laterSlots)
        {
            var worn = new List<WornSet>();
            if (sets == null) return worn;
            var other = new HashSet<string>(elsewhere ?? Array.Empty<string>(), StringComparer.Ordinal);
            var later = new HashSet<Slot>(laterSlots ?? Array.Empty<Slot>());
            foreach (var set in sets)
            {
                // Each slot keeps the last item handed out for it.
                var last = new Dictionary<Slot, int>();
                for (var i = 0; i < set.Count; i++) last[set[i].Slot] = i;
                var on = last.Where(kept => IsSure(kept.Key) && !later.Contains(kept.Key)).Select(kept => kept.Value).OrderBy(i => i).Select(i => set[i].Item).ToList();
                var tells = on.Where(item => !other.Contains(item) && !sets.Any(each => each != set && each.Any(e => e.Item == item))).ToList();
                worn.Add(new WornSet(on, tells));
            }
            return worn;
        }

        /// <summary>Whether an item was seen on a creature with what was seen worn (<paramref name="seenWorn"/>): one of its sets puts it on, and an item telling that set was seen.</summary>
        public static bool Seen(string item, IEnumerable<WornSet> sets, Func<string, bool> seenWorn)
        {
            if (sets == null || string.IsNullOrEmpty(item)) return false;
            foreach (var set in sets)
            {
                if (set.Worn.Contains(item) && set.Tells.Any(seenWorn)) return true;
            }
            return false;
        }
    }
}
