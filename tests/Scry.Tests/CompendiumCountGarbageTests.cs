using System;
using System.Collections.Generic;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class CompendiumCountGarbageTests
    {
        // Walking a dungeon's rooms or reading places counts the compendium afresh, through every
        // entry; what that leaves for the game's memory cleanup becomes a pause in play, so a whole
        // count makes little garbage: it marks what the known name as heard of without gathering
        // every name first, and keeps its queue and lists from one count to the next.

        [Fact]
        public void AWholeCountOfABigCatalogLeavesLittleGarbage()
        {
            var all = new List<Entry>();
            for (var i = 0; i < 6000; i++) all.Add(E("e" + i, i % 3 == 0 ? Kind.Creature : Kind.Item));
            for (var i = 0; i < 6000; i++) for (var k = 1; k <= 8; k++) all[i].AddTermLink("drops", all[(i * 7 + k * 13) % 6000]);
            var view = new CompendiumView(new EntryCatalog(all));
            var book = new CompendiumBook();
            for (var i = 0; i < 6000; i += 2) book.Raise(all[i].Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Loose);
            view.Tell(book, CompendiumRule.Strict);

            var before = GC.GetAllocatedBytesForCurrentThread();
            view.Tell(book, CompendiumRule.Loose);
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;

            // 2 MB a count before; about 0.4 MB now.
            Assert.True(bytes < 1024 * 1024, $"{bytes / 1024} KB");
        }
    }
}
