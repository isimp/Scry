using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class SearchSlipTests
    {
        // When a search finds nothing at all, a typed word that is one slip away from a word of
        // the catalog's names (a letter wrong, missing, extra, or two swapped) is read as that
        // word, and the list says so: "No match for greydwraf; showing greydwarf".

        [Fact]
        public void OneSlipIsALetterWrongMissingExtraOrTwoSwapped()
        {
            Assert.True(SearchSlip.OneSlip("greydwarf", "greydwarf"));
            Assert.True(SearchSlip.OneSlip("greydwerf", "greydwarf"));
            Assert.True(SearchSlip.OneSlip("greydwrf", "greydwarf"));
            Assert.True(SearchSlip.OneSlip("greyddwarf", "greydwarf"));
            Assert.True(SearchSlip.OneSlip("greydwraf", "greydwarf"));
            Assert.False(SearchSlip.OneSlip("greydwrfa", "greydwarf"));
            Assert.False(SearchSlip.OneSlip("grydwrf", "greydwarf"));
            Assert.False(SearchSlip.OneSlip("draugr", "greydwarf"));
        }

        [Fact]
        public void TheNamesWordsAreSplitAtSpacesMarksAndCapitals()
        {
            var words = SearchSlip.Words(new[] { E("Greydwarf_Elite", Kind.Creature, "Greydwarf brute"), E("MountainTroll", Kind.Creature, "Mountain troll") });
            Assert.Contains("greydwarf", words);
            Assert.Contains("brute", words);
            Assert.Contains("elite", words);
            Assert.Contains("mountain", words);
            Assert.Contains("troll", words);
            // A word only the prefab's capitals part.
            Assert.Contains("hat", SearchSlip.Words(new[] { E("TrollHat", Kind.Item, "Troll cap") }));
        }

        [Fact]
        public void AWordIsMendedOnlyByOneSlipAndOnlyWhenLongEnough()
        {
            var words = new HashSet<string> { "greydwarf", "troll", "draugr" };
            Assert.Equal("greydwarf", SearchSlip.Nearest("greydwraf", words));
            Assert.Equal("troll", SearchSlip.Nearest("trlol", words));
            // The start of a word being typed, one slip off, reads as that word.
            Assert.Equal("greydwarf", SearchSlip.Nearest("greydwe", words));
            Assert.Null(SearchSlip.Nearest("trol", new HashSet<string> { "boar" }));
            // Three letters or fewer are too few to tell a slip from another word.
            Assert.Null(SearchSlip.Nearest("trl", words));
            // A whole word one slip away comes before one whose start is.
            Assert.Equal("troll", SearchSlip.Nearest("trole", new HashSet<string> { "trolley", "troll" }));
            // Nothing to mend, nothing given back.
            var none = new List<(string, string)>();
            Assert.Null(SearchSlip.Mend("zzzqqq", Catalog(), words, none));
            Assert.Empty(none);
        }

        private static List<Entry> Catalog() => new List<Entry>
        {
            E("Greydwarf", Kind.Creature, "Greydwarf"),
            E("Greydwarf_Elite", Kind.Creature, "Greydwarf brute"),
            E("Troll", Kind.Creature, "Troll"),
            E("TrollHat", Kind.Item, "Troll hat"),
        };

        [Fact]
        public void ASearchFindingNothingShowsWhatItsSlipsMeantAndSaysSo()
        {
            var explorer = new Explorer(Catalog(), new Favourites(System.IO.Path.Combine(TempDir(), "f.txt")));
            explorer.Text = "greydwraf";
            Assert.Equal(new[] { "Greydwarf", "Greydwarf_Elite" }, explorer.Results.Select(e => e.Name));
            Assert.Equal(new[] { ("greydwraf", "greydwarf") }, explorer.Mended);
            Assert.Equal("greydwarf", explorer.MendedText);
            Assert.Equal("No match for greydwraf; showing greydwarf", ListWords.Mended(explorer.Mended));

            // Only the word no name holds is mended; the rest of the search stays.
            explorer.Text = "trlol hat";
            Assert.Equal(new[] { "TrollHat" }, explorer.Results.Select(e => e.Name));
            Assert.Equal(new[] { ("trlol", "troll") }, explorer.Mended);

            // A word some name holds is no slip, even where another word is one slip from it.
            explorer.Text = "grydwarf brute";
            Assert.Equal(new[] { "Greydwarf_Elite" }, explorer.Results.Select(e => e.Name));
            Assert.Equal(new[] { ("grydwarf", "greydwarf") }, explorer.Mended);

            // Where the mended search finds nothing either, nothing is said to be mended.
            explorer.Text = "greydwraf has:nothinghere";
            Assert.Empty(explorer.Results);
            Assert.Empty(explorer.Mended);

            // A tab with none of what the mended search finds shows every kind's, as for any search.
            explorer.KindFilter = Kind.Item;
            explorer.Text = "greydwraf";
            Assert.True(explorer.ShowingEveryKind);
            Assert.Equal(new[] { "Greydwarf", "Greydwarf_Elite" }, explorer.Results.Select(e => e.Name));
            explorer.KindFilter = null;

            // A search that finds something is never mended.
            explorer.Text = "troll";
            Assert.Empty(explorer.Mended);
            Assert.Null(explorer.MendedText);
            explorer.Text = "zzzqqq";
            Assert.Empty(explorer.Results);
            Assert.Empty(explorer.Mended);
        }

        [Fact]
        public void SeveralMendedWordsAreToldTogether()
        {
            Assert.Equal("No match for trlol, haat; showing troll, hat", ListWords.Mended(new[] { ("trlol", "troll"), ("haat", "hat") }));
        }
    }
}
