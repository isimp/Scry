using System;

namespace Scry
{
    /// <summary>
    /// What is read for how much a part of Scry's work allocates: the runtime's count of what the
    /// thread asking allocated, where it keeps one, else the memory in use, which counts what every
    /// thread allocates meanwhile too, so a part that runs long takes on other mods' allocations.
    /// </summary>
    internal static class AllocationMeter
    {
        /// <summary>How much is made between two reads to see whether a count of the thread is kept.</summary>
        private const int Probe = 4096;

        /// <summary>
        /// <paramref name="perThread"/> where it counts what this thread allocates, as an array made
        /// between two reads of it shows; else <paramref name="whole"/>, as where the runtime has
        /// the count but keeps none, reading nought, or fails to read it.
        /// </summary>
        public static Func<long> Pick(Func<long> perThread, Func<long> whole)
        {
            if (Steps.Run(perThread, out var before, null) != null) return whole;
            var made = new byte[Probe];
            if (Steps.Run(perThread, out var after, null) != null) return whole;
            GC.KeepAlive(made);
            return after - before >= Probe ? perThread : whole;
        }
    }
}
