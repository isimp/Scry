using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class KeepTests
    {
        // The self-test searches, selects and jumps through the panel as a player would; afterwards
        // the panel must be as the player left it: search, filters, selection, recent and history.

        private static Explorer Open() => new Explorer(Game(), new Favourites(Path.Combine(TempDir(), "f.txt")));

        private static Explorer Used()
        {
            var explorer = Open();
            explorer.Text = "troll";
            explorer.KindFilter = Kind.Creature;
            explorer.Select(explorer.Results[0]);
            explorer.Jump("Bow");
            explorer.Back();
            return explorer;
        }

        private static void Wander(Explorer explorer)
        {
            explorer.SearchEverything("wood");
            if (explorer.Results.Count > 0) explorer.Select(explorer.Results[0]);
            explorer.Jump("Bow");
            explorer.RecentOnly = true;
            explorer.Origin = OriginFilter.Mods;
            explorer.FavouritesOnly = true;
        }

        [Fact]
        public void PuttingBackWhatWasKeptUndoesEverySearchSelectionAndJump()
        {
            var explorer = Used();
            var recent = explorer.RecentKeys.ToList();
            var kept = explorer.Keep();

            Wander(explorer);
            explorer.Restore(kept);

            Assert.Equal("troll", explorer.Text);
            Assert.Equal(Kind.Creature, explorer.KindFilter);
            Assert.False(explorer.RecentOnly);
            Assert.False(explorer.FavouritesOnly);
            Assert.Equal(OriginFilter.All, explorer.Origin);
            Assert.Equal("Troll", explorer.Selected?.Name);
            Assert.Equal(explorer.Results.ToList().IndexOf(explorer.Selected), explorer.SelectedIndex);
            Assert.Equal(recent, explorer.RecentKeys.ToList());
        }

        [Fact]
        public void TheHistoryIsAsItWasSoBackAndForwardGoWhereTheyWent()
        {
            var explorer = Used();
            var kept = explorer.Keep();

            Wander(explorer);
            explorer.Restore(kept);

            // Before: back had nothing further, forward led to the bow.
            Assert.False(explorer.CanGoBack);
            Assert.True(explorer.Forward());
            Assert.Equal("Bow", explorer.Selected?.Name);
            Assert.True(explorer.Back());
            Assert.Equal("Troll", explorer.Selected?.Name);
        }

        [Fact]
        public void AHistoryWithStepsBothWaysComesBackWhole()
        {
            var explorer = Open();
            explorer.Text = "troll";
            explorer.KindFilter = Kind.Creature;
            explorer.Select(explorer.Results[0]);
            explorer.Jump("Bow");
            explorer.Jump("MountainTroll");
            explorer.Back();
            var kept = explorer.Keep();

            // Steps of its own both ways, which must not stay behind.
            Wander(explorer);
            explorer.Jump("TrollArmorChest");
            explorer.Back();
            explorer.Restore(kept);

            Assert.Equal("Bow", explorer.Selected?.Name);
            Assert.True(explorer.Forward());
            Assert.Equal("MountainTroll", explorer.Selected?.Name);
            Assert.False(explorer.CanGoForward);
            Assert.True(explorer.Back());
            Assert.True(explorer.Back());
            Assert.Equal("Troll", explorer.Selected?.Name);
            Assert.False(explorer.CanGoBack);
        }

        [Fact]
        public void NothingSelectedStaysNothingSelected()
        {
            var explorer = Open();
            var kept = explorer.Keep();

            Wander(explorer);
            explorer.Restore(kept);

            Assert.Null(explorer.Selected);
            Assert.Empty(explorer.RecentKeys);
            Assert.False(explorer.CanGoBack);
            Assert.False(explorer.CanGoForward);
        }
    }
}
