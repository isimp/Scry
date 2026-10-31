using Xunit;

namespace Scry.Tests
{
    public class FishCaughtTests
    {
        // Under the strict rules a fish's biting, fight and what a catch brings show once the
        // character caught one. A catch has always paired the fish with the bait it took, so a book
        // kept before catches were noted as such is caught up by those pairs as it is read: a fish
        // paired so with a bait it bites was caught. A tame creature eating a fish pairs them the
        // same way, which is no catch.

        private static readonly string Caught = EntryKeys.Did(Deed.Caught);

        private static bool BitesOn(string fish, string bait) => fish.StartsWith("Fish") && !fish.Contains("Bait") && bait.Contains("Bait");

        private static CompendiumBook ReadIn(CompendiumBook book)
        {
            new CompendiumCatchUp(new EntryCatalog(new Entry[0]), bitesOn: BitesOn).Fill(new CompendiumRecords(), book, firstLoad: true);
            return book;
        }

        [Fact]
        public void AFishCaughtIsProvedCaught()
        {
            var book = new CompendiumBook();
            Assert.False(book.Proved("Fish1", Caught));
            book.Prove("Fish1", Caught);
            Assert.True(book.Proved("Fish1", Caught));
        }

        [Fact]
        public void AFishPairedWithABaitItBitesCountsAsCaughtOnceTheBookIsRead()
        {
            var book = new CompendiumBook();
            book.SawWith("Fish2", "FishingBait", Tie.Eats);
            // A mod's bait may come first by its name; the pair is the same.
            book.SawWith("Fish6", "AnglersBait", Tie.Eats);
            // Paired another way, seen together with no way told, or eaten by a tame creature, it was not caught.
            book.SawWith("Fish3", "FishingBaitOcean", Tie.Drops);
            book.Witness("Fish4", "FishingBait");
            book.SawWith("Wolf", "Fish5", Tie.Eats);
            ReadIn(book);
            Assert.True(book.Proved("Fish2", Caught));
            Assert.True(book.Proved("Fish6", Caught));
            Assert.False(book.Proved("Fish3", Caught));
            Assert.False(book.Proved("Fish4", Caught));
            Assert.False(book.Proved("Fish5", Caught));
            // Neither the bait nor the creature that ate was caught.
            Assert.False(book.Proved("FishingBait", Caught));
            Assert.False(book.Proved("Wolf", Caught));
            // Only the catch is proved by it, nothing else.
            Assert.False(book.Proved("Fish2", EntryKeys.Did(Deed.Killed)));
        }

        [Fact]
        public void AFishEatenByATameCreatureIsNoCatch()
        {
            var book = new CompendiumBook();
            book.SawWith("Wolf", "Fish1", Tie.Eats);
            Assert.False(book.Proved("Fish1", Caught));
        }

        [Fact]
        public void OnlyAnItemPageWaitsForACatch()
        {
            Assert.True(StrictDeeds.Fits(Deed.Caught, Kind.Item));
            Assert.False(StrictDeeds.Fits(Deed.Caught, Kind.Creature));
        }
    }
}
