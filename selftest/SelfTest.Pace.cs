using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// How fast this machine runs while the self-test runs (<see cref="MachinePace"/>): a fixed
    /// piece of reference work timed as each part begins and again when a part's budget is judged
    /// or its time runs out, against the quickest the work ever took here, kept beside the log.
    /// A part judges by the slowest the machine ran since it began. The quickest is only as good
    /// as the runs before: the first run on a machine takes itself as the best.
    /// </summary>
    internal static partial class SelfTest
    {
        public static string PaceFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-selftest.pace");

        private static MachinePace _pace;

        /// <summary>The slowest the machine ran since the part began, and when it was last sampled.</summary>
        private static double _partSlowdown = 1.0;
        private static float _pacedAt = -10f;

        /// <summary>The reference work's numbers, gone over <see cref="MachinePace.Work"/> times: about a millisecond on a quick machine.</summary>
        private static readonly int[] PaceWork = Enumerable.Range(0, 1 << 14).ToArray();
        private static int _paceSink;

        /// <summary>A part begins: the machine's pace sampled afresh for it.</summary>
        private static void PaceForPart()
        {
            if (_pace == null) _pace = MachinePace.Load(Kept(PaceFile));
            _partSlowdown = 1.0;
            _partSlowdown = SamplePace();
            _pacedAt = Time.realtimeSinceStartup;
        }

        /// <summary>How many times slower than its best the machine ran since the part began, sampled again at most once a second.</summary>
        private static double Slowdown()
        {
            if (_pace == null) return 1.0;
            if (Time.realtimeSinceStartup - _pacedAt >= 1f)
            {
                _partSlowdown = Math.Max(_partSlowdown, SamplePace());
                _pacedAt = Time.realtimeSinceStartup;
            }
            return _partSlowdown;
        }

        /// <summary>The reference work timed five times, the middle time taken, against the quickest seen.</summary>
        private static double SamplePace()
        {
            var times = new double[5];
            var watch = new Stopwatch();
            for (var i = 0; i < times.Length; i++)
            {
                watch.Restart();
                var hash = 17;
                for (var round = 0; round < MachinePace.Work; round++)
                {
                    foreach (var value in PaceWork) hash = unchecked(hash * 31 + (value ^ round));
                }
                _paceSink ^= hash;
                times[i] = watch.Elapsed.TotalMilliseconds;
            }
            Array.Sort(times, System.Collections.Generic.Comparer<double>.Default);
            return _pace.Slowdown(times[2]);
        }

        /// <summary>How fast the machine runs between runs, the pace kept read first where no run read it yet.</summary>
        private static double IdlePace()
        {
            if (_pace == null) _pace = MachinePace.Load(Kept(PaceFile));
            return SamplePace();
        }

        /// <summary>Keeps the quickest the reference work took, for the runs to come.</summary>
        private static void KeepPace()
        {
            if (_pace != null) Steps.Run(() => File.WriteAllText(PaceFile, _pace.Save()), null);
        }
    }
}
