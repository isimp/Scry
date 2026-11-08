using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class KeptMarksTests
    {
        // Each kind on Valheim's map stands in a mark of its own, so a dot or a square tells at a
        // glance which kind it is: one of eight colours, then, past eight kinds, the same colours
        // in another shape, a circle, a diamond, a square or a triangle, 32 marks told apart. A
        // kind keeps its mark while it is kept, with the character; one kept anew takes the
        // lowest mark free. Every kind of dungeon keeps its amber discs.

        [Fact]
        public void EachKindKeptStandsInAMarkOfItsOwnKeptWithTheCharacter()
        {
            var book = new CompendiumBook();
            book.Keep("rock4_copper", true);
            book.Keep("rock4_tin", true);
            book.Keep("loc:Grave1", true);
            Assert.Equal(new int?[] { KeptMarks.Plain(0, MarkShape.Triangle), KeptMarks.Plain(1, MarkShape.Triangle), KeptMarks.Plain(0, MarkShape.Circle) }, new[] { book.MarkOf("rock4_copper"), book.MarkOf("rock4_tin"), book.MarkOf("loc:Grave1") });
            Assert.Equal(KeptMarks.Dungeons, book.MarkOf(CompendiumBook.Dungeons));
            Assert.Null(book.MarkOf("rock4_silver"));

            var back = CompendiumBook.Load(book.Save());
            Assert.Equal(book.MarkOf("loc:Grave1"), back.MarkOf("loc:Grave1"));

            // Taken off, its mark is free for the next kept; the others keep theirs.
            back.Keep("rock4_copper", false);
            back.Keep("rock4_silver", true);
            Assert.Equal(KeptMarks.Plain(0, MarkShape.Triangle), back.MarkOf("rock4_silver"));
            Assert.Equal(KeptMarks.Plain(1, MarkShape.Triangle), back.MarkOf("rock4_tin"));
            // A kind shown and not kept takes the lowest mark free in its category's shape.
            Assert.Equal(KeptMarks.Plain(2, MarkShape.Triangle), back.FreeMark("rock4_obsidian"));
            Assert.Equal(KeptMarks.Plain(1, MarkShape.Circle), back.FreeMark("loc:Ruin1"));
        }

        [Fact]
        public void AnOlderScryReadingTheBookKeepsItsKeptKindsTheMarksBesideThem()
        {
            // The kept line stays as an older Scry reads it, the mark on a line of its own, which an older Scry passes over.
            var book = new CompendiumBook();
            book.Keep("rock4_copper", true);
            var saved = book.Save();
            Assert.Contains("kept\trock4_copper\n", saved);
            Assert.Equal(KeptMarks.Plain(0, MarkShape.Triangle), CompendiumBook.Load(saved).MarkOf("rock4_copper"));
        }

        [Fact]
        public void EachShapeStandsWholeInsideItsPictureItsRingAndHaloUncut()
        {
            // The picture spans -1 to 1 each way: at its edge every shape has reached past its own, so nothing of it is cut off.
            foreach (MarkShape shape in System.Enum.GetValues(typeof(MarkShape)))
                foreach (var (x, y) in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
                    Assert.True(SeenOnMap.ShapeShare(shape, x, y) >= 1f, $"{shape} reaches its edge only at {SeenOnMap.ShapeShare(shape, x, y)} at ({x}, {y})");
        }

        [Fact]
        public void AnOlderBooksKeptKindsAreGivenMarksInTheirOrder()
        {
            var book = CompendiumBook.Load("kept\trock4_tin\nkept\trock4_copper\n");
            Assert.Equal(KeptMarks.Plain(0, MarkShape.Triangle), book.MarkOf("rock4_copper"));
            Assert.Equal(KeptMarks.Plain(1, MarkShape.Triangle), book.MarkOf("rock4_tin"));
            // Two kept in one mark stay so: the player may pick one look for both.
            var twice = CompendiumBook.Load("kept\trock4_tin\t0\nkept\trock4_copper\t0\n");
            Assert.Equal(twice.MarkOf("rock4_tin"), twice.MarkOf("rock4_copper"));
        }

        [Fact]
        public void ThirtyTwoKindsStandInThirtyTwoMarksTheNinthInTheFirstColourAsADiamond()
        {
            var looks = Enumerable.Range(0, KeptMarks.Most).Select(m => (KeptMarks.ColourOf(m), KeptMarks.ShapeOf(m))).ToList();
            Assert.Equal(KeptMarks.Most, looks.Distinct().Count());
            Assert.Equal((0, MarkShape.Circle), looks[0]);
            Assert.Equal((7, MarkShape.Circle), looks[7]);
            Assert.Equal((0, MarkShape.Diamond), looks[8]);
            Assert.Equal((0, MarkShape.Triangle), looks[24]);
            // Dealt within a shape, the colour fewest stand in, past them beginning again.
            var circles = Enumerable.Range(0, KeptMarks.Colours).ToList();
            Assert.Equal(KeptMarks.Plain(0, MarkShape.Triangle), KeptMarks.Dealt(MarkShape.Triangle, circles));
            Assert.Equal(0, KeptMarks.Dealt(MarkShape.Circle, circles));
            Assert.Equal(1, KeptMarks.Dealt(MarkShape.Circle, circles.Append(0)));
            // A picture stands in no shape's colours.
            Assert.Equal(0, KeptMarks.Dealt(MarkShape.Circle, new[] { KeptMarks.Pictured(0, 1) }));
        }

        [Fact]
        public void EachShapeIsDrawnByHowFarAPointLiesFromItsMiddleItsEdgeAtTheWhole()
        {
            // The circle as the dots were drawn; the others reach the whole at their own edge.
            Assert.Equal(0.5f, SeenOnMap.ShapeShare(MarkShape.Circle, 0.3f, 0.4f), 3);
            Assert.Equal(0f, SeenOnMap.ShapeShare(MarkShape.Diamond, 0f, 0f), 3);
            var square = SeenOnMap.ShapeShare(MarkShape.Square, 0.5f, 0.5f);
            Assert.Equal(square, SeenOnMap.ShapeShare(MarkShape.Square, 0.5f, -0.2f), 3);
            Assert.True(SeenOnMap.ShapeShare(MarkShape.Diamond, 0.5f, 0.5f) > SeenOnMap.ShapeShare(MarkShape.Diamond, 0.7f, 0f));
            // A triangle stands point up: as far up as down, the point is nearer its edge.
            Assert.True(SeenOnMap.ShapeShare(MarkShape.Triangle, 0f, 0.5f) < SeenOnMap.ShapeShare(MarkShape.Triangle, 0f, -0.5f));
            foreach (MarkShape shape in System.Enum.GetValues(typeof(MarkShape)))
                Assert.True(SeenOnMap.ShapeShare(shape, 0f, 0f) < 0.01f);
        }

        [Fact]
        public void ASquareOfOneKindStandsInItsColourOneOfSeveralInNone()
        {
            Assert.Equal(2, KeptMarks.SquareMark(new[] { (2, 3) }));
            Assert.Equal(KeptMarks.Dungeons, KeptMarks.SquareMark(new[] { (KeptMarks.Dungeons, 1) }));
            Assert.Null(KeptMarks.SquareMark(new[] { (2, 3), (5, 1) }));
            Assert.Equal(4, KeptMarks.SquareCount(new[] { (2, 3), (5, 1) }));
        }
    }
}
