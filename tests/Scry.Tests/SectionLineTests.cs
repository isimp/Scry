using Xunit;

namespace Scry.Tests
{
    public class SectionLineTests
    {
        // A long page of details is gone round by a line of its section names under the title,
        // each with its count where its heading has one: a click brings that section's heading
        // to the top, and the section being read is lit.

        [Fact]
        public void TheLineNamesEachSectionWithItsCount()
        {
            Assert.Equal("In the game", PanelWords.SectionLink("IN THE GAME", -1));
            Assert.Equal("Animations 1,234", PanelWords.SectionLink("ANIMATIONS", 1234));
            Assert.Equal("Effects 0", PanelWords.SectionLink("EFFECTS", 0));
            Assert.Equal("Readme", PanelWords.SectionLink("README", -1));
        }

        [Fact]
        public void ALinksTipSaysWhereItGoesAndThatAFoldedSectionOpens()
        {
            Assert.Equal("Go to Animations", PanelWords.SectionLinkTip("ANIMATIONS", false));
            Assert.Equal("Open Animations and go to it", PanelWords.SectionLinkTip("ANIMATIONS", true));
            Assert.Equal("Go to In the game", PanelWords.SectionLinkTip("IN THE GAME", false));
        }

        [Fact]
        public void TheLineShowsOnlyWhereThereIsMoreThanOneSectionToGoTo()
        {
            Assert.False(SectionLine.Shows(0));
            Assert.False(SectionLine.Shows(1));
            Assert.True(SectionLine.Shows(2));
        }

        [Fact]
        public void AClickBringsTheSectionsHeadingToTheTopAsFarAsThePageScrolls()
        {
            // A little gap is kept over the heading; the page scrolls no further than its end, nor above its top.
            Assert.Equal(396f, SectionLine.JumpTo(400f, 4f, 1000f));
            Assert.Equal(1000f, SectionLine.JumpTo(1200f, 4f, 1000f));
            Assert.Equal(0f, SectionLine.JumpTo(2f, 4f, 1000f));
            Assert.Equal(0f, SectionLine.JumpTo(400f, 4f, -50f));
        }

        [Fact]
        public void TheSectionBeingReadIsLit()
        {
            // Headings at 0, 300, 900, 1,000 and 1,500 on a page showing 600 at a time, scrolling to 1,200.
            var headings = new[] { 0f, 300f, 900f, 1000f, 1500f };

            // At the top, the first; once a heading comes within the top third of what shows, its section.
            Assert.Equal(0, SectionLine.InView(headings, 0f, 600f, 1200f, -1, 0f));
            Assert.Equal(0, SectionLine.InView(headings, 99f, 600f, 1200f, -1, 0f));
            Assert.Equal(1, SectionLine.InView(headings, 100f, 600f, 1200f, -1, 0f));
            Assert.Equal(1, SectionLine.InView(headings, 699f, 600f, 1200f, -1, 0f));
            Assert.Equal(2, SectionLine.InView(headings, 700f, 600f, 1200f, -1, 0f));

            // At the end of the page the last, though its heading never reaches the top third.
            Assert.Equal(4, SectionLine.InView(headings, 1200f, 600f, 1200f, -1, 0f));

            // A section clicked stays lit while the page stays where the click put it, short as it is.
            Assert.Equal(2, SectionLine.InView(headings, 896f, 600f, 1200f, 2, 896f));
            Assert.Equal(3, SectionLine.InView(headings, 1200f, 600f, 1200f, 3, 1200f));
            // Scrolled away from there, the rule above holds again.
            Assert.Equal(1, SectionLine.InView(headings, 500f, 600f, 1200f, 2, 896f));

            // A page too short to scroll lights its first section at the top.
            Assert.Equal(0, SectionLine.InView(headings, 0f, 600f, 0f, -1, 0f));
            Assert.Equal(-1, SectionLine.InView(new float[0], 0f, 600f, 0f, -1, 0f));
        }
    }
}
