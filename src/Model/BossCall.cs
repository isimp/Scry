using System.Collections.Generic;

namespace Scry
{
    /// <summary>What a boss called at its altar ties together, for the compendium's strict rules.</summary>
    internal static class BossCall
    {
        /// <summary>
        /// The pairs a call ties: the altar's stone and the offering each with the boss and with
        /// each other, and the boss and the offering with the place the altar stands in, which a
        /// summoning chain names as its altar's step; any of them null is left out.
        /// </summary>
        public static IEnumerable<(string, string)> Pairs(string stone, string offering, string boss, string place)
        {
            if (boss == null) yield break;
            if (stone != null) yield return (stone, boss);
            if (offering != null) yield return (offering, boss);
            if (stone != null && offering != null) yield return (stone, offering);
            if (place == null) yield break;
            yield return (boss, place);
            if (offering != null) yield return (offering, place);
        }
    }
}
