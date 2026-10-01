using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class FloorFinderTests
    {
        // A place's floors come from the place itself: rays cast straight down over it land on
        // whatever faces up (floors, landings, platforms, but also tables and crates). Where many
        // land at one height there is a floor; a few on a table are none. Heights closer than
        // 2 m are one floor, a step or a slope, the one most rays landed on standing for it.

        private static List<float> Hits(params (float Height, int Count)[] heights) =>
            heights.SelectMany(h => Enumerable.Repeat(h.Height, h.Count)).ToList();

        [Fact]
        public void ATowersStoreysAreItsFloorsFromTheTopDown()
        {
            var hits = Hits((0.1f, 400), (4.2f, 300), (8.3f, 300), (1.0f, 5), (5.1f, 4));
            var floors = FloorFinder.Floors(hits, 400);

            Assert.Equal(3, floors.Count);
            Assert.Equal(8.3f, floors[0], 2);
            Assert.Equal(4.2f, floors[1], 2);
            Assert.Equal(0.1f, floors[2], 2);
        }

        [Fact]
        public void AFewRaysOnATableMakeNoFloor()
        {
            var floors = FloorFinder.Floors(Hits((0f, 400), (2.2f, 5)), 400);
            Assert.Equal(new[] { 0f }, floors);
        }

        [Fact]
        public void AFloorTooSmallForItsShareOfTheRaysIsNone()
        {
            // 1.5% of the rays at the least: a balcony of 5 rays in 400 is furniture.
            Assert.Single(FloorFinder.Floors(Hits((0f, 400), (3f, 5)), 400));
            Assert.Equal(2, FloorFinder.Floors(Hits((0f, 400), (3f, 7)), 400).Count);

            // Over a big place the share counts: 20 rays of 2000 are too few.
            Assert.Single(FloorFinder.Floors(Hits((0f, 2000), (3f, 20)), 2000));
            Assert.Equal(2, FloorFinder.Floors(Hits((0f, 2000), (3f, 30)), 2000).Count);

            // Over a small one, six rays at the least: 4 of 100 are too few.
            Assert.Single(FloorFinder.Floors(Hits((0f, 100), (3f, 4)), 100));
            Assert.Equal(2, FloorFinder.Floors(Hits((0f, 100), (3f, 6)), 100).Count);
        }

        [Fact]
        public void ASlopeOrStairsGiveAFloorEveryTwoMetresAtMost()
        {
            var hits = new List<float>();
            for (var h = 0f; h <= 6.01f; h += 0.25f) hits.AddRange(Enumerable.Repeat(h, 20));
            var floors = FloorFinder.Floors(hits, 400);

            Assert.True(floors.Count >= 3, string.Join(", ", floors));
            for (var i = 1; i < floors.Count; i++) Assert.True(floors[i - 1] - floors[i] >= PlaceView.SameFloor - 1e-3, string.Join(", ", floors));
        }

        [Fact]
        public void TheFloorMostRaysLandOnStandsForThoseNearIt()
        {
            // A floor at 3 m with a raised dais at 3.5 m: one floor, at 3 m.
            var floors = FloorFinder.Floors(Hits((3f, 200), (3.5f, 60), (0f, 300)), 400);
            Assert.Equal(new[] { 3f, 0f }, floors);
        }

        [Fact]
        public void AFloorIsWhereItsRaysLandOnAverage()
        {
            var floors = FloorFinder.Floors(Hits((4.05f, 100), (4.15f, 100)), 300);
            Assert.Equal(4.1f, Assert.Single(floors), 3);
        }

        [Fact]
        public void NothingHitIsNoFloor()
        {
            Assert.Empty(FloorFinder.Floors(new List<float>(), 400));
            Assert.Empty(FloorFinder.Floors(new List<float>(), 0));
        }

        [Fact]
        public void RaysAreCastEveryHalfMetreAtMostFortyToASide()
        {
            Assert.Equal(17 * 9, FloorFinder.Grid(0f, 8f, 0f, 4f).Count);
            var big = FloorFinder.Grid(-100f, 100f, -50f, 50f);
            Assert.Equal(40 * 21, big.Count);
            Assert.Contains(big, p => p.X == -100f && p.Z == -50f);
            Assert.Contains(big, p => p.X == 100f && p.Z == 50f);
        }
    }
}
