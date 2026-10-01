using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class NamesInTextTests
    {
        // The self-test looks through every entry's details for the names of other entries told
        // as text, where a chip could go to them: whole words, the first capitalised as the game
        // names things, the longest name at a place taken.

        private static readonly HashSet<string> Names = new HashSet<string>
        {
            "Meadows", "Black Forest", "Forest", "Wood", "Troll", "Deer hide", "Wood Stone", "Bonemass", "Jack-o-turnip", "fire",
        };

        [Fact]
        public void ItFindsTheNamesALineHolds()
        {
            Assert.Equal(new[] { "Meadows", "Black Forest" }, NamesInText.Find("grows in Meadows and Black Forest", Names));
            Assert.Equal(new[] { "Bonemass" }, NamesInText.Find("while fighting Bonemass, within 100 m", Names));
        }

        [Fact]
        public void TheLongestNameAtAPlaceIsTaken()
        {
            Assert.Equal(new[] { "Black Forest" }, NamesInText.Find("Black Forest", Names));
        }

        [Fact]
        public void OnlyWholeWordsCount()
        {
            Assert.Empty(NamesInText.Find("Woodland and Trolls", Names));
        }

        [Fact]
        public void ANameStartsCapitalisedAsTheGameNamesThings()
        {
            Assert.Empty(NamesInText.Find("made of wood, in the meadows", Names));
            // A name some mod spells in lower case is not looked for in prose.
            Assert.Empty(NamesInText.Find("keeps away from fire", Names));
            // Its later words may be lower case.
            Assert.Equal(new[] { "Deer hide" }, NamesInText.Find("Deer hide, 2", Names));
        }

        [Fact]
        public void PunctuationEndsAName()
        {
            Assert.Equal(new[] { "Wood" }, NamesInText.Find("Wood, Stone", Names));
            Assert.Equal(new[] { "Wood Stone" }, NamesInText.Find("Wood Stone", Names));
        }

        [Fact]
        public void AHyphenKeepsAWordWhole()
        {
            Assert.Equal(new[] { "Jack-o-turnip" }, NamesInText.Find("a carved Jack-o-turnip", Names));
        }

        [Fact]
        public void APossessiveNamesItsOwner()
        {
            Assert.Equal(new[] { "Troll" }, NamesInText.Find("a Troll's head", Names));
            Assert.Equal(new[] { "Troll" }, NamesInText.Find("a Troll\u2019s head", Names));
        }

        [Fact]
        public void NoTextHoldsNoName()
        {
            Assert.Empty(NamesInText.Find("", Names));
            Assert.Empty(NamesInText.Find(null, Names));
        }
    }
}
