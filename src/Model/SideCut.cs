using System;

namespace Scry
{
    /// <summary>
    /// The cut from the side: an upright plane through a point, the room gone into's middle or
    /// the example's, facing away from the camera level with the ground, so the half of every room
    /// facing the camera is taken away and the far half shows, as a stack of rooms one under
    /// another is seen whole at once. It turns as the camera turns.
    /// </summary>
    internal static class SideCut
    {
        /// <summary>
        /// The way the cut faces: the camera's look level with the ground, unit length; looking
        /// straight down, the way the top of the picture is (<paramref name="up"/>) instead.
        /// </summary>
        public static Vec3 Facing(Vec3 forward, Vec3 up)
        {
            var x = forward.X;
            var z = forward.Z;
            if (Math.Sqrt(x * x + z * z) < 0.05)
            {
                x = up.X;
                z = up.Z;
            }
            var length = (float)Math.Sqrt(x * x + z * z);
            return length < 1e-6f ? new Vec3(0f, 0f, 1f) : new Vec3(x / length, 0f, z / length);
        }

        /// <summary>Whether a point is kept: on the cut or past it, away from the camera.</summary>
        public static bool Keeps(Vec3 point, Vec3 through, Vec3 facing) => Vec3.Dot(point - through, facing) >= 0f;
    }
}
