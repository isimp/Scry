using System;
using Xunit;

namespace Scry.Tests
{
    public class AllocationMeterTests
    {
        // Scry times what each part of its work allocates. The memory in use counts what every
        // thread allocates meanwhile, so a part that runs long takes on other mods' allocations
        // too; where the runtime counts what each thread allocates, that is what is read.

        private static long Whole() => GC.GetTotalMemory(false);

        [Fact]
        public void ACountOfWhatThisThreadAllocatesIsReadWhereItCounts()
        {
            Func<long> perThread = GC.GetAllocatedBytesForCurrentThread;
            Func<long> whole = Whole;

            Assert.Same(perThread, AllocationMeter.Pick(perThread, whole));
        }

        [Fact]
        public void TheMemoryInUseIsReadWhereNoCountOfTheThreadIsKept()
        {
            // A runtime that has the count but does not keep it reads nought, or fails.
            Func<long> whole = Whole;

            Assert.Same(whole, AllocationMeter.Pick(() => 0L, whole));
            Assert.Same(whole, AllocationMeter.Pick(() => throw new NotSupportedException("no count"), whole));
            Assert.Same(whole, AllocationMeter.Pick(null, whole));
        }
    }
}
