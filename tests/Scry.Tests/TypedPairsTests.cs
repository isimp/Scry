using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// A pair carries the ways the two were seen together: a drop seen to fall is no proof the
    /// creature was seen beside the item in a chest, and the strict rules ask by the way where a
    /// row has one. A pair seen with no way named, or kept before ways were, stands for any.
    /// </summary>
    public class TypedPairsTests
    {
        [Fact]
        public void APairSeenOneWayAnswersForThatWayAlone()
        {
            var book = new CompendiumBook();
            Assert.True(book.SawWith("Troll", "TrollHide", Tie.Drops));
            Assert.True(book.Witnessed("Troll", "TrollHide"));
            Assert.True(book.Witnessed("TrollHide", "Troll", Tie.Drops));
            Assert.False(book.Witnessed("Troll", "TrollHide", Tie.Holds));
            // Seen another way too, it answers for both; seen the same way again, nothing is new.
            Assert.True(book.SawWith("Troll", "TrollHide", Tie.Holds));
            Assert.False(book.SawWith("Troll", "TrollHide", Tie.Holds));
            Assert.True(book.Witnessed("Troll", "TrollHide", Tie.Holds));
            Assert.Equal(1, book.Pairs);
        }

        [Fact]
        public void APairSeenWithNoWayNamedStandsForAnyWay()
        {
            var book = new CompendiumBook();
            Assert.True(book.Witness("Troll", "Meadows"));
            Assert.True(book.Witnessed("Troll", "Meadows", Tie.Where));
            Assert.True(book.Witnessed("Troll", "Meadows", Tie.Drops));
            // A way named after stands beside it, taking nothing away.
            Assert.False(book.SawWith("Troll", "Meadows", Tie.Where));
            Assert.True(book.Witnessed("Troll", "Meadows", Tie.Drops));
        }

        [Fact]
        public void TheWaysAreKeptWithTheBookAndAPairKeptBeforeWaysWereStandsForAny()
        {
            var book = new CompendiumBook();
            book.SawWith("Troll", "TrollHide", Tie.Drops);
            book.SawWith("Troll", "TrollHide", Tie.Gives);
            book.Witness("Troll", "Meadows");
            var text = book.Save();
            Assert.Contains("saw\tTroll\tTrollHide\tdrops,gives\n", text);
            Assert.Contains("saw\tMeadows\tTroll\n", text);
            var read = CompendiumBook.Load(text);
            Assert.True(read.Witnessed("Troll", "TrollHide", Tie.Gives));
            Assert.False(read.Witnessed("Troll", "TrollHide", Tie.Holds));
            Assert.True(read.Witnessed("Troll", "Meadows", Tie.Holds));
            // A line of an older book names no way; a way this Scry does not know reads as any.
            var older = CompendiumBook.Load("format\t2\nsaw\tTroll\tTrollHide\n");
            Assert.True(older.Witnessed("Troll", "TrollHide", Tie.Holds));
            Assert.True(CompendiumBook.Load("format\t3\nsaw\tTroll\tTrollHide\tsomeday\n").Witnessed("Troll", "TrollHide", Tie.Holds));
        }
    }
}
