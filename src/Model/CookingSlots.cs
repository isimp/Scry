using System.Collections.Generic;

namespace Scry
{
    /// <summary>What a cooking station's slot holds, as the game keeps it for every game to draw: raw, cooked, or left too long.</summary>
    internal enum CookedState
    {
        NotDone,
        Done,
        Burnt,
    }

    /// <summary>
    /// Cooking stations' slots as last looked at, each by its station and number: tells a slot that
    /// turned done or burnt since, once. The first look at a slot only notes it, as food already
    /// done or burnt then was not seen turning.
    /// </summary>
    internal sealed class CookingSlots<T>
    {
        /// <summary>How many slots are remembered at most.</summary>
        public const int Kept = 256;

        private readonly Dictionary<(T Station, int Slot), (CookedState State, long Order)> _last = new Dictionary<(T, int), (CookedState, long)>();
        private long _looked;

        /// <summary>The key a station's data keeps a slot's state under, as the game names it.</summary>
        public static string StateKey(int slot) => "slotstatus" + Stored.Count(slot);

        /// <summary>The key a station's data keeps what a slot holds under, raw, cooked or burnt, as the game names it.</summary>
        public static string ItemKey(int slot) => "slot" + Stored.Count(slot);

        /// <summary>How many slots are remembered.</summary>
        public int Count => _last.Count;

        /// <summary>A slot looked at now: what it turned to, done or burnt, where it held something else at the last look; none otherwise.</summary>
        public CookedState? Turned(T station, int slot, CookedState now)
        {
            var key = (station, slot);
            var known = _last.TryGetValue(key, out var last);
            _last[key] = (now, ++_looked);
            if (!known)
            {
                if (_last.Count > Kept) _last.Remove(LongestUnseen());
                return null;
            }
            return now != last.State && now != CookedState.NotDone ? now : (CookedState?)null;
        }

        /// <summary>A station out of sight, with so many slots: forgotten, so what it holds when back in sight was not seen turning.</summary>
        public void Forget(T station, int slots)
        {
            for (var i = 0; i < slots; i++) _last.Remove((station, i));
        }

        private (T, int) LongestUnseen()
        {
            var oldest = default((T, int));
            var order = long.MaxValue;
            foreach (var pair in _last)
            {
                if (pair.Value.Order >= order) continue;
                order = pair.Value.Order;
                oldest = pair.Key;
            }
            return oldest;
        }
    }
}
