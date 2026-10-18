namespace Scry
{
    /// <summary>
    /// Where the plan box's controls stand: its header holds the views from the left and, in the
    /// box's top right corner, grow and fold beside them, the views at their width where that
    /// leaves room, else drawn tighter; only where even that leaves none do grow and fold take a
    /// row of their own under the views, still in the corner. The plan's own switches, the rooms
    /// below (from above only) and the names, stand joined in the plan's top right corner, the
    /// names outermost. Places are from the left edge of the box's inside, or of the plan's.
    /// </summary>
    internal static class PlanBoxHeader
    {
        /// <summary>How the header is laid out: how many rows, whether the views are drawn tighter, which row grow and fold stand on, and where they stand.</summary>
        internal struct Laid
        {
            public int Rows;
            public bool Tight;
            public int ControlsRow;
            public float GrowX;
            public float FoldX;
        }

        /// <summary>
        /// The header in a box's inside of a width, its views as wide as <paramref name="views"/>,
        /// or <paramref name="tight"/> drawn tighter, its buttons square, a gap between them.
        /// </summary>
        public static Laid Lay(float width, float views, float tight, float button, float gap)
        {
            var fold = width - button;
            var controls = gap + button + gap + button;
            var wide = views + controls <= width;
            var oneRow = wide || tight + controls <= width;
            return new Laid { Rows = oneRow ? 1 : 2, Tight = oneRow && !wide, ControlsRow = oneRow ? 0 : 1, FoldX = fold, GrowX = fold - gap - button };
        }

        /// <summary>The plan's own switches in its top right corner, joined: the names outermost, the rooms below (none where it is not shown, NaN) inside it, and the group's left edge.</summary>
        public static (float NamesX, float BelowX, float Left) Corner(float planWidth, float button, bool below)
        {
            var names = planWidth - button;
            var belowX = below ? names - button : float.NaN;
            return (names, belowX, below ? belowX : names);
        }
    }
}
