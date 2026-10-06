using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// A walk over a tree a part at a time, each part before what is under it and those in the
    /// order held, as Unity gives a part and all under it. It goes on where it stopped, so a big
    /// tree is walked over several frames rather than in one.
    /// </summary>
    internal sealed class PreOrderWalk<T>
    {
        private readonly Stack<T> _left = new Stack<T>();
        private readonly Func<T, int> _count;
        private readonly Func<T, int, T> _under;

        /// <summary>A walk from a root, with how many parts each has under it and the one at each place.</summary>
        public PreOrderWalk(T root, Func<T, int> count, Func<T, int, T> under)
        {
            _count = count;
            _under = under;
            _left.Push(root);
        }

        /// <summary>Whether every part has been given.</summary>
        public bool Done => _left.Count == 0;

        /// <summary>The next part; false once there are none.</summary>
        public bool Next(out T part)
        {
            if (_left.Count == 0)
            {
                part = default;
                return false;
            }
            part = _left.Pop();
            // Last first, so the first under it is the next one taken.
            for (var i = _count(part) - 1; i >= 0; i--) _left.Push(_under(part, i));
            return true;
        }
    }
}
