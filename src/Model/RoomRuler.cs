using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The floor ruler in a room gone into: it spans the room's lowest level up to the top of the
    /// rooms above it, with a tick for each of the room's own levels and one for each level of the
    /// rooms above, those within a level of each other sharing one (<see cref="RoomFocus.SameLevel"/>),
    /// a room above on a level of the room's own adding none. The cut is kept over the lowest level
    /// (<see cref="FloorCut.LeastOver"/>); raised past the top, the room's roof goes on.
    /// </summary>
    internal sealed class RoomRuler
    {
        /// <summary>A tick of the ruler: its height, whether it is a level of the room's own, and how many rooms above stand on it.</summary>
        internal struct Tick
        {
            public float Height;
            public bool Own;
            public int Rooms;
        }

        private readonly List<Tick> _ticks = new List<Tick>();

        /// <summary>The ticks from the bottom up.</summary>
        public IReadOnlyList<Tick> Ticks => _ticks;

        /// <summary>The room's ground, which its own levels are named from.</summary>
        public float Ground { get; private set; }

        /// <summary>The room's lowest level, where the ruler begins.</summary>
        public float Low { get; private set; }

        /// <summary>The top of the room or of the highest room above it, where the ruler ends and over which the roof goes on.</summary>
        public float High { get; private set; }

        /// <summary>The lowest the cut goes.</summary>
        public float Least => Low + FloorCut.LeastOver;

        /// <summary>
        /// The ruler of a room: its ground and own levels (its ground where none were read), the
        /// top of its box, and the ground and top of each room above it.
        /// </summary>
        public static RoomRuler Of(float ground, IEnumerable<float> levels, float top, IEnumerable<(float Ground, float Top)> above)
        {
            var ruler = new RoomRuler { Ground = ground };
            var own = levels.Distinct().OrderBy(l => l).ToList();
            if (own.Count == 0) own.Add(ground);
            foreach (var level in own) ruler._ticks.Add(new Tick { Height = level, Own = true });
            ruler.Low = own[0];
            ruler.High = top;

            Tick? shared = null;
            foreach (var (roomGround, roomTop) in above.OrderBy(r => r.Ground))
            {
                ruler.High = Math.Max(ruler.High, roomTop);
                if (own.Any(level => Math.Abs(level - roomGround) <= RoomFocus.SameLevel)) continue;
                if (shared is Tick tick && roomGround - tick.Height <= RoomFocus.SameLevel)
                {
                    tick.Rooms++;
                    shared = tick;
                    ruler._ticks[ruler._ticks.Count - 1] = tick;
                    continue;
                }
                shared = new Tick { Height = roomGround, Rooms = 1 };
                ruler._ticks.Add(shared.Value);
            }
            ruler._ticks.Sort((a, b) => a.Height.CompareTo(b.Height));
            return ruler;
        }

        /// <summary>The tick lit: the highest of the room's own levels the cut stands at or over, or of the rooms above once they are back (<see cref="RoomFocus.WithCut"/>), the room first cut at <paramref name="from"/>; -1 under them all.</summary>
        public int TickLit(float cut, float from)
        {
            var lit = -1;
            for (var i = 0; i < _ticks.Count; i++)
            {
                var tick = _ticks[i];
                if (tick.Own ? tick.Height <= cut : RoomFocus.WithCut(RoomLook.Away, tick.Height, cut, from) == RoomLook.Near) lit = i;
            }
            return lit;
        }
    }
}
