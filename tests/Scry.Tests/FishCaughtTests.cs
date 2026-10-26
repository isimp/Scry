using Xunit;

namespace Scry.Tests
{
    public class FishCaughtTests
    {
        // Under the strict rules a fish's biting, fight and what a catch brings show once the
        // character caught one. A catch has always paired the fish with the bait it took, so a
        // fish caught before catches were noted as such counts as caught by that pair.

        private static readonly string Caught = EntryKeys.Did(Deed.Caught);

        [Fact]
        public void AFishCaughtIsProvedCaught()
        {
            var book = new CompendiumBook();
            Assert.False(book.Proved("Fish1", Caught));
            book.Prove("Fish1", Caught);
            Assert.True(book.Proved("Fish1", Caught));
        }

        [Fact]
        public void AFishPairedWithTheBaitItTookCountsAsCaught()
        {
            var book = new CompendiumBook();
            book.SawWith("Fish2", "FishingBait", Tie.Eats);
            Assert.True(book.Proved("Fish2", Caught));
            // Paired another way, or seen together with no way told, it was not caught.
            book.SawWith("Fish3", "FishingBaitOcean", Tie.Drops);
            Assert.False(book.Proved("Fish3", Caught));
            book.Witness("Fish4", "FishingBait");
            Assert.False(book.Proved("Fish4", Caught));
            // A later pairing counts at once.
            book.SawWith("Fish3", "FishingBaitOcean", Tie.Eats);
            Assert.True(book.Proved("Fish3", Caught));
            // Only the catch is proved by it, nothing else.
            Assert.False(book.Proved("Fish2", EntryKeys.Did(Deed.Killed)));
        }

        [Fact]
        public void OnlyAnItemPageWaitsForACatch()
        {
            Assert.True(StrictDeeds.Fits(Deed.Caught, Kind.Item));
            Assert.False(StrictDeeds.Fits(Deed.Caught, Kind.Creature));
        }
    }
}
