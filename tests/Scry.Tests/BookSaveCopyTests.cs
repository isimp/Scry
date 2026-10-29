using System;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class BookSaveCopyTests
    {
        // A book is saved every few seconds while the character learns, its text written on a
        // worker thread from a copy made in the frame. A grown book holds thousands of pairs and
        // levels; copying them into fresh arrays each time made a megabyte of garbage a save.

        private static CompendiumBook Big()
        {
            var book = new CompendiumBook();
            for (var i = 0; i < 8000; i++) book.Witness("thing" + i, "other" + (i % 97));
            for (var i = 0; i < 2000; i++) book.Raise("entry" + i, i % 2 == 0 ? KnownLevel.Known : KnownLevel.Seen);
            for (var i = 0; i < 300; i++) book.SawWith("thing" + i, "drop" + i, Tie.Drops);
            return book;
        }

        [Fact]
        public void ASaveFromCopiesKeptFromTheLastTellsTheSameAsOneMadeAfresh()
        {
            var book = Big();
            var fresh = Big();
            book.ToSave(mayReuse: true)();
            fresh.ToSave()();
            book.Witness("late", "pair");
            fresh.Witness("late", "pair");
            book.Raise("late", KnownLevel.Known);
            fresh.Raise("late", KnownLevel.Known);

            Assert.Equal(fresh.ToSave()(), book.ToSave(mayReuse: true)());
        }

        [Fact]
        public void ASaveOnceTheLastIsWrittenMakesLittleGarbageForAGrownBook()
        {
            var book = Big();
            book.ToSave(mayReuse: true)();
            book.Witness("late", "pair");

            var before = GC.GetAllocatedBytesForCurrentThread();
            var text = book.ToSave(mayReuse: true);
            var made = GC.GetAllocatedBytesForCurrentThread() - before;
            text();

            Assert.True(made < 64 * 1024, $"the copy made {made / 1024} KB");
        }

        /// <summary>A book holding a line of every kind a save writes.</summary>
        private const string EveryKind =
            "format\t2\n" +
            "know\tBoar\t3\nknow\tWood\t3\nknow\tTroll\t2\n" +
            "lately\tWood\t12\tAWorld\nlately\tBoar\t11\n" +
            "met\tBoar\t11\tAWorld\n" +
            "mapped\t-101\n" +
            "read\t$lore_one\n" +
            "place\t-101\tExplored\tCrypt2\n" +
            "shows\tExplored\tKnown\n" +
            "marks\tExplored\n" +
            "rule\tStrict\n" +
            "floor\t2\n" +
            "welcomed\tyes\nmapapart\tyes\nbiomesapart\tyes\n" +
            "ruled\t-101\tThis server plays Strict.\n" +
            "saw\tBoar\tbiome:Meadows\nsaw\tWood\tBeech1\n" +
            "ways\tWood\tBeech1\tgives\n" +
            "did\tBoar\tdid:Killed\n" +
            "attacksaspairs\tyes\n" +
            "sight\tBoar\tstars:0\n" +
            "came\tWood\tHeld\t12\tMeadows\tDay\tClear\tAxeStone\tAWorld\n" +
            "seen\tTroll\t-1\t11\tBlackForest\tNight\tRain\tClub\t\n" +
            "died\tTroll\t2\t11\tAWorld\n" +
            "found\tBoar\tdmg:Slash\n" +
            "fell\tkills\tBoar\t2\nfell\tdrop\tBoar\tLeatherScraps\t2\t3\t1\t2\n" +
            "starfell\tkills\tBoar@0\t2\n" +
            "gave\tkills\tBeech1\t1\ngave\tdrop\tBeech1\tWood\t1\t4\t4\t4\n" +
            "held\tkills\tTreasureChest_meadows\t1\n" +
            "weather\tkills\tMeadows\t3\n" +
            "opened\t-101/TreasureChest_meadows@1:2:3\n";

        [Fact]
        public void EveryKindOfLineSavesTheSameFromKeptCopies()
        {
            var book = CompendiumBook.Load(EveryKind);
            var fresh = CompendiumBook.Load(EveryKind);
            // Every kind is read back and written again, so each one's copy is held to it.
            var saved = fresh.ToSave()();
            foreach (var kind in EveryKind.Split('\n').Where(l => l.Length > 0).Select(l => l.Split('\t')[0]).Distinct())
            {
                Assert.Contains("\n" + kind + "\t", "\n" + saved);
            }
            Assert.Equal(saved, book.ToSave(mayReuse: true)());
            foreach (var each in new[] { book, fresh })
            {
                each.Raise("Greydwarf", KnownLevel.Known, way: CameBy.Killed);
                each.Witness("Greydwarf", "biome:BlackForest");
                each.Prove("Greydwarf", EntryKeys.Did(Deed.Killed));
            }

            Assert.Equal(fresh.ToSave()(), book.ToSave(mayReuse: true)());
            Assert.Contains("came\tGreydwarf", fresh.ToSave()());
        }

        [Fact]
        public void AKeptCopyIsFilledAgainOnlyWhereItMayBe()
        {
            var copy = new SaveCopy<string>();
            var first = copy.Of(new[] { "a", "b" }, mayReuse: false);
            var again = copy.Of(new[] { "c" }, mayReuse: true);
            var apart = copy.Of(new[] { "d", "e", "f" }, mayReuse: false);

            Assert.Same(first.Array, again.Array);
            Assert.NotSame(again.Array, apart.Array);
            Assert.Equal(new[] { "c" }, again);
            Assert.Equal(new[] { "d", "e", "f" }, apart);
            // Grown past its room, it holds them all.
            var many = copy.Of(System.Linq.Enumerable.Range(0, 100).Select(i => "n" + i), mayReuse: true);
            Assert.Equal(100, many.Count);
            Assert.Equal("n99", many.Array[99]);
        }

        [Fact]
        public void ASaveAskedWhileTheLastMayStillBeWrittenKeepsItsOwnCopy()
        {
            // The last save's text may not be written yet: its copy must stay as it was.
            var book = Big();
            var first = book.ToSave(mayReuse: true);
            var expected = Big().ToSave()();
            book.Witness("late", "pair");
            book.Raise("late", KnownLevel.Known);
            book.ToSave();

            Assert.Equal(expected, first());
        }
    }
}
