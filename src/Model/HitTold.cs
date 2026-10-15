using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// What the game's popup on a hit tells of a creature, for the compendium's strict rules: it
    /// shows one kind of landing, the one most of the hit's damage took (plain, resisted, weak or
    /// none taken; <c>HitData.ApplyResistance</c>'s significant modifier), so it tells how each
    /// type carrying damage that landed so is taken.
    /// </summary>
    internal static class HitTold
    {
        /// <summary>The damage types a hit's popup told how the creature takes, of the hit's types with their damage and how the creature takes each.</summary>
        public static List<string> Types(IEnumerable<(string Type, float Damage, Degree Degree)> hit, Degree shown) =>
            hit.Where(part => part.Damage > 0f && part.Degree != Degree.Ignore && Landing(part.Degree) == Landing(shown)).Select(part => part.Type).ToList();

        /// <summary>The kind of landing a degree is, as the game sums a hit's damage by it: plain, resisted, weak or none taken.</summary>
        private static int Landing(Degree degree)
        {
            switch (degree)
            {
                case Degree.SlightlyResistant:
                case Degree.Resistant:
                case Degree.VeryResistant:
                    return 1;
                case Degree.SlightlyWeak:
                case Degree.Weak:
                case Degree.VeryWeak:
                    return 2;
                case Degree.Immune:
                    return 3;
                default:
                    return 0;
            }
        }
    }
}
