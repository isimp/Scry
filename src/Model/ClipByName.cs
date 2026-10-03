using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Clips paired with what the game plays with an action by their names alone, where the
    /// animator could not be seen to go to them (its controller wants something more that cannot
    /// be read): a clip named for swimming or treading water with the water effect, one named for
    /// jumping with the jump. Only for actions the animator showed nothing for, never an attack's
    /// clip or one paired already, and told apart as found by name.
    /// </summary>
    internal static class ClipByName
    {
        /// <param name="actions">Each action, the words a clip's name holds for it, and a key to what it plays.</param>
        /// <param name="seen">The clips each action was seen to lead through, by action.</param>
        /// <param name="clips">The names of all the clips the creature has.</param>
        /// <param name="taken">The clips attacks play or that are paired already.</param>
        /// <returns>The key of what each clip found by name plays, by clip name.</returns>
        public static Dictionary<string, object> Match(IEnumerable<(string Action, string[] Words, object Key)> actions, IReadOnlyDictionary<string, IReadOnlyList<string>> seen,
            IEnumerable<string> clips, IEnumerable<string> taken)
        {
            var names = clips.Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
            var paired = new HashSet<string>(taken, StringComparer.Ordinal);
            var found = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var (action, words, key) in actions)
            {
                if (seen != null && seen.TryGetValue(action, out var saw) && saw.Count > 0) continue;
                foreach (var clip in names)
                {
                    if (paired.Contains(clip) || found.ContainsKey(clip)) continue;
                    var lower = clip.ToLowerInvariant();
                    if (words.Any(w => lower.Contains(w))) found[clip] = key;
                }
            }
            return found;
        }
    }
}
