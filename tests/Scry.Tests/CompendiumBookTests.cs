using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class CompendiumBookTests
    {
        // What a character has come to know, entry by entry: unknown, heard of, seen or known,
        // only ever rising, kept from session to session.

        [Fact]
        public void WhatWasNeverMetIsUnknown()
        {
            var book = new CompendiumBook();

            Assert.Equal(KnownLevel.Unknown, book.Level("Troll"));
            Assert.Equal(KnownLevel.Unknown, book.Level(null));
            Assert.Equal(KnownLevel.Unknown, book.Level(""));
        }

        [Fact]
        public void KnowledgeOnlyRises()
        {
            var book = new CompendiumBook();

            Assert.True(book.Raise("Troll", KnownLevel.Seen));
            Assert.True(book.Raise("Troll", KnownLevel.Known));
            Assert.False(book.Raise("Troll", KnownLevel.HeardOf));
            Assert.False(book.Raise("Troll", KnownLevel.Known));

            Assert.Equal(KnownLevel.Known, book.Level("Troll"));
        }

        [Fact]
        public void NothingIsLearnedOfNoEntryOrAtNoLevel()
        {
            var book = new CompendiumBook();

            Assert.False(book.Raise(null, KnownLevel.Known));
            Assert.False(book.Raise("", KnownLevel.Known));
            Assert.False(book.Raise("Troll", KnownLevel.Unknown));
            Assert.Equal(0, book.Version);
            Assert.False(book.Changed);
        }

        [Fact]
        public void EachThingLearnedIsCountedAndMarksTheBookToBeKept()
        {
            var book = new CompendiumBook();

            book.Raise("Troll", KnownLevel.Seen);
            book.Raise("Troll", KnownLevel.Seen);
            book.Raise("Wood", KnownLevel.Known);

            Assert.Equal(2, book.Version);
            Assert.True(book.Changed);
            book.Save();
            Assert.False(book.Changed);
        }

        [Fact]
        public void WhatWasLearnedSinceACountTakenBeforeIsToldNewestFirst()
        {
            // The panel tells what is new since it last told; what was known before, only written
            // down now, is nothing new, and a creature seen before and fought since is.
            var book = new CompendiumBook();
            book.Raise("Troll", KnownLevel.Seen);
            book.Raise("Boar", KnownLevel.Known);
            var mark = book.Learned;

            book.Raise("Neck", KnownLevel.Seen);
            book.Raise("Wood", KnownLevel.Known, quietly: true);
            book.Raise("Troll", KnownLevel.Known);
            book.Raise("Boar", KnownLevel.Known);

            Assert.Equal(new[] { "Troll", "Neck" }, book.LearnedSince(mark).ToArray());
            Assert.Empty(book.LearnedSince(book.Learned));
            Assert.Equal(new[] { "Troll", "Neck", "Boar" }, book.LearnedSince(0).ToArray());
        }

        [Fact]
        public void WhatWasLearnedLatelyComesNewestFirstAndOnlyTheLatestAreKept()
        {
            var book = new CompendiumBook();
            for (var i = 0; i < CompendiumBook.LatelyKept + 5; i++) book.Raise("Item" + i, KnownLevel.Known);
            book.Raise("Item3", KnownLevel.Known);
            book.Raise("Item" + (CompendiumBook.LatelyKept + 4), KnownLevel.Known);
            book.Raise("Troll", KnownLevel.Seen);
            book.Raise("Troll", KnownLevel.Known);

            var lately = book.Lately.ToList();

            Assert.Equal(CompendiumBook.LatelyKept, lately.Count);
            Assert.Equal("Troll", lately[0]);
            Assert.Equal("Item" + (CompendiumBook.LatelyKept + 4), lately[1]);
            Assert.Single(lately, k => k == "Troll");
            Assert.DoesNotContain("Item0", lately);
        }

        [Fact]
        public void WhatIsWrittenDownQuietlyIsKnownAndKeptButNotAmongTheLatest()
        {
            var book = new CompendiumBook();
            book.Raise("Boar", KnownLevel.Seen);
            book.Save();
            var version = book.Version;

            Assert.True(book.Raise("Troll", KnownLevel.Known, quietly: true));
            Assert.True(book.Raise("Boar", KnownLevel.Known, quietly: true));

            Assert.Equal(KnownLevel.Known, book.Level("Troll"));
            Assert.Equal(new[] { "Boar" }, book.Lately.ToArray());
            Assert.True(book.Version > version);
            Assert.True(book.Changed);
        }

        [Fact]
        public void EachCharacterIsKeptInAFileNamedByItsOwnFileAndWhereThatLives()
        {
            Assert.Equal("Kevin-Local.dat", CompendiumBook.FileName("Kevin", "Local"));
            Assert.NotEqual(CompendiumBook.FileName("Kevin", "Local"), CompendiumBook.FileName("Kevin", "Cloud"));
        }

        [Fact]
        public void HowManyAreKnownAtLeastSoFar()
        {
            var book = new CompendiumBook();
            book.Raise("Troll", KnownLevel.Known);
            book.Raise("Boar", KnownLevel.Seen);
            book.Raise("Wood", KnownLevel.HeardOf);

            Assert.Equal(3, book.Count(KnownLevel.HeardOf));
            Assert.Equal(2, book.Count(KnownLevel.Seen));
            Assert.Equal(1, book.Count(KnownLevel.Known));
        }

        [Fact]
        public void AWorldsMapIsCaughtUpOnce()
        {
            var book = new CompendiumBook();

            Assert.False(book.Mapped("123"));
            book.MarkMapped("123");
            Assert.True(book.Mapped("123"));
            Assert.False(book.Mapped("456"));
            Assert.True(book.Changed);
        }

        [Fact]
        public void MarkingAWorldMarksTheBookToBeKeptOnlyWhenItChangesSomething()
        {
            var book = new CompendiumBook();
            book.MarkMapped("123");
            Assert.True(book.Changed);
            book.Save();
            book.MarkMapped("123");
            Assert.False(book.Changed);
            book.Unmap("123");
            Assert.True(book.Changed);
        }

        [Fact]
        public void AWorldsCatchUpCanBeForgottenToRunAgain()
        {
            var book = new CompendiumBook();
            book.MarkMapped("123");
            book.MarkMapped("456");
            book.Save();

            book.Unmap("123");

            Assert.False(book.Mapped("123"));
            Assert.True(book.Mapped("456"));
            Assert.True(book.Changed);

            book.Save();
            book.Unmap("999");
            book.Unmap(null);
            Assert.False(book.Changed);
        }

        [Fact]
        public void WhatIsKeptReadsBackTheSame()
        {
            var book = new CompendiumBook();
            book.Raise("Troll", KnownLevel.Known);
            book.Raise("se:Rested", KnownLevel.Seen);
            book.Raise("odd\tname", KnownLevel.HeardOf);
            book.Raise("Boar", KnownLevel.Known);
            book.MarkMapped("123");

            var back = CompendiumBook.Load(book.Save());

            Assert.Equal(KnownLevel.Known, back.Level("Troll"));
            Assert.Equal(KnownLevel.Seen, back.Level("se:Rested"));
            Assert.Equal(KnownLevel.HeardOf, back.Level("odd\tname"));
            Assert.Equal(book.Lately, back.Lately);
            Assert.True(back.Mapped("123"));
            Assert.False(back.Changed);
            Assert.Equal(book.Save(), back.Save());
        }

        [Fact]
        public void ALineThatCannotBeReadIsPassedOver()
        {
            // "asked" lines kept a question no longer asked: read past and not written again.
            var back = CompendiumBook.Load("know\tTroll\t3\nknow\tBoar\tlots\nknow\t\t2\nknow\tWood\t9\nknow\tStone\t0\nsomething\nmapped\t\nasked\t1\tmaybe\nasked\t2\t1\nknow\tDeer\t2");

            Assert.Equal(KnownLevel.Known, back.Level("Troll"));
            Assert.Equal(KnownLevel.Seen, back.Level("Deer"));
            Assert.Equal(KnownLevel.Unknown, back.Level("Boar"));
            Assert.Equal(KnownLevel.Unknown, back.Level("Wood"));
            Assert.Equal(KnownLevel.Unknown, back.Level("Stone"));
            Assert.DoesNotContain("asked", back.Save());
            Assert.Equal(2, back.Count(KnownLevel.HeardOf));
            Assert.DoesNotContain(back.All, p => p.Key == "Stone");
        }

        [Fact]
        public void TheLatestReadBackHoldEachEntryOnceAndNoMoreThanAreKept()
        {
            var text = "lately\tTroll\nlately\tTroll\n" + string.Concat(Enumerable.Range(0, CompendiumBook.LatelyKept + 10).Select(i => $"lately\tItem{i}\n"));

            var lately = CompendiumBook.Load(text).Lately.ToList();

            Assert.Equal(CompendiumBook.LatelyKept, lately.Count);
            Assert.Single(lately, k => k == "Troll");
        }

        [Fact]
        public void NothingKeptIsAnEmptyBook()
        {
            Assert.Equal(0, CompendiumBook.Load(null).Count(KnownLevel.HeardOf));
            Assert.Equal(0, CompendiumBook.Load("").Count(KnownLevel.HeardOf));
        }
    }
}
