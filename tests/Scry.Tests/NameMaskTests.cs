using Xunit;

namespace Scry.Tests
{
    public class NameMaskTests
    {
        // Under the strict rules a known page's text keeps back the names of what the character has
        // not seen: each whole name reads ???, the longest first, and a name also borne by
        // something it has seen stays.

        private static readonly NameMask Mask = new NameMask(
            new[] { "Fader", "Black metal scrap", "Black metal", "scrap", "Troll", "Ox", "Wolf" },
            new[] { "Wolf" });

        [Fact]
        public void AWholeNameNotSeenReadsQuestionMarks()
        {
            Assert.Equal("Dropped by ??? in the Ashlands.", Mask.Apply("Dropped by Fader in the Ashlands."));
            Assert.Equal("??? and ???", Mask.Apply("Troll and Fader"));
        }

        [Fact]
        public void TheLongestNameIsTakenFirst()
        {
            Assert.Equal("Smelt ??? into bars.", Mask.Apply("Smelt Black metal scrap into bars."));
            Assert.Equal("??? axe", Mask.Apply("Black metal axe"));
            Assert.Equal("Melt ??? down", Mask.Apply("Melt scrap down"));
        }

        [Fact]
        public void OnlyWholeNamesAreTakenNotPartsOfWords()
        {
            Assert.Equal("??? and Fadermoon", Mask.Apply("Trolls and Fadermoon"));
            Assert.Equal("an Oxford", Mask.Apply("an Oxford"));
        }

        [Fact]
        public void ANameIsTakenInAnyCaseAndAsAPlainPlural()
        {
            // A game's text names things in its own case, and many of a thing with an s.
            Assert.Equal("a ??? hide", Mask.Apply("a troll hide"));
            Assert.Equal("Beware the ??? of the swamp", Mask.Apply("Beware the TROLLS of the swamp"));
            Assert.Equal("??? and ???", Mask.Apply("black metal scraps and Scrap"));
            // A name also seen stays whatever its case.
            Assert.Equal("a wolf and wolfs", Mask.Apply("a wolf and wolfs"));
        }

        [Fact]
        public void ShortNamesAndNamesAlsoSeenStay()
        {
            // "Ox" is too short to tell from a word; a wolf was seen.
            Assert.Equal("Ox and Wolf", Mask.Apply("Ox and Wolf"));
            Assert.Equal("", Mask.Apply(""));
            Assert.Null(Mask.Apply(null));
        }
    }
}
