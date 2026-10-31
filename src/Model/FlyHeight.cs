using System;

namespace Scry
{
    /// <summary>
    /// How high over its ground a creature flies on the stage: one the game starts flying
    /// (<c>Character.m_flying</c>) takes off and is kept at least its least flying altitude over
    /// the ground under it (<c>BaseAI.m_flyAltitudeMin</c>, as <c>BaseAI.MoveTo</c> keeps a flyer's
    /// way over the solid ground); one that walks stands on it.
    /// </summary>
    internal static class FlyHeight
    {
        public static float Over(bool flies, float flyAltitudeMin) => flies ? Math.Max(0f, flyAltitudeMin) : 0f;
    }
}
