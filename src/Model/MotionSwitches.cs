namespace Scry
{
    /// <summary>
    /// The switches the game tells a creature's animator every frame of how it moves: flying and
    /// out of the water while it flies (<c>Character.UpdateFlying</c>), in water while it swims
    /// (<c>Character.UpdateSwimming</c>, <see cref="SeaHold.InWater"/>), neither while it walks.
    /// </summary>
    internal static class MotionSwitches
    {
        /// <summary>What a creature's animator is told: in water, and flying; a creature that <paramref name="flies"/> flies whatever lies under it.</summary>
        public static (bool InWater, bool Flying) Told(bool flies, SeaHeld held, bool seaShown, bool livesInWater) =>
            flies ? (false, true) : (SeaHold.InWater(held, seaShown, livesInWater), false);
    }
}
