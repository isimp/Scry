using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class KeptCategoriesTests
    {
        // Kinds on the map stand under categories, and a category gives its marks their shape.

        private static bool IsDungeon(string key) => key == "loc:Crypt2" || key == "loc:Crypt3";

        [Fact]
        public void APlaceStandsInACircleAndWhatIsFoundInATriangle()
        {
            Assert.Equal(MarkShape.Circle, KeptCategories.ShapeOf("loc:Grave1"));
            Assert.Equal(MarkShape.Triangle, KeptCategories.ShapeOf("rock4_copper"));
            // Several kinds of one place kept as one line go by the first.
            Assert.Equal(MarkShape.Circle, KeptCategories.ShapeOf("loc:Ruin1+loc:Ruin2"));
        }

        [Fact]
        public void EachKeptLineFallsUnderItsCategory()
        {
            Assert.Equal(KeptCategory.Dungeons, KeptCategories.Of(CompendiumBook.Dungeons, IsDungeon));
            Assert.Equal(KeptCategory.Dungeons, KeptCategories.Of("loc:Crypt2", IsDungeon));
            Assert.Equal(KeptCategory.Dungeons, KeptCategories.Of("loc:Crypt2+loc:Crypt3", IsDungeon));
            Assert.Equal(KeptCategory.Locations, KeptCategories.Of("loc:Grave1", IsDungeon));
            Assert.Equal(KeptCategory.Resources, KeptCategories.Of("rock4_copper", IsDungeon));
        }

        [Fact]
        public void AKindKeptIsDealtItsCategorysShapeInTheColourFewestStandIn()
        {
            var book = new CompendiumBook();
            book.Keep("rock4_copper", true);
            book.Keep("rock4_tin", true);
            book.Keep("loc:Grave1", true);
            Assert.Equal(KeptMarks.Plain(0, MarkShape.Triangle), book.MarkOf("rock4_copper"));
            Assert.Equal(KeptMarks.Plain(1, MarkShape.Triangle), book.MarkOf("rock4_tin"));
            // Colours are counted within a shape: the first location takes the first colour too.
            Assert.Equal(KeptMarks.Plain(0, MarkShape.Circle), book.MarkOf("loc:Grave1"));
        }

        [Fact]
        public void TheNinthOfACategoryRepeatsAColour()
        {
            var book = new CompendiumBook();
            for (var i = 0; i < KeptMarks.Colours; i++) book.Keep("loc:P" + i, true);
            Assert.Equal(KeptMarks.Colours, Enumerable.Range(0, KeptMarks.Colours).Select(i => book.MarkOf("loc:P" + i)).Distinct().Count());
            book.Keep("loc:Q", true);
            Assert.Equal(KeptMarks.Plain(0, MarkShape.Circle), book.MarkOf("loc:Q"));
            // The next goes to the colour fewest stand in, not the first again.
            book.Keep("loc:R", true);
            Assert.Equal(KeptMarks.Plain(1, MarkShape.Circle), book.MarkOf("loc:R"));
        }

        [Fact]
        public void ABookReadFromBeforeCategoriesMovesItsPlainMarksOnceKeepingColours()
        {
            var book = CompendiumBook.Load("kept\tloc:Grave1\nkeptmark\tloc:Grave1\t9\nkept\trock4_copper\nkeptmark\trock4_copper\t2\n");
            Assert.Equal(KeptMarks.Plain(1, MarkShape.Circle), book.MarkOf("loc:Grave1"));
            Assert.Equal(KeptMarks.Plain(2, MarkShape.Triangle), book.MarkOf("rock4_copper"));
        }

        [Fact]
        public void TheMoveKeepsPicturesAndTellsKindsApartWhereAColourIsTaken()
        {
            var picture = KeptMarks.Pictured(3, 2);
            // A circle in lime stays; a diamond in lime can't join it, so it takes the next colour free.
            var book = CompendiumBook.Load($"kept\tloc:A\nkeptmark\tloc:A\t0\nkept\tloc:B\nkeptmark\tloc:B\t8\nkept\tloc:C\nkeptmark\tloc:C\t{picture}\n");
            Assert.Equal(KeptMarks.Plain(0, MarkShape.Circle), book.MarkOf("loc:A"));
            Assert.Equal(KeptMarks.Plain(1, MarkShape.Circle), book.MarkOf("loc:B"));
            Assert.Equal(picture, book.MarkOf("loc:C"));
        }

        [Fact]
        public void KindsThatSharedAMarkStillShareOneAfterTheMove()
        {
            var book = CompendiumBook.Load("kept\trock4_tin\t0\nkept\trock4_copper\t0\n");
            Assert.Equal(book.MarkOf("rock4_tin"), book.MarkOf("rock4_copper"));
            Assert.Equal(MarkShape.Triangle, KeptMarks.ShapeOf(book.MarkOf("rock4_tin").Value));
        }

        [Fact]
        public void AShapePickedAfterTheMoveStaysAcrossSaves()
        {
            var book = CompendiumBook.Load("kept\tloc:Grave1\nkeptmark\tloc:Grave1\t0\n");
            var picked = KeptMarks.Plain(4, MarkShape.Square);
            Assert.True(book.Pick("loc:Grave1", picked));
            var back = CompendiumBook.Load(book.Save());
            Assert.Equal(picked, back.MarkOf("loc:Grave1"));
            Assert.Equal(picked, CompendiumBook.Load(back.Save()).MarkOf("loc:Grave1"));
        }

        [Fact]
        public void TheListStandsEachCategoryUnderItsHeading()
        {
            var lines = KeptLine.Of(new[] { CompendiumBook.Dungeons, "loc:Grave1", "loc:Ruin1", "rock4_copper" }, new[] { "loc:Crypt2" }, IsDungeon, 0);
            Assert.Equal(new[] { KeptLineLook.Heading, KeptLineLook.Type, KeptLineLook.Heading, KeptLineLook.Kept, KeptLineLook.Kept, KeptLineLook.Heading, KeptLineLook.Kept }, lines.Select(l => l.Look));
            Assert.Equal(new[] { null, "loc:Crypt2", null, "loc:Grave1", "loc:Ruin1", null, "rock4_copper" }, lines.Select((l, i) => i == 0 ? null : l.Key));
            Assert.Equal(CompendiumBook.Dungeons, lines[0].Key);
            Assert.Equal((1, 1), (lines[0].Under, lines[0].Keeps));
            Assert.Equal((2, 2), (lines[2].Under, lines[2].Keeps));
        }

        [Fact]
        public void AFoldedCategoryStandsItsHeadingAlone()
        {
            var folded = KeptCategories.Toggled(0, KeptCategory.Locations);
            Assert.True(KeptCategories.Folded(folded, KeptCategory.Locations));
            Assert.False(KeptCategories.Folded(folded, KeptCategory.Resources));
            var lines = KeptLine.Of(new[] { "loc:Grave1", "rock4_copper" }, new string[0], IsDungeon, folded);
            Assert.Equal(new[] { KeptCategory.Dungeons, KeptCategory.Locations, KeptCategory.Resources, KeptCategory.Resources }, lines.Select(l => l.Category));
            // Still saying what it holds, for its × and its arrow.
            Assert.Equal((1, 1), (lines[1].Under, lines[1].Keeps));
            Assert.Equal(0, KeptCategories.Toggled(folded, KeptCategory.Locations));
        }

        [Fact]
        public void DungeonsTakenOffStayAtTheTopAndTheirTypesGoEmptyCategoriesStandNoHeading()
        {
            var lines = KeptLine.Of(new[] { "rock4_copper" }, new[] { "loc:Crypt2" }, IsDungeon, 0);
            Assert.Equal(new[] { KeptLineLook.Heading, KeptLineLook.Heading, KeptLineLook.Kept }, lines.Select(l => l.Look));
            Assert.Equal(KeptCategory.Dungeons, lines[0].Category);
            Assert.Equal((0, 0), (lines[0].Under, lines[0].Keeps));
            // A dungeon kept from its page stands under them all the same.
            var own = KeptLine.Of(new[] { "loc:Crypt2" }, new string[0], IsDungeon, 0);
            Assert.Equal(new[] { KeptLineLook.Heading, KeptLineLook.Kept }, own.Select(l => l.Look));
            Assert.Equal((1, 1), (own[0].Under, own[0].Keeps));
        }

        [Fact]
        public void TheKindsListLaysHeadingsOverTheirRows()
        {
            var open = new (bool Folded, int Kinds)[] { (false, 3), (true, 1), (false, 1) };
            // One column: each kind a line under its heading, a folded category its heading alone.
            var laid = KindsList.LaidRows(open, 1);
            Assert.Equal(new[] { 0, 4, 5 }, laid.Headings);
            Assert.Equal(new[] { (0, 1), (0, 2), (0, 3) }, laid.Rows[0]);
            Assert.Empty(laid.Rows[1]);
            Assert.Equal(new[] { (0, 6) }, laid.Rows[2]);
            Assert.Equal(7, laid.Lines);
            Assert.Equal(0, KindsList.LaidRows(new (bool, int)[0], 1).Lines);
        }

        [Fact]
        public void WiderTheRowsFillAcrossThenDownEachCategoryOnLinesOfItsOwn()
        {
            var open = new (bool Folded, int Kinds)[] { (false, 5), (true, 4), (false, 2) };
            var laid = KindsList.LaidRows(open, 2);
            Assert.Equal(new[] { 0, 4, 5 }, laid.Headings);
            Assert.Equal(new[] { (0, 1), (1, 1), (0, 2), (1, 2), (0, 3) }, laid.Rows[0]);
            Assert.Equal(new[] { (0, 6), (1, 6) }, laid.Rows[2]);
            Assert.Equal(7, laid.Lines);
        }

        [Fact]
        public void TheCategoriesFoldedAreKeptWithThePanelsPlace()
        {
            var read = PanelPlace.Read(new PanelPlace { KindsCats = 5, KeptCats = 2 }.Lines());
            Assert.Equal((5, 2), (read.KindsCats, read.KeptCats));
            var before = PanelPlace.Read(new PanelPlace { PlanBig = true }.Lines());
            Assert.Null(before.KindsCats);
            Assert.Null(before.KeptCats);
        }

        [Fact]
        public void TheListsNameCategoriesAndSayHowManyKinds()
        {
            Assert.Equal("Locations", SpotWords.CategoryName(KeptCategory.Locations));
            Assert.Equal("Resources", SpotWords.CategoryName(KeptCategory.Resources));
            Assert.Equal("Dungeons", SpotWords.CategoryName(KeptCategory.Dungeons));
            Assert.Equal("1 kind", SpotWords.CategoryKinds(1));
            Assert.Equal("9 kinds", SpotWords.CategoryKinds(9));
            Assert.Contains("every kind under it", SpotWords.KindHeadingTip(picked: false, anyPicked: false));
            Assert.Contains("fade", SpotWords.KindHeadingTip(picked: true, anyPicked: true));
        }
    }
}
