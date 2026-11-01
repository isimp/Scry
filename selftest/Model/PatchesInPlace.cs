using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>Which of the patch classes Scry registers sit on no game method, so a moved or renamed hook cannot leave the self-test checking an old list.</summary>
    internal static class PatchesInPlace
    {
        /// <summary>
        /// Each registered patch class not among those sitting on a method, leaving out those whose
        /// feature the game turned off, which are told as such, and those that only copy a game
        /// method's body as it was (a reverse patch), which by design sit on nothing.
        /// </summary>
        public static List<string> Missing(IEnumerable<(string Patch, bool FeatureOff, bool CopiesOnly)> registered, ICollection<string> sitting) =>
            registered.Where(r => !r.FeatureOff && !r.CopiesOnly && !sitting.Contains(r.Patch)).Select(r => r.Patch).ToList();
    }
}
