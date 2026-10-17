using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The walkway a room of a dungeon's plan is drawn with from above: a hall along its longer
    /// side through its middle, and each doorway joined to it square to the wall it is in, so a
    /// way runs along the room's walls as a person walks it, never across the room. A doorway in
    /// a wall along the hall goes straight in to it; one in an end wall beside the hall steps in
    /// first, then turns to it; a hole in the floor joins it as the wall it is nearer would.
    /// </summary>
    internal static class RoomWalkway
    {
        /// <summary>Within this of the hall's line, in metres, a doorway in an end wall is on it.</summary>
        public const float Near = 0.6f;

        /// <summary>How far a doorway in an end wall beside the hall steps in before turning to it, at most, in metres; in a small room, half way to the middle.</summary>
        public const float StepIn = 3f;

        /// <summary>The points from a doorway to where it meets the room's hall: the doorway first, its foot on the hall last, each line square to the room's walls.</summary>
        public static List<Vec3> Stub(PlacedRoom room, Vec3 door)
        {
            var points = new List<Vec3> { door };
            var (along, across) = Local(room, door);
            var (length, depth) = Halves(room);
            if (Math.Abs(along) - length >= Math.Abs(across) - depth && Math.Abs(across) > Near)
            {
                along -= Math.Sign(along) * Math.Min(length / 2f, StepIn);
                Add(points, World(room, along, across));
            }
            Add(points, World(room, along, 0f));
            return points;
        }

        /// <summary>Where a doorway meets the room's hall.</summary>
        public static Vec3 Foot(PlacedRoom room, Vec3 door)
        {
            var stub = Stub(room, door);
            return stub[stub.Count - 1];
        }

        /// <summary>The room's hall: along its longer side through its middle, from the farthest foot of the doorways given one way to the farthest the other, the middle always on it.</summary>
        public static (Vec3 From, Vec3 To) HallOf(PlacedRoom room, IEnumerable<Vec3> doors)
        {
            float least = 0f, most = 0f;
            foreach (var door in doors)
            {
                var (along, _) = Local(room, Foot(room, door));
                least = Math.Min(least, along);
                most = Math.Max(most, along);
            }
            return (World(room, least, 0f), World(room, most, 0f));
        }

        /// <summary>The line along the hall's line from its nearer end to a doorway's foot lying past it; none where the foot is on the hall.</summary>
        public static (Vec3 From, Vec3 To)? Beyond(PlacedRoom room, (Vec3 From, Vec3 To) hall, Vec3 door)
        {
            var (foot, _) = Local(room, Foot(room, door));
            var (least, _) = Local(room, hall.From);
            var (most, _) = Local(room, hall.To);
            if (foot < least - 0.001f) return (hall.From, World(room, foot, 0f));
            if (foot > most + 0.001f) return (hall.To, World(room, foot, 0f));
            return null;
        }

        /// <summary>The way through a room from one doorway to another, or from or to its middle where either is none: in along the one's stub, along the hall, and out along the other's.</summary>
        public static List<Vec3> Through(PlacedRoom room, Vec3? from, Vec3? to)
        {
            var points = from is Vec3 entry ? Stub(room, entry) : new List<Vec3> { Middle(room) };
            if (to is Vec3 exit)
            {
                var stub = Stub(room, exit);
                for (var i = stub.Count - 1; i >= 0; i--) Add(points, stub[i]);
            }
            else Add(points, Middle(room));
            return points;
        }

        /// <summary>The room's middle as seen from above, at its own height.</summary>
        public static Vec3 Middle(PlacedRoom room) => room.Position;

        /// <summary>Adds a point, unless it is where the last one is.</summary>
        private static void Add(List<Vec3> points, Vec3 point)
        {
            if (points.Count == 0 || Vec3.Distance(points[points.Count - 1], point) > 0.001f) points.Add(point);
        }

        /// <summary>Half the room's longer side, along which its hall runs, and half its shorter.</summary>
        private static (float Length, float Depth) Halves(PlacedRoom room) =>
            Lengthwise(room) ? (room.Room.Size.X / 2f, room.Room.Size.Z / 2f) : (room.Room.Size.Z / 2f, room.Room.Size.X / 2f);

        /// <summary>Whether the room's hall runs along its own X: the longer side, X where both are as long.</summary>
        private static bool Lengthwise(PlacedRoom room) => room.Room.Size.X >= room.Room.Size.Z;

        /// <summary>A point as far along the room's hall from its middle and as far across it.</summary>
        private static (float Along, float Across) Local(PlacedRoom room, Vec3 point)
        {
            var local = room.Rotation.Inverse() * (point - room.Position);
            return Lengthwise(room) ? (local.X, local.Z) : (local.Z, local.X);
        }

        /// <summary>The point as far along the room's hall from its middle and as far across it, in the example, at the room's height.</summary>
        private static Vec3 World(PlacedRoom room, float along, float across) =>
            room.Position + room.Rotation * (Lengthwise(room) ? new Vec3(along, 0f, across) : new Vec3(across, 0f, along));
    }
}
