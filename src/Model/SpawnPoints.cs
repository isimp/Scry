using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>A creature spawn point of a location or room (<c>CreatureSpawner</c>), as the stage rolls it.</summary>
    public struct SpawnPoint
    {
        public Vec3 At;

        /// <summary>Its group (<c>m_spawnGroupID</c>), how far its group reaches (<c>m_spawnGroupRadius</c>), and how many of the group live at once (<c>m_maxGroupSpawned</c>).</summary>
        public int Group;
        public float GroupRadius;
        public int MaxInGroup;

        /// <summary>Its share when its group picks one (<c>m_spawnerWeight</c>).</summary>
        public float Weight;

        /// <summary>The stars it gives, as levels, and the chance of each one more, in percent, as the world has it.</summary>
        public int MinLevel, MaxLevel;
        public float LevelUpChance;
    }

    /// <summary>
    /// Which of a place's spawn points put their creature there, and at what level, as the game's
    /// <c>CreatureSpawner</c> does once you come near: a point on its own always does; points of
    /// one group within their two radii of each other, step by step, are a group, which keeps up to
    /// its most alive, each picked by weight among those not yet spawned
    /// (<c>CheckGroupSpawnBlocked</c>, <c>Group.SpawnWeighted</c>); a point of no radius or no
    /// most stands on its own. Each creature's level goes up from the point's least, one at a
    /// time, while a roll under its chance holds, to its most at the highest.
    /// </summary>
    public static class SpawnPoints
    {
        /// <summary>The groups the points make, each a list of their indexes; a point on its own is a group of one.</summary>
        public static List<List<int>> Groups(IReadOnlyList<SpawnPoint> points)
        {
            var groups = new List<List<int>>();
            var placed = new bool[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                if (placed[i]) continue;
                placed[i] = true;
                var group = new List<int> { i };
                if (Grouped(points[i]))
                {
                    var next = new Stack<int>();
                    next.Push(i);
                    while (next.Count > 0)
                    {
                        var from = points[next.Pop()];
                        for (var j = 0; j < points.Count; j++)
                        {
                            if (placed[j] || !Grouped(points[j]) || points[j].Group != from.Group) continue;
                            if (Distance(from.At, points[j].At) > from.GroupRadius + points[j].GroupRadius) continue;
                            placed[j] = true;
                            group.Add(j);
                            next.Push(j);
                        }
                    }
                }
                group.Sort();
                groups.Add(group);
            }
            return groups;
        }

        /// <summary>The points that put their creature there, in their order, each with the creature's level.</summary>
        public static List<(int Point, int Level)> Roll(IReadOnlyList<SpawnPoint> points, Func<double> dice)
        {
            var spawned = new List<int>();
            foreach (var group in Groups(points))
            {
                if (group.Count == 1 || !Grouped(points[group[0]]))
                {
                    spawned.AddRange(group);
                    continue;
                }
                // The group goes on spawning while any of its points allows more.
                var most = Math.Min(group.Count, group.Max(i => points[i].MaxInGroup));
                var left = new List<int>(group);
                for (var n = 0; n < most; n++)
                {
                    var whole = left.Sum(i => Math.Max(0f, points[i].Weight));
                    if (whole <= 0f) break;
                    var roll = dice() * whole;
                    var sum = 0.0;
                    foreach (var i in left)
                    {
                        sum += Math.Max(0f, points[i].Weight);
                        if (roll >= sum) continue;
                        spawned.Add(i);
                        left.Remove(i);
                        break;
                    }
                }
            }
            spawned.Sort();
            return spawned.Select(i => (i, Level(points[i].MinLevel, points[i].MaxLevel, points[i].LevelUpChance, dice))).ToList();
        }

        /// <summary>A creature's level: from the least, one more while a roll of 0 to 100 is under the chance, to the most at the highest.</summary>
        public static int Level(int least, int most, float chance, Func<double> dice)
        {
            var level = least;
            while (level < most && dice() * 100.0 <= chance) level++;
            return Math.Max(1, level);
        }

        /// <summary>
        /// How far above its point a creature stands, as the game has it: dropped
        /// <paramref name="drop"/> to the first solid thing under a metre above the point
        /// (<c>ZoneSystem.FindFloor</c>), and one that flies kept <paramref name="lift"/> over that
        /// ground at the least (<c>BaseAI.m_flyAltitudeMin</c>); at its point with nothing under it.
        /// </summary>
        public static float Rise(bool grounded, float drop, float lift) => grounded ? lift - drop : 0f;

        /// <summary>
        /// Whether a creature stands on a floor above a cut, to be cut away with it: the ground
        /// under it, its feet less what it flies over that, above the cut.
        /// </summary>
        public static bool AboveCut(float feet, float lift, float cut) => feet - lift > cut;

        /// <summary>Whether rolling the points can come out more than one way: a star to roll, or a group with more points than it keeps.</summary>
        public static bool LeftToChance(IReadOnlyList<SpawnPoint> points) =>
            points.Any(p => p.MaxLevel > p.MinLevel && p.LevelUpChance > 0f)
            || Groups(points).Any(g => g.Count > 1 && g.Count > g.Max(i => points[i].MaxInGroup));

        private static bool Grouped(SpawnPoint point) => point.GroupRadius > 0f && point.MaxInGroup >= 1;

        private static float Distance(Vec3 a, Vec3 b)
        {
            var x = a.X - b.X;
            var y = a.Y - b.Y;
            var z = a.Z - b.Z;
            return (float)Math.Sqrt(x * x + y * y + z * z);
        }
    }
}
