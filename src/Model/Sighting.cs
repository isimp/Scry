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
    }
}
