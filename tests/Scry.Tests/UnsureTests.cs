using Xunit;

namespace Scry.Tests
{
    public class UnsureTests
    {
        // Scry is sure of what it reads from the game's data and of what it works out by the
        // game's own rules. Everything else it tells is its best knowledge: which mod added a
        // thing when only clues say so, what mods may change in code it cannot read, what was
        // seen in play, and what it found nothing of. Such a line is marked, and says why.

        [Fact]
        public void AnUnsureLineIsMarked()
        {
            Assert.Equal("~ Mods and its drops", UnsureWords.Marked("Mods and its drops"));
            Assert.Equal("~ ", UnsureWords.Mark);
        }

        [Fact]
        public void AModNamedByItsOwnWordIsSureAndByCluesIsNot()
        {
            Assert.True(UnsureWords.IsSureClue("Jotunn's registry"));
            Assert.True(UnsureWords.IsSureClue(null));
            Assert.True(UnsureWords.IsSureClue(""));
            Assert.False(UnsureWords.IsSureClue("its scripts"));
            Assert.False(UnsureWords.IsSureClue("a bundle holding it by name"));
            Assert.False(UnsureWords.IsSureClue("the bundles holding what it uses"));
        }

        [Fact]
        public void EachClueSaysHowTheModWasFound()
        {
            Assert.Equal("Scry matched it to this mod by the scripts it carries, which come from the mod's own assembly; the mod does not say so itself", UnsureWords.ModClue("its scripts"));
            Assert.Equal("Scry matched it to this mod by an asset bundle the mod ships that holds something of its name; the mod does not say so itself", UnsureWords.ModClue("a bundle holding it by name"));
            Assert.Equal("Scry matched it to this mod by its sounds or icons, found in asset bundles the mod ships; the mod does not say so itself", UnsureWords.ModClue("the bundles holding what it uses"));
            Assert.Equal("Scry matched it to this mod by clues; the mod does not say so itself", UnsureWords.ModClue("something new"));
        }
    }
}
