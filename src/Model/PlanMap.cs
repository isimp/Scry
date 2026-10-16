using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The plan box's map of a dungeon's example: each room across against its depth, the ground
    /// at the top and the deepest room at the bottom. Across is one way round the level, at an
    /// angle that can be turned to any; the other way round the level is flattened, so rooms
    /// standing one behind the other at one height cover each other. Least overlap turns it to
    /// the angle where the fewest do; following the view turns it with the stage.
    /// </summary>
    internal static class PlanMap
    {
        /// <summary>The angles Least overlap tries, in degrees apart.</summary>
        public const float Step = 5f;

        /// <summary>How far two rooms must reach into each other across to cover each other, so rooms wall to wall do not.</summary>
        public const float Touch = 0.05f;

        /// <summary>How many degrees the map turns for each pixel it is dragged across.</summary>
        public const float PerPixel = 0.8f;

        /// <summary>An angle from 0 up to a whole turn.</summary>
        public static float Normal(float angle)
        {
            var turned = angle % 360f;
            return turned < 0f ? turned + 360f : turned;
        }

        /// <summary>How far across a room reaches at an angle: its box's corners seen along the direction the angle points, from the example's middle.</summary>
        public static (float Min, float Max) Across(PlacedRoom room, float angle)
        {
            var a = angle * Math.PI / 180.0;
            var cos = (float)Math.Cos(a);
            var sin = (float)Math.Sin(a);
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (var corner in room.Corners())
            {
                var along = corner.X * cos + corner.Z * sin;
                min = Math.Min(min, along);
                max = Math.Max(max, along);
            }
            return (min, max);
        }

        /// <summary>How high a room reaches: its box's foot and top.</summary>
        public static (float Foot, float Top) Upright(PlacedRoom room) =>
            (room.Position.Y - room.Room.Size.Y / 2f, room.Position.Y + room.Room.Size.Y / 2f);

        /// <summary>How high and how deep the rooms reach together: the highest top and the lowest foot.</summary>
        public static (float Top, float Bottom) Heights(IEnumerable<PlacedRoom> rooms)
        {
            float top = float.NegativeInfinity, bottom = float.PositiveInfinity;
            foreach (var room in rooms)
            {
                var (foot, roof) = Upright(room);
                top = Math.Max(top, roof);
                bottom = Math.Min(bottom, foot);
            }
            return float.IsNegativeInfinity(top) ? (0f, 0f) : (top, bottom);
        }

        /// <summary>How many two rooms cover each other on the map at an angle: reaching into each other across, and sharing heights.</summary>
        public static int Overlaps(IReadOnlyList<PlacedRoom> rooms, float angle)
        {
            var across = new (float Min, float Max)[rooms.Count];
            var upright = new (float Foot, float Top)[rooms.Count];
            for (var i = 0; i < rooms.Count; i++)
            {
                across[i] = Across(rooms[i], angle);
                upright[i] = Upright(rooms[i]);
            }
            var count = 0;
            for (var a = 0; a < rooms.Count; a++)
            {
                for (var b = a + 1; b < rooms.Count; b++)
                {
                    var sideBySide = across[a].Max - Touch <= across[b].Min || across[b].Max - Touch <= across[a].Min;
                    var oneAbove = upright[a].Top <= upright[b].Foot || upright[b].Top <= upright[a].Foot;
                    if (!sideBySide && !oneAbove) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// The angle where the fewest rooms cover each other, of every <see cref="Step"/> degrees
        /// round; of those as good, the nearest the angle shown, then the smaller. Half a turn
        /// round shows as many, so only half a turn is counted.
        /// </summary>
        public static float LeastOverlap(IReadOnlyList<PlacedRoom> rooms, float current)
        {
            current = Normal(current);
            var best = current;
            var fewest = int.MaxValue;
            var nearest = float.MaxValue;
            for (var angle = 0f; angle < 180f; angle += Step)
            {
                var count = Overlaps(rooms, angle);
                foreach (var candidate in new[] { angle, angle + 180f })
                {
                    var off = Apart(candidate, current);
                    if (count < fewest || count == fewest && (off < nearest || off == nearest && candidate < best))
                    {
                        fewest = count;
                        nearest = off;
                        best = candidate;
                    }
                }
            }
            return best;
        }

        /// <summary>How far apart two angles are, the short way round.</summary>
        public static float Apart(float a, float b)
        {
            var off = Math.Abs(Normal(a) - Normal(b));
            return Math.Min(off, 360f - off);
        }

        /// <summary>The angle after dragging the map across by so many pixels, scaled to the panel.</summary>
        public static float Turned(float angle, float dragged) => Normal(angle + dragged * PerPixel);

        /// <summary>
        /// The angle that shows on the map what the stage shows from the camera turned by a yaw
        /// (<see cref="ExamplePlan.Turn"/>): what stands to the right on the stage stands to the right on the map.
        /// </summary>
        public static float Following(float viewYaw) => Normal(-viewYaw);
    }
}
