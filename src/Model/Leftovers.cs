using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>Something a prefab leaves behind, found in the game: its name, whose it is, and what it is to it.</summary>
    public struct Leftover
    {
        public string Name;
        public string Owner;

        /// <summary>What it is to its owner, such as "ragdoll", "log" or "debris".</summary>
        public string Role;

        public Leftover(string name, string owner, string role)
        {
            Name = name ?? "";
            Owner = owner ?? "";
            Role = role ?? "";
        }
    }

    /// <summary>
    /// Pairs what prefabs leave behind (a creature's ragdoll, a tree's log and stump, a wall's
    /// debris) with them. These would otherwise stand among the unsorted others under their bare
    /// prefab names. Paired, one is named after its owner ("Troll · ragdoll") and takes its
    /// owner's kind, so it sits beside it in the list and a search for the owner finds it, and
    /// each knows the other. A leftover shared by owners of different names (the debris of every
    /// wooden piece) is named for what it is and how many leave it; one shared by owners of
    /// different kinds stays where it was. Only what is listed as other is paired.
    /// </summary>
    public static class Leftovers
    {
        public static void Pair(IList<Entry> catalog, IEnumerable<Leftover> found)
        {
            var byName = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in catalog)
            {
                if (entry.Kind != Kind.StatusEffect && !byName.ContainsKey(entry.Name)) byName[entry.Name] = entry;
            }

            foreach (var group in found.GroupBy(f => f.Name))
            {
                if (!byName.TryGetValue(group.Key, out var leftover) || leftover.Kind != Kind.Other) continue;

                var owners = group.Select(f => f.Owner).Distinct()
                    .Where(o => o != group.Key && byName.ContainsKey(o))
                    .OrderBy(o => o, StringComparer.OrdinalIgnoreCase)
                    .Select(o => byName[o]).ToList();
                if (owners.Count == 0) continue;

                var role = group.First().Role;
                leftover.LeftBy = owners.Select(o => o.Name).ToList();
                foreach (var owner in owners) owner.LeavesBehind.Add(leftover.Name);

                var shown = owners.Select(o => string.IsNullOrEmpty(o.DisplayName) ? o.Name : o.DisplayName).Distinct().ToList();
                leftover.DisplayName = shown.Count == 1
                    ? shown[0] + " · " + role
                    : Capital(role) + " of " + owners.Count;

                var kinds = owners.Select(o => o.Kind).Distinct().ToList();
                if (kinds.Count == 1) leftover.Kind = kinds[0];
            }
        }

        private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
