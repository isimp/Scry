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
    /// telling once would, and a place no longer named goes by what it went by as made. It is
    /// worked out a slice at a time (<see cref="Steps"/>), the catalog left as it was meanwhile,
    /// and put on it at once (<see cref="Publish"/>), so a telling spread over frames shows no
    /// half of it. One is kept for a catalog; a new world's catalog wants a new one.
    /// </summary>
    internal sealed class PlaceFinds
    {
        /// <summary>How many things found are ordered in one step.</summary>
        private const int Slice = 400;

        /// <summary>The groups of the entries as the catalog made them, before the places were told.</summary>
        private readonly Dictionary<Entry, (string Group, int Order)> _made = new Dictionary<Entry, (string, int)>();

        /// <summary>What each place entry went by as the catalog made it, before its place was read: told again where it is no longer named.</summary>
        private readonly Dictionary<Entry, (string[] FoundIn, string Name)> _madePlaces = new Dictionary<Entry, (string[], string)>();

        // The telling worked out and not yet put on the catalog.
        private IReadOnlyList<Entry> _catalog;
        private IReadOnlyDictionary<(string Prefab, bool Room), string[]> _labels;
        private Func<Entry, (string Prefab, bool Room)?> _placeOf;
        private Dictionary<string, string[]> _ordered;

        /// <summary>
        /// Tells <paramref name="found"/> (what each prefab is found in) and <paramref name="labels"/>
        /// (what each place goes by, by its prefab and whether a room; <paramref name="placeOf"/>
        /// tells which entries are places) on the catalog at once; returns how many other entries are
        /// found in a place, and how many went under "In locations".
        /// </summary>
        public (int InCatalog, int Moved) Tell(IReadOnlyList<Entry> catalog, IDictionary<string, HashSet<string>> found,
            IReadOnlyDictionary<(string Prefab, bool Room), string[]> labels, Func<Entry, (string Prefab, bool Room)?> placeOf)
        {
            foreach (var _ in Steps(catalog, found, labels, placeOf))
            {
            }
            return Publish();
        }

        /// <summary>
        /// Works out a telling (as <see cref="Tell"/>) a slice at a time, leaving the catalog as it
        /// is; <see cref="Publish"/> puts it on. A telling worked out takes the place of one not yet put on.
        /// </summary>
        public IEnumerable<bool> Steps(IReadOnlyList<Entry> catalog, IDictionary<string, HashSet<string>> found,
            IReadOnlyDictionary<(string Prefab, bool Room), string[]> labels, Func<Entry, (string Prefab, bool Room)?> placeOf)
        {
            _catalog = null;
            // Each place's biome rank, from the locations as they will go by the names read:
            // where things are found is ordered by the biome of the location going by each.
            var ranks = Places.Ranks(catalog, entry => Named(entry, labels, placeOf) ?? (_madePlaces.TryGetValue(entry, out var made) ? made.FoundIn : entry.FoundIn));
            yield return true;

            var ordered = new Dictionary<string, string[]>(found.Count, StringComparer.Ordinal);
            var done = 0;
            foreach (var pair in found)
            {
                if (pair.Value.Count > 0) ordered[pair.Key] = Places.Ordered(pair.Value, ranks);
                if (++done % Slice == 0) yield return true;
            }

            _catalog = catalog;
            _labels = labels;
            _placeOf = placeOf;
            _ordered = ordered;
        }

        /// <summary>The names a place entry is to go by, as read; null for any other entry or a place read to have none.</summary>
        private static string[] Named(Entry entry, IReadOnlyDictionary<(string Prefab, bool Room), string[]> labels, Func<Entry, (string Prefab, bool Room)?> placeOf)
        {
            var place = placeOf(entry);
            return place != null && labels.TryGetValue(place.Value, out var named) && named != null && named.Length > 0 ? named : null;
        }

        /// <summary>Puts the telling worked out on the catalog; returns how many other entries are found in a place, and how many went under "In locations". Nothing worked out, nothing changes.</summary>
        public (int InCatalog, int Moved) Publish()
        {
            if (_catalog == null) return (0, 0);
            var inCatalog = 0;
            var moved = 0;
            foreach (var entry in _catalog)
            {
                var place = _placeOf(entry);
                if (place != null)
                {
                    if (_madePlaces.TryGetValue(entry, out var made))
                    {
                        entry.FoundIn = made.FoundIn;
                        entry.DisplayName = made.Name;
                    }
                    else _madePlaces[entry] = (entry.FoundIn, entry.DisplayName);
                    if (_labels.TryGetValue(place.Value, out var named)) Places.Named(entry, named, place.Value.Room);
                }
                else
                {
                    if (_made.TryGetValue(entry, out var made))
                    {
                        entry.Group = made.Group;
                        entry.GroupOrder = made.Order;
                    }
                    else _made[entry] = (entry.Group, entry.GroupOrder);
                    entry.FoundIn = Array.Empty<string>();
                }

                if (!EntryKeys.HasOwnNamespace(entry.Kind) && _ordered.TryGetValue(entry.Name, out var places))
                {
                    entry.FoundIn = (string[])places.Clone();
                    Places.FoundOnlyThere(entry);
                }

                if (place != null) continue;
                if (entry.FoundIn.Length > 0) inCatalog++;
                if (entry.Group != _made[entry].Group) moved++;
            }
            _catalog = null;
            _labels = null;
            _placeOf = null;
            _ordered = null;
            return (inCatalog, moved);
        }
    }
}
