using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PlaceSquaresTests
    {
        // Show on map's squares (B): for each place a character never came near but whose ground
        // it explored, the server tells the 128 m square it stands in, never where; the map shades
        // that square so the player knows one stands somewhere inside.

        private const int MapSize = 200;
        private const float PixelSize = 12f;

        /// <summary>A map explored where the predicate holds for a world position, by the pixel the game maps it to.</summary>
        private static ExploredGrid Explored(System.Func<float, float, bool> at) =>
            ExploredGrid.FromMap(MapSize, PixelSize, i => at((i % MapSize - MapSize / 2) * PixelSize, (i / MapSize - MapSize / 2) * PixelSize));

        [Fact]
        public void APlaceInExploredGroundIsToldByItsSquareOnly()
        {
            var grid = Explored((x, z) => true);
            var squares = PlaceSquares.Of(grid, new[] { ("TrollCave02", 700f, 1030f) });
            var square = Assert.Single(squares);
            Assert.Equal(("TrollCave02", 5, 8, 1), (square.Kind, square.X, square.Z, square.Count));
        }

        [Fact]
        public void SquaresGoByTheWorldsOwnGridWestAndSouthOfTheMiddleToo()
        {
            Assert.Equal(0, PlaceSquares.Index(0f));
            Assert.Equal(0, PlaceSquares.Index(127.9f));
            Assert.Equal(1, PlaceSquares.Index(128f));
            Assert.Equal(-1, PlaceSquares.Index(-0.5f));
            Assert.Equal(-2, PlaceSquares.Index(-129f));
        }

        [Fact]
        public void PlacesInFogAreNotTold()
        {
            // Explored east of x = 0 only.
            var grid = Explored((x, z) => x >= 0f);
            var squares = PlaceSquares.Of(grid, new[] { ("Crypt2", 100f, 40f), ("Crypt2", -100f, 40f), ("Crypt2", -900f, -900f) });
            Assert.Equal(new[] { (0, 0, 1) }, squares.Select(s => (s.X, s.Z, s.Count)));
        }

        [Fact]
        public void TwoOfAKindInOneSquareAreCountedTogetherAndKindsApart()
        {
            var grid = Explored((x, z) => true);
            var squares = PlaceSquares.Of(grid, new[] { ("Runestone", 10f, 10f), ("Runestone", 100f, 90f), ("Crypt2", 20f, 20f), ("Runestone", 200f, 10f) });
            Assert.Equal(new[] { ("Runestone", 0, 0, 2), ("Crypt2", 0, 0, 1), ("Runestone", 1, 0, 1) },
                squares.Select(s => (s.Kind, s.X, s.Z, s.Count)));
        }

        [Fact]
        public void ASquareWhosePlacesWereAllSeenIsLeftOut()
        {
            var squares = new Dictionary<(int X, int Z), int> { [(0, 0)] = 2, [(1, 0)] = 1, [(-1, -1)] = 1 };
            // One of the two in (0, 0) seen, the one in (1, 0) seen, a spot out in (5, 5) seen as well.
            var seen = new[] { (100, 100), (200, 20), (2600, 2600) };
            var shown = PlaceSquares.Shown(squares, seen);
            Assert.Equal(new[] { (-1, -1, 1), (0, 0, 1) }, shown.OrderBy(s => s.X).Select(s => (s.X, s.Z, s.Left)));
            Assert.Equal(2, PlaceSquares.More(shown));
        }

        [Fact]
        public void TheSquaresOfAKindCombineYourGroundAndSharedMapsWithoutCountingOneTwice()
        {
            var counts = new PlaceCounts();
            counts.TookSquares(WorldSource.Explored, new[] { new PlaceSquare("Crypt2", 0, 0, 1), new PlaceSquare("Crypt2", 3, 1, 1) }, whole: true);
            // The ground explored since adds what lies in it.
            counts.TookSquares(WorldSource.Explored, new[] { new PlaceSquare("Crypt2", 4, 1, 1) }, whole: false);
            // A shared map showing the same crypt again, and one more.
            counts.TookSquares(WorldSource.SharedMaps, new[] { new PlaceSquare("Crypt2", 0, 0, 1), new PlaceSquare("Crypt2", -2, 5, 1) }, whole: true);
            var both = counts.Squares("Crypt2", source => true);
            Assert.Equal(new[] { (-2, 5, 1), (0, 0, 1), (3, 1, 1), (4, 1, 1) }, both.OrderBy(s => s.Key.X).Select(s => (s.Key.X, s.Key.Z, s.Value)));
            // A ground the book does not show gives none.
            var own = counts.Squares("Crypt2", source => source == WorldSource.Explored);
            Assert.Equal(3, own.Count);
            Assert.Empty(counts.Squares("TrollCave02", source => true));
        }

        [Fact]
        public void AWholeAnswerReplacesTheSquaresBeforeAndLeavingAWorldForgetsThem()
        {
            var counts = new PlaceCounts();
            counts.TookSquares(WorldSource.Explored, new[] { new PlaceSquare("Crypt2", 0, 0, 1) }, whole: true);
            counts.TookSquares(WorldSource.Explored, new[] { new PlaceSquare("Crypt2", 7, 7, 1) }, whole: true);
            Assert.Equal(new[] { (7, 7) }, counts.Squares("Crypt2", source => true).Keys);
            counts.Forget();
            Assert.Empty(counts.Squares("Crypt2", source => true));
            Assert.Equal(SquaresHeard.Waiting, counts.Heard(source => true));
        }

        [Fact]
        public void WhatTheServerSaidOfSquaresIsToldOnTheStrip()
        {
            var counts = new PlaceCounts();
            Assert.Equal(SquaresHeard.Waiting, counts.Heard(source => true));
            counts.SquaresNot(WorldSource.Explored, SquaresHeard.Off);
            Assert.Equal(SquaresHeard.Off, counts.Heard(source => true));
            // Squares told for a ground shown win over a ground the server turned away.
            counts.TookSquares(WorldSource.SharedMaps, new PlaceSquare[0], whole: true);
            Assert.Equal(SquaresHeard.Told, counts.Heard(source => true));
            Assert.Equal(SquaresHeard.Off, counts.Heard(source => source == WorldSource.Explored));
            Assert.Equal("no squares on this server", SpotWords.SquaresNote(SquaresHeard.Off));
            Assert.Equal("no squares: this server has no Scry", SpotWords.SquaresNote(SquaresHeard.NoScry));
            Assert.Equal("no squares: this server runs another version of Scry", SpotWords.SquaresNote(SquaresHeard.OtherScry));
            Assert.Equal("squares once the server has read your map", SpotWords.SquaresNote(SquaresHeard.Waiting));
            Assert.Null(SpotWords.SquaresNote(SquaresHeard.Told));
        }

        [Fact]
        public void AnAnswerThatNeverCameKeepsTheSquaresToldBefore()
        {
            var counts = new PlaceCounts();
            counts.TookSquares(WorldSource.Explored, new[] { new PlaceSquare("Crypt2", 0, 0, 1) }, whole: true);
            // An ask passed over at the server's pace, or a slow answer, is given up on.
            counts.NoAnswer(WorldSource.Explored);
            counts.NoAnswer(WorldSource.SharedMaps);
            Assert.Equal(new[] { (0, 0) }, counts.Squares("Crypt2", source => true).Keys);
            Assert.Equal(SquaresHeard.Told, counts.Heard(source => true));
            // With nothing told before, it says the server answers nothing.
            Assert.Equal(SquaresHeard.NoScry, counts.Heard(source => source == WorldSource.SharedMaps));
        }

        [Fact]
        public void TheStripCountsTheSquaresOnceAndNamesThemAloneWhereNoneWasSeen()
        {
            Assert.Equal("Troll cave · 4 you saw · nearest 730 m north-east · 7 more in squares",
                SpotWords.Strip("Troll cave", 4, 730f, "north-east", more: 7, note: null));
            Assert.Equal("Fuling village · 3 in squares, somewhere inside each",
                SpotWords.Strip("Fuling village", 0, null, null, more: 3, note: null));
            Assert.Equal("Fuling village · 1 in a square, somewhere inside it",
                SpotWords.Strip("Fuling village", 0, null, null, more: 1, note: null));
            Assert.Equal("Troll cave · 4 you saw · nearest 730 m north-east · no squares on this server",
                SpotWords.Strip("Troll cave", 4, 730f, "north-east", more: 0, note: SpotWords.SquaresNote(SquaresHeard.Off)));
            // A resource's strip, with no squares and nothing to say of them, is as before.
            Assert.Equal("Copper deposit · 13 you saw · nearest 140 m north-east",
                SpotWords.Strip("Copper deposit", 13, 140f, "north-east", more: 0, note: null));
        }

        [Fact]
        public void SquaresCrossTheWireByTheirKindsPlaceInTheAnswer()
        {
            var kinds = new List<string> { "Crypt2", "TrollCave02" };
            var squares = new List<PlaceSquare> { new PlaceSquare("TrollCave02", -3, 9, 2), new PlaceSquare("Crypt2", 0, 0, 1) };
            var back = PlaceSquares.Unpack(PlaceSquares.Pack(kinds, squares), kinds);
            Assert.Equal(SquaresHeard.Told, back.Heard);
            Assert.Equal(new[] { ("TrollCave02", -3, 9, 2), ("Crypt2", 0, 0, 1) }, back.Squares.Select(s => (s.Kind, s.X, s.Z, s.Count)));
        }

        [Fact]
        public void SquaresOnAnotherGridThanThisScrysAreReadAsNone()
        {
            var kinds = new List<string> { "Crypt2" };
            var packed = PlaceSquares.Pack(kinds, new List<PlaceSquare> { new PlaceSquare("Crypt2", 2, 2, 1) });
            // The grid's size is the answer's third to sixth bytes, after what it begins with.
            System.BitConverter.GetBytes(512).CopyTo(packed, 4);
            var back = PlaceSquares.Unpack(packed, kinds);
            Assert.Equal(SquaresHeard.OtherScry, back.Heard);
            Assert.Empty(back.Squares);
            // Nor a server saying it gives none in a shape without the grid: what it gives cannot be told.
            var shorter = new byte[8];
            packed.Take(4).ToArray().CopyTo(shorter, 0);
            System.BitConverter.GetBytes(-1).CopyTo(shorter, 4);
            Assert.Equal(SquaresHeard.OtherScry, PlaceSquares.Unpack(shorter, kinds).Heard);
        }

        [Fact]
        public void AServerGivingNoSquaresSaysSo()
        {
            var back = PlaceSquares.Unpack(PlaceSquares.PackOff(), new List<string>());
            Assert.Equal(SquaresHeard.Off, back.Heard);
            Assert.Empty(back.Squares);
        }

        [Fact]
        public void BrokenOrForeignSquaresAreReadAsNone()
        {
            var kinds = new List<string> { "Crypt2" };
            Assert.Equal((SquaresHeard.OtherScry, 0), (PlaceSquares.Unpack(null, kinds).Heard, PlaceSquares.Unpack(null, kinds).Squares.Count));
            Assert.Equal((SquaresHeard.OtherScry, 0), (PlaceSquares.Unpack(new byte[] { 1, 2, 3 }, kinds).Heard, PlaceSquares.Unpack(new byte[] { 1, 2, 3 }, kinds).Squares.Count));
            // A square naming a kind the answer does not hold is passed over, the rest kept.
            var packed = PlaceSquares.Pack(new List<string> { "Crypt2", "Other" }, new List<PlaceSquare> { new PlaceSquare("Other", 1, 1, 1), new PlaceSquare("Crypt2", 2, 2, 1) });
            Assert.Equal(new[] { "Crypt2" }, PlaceSquares.Unpack(packed, kinds).Squares.Select(s => s.Kind));
            Assert.Equal(SquaresHeard.Told, PlaceSquares.Unpack(packed, kinds).Heard);
            // Squares told longer or shorter than they say are none of this Scry's.
            Assert.Equal(SquaresHeard.OtherScry, PlaceSquares.Unpack(packed.Take(packed.Length - 4).ToArray(), kinds).Heard);
        }

        [Fact]
        public void ASquareStandsOnTheMapWhereItsCornersAre()
        {
            // The game's map: 2,048 pixels of 12 m; a window of a tenth of it around the middle, on a layer 1,000 by 1,000.
            var window = (0.45f, 0.45f, 0.1f, 0.1f);
            Assert.True(PlaceSquares.OnMap(0, 0, 12f, 2048, window, (1000f, 1000f), out var at));
            // 128 m is 10.67 pixels of 2,048, a tenth of the window's 204.8 pixels: 52.1 of the layer's 1,000.
            Assert.Equal(500f, at.X, 1);
            Assert.Equal(500f, at.Y, 1);
            Assert.Equal(52.1f, at.W, 1);
            Assert.Equal(52.1f, at.H, 1);
            // The window reaches 1,228.8 m either side. Partly in it a square still stands; wholly out of it, not.
            Assert.True(PlaceSquares.OnMap(9, 0, 12f, 2048, window, (1000f, 1000f), out _));
            Assert.False(PlaceSquares.OnMap(10, 0, 12f, 2048, window, (1000f, 1000f), out _));
            Assert.True(PlaceSquares.OnMap(-10, 0, 12f, 2048, window, (1000f, 1000f), out _));
            Assert.False(PlaceSquares.OnMap(-11, 0, 12f, 2048, window, (1000f, 1000f), out _));
        }

        [Fact]
        public void ASquaresFogMaskIsTheMapsOwnPixelsUnderIt()
        {
            var mask = PlaceSquares.MaskPixels(0, 0, 12f, 2048);
            // The middle of the map is pixel 1,024; 128 m is 10.67 pixels, so 12 cover it whole.
            Assert.Equal((1024, 1024, 12), (mask.FromX, mask.FromY, mask.Side));
            Assert.Equal(0f, mask.U, 3);
            Assert.Equal(10.667f / 12f, mask.Span, 3);
            var west = PlaceSquares.MaskPixels(-1, -1, 12f, 2048);
            // -128 m is pixel 1,013.33: from 1,013, a third of a pixel in.
            Assert.Equal((1013, 1013), (west.FromX, west.FromY));
            Assert.Equal(0.333f / 12f, west.U, 3);
        }

        [Fact]
        public void APlaceWithSquaresAloneCanBeShownWhereNoneWasSeen()
        {
            Assert.Equal(MapBlock.None, SeenOnMap.Block(false, 50f, true, false, count: 0, squares: 3));
            // Squares come from the server, not from what the character keeps: the map reach and recording do not stop them.
            Assert.Equal(MapBlock.None, SeenOnMap.Block(false, 0f, false, false, count: 0, squares: 1));
            Assert.Equal(MapBlock.NoMap, SeenOnMap.Block(true, 50f, true, false, count: 4, squares: 3));
            Assert.Equal(MapBlock.NoneSeen, SeenOnMap.Block(false, 50f, true, false, count: 0, squares: 0));
            Assert.Equal("Open Valheim's map with the 3 squares one of these stands somewhere in", SpotWords.Tip(0, 3));
            Assert.Equal("Open Valheim's map with a dot on each of the 2 your character saw in this world, and a square round each of 3 more", SpotWords.Tip(2, 3));
        }
    }
}
