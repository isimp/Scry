using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What marks an item among the loot a page lists as worth having beyond its odds (<see cref="DropWords.OnlyHereMark"/>, <see cref="DropWords.WorthMark"/>).</summary>
    internal static class LootMarks
    {
        /// <summary>
        /// Whether nothing else in the world gives it: every prefab known to give it is among
        /// what is here (the creature, the chest, a dungeon and the chests in its rooms), and no
        /// station makes it nor the world places it.
        /// </summary>
        /// <param name="givers">The prefabs known to give it.</param>
        /// <param name="madeOrPlaced">A station makes it, the world places it, or a place elsewhere holds it.</param>
        /// <param name="here">The page's own prefab and what it holds.</param>
        public static bool OnlyHere(IReadOnlyCollection<string> givers, bool madeOrPlaced, ICollection<string> here) =>
            !madeOrPlaced && givers.Count > 0 && givers.All(here.Contains);
    }
}
