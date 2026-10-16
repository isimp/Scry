using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The plan box's tree of a dungeon's example: each room's node at its spot on the plan seen
    /// from above, pushed away from another only where the two crowd closer than a gap, and kept
    /// inside the box. A node with room to spare stays on its spot; one pushed off it is told so,
    /// for a dotted line back to where it belongs. The same spots lay out the same each time.
    /// </summary>
    internal static class PinnedLayout
    {
        /// <summary>How many times every two nodes are pushed apart at most.</summary>
        private const int Rounds = 400;

        /// <summary>How far off its spot a node must stand to be told moved, in pixels.</summary>
        public const float MovedBy = 2f;

        /// <summary>Where each node stands, from its spot, at least a gap from every other where the box has the room, and inside the box.</summary>
        public static List<(float X, float Y)> Of(IReadOnlyList<(float X, float Y)> homes, float gap, float left, float top, float right, float bottom)
        {
            var x = new float[homes.Count];
            var y = new float[homes.Count];
            for (var i = 0; i < homes.Count; i++)
            {
                x[i] = Clamp(homes[i].X, left, right);
                y[i] = Clamp(homes[i].Y, top, bottom);
            }
            for (var round = 0; round < Rounds; round++)
            {
                var moved = false;
                for (var a = 0; a < homes.Count; a++)
                {
                    for (var b = a + 1; b < homes.Count; b++)
                    {
                        var dx = x[b] - x[a];
                        var dy = y[b] - y[a];
                        var d = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (d >= gap) continue;
                        if (d < 0.01f)
                        {
                            // Nodes on one spot part each its own way, the same each time.
                            var turn = (a * 7 + b * 13) * 2.399963f;
                            dx = (float)Math.Cos(turn);
                            dy = (float)Math.Sin(turn);
                            d = 1f;
                        }
                        else
                        {
                            dx /= d;
                            dy /= d;
                        }
                        // The first goes half the way back, as far as the box lets it; the other the rest of the way on.
                        var push = gap - d + 0.01f;
                        var ax = Clamp(x[a] - dx * push / 2f, left, right);
                        var ay = Clamp(y[a] - dy * push / 2f, top, bottom);
                        x[b] = Clamp(x[b] + dx * push + (ax - x[a]), left, right);
                        y[b] = Clamp(y[b] + dy * push + (ay - y[a]), top, bottom);
                        x[a] = ax;
                        y[a] = ay;
                        moved = true;
                    }
                }
                if (!moved) break;
            }
            var laid = new List<(float, float)>(homes.Count);
            for (var i = 0; i < homes.Count; i++) laid.Add((x[i], y[i]));
            return laid;
        }

        /// <summary>Whether a node stands off its spot, so a dotted line leads back to it.</summary>
        public static bool Moved((float X, float Y) home, (float X, float Y) at)
        {
            var dx = home.X - at.X;
            var dy = home.Y - at.Y;
            return dx * dx + dy * dy > MovedBy * MovedBy;
        }

        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
