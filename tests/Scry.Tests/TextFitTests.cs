using Xunit;

namespace Scry.Tests
{
    public class TextFitTests
    {
        // Each letter one wide, so a width is a number of letters.
        private static float Letters(string text) => text.Length;

        [Fact]
        public void ATextThatFitsShowsWhole()
        {
            Assert.Equal("Crossroads", TextFit.Cut("Crossroads", 10f, Letters));
            Assert.Equal("", TextFit.Cut("", 3f, Letters));
        }

        [Fact]
        public void ATextTooWideIsCutAtTheEndWithAnEllipsisNeverAtTheStart()
        {
            // The way chip's name loses its end, never its start as a centred clipped label did.
            var cut = TextFit.Cut("Crossroads 01 hole shrine", 16f, Letters);
            Assert.StartsWith("Crossroads", cut);
            Assert.EndsWith("…", cut);
            Assert.True(cut.Length <= 16);
        }

        [Fact]
        public void ACutKeepsWholeWordsWhereThatLosesLittle()
        {
            Assert.Equal("Crossroads 01…", TextFit.Cut("Crossroads 01 hole shrine", 16f, Letters));
            // One long word is cut inside it rather than left with nothing.
            Assert.Equal("Crossro…", TextFit.Cut("Crossroadshole", 8f, Letters));
        }

        [Fact]
        public void NothingShowsWhereNotEvenTheEllipsisFits()
        {
            Assert.Equal("", TextFit.Cut("Crossroads", 0.5f, Letters));
        }

        [Fact]
        public void ARowsTagTooWideDropsItsFirstPartBeforeItIsCut()
        {
            // Three Burial Chambers rows told apart by their last part, not "Burial Chambers dungeon · Cr".
            Assert.Equal("Burial Chambers dungeon · Crypt2", TextFit.Tag("Burial Chambers dungeon · Crypt2", 40f, Letters));
            Assert.Equal("Crypt2", TextFit.Tag("Burial Chambers dungeon · Crypt2", 12f, Letters));
            Assert.Equal("Cryp…", TextFit.Tag("Burial Chambers dungeon · Crypt2", 5f, Letters));
            Assert.Equal("Burial…", TextFit.Tag("Burial Chambers dungeon", 10f, Letters));
        }
    }
}
