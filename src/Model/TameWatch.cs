using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Creatures seen wild near the character, so that one later seen tame was tamed while in
    /// sight, whichever player's game tamed it: whether a creature is tame is kept on it for every
    /// player to see. One tame when first seen was tamed before, and is not told. The wild
    /// remembered are few: past <see cref="Kept"/>, the one longest unseen is forgotten, and one
    /// out of sight for half a minute is forgotten too, as its taming meanwhile was not seen.
    /// </summary>
    internal sealed class TameWatch<T>
    {
        /// <summary>How many wild creatures are remembered at most.</summary>
        public const int Kept = 256;

        /// <summary>How long a wild creature stays remembered once out of sight, in seconds: one tamed farther off meanwhile was not seen tamed.</summary>
        public const double OutOfSight = 30.0;

        private readonly Dictionary<T, (long Order, double At)> _wild = new Dictionary<T, (long, double)>();
        private long _seen;

        /// <summary>How many wild creatures are remembered.</summary>
        public int Count => _wild.Count;

        /// <summary>The creatures in sight now, each wild or tame, at a time; tells those remembered wild that are tame now, once each, and forgets those out of sight too long (<see cref="OutOfSight"/>).</summary>
        public List<T> Look(IEnumerable<(T Creature, bool Tame)> near, double now = 0.0)
        {
            var tamed = new List<T>();
            foreach (var (creature, tame) in near)
            {
                if (tame)
                {
                    if (_wild.Remove(creature)) tamed.Add(creature);
                    continue;
                }
                _wild[creature] = (++_seen, now);
                if (_wild.Count > Kept) _wild.Remove(LongestUnseen());
            }
            List<T> gone = null;
            foreach (var pair in _wild)
            {
                if (now - pair.Value.At <= OutOfSight) continue;
                (gone ?? (gone = new List<T>())).Add(pair.Key);
            }
            if (gone != null) foreach (var creature in gone) _wild.Remove(creature);
            return tamed;
        }

        /// <summary>Forgets every wild creature remembered, as the world is left.</summary>
        public void Forget() => _wild.Clear();

        private T LongestUnseen()
        {
            var oldest = default(T);
            var at = long.MaxValue;
            foreach (var pair in _wild)
            {
                if (pair.Value.Order >= at) continue;
                oldest = pair.Key;
                at = pair.Value.Order;
            }
            return oldest;
        }
    }
}
