using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What a clip plays of the attack it is part of.</summary>
    internal sealed class ClipAttack
    {
        /// <summary>The attack.</summary>
        public object Key;

        /// <summary>The attack begins in this clip: what it plays as it starts plays as the clip starts.</summary>
        public bool Begins;

        /// <summary>The attack strikes in this clip: what lands plays when the clip says, or halfway.</summary>
        public bool Strikes;

        /// <summary>No clip of the attack says when it strikes, so it strikes halfway through this one.</summary>
        public bool Halfway;
    }

    /// <summary>
    /// Which of a creature's animation clips each of its attacks plays, so a clip can play what
    /// its attack plays. The game starts an attack by an animator trigger, and the clips that
    /// trigger leads through are best seen by pulling it; where that was not seen, the clip named
    /// as the trigger ("attack_claws" is "Attack Claws"), else the only clip whose name holds the
    /// trigger whole ("stomp_l" in "Attack Stomp L"). The attack begins in its first clip and
    /// strikes in those whose events say so; where none says so, halfway through the first. A clip
    /// two attacks play is the first one's, so what the creature has now is given first.
    /// </summary>
    internal static class ClipAttacks
    {
        /// <param name="attacks">Each attack's trigger and a key to it, what the creature has now first.</param>
        /// <param name="seen">The clips each trigger was seen to play, in order, by trigger.</param>
        /// <param name="clips">The names of all the clips the creature has.</param>
        /// <param name="striking">The clips whose events say when an attack strikes.</param>
        /// <returns>What each clip plays of its attack, by clip name.</returns>
        public static Dictionary<string, ClipAttack> Match(IEnumerable<(string Trigger, object Key)> attacks, IReadOnlyDictionary<string, IReadOnlyList<string>> seen,
            IEnumerable<string> clips, IEnumerable<string> striking)
        {
            var names = clips.Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
            var strikes = new HashSet<string>(striking, StringComparer.Ordinal);
            var played = new Dictionary<string, ClipAttack>(StringComparer.Ordinal);
            foreach (var (trigger, key) in attacks)
            {
                if (string.IsNullOrEmpty(trigger)) continue;
                var sequence = seen != null && seen.TryGetValue(trigger, out var saw) ? saw.ToList() : new List<string>();
                if (sequence.Count == 0)
                {
                    var named = ByName(trigger, names);
                    if (named != null) sequence.Add(named);
                }
                if (sequence.Count == 0) continue;

                var halfway = !sequence.Any(strikes.Contains);
                for (var i = 0; i < sequence.Count; i++)
                {
                    if (played.ContainsKey(sequence[i])) continue;
                    played[sequence[i]] = new ClipAttack
                    {
                        Key = key,
                        Begins = i == 0,
                        Strikes = strikes.Contains(sequence[i]) || (halfway && i == 0),
                        Halfway = halfway && i == 0,
                    };
                }
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
