using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>How a room of an example shows on its plan with a floor opened.</summary>
    public enum PlanRoomShown
    {
        /// <summary>Above the floor opened, cut away as on the stage.</summary>
        None,

        /// <summary>Below it, faintly, to see where the floor lies over.</summary>
        Faint,

        /// <summary>On it, or every room while the roof is on.</summary>
        Whole,
    }

    /// <summary>
    /// An example's plan in the stage's corner, as seen from above. It turns with the view, what
    /// lies ahead of the camera up on the plan, and is framed on its rooms alone, at a scale that
    /// keeps while the view turns. With a floor opened it shows that floor's rooms whole, those
    /// below it faintly and none above it, as the stage shows the example cut open. Heights are
    /// the example's own; a room's box stands centred on its pivot (<see cref="DungeonExample.Pick"/>).
    /// </summary>
    public static class ExamplePlan
    {
        /// <summary>How high above a floor a room must start to be above it: a storey, as floors closer are one (<see cref="PlaceView.SameFloor"/>).</summary>
        public const float Headroom = 2f;

        /// <summary>How far a room may reach over a floor and still be below it: the floor's own thickness.</summary>
        public const float Slack = 0.25f;

        /// <summary>Half the height a room with none of its own counts, an end cap or a divider.</summary>
        private const float LeastHalf = 0.25f;

        /// <summary>A point of the plan turned as the view is: how far right and up it is, the view's yaw in degrees.</summary>
        public static (float Right, float Up) Turn(float x, float z, float yaw)
        {
            var a = yaw * Math.PI / 180.0;
            var cos = Math.Cos(a);
            var sin = Math.Sin(a);
            return ((float)(x * cos - z * sin), (float)(x * sin + z * cos));
        }

        /// <summary>Where a point of the turned plan is in the example.</summary>
        public static (float X, float Z) Back(float right, float up, float yaw)
        {
            var a = yaw * Math.PI / 180.0;
            var cos = Math.Cos(a);
            var sin = Math.Sin(a);
            return ((float)(right * cos + up * sin), (float)(-right * sin + up * cos));
        }

        /// <summary>How far the rooms reach, turned as the view is; all nought for none.</summary>
        public static (float MinRight, float MaxRight, float MinUp, float MaxUp) Extent(IEnumerable<PlacedRoom> rooms, float yaw)
        {
            float minR = float.PositiveInfinity, maxR = float.NegativeInfinity, minU = float.PositiveInfinity, maxU = float.NegativeInfinity;
            foreach (var room in rooms)
            {
                foreach (var corner in room.Corners())
                {
                    var (right, up) = Turn(corner.X, corner.Z, yaw);
                    minR = Math.Min(minR, right);
                    maxR = Math.Max(maxR, right);
                    minU = Math.Min(minU, up);
                    maxU = Math.Max(maxU, up);
                }
            }
            return float.IsPositiveInfinity(minR) ? (0f, 0f, 0f, 0f) : (minR, maxR, minU, maxU);
        }

        /// <summary>The widest the rooms are any way round, their farthest corners apart, which the plan's scale goes by.</summary>
        public static float Widest(IEnumerable<PlacedRoom> rooms)
        {
            var corners = rooms.SelectMany(r => r.Corners()).ToList();
            var widest = 0.0;
            for (var i = 0; i < corners.Count; i++)
            {
                for (var j = i + 1; j < corners.Count; j++)
                {
                    var dx = corners[i].X - corners[j].X;
                    var dz = corners[i].Z - corners[j].Z;
                    widest = Math.Max(widest, dx * dx + dz * dz);
                }
            }
            return (float)Math.Sqrt(widest);
        }

        /// <summary>How a room shows with a floor opened at that height; with none (the roof on), whole.</summary>
        public static PlanRoomShown Shown(PlacedRoom room, float? floor)
        {
            if (floor == null) return PlanRoomShown.Whole;
            var half = Math.Max(LeastHalf, room.Room.Size.Y / 2f);
            if (room.Position.Y + half <= floor.Value + Slack) return PlanRoomShown.Faint;
            if (room.Position.Y - half >= floor.Value + Headroom) return PlanRoomShown.None;
            return PlanRoomShown.Whole;
        }

        /// <summary>Whether a door shows: one opening on the floor opened, or every one with the roof on.</summary>
        public static bool DoorShown(Vec3 door, float? floor) =>
            floor == null || door.Y >= floor.Value - 1f && door.Y < floor.Value + Headroom;

        /// <summary>The room a point of the plan is in: the topmost of those shown whole there, or null.</summary>
        public static PlacedRoom RoomAt(IEnumerable<PlacedRoom> rooms, float x, float z, float? floor) =>
            TopmostAt(rooms, x, z, r => Shown(r, floor) == PlanRoomShown.Whole);

        /// <summary>The room a point of the plan is in: the topmost of those <paramref name="whole"/> says show whole, or null.</summary>
        public static PlacedRoom TopmostAt(IEnumerable<PlacedRoom> rooms, float x, float z, Func<PlacedRoom, bool> whole) =>
            rooms.Where(r => whole(r) && r.Covers(x, z)).OrderBy(r => r.Position.Y).LastOrDefault();
    }
}
