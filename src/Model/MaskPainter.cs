using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The ground's mask painted in parts (<see cref="StageGround.PaintMask"/>): its paints noted
    /// once in the cells of a coarse grid each may reach, in the game's order, then so many rows
    /// at a time, each point weighing only the paints of its cell, so a camp's hundreds of paints
    /// are laid over frames; the mask is the same as painted at once.
    /// </summary>
    internal sealed class MaskPainter
    {
        /// <summary>The cells each side of the ground's mask is split into, to note which paints reach each.</summary>
        private const int MaskCells = 32;

        private readonly float _middleX, _middleZ, _across;
        private readonly IReadOnlyList<GroundPaint> _paints;
        private readonly (float R, float G, float B, float A) _bare;
        private readonly int _cells;
        private readonly List<int>[] _reach;
        private readonly List<GroundPaint> _near = new List<GroundPaint>();

        /// <summary>The points each side of the mask.</summary>
        public int Size { get; }

        /// <summary>A mask of size by size points across a square <paramref name="across"/> wide around a middle, by these paints over bare ground.</summary>
        public MaskPainter(int size, float middleX, float middleZ, float across, IReadOnlyList<GroundPaint> paints, (float R, float G, float B, float A) bare)
        {
            Size = size;
            _middleX = middleX;
            _middleZ = middleZ;
            _across = across;
            _paints = paints;
            _bare = bare;
            _cells = Math.Min(size, MaskCells);
            _reach = new List<int>[_cells * _cells];
            for (var n = 0; n < paints.Count; n++)
            {
                var paint = paints[n];
                if (!(paint.Radius >= 0f)) continue;
                var (i0, i1) = StageGround.Span(paint.X, paint.Radius, middleX, across, size);
                var (j0, j1) = StageGround.Span(paint.Z, paint.Radius, middleZ, across, size);
                if (i0 > i1 || j0 > j1) continue;
                for (var cj = j0 * _cells / size; cj <= j1 * _cells / size; cj++)
                {
                    for (var ci = i0 * _cells / size; ci <= i1 * _cells / size; ci++)
                    {
                        var cell = cj * _cells + ci;
                        (_reach[cell] ?? (_reach[cell] = new List<int>())).Add(n);
                    }
                }
            }
        }

        /// <summary>Paints the rows from one up to another, not including it, into the points given, written row by row.</summary>
        public void PaintRows((float R, float G, float B, float A)[] into, int from, int to)
        {
            for (var j = Math.Max(0, from); j < Math.Min(Size, to); j++)
            {
                var z = _middleZ + ((j + 0.5f) / Size - 0.5f) * _across;
                var row = j * _cells / Size * _cells;
                for (var i = 0; i < Size; i++)
                {
                    var x = _middleX + ((i + 0.5f) / Size - 0.5f) * _across;
                    _near.Clear();
                    var those = _reach[row + i * _cells / Size];
                    if (those != null)
                    {
                        foreach (var n in those)
                        {
                            var paint = _paints[n];
                            if (Math.Abs(x - paint.X) <= paint.Radius && Math.Abs(z - paint.Z) <= paint.Radius) _near.Add(paint);
                        }
                    }
                    into[j * Size + i] = _near.Count == 0 ? _bare : StageGround.Painted(_bare, x, z, _near);
                }
            }
        }
    }
}
