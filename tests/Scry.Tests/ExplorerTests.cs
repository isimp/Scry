using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class ExplorerTests
    {
        private static Explorer Open()
        {
            return new Explorer(Game(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
        }

        private static Entry Named(Explorer explorer, string name) => explorer.Catalog.First(e => e.Name == name);

        [Fact]
        public void OpeningShowsEverythingWithNothingSelected()
        {
            var explorer = Open();

            Assert.Equal(Game().Count, explorer.Results.Count);
            Assert.Null(explorer.Selected);
        }

        [Fact]
        public void KindChipsCountWhatMatchesTheSearch()
        {
            var explorer = Open();
            explorer.Text = "troll";

            Assert.Equal(3, explorer.CountOf(Kind.Creature));
            Assert.Equal(1, explorer.CountOf(Kind.Item));
            Assert.Equal(0, explorer.CountOf(Kind.StatusEffect));
            Assert.Equal(explorer.Results.Count, explorer.CountAll);
        }

        [Fact]
        public void PickingAKindKeepsTheOtherChipsCounting()
        {
            var explorer = Open();
            explorer.Text = "troll";
            explorer.KindFilter = Kind.Creature;

            Assert.Equal(3, explorer.Results.Count);
            Assert.Equal(1, explorer.CountOf(Kind.Item));
            Assert.True(explorer.CountAll > explorer.Results.Count);
        }

        [Fact]
        public void TheSelectionStaysWhileNarrowingTheSearchIfItStillMatches()
        {
            var explorer = Open();
            explorer.Select(Named(explorer, "MountainTroll"));

            explorer.Text = "mountain";

            Assert.Equal("MountainTroll", explorer.Selected?.Name);
            Assert.Equal(0, explorer.SelectedIndex);
        }

        [Fact]
        public void ASelectionTheSearchNoLongerListsIsLetGo()
        {
            var explorer = Open();
            explorer.Select(Named(explorer, "Bow"));

            explorer.Text = "troll";

            Assert.Null(explorer.Selected);
            Assert.Equal(-1, explorer.SelectedIndex);
        }

        [Fact]
        public void ArrowingDownWithNothingSelectedStartsAtTheTop()
        {
            var explorer = Open();
            explorer.Text = "troll";

            explorer.Move(1);

            Assert.Equal("Troll", explorer.Selected?.Name);
        }

        [Fact]
        public void ArrowingStopsAtEitherEndOfTheList()
        {
            var explorer = Open();
            explorer.Text = "troll";

            explorer.Move(100);
            Assert.Same(explorer.Results.Last(), explorer.Selected);

            explorer.Move(-100);
            Assert.Same(explorer.Results.First(), explorer.Selected);
        }

        [Fact]
        public void ArrowingInAnEmptyListSelectsNothing()
        {
            var explorer = Open();
            explorer.Text = "dragonfruit";

            explorer.Move(1);

            Assert.Null(explorer.Selected);
        }

        [Fact]
        public void ChoosingAnotherPrefabStartsItAsItIs()
        {
            var explorer = Open();
            explorer.Select(Named(explorer, "Troll"));
            explorer.Modifiers.Scale = 4f;

            explorer.Select(Named(explorer, "MountainTroll"));

            Assert.Equal(1f, explorer.Modifiers.Scale);
        }

        [Fact]
        public void EachNewSelectionIsNoticeable()
        {
            var explorer = Open();
            var before = explorer.SelectionVersion;

            explorer.Select(Named(explorer, "Troll"));
            var afterFirst = explorer.SelectionVersion;
            explorer.Select(Named(explorer, "Troll"));

            Assert.NotEqual(before, afterFirst);
            Assert.Equal(afterFirst, explorer.SelectionVersion);
        }

        [Fact]
        public void StarringAPrefabPutsItInTheFavourites()
        {
            var explorer = Open();
            explorer.ToggleFavourite(Named(explorer, "Bow"));

            explorer.FavouritesOnly = true;

            Assert.Equal(new[] { "Bow" }, explorer.Results.Select(e => e.Name));
        }

        [Fact]
        public void UnstarringTheLastFavouriteEmptiesTheFavouritesList()
        {
            var explorer = Open();
            explorer.FavouritesOnly = true;
            explorer.ToggleFavourite(Named(explorer, "Bow"));
            Assert.Single(explorer.Results);

            explorer.ToggleFavourite(Named(explorer, "Bow"));

            Assert.Empty(explorer.Results);
        }
    }
}
