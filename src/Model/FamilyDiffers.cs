using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Where a family's members differ (<see cref="Family"/>): their lines side by side, each line
    /// once, only where the values that show differ. A member only seen has no lines, its page
    /// closed; a value kept back counts as none, so no row tells that something kept back differs.
    /// </summary>
    internal static class FamilyDiffers
    {
        /// <summary>A member's cell for a line its page has not.</summary>
        public const string Missing = "—";

        /// <summary>
        /// The lines that differ, in the order the members first have them, a value for each
        /// member: its own, <see cref="Missing"/> where it has not the line, none where its page
        /// is closed (its lines none). A line is set side by side only where two of the values
        /// showing differ; one reading <paramref name="hidden"/> shows nothing.
        /// </summary>
        public static List<(string Label, string[] Values)> Rows(IReadOnlyList<IReadOnlyList<KeyValuePair<string, string>>> members, string hidden)
        {
            var labels = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var byMember = new Dictionary<string, string>[members.Count];
            for (var i = 0; i < members.Count; i++)
            {
                if (members[i] == null) continue;
                byMember[i] = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var line in members[i])
                {
                    if (byMember[i].ContainsKey(line.Key)) continue;
                    byMember[i][line.Key] = line.Value;
                    if (seen.Add(line.Key)) labels.Add(line.Key);
                }
            }

            var rows = new List<(string, string[])>();
            foreach (var label in labels)
            {
                var values = new string[members.Count];
                string first = null;
                var differs = false;
                for (var i = 0; i < members.Count; i++)
                {
                    if (byMember[i] == null) continue;
                    values[i] = byMember[i].TryGetValue(label, out var value) ? value : Missing;
                    if (values[i] == hidden) continue;
                    if (first == null) first = values[i];
                    else if (!string.Equals(first, values[i], StringComparison.Ordinal)) differs = true;
                }
                if (differs) rows.Add((label, values));
            }
            return rows;
        }
    }
}
