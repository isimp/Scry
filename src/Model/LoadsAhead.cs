using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What a piece of work needs loaded, asked for ahead and left to load on its own: the work goes
    /// on once none of it is still loading, each thing asked for once until forgotten.
    /// </summary>
    internal sealed class LoadsAhead<T>
    {
        private readonly HashSet<T> _asked = new HashSet<T>();

        /// <summary>
        /// Asks for each thing needed not asked for before; true once none of those needed is
        /// still loading, whether it loaded or failed.
        /// </summary>
        public bool Ready(IEnumerable<T> needed, Func<T, bool> loading, Action<T> ask)
        {
            var ready = true;
            foreach (var thing in needed)
            {
                if (_asked.Add(thing)) ask(thing);
                if (loading(thing)) ready = false;
            }
            return ready;
        }

        /// <summary>Whether a thing was asked for since the last time all was forgotten.</summary>
        public bool Asked(T thing) => _asked.Contains(thing);

        /// <summary>Forgets what was asked for, as what it held is let go of.</summary>
        public void Forget() => _asked.Clear();
    }
}
