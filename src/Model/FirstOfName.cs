using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Each of a list once by its name, the first: the game finds a status effect by its name's
    /// hash, the first of that name in its list (<c>ObjectDB.GetStatusEffect</c>), so a second
    /// of the same name, such as a mod putting its own in twice, is never what the game uses.
    /// The catalog reads the scene's prefabs the same way. A name is compared as it is written,
    /// as the game hashes it; something with no name is left out.
    /// </summary>
    internal static class FirstOfName
    {
        public static IEnumerable<T> Each<T>(IEnumerable<T> all, Func<T, string> name)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var each in all)
            {
                var named = name(each);
                if (string.IsNullOrEmpty(named) || !seen.Add(named)) continue;
                yield return each;
            }
        }
    }
}
