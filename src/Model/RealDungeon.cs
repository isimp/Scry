using System;
using System.Collections.Generic;
using System.IO;

namespace Scry
{
    /// <summary>
    /// The rooms of a dungeon as a world keeps them, on its generator's network object
    /// (<c>DungeonGenerator.Save</c>): a count, then for each room the stable hash of its prefab's
    /// name, its position, and its turn as Euler angles in degrees, as the game writes a turn.
    /// </summary>
    internal static class SavedRooms
    {
        /// <summary>The rooms saved, as many as the data holds whole; none for no data or a count under one.</summary>
        public static List<(int Hash, Vec3 Position, Vec3 Angles)> Read(byte[] data)
        {
            var rooms = new List<(int, Vec3, Vec3)>();
            if (data == null || data.Length < 4) return rooms;
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                var count = reader.ReadInt32();
                for (var i = 0; i < count && reader.BaseStream.Length - reader.BaseStream.Position >= 28; i++)
                {
                    var hash = reader.ReadInt32();
                    var position = new Vec3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    var angles = new Vec3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    rooms.Add((hash, position, angles));
                }
            }
            return rooms;
        }

        /// <summary>
        /// The saved room a point stands in, by each room's box round its place, turned as saved and
        /// as big as <paramref name="sizeOf"/> gives its hash (none for an end cap, a divider or a
        /// kind not known); of boxes overlapping, the one whose foot is nearest under the point; -1
        /// in none.
        /// </summary>
        public static int Holding(IReadOnlyList<(int Hash, Vec3 Position, Vec3 Angles)> saved, Func<int, Vec3?> sizeOf, Vec3 point)
        {
            var found = -1;
            var highest = float.NegativeInfinity;
            for (var i = 0; i < saved.Count; i++)
            {
                var (hash, position, angles) = saved[i];
                var size = sizeOf(hash);
                if (!size.HasValue) continue;
                var local = Quat.Euler(angles.X, angles.Y, angles.Z).Inverse() * (point - position);
                var half = size.Value * 0.5f;
                if (Math.Abs(local.X) > half.X || Math.Abs(local.Y) > half.Y || Math.Abs(local.Z) > half.Z) continue;
                var foot = position.Y - half.Y;
                if (foot <= highest) continue;
                highest = foot;
                found = i;
            }
            return found;
        }

        /// <summary>The keys an older world kept a room under instead, by its place in the list: its hash, its position and its turn.</summary>
        public static (string Hash, string Position, string Turn) OldKeys(int index)
        {
            var room = "room" + Stored.Count(index);
            return (room, room + "_pos", room + "_rot");
        }

        /// <summary>The rooms written as the game writes them, as a dungeon's map keeps them.</summary>
        public static byte[] Write(IReadOnlyList<(int Hash, Vec3 Position, Vec3 Angles)> rooms)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(rooms.Count);
                foreach (var (hash, position, angles) in rooms)
                {
                    writer.Write(hash);
                    writer.Write(position.X);
                    writer.Write(position.Y);
                    writer.Write(position.Z);
                    writer.Write(angles.X);
                    writer.Write(angles.Y);
                    writer.Write(angles.Z);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }
    }

    /// <summary>
    /// A dungeon as it stands in a world, laid out from its saved rooms (<see cref="SavedRooms"/>)
    /// as an example is, to be staged and planned the same way. It is read, never generated.
    /// </summary>
    internal static class RealDungeon
    {
        /// <summary>How near two doorways stand to be one, in metres.</summary>
        private const float SameDoorway = 0.5f;

        /// <summary>
        /// Each saved room by its hash among the rooms known (<paramref name="roomOf"/>, null for
        /// one not read), placed and turned as kept, the generator's place and turn the site's; the
        /// rooms not known left out and counted. The way in stands first, as the game saves its
        /// start room first: the first entrance known, else the first room. An end cap or divider
        /// is joined to the room whose doorway it closes, by the doorway they share.
        /// </summary>
        public static DungeonExample Of(IReadOnlyList<(int Hash, Vec3 Position, Vec3 Angles)> saved, Func<int, RoomShape> roomOf, Vec3 generator, Quat turn, out int unknown)
        {
            unknown = 0;
            var dungeon = new DungeonExample { Site = new DungeonSite { Generator = generator, Turn = turn } };
            for (var i = 0; i < saved.Count; i++)
            {
                var (hash, position, angles) = saved[i];
                var shape = roomOf(hash);
                if (shape == null)
                {
                    unknown++;
                    continue;
                }
                dungeon.Rooms.Add(new PlacedRoom { Room = shape, Position = position, Rotation = Quat.Euler(angles.X, angles.Y, angles.Z), SavedAt = i });
            }

            var way = dungeon.Rooms.FindIndex(r => r.Room.Entrance);
            if (way < 0) way = 0;
            var order = 1;
            for (var i = 0; i < dungeon.Rooms.Count; i++) dungeon.Rooms[i].PlaceOrder = i == way ? 0 : order++;

            foreach (var cap in dungeon.Rooms)
            {
                if (!cap.Room.EndCap && !cap.Room.Divider) continue;
                cap.JoinedTo = dungeon.Rooms.Find(other => !other.Room.EndCap && !other.Room.Divider && ShareDoorway(cap, other));
            }
            return dungeon;
        }

        private static bool ShareDoorway(PlacedRoom a, PlacedRoom b)
        {
            for (var i = 0; i < a.Room.Doorways.Count; i++)
            {
                var at = a.DoorwayAt(i);
                for (var j = 0; j < b.Room.Doorways.Count; j++)
                {
                    var other = b.DoorwayAt(j);
                    var dx = at.X - other.X;
                    var dy = at.Y - other.Y;
                    var dz = at.Z - other.Z;
                    if (dx * dx + dy * dy + dz * dz <= SameDoorway * SameDoorway) return true;
                }
            }
            return false;
        }
    }
}
