using Xunit;

namespace Scry.Tests
{
    public class TableFitTests
    {
        [Fact]
        public void ATableThatFitsKeepsEachColumnAsWideAsItWants()
        {
            var widths = new[] { 40f, 60f };
            TableFit.Fit(widths, new[] { 30f, 30f }, 200f);
            Assert.Equal(new[] { 40f, 60f }, widths);
        }

        [Fact]
        public void ATableTooWideShrinksOnlyWhatIsWiderThanItsHeading()
        {
            // An attack table in the compact view: "Reach" and "Every" keep their headings whole, the wide damage gives way.
            var widths = new[] { 60f, 200f, 50f, 40f };
            TableFit.Fit(widths, new[] { 40f, 50f, 40f, 40f }, 250f);
            Assert.Equal(250f, widths[0] + widths[1] + widths[2] + widths[3], 3);
            Assert.True(widths[2] >= 40f && widths[3] >= 40f);
            Assert.True(widths[1] < 200f);
        }

        [Fact]
        public void WhereEvenTheHeadingsDoNotFitEveryColumnShrinksAlike()
        {
            var widths = new[] { 100f, 100f };
            TableFit.Fit(widths, new[] { 80f, 80f }, 100f);
            Assert.Equal(new[] { 50f, 50f }, widths);
        }
    }
}
