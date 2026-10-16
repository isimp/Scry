using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>How a room of an example shows on its plan with a floor opened.</summary>
    internal enum PlanRoomShown
    {
        /// <summary>Above the floor opened, cut away as on the stage.</summary>
        None,

        /// <summary>Below it: dimmed on the stage, to see where the floor lies over, and left off the plan.</summary>
        Faint,

        /// <summary>On it, or every room while the roof is on.</summary>
        Whole,
    }

    /// <summary>How a room of an example shows on its plan with a floor opened.</summary>
    internal enum PlanTile
    {
        /// <summary>Not on the plan: above the floor opened, or below the floor under it.</summary>
        Off,

        /// <summary>Standing on the floor under the one opened: outlined, dashed, to see what lies beneath.</summary>
        Below,

        /// <summary>A room of the floor opened, a tile; every room while the roof is on.</summary>
        On,
    }

    /// <summary>Where a doorway on the floor opened leads, marked on the plan: up, down, or along the floor.</summary>
    internal enum DoorLeads
    {
        Level,
        Up,
        Down,
    }

    /// <summary>
    /// An example's plan in the stage's corner, as seen from above. It turns with the view, what
    /// lies ahead of the camera up on the plan, and is framed on its rooms alone, at a scale that
    /// keeps while the view turns. With a floor opened, a room is on that floor, below it or above
    /// it: the plan shows only the floor's rooms, the stage those below too, dimmed. Heights are
    /// the example's own; a room's box stands centred on its pivot (<see cref="DungeonExample.Pick"/>).
    /// </summary>
    internal static class ExamplePlan
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

        /// <summary>
        /// How a room shows on the stage with the floor at that height opened, by the plan's rule
        /// (<see cref="IsOn"/>): whole on that floor; dimmed below it, as a tall room from a floor
        /// under it reaching up through it; put away above it. With <paramref name="ownFloor"/>,
        /// the room has a floor of its own at one of the example's floors. With none opened (the
        /// roof on), whole.
        /// </summary>
        public static PlanRoomShown OnStage(PlacedRoom room, float? floor, bool ownFloor, Func<PlacedRoom, float, bool> standsOn)
        {
            if (floor == null || IsOn(room, floor.Value, ownFloor, standsOn)) return PlanRoomShown.Whole;
            return Shown(room, floor) == PlanRoomShown.None ? PlanRoomShown.None : PlanRoomShown.Faint;
        }

        /// <summary>
        /// How a room shows on the plan with floor <paramref name="level"/> of <paramref name="floors"/>
        /// (from the top down) opened, by the floors it is on (<see cref="IsOn"/>): a tile on that
        /// floor; dashed on the floor under it, as a tall room from there reaching up through the
        /// floor opened, which is not a room of it. With the roof on, every room is a tile.
        /// </summary>
        public static PlanTile Tile(PlacedRoom room, IReadOnlyList<float> floors, int level, Func<PlacedRoom, float, bool> standsOn)
        {
            if (level < 0 || level >= floors.Count) return PlanTile.On;
            var own = HasOwnFloor(room, floors, standsOn);
            if (IsOn(room, floors[level], own, standsOn)) return PlanTile.On;
            var closing = room.Room.EndCap || room.Room.Divider;
            return !closing && level + 1 < floors.Count && IsOn(room, floors[level + 1], own, standsOn) ? PlanTile.Below : PlanTile.Off;
        }

        /// <summary>
        /// Whether a room is on a floor, as the plan shows it: by a floor of its own there
        /// (<paramref name="standsOn"/>, as its rays found it); a room with no floor of its own at
        /// any of the floors, a tunnel sloping between them, a passage of rock or an end cap
        /// closing the rooms of the floors it reaches, by its box reaching it.
        /// </summary>
        private static bool IsOn(PlacedRoom room, float floor, bool own, Func<PlacedRoom, float, bool> standsOn) =>
            own ? standsOn(room, floor) : Shown(room, floor) == PlanRoomShown.Whole;

        /// <summary>Whether a room has a floor of its own at any of the floors.</summary>
        private static bool HasOwnFloor(PlacedRoom room, IReadOnlyList<float> floors, Func<PlacedRoom, float, bool> standsOn)
        {
            for (var i = 0; i < floors.Count; i++)
            {
                if (standsOn(room, floors[i])) return true;
            }
            return false;
        }

        /// <summary>How near a room's doorway must be to a door for the room to be joined there.</summary>
        private const float DoorReach = 0.5f;

        /// <summary>
        /// Where a door on floor <paramref name="level"/> leads: where a room on that floor is
        /// joined there to one not on it but on one above or below (<see cref="IsOn"/>), up or
        /// down to the nearest such floor; along the floor otherwise, or with the roof on.
        /// </summary>
        public static DoorLeads Leads(Vec3 door, IEnumerable<PlacedRoom> rooms, IReadOnlyList<float> floors, int level, Func<PlacedRoom, float, bool> standsOn)
        {
            if (level < 0 || level >= floors.Count) return DoorLeads.Level;
            var fromHere = false;
            var nearest = int.MaxValue;
            var leads = DoorLeads.Level;
            foreach (var room in rooms)
            {
                if (!JoinedAt(room, door)) continue;
                var own = HasOwnFloor(room, floors, standsOn);
                if (IsOn(room, floors[level], own, standsOn))
                {
                    fromHere = true;
                    continue;
                }
                for (var i = 0; i < floors.Count; i++)
                {
                    if (i == level || !IsOn(room, floors[i], own, standsOn) || Math.Abs(i - level) >= nearest) continue;
                    nearest = Math.Abs(i - level);
                    leads = i < level ? DoorLeads.Up : DoorLeads.Down;
                }
            }
            return fromHere ? leads : DoorLeads.Level;
        }

        /// <summary>Whether one of a room's doorways is at a door.</summary>
        private static bool JoinedAt(PlacedRoom room, Vec3 door)
        {
            for (var i = 0; i < room.Room.Doorways.Count; i++)
            {
                if (Vec3.Distance(room.DoorwayAt(i), door) < DoorReach) return true;
            }
            return false;
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
