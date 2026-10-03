using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Unity's points written as Scry writes numbers (<see cref="Numbers"/>), figure by figure,
    /// rather than as Unity writes them; the model knows no Unity, so these hand it the figures.
    /// </summary>
    internal static class Figures
    {
        /// <summary>A point on a picture or a plane: "(0.50, 1.25)".</summary>
        public static string Point(Vector2 point, int decimals = 2) => Numbers.Point(point.x, point.y, decimals);

        /// <summary>A point or a size in space: "(1.00, 2.50, -3.00)".</summary>
        public static string Point(Vector3 point, int decimals = 2) => Numbers.Point(point.x, point.y, point.z, decimals);
    }
}
