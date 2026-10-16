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

        /// <summary>
        /// Kept, but nothing new can show of it until the eye moves, as of a thing standing out of
        /// reach or out of view: passed over without a look until then (<see cref="Rounds{T}.Wake"/>).
        /// </summary>
        Rests,
    }

    /// <summary>
    /// Things looked at in rounds, a share of them each frame, so that each is looked at once a
    /// round however many there are and no one frame pays for all: the share is the frame's part
    /// of the round, what is left over carried to the next frame, and never more than a round's
    /// worth. One let go or dropped leaves without another being passed over or looked at twice.
    /// One resting keeps its turn in the round but is passed over without a look, costing next to
    /// nothing, until the eye moves; then it is looked at in its turn, within a round.
    /// </summary>
    internal sealed class Rounds<T> where T : class
    {
        /// <summary>
        /// A thing kept, with how often the eye had moved when it last came to rest; it rests
        /// while the eye has moved no more since, and is awake once it has.
        /// </summary>
        private struct Held
        {
            public T Thing;
            public int RestedAt;
        }

        /// <summary>What <see cref="Held.RestedAt"/> holds for a thing that never rested.</summary>
        private const int Awake = -1;

        private readonly List<Held> _things = new List<Held>();
        private readonly float _round;
        private int _next;
        private double _owed;

        /// <summary>How often the eye moved (<see cref="Wake"/>): a thing rests while this is what it was when it came to rest.</summary>
        private int _moves;

        /// <summary>Things looked at once every so many seconds.</summary>
        public Rounds(float round) => _round = round;

        /// <summary>How many things are kept to look at, those resting among them.</summary>
        public int Count => _things.Count;

        /// <summary>How many things kept rest now, one by one; for a check, not each frame.</summary>
        public int Resting
        {
            get
            {
                var resting = 0;
                foreach (var kept in _things) if (kept.RestedAt == _moves) resting++;
                return resting;
            }
        }

        /// <summary>How many were let go since the things were last cleared.</summary>
        public int LetGo { get; private set; }

        /// <summary>How many were dropped since the things were last cleared.</summary>
        public int Dropped { get; private set; }

        /// <summary>Whether a thing is kept to look at, resting or not, one by one; for a check, not each frame.</summary>
        public bool Contains(T thing)
        {
            foreach (var kept in _things) if (ReferenceEquals(kept.Thing, thing)) return true;
            return false;
        }

        /// <summary>Keeps a thing to look at, from the next share on.</summary>
        public void Add(T thing)
        {
            if (thing != null) _things.Add(new Held { Thing = thing, RestedAt = Awake });
        }

        /// <summary>The eye moved: every thing resting is looked at again in its turn.</summary>
        public void Wake() => _moves++;

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
        /// became of it; where the frame's time is spent first, the rest stay owed to the frames
        /// after. A thing resting takes its turn without a look, which spends no time.
        /// </summary>
        public void Look(float seconds, Func<T, Looked> look, Func<bool> spent = null)
        {
            var count = _things.Count;
            _owed = Math.Min(_owed + count * Math.Max(0.0, seconds) / _round, count);
            for (; _owed >= 1.0 && _things.Count > 0; _owed -= 1.0)
            {
                if (_next >= _things.Count) _next = 0;
                var kept = _things[_next];
                if (kept.RestedAt == _moves)
                {
                    _next++;
                    continue;
                }
                if (spent != null && spent()) break;
                switch (look(kept.Thing))
                {
                    case Looked.Kept:
                        _next++;
                        break;
                    case Looked.Rests:
                        _things[_next] = new Held { Thing = kept.Thing, RestedAt = _moves };
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

        /// <summary>Looks at every thing kept once, resting ones too, letting none go and waking none whatever it says; how a whole round is timed.</summary>
        public void LookAtAll(Func<T, Looked> look)
        {
            foreach (var kept in _things) look(kept.Thing);
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
