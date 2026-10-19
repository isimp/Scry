namespace Scry
{
    /// <summary>
    /// How far to turn a body about its up axis so its front faces +Z, where the stage's camera
    /// starts, from where its left and right legs stand in its own space (seen from above).
    /// </summary>
    internal static class FrontTurn
    {
        /// <summary>Degrees to turn by, positive turning +Z towards +X; none where the legs stand on one spot.</summary>
        public static float Degrees(float leftX, float leftZ, float rightX, float rightZ)
        {
            // Right of the body across its legs; its front is that turned a quarter, up crossed in.
            float acrossX = rightX - leftX, acrossZ = rightZ - leftZ;
            if (acrossX * acrossX + acrossZ * acrossZ < 1e-8f) return 0f;
            var facing = System.Math.Atan2(-acrossZ, acrossX) * 180.0 / System.Math.PI;
            return (float)-facing;
        }
    }
}
