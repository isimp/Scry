using UnityEngine;

namespace Scry
{
    /// <summary>The marks of the settings card's controls: a tick for what is on among several at once.</summary>
    internal static partial class Skin
    {
        private static Texture2D _tick;

        /// <summary>A tick, two strokes meeting low, drawn once.</summary>
        public static Texture2D Tick => _tick != null ? _tick : _tick = Shape(32, 32, TickCoverage);

        /// <summary>Whether a point lies on the tick's short stroke down to the right or its long stroke up to the right, each about three pixels thick.</summary>
        private static bool TickCoverage(float x, float y) =>
            Near(x, y, 7f, 17f, 13f, 10f) || Near(x, y, 13f, 10f, 26f, 24f);

        private static bool Near(float x, float y, float ax, float ay, float bx, float by)
        {
            var dx = bx - ax;
            var dy = by - ay;
            var t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
            var px = ax + t * dx - x;
            var py = ay + t * dy - y;
            return px * px + py * py <= 2.6f * 2.6f;
        }
    }
}
