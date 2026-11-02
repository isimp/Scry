using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class NewsShownTests
    {
        // News of what the character learned: all of it, new pages only (no numbers found out
        // about a page already met, which under Strict come after nearly every fight), or none.

        private static CompendiumView StrictOn(CompendiumBook book)
        {
            var view = new CompendiumView(new EntryCatalog(new List<Entry>()));
            view.Tell(book, CompendiumRule.Strict);
            return view;
        }

        [Fact]
        public void NewPagesOnlyLeavesOutWhatWasFoundOutAboutAPageMetBefore()
        {
            var book = new CompendiumBook();
            book.Raise("Troll", KnownLevel.Known);
            var mark = book.Learned;
            book.FoundOut("Troll", EntryKeys.Did(Deed.Killed));
            book.Raise("Boar", KnownLevel.Known);
            var view = StrictOn(book);
            Assert.Equal(new[] { "Boar", "Troll" }, view.LearnedSince(mark, pagesOnly: false).ToArray());
            Assert.Equal(new[] { "Boar" }, view.LearnedSince(mark, pagesOnly: true).ToArray());
        }

        [Fact]
        public void APageNewlyMetIsToldAsMetNotAsWhatWasFoundOutAfter()
        {
            var book = new CompendiumBook();
            var mark = book.Learned;
            book.Raise("Greyling", KnownLevel.Known);
            book.FoundOut("Greyling", EntryKeys.Did(Deed.Killed));
            var view = StrictOn(book);
            Assert.Equal(NewsHow.FoundOut, view.NewsOf("Greyling")?.How);
            Assert.NotEqual(NewsHow.FoundOut, view.NewsOf("Greyling", metOnly: true)?.How);
            Assert.Equal(new[] { "Greyling" }, view.LearnedSince(mark, pagesOnly: true).ToArray());
        }

        [Fact]
        public void TheNewsStripShowsAsSet()
        {
            Assert.True(NewsChoices.Shows(NewsShown.All));
            Assert.True(NewsChoices.Shows(NewsShown.NewPages));
            Assert.False(NewsChoices.Shows(NewsShown.Off));
            Assert.False(NewsChoices.PagesOnly(NewsShown.All));
            Assert.True(NewsChoices.PagesOnly(NewsShown.NewPages));
        }

        [Fact]
        public void NewsTurnedOffBeforeStaysOff()
        {
            // The setting a single switch was before: off is kept as off, on as all of it.
            Assert.Equal(NewsShown.Off, NewsChoices.From(false));
            Assert.Equal(NewsShown.All, NewsChoices.From(true));
        }
    }
}
