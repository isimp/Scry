using System;

namespace Scry
{
    /// <summary>
    /// For the self-test: what other threads allocate while Scry works, where the runtime tells
    /// only the memory in use (<see cref="AllocationMeter"/>), which grows by every thread's
    /// allocations. Each wait lasts the time it is given, doing nothing on the thread waiting, with
    /// the memory in use read before and after; what it grew by is added, as Scry's own parts are
    /// counted (<c>Timing</c>): never less than nothing, and nothing across a memory cleanup.
    /// </summary>
    internal sealed class OtherThreads
    {
        private readonly Func<long> _memory;
        private readonly Func<int> _cleanups;
        private readonly Func<double> _clockMs;

        /// <summary>Waits read the memory in use, the cleanups run so far and a clock in milliseconds.</summary>
        public OtherThreads(Func<long> memory, Func<int> cleanups, Func<double> clockMs)
        {
            _memory = memory;
            _cleanups = cleanups;
            _clockMs = clockMs;
        }

        /// <summary>What the memory in use grew by over the waits, in bytes.</summary>
        public long Bytes { get; private set; }

        /// <summary>How long the waits took in all, in milliseconds.</summary>
        public double Ms { get; private set; }

        /// <summary>Waits so many milliseconds doing nothing, adding what the memory in use grew by meanwhile; no time is no wait.</summary>
        public void Wait(double ms)
        {
            if (!(ms > 0)) return;
            var cleanups = _cleanups();
            var before = _memory();
            var started = _clockMs();
            var now = started;
            while (now - started < ms) now = _clockMs();
            var after = _memory();
            Ms += now - started;
            if (_cleanups() == cleanups) Bytes += Math.Max(0, after - before);
        }
    }
}
