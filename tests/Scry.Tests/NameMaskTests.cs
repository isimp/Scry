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
        public void OnlyANameTheGameGivesAThingOfTheWorldIsKeptBack()
        {
            // A name Scry made of a prefab's words (the Swamp's "SwampWell1" goes by "Well") is
            // no name a text holds: kept back, it would hide a plain word ("feel so well").
            var well = new Entry { Name = "SwampWell1", DisplayName = "Well", Kind = Kind.Location };
            Assert.False(NameMask.Keeps(well, byWords: true));
            Assert.True(NameMask.Keeps(new Entry { Name = "Crypt2", DisplayName = "Burial Chambers", Kind = Kind.Location }, byWords: false));
            Assert.True(NameMask.Keeps(new Entry { Name = "Troll", DisplayName = "Troll", Kind = Kind.Creature }, byWords: false));
            // Nothing the game names, nor the parts the game is built of.
            Assert.False(NameMask.Keeps(new Entry { Name = "rock_mistlands1", Kind = Kind.Resource }, byWords: false));
            Assert.False(NameMask.Keeps(new Entry { Name = "vfx_fire", DisplayName = "Fire", Kind = Kind.Effect }, byWords: false));
            // A status effect's name is mostly a word of what it does to you ("Fire", "Wet", "Cold"): "fires" stays.
            Assert.False(NameMask.Keeps(new Entry { Name = "Fire", DisplayName = "Fire", Kind = Kind.StatusEffect }, byWords: false));
            Assert.False(NameMask.Keeps(null, byWords: false));
        }

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
