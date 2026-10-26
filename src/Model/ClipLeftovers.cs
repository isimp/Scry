using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The clips of a creature's animator it never plays: those only an attack it cannot make leads
    /// to. Kin share one animator (every Charred kind's holds all their attacks), while the game
    /// starts an attack only by the trigger of an item the creature carries (<c>Attack.Start</c>).
    /// A clip that a trigger it does pull also leads to, or that it plays another way (its death, a
    /// jump, an idle), is played.
    /// </summary>
    internal static class ClipLeftovers
    {
        /// <summary>
        /// The attack-like triggers the game's own code pulls by name, never an item:
        /// <c>Humanoid.StartAttack</c> pulls "attack_abort" to end any attack.
        /// </summary>
        private static readonly string[] GameTriggers = { "attack_abort" };

        /// <summary>Words in a trigger's name that tell an attack: "attack_bow", "taunt", "swing_sledge".</summary>
        private static readonly string[] AttackWords = { "attack", "taunt", "swing" };

        /// <summary>The triggers of an animator worth asking where they lead: those named as attacks, not pulled by the game's own code.</summary>
        public static List<string> Candidates(IEnumerable<string> triggers) =>
            triggers.Where(t => !string.IsNullOrEmpty(t) && Array.IndexOf(GameTriggers, t) < 0 && AttackWords.Any(w => t.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();

        /// <param name="leadsTo">The clips each asked trigger was seen to lead to; null while not seen.</param>
        /// <param name="pulled">The triggers the creature's items pull, as it carries them now.</param>
        /// <param name="playedOtherwise">The clips it plays by the game's own actions, idling, by name or around them.</param>
        /// <returns>The clips only triggers it does not pull lead to.</returns>
        public static HashSet<string> Unplayed(IReadOnlyDictionary<string, IReadOnlyList<string>> leadsTo, IEnumerable<string> pulled, IEnumerable<string> playedOtherwise) =>
            new HashSet<string>(UnplayedBy(leadsTo, pulled, playedOtherwise).Keys, StringComparer.Ordinal);

        /// <summary>As <see cref="Unplayed"/>, each clip with the first trigger seen to lead to it.</summary>
        public static Dictionary<string, string> UnplayedBy(IReadOnlyDictionary<string, IReadOnlyList<string>> leadsTo, IEnumerable<string> pulled, IEnumerable<string> playedOtherwise)
        {
            var left = new Dictionary<string, string>(StringComparer.Ordinal);
            if (leadsTo == null) return left;
            var pulling = new HashSet<string>(pulled ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var played = new HashSet<string>(playedOtherwise ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            foreach (var lead in leadsTo) if (pulling.Contains(lead.Key)) played.UnionWith(lead.Value);
            foreach (var lead in leadsTo.OrderBy(l => l.Key, StringComparer.Ordinal))
            {
                if (pulling.Contains(lead.Key)) continue;
                foreach (var clip in lead.Value) if (!played.Contains(clip) && !left.ContainsKey(clip)) left[clip] = lead.Key;
            }
            return left;
        }
    }
}
