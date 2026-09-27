using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    /// <summary>
    /// What searching has to keep doing while it works out names, keys and terms once instead of
    /// on every keystroke for every entry.
    /// </summary>
    public class SearchSpeedTests
    {
        private static List<string> Find(IReadOnlyList<Entry> catalog, string text) =>
            Search.Run(catalog, new Query { Text = text }, new List<string>()).Select(e => e.Name).ToList();

        [Fact]
        public void CaseDoesNotMatterInNamesBeyondPlainEnglishLetters()
        {
            var catalog = new List<Entry> { E("AxeBronze", Kind.Item, "Bronzeaxt"), E("Club", Kind.Item, "Große Äxte") };

            Assert.Equal(new[] { "Club" }, Find(catalog, "äxte"));
            Assert.Equal(new[] { "Club" }, Find(catalog, "ÄXTE"));
            Assert.Equal(new[] { "Club" }, Find(catalog, "GROßE"));
        }

        [Fact]
        public void ANameGivenAfterTheEntryWasMadeIsWhatIsSearched()
        {
            // Leftovers are renamed after their owner once the catalog is read.
            var ragdoll = E("Troll_ragdoll", Kind.Other);
            var catalog = new List<Entry> { ragdoll, E("Bow", Kind.Item, "Crude bow") };
            Assert.Empty(Find(catalog, "remains"));

            ragdoll.DisplayName = "Troll remains";
            Assert.Equal(new[] { "Troll_ragdoll" }, Find(catalog, "remains"));

            ragdoll.Name = "Troll_body";
            Assert.Equal(new[] { "Troll_body" }, Find(catalog, "body"));
            Assert.Empty(Find(catalog, "ragdoll"));
        }

        [Fact]
        public void AnEntrysKeyFollowsItsKindAndName()
        {
            var entry = E("Rested", Kind.Other);
            Assert.Equal("Rested", entry.Key);

            entry.Kind = Kind.StatusEffect;
            Assert.Equal("se:Rested", entry.Key);

            entry.Name = "Resting";
            Assert.Equal("se:Resting", entry.Key);
        }

        [Fact]
        public void TheKindChipsAndTheListAgreeForEveryKindAndSearch()
        {
            var explorer = new Explorer(Game(), new Favourites(Path.Combine(TempDir(), "f.txt")));

            foreach (var text in new[] { "", "troll", "o", "-troll", "kind:creature", "troll -kind:creature", "has:", "nothing-matches" })
            {
                explorer.KindFilter = null;
                explorer.Text = text;
                var all = explorer.CountAll;
                var sum = 0;
                foreach (Kind kind in Enum.GetValues(typeof(Kind)))
                {
                    explorer.KindFilter = kind;
                    Assert.Equal(explorer.Results.Count, explorer.CountOf(kind));
                    sum += explorer.CountOf(kind);
                }
                explorer.KindFilter = null;
                Assert.Equal(explorer.Results.Count, all);
                Assert.Equal(all, sum);
            }
        }

        [Fact]
        public void TheSameSearchTwiceListsTheSameInTheSameOrder()
        {
            var catalog = Game();

            Assert.Equal(Find(catalog, "troll"), Find(catalog, "troll"));
            Assert.Equal(Find(catalog, "t -statue kind:creature"), Find(catalog, "t -statue kind:creature"));
        }
    }
}
