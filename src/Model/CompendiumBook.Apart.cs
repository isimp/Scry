using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>What the game counted as the character's making that was none of it: food taken off a station this game ran by another player.</summary>
    internal sealed partial class CompendiumBook
    {
        /// <summary>How often the game counted an item as made by the character for another player's taking it off a station this game ran (<see cref="CreditedApart"/>), by the item's name token.</summary>
        private readonly Dictionary<string, int> _apart = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// The game counted an item as made by the character, by its name token, for another
        /// player's taking it off a station whose ground this game ran: none of the character's
        /// own making, so the catch-up leaves it out where that is all the count holds.
        /// </summary>
        public void CreditedApart(string token)
        {
            if (string.IsNullOrEmpty(token)) return;
            _apart[token] = (_apart.TryGetValue(token, out var so) ? so : 0) + 1;
            Changed = true;
        }

        /// <summary>How often the game counted an item so (<see cref="CreditedApart"/>).</summary>
        public int CreditedApartOf(string token) => token != null && _apart.TryGetValue(token, out var count) ? count : 0;
    }
}
