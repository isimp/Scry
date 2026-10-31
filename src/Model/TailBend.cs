using System;

namespace Scry
{
    /// <summary>
    /// How the game bends a tail every frame, after its animator (<c>Tail.CustomLateUpdate</c>):
    /// a Serpent's body or a Leech's, joint by joint from the head back. Each joint sinks, fast
    /// out of the water and slowly in it, then is set back on its parent's line, at most an angle
    /// off it, kept its radius over the ground, a share of the way a frame. Positions are in
    /// metres; the stage gives its own sea's surface and ground in place of the world's.
    /// </summary>
    internal static class TailBend
    {
        /// <summary>A tail's settings, as the game's prefab holds them.</summary>
        internal struct Rules
        {
            /// <summary>How far a joint may bend off its parent's line, in degrees.</summary>
            public float MaxAngle;

            /// <summary>How fast a joint sinks out of the water, and in it, in metres a second.</summary>
            public float Gravity, GravityInWater;

            /// <summary>Whether a joint sinks slower in water, and is kept over the ground.</summary>
            public bool WaterCheck, GroundCheck;

            /// <summary>The share of the way to where it belongs a joint moves a frame.</summary>
            public float Smoothness;

            /// <summary>How thick the tail is: how far a joint reaching out of the water counts as out, and how far over the ground it stays.</summary>
            public float Radius;
        }

        /// <summary>
        /// Where a joint moves this frame, from where it was, its parent's position and line (the
        /// way its parent points to it), its length, the water's surface (NaN for none) and the
        /// ground's height under it.
        /// </summary>
        public static Vec3 Next(Vec3 was, Vec3 parent, Vec3 parentLine, float length, float water, float ground, Rules rules, float dt)
        {
            var pos = was;
            // Out of the water where it reaches over the surface by its radius, or where there is no water.
            var inWater = rules.WaterCheck && !float.IsNaN(water) && pos.Y + rules.Radius <= water;
            pos.Y -= (inWater ? rules.GravityInWater : rules.Gravity) * dt;

            var half = length * 0.5f;
            var middle = parent + parentLine * half;
            var toward = Turned(parentLine * -1f, Unit(middle - pos), rules.MaxAngle * (float)Math.PI / 180f);
            var at = middle - toward * half;
            if (rules.GroundCheck && at.Y - rules.Radius < ground) at.Y = ground + rules.Radius;
            // The share of the way is held between none and all, as the game's Vector3.Lerp holds it.
            return pos + (at - pos) * Math.Max(0f, Math.Min(1f, rules.Smoothness));
        }

        /// <summary>The way <paramref name="from"/> turned toward <paramref name="to"/>, by at most an angle in radians; both of length one.</summary>
        private static Vec3 Turned(Vec3 from, Vec3 to, float most)
        {
            var dot = Math.Max(-1f, Math.Min(1f, Vec3.Dot(from, to)));
            if ((float)Math.Acos(dot) <= most) return to;
            var side = to - from * dot;
            // Straight back the other way: any side will do.
            if (side.Length < 1e-6f) side = Math.Abs(from.Y) < 0.9f ? new Vec3(0f, 1f, 0f) - from * from.Y : new Vec3(1f, 0f, 0f) - from * from.X;
            return from * (float)Math.Cos(most) + Unit(side) * (float)Math.Sin(most);
        }

        /// <summary>The way a vector points, of length one; none for none.</summary>
        private static Vec3 Unit(Vec3 a) => a.Length > 1e-6f ? a * (1f / a.Length) : new Vec3(0f, 0f, 0f);
    }
}
