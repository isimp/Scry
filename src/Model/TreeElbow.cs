using System;

namespace Scry
{
    /// <summary>
    /// A way of the plan's tree drawn square, as the plan's walkways are: from the higher node
    /// down to half way over the lower node's row, across, and down into the lower node; straight
    /// down where one stands under the other. Two nodes of one row are joined under it, or over it
    /// on the last row, where the box ends.
    /// </summary>
    internal static class TreeElbow
    {
        /// <summary>The points of the way between two nodes' middles, as far apart as the rows are, each node as tall as twice <paramref name="half"/>; joined under a shared row where <paramref name="below"/>, else over it.</summary>
        public static (float X, float Y)[] Points(float ax, float ay, float bx, float by, float half, float rowGap, bool below)
        {
            if (Math.Abs(ay - by) < 0.5f)
            {
                var side = below ? 1f : -1f;
                var across = ay + side * rowGap / 2f;
                return new[] { (ax, ay + side * half), (ax, across), (bx, across), (bx, by + side * half) };
            }
            if (by < ay)
            {
                (ax, bx) = (bx, ax);
                (ay, by) = (by, ay);
            }
            if (Math.Abs(ax - bx) < 0.5f) return new[] { (ax, ay + half), (bx, by - half) };
            var turn = by - rowGap / 2f;
            return new[] { (ax, ay + half), (ax, turn), (bx, turn), (bx, by - half) };
        }
    }
}
