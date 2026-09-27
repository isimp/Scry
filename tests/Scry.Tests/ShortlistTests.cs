using Xunit;

namespace Scry.Tests
{
    public class ShortlistTests
    {
        [Fact]
        public void AShortListShowsEverything()
        {
            Assert.Equal(12, Shortlist.Shown(12, 18, open: false));
            Assert.Equal(0, Shortlist.Hidden(12, 18, open: false));
        }

        [Fact]
        public void ALongListShowsItsFirstPartAndTellsHowManyMore()
        {
            // Wood builds over a hundred pieces.
            Assert.Equal(18, Shortlist.Shown(130, 18, open: false));
            Assert.Equal(112, Shortlist.Hidden(130, 18, open: false));
        }

        [Fact]
        public void ALongListOpenedShowsEverything()
        {
            Assert.Equal(130, Shortlist.Shown(130, 18, open: true));
            Assert.Equal(0, Shortlist.Hidden(130, 18, open: true));
        }

        [Fact]
        public void AListIsNeverShortenedToHideOnlyOneOrTwo()
        {
            // "1 more" takes the room of the one it hides.
            Assert.Equal(20, Shortlist.Shown(20, 18, open: false));
            Assert.Equal(18, Shortlist.Shown(21, 18, open: false));
            Assert.Equal(3, Shortlist.Hidden(21, 18, open: false));
            Assert.Equal(6, Shortlist.Shown(6, 4, open: false));
            Assert.Equal(4, Shortlist.Shown(7, 4, open: false));
        }

        [Fact]
        public void OnlyALongListCanBeOpenedOrFolded()
        {
            Assert.False(Shortlist.Long(20, 18));
            Assert.True(Shortlist.Long(21, 18));
        }
    }
}
