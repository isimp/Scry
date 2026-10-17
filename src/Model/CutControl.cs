using System.Collections.Generic;

namespace Scry
{
    /// <summary>How the stage cuts what it shows, as its cut control has it.</summary>
    internal enum CutMode
    {
        /// <summary>The roof on, or in a room gone into its own roof.</summary>
        Closed,

        /// <summary>Cut from above, over a floor or level, or at head height in a room gone into.</summary>
        Top,

        /// <summary>Cut from the side, a dungeon's inside alone.</summary>
        Side,
    }

    /// <summary>
    /// The stage's cut control in its row of chips, one control for what was the roof button over
    /// the ruler and the two chips of a dungeon's cut: Closed, Top and, inside a dungeon, Side.
    /// </summary>
    internal static class CutControl
    {
        private static readonly CutMode[] All = { CutMode.Closed, CutMode.Top, CutMode.Side };
        private static readonly CutMode[] Above = { CutMode.Closed, CutMode.Top };

        /// <summary>The part lit: Side while cut from the side, Top while cut from above, else Closed.</summary>
        public static CutMode Shown(bool side, bool cutting) => side ? CutMode.Side : cutting ? CutMode.Top : CutMode.Closed;

        /// <summary>The parts offered: all three inside a dungeon, Closed and Top for any other place with floors.</summary>
        public static IReadOnlyList<CutMode> Offered(bool inside) => inside ? All : Above;
    }
}
