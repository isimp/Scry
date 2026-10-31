namespace Scry
{
    /// <summary>
    /// A pickable's regrowth as met in play: coming back to it bearing again. The game keeps when
    /// a pickable was last picked after it bears again (<c>Pickable.SetPicked</c> sets it only on
    /// a pick, never back), so one bearing that was picked before has grown back.
    /// </summary>
    internal static class Regrowth
    {
        /// <summary>Whether a pickable seen now, picked or bearing, with the time it was last picked as the game keeps it, has grown back since.</summary>
        public static bool SeenBearingAgain(bool picked, long pickedTicks) => !picked && pickedTicks > 1;
    }
}
