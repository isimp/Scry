using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The animation that goes with an effect list of a creature. The game plays both at the same
    /// moment (it staggers as its hit effect plays, it rears up as it is alerted), but nothing ties
    /// them by name, so they are matched by what the effect is for: each purpose has the words its
    /// animations are named with, tried in order, and the plainest animation with the first word
    /// that turns up is the one.
    /// </summary>
    public static class ClipMatch
    {
        /// <summary>A word of the effect's label, and the words of the animations that go with it.</summary>
        private static readonly KeyValuePair<string, string[]>[] Words =
        {
            new KeyValuePair<string, string[]>("death", new[] { "death", "die", "dead" }),
            new KeyValuePair<string, string[]>("jump", new[] { "jump" }),
            new KeyValuePair<string, string[]>("hit", new[] { "hit" }),
            new KeyValuePair<string, string[]>("stagger", new[] { "stagger", "hit" }),
            new KeyValuePair<string, string[]>("alerted", new[] { "alert" }),
            new KeyValuePair<string, string[]>("wakeup", new[] { "wakeup", "wake" }),
            new KeyValuePair<string, string[]>("consume", new[] { "eat", "consume" }),
            new KeyValuePair<string, string[]>("block", new[] { "block" }),
            new KeyValuePair<string, string[]>("attack", new[] { "attack" }),
        };

        /// <summary>The name of the animation that goes with the effect, or null when there is none.</summary>
        public static string For(string effect, IEnumerable<string> clips)
        {
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var word in (effect ?? "").Split(new[] { ' ', '(', ')', ':', '_', '-' }, StringSplitOptions.RemoveEmptyEntries))
            {
                words.Add(word);
            }

            var names = new List<string>(clips ?? new string[0]);
            foreach (var pair in Words)
            {
                if (!words.Contains(pair.Key)) continue;
                foreach (var wanted in pair.Value)
                {
                    string best = null;
                    foreach (var name in names)
                    {
                        if (name == null || name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (best == null || name.Length < best.Length) best = name;
                    }
                    if (best != null) return best;
                }
            }
            return null;
        }
    }
}
