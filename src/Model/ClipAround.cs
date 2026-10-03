using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What is heard around a creature's clip, though the game does not play it with the clip:
    /// waking or a spawn roar with what the creature calls out once alerted, which is what the
    /// game plays next (a troll wakes silently and roars when it spots you), and the clips it
    /// idles in with the idle sound it makes now and then while awake. Only clips the animator was
    /// seen to go to count; an attack clip hears nothing around it, and what an action leads to
    /// outranks idling.
    /// </summary>
    internal static class ClipAround
    {
        /// <param name="actions">Each action by name and a key to what is heard around the clip it leads to.</param>
        /// <param name="seen">The clips each action was seen to lead through, in order, by action.</param>
        /// <param name="idle">The clips the animator plays when left alone.</param>
        /// <param name="idleSound">A key to the idle sound, or null when the creature has none.</param>
        /// <param name="attacks">The clips attacks play.</param>
        /// <returns>The key of what is heard around each clip, by clip name.</returns>
        public static Dictionary<string, object> Match(IEnumerable<(string Action, object Key)> actions, IReadOnlyDictionary<string, IReadOnlyList<string>> seen,
            IEnumerable<string> idle, object idleSound, IEnumerable<string> attacks)
        {
            var taken = new HashSet<string>(attacks, StringComparer.Ordinal);
            var around = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var (action, key) in actions)
            {
                if (seen == null || !seen.TryGetValue(action, out var clips) || clips.Count == 0) continue;
                var clip = clips[0];
                if (!taken.Contains(clip) && !around.ContainsKey(clip)) around[clip] = key;
            }
            if (idleSound == null) return around;
            foreach (var clip in idle)
            {
                if (!taken.Contains(clip) && !around.ContainsKey(clip)) around[clip] = idleSound;
            }
            return around;
        }
    }
}
