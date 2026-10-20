using Xunit;

namespace Scry.Tests
{
    public class HeaderLayoutTests
    {
        // The switch between all, the game's own and mods' things stands at the end of the
        // header's first row in the full view, where the search leaves room for it.

        [Fact]
        public void InTheFullViewTheOriginSwitchEndsTheFirstRow()
        {
            // Wherever the buttons before it end, summed in floats a hair past where its room starts.
            Assert.Equal(800f, HeaderLayout.OriginSwitch(width: 200f, rowRight: 1000f));
        }

        // The compact view's header is three rows however many kinds the list holds: the name with
        // the list button and the way back and forward after it; the search with its help,
        // favourites and recent at its end; and the kind picked as one pill that opens the others,
        // Learned after it while it holds news, and where the list is taken from as one chip at
        // the row's end.

        [Fact]
        public void InTheCompactViewTheListButtonAndTheWayBackFollowTheNameWhereTheTitleRowHasRoom()
        {
            // After the name, three buttons and their gaps, ending before the buttons at the row's right.
            Assert.True(HeaderLayout.NavInTitleRow(afterName: 114f, navWidth: 92f, before: 350f));
            Assert.True(HeaderLayout.NavInTitleRow(114f, 92f, 205.75f));
            // Clear world beside the view button leaves no room at the narrowest: they start the search row, as before.
            Assert.False(HeaderLayout.NavInTitleRow(114f, 92f, 205f));
        }

        [Fact]
        public void TheSearchFillsItsRowUpToItsHelpFavouritesAndRecent()
        {
            var row = HeaderLayout.CompactSearchRow(left: 16f, right: 404f, navWidth: 0f, buttonsWidth: 124f, gap: 8f);
            Assert.Equal((16f, 256f, 280f), (row.SearchX, row.SearchWidth, row.ButtonsX));
            // Where the list button and the way back start the row, the search follows them.
            row = HeaderLayout.CompactSearchRow(16f, 404f, 92f, 124f, 8f);
            Assert.Equal((116f, 156f, 280f), (row.SearchX, row.SearchWidth, row.ButtonsX));
        }

        [Fact]
        public void ThePickedKindLeadsItsRowLearnedFollowsItAndWhereTheListIsTakenFromEndsIt()
        {
            var row = HeaderLayout.CompactKindRow(left: 16f, right: 404f, pillWidth: 150f, learnedWidth: 95f, originWidth: 90f, gap: 4f);
            Assert.Equal((16f, 150f, 170f, 314f), (row.PillX, row.PillWidth, row.LearnedX, row.OriginX));
            // A long kind's pill gives way before Learned and the origin chip do.
            row = HeaderLayout.CompactKindRow(16f, 404f, 260f, 95f, 90f, 4f);
            Assert.Equal((16f, 195f, 215f, 314f), (row.PillX, row.PillWidth, row.LearnedX, row.OriginX));
            // With nothing learned to tell, the pill alone leads it.
            row = HeaderLayout.CompactKindRow(16f, 404f, 330f, 0f, 90f, 4f);
            Assert.Equal((16f, 294f, 314f), (row.PillX, row.PillWidth, row.OriginX));
        }
    }
}
