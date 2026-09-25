using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class RecentTests
    {
        private static Explorer Open() => new Explorer(Game(), new Favourites(Path.Combine(TempDir(), "f.txt")));
        private static Entry Named(Explorer explorer, string name) => explorer.Catalog.First(e => e.Name == name);

        [Fact]
        public void RecentListsWhatWasLookedAtNewestFirst()
        {
            var explorer = Open();
            explorer.Select(Named(explorer, "Troll"));
            explorer.Select(Named(explorer, "Bow"));
            explorer.Select(Named(explorer, "wood_wall"));

            explorer.RecentOnly = true;

            Assert.Equal(new[] { "wood_wall", "Bow", "Troll" }, explorer.Results.Select(e => e.Name));
        }

        [Fact]
        public void LookingAtSomethingAgainMovesItToTheTop()
        {
            var explorer = Open();
            explorer.Select(Named(explorer, "Troll"));
            explorer.Select(Named(explorer, "Bow"));
            explorer.Select(Named(explorer, "Troll"));

            explorer.RecentOnly = true;

            Assert.Equal(new[] { "Troll", "Bow" }, explorer.Results.Select(e => e.Name));
        }

        [Fact]
        public void RecentKeepsOnlyTheLastThirty()
        {
            var catalog = Enumerable.Range(0, 40).Select(i => E("Thing" + i, Kind.Other)).ToList();
            var explorer = new Explorer(catalog, new Favourites(Path.Combine(TempDir(), "f.txt")));
            foreach (var entry in catalog) explorer.Select(entry);

            Assert.Equal(Explorer.RecentLimit, explorer.RecentKeys.Count);
            Assert.Equal("Thing39", explorer.RecentKeys[0]);
        }

        [Fact]
        public void TheKindChipsCountOnlyWhatIsRecentWhileRecentIsOn()
        {
            var explorer = Open();
            explorer.Select(Named(explorer, "Troll"));
            explorer.Select(Named(explorer, "Bow"));

            explorer.RecentOnly = true;

            Assert.Equal(2, explorer.CountAll);
            Assert.Equal(1, explorer.CountOf(Kind.Creature));
        }

        [Fact]
        public void RecentStillAnswersToTheSearch()
        {
            var explorer = Open();
            explorer.Select(Named(explorer, "Troll"));
            explorer.Select(Named(explorer, "Bow"));
            explorer.RecentOnly = true;

            explorer.Text = "troll";

            Assert.Equal(new[] { "Troll" }, explorer.Results.Select(e => e.Name));
        }
    }
}
