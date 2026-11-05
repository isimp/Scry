using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Which looks of a creature rolling one of several sets an attack comes with
    /// (<see cref="Entry.GearSets"/>): it fights with what its set gives it, so its attacks table
    /// lists the attacks of the look chosen, and every one at "No gear", which stands for no set
    /// in particular.
    /// </summary>
    internal static class GearLooks
    {
        /// <summary>The looks whose set gives an attack's item, each set's look its place among them after "No gear"; none where no set does, as it is then given whatever is rolled, or the creature rolls no set.</summary>
        public static int[] Of(string item, IReadOnlyList<string[]> sets)
        {
            if (sets == null) return null;
            List<int> looks = null;
            for (var i = 0; i < sets.Count; i++)
            {
                if (System.Array.IndexOf(sets[i], item) < 0) continue;
                (looks = looks ?? new List<int>()).Add(i + 1);
            }
            return looks?.ToArray();
        }

        /// <summary>Whether an attack coming with those looks (<see cref="Of"/>) is listed at the look chosen: always at "No gear", and always for one no set gives.</summary>
        public static bool Shown(int[] looks, int look) => looks == null || look <= 0 || System.Array.IndexOf(looks, look) >= 0;
    }
}
