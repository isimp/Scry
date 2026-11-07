using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Marks on a map that stand so near on the screen that they touch, gathered as stacks: each
    /// mark joins the stack of the first mark before it within a touch of it, the stack's anchor,
    /// so a row of marks each touching the next is several stacks, never one long one. Gathered by
    /// where the marks lie in the world, with the touch in metres at the map's zoom, so panning
    /// the map changes nothing and only a zoom gathers again. At rest a stack's marks stand where
    /// they lie with a pip under its anchor for each mark (<see cref="Pips"/>); pointed at, its
    /// marks fan out round the anchor (<see cref="FanAt"/>).
    /// </summary>
    internal sealed class MarkStacks
    {
        /// <summary>How near two marks stand to touch, as a share of a mark's width: the ring, not the halo round it.</summary>
        public const float Touching = 0.9f;

        /// <summary>The most pips under a stack; more marks there still show this many.</summary>
        public const int MostPips = 4;

        /// <summary>The most marks a stack fans out; its words still name every kind there.</summary>
        public const int MostFanned = 12;

        private readonly List<int> _anchorOf = new List<int>();
        private readonly Dictionary<int, List<int>> _members = new Dictionary<int, List<int>>();
        private readonly Dictionary<long, List<int>> _anchorsIn = new Dictionary<long, List<int>>();
        private readonly List<(float X, float Z)> _spots = new List<(float X, float Z)>();
        private readonly Stack<List<int>> _spare = new Stack<List<int>>();
        private static readonly IReadOnlyList<int> Alone = Array.Empty<int>();

        /// <summary>
        /// The marks gathered afresh: <paramref name="count"/> of them, each where
        /// <paramref name="at"/> says it lies, touching within <paramref name="touch"/> metres;
        /// by a grid of that width, so each mark looks only at the anchors in the cells round it.
        /// </summary>
        public void Gather(int count, Func<int, (float X, float Z)> at, float touch)
        {
            foreach (var list in _members.Values) Spare(list);
            foreach (var list in _anchorsIn.Values) Spare(list);
            _members.Clear();
            _anchorsIn.Clear();
            _anchorOf.Clear();
            if (!(touch > 0.001f) || float.IsInfinity(touch)) touch = 0.001f;
            var reach = touch * touch;
            var spots = _spots;
            spots.Clear();
            for (var i = 0; i < count; i++)
            {
                var (x, z) = at(i);
                spots.Add((x, z));
                var (cx, cz) = (Cell(x, touch), Cell(z, touch));
                var anchor = -1;
                for (var gx = cx - 1; gx <= cx + 1; gx++)
                {
                    for (var gz = cz - 1; gz <= cz + 1; gz++)
                    {
                        if (!_anchorsIn.TryGetValue(Key(gx, gz), out var anchors)) continue;
                        for (var k = 0; k < anchors.Count; k++)
                        {
                            var a = anchors[k];
                            var (dx, dz) = (spots[a].X - x, spots[a].Z - z);
                            if (dx * dx + dz * dz >= reach) continue;
                            // The first anchor before it wins, as the marks stand in order.
                            if (anchor < 0 || a < anchor) anchor = a;
                        }
                    }
                }
                if (anchor < 0)
                {
                    anchor = i;
                    var cell = Key(cx, cz);
                    if (!_anchorsIn.TryGetValue(cell, out var here)) _anchorsIn[cell] = here = Fresh();
                    here.Add(i);
                    _members[i] = Fresh();
                }
                _members[anchor].Add(i);
                _anchorOf.Add(anchor);
            }
        }

        /// <summary>The anchor of the stack a mark stands in, itself where it stands alone; -1 for one never gathered.</summary>
        public int AnchorOf(int mark) => mark >= 0 && mark < _anchorOf.Count ? _anchorOf[mark] : -1;

        /// <summary>How many marks stand in the stack an anchor heads; none for a mark that heads none.</summary>
        public int CountAt(int anchor) => _members.TryGetValue(anchor, out var members) ? members.Count : 0;

        /// <summary>The marks of the stack an anchor heads, in the order they stand, the anchor first.</summary>
        public IReadOnlyList<int> MembersOf(int anchor) => _members.TryGetValue(anchor, out var members) ? members : Alone;

        private static int Cell(float metres, float width) => (int)Math.Floor(metres / width);

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        private List<int> Fresh() => _spare.Count > 0 ? _spare.Pop() : new List<int>();

        private void Spare(List<int> list)
        {
            list.Clear();
            _spare.Push(list);
        }

        /// <summary>How many pips stand under a stack of so many marks: one a mark, up to <see cref="MostPips"/>; none under a mark alone.</summary>
        public static int Pips(int marks) => marks < 2 ? 0 : Math.Min(marks, MostPips);

        /// <summary>How wide a pip stands under a mark of a size: 0.15 of it, never under three pixels, so it shows on the minimap.</summary>
        public static float PipSize(float size) => Math.Max(3f, size * 0.15f);

        /// <summary>Where a pip stands from its mark's middle, up being up: in a row under the mark's ring, centred, a pip and a half apart.</summary>
        public static (float X, float Y) PipAt(int pip, int pips, float size)
        {
            var width = PipSize(size);
            var step = width * 1.5f;
            return ((pip - (pips - 1) / 2f) * step, -(size / 2f + width * 0.7f));
        }

        /// <summary>How far from their spot a stack's marks fan out: a mark's width at least, more as there are more, so neighbours stay a mark's width apart.</summary>
        public static float FanRadius(int marks, float size) =>
            Math.Max(size * 0.95f, size * 1.05f / (2f * (float)Math.Sin(Math.PI / Math.Max(2, marks))));

        /// <summary>
        /// Where the <paramref name="mark"/>th of a stack's marks stands fanned out from its spot, up
        /// being up: round it, evenly, two to its left and right, three with one under and two
        /// over, four on the diagonals.
        /// </summary>
        public static (float X, float Y) FanAt(int mark, int marks, float size)
        {
            var radius = FanRadius(marks, size);
            var angle = Math.PI / 2 + Math.PI / marks + mark * 2 * Math.PI / marks;
            return ((float)(Math.Cos(angle) * radius), (float)(Math.Sin(angle) * radius));
        }

        /// <summary>Whether the pointer, so far from a fanned stack's spot, still rests on it: anywhere within its marks' reach and half a mark past.</summary>
        public static bool StaysFanned(float dx, float dy, int marks, float size)
        {
            var reach = FanRadius(marks, size) + size / 2f;
            return dx * dx + dy * dy <= reach * reach;
        }
    }
}
