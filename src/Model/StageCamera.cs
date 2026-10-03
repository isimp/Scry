using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The stage camera's moves. It circles a point it looks at from a distance. The wheel zooms
    /// toward what is under the pointer, which stays under it; a drag with the right button moves
    /// the view so what was grabbed follows the pointer, along the floor where one is opened; with
    /// a floor opened the camera looks at that floor, framed on what stands on it. A floor's cut
    /// leaves the creatures whole: drawn again, they keep only what is above the cut
    /// (<see cref="AboveCutRow"/>).
    /// </summary>
    internal static class StageCamera
    {
        /// <summary>How near the wheel brings the camera to what it looks at, in metres, however big what is framed.</summary>
        public const float NearestMetres = 2f;

        /// <summary>
        /// How near the wheel brings the camera, as a share of the distance it frames from: as
        /// near as <see cref="NearestMetres"/>, never nearer than a five-hundredth, and a small
        /// model no nearer than a seventh.
        /// </summary>
        public static float LeastZoom(float framedDistance) =>
            framedDistance > 0f ? Math.Max(0.002f, Math.Min(0.15f, NearestMetres / framedDistance)) : 0.15f;

        /// <summary>
        /// The point the camera circles once zoomed toward <paramref name="point"/> by
        /// <paramref name="factor"/>, the new distance over the old: the point stays where it is
        /// in the picture, as the camera keeps its turn.
        /// </summary>
        public static Vec3 ZoomToward(Vec3 pivot, Vec3 point, float factor) => point + (pivot - point) * factor;

        /// <summary>
        /// Where a ray meets the level at height <paramref name="y"/>, no farther off than
        /// <paramref name="farthest"/>; null where it runs away from it or along it.
        /// </summary>
        public static Vec3? OnLevel(Vec3 from, Vec3 direction, float y, float farthest)
        {
            if (Math.Abs(direction.Y) < 1e-6f) return null;
            var t = (y - from.Y) / direction.Y;
            if (t <= 0f) return null;
            var at = from + direction * t;
            return Vec3.Distance(from, at) <= farthest ? at : (Vec3?)null;
        }

        /// <summary>Where a ray meets the plane through <paramref name="through"/> that faces the camera looking along <paramref name="forward"/>; null where it looks away.</summary>
        public static Vec3? OnFacing(Vec3 from, Vec3 direction, Vec3 through, Vec3 forward)
        {
            var toward = Vec3.Dot(direction, forward);
            if (toward <= 1e-6f) return null;
            var t = Vec3.Dot(through - from, forward) / toward;
            return t > 0f ? from + direction * t : (Vec3?)null;
        }

        /// <summary>
        /// How far the view moves along a floor for the pointer moved from one ray to another:
        /// what was under it on the floor stays under it. Null where either ray misses the floor.
        /// </summary>
        public static Vec3? DragOnLevel(Vec3 eye, Vec3 before, Vec3 now, float y, float farthest)
        {
            var grabbed = OnLevel(eye, before, y, farthest);
            var under = OnLevel(eye, now, y, farthest);
            if (grabbed == null || under == null) return null;
            return grabbed.Value - under.Value;
        }

        /// <summary>
        /// How far the view moves, right and up, for the pointer moved across the picture by
        /// shares of its width and height, at a distance from the camera: what is under the
        /// pointer there goes with it.
        /// </summary>
        public static (float Right, float Up) Drag(float across, float up, float distance, float fieldOfView, float aspect)
        {
            var tall = 2f * distance * (float)Math.Tan(fieldOfView * 0.5 * Math.PI / 180.0);
            return (-across * tall * aspect, -up * tall);
        }

        /// <summary>How far over a floor's cut the camera stays, in metres.</summary>
        public const float OverCutMetres = 0.5f;

        /// <summary>
        /// How far off the camera stays, looking down <paramref name="pitch"/> degrees at a point
        /// <paramref name="cutAbove"/> metres under a floor's cut, to stay over it, as a cut only
        /// opens what is seen from above it; 0 where the cut is no higher, or it looks level or up.
        /// </summary>
        public static float OverCut(float cutAbove, float pitch)
        {
            var up = cutAbove + OverCutMetres;
            if (up <= 0f || pitch <= 5f) return 0f;
            return up / (float)Math.Sin(pitch * Math.PI / 180.0);
        }

        /// <summary>The middle of what stands on a floor, across, and how far it reaches from there: half the way from corner to corner. Null for nothing.</summary>
        public static (float X, float Z, float Radius)? Across(IEnumerable<Vec3> corners)
        {
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            foreach (var corner in corners)
            {
                minX = Math.Min(minX, corner.X);
                maxX = Math.Max(maxX, corner.X);
                minZ = Math.Min(minZ, corner.Z);
                maxZ = Math.Max(maxZ, corner.Z);
            }
            if (float.IsPositiveInfinity(minX)) return null;
            var wide = maxX - minX;
            var deep = maxZ - minZ;
            return ((minX + maxX) / 2f, (minZ + maxZ) / 2f, (float)Math.Sqrt(wide * wide + deep * deep) / 2f);
        }

        /// <summary>
        /// The depth row of a projection that draws only what is above a cut, its far end laid
        /// along the cut and its near end <paramref name="near"/> ahead of the camera. The cut is
        /// the plane (<paramref name="a"/>, <paramref name="b"/>, <paramref name="c"/>) facing up
        /// in the camera's own space (looking along -Z), <paramref name="height"/> below the
        /// camera. Nothing above a cut can be hidden by what is below it, as the camera looks
        /// from above it, so what is drawn this way goes over the picture. Null with the camera
        /// not above the cut.
        /// </summary>
        public static (float X, float Y, float Z, float W)? AboveCutRow(float a, float b, float c, float height, float near)
        {
            if (height <= 0f) return null;
            var scale = -2f * near / height;
            return (scale * a, scale * b, scale * c - 1f, scale * height);
        }
    }
}
