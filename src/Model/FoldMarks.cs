using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Which sections of the details stand folded, kept as marks with the panel's place: a section
    /// folds while its key is marked, but one shut until opened stands folded while its open mark
    /// is missing, so it starts folded however the panel was kept before. LINKED with the
    /// compendium on is such a section: it holds the page's own parts and wiring, nothing to find.
    /// </summary>
    internal static class FoldMarks
    {
        /// <summary>LINKED's key while the compendium is on, apart from its key with the compendium off.</summary>
        public const string LinkedInBook = "linksbook";

        /// <summary>The sections shut until opened, each with the mark kept while it stands open.</summary>
        private static readonly Dictionary<string, string> ShutUntilOpened = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LinkedInBook] = LinkedInBook + "open",
        };

        /// <summary>Whether a section stands folded; none for no section.</summary>
        public static bool Folded(ICollection<string> marks, string key)
        {
            if (key == null) return false;
            return ShutUntilOpened.TryGetValue(key, out var open) ? !marks.Contains(open) : marks.Contains(key);
        }

        /// <summary>Folds a section or opens it.</summary>
        public static void Set(ICollection<string> marks, string key, bool folded)
        {
            if (key == null) return;
            var shut = !ShutUntilOpened.TryGetValue(key, out var open);
            var mark = shut ? key : open;
            if (folded == shut) marks.Add(mark);
            else marks.Remove(mark);
        }

        /// <summary>Folds every section, or opens every one, letting go of every other mark as opening them all always has.</summary>
        public static void All(ICollection<string> marks, IEnumerable<string> sections, bool fold)
        {
            if (!fold) marks.Clear();
            foreach (var section in sections) Set(marks, section, fold);
        }
    }
}
