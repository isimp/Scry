using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class CompendiumBookTests
    {
        [Fact]
        public void ACrashLosesAtMostTenSecondsOfWhatTheCharacterLearned()
        {
            // The book and the character's dungeon walks are written at most this long after they
            // change (and at once on leaving a world or quitting), so a crash loses no more.
            Assert.Equal(10f, CompendiumBook.WriteEvery);
        }

        // What a character has come to know, entry by entry: unknown, heard of, seen or known,
        // only ever rising, kept from session to session.

        [Fact]
        public void ATextReadIsKeptInTheBookWhateverItsLabel()
        {
            // The game keeps a runestone's text read only where it has a label; the book keeps every one.
            var book = new CompendiumBook();
            Assert.False(book.HasRead("$lore_meadows_random01"));
            book.Save();
            Assert.True(book.ReadText("$lore_meadows_random01"));
            Assert.True(book.Changed);
            Assert.False(book.ReadText("$lore_meadows_random01"));
            Assert.False(book.ReadText(""));
            Assert.False(book.ReadText(null));
            Assert.True(book.HasRead("$lore_meadows_random01"));
            Assert.False(book.HasRead("$lore_meadows_random02"));
            Assert.False(book.HasRead(null));
            var kept = CompendiumBook.Load(book.Save());
            Assert.True(kept.HasRead("$lore_meadows_random01"));
            Assert.False(kept.HasRead("$lore_meadows_random02"));
        }

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
        public void AnEntryNoRecordTellsAnyLongerIsTakenBackWhole()
        {
            // A kill once counted for a creature it never stood for: its level, the kill and its
            // news go, the rest stays, and it stays gone once the book is kept and read again.
            var killed = EntryKeys.Did(Deed.Killed);
            var book = new CompendiumBook();
            book.Raise("Greydwarf", KnownLevel.Known);
            book.FoundOut("Greydwarf", killed);
            book.Raise("Greydwarf_Frozen", KnownLevel.Known);
            book.FoundOut("Greydwarf_Frozen", killed);
            var version = book.Version;
            var pairs = book.PairsVersion;

            Assert.True(book.TakeBack("Greydwarf_Frozen", killed));

            Assert.Equal(KnownLevel.Unknown, book.Level("Greydwarf_Frozen"));
            Assert.False(book.Proved("Greydwarf_Frozen", killed));
            Assert.Null(book.FoundOutOf("Greydwarf_Frozen"));
            Assert.DoesNotContain("Greydwarf_Frozen", book.Lately);
            Assert.Equal(KnownLevel.Known, book.Level("Greydwarf"));
            Assert.True(book.Proved("Greydwarf", killed));
            Assert.True(book.Version > version && book.PairsVersion > pairs && book.Changed);
            Assert.False(book.TakeBack("Greydwarf_Frozen", killed));
            Assert.False(book.TakeBack(null, killed));

            // Counted on top, a level only rises: what was counted before is counted whole again.
            var raised = new System.Collections.Generic.List<string>();
            Assert.False(book.RaisedSince(version, raised));
            var after = book.Version;
            book.Raise("Boar", KnownLevel.Known);
            Assert.True(book.RaisedSince(after, raised));
            Assert.Equal(new[] { "Boar" }, raised);

            var kept = CompendiumBook.Load(book.Save());
            Assert.Equal(KnownLevel.Unknown, kept.Level("Greydwarf_Frozen"));
            Assert.False(kept.Proved("Greydwarf_Frozen", killed));
            Assert.Equal(KnownLevel.Known, kept.Level("Greydwarf"));
        }

        [Fact]
        public void WhatWasRaisedSinceAVersionIsToldOldestFirstWhileTheBookRemembersIt()
        {
            // What the panel counts on top of what it counted before (CompendiumView.Tell).
            var book = new CompendiumBook();
            book.Raise("Troll", KnownLevel.Seen);
            var version = book.Version;
            book.Raise("Boar", KnownLevel.Known);
            book.Raise("Troll", KnownLevel.Known);
            book.Raise("Troll", KnownLevel.Known);

            var raised = new System.Collections.Generic.List<string>();
            Assert.True(book.RaisedSince(version, raised));
            Assert.Equal(new[] { "Boar", "Troll" }, raised);
            raised.Clear();
            Assert.True(book.RaisedSince(book.Version, raised));
            Assert.Empty(raised);
            Assert.False(book.RaisedSince(book.Version + 1, raised));

            for (var i = 0; i < CompendiumBook.RaisesKept; i++) book.Raise("thing" + i, KnownLevel.Seen);
            Assert.False(book.RaisedSince(version, raised));
            Assert.Empty(raised);
            Assert.True(book.RaisedSince(book.Version - 10, raised));
            Assert.Equal(10, raised.Count);
        }

        [Fact]
        public void ABookWrittenOnAWorkerIsTheBookAsItStoodWhenItWasHandedOver()
        {
            // Writing the text costs a few milliseconds, so it is worked out on a worker thread
            // while play goes on; what is learned meanwhile waits for the next save.
            var book = Learned();
            var twin = Learned();

            var later = book.ToSave();
            Assert.False(book.Changed);
            book.Raise("Troll", KnownLevel.Known, way: CameBy.Fought);
            book.Raise("Boar", KnownLevel.Known);
            book.Witness("Troll", "BlackForest");
            book.MarkMapped("Other");
            book.Rule = CompendiumRule.Loose;
            book.Floor = 2;
            Assert.True(book.Changed);

            var read = CompendiumBook.Load(later());
            Assert.Equal(KnownLevel.Seen, read.Level("Neck"));
            Assert.Equal(KnownLevel.Unknown, read.Level("Troll"));
            Assert.Equal(new[] { "Neck", "Boar" }, read.Lately);
            Assert.False(read.Witnessed("Troll", "BlackForest"));
            Assert.False(read.Mapped("Other"));
            Assert.Equal(CompendiumRule.Strict, read.Rule);
            Assert.Equal(1, read.Floor);
            Assert.Null(read.Came("Troll"));
            // Everything else as a save made then would have it.
            Assert.Equal(twin.Save(), later());

            static CompendiumBook Learned()
            {
                var learned = new CompendiumBook { Now = () => new Moment(3, "Meadows") };
                learned.Raise("Boar", KnownLevel.Known, way: CameBy.Killed);
                learned.Raise("Neck", KnownLevel.Seen);
                learned.Witness("Boar", "Meadows");
                learned.MarkMapped("World");
                learned.Rule = CompendiumRule.Strict;
                learned.Floor = 1;
                return learned;
            }
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
        public void WhatWasLearnedComesNewestFirstAndAllOfItIsKept()
        {
            var book = new CompendiumBook();
            for (var i = 0; i < 55; i++) book.Raise("Item" + i, KnownLevel.Known);
            book.Raise("Item3", KnownLevel.Known);
            book.Raise("Item54", KnownLevel.Known);
            book.Raise("Troll", KnownLevel.Seen);
            book.Raise("Troll", KnownLevel.Known);

            var lately = book.Lately.ToList();

            Assert.Equal(56, lately.Count);
            Assert.Equal("Troll", lately[0]);
            Assert.Equal("Item54", lately[1]);
            Assert.Single(lately, k => k == "Troll");
            Assert.Equal("Item0", lately.Last());
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
        public void EachCharacterIsKeptInAFileNamedByItsOwnFileWhereThatLivesAndItsId()
        {
            Assert.Equal("Astrid-Local-17.dat", CompendiumBook.FileName("Astrid", "Local", 17));
            Assert.NotEqual(CompendiumBook.FileName("Astrid", "Local", 17), CompendiumBook.FileName("Astrid", "Cloud", 17));
            // A character made anew under the name of one deleted gets an id of its own, so a book of its own.
            Assert.NotEqual(CompendiumBook.FileName("Astrid", "Local", 17), CompendiumBook.FileName("Astrid", "Local", 18));
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
        public void TheRulesChosenForTheCompendiumAreKeptWithTheCharacter()
        {
            // Chosen in the panel, each character its own: a new one starts off, and off is not written down.
            var book = new CompendiumBook();
            Assert.Equal(CompendiumRule.Off, book.Rule);
            Assert.DoesNotContain("rule", book.Save());

            book.Rule = CompendiumRule.Strict;
            Assert.True(book.Changed);
            var back = CompendiumBook.Load(book.Save());
            Assert.Equal(CompendiumRule.Strict, back.Rule);
            Assert.False(book.Changed);

            book.Rule = CompendiumRule.Strict;
            Assert.False(book.Changed);
            book.Rule = CompendiumRule.Loose;
            Assert.Equal(CompendiumRule.Loose, CompendiumBook.Load(book.Save()).Rule);
            book.Rule = CompendiumRule.Off;
            Assert.True(book.Changed);
            Assert.DoesNotContain("rule", book.Save());

            // Only a rule's own name reads back; anything else leaves it off.
            Assert.Equal(CompendiumRule.Off, CompendiumBook.Load("rule\tlots").Rule);
            Assert.Equal(CompendiumRule.Off, CompendiumBook.Load("rule\t2").Rule);
            Assert.Equal(CompendiumRule.Off, CompendiumBook.Load("rule\tstrict").Rule);
            Assert.Equal(CompendiumRule.Off, CompendiumBook.Load("rule\t99").Rule);
        }

        [Fact]
        public void AFloorSavedPastTheLastTierReadsAsTheLastTier()
        {
            Assert.Equal(Tiers.Last, CompendiumBook.Load("floor\t99").ChosenFloor);
            Assert.Equal(2, CompendiumBook.Load("floor\t2").ChosenFloor);
            Assert.Equal(0, CompendiumBook.Load("floor\tlots").ChosenFloor);
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
        public void GuessesAtWhatRanInAnotherPlayersGameAreReadPastAndNotWrittenAgain()
        {
            // A book from before events crossed between games kept counts of the character's
            // kills, breaks and picks another game ran, and that it played with others; the
            // events themselves replaced both.
            var back = CompendiumBook.Load("know\tTroll\t3\nelsewhere\tTroll\tKill\t2\nelsewhere\tBeech1\tBreak\t1\nplayed\twith-others\nknow\tDeer\t2");

            Assert.Equal(KnownLevel.Known, back.Level("Troll"));
            Assert.Equal(KnownLevel.Seen, back.Level("Deer"));
            var saved = back.Save();
            Assert.DoesNotContain("elsewhere", saved);
            Assert.DoesNotContain("played", saved);
            Assert.Equal(saved, CompendiumBook.Load(saved).Save());
        }

        [Fact]
        public void WhatWasLearnedReadBackHoldsEachEntryOnce()
        {
            var text = "lately\tTroll\nlately\tTroll\n" + string.Concat(Enumerable.Range(0, 60).Select(i => $"lately\tItem{i}\n"));

            var lately = CompendiumBook.Load(text).Lately.ToList();

            Assert.Equal(61, lately.Count);
            Assert.Single(lately, k => k == "Troll");
        }

        [Fact]
        public void NothingKeptIsAnEmptyBook()
        {
            Assert.Equal(0, CompendiumBook.Load(null).Count(KnownLevel.HeardOf));
            Assert.Equal(0, CompendiumBook.Load("").Count(KnownLevel.HeardOf));
        }

        [Fact]
        public void ForgettingChestsDropsWhatTheyHeldButKeepsWhichWereOpened()
        {
            // Chests counted again after each world load left tallies too high in older books:
            // forgotten, they count afresh, but a chest opened before never counts a second time.
            var book = new CompendiumBook();
            var chest = CompendiumBook.ChestId("w", "TreasureChest_meadows", 1f, 2f, 3f);
            Assert.True(book.FirstOpen(chest));
            book.Held("TreasureChest_meadows", new[] { ("Coins", 12) });
            book.Held("TreasureChest_meadows", new[] { ("Coins", 9) });
            book.Held("TreasureChest_forestcrypt", new[] { ("Amber", 1) });
            book.Save();

            Assert.Equal(2, book.ForgetChests());
            Assert.True(book.Changed);
            Assert.Equal(0, book.Tallied(Tally.Held).Kills("TreasureChest_meadows"));
            Assert.Empty(book.Tallied(Tally.Held).Of("TreasureChest_forestcrypt"));
            Assert.False(book.FirstOpen(chest));
            var kept = CompendiumBook.Load(book.Save());
            Assert.Equal(0, kept.Tallied(Tally.Held).Kills("TreasureChest_meadows"));
            Assert.False(kept.FirstOpen(chest));
            Assert.Equal(0, kept.ForgetChests());
        }
    }
}
