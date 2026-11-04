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
        public const float PersonSize = 1.8f;

        /// <summary>How far a thing of a size, its height or width whichever is larger (a creature's <c>Character.GetHeight</c>), is seen.</summary>
        public static float Reach(float size) => Math.Min(MostReach, PersonReach * Math.Max(1f, size / PersonSize));

        /// <summary>
        /// How far a creature or a thing is seen, a place not: within its size's reach, and within
        /// the world's seen reach, as the game names a creature within it and an item, a piece or a
        /// resource only on hover. A seen reach of 0 keeps no stars or gear but blinds no sight:
        /// the size's reach stands alone then.
        /// </summary>
        public static float ThingReach(float reach, float seenReach) => seenReach <= 0f ? reach : Math.Min(reach, seenReach);

        /// <summary>
        /// How far a creature in view is seen: as far as its size makes it out
        /// (<see cref="Reach"/>), or the world's seen reach where that is farther. The seen reach,
        /// where the game names a creature and marks its stars, keeps only what is told up close:
        /// its stars, what it carries and its attacks begun.
        /// </summary>
        public static float CreatureReach(float size, float seenReach) => Math.Max(Reach(size), seenReach);

        /// <summary>
        /// How far a place in view is seen: within its radius and <see cref="PersonReach"/> beyond,
        /// or as far as its height makes it out, as a landmark stands out over the ground far past
        /// its radius (a rock spire 4 m round and 40 m tall).
        /// </summary>
        public static float PlaceReach(float radius, float height) => Math.Max(radius + PersonReach, Reach(height));

        /// <summary>
        /// How far the eye is from the nearest part of a thing standing upright at its foot, its box
        /// from <paramref name="bottom"/> to <paramref name="top"/> over the foot and
        /// <paramref name="radius"/> round it, squared: as the game names a thing where the eye's
        /// ray meets it (<c>Player.FindHoverObject</c>), not by its middle, which a tree carries far overhead.
        /// </summary>
        public static float NearestSq(Vec3 eye, Vec3 foot, float bottom, float top, float radius)
        {
            var dx = eye.X - foot.X;
            var dz = eye.Z - foot.Z;
            var across = Math.Max(0f, (float)Math.Sqrt(dx * dx + dz * dz) - radius);
            var low = foot.Y + bottom;
            var high = foot.Y + top;
            var up = eye.Y < low ? low - eye.Y : eye.Y > high ? eye.Y - high : 0f;
            return across * across + up * up;
        }

        /// <summary>Where a thing standing upright is looked at: on its upright at the eye's height, kept within its own height.</summary>
        public static Vec3 Aim(Vec3 eye, Vec3 foot, float bottom, float top) =>
            new Vec3(foot.X, Math.Min(foot.Y + top, Math.Max(foot.Y + bottom, eye.Y)), foot.Z);

        /// <summary>
        /// How far a creature's attack is seen begun (<c>CompendiumSight.SwingSeen</c>): within the
        /// world's seen reach, which starts at the distance the game's enemy hud names a creature
        /// within (<paramref name="hudReach"/>); a boss's as much farther as the hud shows a boss's
        /// health bar (<paramref name="bossHudReach"/>), the fight's own reach; and any as far as
        /// its creature uses it from (<paramref name="attackRange"/>, the attack item's
        /// <c>m_aiAttackRange</c>), as a taunt or a spit begun at 20 m is in plain view and may
        /// hurt no one. A seen reach of 0 keeps none, a boss's neither; with no hud reach to scale
        /// by, a boss's is seen as any.
        /// </summary>
        public static float AttackReach(bool boss, float seenReach, float hudReach, float bossHudReach, float attackRange = 0f)
        {
            if (seenReach <= 0f) return 0f;
            var reach = boss && hudReach > 0f ? seenReach * bossHudReach / hudReach : seenReach;
            return Math.Max(reach, attackRange);
        }

        /// <summary>
        /// Where a place is looked for, from its middle: there and four points half way out to its
        /// edge, a person's height over its ground, so one in a dip or behind a low wall is looked
        /// for where it stands up, and a big one half hidden is still found.
        /// </summary>
        public static (float X, float Y, float Z)[] PlacePoints(float radius, float height = 0f)
        {
            var half = radius * 0.5f;
            var points = new[] { (0f, PersonSize, 0f), (half, PersonSize, 0f), (-half, PersonSize, 0f), (0f, PersonSize, half), (0f, PersonSize, -half) };
            // One standing tall is looked for three quarters of the way up too, as it shows over what hides its foot.
            if (height <= 2f * PersonSize) return points;
            var high = new (float, float, float)[points.Length + 1];
            points.CopyTo(high, 0);
            high[points.Length] = (0f, height * 0.75f, 0f);
            return high;
        }

        /// <summary>
        /// Whether a place is in sight along one line from the eye: stopped by nothing, or by
        /// something within the place's own ground, its walls, roof or the ground in it, which is
        /// seeing it; the stop given from the place's middle across the ground.
        /// </summary>
        public static bool PlaceInSight(bool blocked, float offX, float offZ, float radius) => !blocked || offX * offX + offZ * offZ <= radius * radius;

        /// <summary>How far over where a deed happened the eye looks for it, in metres: a creature's or a person's middle.</summary>
        public const float DeedHeight = 1f;

        /// <summary>How near the point looked at the line may stop and the deed still be seen, in metres: what it was done to, or by, standing in the way.</summary>
        public const float DeedNear = 1.5f;

        /// <summary>Whether a deed is in sight: the line from the eye meets nothing solid, or stops within <see cref="DeedNear"/> of the point (<paramref name="hitOff"/>, how far from it).</summary>
        public static bool DeedInSight(bool blocked, float hitOff) => !blocked || hitOff <= DeedNear;
    }
}
