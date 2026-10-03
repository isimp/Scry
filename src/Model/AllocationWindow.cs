using System;

namespace Scry
{
    /// <summary>
    /// A stretch of time over which the whole game's allocation, Scry's share of it and the
    /// runtime's memory cleanups are counted. The game's allocation is how much the memory in
    /// use grows from frame to frame; a frame with a cleanup has its drop in memory left out,
    /// since what was allocated across it is unknown, so the total is a lower bound.
    /// </summary>
    public sealed class AllocationWindow
    {
        private long _last;
        private int _lastCollections;
        private bool _started;

        public long AllBytes { get; private set; }
        public long ScryBytes { get; private set; }
        public int Cleanups { get; private set; }
        public int ScryCleanups { get; private set; }

        /// <summary>The memory in use and the cleanups run so far, once a frame.</summary>
        public void Sample(long memoryInUse, int collections)
        {
            if (_started)
            {
                if (collections > _lastCollections) Cleanups += collections - _lastCollections;
                else if (memoryInUse > _last) AllBytes += memoryInUse - _last;
            }
            _started = true;
            _last = memoryInUse;
            _lastCollections = collections;
        }

        /// <summary>What Scry's own work allocated, and the cleanups that ran during it.</summary>
        public void AddScry(long bytes, int cleanups)
        {
            ScryBytes += Math.Max(0, bytes);
            ScryCleanups += Math.Max(0, cleanups);
        }

        /// <summary>Starts counting again from the last sample.</summary>
        public void Reset()
        {
            AllBytes = 0;
            ScryBytes = 0;
            Cleanups = 0;
            ScryCleanups = 0;
        }

        public string Line(double seconds)
        {
            string Mb(long bytes) => Numbers.Amount(bytes / (1024.0 * 1024.0), 0);
            var share = AllBytes > 0 ? $" ({Numbers.Percent((double)ScryBytes / AllBytes)})" : "";
            return $"In the last {Numbers.Amount(seconds, 0)} s: {Numbers.Count(Cleanups)} memory cleanups, {Numbers.Count(ScryCleanups)} of them during Scry's work; "
                + $"Scry allocated about {Mb(ScryBytes)} MB of the {Mb(AllBytes)} MB allocated in all{share}.";
        }
    }
}
