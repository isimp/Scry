using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class RootWordsTests
    {
        // An ancient root's page tells how much sap it holds, how fast it refills, which line
        // the game shows on it at how much sap, and the extractor's page what Extracting slowly
        // means; under the strict rules the amounts wait for the root to be tapped.

        // The game's ancient root: 50 at most, a line for above 40 and one for 5 or less, one back every 400 s.
        private const float Most = 50f;
        private const float High = 40f;
        private const float Empty = 5f;
        private const float PerSecond = 0.0025f;

        [Fact]
        public void ARootTellsHowMuchSapItHoldsAndHowFastItRefills()
        {
            Assert.Equal("up to 50, full when first found", RootWords.Holds(Most));
            Assert.Equal("one every " + Numbers.Duration(400.0) + ", " + Numbers.Duration(20000.0) + " from empty to full", RootWords.Refills(PerSecond, Most));
            Assert.Equal("one every 6.7 min, 5.6 h from empty to full", RootWords.Refills(PerSecond, Most));
            // A root that never refills says so rather than dividing by nothing.
            Assert.Equal("never", RootWords.Refills(0f, Most));
        }

        [Fact]
        public void EachLineTheGameShowsOnARootStandsForAnAmountOfSap()
        {
            // GetHoverText: above the high mark the first line, above the empty mark the second, else the third.
            Assert.Equal(new[] { "above 40", "above 5, up to 40", "5 or less" }, RootWords.Bands(High, Empty));
            Assert.Equal(new[] { "It reads", "Sap left" }, RootWords.Columns);
        }

        [Fact]
        public void ExtractingSlowlyIsToldAsTheRootRunningLowNotTheExtractorSlowing()
        {
            // Said without the word "root", which is the name of an item the strict rules hide until met.
            Assert.Equal("Extracting slowly shows once what it stands on has 5 or less left, each sap then waiting the 6.7 min that takes to gain one back",
                RootWords.Slows("Extracting slowly", Empty, PerSecond));
            Assert.Null(RootWords.Slows("Extracting slowly", Empty, 0f));
        }

        [Fact]
        public void TheSapBoxStandsOnARootsPageAndTheNoteUnderTheExtractorsNeeds()
        {
            var root = FactLayout.Plan(Kind.Resource, new[] { ("Holds", "root"), ("Refills", "root"), ("Tapped by", "root") }, new[] { (RootWords.LooksTitle, "root") }, new FactBlock[0]);
            var sap = root.Single(t => t.Heading == "Sap");
            Assert.Equal("sap", sap.Id);
            Assert.Equal(new[] { 0, 1, 2 }, sap.Bits.Where(b => b.Pair >= 0).Select(b => b.Pair));
            Assert.Contains(sap.Bits, b => b.Row == 0);

            var tap = FactLayout.Plan(Kind.Piece, new[] { ("Makes", "station"), ("Needs", "station"), (RootWords.SlowLabel, "station") }, new (string, string)[0], new FactBlock[0]);
            var needs = tap.SelectMany(t => t.Bits).Single(b => b.Pair == 1);
            Assert.Equal(new[] { 2 }, needs.Notes);
        }

        [Fact]
        public void TappingARootShowsWhatItHoldsUnderTheStrictRules()
        {
            var root = new Entry { Kind = Kind.Resource, Name = "YggdrasilRoot" };
            var catchUp = new CompendiumCatchUp(new EntryCatalog(new[] { root }));
            var book = new CompendiumBook();
            var tapped = EntryKeys.Did(Deed.Tapped);

            Assert.False(book.Proved(root.Key, tapped));
            Assert.True(catchUp.Tapped(root.Key, book));
            Assert.True(book.Proved(root.Key, tapped));
            // Known already by another way, a tap still finds out its sap.
            var seenFirst = new CompendiumBook();
            seenFirst.Raise(root.Key, KnownLevel.Known, way: CameBy.Used);
            Assert.True(catchUp.Tapped(root.Key, seenFirst));
            Assert.True(seenFirst.Proved(root.Key, tapped));

            // What the page still keeps back says how to find it out, and the news names it.
            Assert.Contains(("Tap one", "shows how much sap it holds and how fast it refills"),
                CompendiumWords.StillToFindOut(new[] { tapped }, new string[0], 0, 0, new Tally[0]));
            Assert.Equal("how much sap YggdrasilRoot holds", JournalWords.FoundOutOf(tapped, "YggdrasilRoot"));
        }
    }
}
