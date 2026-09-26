using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>One effect list a sound or effect is in: who plays it, what for, and what plays along.</summary>
    public sealed class EffectUse
    {
        /// <summary>The player of the list as shown, e.g. "Troll".</summary>
        public readonly string Owner;

        /// <summary>Where clicking the player goes, or null when it is nothing in the catalog (the interface).</summary>
        public readonly string OwnerKey;

        /// <summary>What the list is for, e.g. "Death".</summary>
        public readonly string Label;

        /// <summary>The prefabs the list plays, each once.</summary>
        public readonly string[] Members;

        /// <summary>The list itself, to play it.</summary>
        public readonly object List;

        public EffectUse(string owner, string ownerKey, string label, IEnumerable<string> members, object list)
        {
            Owner = owner ?? "";
            OwnerKey = ownerKey;
            Label = label ?? "";
            Members = (members ?? Enumerable.Empty<string>()).Where(m => !string.IsNullOrEmpty(m)).Distinct().ToArray();
            List = list;
        }
    }

    /// <summary>A row of <see cref="PlaysIn"/>: one list, with everything that plays it.</summary>
    public sealed class PlaysInRow
    {
        public string Label;
        public string[] Members;
        public List<(string Shown, string Key)> Owners = new List<(string, string)>();
        public object List;
    }

    /// <summary>
    /// The effect lists a sound or effect plays in, as rows. Many prefabs carry lists that are
    /// alike (every wooden piece breaks with the same sound and dust), so lists made for the same
    /// purpose of the same members are one row naming all of their players.
    /// </summary>
    public static class PlaysIn
    {
        public static List<PlaysInRow> Rows(IEnumerable<EffectUse> uses)
        {
            var rows = new Dictionary<string, PlaysInRow>(StringComparer.Ordinal);
            foreach (var use in uses)
            {
                var members = use.Members.OrderBy(m => m, StringComparer.OrdinalIgnoreCase).ToArray();
                var key = use.Label + "\n" + string.Join("\n", members);
                if (!rows.TryGetValue(key, out var row))
                {
                    row = new PlaysInRow { Label = use.Label, Members = members };
                    rows[key] = row;
                }
                if (!row.Owners.Any(o => o.Shown == use.Owner)) row.Owners.Add((use.Owner, use.OwnerKey));
            }

            var list = rows.Values.ToList();
            foreach (var row in list) row.Owners.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Shown, b.Shown));

            // Each row plays the list of the player named first.
            var byOwner = new Dictionary<string, object>();
            foreach (var use in uses)
            {
                var members = use.Members.OrderBy(m => m, StringComparer.OrdinalIgnoreCase);
                var key = use.Label + "\n" + string.Join("\n", members) + "\n" + use.Owner;
                if (!byOwner.ContainsKey(key)) byOwner[key] = use.List;
            }
            foreach (var row in list)
            {
                row.List = byOwner[row.Label + "\n" + string.Join("\n", row.Members) + "\n" + row.Owners[0].Shown];
            }

            list.Sort((a, b) =>
            {
                var byName = StringComparer.OrdinalIgnoreCase.Compare(a.Owners[0].Shown, b.Owners[0].Shown);
                return byName != 0 ? byName : StringComparer.OrdinalIgnoreCase.Compare(a.Label, b.Label);
            });
            return list;
        }
    }
}
