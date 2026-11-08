using System;

namespace Scry
{
    /// <summary>What one spot on the map is to the player: as any other, crossed off, or highlighted.</summary>
    internal enum SpotMode
    {
        Plain,
        Crossed,
        Highlighted,
    }

    /// <summary>How the spots crossed off stand on the map.</summary>
    internal enum CrossedMarks
    {
        Faded,
        Hidden,
    }

    /// <summary>
    /// A spot's mode on the map (<see cref="SpotMode"/>), given by a right click and kept with the
    /// character for its world: how the modes follow each other, how bright and how big each stands,
    /// and how each merges far out: a kind's spots only with those of their own mode, so a plain
    /// mark's count holds no crossed one, and a highlighted spot never, so it stays where it can be seen.
    /// </summary>
    internal static class SpotModes
    {
        /// <summary>How near, in metres each way, a spot takes the mode kept for another: what the server told of and what you saw of one thing lie apart by rounding.</summary>
        public const int Near = 2;

        /// <summary>How much of its look a crossed mark keeps.</summary>
        public const float CrossedShare = 0.45f;

        /// <summary>How much bigger a highlighted mark stands, its ring's width of a mark's, and a cross's.</summary>
        public const float HighlightScale = 1.35f, RingScale = 2.1f, CrossScale = 0.9f;

        /// <summary>The mode a right click gives a spot in another.</summary>
        public static SpotMode Next(SpotMode mode) =>
            mode == SpotMode.Plain ? SpotMode.Crossed : mode == SpotMode.Crossed ? SpotMode.Highlighted : SpotMode.Plain;

        /// <summary>How bright a spot of a mode stands, its kind standing so bright: a crossed one no brighter than its share.</summary>
        public static float ShareOf(SpotMode mode, float share) => mode == SpotMode.Crossed ? Math.Min(share, CrossedShare) : share;

        /// <summary>The group a spot merges in, of a kind's number, by its mode: a highlighted one in a group of its own.</summary>
        public static int MergeGroup(int kind, SpotMode mode, int dot) => mode == SpotMode.Highlighted ? -1 - dot : kind * 3 + (int)mode;
    }
}
