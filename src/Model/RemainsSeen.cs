using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// A creature's remains seen on any game, whichever game ran its death: the ragdoll the game
    /// makes for every player, carrying the creature's level and what it drops. Names the
    /// creature only where one creature alone leaves those remains, as the remains name no
    /// creature themselves.
    /// </summary>
    internal static class RemainsSeen
    {
        /// <summary>The keys a ragdoll's drop list is stored under, one pair per drop, numbered from 0 (<c>Ragdoll.Setup</c>).</summary>
        public const string DropHash = "drop_hash";
        public const string DropAmount = "drop_amount";

        /// <summary>The key of the drop numbered so, as the game writes it.</summary>
        public static string DropKey(string key, int number) => key + Stored.Count(number);

        /// <summary>The one creature leaving the remains given, by what the catalog found leaves them; none where none or several do.</summary>
        public static string CreatureOf(string remains, Func<string, IReadOnlyList<string>> leftBy)
        {
            if (string.IsNullOrEmpty(remains)) return null;
            var owners = leftBy(remains);
            return owners != null && owners.Count == 1 ? owners[0] : null;
        }
    }
}
