using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The amber line round the room gone into on the stage: every edge of its box, from its
    /// ground to its top or to just under the cut where that is lower, so the room can be found
    /// among others that look alike, and the cut never clips the line.
    /// </summary>
    internal static class RoomOutline
    {
        /// <summary>How far under the cut the outline's top runs, in metres.</summary>
        public const float UnderCut = 0.05f;

        /// <summary>The outline's top: the room's own top, or just under the cut where that is lower.</summary>
        public static float Top(float cut, float roomTop) => Math.Min(cut - UnderCut, roomTop);

        /// <summary>
        /// One line through every edge of a room's box, from the corners of its floor seen from
        /// above, in order round it, at its ground and at its top; only its floor where its top is
        /// no higher; none without four corners.
        /// </summary>
        public static List<Vec3> Path(IReadOnlyList<Vec3> corners, float ground, float top)
        {
            var path = new List<Vec3>();
            if (corners.Count != 4) return path;
            Vec3 Low(int i) => new Vec3(corners[i % 4].X, ground, corners[i % 4].Z);
            Vec3 High(int i) => new Vec3(corners[i % 4].X, top, corners[i % 4].Z);
            for (var i = 0; i <= 4; i++) path.Add(Low(i));
            if (top <= ground) return path;
            // Up a corner, round the top, down and up again at each of the others.
            path.Add(High(0));
            for (var i = 1; i <= 3; i++)
            {
                path.Add(High(i));
                path.Add(Low(i));
                path.Add(High(i));
            }
            path.Add(High(0));
            return path;
        }
    }
}
