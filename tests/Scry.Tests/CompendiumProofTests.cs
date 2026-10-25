using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// What the character saw done (a kill, a hit telling how a type lands, a weak spot found, an
    /// attack taken) is kept apart from what it saw go together, so a rule about one never reads the other.
    /// </summary>
    public class CompendiumProofTests
    {
        [Fact]
        public void AProofIsKeptApartFromWhatWasSeenTogether()
        {
            var book = new CompendiumBook();
            var killed = EntryKeys.Did(Deed.Killed);
            Assert.True(book.Prove("Troll", killed));
            Assert.True(book.Proved("Troll", killed));
            Assert.False(book.Witnessed("Troll", killed));
            Assert.False(book.Prove("Troll", killed));

            Assert.True(book.Witness("Troll", "TrollHide"));
            Assert.True(book.Witnessed("Troll", "TrollHide"));
            Assert.False(book.Proved("Troll", "TrollHide"));

            Assert.Equal(1, book.Pairs);
            Assert.Equal(1, book.Proofs);
            Assert.False(book.Prove("", killed));
            Assert.False(book.Prove("Troll", null));
            Assert.False(book.Proved(null, killed));
        }

        [Fact]
        public void AProofMovesTheCountThatListsTheStrictRulesAgain()
        {
            // A proof changes what a strict page keeps back, as a pair changes what it leaves out:
            // both move the one count the panel lists again by.
            var book = new CompendiumBook();
            var before = book.PairsVersion;
            book.Prove("Troll", EntryKeys.Damage("Fire"));
            Assert.True(book.PairsVersion > before);
            Assert.True(book.Changed);
        }

        [Fact]
        public void ProofsAreKeptAsTheirOwnLinesAndReadBack()
        {
            var book = new CompendiumBook();
            book.Prove("Troll", EntryKeys.Did(Deed.Killed));
            book.Prove("Troll", EntryKeys.WeakSpot("head"));
            book.Witness("Troll", "TrollHide");
            var text = book.Save();
            Assert.Contains("did\tTroll\tdid:Killed\n", text);
            Assert.Contains("did\tTroll\tspot:head\n", text);
            Assert.Contains("saw\tTroll\tTrollHide\n", text);
            Assert.DoesNotContain("saw\tTroll\tdid:Killed", text);

            var read = CompendiumBook.Load(text);
            Assert.True(read.Proved("Troll", EntryKeys.Did(Deed.Killed)));
            Assert.True(read.Proved("Troll", EntryKeys.WeakSpot("head")));
            Assert.False(read.Witnessed("Troll", EntryKeys.Did(Deed.Killed)));
            Assert.True(read.Witnessed("Troll", "TrollHide"));
            Assert.Equal(2, read.Proofs);
            Assert.Equal(1, read.Pairs);
        }

        [Fact]
        public void AnOlderBookKeptProofsAmongItsPairsAndTheyAreReadAsProofs()
        {
            // Books kept before held deeds, damage types and weak spots as pairs, either way round.
            var read = CompendiumBook.Load("saw\tTroll\tdid:Killed\nsaw\tdmg:Fire\tTroll\nsaw\tTroll\tspot:head\nsaw\tTroll\tTrollHide\n");
            Assert.True(read.Proved("Troll", EntryKeys.Did(Deed.Killed)));
            Assert.True(read.Proved("Troll", EntryKeys.Damage("Fire")));
            Assert.True(read.Proved("Troll", EntryKeys.WeakSpot("head")));
            Assert.False(read.Witnessed("Troll", EntryKeys.Did(Deed.Killed)));
            Assert.True(read.Witnessed("Troll", "TrollHide"));
            Assert.Equal(3, read.Proofs);
            Assert.Equal(1, read.Pairs);
            // Kept again, they are proofs.
            var again = CompendiumBook.Load(read.Save());
            Assert.True(again.Proved("Troll", EntryKeys.Damage("Fire")));
            Assert.Contains("did\tTroll\tdmg:Fire\n", read.Save());
        }

        [Fact]
        public void AnAttackTakenIsAProofByItsOwnKey()
        {
            var book = new CompendiumBook();
            var log = EntryKeys.Attack("Troll_log");
            Assert.Equal("attack:Troll_log", log);
            Assert.True(EntryKeys.IsAttack(log, out var item));
            Assert.Equal("Troll_log", item);
            Assert.False(EntryKeys.IsAttack("Troll_log", out _));
            Assert.False(EntryKeys.IsAttack(null, out _));

            Assert.True(book.FoundOut("Troll", log));
            Assert.True(book.Proved("Troll", log));
            Assert.False(book.Witnessed("Troll", "Troll_log"));
            Assert.Contains("did\tTroll\tattack:Troll_log\n", book.Save());
            // A pair with the item, as a new book might hold for a creature's own weapon seen, is no proof.
            var fresh = new CompendiumBook();
            fresh.Witness("Troll", "Troll_log");
            Assert.False(fresh.Proved("Troll", log));
        }

        [Fact]
        public void AnOlderBookKeptAttacksTakenAsPlainPairsWhichStillCount()
        {
            // Before attacks had a key of their own, an attack taken was the creature paired with its
            // weapon's prefab, which nothing can tell from any other pair; such a book counts them still.
            var read = CompendiumBook.Load("know\tTroll\t3\nsaw\tTroll\tTroll_log\n");
            Assert.True(read.Proved("Troll", EntryKeys.Attack("Troll_log")));
            Assert.False(read.Proved("Troll", EntryKeys.Attack("Troll_groundslam")));
            // Once the book keeps a proof of its own, it still counts those older pairs.
            read.Prove("Troll", EntryKeys.Attack("Troll_groundslam"));
            var again = CompendiumBook.Load(read.Save());
            Assert.True(again.Proved("Troll", EntryKeys.Attack("Troll_log")));
            Assert.True(again.Proved("Troll", EntryKeys.Attack("Troll_groundslam")));
            // A book never kept counts no pair as an attack.
            Assert.False(new CompendiumBook().Proved("Troll", EntryKeys.Attack("Troll_log")));
            Assert.False(CompendiumBook.Load("").Proved("Troll", EntryKeys.Attack("Troll_log")));
            // A book kept before proofs had lines of their own is told by its format line's absence, not by
            // whether it held proofs among its pairs: one with both counts its older pairs still.
            var mixed = CompendiumBook.Load("know\tTroll\t3\nknow\tDraugr\t3\nsaw\tTroll\tspot:WEAKSPOT_HEAD\nsaw\tDraugr\tdraugr_axe\n");
            Assert.True(mixed.Proved("Draugr", EntryKeys.Attack("draugr_axe")));
            Assert.True(mixed.Proved("Troll", EntryKeys.WeakSpot("WEAKSPOT_HEAD")));
            // One kept since says its format, and counts a pair as an attack only where it says so itself.
            var kept = CompendiumBook.Load("format\t2\nknow\tTroll\t3\nsaw\tTroll\tTroll_log\n");
            Assert.False(kept.Proved("Troll", EntryKeys.Attack("Troll_log")));
            Assert.StartsWith("format\t2\n", kept.Save());
            var migrated = CompendiumBook.Load(mixed.Save());
            Assert.True(migrated.Proved("Draugr", EntryKeys.Attack("draugr_axe")));
        }

        [Fact]
        public void ABookOfALaterFormatKeepsTheLinesThisScryCannotReadForItsNextSave()
        {
            // A newer Scry's book opened by this one: what this one reads, it keeps; the rest it carries along unread, so the newer Scry finds it again.
            var later = CompendiumBook.Load("format\t3\nknow\tTroll\t3\nfuture\tTroll\tx\ty\n");
            Assert.Equal(KnownLevel.Known, later.Level("Troll"));
            Assert.Contains("future\tTroll\tx\ty\n", later.Save());
            // Saved and read again, the book still says the later format and still carries the lines: the promise holds past one save.
            var again = CompendiumBook.Load(later.Save());
            Assert.StartsWith("format\t3\n", again.Save());
            Assert.Contains("future\tTroll\tx\ty\n", again.Save());
            // A book of this format drops a line it cannot read, as ever.
            Assert.DoesNotContain("future", CompendiumBook.Load("format\t2\nknow\tTroll\t3\nfuture\tTroll\tx\n").Save());
        }

        [Fact]
        public void TakingBackAnEntryForgetsItsProof()
        {
            var killed = EntryKeys.Did(Deed.Killed);
            var book = new CompendiumBook();
            book.Raise("Greydwarf_Frozen", KnownLevel.Known);
            book.FoundOut("Greydwarf_Frozen", killed);
            var pairs = book.PairsVersion;
            Assert.True(book.TakeBack("Greydwarf_Frozen", killed));
            Assert.False(book.Proved("Greydwarf_Frozen", killed));
            Assert.Equal(0, book.Proofs);
            Assert.True(book.PairsVersion > pairs);
        }
    }
}
