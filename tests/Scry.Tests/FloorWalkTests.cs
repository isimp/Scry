using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class FloorWalkTests
    {
        // A place's floors are only where a person can walk to: from a room's doorways, or for a
        // location from its ground and anything under the open sky, climbing no more than a step
        // or a stair at a time, dropping any height, standing only with headroom. The tops of
        // pillars, walls and rock, and a deck with no way up, are none.

        private static readonly List<RayPart> Parts = new[] { "floor", "pillar", "roof", "stair", "rock", "ice" }.Select(n => new RayPart { Name = n }).ToList();
        private const int Floor = 0, Pillar = 1, Roof = 2, Stair = 3, Rock = 4, Ice = 5;
        private static readonly ISet<int> All = new HashSet<int>(Enumerable.Range(0, Parts.Count));

        private static RaySurface At(float height, int part, bool flat = true) => new RaySurface { Height = height, Flat = flat, Part = part };

        /// <summary>A place so many rays wide and deep, half a metre apart, each ray meeting what its column gives.</summary>
        private static FloorRecord Place(int wide, int deep, Func<int, int, RaySurface[]> column, bool room = false)
        {
            var record = FloorRecord.New("test", room, "stamp", 0f, (wide - 1) * 0.5f, 0f, (deep - 1) * 0.5f, 20f, -20f, Parts);
            var seen = new ProbeSeen();
            seen.Present.UnionWith(All);
            var grid = record.Grid;
            for (var r = 0; r < grid.Count; r++)
            {
                var surfaces = column(grid[r].I, grid[r].J);
                if (surfaces.Length > 0) seen.Rays[r] = surfaces.ToList();
            }
            return record.With(seen);
        }

        private static bool Has(IEnumerable<FloorLanding> landings, float height) => landings.Any(l => Math.Abs(l.Height - height) < 0.01f);

        private static List<float> Floors(FloorRecord record, IEnumerable<FloorLanding> landings) =>
            FloorFinder.Floors(landings.Select(l => new FloorHit { I = l.I, J = l.J, Height = l.Height, Area = record.Cell, Open = l.Open }).ToList(), record.Ground);

        [Fact]
        public void APillarsTopNoOneCanClimbIsNoFloor()
        {
            // A ruin under an arch: a pillar three metres across, three metres high, on its ground.
            var ruin = Place(20, 20, (i, j) => i >= 7 && i < 13 && j >= 7 && j < 13
                ? new[] { At(6f, Roof), At(3f, Pillar), At(0f, Floor) }
                : new[] { At(6f, Roof), At(0f, Floor) });

            Assert.Contains(3f, Floors(ruin, ruin.Landings(All)));
            var walked = FloorWalk.Landings(ruin, All, FloorWalk.Start.Outdoors);
            Assert.False(Has(walked, 3f));
            Assert.True(Has(walked, 0f));
            Assert.Equal(new[] { 0f }, Floors(ruin, walked));
        }

        [Fact]
        public void StairsLeadUpToTheFloorAbove()
        {
            // Ground, a stair of half-metre steps, and a deck at 3 m under a roof.
            var house = Place(20, 20, (i, j) =>
                i >= 12 ? new[] { At(6f, Roof), At(3f, Floor), At(0f, Floor) }
                : i >= 6 ? new[] { At(6f, Roof), At((i - 5) * 0.5f, Stair), At(0f, Floor) }
                : new[] { At(6f, Roof), At(0f, Floor) });

            var walked = FloorWalk.Landings(house, All, FloorWalk.Start.Outdoors);
            Assert.True(Has(walked, 3f));
            Assert.Equal(new[] { 3f, 0f }, Floors(house, walked));
        }

        [Fact]
        public void ADeckWithNoWayUpIsNoFloor()
        {
            // The deck as before with its stair gone: nothing leads up to it.
            var house = Place(20, 20, (i, j) =>
                i >= 12 ? new[] { At(6f, Roof), At(3f, Floor), At(0f, Floor) } : new[] { At(6f, Roof), At(0f, Floor) });

            Assert.False(Has(FloorWalk.Landings(house, All, FloorWalk.Start.Outdoors), 3f));
        }

        [Fact]
        public void GroundUnderTheOpenSkyIsReachedFromOutdoors()
        {
            // A hut on stilts, its open porch at 3 m before its roofed room at the same height.
            var hut = Place(20, 20, (i, j) =>
                i >= 12 ? new[] { At(6f, Roof), At(3f, Floor) }
                : i >= 8 ? new[] { At(3f, Floor) }
                : Array.Empty<RaySurface>());

            var walked = FloorWalk.Landings(hut, All, FloorWalk.Start.Outdoors);
            Assert.Contains(walked, l => l.I >= 12 && Math.Abs(l.Height - 3f) < 0.01f);
        }

        [Fact]
        public void ARoomIsWalkedFromItsDoorways()
        {
            // A cave room walked into at its west edge, its floor at 0 m, the top of its rock at
            // 6 m over its east half, open as nothing of the room is over it.
            var cave = Place(20, 20, (i, j) => i >= 10 ? new[] { At(6f, Rock), At(0f, Floor) } : new[] { At(0f, Floor) }, room: true);
            var door = FloorWalk.Start.At(new[] { new Vec3(0f, 0f, 4.75f) });

            var walked = FloorWalk.Landings(cave, All, door);
            Assert.True(Has(walked, 0f));
            Assert.False(Has(walked, 6f));

            // However wide its rock's top, its main floor is where it is walked.
            var patch = FloorFinder.Patch(walked.Select(l => new FloorHit { I = l.I, J = l.J, Height = l.Height, Area = cave.Cell }));
            Assert.Equal(0f, FloorFinder.MainFloor(patch).Value, 2);
        }

        [Fact]
        public void ADoorwayHighInAWallDropsToTheGroundUnderIt()
        {
            // The Hole's corridors are walked into 2.4 m over their floor, a frost cave's deep room
            // through its roof 9 m over its floor: one lands on the first ground under the doorway.
            var corridor = Place(20, 20, (i, j) => new[] { At(5f, Rock, flat: false), At(-2.4f, Floor) }, room: true);
            Assert.True(Has(FloorWalk.Landings(corridor, All, FloorWalk.Start.At(new[] { new Vec3(0f, 0f, 4.75f) })), -2.4f));
            var deep = Place(20, 20, (i, j) => new[] { At(-9f, Floor) }, room: true);
            Assert.True(Has(FloorWalk.Landings(deep, All, FloorWalk.Start.At(new[] { new Vec3(4.75f, 0f, 4.75f) })), -9f));
            // One lands on the first ground under a doorway, not on what lies under that: a deck
            // over a cellar no stair leads down to.
            var decked = Place(20, 20, (i, j) => new[] { At(0f, Floor), At(-5f, Floor) }, room: true);
            var onDeck = FloorWalk.Landings(decked, All, FloorWalk.Start.At(new[] { new Vec3(0f, 0f, 4.75f) }));
            Assert.True(Has(onDeck, 0f));
            Assert.False(Has(onDeck, -5f));
            // A doorway nothing is under leads nowhere.
            Assert.Empty(FloorWalk.Landings(Place(4, 4, (i, j) => Array.Empty<RaySurface>(), room: true), All, FloorWalk.Start.At(new[] { new Vec3(0f, 0f, 0f) })));
        }

        [Fact]
        public void ABigRoomsDoorwayFindsItsGroundOnAWideGrid()
        {
            // Morkhalla's rooms are 60 m across, their rays two metres apart, their doorways at
            // their edge between two rays: the ground by a doorway is found within two rays of it.
            var record = FloorRecord.New("hall", true, "stamp", 0f, 78f, 0f, 78f, 20f, -20f, Parts);
            var seen = new ProbeSeen();
            seen.Present.UnionWith(All);
            for (var r = 0; r < record.Grid.Count; r++) seen.Rays[r] = new List<RaySurface> { At(-9.5f, Floor) };
            var hall = record.With(seen);
            Assert.True(Math.Sqrt(hall.Cell) > 1.9);
            Assert.True(Has(FloorWalk.Landings(hall, All, FloorWalk.Start.At(new[] { new Vec3(-1.8f, -10f, 39f) })), -9.5f));
        }

        [Fact]
        public void OnAWideGridAStairClimbsAsMuchAsItsRaysLieApart()
        {
            // A big place's rays two metres apart: a stair to a deck at 6 m rises one and a half
            // metres from one ray to the next, as a stair does over two metres.
            var record = FloorRecord.New("keep", false, "stamp", 0f, 78f, 0f, 78f, 20f, -20f, Parts);
            var seen = new ProbeSeen();
            seen.Present.UnionWith(All);
            var grid = record.Grid;
            for (var r = 0; r < grid.Count; r++)
            {
                var i = grid[r].I;
                seen.Rays[r] = i >= 24 ? new List<RaySurface> { At(9f, Roof), At(6f, Floor), At(0f, Floor) }
                    : i >= 20 ? new List<RaySurface> { At(9f, Roof), At((i - 19) * 1.5f, Stair), At(0f, Floor) }
                    : new List<RaySurface> { At(9f, Roof), At(0f, Floor) };
            }
            var keep = record.With(seen);
            Assert.True(Has(FloorWalk.Landings(keep, All, FloorWalk.Start.Outdoors), 6f));
        }

        [Fact]
        public void WhatLiesUnderWalkedGroundIsPartOfIt()
        {
            // Ice half a metre over a cave's rock floor over half the room: the rock under the ice
            // is floor as much as the rock beside it.
            var cave = Place(20, 20, (i, j) => i < 10 ? new[] { At(-3.6f, Ice), At(-4.1f, Rock) } : new[] { At(-4.1f, Rock) }, room: true);
            var walked = FloorWalk.Landings(cave, All, FloorWalk.Start.At(new[] { new Vec3(9.5f, -4f, 4.75f) }));
            Assert.Equal(400, walked.Count(l => Math.Abs(l.Height + 4.1f) < 0.01f));
            Assert.True(Has(walked, -3.6f));
        }

        [Fact]
        public void OneStandsOnlyWithHeadroomAndClimbsOnlyAStep()
        {
            // A room cut in two by a crawlway a metre high: what lies past it is out of reach, as
            // is a ledge a metre up with nothing to climb it by.
            var crawl = Place(20, 20, (i, j) => i >= 6 && i < 10 ? new[] { At(1f, Rock), At(0f, Floor) } : new[] { At(0f, Floor) }, room: true);
            var walked = FloorWalk.Landings(crawl, All, FloorWalk.Start.At(new[] { new Vec3(0f, 0f, 4.75f) }));
            Assert.Contains(walked, l => l.I < 6);
            Assert.DoesNotContain(walked, l => l.I >= 10);
        }
    }
}
