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
        public void TheLineShowsOnEveryPageWithASection()
        {
            // A page of one section names it too, so every page has the same rows, fold all at the line's end.
            Assert.False(SectionLine.Shows(0));
            Assert.True(SectionLine.Shows(1));
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
            // At the top the first is lit however short it is, the next heading in the top third already.
            Assert.Equal(0, SectionLine.InView(new[] { 0f, 60f, 900f }, 0f, 600f, 1200f, -1, 0f));
            Assert.Equal(-1, SectionLine.InView(new float[0], 0f, 600f, 0f, -1, 0f));
        }

        [Fact]
        public void APageLaidOutOverFramesHoldsAsManyRowsForItsLineAsTheLastLineAtItsWidthTook()
        {
            // The names come only once the page is whole: the skeleton stands in as many rows as the
            // last line drawn across the same width, so a wrapping line moves nothing below it.
            Assert.Equal(2, SectionLine.SkeletonRows(lastRows: 2, lastWidth: 300f, width: 300f));
            Assert.Equal(2, SectionLine.SkeletonRows(2, 300.2f, 300f));
            // Another width wraps otherwise: one row, as before any line was drawn.
            Assert.Equal(1, SectionLine.SkeletonRows(2, 300f, 420f));
            Assert.Equal(1, SectionLine.SkeletonRows(0, 0f, 300f));
            Assert.Equal(1, SectionLine.SkeletonRows(0, 300f, 300f));
            Assert.Equal(1, SectionLine.SkeletonRows(1, 300f, 300f));
            // Never more than a few rows, however narrow the last panel was.
            Assert.Equal(SectionLine.MostSkeletonRows, SectionLine.SkeletonRows(9, 120f, 120f));
        }

        [Fact]
        public void HowManyRowsALineTookIsToldByItsHeight()
        {
            // Rows 20 tall, 2 apart: one row is 20, two 42, three 64.
            Assert.Equal(1, SectionLine.RowsOf(20f, 20f, 2f));
            Assert.Equal(2, SectionLine.RowsOf(42f, 20f, 2f));
            Assert.Equal(3, SectionLine.RowsOf(64f, 20f, 2f));
            Assert.Equal(1, SectionLine.RowsOf(0f, 20f, 2f));
            // Rows wider apart than they are tall: two rows 10 tall, 20 apart, stand 40.
            Assert.Equal(2, SectionLine.RowsOf(40f, 10f, 20f));
            // And the height of so many rows is told back the same way.
            Assert.Equal(42f, SectionLine.RowsHeight(2, 20f, 2f), 3);
            Assert.Equal(20f, SectionLine.RowsHeight(1, 20f, 2f), 3);
        }
    }
}
