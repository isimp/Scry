using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>A group of a raid's creatures as rolled: how many, how wide each body is (its radius), and how far the game spreads the group (<c>m_groupRadius</c>).</summary>
    public struct CrowdGroup
    {
        public int Count;
        public float Body;
        public float Spread;
    }

    /// <summary>
    /// Where a raid's creatures, as rolled, stand on the stage. The copies have no bodies to push each other apart
    /// as live creatures do, so each group stands in rings around its own point, every body clear
    /// of the next and the group as wide as the game spreads it where that is wider; the groups'
    /// points lie in a ring, far enough apart that no two groups touch.
    /// </summary>
    public static class CrowdLayout
    {
        /// <summary>The room left between two bodies, in metres.</summary>
        public const float Gap = 0.4f;

        /// <summary>Each creature's place, group by group in the order given, its group's number with it.</summary>
        public static List<(int Group, float X, float Z)> Place(IReadOnlyList<CrowdGroup> groups)
        {
            // Each group in rings around its point: one in the middle, six in the first ring,
            // twelve in the second, so neighbours are at least a step apart.
            var shapes = new List<List<(float X, float Z)>>();
            var radii = new List<float>();
            foreach (var group in groups)
            {
                var count = Math.Max(0, group.Count);
                var step = 2f * group.Body + Gap;
                var rings = 0;
                for (var held = 1; held < count; held += 6 * rings) rings++;
                if (rings > 0 && group.Spread - group.Body > rings * step) step = (group.Spread - group.Body) / rings;

                var shape = new List<(float, float)>();
                for (var ring = 0; shape.Count < count; ring++)
                {
                    var around = ring == 0 ? 1 : 6 * ring;
                    for (var k = 0; k < around && shape.Count < count; k++)
                    {
                        var angle = 2.0 * Math.PI * k / around + ring * 0.5;
                        shape.Add(((float)(Math.Sin(angle) * ring * step), (float)(Math.Cos(angle) * ring * step)));
                    }
                }
                shapes.Add(shape);
                radii.Add(rings * step + group.Body);
            }

            // The groups' points in a ring, wide enough that any two groups, however far round
            // from each other, stand clear.
            var m = groups.Count;
            var ringRadius = 0.0;
            for (var a = 0; a < m; a++)
                for (var b = a + 1; b < m; b++)
                {
                    var apart = 2.0 * Math.Sin(Math.PI * (b - a) / m);
                    ringRadius = Math.Max(ringRadius, (radii[a] + radii[b] + Gap) / apart);
                }

            var placed = new List<(int, float, float)>();
            for (var g = 0; g < m; g++)
            {
                var angle = 2.0 * Math.PI * g / Math.Max(1, m);
                var cx = m <= 1 ? 0f : (float)(Math.Sin(angle) * ringRadius);
                var cz = m <= 1 ? 0f : (float)(Math.Cos(angle) * ringRadius);
                placed.AddRange(shapes[g].Select(p => (g, cx + p.X, cz + p.Z)));
            }
            return placed;
        }
    }
}
