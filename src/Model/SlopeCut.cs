using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The cut over a room gone into that has no real floor and climbs: a plane a head's height
    /// over the way through it, from the middle of its lowest doorways to the middle of its
    /// highest, level square to that way; flat where the way is a shaft. Heights are the room's
    /// or the example's own, as its doorways are given.
    /// </summary>
    internal readonly struct RoomCutPlane
    {
        public readonly Vec3 Low, High;

        public RoomCutPlane(Vec3 low, Vec3 high)
        {
            Low = low;
            High = high;
        }

        /// <summary>Whether it climbs, rather than lying flat.</summary>
        public bool Sloping => High.Y > Low.Y;

        /// <summary>How high it is over a point seen from above: along the way through, as high as that far up it; square to it, the same.</summary>
        public float HeightAt(float x, float z)
        {
            var dx = High.X - Low.X;
            var dz = High.Z - Low.Z;
            var across = dx * dx + dz * dz;
            if (across < 1e-6f) return High.Y;
            var share = ((x - Low.X) * dx + (z - Low.Z) * dz) / across;
            return Low.Y + share * (High.Y - Low.Y);
        }

        /// <summary>Whether a point stands under it, kept by the cut.</summary>
        public bool Keeps(Vec3 point) => point.Y <= HeightAt(point.X, point.Z);

        /// <summary>The same plane moved up or down, as the cut is moved by hand.</summary>
        public RoomCutPlane Raised(float metres) => new RoomCutPlane(new Vec3(Low.X, Low.Y + metres, Low.Z), new Vec3(High.X, High.Y + metres, High.Z));
    }

    /// <summary>Which rooms gone into are cut along a slope, and where (<see cref="RoomCutPlane"/>).</summary>
    internal static class SlopeCut
    {
        /// <summary>How far apart doorways must stand to be on different heights of the way through: a storey.</summary>
        public const float Storey = PlaceView.CutAboveFloor;

        /// <summary>How near one another doorways stand to be one end of the way, in metres up or down.</summary>
        public const float SameEnd = 1f;

        /// <summary>The steepest way cut along its slope, in degrees; one steeper is a shaft, cut flat over its top.</summary>
        public const float Steepest = 60f;

        /// <summary>
        /// The cut of a room with no real floor whose doorways stand more than a storey apart: a
        /// head's height over the middle of its lowest doorways and of its highest, or flat a head's
        /// height over its highest where the way between them is steeper than <see cref="Steepest"/>.
        /// None for any other room, which is cut over its ground.
        /// </summary>
        public static RoomCutPlane? Of(IReadOnlyList<Vec3> doorways, bool realFloor)
        {
            if (realFloor || doorways.Count < 2) return null;
            var lowest = doorways.Min(d => d.Y);
            var highest = doorways.Max(d => d.Y);
            if (highest - lowest <= Storey) return null;
            var low = Middle(doorways.Where(d => d.Y <= lowest + SameEnd).ToList(), Storey);
            var high = Middle(doorways.Where(d => d.Y >= highest - SameEnd).ToList(), Storey);
            var across = Math.Sqrt((high.X - low.X) * (high.X - low.X) + (high.Z - low.Z) * (high.Z - low.Z));
            var steepness = Math.Atan2(high.Y - low.Y, across) * 180.0 / Math.PI;
            if (steepness > Steepest) low = new Vec3(low.X, high.Y, low.Z);
            return new RoomCutPlane(low, high);
        }

        /// <summary>The middle of some doorways, raised by a head's height.</summary>
        private static Vec3 Middle(IReadOnlyList<Vec3> doorways, float raised)
        {
            float x = 0f, y = 0f, z = 0f;
            foreach (var d in doorways)
            {
                x += d.X;
                y += d.Y;
                z += d.Z;
            }
            return new Vec3(x / doorways.Count, y / doorways.Count + raised, z / doorways.Count);
        }
    }
}
