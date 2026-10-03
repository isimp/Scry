namespace Scry
{
    /// <summary>
    /// Chips laid out as the panel lays every row of them out (links, clips, effects, buttons,
    /// segments): from a left edge to the right, each a gap after the last; one that would pass
    /// the right edge starts the next row at the left edge again, unless it is its row's first,
    /// which stands there however wide. Rows are a height and a gap apart. The panel measures
    /// each chip and draws it where the flow places it; the flow only places, and makes nothing,
    /// as it runs for every row each time the panel is drawn.
    /// </summary>
    internal struct ChipFlow
    {
        private readonly float _left;
        private readonly float _right;
        private readonly float _rowHeight;
        private readonly float _gap;
        private readonly float _rowGap;

        /// <summary>A flow from its left edge, or from <paramref name="start"/> where something else stands first in its first row.</summary>
        public ChipFlow(float left, float right, float top, float rowHeight, float gap, float rowGap, float? start = null)
        {
            _left = left;
            _right = right;
            _rowHeight = rowHeight;
            _gap = gap;
            _rowGap = rowGap;
            X = start ?? left;
            Y = top;
        }

        /// <summary>Where the next chip would start, and the top of the row it would stand in.</summary>
        public float X { get; private set; }

        public float Y { get; private set; }

        /// <summary>Where a chip of a width goes, its left and its top; the flow moves on past it and its gap.</summary>
        public (float X, float Y) Place(float width)
        {
            if (X + width > _right && X > _left)
            {
                X = _left;
                Y += _rowHeight + _rowGap;
            }
            var at = (X, Y);
            X += width + _gap;
            return at;
        }

        /// <summary>How tall each row is, which its chips are drawn at.</summary>
        public float RowHeight => _rowHeight;

        /// <summary>Whether the row holds a chip already.</summary>
        public bool InRow => X > _left;

        /// <summary>Below the flow: its last row's bottom, or its top when it holds nothing.</summary>
        public float Below => InRow ? Y + _rowHeight : Y;

        /// <summary>The bottom of the row it is in, whether or not anything stands in it.</summary>
        public float RowBottom => Y + _rowHeight;
    }
}
