using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The plan box's map of a dungeon's example: each room across against its depth, a storey
    /// tall from its ground, the ground at the top and the deepest room at the bottom. Across is
    /// one way round the level, at an angle that can be turned to any; the other way round the
    /// level is flattened, so rooms standing one behind the other on one level cover each other.
    /// Least overlap turns it to the angle where the fewest do; following the view turns it with
    /// the stage.
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

        /// <summary>
        /// How tall each room stands on the map, from its ground, in metres: a storey, where its
        /// whole box, 12 to 30 m tall in the caves, would cover the rooms above and below it.
        /// </summary>
        public const float Storey = 3f;

        /// <summary>Where a point, such as a doorway, stands across at an angle.</summary>
        public static float AcrossAt(Vec3 point, float angle)
        {
            var a = angle * Math.PI / 180.0;
            return (float)(point.X * Math.Cos(a) + point.Z * Math.Sin(a));
        }

        /// <summary>How high a room stands on the map: from its ground up a storey.</summary>
        public static (float Ground, float Top) Tile(float ground) => (ground, ground + Storey);

        /// <summary>How high and how deep the rooms stand together, by their grounds: the highest storey's top and the lowest ground.</summary>
        public static (float Top, float Bottom) Heights(IEnumerable<float> grounds)
        {
            float top = float.NegativeInfinity, bottom = float.PositiveInfinity;
            foreach (var ground in grounds)
            {
                top = Math.Max(top, Tile(ground).Top);
                bottom = Math.Min(bottom, ground);
            }
            return float.IsNegativeInfinity(top) ? (0f, 0f) : (top, bottom);
        }

        /// <summary>
        /// How high each room stands on the map at an angle, so each shows whole: a room covering
        /// no other its storey from its ground (<see cref="Tile"/>); rooms covering each other on
        /// one level, and those covering them in turn, share a band from their lowest ground to
        /// their highest storey's top, each in a lane of it. Lanes go from the highest room down,
        /// a room under every lane of the rooms above it that it covers, else in the first lane no
        /// room it covers holds, so a deeper room stands under a higher one it covers.
        /// </summary>
        public static List<(float Bottom, float Top)> Tiles(IReadOnlyList<PlacedRoom> rooms, IReadOnlyList<float> grounds, float angle)
        {
            var count = rooms.Count;
            var across = new (float Min, float Max)[count];
            for (var i = 0; i < count; i++) across[i] = Across(rooms[i], angle);
            bool SideBySide(int a, int b) => across[a].Max - Touch <= across[b].Min || across[b].Max - Touch <= across[a].Min;

            // Rooms across from each other whose heights meet are one band, a room's height its
            // storey and a band's from its lowest ground to its highest storey's top; a band takes
            // in any room across from one of its rooms whose height its own reaches, until none is
            // left that a lane of the band would cover.
            var band = new int[count];
            for (var i = 0; i < count; i++) band[i] = i;
            int Find(int i) => band[i] == i ? i : band[i] = Find(band[i]);
            var bottoms = new float[count];
            var tops = new float[count];
            for (var joined = true; joined;)
            {
                joined = false;
                for (var i = 0; i < count; i++)
                {
                    bottoms[i] = float.PositiveInfinity;
                    tops[i] = float.NegativeInfinity;
                }
                for (var i = 0; i < count; i++)
                {
                    var root = Find(i);
                    bottoms[root] = Math.Min(bottoms[root], grounds[i]);
                    tops[root] = Math.Max(tops[root], Tile(grounds[i]).Top);
                }
                for (var a = 0; a < count && !joined; a++)
                {
                    for (var b = a + 1; b < count && !joined; b++)
                    {
                        int ra = Find(a), rb = Find(b);
                        if (ra == rb || SideBySide(a, b) || tops[ra] <= bottoms[rb] || tops[rb] <= bottoms[ra]) continue;
                        band[ra] = rb;
                        joined = true;
                    }
                }
            }

            var tiles = new List<(float Bottom, float Top)>(count);
            for (var i = 0; i < count; i++) tiles.Add(Tile(grounds[i]));
            foreach (var group in Enumerable.Range(0, count).GroupBy(Find))
            {
                var members = group.OrderByDescending(i => grounds[i]).ThenBy(i => across[i].Min).ThenBy(i => i).ToList();
                if (members.Count < 2) continue;
                var lane = new Dictionary<int, int>();
                foreach (var room in members)
                {
                    var under = 0;
                    var taken = new HashSet<int>();
                    foreach (var placed in lane.Keys)
                    {
                        if (SideBySide(room, placed)) continue;
                        if (grounds[placed] > grounds[room] + Touch) under = Math.Max(under, lane[placed] + 1);
                        else taken.Add(lane[placed]);
                    }
                    while (taken.Contains(under)) under++;
                    lane[room] = under;
                }
                var lanes = lane.Values.Max() + 1;
                var top = members.Max(i => Tile(grounds[i]).Top);
                var bottom = members.Min(i => grounds[i]);
                var height = (top - bottom) / lanes;
                foreach (var room in members) tiles[room] = (top - (lane[room] + 1) * height, top - lane[room] * height);
            }
            return tiles;
        }

        /// <summary>How many two rooms cover each other on the map at an angle: reaching into each other across, and sharing their storeys.</summary>
        public static int Overlaps(IReadOnlyList<PlacedRoom> rooms, IReadOnlyList<float> grounds, float angle)
        {
            var across = new (float Min, float Max)[rooms.Count];
            for (var i = 0; i < rooms.Count; i++) across[i] = Across(rooms[i], angle);
            var count = 0;
            for (var a = 0; a < rooms.Count; a++)
            {
                for (var b = a + 1; b < rooms.Count; b++)
                {
                    var sideBySide = across[a].Max - Touch <= across[b].Min || across[b].Max - Touch <= across[a].Min;
                    var oneAbove = Tile(grounds[a]).Top <= grounds[b] || Tile(grounds[b]).Top <= grounds[a];
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
        public static float LeastOverlap(IReadOnlyList<PlacedRoom> rooms, IReadOnlyList<float> grounds, float current)
        {
            current = Normal(current);
            var best = current;
            var fewest = int.MaxValue;
            var nearest = float.MaxValue;
            for (var angle = 0f; angle < 180f; angle += Step)
            {
                var count = Overlaps(rooms, grounds, angle);
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
