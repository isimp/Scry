using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Which of a creature's clips play what the game plays as it moves the animator there: a
    /// jump's effects with the clip the jump trigger leads to, waking's with the clip waking leads
    /// to, and so on. Only what the animator was seen to do counts, never a name; the first clip
    /// an action leads to is its, unless an attack plays it, and a clip two actions lead to is
    /// the first one's.
    /// </summary>
    internal static class ClipActions
    {
        /// <param name="actions">Each action by name and a key to what the game plays with it.</param>
        /// <param name="seen">The clips each action was seen to lead through, in order, by action.</param>
        /// <param name="attacks">The clips attacks play.</param>
        /// <returns>The key of what each clip plays, by clip name.</returns>
        public static Dictionary<string, object> Match(IEnumerable<(string Action, object Key)> actions, IReadOnlyDictionary<string, IReadOnlyList<string>> seen, IEnumerable<string> attacks)
        {
            var taken = new HashSet<string>(attacks, StringComparer.Ordinal);
            var played = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var (action, key) in actions)
            {
                if (seen == null || !seen.TryGetValue(action, out var clips) || clips.Count == 0) continue;
                var clip = clips[0];
                if (taken.Contains(clip) || played.ContainsKey(clip)) continue;
                played[clip] = key;
            }
            return played;
        }
    }
}
