using Xunit;

namespace Scry.Tests
{
    public class PendingPageTests
    {
        // Opened before the catalog is read, as a click on a dungeon's mark on Valheim's map does
        // while Scry still reads it, the panel keeps the page it was asked for and the search,
        // and shows them once the catalog is read.

        [Fact]
        public void APageAskedForBeforeTheCatalogIsReadIsShownOnceItIs()
        {
            var pending = new PendingPage();
            // A search typed, then a click on a dungeon's mark: both are kept.
            pending.Ask("troll", null);
            pending.Ask(null, "Crypt2");
            Assert.Equal(("troll", "Crypt2"), pending.Take());
            // Handed over once.
            Assert.Equal(((string)null, (string)null), pending.Take());
            // The other way round, the page kept as the search is asked for.
            pending.Ask(null, "Crypt2");
            pending.Ask("troll", null);
            Assert.Equal(("troll", "Crypt2"), pending.Take());

            // A later click asks for its own page.
            pending.Ask(null, "Crypt2");
            pending.Ask(null, "SunkenCrypt4");
            Assert.Equal("SunkenCrypt4", pending.Take().Entry);
        }

        [Fact]
        public void ThePanelClosedBeforeTheCatalogIsReadLetsGoOfThePageTheSearchKept()
        {
            var pending = new PendingPage();
            pending.Ask("troll", "Crypt2");
            pending.Closed();
            Assert.Equal(("troll", (string)null), pending.Take());
        }
    }
}
