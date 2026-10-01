using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class FloorFinderTests
    {
        // A place's floors come from the place itself: rays cast straight down over it, on a grid,
        // land on what is flat (floors, landings, platforms, but also tables, beds and beams).
        // Only ground one can stand on makes a floor: a spot counts where the spots beside it,
        // all four ways, are at the same height too, and those spots must add up to enough room.
        // So a beam, a table or a bed is none; a loft or a storey is one. Heights closer than
        // 2 m are one floor, the one with the most room standing for them.

        private const float Cell = 0.25f;

        /// <summary>A flat patch of cells, so many wide and deep, at a height.</summary>
        private static IEnumerable<FloorHit> Patch(int i0, int j0, int wide, int deep, float height, int patch = 0) =>
            from i in Enumerable.Range(i0, wide) from j in Enumerable.Range(j0, deep) select new FloorHit { Patch = patch, I = i, J = j, Height = height, Area = Cell };

        private static List<float> Floors(IEnumerable<FloorHit> hits, int raysPerSide = 20) =>
            FloorFinder.Floors(hits.ToList(), raysPerSide * raysPerSide * Cell);

        [Fact]
        public void ATowersStoreysAreItsFloorsFromTheTopDown()
        {
            var floors = Floors(Patch(0, 0, 20, 20, 0.1f).Concat(Patch(0, 0, 20, 20, 4.2f)).Concat(Patch(0, 0, 20, 20, 8.3f)));

            Assert.Equal(3, floors.Count);
            Assert.Equal(8.3f, floors[0], 2);
            Assert.Equal(4.2f, floors[1], 2);
            Assert.Equal(0.1f, floors[2], 2);
        }

        [Fact]
        public void ABeamOneCannotStandOnIsNoFloor()
        {
            // A beam a cell wide runs the whole cabin at 2.4 m: nothing beside it is at its height.
            Assert.Equal(new[] { 0f }, Floors(Patch(0, 0, 20, 20, 0f).Concat(Patch(0, 5, 20, 1, 2.4f)).Concat(Patch(0, 12, 20, 1, 2.4f))));
        }

        [Fact]
        public void ATableOrABedIsNoFloor()
        {
            // A bed of 2 by 2.5 m and a table on a shelf, both above 2 m from the floor: too little room.
            var floors = Floors(Patch(0, 0, 20, 20, 0f).Concat(Patch(2, 2, 4, 5, 2.6f)).Concat(Patch(12, 12, 4, 2, 3.4f)));
            Assert.Equal(new[] { 0f }, floors);

            // A bed with its blanket lying on it: two flat layers over the same spots still hold only the bed's room.
            Assert.Equal(new[] { 0f }, Floors(Patch(0, 0, 20, 20, 0f).Concat(Patch(2, 2, 4, 5, 2.6f)).Concat(Patch(2, 2, 4, 5, 2.7f))));
        }

        [Fact]
        public void ALoftIsAFloor()
        {
            var floors = Floors(Patch(0, 0, 20, 20, 0f).Concat(Patch(0, 0, 8, 8, 2.6f)));
            Assert.Equal(new[] { 2.6f, 0f }, floors);
        }

        [Fact]
        public void InABigPlaceASmallPlatformIsNoFloor()
        {
            // Room enough to stand on, but a sliver of a big place: 1.5% of its ground at the least.
            var hits = Patch(0, 0, 80, 80, 0f).Concat(Patch(0, 0, 8, 8, 5f)).ToList();
            Assert.Equal(new[] { 0f }, FloorFinder.Floors(hits, 80 * 80 * Cell));
            Assert.Equal(new[] { 5f, 0f }, FloorFinder.Floors(hits.Concat(Patch(10, 10, 12, 12, 5f)).ToList(), 80 * 80 * Cell));
        }

        [Fact]
        public void TheFloorWithTheMostRoomStandsForThoseNearIt()
        {
            // A floor at 3 m with a raised dais at 3.5 m: one floor, at 3 m.
            var floors = Floors(Patch(0, 0, 20, 12, 3f).Concat(Patch(0, 12, 20, 8, 3.5f)).Concat(Patch(0, 0, 20, 20, 0f)));
            Assert.Equal(2, floors.Count);
            Assert.Equal(3f, floors[0], 1);
            Assert.Equal(0f, floors[1], 2);
        }

        [Fact]
        public void AFloorIsWhereItsRaysLandOnAverage()
        {
            var floors = Floors(Patch(0, 0, 20, 10, 4.05f).Concat(Patch(0, 10, 20, 10, 4.15f)));
            Assert.Equal(4.1f, Assert.Single(floors), 2);
        }

        [Fact]
        public void SpotsOfDifferentRoomsAreNotNeighbours()
        {
            // Two rooms' grids both start at 0: a strip of each side by side is still a strip.
            var hits = Patch(0, 0, 20, 20, 0f).Concat(Patch(0, 0, 20, 1, 3f, patch: 1)).Concat(Patch(0, 1, 20, 1, 3f, patch: 2)).Concat(Patch(0, 2, 20, 1, 3f, patch: 3));
            Assert.Equal(new[] { 0f }, Floors(hits));

            // A floor of a later room is a floor all the same.
            Assert.Equal(new[] { 0f }, Floors(Patch(0, 0, 20, 20, 0f, patch: 4)));
        }

        [Fact]
        public void AFloorLyingAcrossTwoBandsCountsWholeThoughItsRoomsAreReadOneByOne()
        {
            // Two rooms side by side, one floor at 3 m, the other at 3.25 m: one floor between them,
            // whether all their rays are looked at together or each room is read as it comes in.
            var first = Patch(0, 0, 20, 20, 3f, patch: 1).ToList();
            var second = Patch(0, 0, 20, 20, 3.25f, patch: 2).ToList();

            Assert.Equal(3.125f, Assert.Single(Floors(first.Concat(second))), 3);
            var rooms = new[] { FloorFinder.Patch(first), FloorFinder.Patch(second) };
            Assert.Equal(3.125f, Assert.Single(FloorFinder.Floors(rooms, 20 * 20 * Cell)), 3);
        }

        [Fact]
        public void HeightsNoRayLandedAtAreNoFloorOfTheirOwn()
        {
            // Half a room at 2.5 m, the other half at 3 m, nothing between: the floor is at 3 m,
            // not halfway, where both halves would count together as one wide floor.
            var hits = Patch(0, 0, 20, 10, 2.5f).Concat(Patch(0, 10, 20, 10, 3f)).ToList();

            Assert.Equal(new[] { 3f }, Floors(hits));
            Assert.Equal(new[] { 3f }, FloorFinder.Floors(new[] { FloorFinder.Patch(hits) }, 20 * 20 * Cell));
        }

        [Fact]
        public void AnExamplesRoomsReadOneByOneGiveItsFloors()
        {
            // A storey of rooms at 0 m and one of rooms at 6 m, read as they come in, with a table in one.
            var rooms = new List<FloorPatch>();
            for (var room = 0; room < 6; room++) rooms.Add(FloorFinder.Patch(Patch(0, 0, 12, 12, room < 4 ? 0f : 6f, patch: room)));
            rooms.Add(FloorFinder.Patch(Patch(0, 0, 12, 12, 0f, patch: 6).Concat(Patch(3, 3, 3, 2, 1.1f, patch: 6))));

            Assert.Equal(new[] { 6f, 0f }, FloorFinder.Floors(rooms, 7 * 12 * 12 * Cell));
            Assert.Empty(FloorFinder.Floors(new List<FloorPatch>(), 100f));

            // Small rooms, too little room each to stand for a floor, make one together.
            var small = Enumerable.Range(0, 10).Select(room => FloorFinder.Patch(Patch(0, 0, 4, 4, 2f, patch: room))).ToList();
            Assert.Empty(FloorFinder.Floors(small.Take(1), 4 * 4 * Cell));
            Assert.Equal(new[] { 2f }, FloorFinder.Floors(small, 10 * 4 * 4 * Cell));
        }

        [Fact]
        public void AFloorSteppingDownRoomByRoomIsOneFloorWhereMostOfItIs()
        {
            // Three rooms, each a quarter of a metre below the last: one floor, at the middle one.
            var rooms = new[] { 3f, 2.75f, 2.5f }.Select((height, room) => Patch(0, 0, 20, 20, height, patch: room).ToList()).ToList();

            Assert.Equal(2.75f, Assert.Single(Floors(rooms.SelectMany(r => r))), 3);
            Assert.Equal(2.75f, Assert.Single(FloorFinder.Floors(rooms.Select(r => FloorFinder.Patch(r)), 3 * 20 * 20 * Cell)), 3);
        }

        [Fact]
        public void NothingHitIsNoFloor()
        {
            Assert.Empty(FloorFinder.Floors(new List<FloorHit>(), 100f));
            Assert.Empty(FloorFinder.Floors(new List<FloorHit>(), 0f));
        }

        [Fact]
        public void RaysAreCastEveryHalfMetreAtMostFortyToASide()
        {
            Assert.Equal(17 * 9, FloorFinder.Grid(0f, 8f, 0f, 4f).Count);
            var big = FloorFinder.Grid(-100f, 100f, -50f, 50f);
            Assert.Equal(40 * 21, big.Count);
            Assert.Contains(big, p => p.X == -100f && p.Z == -50f && p.I == 0 && p.J == 0);
            Assert.Contains(big, p => p.X == 100f && p.Z == 50f && p.I == 39 && p.J == 20);
        }
    }
}
