using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What each play button started, so the button stays lit until all of it has finished. Each
    /// thing started is tagged with the part it copies, so the parts of a list can be lit one by
    /// one. The button pressed last is remembered after it finishes, for showing what it played.
    /// </summary>
    public sealed class Playback<T> where T : class
    {
        private readonly Func<T, bool> _alive;
        private readonly Dictionary<object, List<(string Tag, T Thing)>> _started = new Dictionary<object, List<(string, T)>>();

        public Playback(Func<T, bool> alive)
        {
            _alive = alive;
        }

        /// <summary>The button pressed last, or null.</summary>
        public object Last { get; private set; }

        /// <summary>How many things are being watched, for keeping it small.</summary>
        public int Tracked
        {
            get
            {
                var count = 0;
                foreach (var list in _started.Values) count += list.Count;
                return count;
            }
        }

        public void Started(object key, IEnumerable<(string Tag, T Thing)> things)
        {
            if (key == null) return;
            Last = key;
            if (!_started.TryGetValue(key, out var list))
            {
                list = new List<(string, T)>();
                _started[key] = list;
            }
            foreach (var thing in things)
            {
                if (thing.Thing != null) list.Add(thing);
            }
        }

        /// <summary>Whether anything the button started, or the part of it with this tag, still plays.</summary>
        public bool IsPlaying(object key, string tag = null)
        {
            if (key == null || !_started.TryGetValue(key, out var list)) return false;

            list.RemoveAll(t => t.Thing == null || !_alive(t.Thing));
            if (list.Count == 0) _started.Remove(key);

            foreach (var thing in list)
            {
                if (tag == null || thing.Tag == tag) return true;
            }
            return false;
        }

        public void Forget()
        {
            _started.Clear();
            Last = null;
        }
    }
}
