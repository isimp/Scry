using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class WakeChunksTests
    {
        // A location's copy can hold thousands of parts, and waking them all at once holds the
        // game up for a third of a second. It is woken a few parts each frame instead: each part it
        // is woken in holds no more than so many, and one holding more is gone into, woken itself
        // with the copy while its own parts wait their turn.

        /// <summary>A copy's parts as a list of parents, the root's being -1, all switched on.</summary>
        private static List<int> Parents(params int[] parents) => parents.ToList();

        private static List<bool> AllOn(int count) => Enumerable.Repeat(true, count).ToList();

        /// <summary>A root with so many parts straight under it.</summary>
        private static List<int> Flat(int count) => new[] { -1 }.Concat(Enumerable.Repeat(0, count)).ToList();

        [Fact]
        public void ASmallCopyWakesAtOnce()
        {
            Assert.Empty(WakeChunks.Pick(Flat(4), AllOn(5), 10));
            // As many parts as a chunk may hold, the root among them, is still small.
            Assert.Empty(WakeChunks.Pick(Flat(9), AllOn(10), 10));
        }

        [Fact]
        public void ManyPartsUnderTheRootWakeOneByOneInTheirOrder()
        {
            var chunks = WakeChunks.Pick(Flat(30), AllOn(31), 10);
            Assert.Equal(Enumerable.Range(1, 30), chunks);
        }

        [Fact]
        public void APartHoldingTooManyIsGoneIntoAndASmallOneWokenWhole()
        {
            // 0 the root; 1 a big part with 25 parts (2 to 26); 27 a small one with 3 (28 to 30).
            var parents = new List<int> { -1, 0 };
            parents.AddRange(Enumerable.Repeat(1, 25));
            parents.Add(0);
            parents.AddRange(Enumerable.Repeat(27, 3));

            var chunks = WakeChunks.Pick(parents, AllOn(parents.Count), 10);

            Assert.Equal(Enumerable.Range(2, 25).Concat(new[] { 27 }), chunks);
        }

        [Fact]
        public void APartOfExactlyAChunksSizeIsWokenWhole()
        {
            // 1 holds 9 parts, 10 with itself; 11 holds 10, 11 with itself.
            var parents = new List<int> { -1, 0 };
            parents.AddRange(Enumerable.Repeat(1, 9));
            parents.Add(0);
            parents.AddRange(Enumerable.Repeat(11, 10));

            var chunks = WakeChunks.Pick(parents, AllOn(parents.Count), 10);

            Assert.Equal(new[] { 1 }.Concat(Enumerable.Range(12, 10)), chunks);
        }

        [Fact]
        public void PartsSwitchedOffStayAsTheyAreAndDoNotCount()
        {
            // Twenty parts switched off and three on: a small copy.
            var on = new[] { true }.Concat(Enumerable.Repeat(false, 20)).Concat(Enumerable.Repeat(true, 3)).ToList();
            Assert.Empty(WakeChunks.Pick(Flat(23), on, 10));

            // 1 is on with 25 parts; 27 is off, and so are the 30 parts under it, though they are on themselves.
            var parents = new List<int> { -1, 0 };
            parents.AddRange(Enumerable.Repeat(1, 25));
            parents.Add(0);
            parents.AddRange(Enumerable.Repeat(27, 30));
            var some = AllOn(parents.Count);
            some[27] = false;

            var chunks = WakeChunks.Pick(parents, some, 10);

            Assert.Equal(Enumerable.Range(2, 25), chunks);

            // A copy switched off at its root does not wake, and has nothing to wake in parts.
            var off = AllOn(31);
            off[0] = false;
            Assert.Empty(WakeChunks.Pick(Flat(30), off, 10));
        }

        [Fact]
        public void TheOrderOfTheListDoesNotMatterOnlyWhatHangsWhere()
        {
            // The root last, its parts before it, a big part's own parts before that part.
            var parents = new List<int>();
            parents.AddRange(Enumerable.Repeat(12, 12)); // 0 to 11 hang on 12
            parents.Add(13);                              // 12 hangs on the root
            parents.Add(-1);                              // 13 is the root
            parents.Add(13);                              // 14 a small part on the root

            var chunks = WakeChunks.Pick(parents, AllOn(parents.Count), 10);

            Assert.Equal(Enumerable.Range(0, 12).Concat(new[] { 14 }), chunks);
        }

        [Fact]
        public void AChunkOfOneMeansEachPartOnItsOwn()
        {
            // A chain: each part holds the next, so every part but the last is gone into.
            var chunks = WakeChunks.Pick(Parents(-1, 0, 1, 2), AllOn(4), 1);
            Assert.Equal(new[] { 3 }, chunks);
        }
    }
}
