namespace Scry
{
    /// <summary>
    /// Where the plan box's controls stand: its header holds the views from the left and, in the
    /// box's top right corner, grow and fold, on the views' row where they fit beside them, else on
    /// a row of their own under it, still in the corner; the plan's own switches, the rooms below
    /// (from above only) and the names, stand joined in the plan's top right corner, the names
    /// outermost. Places are from the left edge of the box's inside, or of the plan's.
    /// </summary>
    internal static class PlanBoxHeader
    {
        /// <summary>How the header is laid out: how many rows, which row grow and fold stand on, and where they stand.</summary>
        internal struct Laid
        {
            public int Rows;
            public int ControlsRow;
            public float GrowX;
            public float FoldX;
        }

        /// <summary>The header in a box's inside of a width, its views as wide as <paramref name="views"/>, its buttons square, a gap between them.</summary>
        public static Laid Lay(float width, float views, float button, float gap)
        {
            var fold = width - button;
            var oneRow = views + gap + button + gap + button <= width;
            return new Laid { Rows = oneRow ? 1 : 2, ControlsRow = oneRow ? 0 : 1, FoldX = fold, GrowX = fold - gap - button };
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
