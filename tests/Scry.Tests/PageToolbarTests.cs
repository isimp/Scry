using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class PageToolbarTests
    {
        // Under a page's name one toolbar: what the entry does as one group from the left, its
        // sliders' icons as one group at the right end of the first line. Where the first line
        // has no room the actions wrap as a group onto the lines under it, each from the left;
        // the icons never move.

        private static readonly List<(float X, int Line)> Placed = new List<(float, int)>();

        [Fact]
        public void ActionsStandFromTheLeftJoinedAndTheIconsAtTheFirstLinesEnd()
        {
            var laid = PageToolbar.Lay(400f, new[] { 80f, 60f }, icons: 3, icon: 24f, seam: 1f, gap: 8f, Placed);
            Assert.Equal(new[] { (0f, 0), (81f, 0) }, Placed);
            Assert.Equal(400f - (3 * 24f + 2 * 1f), laid.IconsX);
            Assert.Equal(1, laid.Lines);
        }

        [Fact]
        public void WhereTheFirstLineHasNoRoomTheActionsWrapAsAGroupTheIconsStaying()
        {
            var laid = PageToolbar.Lay(300f, new[] { 120f, 120f, 120f }, icons: 2, icon: 24f, seam: 1f, gap: 8f, Placed);
            // The first line keeps room for the icons and the gap before them: 300 - 49 - 8.
            Assert.Equal(new[] { (0f, 0), (121f, 0), (0f, 1) }, Placed);
            Assert.Equal(300f - 49f, laid.IconsX);
            Assert.Equal(2, laid.Lines);
            // Without icons the first line runs the whole width.
            PageToolbar.Lay(362f, new[] { 120f, 120f, 120f }, icons: 0, icon: 24f, seam: 1f, gap: 8f, Placed);
            Assert.Equal(new[] { (0f, 0), (121f, 0), (242f, 0) }, Placed);
            // What would fit the whole width but runs into the icons goes under them.
            PageToolbar.Lay(300f, new[] { 100f, 100f, 60f }, icons: 2, icon: 24f, seam: 1f, gap: 8f, Placed);
            Assert.Equal(new[] { (0f, 0), (101f, 0), (0f, 1) }, Placed);
            // Under the icons' line the actions have the whole width.
            PageToolbar.Lay(300f, new[] { 200f, 200f, 90f }, icons: 2, icon: 24f, seam: 1f, gap: 8f, Placed);
            Assert.Equal(new[] { (0f, 0), (0f, 1), (201f, 1) }, Placed);
        }

        [Fact]
        public void ACaptionStandsFaintAfterTheActionsAGapAwayWrappingAsTheyDo()
        {
            // A status effect's "only the look, never the effect" beside its button.
            var laid = PageToolbar.Lay(400f, new[] { 100f }, icons: 3, icon: 24f, seam: 1f, gap: 8f, Placed, caption: 150f);
            Assert.Equal((108f, 0), (laid.CaptionX, laid.CaptionLine));
            Assert.Equal(1, laid.Lines);
            // No room left of the icons: under the actions, from the left.
            laid = PageToolbar.Lay(300f, new[] { 100f }, icons: 3, icon: 24f, seam: 1f, gap: 8f, Placed, caption: 150f);
            Assert.Equal((0f, 1), (laid.CaptionX, laid.CaptionLine));
            Assert.Equal(2, laid.Lines);
            // With no action, where the button would be.
            laid = PageToolbar.Lay(300f, new float[0], icons: 0, icon: 24f, seam: 1f, gap: 8f, Placed, caption: 150f);
            Assert.Equal((0f, 0), (laid.CaptionX, laid.CaptionLine));
            Assert.Equal(1, laid.Lines);
        }

        [Fact]
        public void AnActionWiderThanALineStandsAloneAtItsStartAndNothingIsNoLine()
        {
            var laid = PageToolbar.Lay(100f, new[] { 150f, 40f }, icons: 0, icon: 24f, seam: 1f, gap: 8f, Placed);
            Assert.Equal(new[] { (0f, 0), (0f, 1) }, Placed);
            Assert.Equal(2, laid.Lines);
            Assert.Equal(0, PageToolbar.Lay(300f, new float[0], icons: 0, icon: 24f, seam: 1f, gap: 8f, Placed).Lines);
            Assert.Empty(Placed);
            Assert.Equal(1, PageToolbar.Lay(300f, new float[0], icons: 2, icon: 24f, seam: 1f, gap: 8f, Placed).Lines);
        }
    }
}
