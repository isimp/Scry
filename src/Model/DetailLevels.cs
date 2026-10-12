namespace Scry
{
    /// <summary>
    /// A part's levels of detail (<c>LODGroup</c>), by the share of the picture's height under
    /// which each gives way to the next, the last to nothing. The game culls a part once it is
    /// small on screen (a cave room's rocks below a tenth to a third of the picture's height),
    /// which a whole place zoomed out on the stage soon is; what stands on the stage keeps its
    /// last level at any size, still turning to its lesser ones as it shrinks.
    /// </summary>
    internal static class DetailLevels
    {
        /// <summary>The heights with the last one 0, so the last level is never culled; null where there is none to change.</summary>
        public static float[] NeverCulled(float[] heights)
        {
            if (heights.Length == 0 || heights[heights.Length - 1] <= 0f) return null;
            var kept = (float[])heights.Clone();
            kept[kept.Length - 1] = 0f;
            return kept;
        }
    }
}
