using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Creatures seen wild near the character, so that one later seen tame was tamed while in
    /// sight, whichever player's game tamed it: whether a creature is tame is kept on it for every
    /// player to see. One tame when first seen was tamed before, and is not told. The wild
    /// remembered are few: past <see cref="Kept"/>, the one longest unseen is forgotten.
    /// </summary>
    internal sealed class TameWatch<T>
    {
        /// <summary>How many wild creatures are remembered at most.</summary>
        public const int Kept = 256;

        private readonly Dictionary<T, long> _wild = new Dictionary<T, long>();
        private long _seen;

        /// <summary>How many wild creatures are remembered.</summary>
        public int Count => _wild.Count;

        /// <summary>The creatures near by now, each wild or tame; tells those remembered wild that are tame now, once each.</summary>
        public List<T> Look(IEnumerable<(T Creature, bool Tame)> near)
        {
            var tamed = new List<T>();
            foreach (var (creature, tame) in near)
            {
                if (tame)
                {
                    if (_wild.Remove(creature)) tamed.Add(creature);
                    continue;
                }
                _wild[creature] = ++_seen;
                if (_wild.Count > Kept) _wild.Remove(LongestUnseen());
            }
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
                if (pair.Value >= at) continue;
                oldest = pair.Key;
                at = pair.Value;
            }
            return oldest;
        }
    }
}
