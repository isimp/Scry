using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What the character hit lately, by an id of each, with its entry's key and when: found again
    /// within a window of the last hit, as a kill the game tells of by the creature's name or a
    /// thing seen go after a hit. The hits remembered are few: past <see cref="Kept"/>, the oldest
    /// is forgotten.
    /// </summary>
    internal sealed class Lately<TId>
    {
        /// <summary>How many hits are remembered at most.</summary>
        public const int Kept = 256;

        private readonly Dictionary<TId, (string Key, double At)> _hits = new Dictionary<TId, (string, double)>();
        private readonly double _window;

        /// <summary>Hits found again within so many seconds of the last one.</summary>
        public Lately(double window) => _window = window;

        /// <summary>How many hits are remembered.</summary>
        public int Count => _hits.Count;

        /// <summary>The character hit a thing, by its id and its entry's key, at a time in seconds; its last hit is the one remembered.</summary>
        public void Hit(TId id, string key, double at)
        {
            if (id == null || string.IsNullOrEmpty(key)) return;
            _hits[id] = (key, at);
            if (_hits.Count > Kept) _hits.Remove(Oldest());
        }

        /// <summary>The key of a thing hit within the window before now; none otherwise.</summary>
        public string Of(TId id, double now) => id != null && _hits.TryGetValue(id, out var hit) && now - hit.At <= _window ? hit.Key : null;

        /// <summary>The same, and the thing forgotten, as one that went goes once.</summary>
        public string Take(TId id, double now)
        {
            var key = Of(id, now);
            if (id != null) _hits.Remove(id);
            return key;
        }

        /// <summary>Forgets every hit, as the world is left.</summary>
        public void Forget() => _hits.Clear();

        private TId Oldest()
        {
            var oldest = default(TId);
            var at = double.MaxValue;
            foreach (var pair in _hits)
            {
                if (pair.Value.At >= at) continue;
                oldest = pair.Key;
                at = pair.Value.At;
            }
            return oldest;
        }
    }
}
