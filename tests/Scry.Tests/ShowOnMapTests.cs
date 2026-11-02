using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class ShowOnMapTests
    {
        // A page's Show on map opens Valheim's large map with a dot on every spot of its kind the
        // character saw in the world played, a strip naming the kind, how many and the nearest,
        // until Hide or the map closes.

        private const string World = "Valhalla-123", Other = "Ashland-9";

        private static readonly IReadOnlyList<(int X, int Z)> Three = new[] { (100, 100), (140, 100), (-300, 40) };

        [Fact]
        public void TheStripNamesTheKindHowManyYouSawAndTheNearest()
        {
            Assert.Equal("Copper deposit · 13 you saw · nearest 140 m north-east", SpotWords.Strip("Copper deposit", 13, 140f, "north-east"));
            Assert.Equal("Fuling village · 1 you saw · nearest 2.3 km south", SpotWords.Strip("Fuling village", 1, 2340f, "south"));
            // Close by, to the metre; far, in tens of metres; past a kilometre, in kilometres.
            Assert.Equal("37 m", SpotWords.Distance(37.4f));
            Assert.Equal("140 m", SpotWords.Distance(143f));
            Assert.Equal("1.2 km", SpotWords.Distance(1249f));
            // One where you stand is beside you.
            Assert.Equal("Tin deposit · 2 you saw · nearest beside you", SpotWords.Strip("Tin deposit", 2, 3f, "east"));
        }

        [Fact]
        public void TheNearestIsGivenByTheMapsCompassNorthUp()
        {
            // The map shows north up: z grows to the north, x to the east.
            Assert.Equal("north", SpotWords.Direction(0f, 50f));
            Assert.Equal("north-east", SpotWords.Direction(40f, 40f));
            Assert.Equal("east", SpotWords.Direction(50f, 3f));
            Assert.Equal("south-east", SpotWords.Direction(30f, -30f));
            Assert.Equal("south", SpotWords.Direction(-2f, -50f));
            Assert.Equal("south-west", SpotWords.Direction(-30f, -30f));
            Assert.Equal("west", SpotWords.Direction(-50f, 0f));
            Assert.Equal("north-west", SpotWords.Direction(-30f, 30f));
        }

        [Fact]
        public void TheNearestSpotIsFoundFromWhereYouStand()
        {
            var (distance, dx, dz) = SeenOnMap.Nearest(Three, 130f, 90f).Value;
            Assert.Equal(System.Math.Sqrt(200.0), distance, 3);
            Assert.Equal(10f, dx);
            Assert.Equal(10f, dz);
            Assert.Null(SeenOnMap.Nearest(new (int, int)[0], 0f, 0f));
        }

        [Fact]
        public void TheButtonShowsWhereSomethingWasSeenAndSaysWhyNotOtherwise()
        {
            Assert.Equal(MapBlock.None, SeenOnMap.Block(noMap: false, reach: 50f, recording: true, unreadable: false, count: 13));
            // Spots kept before recording was switched off still show.
            Assert.Equal(MapBlock.None, SeenOnMap.Block(false, 50f, false, false, 13));
            Assert.Equal(MapBlock.NoneSeen, SeenOnMap.Block(false, 50f, true, false, 0));
            Assert.Equal(MapBlock.RecordingOff, SeenOnMap.Block(false, 50f, false, false, 0));
            Assert.Equal(MapBlock.Unreadable, SeenOnMap.Block(false, 50f, true, true, 0));
            // A world with no map, or whose server keeps no spots, shows none at all.
            Assert.Equal(MapBlock.NoMap, SeenOnMap.Block(true, 50f, true, false, 13));
            Assert.Equal(MapBlock.NoReach, SeenOnMap.Block(false, 0f, true, false, 13));
            foreach (MapBlock block in System.Enum.GetValues(typeof(MapBlock)))
                if (block != MapBlock.None) Assert.False(string.IsNullOrEmpty(SpotWords.WhyNot(block)));
            Assert.Equal("Your character has not seen this in this world since Scry began keeping where things stand.", SpotWords.WhyNot(MapBlock.NoneSeen));
            Assert.Equal("Open Valheim's map with a dot on each of the 13 your character saw in this world", SpotWords.Tip(13));
            Assert.Equal("Open Valheim's map with a dot where your character saw it in this world", SpotWords.Tip(1));
        }

        [Fact]
        public void TheYouBoxSaysHowManyWereSeenInThisWorld()
        {
            Assert.Equal("Where you saw it", SpotWords.YouLabel);
            Assert.Equal("13 in this world", SpotWords.YouLine(13));
            Assert.Equal("1 in this world", SpotWords.YouLine(1));
        }

        [Fact]
        public void ShownSpotsStayUntilTheMapOpenedForThemCloses()
        {
            var showing = new MapShowing();
            showing.Show(World, "rock4_copper", "Copper deposit", Three);
            Assert.True(showing.Showing);
            // The map opens a frame or so after the button: not yet closed.
            showing.Tick(largeOpen: false, World);
            Assert.True(showing.Showing);
            showing.Tick(true, World);
            showing.Tick(true, World);
            Assert.Equal(3, showing.Spots.Count);
            Assert.Equal("Copper deposit", showing.Name);
            // Closed: gone, so the next time the map opens it shows as usual.
            showing.Tick(false, World);
            Assert.False(showing.Showing);
            showing.Tick(true, World);
            Assert.False(showing.Showing);
        }

        [Fact]
        public void HideTakesTheDotsAwayAndAnotherKindTakesTheirPlace()
        {
            var showing = new MapShowing();
            showing.Show(World, "rock4_copper", "Copper deposit", Three);
            showing.Tick(true, World);
            showing.Show(World, "MineRock_Tin", "Tin deposit", new[] { (1, 2) });
            Assert.Equal("MineRock_Tin", showing.Kind);
            Assert.Single(showing.Spots);
            showing.Hide();
            Assert.False(showing.Showing);
            Assert.Empty(showing.Spots);
        }

        [Fact]
        public void SpotsAskedForAMapThatNeverOpensAreDropped()
        {
            // As in a world whose map the game keeps shut: nothing lies in wait for the next M.
            var showing = new MapShowing();
            showing.Show(World, "rock4_copper", "Copper deposit", Three);
            for (var i = 0; i < MapShowing.WaitTicks; i++) showing.Tick(false, World);
            Assert.False(showing.Showing);
        }

        [Fact]
        public void SpotsOfOneWorldAreNotShownInAnother()
        {
            var showing = new MapShowing();
            showing.Show(World, "rock4_copper", "Copper deposit", Three);
            showing.Tick(true, Other);
            Assert.False(showing.Showing);
        }

        [Fact]
        public void ADotShrinksAsTheMapZoomsOutButStaysVisible()
        {
            // The game's large map zooms from 0.01 (nearest) to 1 (the whole world).
            var near = SeenOnMap.DotSize(32f, 0.01f);
            var middle = SeenOnMap.DotSize(32f, 0.1f);
            var far = SeenOnMap.DotSize(32f, 1f);
            Assert.True(near > middle && middle > far);
            Assert.True(near < 32f, "a dot is smaller than a pin");
            Assert.True(far >= 6f, "and never too small to see");
            Assert.Equal(far, SeenOnMap.DotSize(32f, 5f));
            Assert.Equal(near, SeenOnMap.DotSize(32f, 0.001f));
        }

        [Fact]
        public void HowManyOfAKindWereSeenIsCountedWithoutListingThem()
        {
            var spots = new SeenSpots();
            spots.Saw(World, "Tin", 1f, 2f);
            spots.Saw(World, "Tin", 100f, 2f);
            spots.Saw(World, "Tin", 1000f, 2f);
            spots.Saw(World, "Copper", 1f, 2f);
            spots.Saw(Other, "Tin", 1f, 2f);
            Assert.Equal(3, spots.CountOf(World, "Tin"));
            Assert.Equal(0, spots.CountOf(World, "Silver"));
            Assert.Equal(0, spots.CountOf(null, "Tin"));
        }
    }
}
