using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class BookColumnsTests
    {
        // The compendium box stands each level a world's source can show in a column of its own
        // down the box, after the captions: Off, Seen, Known, With insides, so a level stands at
        // one place on every row, a row without one leaving its cell empty. Counted, the whole
        // world's alone, spans Seen and Known; the rules take the first three columns. A column
        // is as wide as the widest that stands in it, and one spanning is made room for at the
        // last column it spans.

        [Fact]
        public void EachLevelStandsInAColumnOfItsOwnCountedSpanningSeenAndKnown()
        {
            Assert.Equal((0, 0), BookColumns.Of(SourceLevel.Off));
            Assert.Equal((1, 1), BookColumns.Of(SourceLevel.Seen));
            Assert.Equal((2, 2), BookColumns.Of(SourceLevel.Known));
            Assert.Equal((3, 3), BookColumns.Of(SourceLevel.WithInsides));
            Assert.Equal((1, 2), BookColumns.Of(SourceLevel.Counted));
            // The rules take the first three, so the box reads as one grid.
            Assert.Equal((0, 0), BookColumns.Of(CompendiumRule.Off));
            Assert.Equal((1, 1), BookColumns.Of(CompendiumRule.Loose));
            Assert.Equal((2, 2), BookColumns.Of(CompendiumRule.Strict));
        }

        [Fact]
        public void AColumnIsAsWideAsTheWidestInItAndASpanWiderThanItsColumnsWidensTheLast()
        {
            var widths = new float[BookColumns.Count];
            var cells = new List<(int, int, float)> { (1, 2, 120f), (0, 0, 30f), (0, 0, 24f), (1, 1, 40f), (1, 1, 48f), (2, 2, 50f), (3, 3, 80f), (1, 2, 60f) };
            BookColumns.Widths(widths, cells, gap: 4f);
            // Seen's 48 and Known's 50 with their gap hold 102: Counted's 120 widens Known by 18.
            Assert.Equal(new[] { 30f, 48f, 68f, 80f }, widths);
            // Laid out again, the columns start afresh; a span its columns hold leaves them as they are.
            BookColumns.Widths(widths, new List<(int, int, float)> { (1, 1, 48f), (2, 2, 50f), (1, 2, 60f) }, 4f);
            Assert.Equal(new[] { 0f, 48f, 50f, 0f }, widths);
        }

        [Fact]
        public void CellsStandAGapApartFromWhereTheColumnsStartAndASpanTakesItsColumnsAndTheirGaps()
        {
            var widths = new[] { 30f, 48f, 68f, 80f };
            Assert.Equal((100f, 30f), BookColumns.Cell(widths, left: 100f, gap: 4f, first: 0, last: 0));
            Assert.Equal((134f, 48f), BookColumns.Cell(widths, 100f, 4f, 1, 1));
            Assert.Equal((258f, 80f), BookColumns.Cell(widths, 100f, 4f, 3, 3));
            Assert.Equal((134f, 120f), BookColumns.Cell(widths, 100f, 4f, 1, 2));
            Assert.Equal(238f, BookColumns.Width(widths, 4f));
        }
    }
}
