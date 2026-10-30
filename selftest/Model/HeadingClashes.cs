using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Scry's own fixed words that hold a thing's whole name: with the compendium on, the name
    /// mask (<see cref="NameMask"/>) reads such a word a smudge while the thing is not seen.
    /// </summary>
    internal static class HeadingClashes
    {
        /// <summary>Each heading holding a name whole, with the first such name.</summary>
        public static List<(string Heading, string Name)> Of(IEnumerable<string> headings, IEnumerable<string> names)
        {
            var all = new List<string>(names);
            var mask = new NameMask(all, null);
            var clashes = new List<(string, string)>();
            foreach (var heading in headings)
            {
                // The whole mask finds the few headings it touches; a name at a time tells which name.
                if (mask.Apply(heading) == heading) continue;
                var name = all.Find(n => new NameMask(new[] { n }, null).Apply(heading) != heading);
                if (name != null) clashes.Add((heading, name));
            }
            return clashes;
        }
    }
}
