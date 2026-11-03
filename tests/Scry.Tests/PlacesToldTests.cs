using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PlacesToldTests
    {
        // A server tells players the places lying in ground they explored or saw on a shared map
        // but never came near in one of three ways, its own setting (in single player, or hosting,
        // the player's): not at all, the 128 m square each stands in, or where each stands, as a
        // hollow dot. Only ground explored is ever told; a player's older Scry, or an older
        // server's, keeps to the squares.

        private const int MapSize = 200;
        private const float PixelSize = 12f;

        private static ExploredGrid Explored(System.Func<float, float, bool> at) =>
            ExploredGrid.FromMap(MapSize, PixelSize, i => at((i % MapSize - MapSize / 2) * PixelSize, (i / MapSize - MapSize / 2) * PixelSize));

        private static readonly (string Kind, float X, float Z)[] Places =
        {
            ("Crypt2", 100.4f, 200.6f), ("Crypt2", -300f, 40f), ("TrollCave02", 500f, 500f), ("Crypt2", 900f, 900f),
        };

        [Fact]
        public void OnDotsTheServerTellsWhereEachPlaceInExploredGroundStandsAndNothingInFog()
        {
            var grid = Explored((x, z) => x < 600f && z < 600f);
            var told = GroundTold.Worked(grid, Places, PlacesTold.Dots);
            Assert.Equal(new[] { ("Crypt2", 100, 201), ("Crypt2", -300, 40), ("TrollCave02", 500, 500) }, told.Spots);
            // The squares too, for a player whose older Scry reads only them.
            Assert.Equal(SquaresHeard.Told, told.Heard);
            Assert.Null(GroundTold.Worked(grid, Places, PlacesTold.Squares).Spots);
            Assert.Equal(SquaresHeard.Off, GroundTold.Worked(grid, Places, PlacesTold.Off).Heard);
        }

        [Fact]
        public void TheSpotsCrossTheWireAsTheyWereAndAPartOfAnotherMakeIsNone()
        {
            var kinds = new[] { "Crypt2", "TrollCave02" };
            var spots = new List<(string Kind, int X, int Z)> { ("Crypt2", 100, 201), ("TrollCave02", -5, 7) };
            Assert.Equal(spots, PlacesToldBy.UnpackSpots(PlacesToldBy.PackSpots(kinds, spots), kinds));
            Assert.Null(PlacesToldBy.UnpackSpots(PlacesToldBy.PackSpots(kinds, spots).Take(10).ToArray(), kinds));
            Assert.Null(PlacesToldBy.UnpackSpots(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, kinds));
            Assert.Null(PlacesToldBy.UnpackSpots(null, kinds));
        }

        [Fact]
        public void AGroundToldAsDotsShowsTheirSpotsAndNoSquares()
        {
            var counts = new PlaceCounts();
            counts.TookSquares(WorldSource.Explored, new[] { new PlaceSquare("Crypt2", 0, 1, 2) }, whole: true);
            counts.TookSquares(WorldSource.SharedMaps, new[] { new PlaceSquare("Crypt2", 5, 5, 1) }, whole: true);
            counts.TookSpots(WorldSource.Explored, new[] { ("Crypt2", 100, 201), ("Crypt2", 20, 30) }, whole: true);
            Assert.Equal(new[] { (100, 201), (20, 30) }, counts.Spots("Crypt2", _ => true));
            // The explored ground's squares give way to its spots; the shared maps', told as squares, stay.
            Assert.Equal(new[] { (5, 5) }, counts.Squares("Crypt2", _ => true).Keys);
            Assert.Empty(counts.Spots("Crypt2", s => s == WorldSource.SharedMaps));
            // Which grounds were told as dots, for an answer told otherwise to ask the ground whole again.
            Assert.True(counts.SpotsTold(WorldSource.Explored));
            Assert.False(counts.SpotsTold(WorldSource.SharedMaps));
            // An answer telling squares again takes the spots away.
            counts.SpotsNot(WorldSource.Explored);
            Assert.Empty(counts.Spots("Crypt2", _ => true));
            Assert.Equal(2, counts.Squares("Crypt2", _ => true).Count);
        }

        [Fact]
        public void APlaceToldThatIsMarkedAlreadyStandsNoSecondDot()
        {
            var told = new[] { (100, 200), (400, 400) };
            var marked = new[] { (103, 198) };
            Assert.Equal(new[] { (400, 400) }, PlacesToldBy.Besides(told, marked));
        }

        [Fact]
        public void TheWordsSayWhatTheServerToldApartFromWhatYouSaw()
        {
            Assert.Equal("Crypt · 2 you saw · nearest 40 m north · 3 more the server told of", SpotWords.Strip("Crypt", 2, 40f, "north", told: 3));
            Assert.Equal("Crypt · 3 the server told of", SpotWords.Strip("Crypt", 0, null, null, told: 3));
            Assert.Equal("2 you saw · 3 told", SpotWords.KeptCount(2, 0, 3));
            Assert.Equal("3 told", SpotWords.KeptCount(0, 0, 3));
            Assert.Equal("1 marked · 4 told", SpotWords.DungeonsCount(1, 0, 4));
            Assert.Equal("Crypt\nthe server says one stands here · 120 m north", SpotWords.ToldHover("Crypt", 120f, "north"));
            Assert.EndsWith(", and a hollow dot on each of 3 more the server told of", SpotWords.Tip(2, 0, 3));
            Assert.Equal("Open Valheim's map with a hollow dot on each of the 3 the server told of", SpotWords.Tip(0, 0, 3));
        }

        [Fact]
        public void AnOlderConfigsSquaresSwitchLeftOffReadsAsOff()
        {
            Assert.Equal(PlacesTold.Off, PlacesToldBy.FromOld(false, PlacesTold.Squares));
            Assert.Equal(PlacesTold.Squares, PlacesToldBy.FromOld(true, PlacesTold.Squares));
            Assert.Equal(PlacesTold.Squares, PlacesToldBy.FromOld(null, PlacesTold.Squares));
            // Once set anew, the old switch says nothing.
            Assert.Equal(PlacesTold.Dots, PlacesToldBy.FromOld(false, PlacesTold.Dots));
        }
    }
}
