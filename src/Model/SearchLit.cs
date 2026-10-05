using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The parts of a name the search lights in the list, so a row shows why it is there: each
    /// typed word where the search finds it first, whatever the case, parts that touch or
    /// overlap taken as one; a name in quotes lights the whole name it matches.
    /// </summary>
    internal static class SearchLit
    {
        /// <summary>Fills <paramref name="into"/> with the lit parts of a name, in order; it is cleared first, so one list serves every row.</summary>
        public static void Spans(string name, IReadOnlyList<string> words, IReadOnlyList<string> names, List<(int Start, int Length)> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(name)) return;
            foreach (var exact in names)
            {
                if (!string.Equals(name, exact, StringComparison.OrdinalIgnoreCase)) continue;
                into.Add((0, name.Length));
                return;
            }
            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(word)) continue;
                var at = name.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                if (at >= 0) into.Add((at, word.Length));
            }
            into.Sort((a, b) => a.Start.CompareTo(b.Start));
            for (var i = into.Count - 1; i > 0; i--)
            {
                var before = into[i - 1];
                var span = into[i];
                if (span.Start > before.Start + before.Length) continue;
                into[i - 1] = (before.Start, Math.Max(before.Start + before.Length, span.Start + span.Length) - before.Start);
                into.RemoveAt(i);
            }
        }
    }
}
