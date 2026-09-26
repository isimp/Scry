using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class HistoryTests
    {
        private static Explorer Open() => new Explorer(Game(), new Favourites(Path.Combine(TempDir(), "f.txt")));

        private static Explorer OnTroll()
        {
            var explorer = Open();
            explorer.Text = "troll";
            explorer.KindFilter = Kind.Creature;
            explorer.Select(explorer.Results[0]);
            return explorer;
        }

        [Fact]
        public void GoingBackAfterAJumpReturnsToWhereYouWere()
        {
            var explorer = OnTroll();
            explorer.Jump("Bow");

            Assert.True(explorer.Back());

            Assert.Equal("Troll", explorer.Selected?.Name);
            Assert.Equal(explorer.Results.ToList().IndexOf(explorer.Selected), explorer.SelectedIndex);
        }

        [Fact]
        public void GoingBackBringsBackTheSearchAndFiltersTheJumpCleared()
        {
            var explorer = OnTroll();
            explorer.Jump("Bow");

            explorer.Back();

            Assert.Equal("troll", explorer.Text);
            Assert.Equal(Kind.Creature, explorer.KindFilter);
        }

        [Fact]
        public void ForwardGoesAgainWhereBackCameFrom()
        {
            var explorer = OnTroll();
            explorer.Jump("Bow");
            explorer.Back();

            Assert.True(explorer.CanGoForward);
            Assert.True(explorer.Forward());

            Assert.Equal("Bow", explorer.Selected?.Name);
            Assert.False(explorer.CanGoForward);
        }

        [Fact]
        public void SeveralJumpsGoBackOneAtATime()
        {
            var explorer = OnTroll();
            explorer.Jump("Bow");
            explorer.Jump("TrollArmorChest");

            explorer.Back();
            Assert.Equal("Bow", explorer.Selected?.Name);
            explorer.Back();
            Assert.Equal("Troll", explorer.Selected?.Name);
            Assert.False(explorer.CanGoBack);
        }

        [Fact]
        public void AJumpAfterGoingBackForgetsTheWayForward()
        {
            var explorer = OnTroll();
            explorer.Jump("Bow");
            explorer.Back();

            explorer.Jump("TrollArmorChest");

            Assert.False(explorer.CanGoForward);
        }

        [Fact]
        public void BeforeAnyJumpThereIsNothingToGoBackTo()
        {
            var explorer = OnTroll();

            Assert.False(explorer.CanGoBack);
            Assert.False(explorer.Back());
            Assert.False(explorer.Forward());
            Assert.Equal("Troll", explorer.Selected?.Name);
        }

        [Fact]
        public void JumpingToWhatIsAlreadySelectedAddsNoStep()
        {
            var explorer = OnTroll();

            explorer.Jump("Troll");

            Assert.False(explorer.CanGoBack);
        }

        [Fact]
        public void AJumpToNothingAddsNoStep()
        {
            var explorer = OnTroll();

            explorer.Jump("NoSuchThing");

            Assert.False(explorer.CanGoBack);
        }

        [Fact]
        public void OnlyTheLastFiftyStepsAreKept()
        {
            var explorer = OnTroll();
            for (var i = 0; i < 60; i++) explorer.Jump(i % 2 == 0 ? "Bow" : "TrollArmorChest");

            var steps = 0;
            while (explorer.Back()) steps++;

            Assert.Equal(Explorer.HistoryLimit, steps);
        }
    }
}
