using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>What became of a thing looked at in its round (<see cref="Rounds{T}"/>).</summary>
    internal enum Looked
    {
        /// <summary>Kept, to be looked at again next round.</summary>
        Kept,

        /// <summary>Seen, or nothing more to learn of it: let go.</summary>
        LetGo,

        /// <summary>Gone, or never one to look at: dropped.</summary>
        Dropped,
    }

    /// <summary>
    /// Things looked at in rounds, a share of them each frame, so that each is looked at once a
    /// round however many there are and no one frame pays for all: the share is the frame's part
    /// of the round, what is left over carried to the next frame, and never more than a round's
    /// worth. One let go or dropped leaves without another being passed over or looked at twice.
    /// </summary>
    internal sealed class Rounds<T> where T : class
    {
        private readonly List<T> _things = new List<T>();
        private readonly float _round;
        private int _next;
        private double _owed;

        /// <summary>Things looked at once every so many seconds.</summary>
        public Rounds(float round) => _round = round;

        /// <summary>How many things are kept to look at.</summary>
        public int Count => _things.Count;

        /// <summary>How many were let go since the things were last cleared.</summary>
        public int LetGo { get; private set; }

        /// <summary>How many were dropped since the things were last cleared.</summary>
        public int Dropped { get; private set; }

        /// <summary>Whether a thing is kept to look at, one by one; for a check, not each frame.</summary>
        public bool Contains(T thing) => _things.Contains(thing);

        /// <summary>Keeps a thing to look at, from the next share on.</summary>
        public void Add(T thing)
        {
            if (thing != null) _things.Add(thing);
        }

        /// <summary>Forgets every thing kept, and what was let go and dropped.</summary>
        public void Clear()
        {
            _things.Clear();
            _owed = 0;
            LetGo = 0;
            Dropped = 0;
        }

        /// <summary>
        /// Looks at the share of the things due after a frame of so many seconds, each told what
        /// became of it; where the frame's time is spent first, the rest stay owed to the frames after.
        /// </summary>
        public void Look(float seconds, Func<T, Looked> look, Func<bool> spent = null)
        {
            var count = _things.Count;
            _owed = Math.Min(_owed + count * Math.Max(0.0, seconds) / _round, count);
            for (; _owed >= 1.0 && _things.Count > 0; _owed -= 1.0)
            {
                if (spent != null && spent()) break;
                if (_next >= _things.Count) _next = 0;
                switch (look(_things[_next]))
                {
                    case Looked.Kept:
                        _next++;
                        break;
                    case Looked.LetGo:
                        LetGo++;
                        Remove(_next);
                        break;
                    default:
                        Dropped++;
                        Remove(_next);
                        break;
                }
            }
        }

        /// <summary>Looks at every thing kept once, letting none go whatever it says; how a whole round is timed.</summary>
        public void LookAtAll(Func<T, Looked> look)
        {
            foreach (var thing in _things) look(thing);
        }

        /// <summary>
        /// Takes a thing out by putting the last in its place: the last is not yet looked at this
        /// round, so it is looked at next, and none is passed over.
        /// </summary>
        private void Remove(int at)
        {
            var last = _things.Count - 1;
            _things[at] = _things[last];
            _things.RemoveAt(last);
        }
    }
}
