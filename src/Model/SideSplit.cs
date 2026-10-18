using System;

namespace Scry
{
    /// <summary>
    /// How tall the stage at the top of a page is: as tall as it is dragged, no shorter than its
    /// least, no more than <see cref="MostShare"/> of the page, and never so tall that the page's
    /// header under it and a little of what follows lose their room, so the header stays inside
    /// the panel however far the stage is dragged. On a page too short for both the stage keeps
    /// its least.
    /// </summary>
    internal static class SideSplit
    {
        /// <summary>The share of the page the stage takes at the most.</summary>
        public const float MostShare = 0.85f;

        /// <summary>The stage's height for the height wanted, the page's, the stage's least and the room kept under it.</summary>
        public static float StageHeight(float wanted, float side, float least, float keep) =>
            Math.Max(least, Math.Min(wanted, Math.Min(side * MostShare, side - keep)));
    }
}
