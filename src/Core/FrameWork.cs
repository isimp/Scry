using System.Diagnostics;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// How long Scry's own Update and LateUpdate took this frame, measured always, for the panel
    /// drawn after them to leave a frame alone that its other work filled already
    /// (<see cref="PageStaging"/>): the frame after a click makes the stage's copy and films it.
    /// </summary>
    internal static class FrameWork
    {
        private static int _frame = -1;
        private static double _ms;
        private static long _started;

        /// <summary>A part of the frame's work begins.</summary>
        public static void Begin()
        {
            if (Time.frameCount != _frame)
            {
                _frame = Time.frameCount;
                _ms = 0;
            }
            _started = Stopwatch.GetTimestamp();
        }

        /// <summary>The part begun last ends.</summary>
        public static void End() => _ms += (Stopwatch.GetTimestamp() - _started) * 1000.0 / Stopwatch.Frequency;

        /// <summary>What Scry's Update and LateUpdate took this frame so far, in milliseconds.</summary>
        public static double SpentMs => Time.frameCount == _frame ? _ms : 0;
    }
}
