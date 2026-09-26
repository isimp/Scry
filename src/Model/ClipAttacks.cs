using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Which of a creature's animation clips each of its attacks plays, so a clip can play what
    /// its attack plays. The game starts an attack by an animator trigger, and the clip that
    /// trigger leads to is best seen by pulling it; where that was not seen, the clip named as the
    /// trigger ("attack_claws" is "Attack Claws"), else the only clip whose name holds the trigger
    /// whole ("stomp_l" in "Attack Stomp L"). A clip two attacks play is the first one's, so what
    /// the creature has now is given first.
    /// </summary>
    public static class ClipAttacks
    {
        /// <param name="attacks">Each attack's trigger and a key to it, what the creature has now first.</param>
        /// <param name="seen">The clip each trigger was seen to play, by trigger.</param>
        /// <param name="clips">The names of all the clips the creature has.</param>
        /// <returns>The key of the attack each clip plays, by clip name.</returns>
        public static Dictionary<string, object> Match(IEnumerable<(string Trigger, object Key)> attacks, IReadOnlyDictionary<string, string> seen, IEnumerable<string> clips)
        {
            var names = clips.Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
            var played = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var (trigger, key) in attacks)
            {
                if (string.IsNullOrEmpty(trigger)) continue;
                var clip = seen != null && seen.TryGetValue(trigger, out var saw) ? saw : ByName(trigger, names);
                if (clip != null && !played.ContainsKey(clip)) played[clip] = key;
            }
            return played;
        }

        /// <summary>Whether two names are the same but for case, spaces, underscores and hyphens.</summary>
        public static bool SameName(string a, string b)
        {
            var plain = Plain(a);
            return plain.Length > 0 && plain == Plain(b);
        }

        private static string ByName(string trigger, List<string> clips)
        {
            var same = clips.Find(c => SameName(trigger, c));
            if (same != null) return same;
            var plain = Plain(trigger);
            if (plain.Length == 0) return null;
            var holding = clips.Where(c => Plain(c).Contains(plain)).ToList();
            return holding.Count == 1 ? holding[0] : null;
        }

        private static string Plain(string name) =>
            new string((name ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}
