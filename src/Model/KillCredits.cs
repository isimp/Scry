using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// A kill the game credits the character with (<c>Game.RPC_RegisterKill</c>, sent to every
    /// player flagged as an attacker, whichever game ran the death) and the remains of a death
    /// seen on this game (<see cref="RemainsSeen"/>): told within seconds of each other, in either
    /// order, they are one death the character did, so its loot counts as a kill's does, however
    /// far the character stood, even where the game running the creature has no Scry to tell it.
    /// The credit names the creature by its name token, so it may fit several prefabs.
    /// </summary>
    internal sealed class KillCredits
    {
        /// <summary>How long a credit and the remains may lie apart, in seconds: the kill's message and the ragdoll travel by different ways.</summary>
        public const double Within = 5.0;

        private readonly List<(string Creature, double At)> _credits = new List<(string, double)>();
        private readonly List<(string Creature, WorldEvent Told, double At)> _remains = new List<(string, WorldEvent, double)>();

        /// <summary>The game credited the character with a kill of a creature of one of these prefabs, at a time.</summary>
        public void Credited(IEnumerable<string> creatures, double now)
        {
            Prune(now);
            foreach (var creature in creatures) if (!string.IsNullOrEmpty(creature)) _credits.Add((creature, now));
        }

        /// <summary>Remains of a creature seen: whether a credit for it within <see cref="Within"/> stands, which this takes.</summary>
        public bool TakeCredit(string creature, double now)
        {
            Prune(now);
            var at = _credits.FindIndex(c => c.Creature == creature);
            if (at < 0) return false;
            // A credit fits one death: every prefab it named goes with it.
            var when = _credits[at].At;
            _credits.RemoveAll(c => c.At == when);
            return true;
        }

        /// <summary>Remains told with no credit yet, kept for a credit that may follow.</summary>
        public void RemainsTold(string creature, WorldEvent told, double now)
        {
            Prune(now);
            if (told != null && !string.IsNullOrEmpty(creature)) _remains.Add((creature, told, now));
        }

        /// <summary>A credit come for creatures of these prefabs: the remains told within <see cref="Within"/> of any of them, each once, taken.</summary>
        public List<WorldEvent> ClaimRemains(IEnumerable<string> creatures, double now)
        {
            Prune(now);
            var claimed = new List<WorldEvent>();
            var named = new HashSet<string>(creatures);
            for (var i = _remains.Count - 1; i >= 0; i--)
            {
                if (!named.Contains(_remains[i].Creature)) continue;
                claimed.Add(_remains[i].Told);
                _remains.RemoveAt(i);
                // One credit, one death.
                break;
            }
            return claimed;
        }

        /// <summary>On leaving a world.</summary>
        public void Forget()
        {
            _credits.Clear();
            _remains.Clear();
        }

        private void Prune(double now)
        {
            _credits.RemoveAll(c => now - c.At > Within);
            _remains.RemoveAll(r => now - r.At > Within);
        }
    }
}
