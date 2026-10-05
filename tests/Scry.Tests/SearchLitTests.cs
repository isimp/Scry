using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class SearchLitTests
    {
        // In the list, the part of each name a typed word matched is lit, so it shows why a row
        // is there: each word where the search finds it first, parts that touch taken together;
        // a name in quotes lights the whole name it matches.

        private static List<(int, int)> Lit(string name, string[] words, string[] names = null)
        {
            var into = new List<(int Start, int Length)>();
            SearchLit.Spans(name, words, names ?? new string[0], into);
            return into.ConvertAll(s => (s.Start, s.Length));
        }

        [Fact]
        public void EachWordLightsWhereTheSearchFindsItFirst()
        {
            Assert.Equal(new[] { (6, 3) }, Lit("Troll hat", new[] { "hat" }));
            Assert.Equal(new[] { (0, 5), (6, 3) }, Lit("Troll leather helmet", new[] { "LEA", "troll" }));
            Assert.Equal(new[] { (4, 5) }, Lit("vfx_troll_troll", new[] { "troll" }));
            Assert.Empty(Lit("Troll", new[] { "boar" }));
            Assert.Empty(Lit("Troll", new[] { "" }));
            Assert.Empty(Lit("", new[] { "troll" }));
        }

        [Fact]
        public void PartsThatTouchOrOverlapAreLitAsOne()
        {
            Assert.Equal(new[] { (0, 6) }, Lit("Greydwarf", new[] { "eydw", "grey" }));
            Assert.Equal(new[] { (0, 9) }, Lit("Greydwarf", new[] { "grey", "dwarf" }));
            Assert.Equal(new[] { (0, 4), (5, 4) }, Lit("Greydwarf", new[] { "grey", "warf" }));
            // A part inside another adds nothing to it.
            Assert.Equal(new[] { (0, 9) }, Lit("Greydwarf", new[] { "greydwarf", "eyd" }));
        }

        [Fact]
        public void OneListServesEveryRow()
        {
            var into = new List<(int Start, int Length)>();
            SearchLit.Spans("Troll", new[] { "troll" }, new string[0], into);
            SearchLit.Spans("Boar", new[] { "troll" }, new string[0], into);
            Assert.Empty(into);
        }

        [Fact]
        public void ANameInQuotesLightsTheWholeNameItMatches()
        {
            Assert.Equal(new[] { (0, 5) }, Lit("Troll", new string[0], new[] { "troll" }));
            Assert.Empty(Lit("Troll hat", new string[0], new[] { "troll" }));
        }
    }
}
