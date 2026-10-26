using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// What a tame creature's birth brought, as the game spawns it (<c>Procreation.Procreate</c>:
    /// its offspring, or its offspring without a partner): a creature by the newest creature in
    /// the world being of its kind; an item, as the egg a hen lays, by its kind where it is the
    /// only item that could have come. None where it cannot be told.
    /// </summary>
    internal static class BirthYoung
    {
        /// <summary>The young named, from the newest creature's prefab and the kinds the birth could bring, each told whether it is a creature.</summary>
        public static string Named(string newestCreature, IEnumerable<(string Name, bool IsCreature)> kinds)
        {
            var named = (kinds ?? Enumerable.Empty<(string, bool)>()).Where(k => !string.IsNullOrEmpty(k.Name)).ToList();
            if (newestCreature != null && named.Any(k => k.IsCreature && k.Name == newestCreature)) return newestCreature;
            var items = named.Where(k => !k.IsCreature).Select(k => k.Name).Distinct(StringComparer.Ordinal).ToList();
            return items.Count == 1 ? items[0] : null;
        }
    }
}
