using System;

namespace Scry
{
    /// <summary>
    /// How far a thing in view is seen, for the compendium, a creature or anything else: one of a
    /// person's size or smaller within <see cref="PersonReach"/>, a bigger one as much farther as
    /// it is bigger, since it looks at that distance as a person does nearer, up to <see cref="MostReach"/>.
    /// </summary>
    internal static class Sighting
    {
        /// <summary>How far a thing of a person's size or smaller in view is seen.</summary>
        public const float PersonReach = 30f;

        /// <summary>How far the biggest thing in view is seen.</summary>
        public const float MostReach = 150f;

        /// <summary>A person's size, as the game's player is tall.</summary>
        private const float PersonSize = 1.8f;

        /// <summary>How far a thing of a size, its height or width whichever is larger (a creature's <c>Character.GetHeight</c>), is seen.</summary>
        public static float Reach(float size) => Math.Min(MostReach, PersonReach * Math.Max(1f, size / PersonSize));

        /// <summary>
        /// Where a place is looked for, from its middle: there and four points half way out to its
        /// edge, a person's height over its ground, so one in a dip or behind a low wall is looked
        /// for where it stands up, and a big one half hidden is still found.
        /// </summary>
        public static (float X, float Y, float Z)[] PlacePoints(float radius)
        {
            var half = radius * 0.5f;
            return new[] { (0f, PersonSize, 0f), (half, PersonSize, 0f), (-half, PersonSize, 0f), (0f, PersonSize, half), (0f, PersonSize, -half) };
        }

        /// <summary>
        /// Whether a place is in sight along one line from the eye: stopped by nothing, or by
        /// something within the place's own ground, its walls, roof or the ground in it, which is
        /// seeing it; the stop given from the place's middle across the ground.
        /// </summary>
        public static bool PlaceInSight(bool blocked, float offX, float offZ, float radius) => !blocked || offX * offX + offZ * offZ <= radius * radius;
    }
}
