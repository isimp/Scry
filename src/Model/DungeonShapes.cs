using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>A point or size, as Unity's <c>Vector3</c> holds one, for working out layouts without the game.</summary>
    public struct Vec3
    {
        public float X, Y, Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float f) => new Vec3(a.X * f, a.Y * f, a.Z * f);

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        public static float Distance(Vec3 a, Vec3 b) => (a - b).Length;

        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public override string ToString() => $"({X:0.##}, {Y:0.##}, {Z:0.##})";
    }

    /// <summary>A rotation, as Unity's <c>Quaternion</c> holds one, with the few operations a layout needs.</summary>
    public struct Quat
    {
        public float X, Y, Z, W;

        public Quat(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static Quat Identity => new Quat(0f, 0f, 0f, 1f);

        /// <summary>A turn about the upright axis, in degrees, as <c>Quaternion.Euler(0, yaw, 0)</c>.</summary>
        public static Quat Yaw(float degrees)
        {
            var half = degrees * (float)Math.PI / 360f;
            return new Quat(0f, (float)Math.Sin(half), 0f, (float)Math.Cos(half));
        }

        public static Quat operator *(Quat a, Quat b) => new Quat(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y + a.Y * b.W + a.Z * b.X - a.X * b.Z,
            a.W * b.Z + a.Z * b.W + a.X * b.Y - a.Y * b.X,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

        /// <summary>A point turned by the rotation.</summary>
        public static Vec3 operator *(Quat q, Vec3 v)
        {
            // v + 2w(u × v) + 2u × (u × v), with u the rotation's vector part.
            float ux = q.X, uy = q.Y, uz = q.Z;
            float cx = uy * v.Z - uz * v.Y, cy = uz * v.X - ux * v.Z, cz = ux * v.Y - uy * v.X;
            float dx = uy * cz - uz * cy, dy = uz * cx - ux * cz, dz = ux * cy - uy * cx;
            return new Vec3(v.X + 2f * (q.W * cx + dx), v.Y + 2f * (q.W * cy + dy), v.Z + 2f * (q.W * cz + dz));
        }

        public Quat Inverse()
        {
            var n = X * X + Y * Y + Z * Z + W * W;
            return n > 0f ? new Quat(-X / n, -Y / n, -Z / n, W / n) : Identity;
        }

        /// <summary>The turn about the upright axis it makes, in degrees from 0 to 360, read off where it takes forward.</summary>
        public float YawDegrees
        {
            get
            {
                var forward = this * new Vec3(0f, 0f, 1f);
                var degrees = (float)(Math.Atan2(forward.X, forward.Z) * 180.0 / Math.PI);
                return degrees < 0f ? degrees + 360f : degrees;
            }
        }
    }

    /// <summary>A room's doorway (<c>RoomConnection</c>): its type, where it sits in the room and which way it faces.</summary>
    public sealed class Doorway
    {
        public string Type = "";
        public bool Entrance;
        public bool AllowDoor = true;
        public bool DoorOnlyIfOtherAllows;
        public Vec3 Position;
        public Quat Rotation = Quat.Identity;
    }

    /// <summary>A dungeon room as the generator places it (<c>Room</c>): its box, its kind and its doorways.</summary>
    public sealed class RoomShape
    {
        public string Name = "";
        public int Theme;
        public Vec3 Size;
        public bool Entrance, EndCap, Divider, Perimeter, FaceCenter;
        public int EndCapPrio, MinPlaceOrder;
        public float Weight = 1f;
        public List<Doorway> Doorways = new List<Doorway>();

        /// <summary>Whether it has a doorway of the other's type (<c>Room.HaveConnection</c>).</summary>
        public bool Fits(Doorway other) => Doorways.Exists(d => d.Type == other.Type);
    }

    /// <summary>How a dungeon or camp is built (<c>DungeonGenerator</c>).</summary>
    public sealed class DungeonPlan
    {
        /// <summary>"Dungeon", "CampGrid" or "CampRadial", as the game's <c>Algorithm</c>.</summary>
        public string Algorithm = "Dungeon";

        /// <summary>The kinds of room it is built of (<c>Room.Theme</c> flags).</summary>
        public int Themes;

        public int MinRooms, MaxRooms, MinRequiredRooms;
        public List<string> RequiredRooms = new List<string>();

        /// <summary>Whether it picks rooms by their weight (<c>m_alternativeFunctionality</c>) rather than all alike.</summary>
        public bool Weighted;

        public float DoorChance;

        /// <summary>The doors it may put in a doorway, by the doorway's type, each with its own chance or none (0).</summary>
        public List<(string Type, float Chance)> Doors = new List<(string, float)>();

        public int GridSize;
        public float TileWidth, SpawnChance, CampRadiusMin, CampRadiusMax, PerimeterBuffer;
        public int PerimeterSections;

        /// <summary>The box rooms must stay inside, as big as a zone (<c>m_zoneSize</c>); where it is, <see cref="DungeonLayout.Site"/> works out.</summary>
        public Vec3 ZoneSize = new Vec3(64f, 64f, 64f);

        /// <summary>
        /// Whether its location puts its interior where it says rather than above the entrance
        /// (<c>Location.m_useCustomInteriorTransform</c> with an interior and a generator): the
        /// generator then stands at a set place in its zone, unturned.
        /// </summary>
        public bool CustomInterior;

        /// <summary>For one with its own interior, where its zone's centre is from the generator: back by the interior's and generator's own offsets.</summary>
        public Vec3 ZoneFromGenerator;

        /// <summary>Otherwise, where the generator is in its location, from the location's centre, as the location is turned.</summary>
        public Vec3 GeneratorAt;

        /// <summary>How the generator is turned in its location, or for one with its own interior, in the interior.</summary>
        public Quat GeneratorTurn = Quat.Identity;
    }

    /// <summary>
    /// What was read of a location or room once its bundle was loaded: what names it, what it
    /// holds and how likely, the dungeon it builds or the room it is, and the levels it sets
    /// for its spawn points.
    /// </summary>
    public sealed class PlaceContents
    {
        public string GameName = "", Boss = "", Trader = "";

        /// <summary>Its networked parts, with how many and how likely (<see cref="PlaceParts"/>).</summary>
        public List<PlacePart> Parts = new List<PlacePart>();

        /// <summary>The creatures its spawn points place, with how many points and how likely each is there.</summary>
        public List<PlacePart> Creatures = new List<PlacePart>();

        /// <summary>The locations its vegvisirs point to, by prefab name.</summary>
        public List<string> Vegvisirs = new List<string>();

        public DungeonPlan Dungeon;
        public RoomShape Room;

        /// <summary>The levels a location sets for its spawn points in place of theirs (<c>Location.m_enemyMinLevelOverride</c> and the like); -1 for none.</summary>
        public int EnemyMinLevel = -1, EnemyMaxLevel = -1;
        public float EnemyLevelUpChance = -1f;

        /// <summary>Whether some of its spawn groups keep their own levels (<c>Location.m_excludeEnemyLevelOverrideGroups</c>).</summary>
        public bool LevelOverrideExceptions;

        public bool NoBuild;
        public float NoBuildRadius;

        /// <summary>Whether anything in it is there only by chance, so another roll can show it otherwise (<see cref="PlaceParts.LeftToChance"/>).</summary>
        public bool LeftToChance;
    }
}
