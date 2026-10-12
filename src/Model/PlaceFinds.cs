using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What the places hold, told on the catalog's entries each time they are read or found kept:
    /// each location and room goes by the names its place was read to have, every other entry is
    /// found in the places naming it, and an effect, sound or projectile only places play or fire
    /// goes under "In locations" (<see cref="Places.Apply"/>). Each telling starts from the
    /// catalog as it was made, each entry's group as it was before the places were first told and
    /// found nowhere, so telling again, with what a step after it regrouped, leaves the catalog as
    /// telling once would. One is kept for a catalog; a new world's catalog wants a new one.
    /// </summary>
    internal sealed class PlaceFinds
    {
        /// <summary>The groups of the entries as the catalog made them, before the places were told.</summary>
        private readonly Dictionary<Entry, (string Group, int Order)> _made = new Dictionary<Entry, (string, int)>();

        /// <summary>
        /// Tells <paramref name="found"/> (what each prefab is found in) and <paramref name="labels"/>
        /// (what each place goes by, by its prefab and whether a room; <paramref name="placeOf"/>
        /// tells which entries are places) on the catalog; returns how many other entries are found
        /// in a place, and how many went under "In locations".
        /// </summary>
        public (int InCatalog, int Moved) Tell(IReadOnlyList<Entry> catalog, IDictionary<string, HashSet<string>> found,
            IReadOnlyDictionary<(string Prefab, bool Room), string[]> labels, Func<Entry, (string Prefab, bool Room)?> placeOf)
        {
            foreach (var entry in catalog)
            {
                // Each place goes by its name first: where things are found is ordered by the biome of the location going by each.
                var place = placeOf(entry);
                if (place != null)
                {
                    if (labels.TryGetValue(place.Value, out var named)) Places.Named(entry, named, place.Value.Room);
                    continue;
                }
                if (_made.TryGetValue(entry, out var made))
                {
                    entry.Group = made.Group;
                    entry.GroupOrder = made.Order;
                }
                else _made[entry] = (entry.Group, entry.GroupOrder);
                entry.FoundIn = Array.Empty<string>();
            }

            Places.Apply(catalog, found);
            var inCatalog = 0;
            var moved = 0;
            foreach (var entry in catalog)
            {
                if (!_made.TryGetValue(entry, out var made)) continue;
                if (entry.FoundIn.Length > 0) inCatalog++;
                if (entry.Group != made.Group) moved++;
            }
            return (inCatalog, moved);
        }
    }
}
