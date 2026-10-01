using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Which mod added something, from the clues found for it: a bundle a mod ships holding it
    /// by its own name is taken first; otherwise the mods whose bundles hold what it uses (its
    /// sounds, icons, materials), or whose assemblies hold its scripts, when they are one mod.
    /// Clues naming several mods leave it unnamed, as a guess would mislead.
    /// </summary>
    public static class ModAttribution
    {
        /// <summary>The mod named by its own name, else the one mod its other clues name, else null.</summary>
        public static string Pick(string byName, IEnumerable<string> byAssets)
        {
            if (!string.IsNullOrEmpty(byName)) return byName;
            if (byAssets == null) return null;
            string found = null;
            foreach (var mod in byAssets)
            {
                if (string.IsNullOrEmpty(mod)) continue;
                if (found == null) found = mod;
                else if (!string.Equals(found, mod, StringComparison.Ordinal)) return null;
            }
            return found;
        }
    }
}
