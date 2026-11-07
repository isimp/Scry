using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class MarkPickTests
    {
        // A kept kind's mark is the player's to pick in the On the map list: one of the eight
        // colours in one of the four shapes, one of the game's map pictures in a colour, or the
        // kind's own picture on a colour. The pick is kept with the character. Two kinds may stand
        // in one look. Every kind of dungeon keeps its amber discs, and a kind kept anew still
        // takes the lowest colour and shape no kind stands in.

        [Fact]
        public void APickedMarkStaysWithTheCharacter()
        {
            var book = new CompendiumBook();
            book.Keep("rock4_copper", true);
            var red = KeptMarks.Plain(7, MarkShape.Triangle);
            Assert.True(book.Pick("rock4_copper", red));
            Assert.Equal(red, book.MarkOf("rock4_copper"));
            Assert.True(book.Changed);

            var back = CompendiumBook.Load(book.Save());
            Assert.Equal(red, back.MarkOf("rock4_copper"));
        }

        [Fact]
        public void EveryLookOnOfferIsItsOwnAndStandsInItsColour()
        {
            var looks = Enumerable.Range(0, KeptMarks.Looks).ToList();
            var told = looks.Select(m => (KeptMarks.ColourOf(m), KeptMarks.ShapeOf(m), KeptMarks.PictureOf(m), KeptMarks.IsOwn(m))).ToList();
            Assert.Equal(looks.Count, told.Distinct().Count());
            Assert.Equal(KeptMarks.Most + KeptMarks.Pictures * KeptMarks.Colours + KeptMarks.Colours, KeptMarks.Looks);

            // Every picture stands in the ring's round shape, and a kind's own is none of the game's.
            for (var mark = KeptMarks.Most; mark < KeptMarks.Looks; mark++) Assert.Equal(MarkShape.Circle, KeptMarks.ShapeOf(mark));
            for (var colour = 0; colour < KeptMarks.Colours; colour++) Assert.Null(KeptMarks.PictureOf(KeptMarks.Own(colour)));

            var fire = KeptMarks.Pictured(3, 0);
            Assert.Equal(3, KeptMarks.ColourOf(fire));
            Assert.Equal(0, KeptMarks.PictureOf(fire));
            Assert.False(KeptMarks.IsOwn(fire));
            // A picture stands in the ring's round shape.
            Assert.Equal(MarkShape.Circle, KeptMarks.ShapeOf(fire));

            var own = KeptMarks.Own(6);
            Assert.Equal(6, KeptMarks.ColourOf(own));
            Assert.True(KeptMarks.IsOwn(own));
            Assert.Null(KeptMarks.PictureOf(own));

            var plain = KeptMarks.Plain(2, MarkShape.Square);
            Assert.Equal((2, MarkShape.Square), (KeptMarks.ColourOf(plain), KeptMarks.ShapeOf(plain)));
            Assert.Null(KeptMarks.PictureOf(plain));
            Assert.False(KeptMarks.IsOwn(plain));
        }

        [Fact]
        public void TwoKindsMayStandInOneLookAndStillDoAfterTheBookIsRead()
        {
            var book = new CompendiumBook();
            book.Keep("rock4_copper", true);
            book.Keep("rock4_tin", true);
            Assert.True(book.Pick("rock4_tin", book.MarkOf("rock4_copper").Value));

            var back = CompendiumBook.Load(book.Save());
            Assert.Equal(back.MarkOf("rock4_copper"), back.MarkOf("rock4_tin"));
            Assert.Equal(KeptMarks.Plain(0, MarkShape.Circle), back.MarkOf("rock4_tin"));
        }

        [Fact]
        public void AKindKeptAnewTakesTheLowestColourAndShapeNoKindStandsIn()
        {
            var book = new CompendiumBook();
            book.Keep("rock4_copper", true);
            book.Pick("rock4_copper", KeptMarks.Pictured(0, 4));
            // Copper left the first colour's circle for a picture, so the next kept takes it.
            book.Keep("rock4_tin", true);
            Assert.Equal(KeptMarks.Plain(0, MarkShape.Circle), book.MarkOf("rock4_tin"));
            Assert.Equal(KeptMarks.Plain(1, MarkShape.Circle), book.FreeMark());
        }

        [Fact]
        public void PastEveryColourAndShapeAKindKeptAnewStandsInOneAgainNeverAPicture()
        {
            var book = new CompendiumBook();
            for (var i = 0; i < KeptMarks.Most; i++) book.Keep("kind" + i, true);
            book.Keep("one more", true);
            var mark = book.MarkOf("one more").Value;
            Assert.Null(KeptMarks.PictureOf(mark));
            Assert.False(KeptMarks.IsOwn(mark));
            Assert.InRange(mark, 0, KeptMarks.Most - 1);
            // One held by fewest kinds, the first of them: none is held twice yet, so the first.
            Assert.Equal(KeptMarks.Plain(0, MarkShape.Circle), mark);
            book.Keep("and another", true);
            Assert.Equal(KeptMarks.Plain(1, MarkShape.Circle), book.MarkOf("and another"));
        }

        [Fact]
        public void TheDungeonsAKindNotKeptAndALookNotOnOfferCannotBePicked()
        {
            var book = new CompendiumBook();
            book.Keep("rock4_copper", true);
            var before = book.Save();
            var version = book.KeptVersion;
            Assert.False(book.Pick(CompendiumBook.Dungeons, KeptMarks.Plain(2, MarkShape.Circle)));
            Assert.False(book.Pick("rock4_tin", KeptMarks.Plain(2, MarkShape.Circle)));
            Assert.False(book.Pick("rock4_copper", -1));
            Assert.False(book.Pick("rock4_copper", KeptMarks.Looks));
            Assert.Equal(KeptMarks.Dungeons, book.MarkOf(CompendiumBook.Dungeons));
            Assert.Equal(before, book.Save());
            Assert.Equal(version, book.KeptVersion);
            // The look it has already is no change.
            Assert.False(book.Pick("rock4_copper", book.MarkOf("rock4_copper").Value));
        }

        [Fact]
        public void AMarkThisScryDoesNotOfferIsDealtAnewWhenTheBookIsRead()
        {
            var book = CompendiumBook.Load("kept\trock4_copper\nkeptmark\trock4_copper\t999\nkept\trock4_tin\t500\n");
            Assert.Equal(KeptMarks.Plain(0, MarkShape.Circle), book.MarkOf("rock4_copper"));
            Assert.Equal(KeptMarks.Plain(1, MarkShape.Circle), book.MarkOf("rock4_tin"));
        }

        [Fact]
        public void ItsOwnPictureIsItsIconAPlantOrDepositsDropAndACreaturesTrophy()
        {
            var none = new List<(string Name, bool HasIcon)>();
            // An item or a piece has an icon of its own.
            Assert.Equal("Wood", KeptMarks.OwnPictureOf("Wood", Kind.Item, true, none));
            Assert.Equal("piece_workbench", KeptMarks.OwnPictureOf("piece_workbench", Kind.Piece, true, none));
            // A deposit or bush shows the first thing it drops that has an icon.
            Assert.Equal("CopperOre", KeptMarks.OwnPictureOf("rock4_copper", Kind.Resource, false, new[] { ("copper_frac", false), ("CopperOre", true), ("Stone", true) }));
            // A creature shows its trophy, never its other drops.
            Assert.Equal("TrophyGreydwarf", KeptMarks.OwnPictureOf("Greydwarf", Kind.Creature, false, new[] { ("Wood", true), ("TrophyGreydwarf", true) }));
            Assert.Null(KeptMarks.OwnPictureOf("Wolf_cub", Kind.Creature, false, new[] { ("WolfPelt", true) }));
            // A place, or what drops nothing with an icon, has none.
            Assert.Null(KeptMarks.OwnPictureOf("loc:Crypt2", Kind.Location, false, none));
            Assert.Null(KeptMarks.OwnPictureOf("rock_mistlands", Kind.Resource, false, new[] { ("rock_frac", false) }));
        }

        [Fact]
        public void ThePickerSaysWhatEachChoiceIsAndWhoStandsInItAlready()
        {
            Assert.Equal(KeptMarks.Colours, KeptMarkWords.Colours.Length);
            Assert.Equal(KeptMarks.Shapes, KeptMarkWords.Shapes.Length);
            Assert.Equal(KeptMarks.Pictures, KeptMarkWords.Pictures.Length);
            Assert.Equal("Red triangle", KeptMarkWords.Choice(KeptMarks.Plain(7, MarkShape.Triangle), null));
            Assert.Equal("Hammer, in sky", KeptMarkWords.Choice(KeptMarks.Pictured(1, 2), null));
            Assert.Equal("Copper ore, on rust", KeptMarkWords.Choice(KeptMarks.Own(5), "Copper ore"));
            Assert.Equal("Sky circle, also the mark of Copper deposit and Tin deposit", KeptMarkWords.Said(KeptMarks.Plain(1, MarkShape.Circle), null, new[] { "Copper deposit", "Tin deposit" }));
            Assert.Equal("Sky circle", KeptMarkWords.Said(KeptMarks.Plain(1, MarkShape.Circle), null, new string[0]));
        }

        [Fact]
        public void KeepingAPlacesKindsAsOneKeepsThePictureOneOfThemStoodIn()
        {
            var book = new CompendiumBook();
            book.Keep("loc:Grave1", true);
            book.Keep("loc:Grave2", true);
            var picked = KeptMarks.Pictured(5, 1);
            book.Pick("loc:Grave2", picked);
            book.Keep("loc:Grave1+loc:Grave2", true);
            Assert.Equal(picked, book.MarkOf("loc:Grave1+loc:Grave2"));
        }

        // Beside the lit Keep on the map, on a kind's page and on the map's strip, a piece shows
        // the mark its line stands in, and a click on it opens the same picker under it.

        [Fact]
        public void APageOffersTheMarkOfTheLineItKeepsItself()
        {
            var book = new CompendiumBook();
            Assert.Null(book.PickableMark("rock4_copper"));
            book.Keep("rock4_copper", true);
            Assert.Equal(book.MarkOf("rock4_copper"), book.PickableMark("rock4_copper"));
            var hammer = KeptMarks.Pictured(5, 2);
            Assert.True(book.Pick("rock4_copper", hammer));
            Assert.Equal(hammer, book.PickableMark("rock4_copper"));

            // Every kind of dungeon keeps its amber discs, so its piece is never offered.
            Assert.True(book.Keeps(CompendiumBook.Dungeons));
            Assert.Null(book.PickableMark(CompendiumBook.Dungeons));

            // Kept only as one of a place's kinds kept at once: the joined line's piece offers it, not the kind's own.
            book.Keep("loc:Grave1+loc:Grave2", true);
            Assert.Null(book.PickableMark("loc:Grave1"));
            Assert.Equal(book.MarkOf("loc:Grave1+loc:Grave2"), book.PickableMark("loc:Grave1+loc:Grave2"));

            // Taken off, it offers none again.
            book.Keep("rock4_copper", false);
            Assert.Null(book.PickableMark("rock4_copper"));
            Assert.Null(book.PickableMark(null));
        }

        [Fact]
        public void ThePickerStandsUnderWhatOpenedItOrOverItWhereTheRoomEnds()
        {
            // Room from 0 to 1,000 wide and from 0 to 800 high; a picker 340 by 300, 2 apart from what opened it.
            Assert.Equal((100f, 132f), MarkPickerPlace.Of(100f, 100f, 130f, 340f, 300f, 0f, 0f, 1000f, 800f, 2f));
            // Just room under it: under it.
            Assert.Equal((100f, 500f), MarkPickerPlace.Of(100f, 468f, 498f, 340f, 300f, 0f, 0f, 1000f, 800f, 2f));
            // No room under it: over it.
            Assert.Equal((100f, 398f), MarkPickerPlace.Of(100f, 700f, 730f, 340f, 300f, 0f, 0f, 1000f, 800f, 2f));
            // No room either way: from the room's top, never past it.
            Assert.Equal((100f, 0f), MarkPickerPlace.Of(100f, 200f, 230f, 340f, 700f, 0f, 0f, 1000f, 800f, 2f));
            // Near the room's right end: moved left to stay inside, and right of its left end.
            Assert.Equal((660f, 132f), MarkPickerPlace.Of(900f, 100f, 130f, 340f, 300f, 0f, 0f, 1000f, 800f, 2f));
            Assert.Equal((50f, 132f), MarkPickerPlace.Of(10f, 100f, 130f, 340f, 300f, 50f, 0f, 1000f, 800f, 2f));
            // A room narrower than the picker starts it at the room's left.
            Assert.Equal((50f, 132f), MarkPickerPlace.Of(200f, 100f, 130f, 340f, 300f, 50f, 0f, 300f, 800f, 2f));
        }

        [Fact]
        public void TheMarkPieceSaysAClickPicksHowTheKindStands()
        {
            Assert.Equal("How Copper deposit stands on Valheim's map: a click picks its colour, shape or picture", KeptMarkWords.PieceTip("Copper deposit"));
        }
    }
}
