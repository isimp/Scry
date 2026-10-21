using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What was worked out for so many keys, kept for the next time each is asked for: no more
    /// than its size, the one asked for longest ago let go of first as another comes in. Asking
    /// for one, or setting it again, counts it as recent.
    /// </summary>
    internal sealed class RecentCache<TKey, TValue>
    {
        private readonly int _size;
        private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _nodes;

        /// <summary>The keys from the most recent to the least.</summary>
        private readonly LinkedList<(TKey Key, TValue Value)> _recent = new LinkedList<(TKey, TValue)>();

        public RecentCache(int size, IEqualityComparer<TKey> comparer = null)
        {
            _size = Math.Max(1, size);
            _nodes = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>(comparer ?? EqualityComparer<TKey>.Default);
        }

        /// <summary>How many are kept.</summary>
        public int Count => _nodes.Count;

        /// <summary>What is kept for a key, counted as recent; false where nothing is.</summary>
        public bool TryGet(TKey key, out TValue value)
        {
            if (!_nodes.TryGetValue(key, out var node))
            {
                value = default;
                return false;
            }
            Recent(node);
            value = node.Value.Value;
            return true;
        }

        /// <summary>Keeps what was worked out for a key as the most recent, letting go of the least recent past the size.</summary>
        public void Set(TKey key, TValue value)
        {
            if (_nodes.TryGetValue(key, out var node))
            {
                node.Value = (key, value);
                Recent(node);
                return;
            }
            _nodes[key] = _recent.AddFirst((key, value));
            while (_nodes.Count > _size)
            {
                var oldest = _recent.Last;
                _recent.RemoveLast();
                _nodes.Remove(oldest.Value.Key);
            }
        }

        /// <summary>Lets go of what was kept for a key; false where nothing was.</summary>
        public bool Remove(TKey key)
        {
            if (!_nodes.TryGetValue(key, out var node)) return false;
            _recent.Remove(node);
            _nodes.Remove(key);
            return true;
        }

        /// <summary>Lets go of everything kept.</summary>
        public void Clear()
        {
            _nodes.Clear();
            _recent.Clear();
        }

        private void Recent(LinkedListNode<(TKey Key, TValue Value)> node)
        {
            if (node == _recent.First) return;
            _recent.Remove(node);
            _recent.AddFirst(node);
        }
    }
}
