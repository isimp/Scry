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

        /// <summary>A flat patch of cells, so many wide and deep, at a height; open with nothing of the place above it.</summary>
        private static IEnumerable<FloorHit> Patch(int i0, int j0, int wide, int deep, float height, int patch = 0, bool open = false) =>
            from i in Enumerable.Range(i0, wide) from j in Enumerable.Range(j0, deep) select new FloorHit { Patch = patch, I = i, J = j, Height = height, Area = Cell, Open = open };

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
        public void WhatHasNothingAboveItIsNoFloorToCutTo()
        {
            // A flat roof's top, a cave's rock over its hollow, a tower's open deck: seen with the
            // roof on, so no floor to cut to. The rooms under it are.
            var floors = Floors(Patch(0, 0, 20, 20, 9f, open: true).Concat(Patch(0, 0, 20, 20, 4.2f)).Concat(Patch(0, 0, 20, 20, 0.1f)));
            Assert.Equal(2, floors.Count);
            Assert.Equal(4.2f, floors[0], 2);
            Assert.Equal(0.1f, floors[1], 2);

            // A place open to the sky all over has none.
            Assert.Empty(Floors(Patch(0, 0, 20, 20, 0f, open: true)));
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
        public void EachRoomsOwnMainFloorIsAFloorHoweverBigTheExample()
        {
            // Six big halls at 0 m and a small chamber far below: the chamber's floor is a sliver of
            // all the example's ground, but the ground of its own room, so a floor all the same.
            var rooms = Enumerable.Range(0, 6).Select(room => FloorFinder.Patch(Patch(0, 0, 40, 40, 0f, patch: room))).ToList();
            var chamber = Patch(0, 0, 10, 10, -40f, patch: 6).ToList();
            rooms.Add(FloorFinder.Patch(chamber));
            var footprint = 6 * 40 * 40 * Cell + 10 * 10 * Cell;
            Assert.Equal(new[] { 0f, -40f }, FloorFinder.Floors(rooms, footprint, PlaceView.Storey));

            // A ledge in it, room enough to stand on but not its main ground, is still no floor.
            rooms[6] = FloorFinder.Patch(chamber.Concat(Patch(20, 20, 6, 6, -36f, patch: 6)));
            Assert.Equal(new[] { 0f, -40f }, FloorFinder.Floors(rooms, footprint, PlaceView.Storey));

            // Nor is a room's main ground with too little room to stand on.
            var cupboard = FloorFinder.Patch(Patch(0, 0, 4, 4, -60f, patch: 7));
            Assert.Equal(new[] { 0f, -40f }, FloorFinder.Floors(rooms.Concat(new[] { cupboard }), footprint + 4 * 4 * Cell, PlaceView.Storey));
        }

        [Fact]
        public void EachRoomsLevelsAreItsFloorsAsWhenItIsShownAlone()
        {
            // Six big halls at 0 m, and a hall below with a gallery half way up it: the gallery is
            // a sliver of all the example's ground but a level of its own room, as it is when that
            // room is shown alone, so a floor. A ledge too small for its own room is none.
            var rooms = Enumerable.Range(0, 6).Select(room => FloorFinder.Patch(Patch(0, 0, 40, 40, 0f, patch: room))).ToList();
            var hall = FloorFinder.Patch(Patch(0, 0, 20, 20, -40f, patch: 6).Concat(Patch(0, 0, 20, 4, -34f, patch: 6)).Concat(Patch(30, 30, 3, 3, -30f, patch: 6)));
            hall.Ground = 20 * 20 * Cell;
            rooms.Add(hall);
            var footprint = 6 * 40 * 40 * Cell + hall.Ground;
            Assert.Equal(new[] { 0f, -34f, -40f }, FloorFinder.Floors(rooms, footprint, PlaceView.Storey));
            Assert.Equal(FloorFinder.Floors(new[] { hall }, hall.Ground, PlaceView.Storey), new[] { -34f, -40f });

            // With its ground not known, only its main floor stands for it.
            hall.Ground = 0f;
            Assert.Equal(new[] { 0f, -40f }, FloorFinder.Floors(rooms, footprint, PlaceView.Storey));

            // A level must hold its share of its own room's ground (here 6 square metres of 400)
            // and room enough to stand on (2) besides: a platform of 2.25 in a big room is none,
            // nor a ledge of 1.5 in a small one, though it holds its share.
            var big = FloorFinder.Patch(Patch(0, 0, 40, 40, -60f, patch: 7).Concat(Patch(0, 0, 5, 5, -55f, patch: 7)));
            big.Ground = 40 * 40 * Cell;
            var small = FloorFinder.Patch(Patch(0, 0, 20, 20, -80f, patch: 8).Concat(Patch(0, 0, 5, 4, -75f, patch: 8)));
            small.Ground = 20 * 20 * Cell;
            Assert.Equal(new[] { 0f, -40f, -60f, -80f }, FloorFinder.Floors(rooms.Concat(new[] { big, small }), footprint + big.Ground + small.Ground, PlaceView.Storey));
        }

        /// <summary>
        /// A dungeon room as its rays found it: the ground they were cast over, its highest
        /// doorway, and each height with the room to stand there, in square metres.
        /// </summary>
        private static FloorPatch Room(float ground, float door, params (float Height, float Room)[] levels)
        {
            var patch = new FloorPatch { Ground = ground, Door = door };
            foreach (var (height, room) in levels)
            {
                patch.Bands[(int)System.Math.Round(height / FloorFinder.Band)] = new FloorPatch.Band { Room = room, Sum = height, Count = 1, Landed = true };
            }
            return patch;
        }

        /// <summary>The floors as the rules before 2026-10-05 found them: a room's own level from 2 square metres, and its ground at any height.</summary>
        private static List<float> FloorsBefore(List<FloorPatch> rooms) =>
            FloorFinder.Floors(rooms, rooms.Sum(r => r.Ground), PlaceView.Storey, FloorRules.Before);

        [Fact]
        public void ASunkenCryptsOneLevelOfRoomsIsOneFloor()
        {
            // As a sunken crypt's rooms were read in game: all on one level, walked into at -2 m
            // (the first at 0 m, from the stairs down), with walkways a metre up, water channels
            // below, and ledges or tomb tops of 3 to 6 square metres 2 m up, 4 m over every
            // doorway of their room, and in the dead ends' caps. Those made a floor of their own;
            // so high over every doorway of their room, they are out of its reach.
            var endcap = Room(11f, -2f);
            var rooms = new List<FloorPatch>
            {
                Room(125f, 0f, (2f, 3f), (-1f, 10f), (-2f, 67f)),
                Room(178f, -2f, (2f, 3f), (-1f, 30f), (-2f, 70f)),
                Room(178f, -2f, (-1f, 30f), (-2f, 70f)),
                Room(98f, -2f, (0.7f, 5f), (-2f, 15f), (-3.5f, 14f)),
                Room(138f, -2f, (2f, 6f), (-1f, 5f), (-2f, 67f)),
                Room(179f, -2f, (3.8f, 5f), (2f, 6f), (-1f, 10f), (-2f, 100f), (-3.5f, 9f)),
                Room(208f, -2f, (-1f, 31f), (-2f, 77f)),
                Room(18f, -3.5f, (-3.5f, 7f)),
                Room(18f, -2f, (1f, 7f), (-2f, 7f)),
                endcap, endcap, endcap,
            };
            Assert.Equal(new[] { -2f }, FloorFinder.Floors(rooms, rooms.Sum(r => r.Ground), PlaceView.Storey));
            Assert.Equal(new[] { 2f, -2f }, FloorsBefore(rooms));
        }

        [Fact]
        public void TheTopOfARoomsRockHighOverItsDoorwaysIsNoFloor()
        {
            // As a frost cave's rooms were read in game: a crossroads walked into at -20.8 and
            // -3 m found ground at 6.8 and 5.1 m, the top of its rock, which made a top floor of
            // one room that is none. Ground more than 2 m over every doorway of its room is out of
            // its reach.
            var entrance = Room(540f, 0f, (6.8f, 8f), (-0.2f, 57f), (-1.7f, 2f), (-3.3f, 77f), (-3.8f, 4f), (-5.8f, 10f));
            var crossroads = Room(272f, -3f, (6.8f, 32f), (5.1f, 14f), (-3.3f, 4f), (-20.2f, 126f));
            var shrine = Room(408f, -18.5f, (-10.7f, 6f), (-14.6f, 3f), (-18.5f, 77f), (-19.3f, 62f), (-20.3f, 119f), (-23.6f, 12f));
            var rooms = new List<FloorPatch> { entrance, crossroads, shrine };

            var floors = FloorFinder.Floors(rooms, rooms.Sum(r => r.Ground), PlaceView.Storey);
            Assert.Equal(-0.2f, floors[0], 2);
            Assert.DoesNotContain(floors, f => f < -9.5f && f > -12f || f > 0f);
            Assert.Contains(6.8f, FloorsBefore(rooms));
            // Nor does the room stand on it by its own ground.
            Assert.False(FloorFinder.Holds(crossroads, 6.8f));
            Assert.True(FloorFinder.Holds(crossroads, -20.2f));
            // Nor an entrance's, high over its doorways.
            Assert.DoesNotContain(FloorFinder.Floors(new[] { Room(540f, 0f, (6.8f, 30f), (-0.2f, 57f), (-3.3f, 77f)) }, 540f, PlaceView.Storey), f => f > 0f);
            // However wide its rock's top, its main floor is where it is walked.
            Assert.Equal(-20.2f, FloorFinder.MainFloor(Room(272f, -3f, (6.8f, 200f), (-20.2f, 126f))).Value, 2);
            // A raised floor a step or two over its doorway is the room's all the same.
            Assert.Equal(1.5f, FloorFinder.MainFloor(Room(200f, 0f, (1.5f, 80f), (-1f, 20f))).Value, 2);
        }

        [Fact]
        public void LedgesDownAShaftAreNoFloorsOfTheirOwn()
        {
            // A frost cave's shaft down from -18.5 to -36.3 m, its ground ledges of 3 to 5 square
            // metres on the way: each made a floor with one room on it. A level of the room's
            // own as big as a landing still is one (the shaft's 9 at -24.5 m).
            var shaft = Room(184f, -18.5f, (-9.9f, 8f), (-18.5f, 50f), (-24.5f, 9f), (-28.5f, 3f), (-30.4f, 5f), (-34.3f, 4f), (-36.3f, 116f));
            var halls = new List<FloorPatch> { Room(400f, -18.5f, (-18.5f, 120f)), Room(400f, -36.3f, (-36.3f, 120f)), shaft };

            var floors = FloorFinder.Floors(halls, halls.Sum(r => r.Ground), PlaceView.Storey);
            Assert.Equal(new[] { -18.5f, -24.5f, -36.3f }, floors);
            Assert.Contains(-30.4f, FloorsBefore(halls));
        }

        [Fact]
        public void ATowersFloorsAndAHallsGalleryAreFoundAsBefore()
        {
            // Rooms stacked a storey apart, and a hall with a gallery walked onto by a doorway:
            // the same floors by the rules now as before.
            var tower = new List<FloorPatch>
            {
                Room(144f, 3f, (0f, 30f)),
                Room(144f, 10f, (7f, 30f)),
                Room(144f, 18f, (15f, 30f)),
                Room(400f, -34f, (-40f, 90f), (-34f, 9f)),
            };
            Assert.Equal(new[] { 15f, 7f, 0f, -34f, -40f }, FloorFinder.Floors(tower, tower.Sum(r => r.Ground), PlaceView.Storey));
            Assert.Equal(FloorsBefore(tower), FloorFinder.Floors(tower, tower.Sum(r => r.Ground), PlaceView.Storey));
        }

        [Fact]
        public void MorkhallasGroundOverItsGateIsNoFloorOfItsInside()
        {
            // As Morkhalla's rooms were read in game: its entrance walked into at 0 m from its gate,
            // its top 1,400 square metres at 7.2 m round the gate, outside; the inside shown is
            // what lies under the gate (Kevin's pick), its levels at -2.8 and -12.8 m and the rooms
            // below, each a floor.
            var rooms = new List<FloorPatch>
            {
                Room(4417f, 0f, (7.2f, 1400f), (-2.8f, 1574f), (-11.1f, 22f), (-12.8f, 1502f)),
                Room(4033f, -13.3f, (-22.8f, 52f)),
                Room(4016f, -23.3f, (-32.8f, 70f)),
                Room(4033f, -33.3f, (-42.8f, 62f)),
                Room(4180f, -43.3f, (-52.8f, 71f), (-62.8f, 1711f)),
                Room(4174f, -63.3f, (-72.8f, 77f), (-82.8f, 1359f)),
                Room(4340f, -83.3f, (-92.8f, 3f), (-102.8f, 1313f)),
            };
            var floors = FloorFinder.Floors(rooms, rooms.Sum(r => r.Ground), PlaceView.Storey);
            Assert.Equal(new[] { -2.8f, -12.8f, -22.8f, -32.8f, -42.8f, -52.8f, -62.8f, -72.8f, -82.8f, -102.8f }, floors.Select(f => (float)System.Math.Round(f, 1)));
            Assert.Contains(7.2f, FloorsBefore(rooms).Select(f => (float)System.Math.Round(f, 1)));
        }

        [Fact]
        public void ADungeonRoomsGroundCountsThoughNothingOfTheRoomIsOverItButOutdoorsAndOutOfReach()
        {
            // Read alone, a room whose ceiling is the room above has nothing of its own over its
            // floor: Morkhalla's middle rooms found 50 to 80 of some 4,000 square metres, their
            // floors coming and going. Within reach of its doorways such ground counts; high over
            // them it is the top of its rock; in the entrance, near or over the doorway out, it is
            // the ground outside.
            var hits = Patch(0, 0, 20, 20, -22.8f, open: true).Concat(Patch(0, 0, 20, 20, 5f, open: true)).Concat(Patch(0, 0, 10, 10, -30f)).ToList();
            var inside = FloorFinder.Inside(hits, door: -13.3f, outerDoor: float.PositiveInfinity);
            Assert.Equal(hits.Count, inside.Count);
            Assert.All(inside.Where(h => h.Height < -20f && h.Height > -25f), h => Assert.False(h.Open));
            Assert.All(inside.Where(h => h.Height > 0f), h => Assert.True(h.Open));
            Assert.Equal(-22.8f, FloorFinder.MainFloor(FloorFinder.Patch(inside)).Value, 2);
            Assert.Equal(-30f, FloorFinder.MainFloor(FloorFinder.Patch(hits)).Value, 2);

            // The burial chambers' entrance, its doorway out at 0.5 m: the open ground round it
            // stays outside, its covered stairs down are its own.
            var entrance = Patch(0, 0, 20, 20, 0.5f, open: true).Concat(Patch(0, 0, 10, 10, -4f)).ToList();
            var gate = FloorFinder.Inside(entrance, door: 0.5f, outerDoor: 0.5f);
            Assert.All(gate.Where(h => h.Height > 0f), h => Assert.True(h.Open));
            Assert.Equal(-4f, FloorFinder.MainFloor(FloorFinder.Patch(gate)).Value, 2);
            // Open ground a storey down inside the gate is its own.
            Assert.All(FloorFinder.Inside(Patch(0, 0, 5, 5, -3f, open: true), door: 0.5f, outerDoor: 0.5f), h => Assert.False(h.Open));
        }

        [Fact]
        public void ASealedTowersStoreysAreFoundAsBefore()
        {
            // As Hildir's sealed tower's rooms were read in game: each 8 m high, walked into at its
            // foot, half way up and at its top, its main floor 0.7 m under its top by the doorway
            // up, a landing half way. Every one of them is a floor, as before.
            var tower = new List<FloorPatch>
            {
                Room(373f, 8f, (7.3f, 141f), (4f, 10f), (1f, 3f), (0.3f, 106f)),
                Room(308f, 16f, (15.3f, 135f), (12f, 9f), (9f, 3f)),
                Room(306f, 24f, (23.3f, 138f), (20f, 9f)),
            };
            var floors = FloorFinder.Floors(tower, tower.Sum(r => r.Ground), PlaceView.Storey);
            Assert.Equal(new[] { 23.3f, 20f, 15.3f, 12f, 7.3f, 4f, 0.3f }, floors.Select(f => (float)System.Math.Round(f, 1)));
            Assert.Equal(FloorsBefore(tower), floors);
        }

        [Fact]
        public void ARoomsMainFloorIsWhereItHasTheMostRoomToStand()
        {
            var room = FloorFinder.Patch(Patch(0, 0, 20, 20, -40f).Concat(Patch(30, 30, 6, 6, -36f)));
            Assert.Equal(-40f, FloorFinder.MainFloor(room).Value, 2);
            Assert.Null(FloorFinder.MainFloor(FloorFinder.Patch(Patch(0, 0, 4, 4, 2f))));
            Assert.Null(FloorFinder.MainFloor(FloorFinder.Patch(Patch(0, 0, 20, 20, 2f, open: true))));
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
        public void ARoomStandsOnAFloorItsOwnRaysFoundGroundOn()
        {
            // A room's meshes can reach past the box the game sizes it by, so a floor found in it
            // is checked against what its own rays found: ground within a metre of the floor.
            var tower = FloorFinder.Patch(Patch(0, 0, 12, 12, 0f).Concat(Patch(0, 0, 12, 12, 6.2f)));
            Assert.True(FloorFinder.Holds(tower, 6f));
            Assert.True(FloorFinder.Holds(tower, 0.5f));
            Assert.False(FloorFinder.Holds(tower, 3f));
            Assert.False(FloorFinder.Holds(tower, 7.5f));

            // Ground open to the sky, a ledge too narrow to stand on and nothing at all hold none.
            Assert.False(FloorFinder.Holds(FloorFinder.Patch(Patch(0, 0, 12, 12, 6f, open: true)), 6f));
            Assert.False(FloorFinder.Holds(FloorFinder.Patch(Patch(0, 0, 12, 1, 6f)), 6f));
            Assert.False(FloorFinder.Holds(FloorFinder.Patch(new FloorHit[0]), 6f));
        }

        [Fact]
        public void AnExamplesGroundSteppingDownLessThanAStoreyAtATimeIsOneFloorAsFarAsItsCutReaches()
        {
            // A cave's ground at -3.5, -4.2 and -5 m, each less than a storey below the last and all
            // within two metres, is one floor, at the one with the most room; a chamber far below is
            // a floor of its own.
            var rooms = new List<FloorPatch>
            {
                FloorFinder.Patch(Patch(0, 0, 12, 12, -3.5f, patch: 0)),
                FloorFinder.Patch(Patch(0, 0, 20, 20, -4.2f, patch: 1)),
                FloorFinder.Patch(Patch(0, 0, 12, 12, -5f, patch: 2)),
                FloorFinder.Patch(Patch(0, 0, 12, 12, -41.5f, patch: 3)),
            };
            var footprint = 4 * 20 * 20 * Cell;

            Assert.Equal(new[] { -4.2f, -41.5f }, FloorFinder.Floors(rooms, footprint, PlaceView.Storey));

            // Ground at -3.2, -5.7 and -7.8 m steps down less than a storey at a time too, but over
            // more than its cut reaches: a floor each, so the cut at -5.7 m does not take -3.2 m away.
            var deeper = new List<FloorPatch>
            {
                FloorFinder.Patch(Patch(0, 0, 12, 12, -3.2f, patch: 0)),
                FloorFinder.Patch(Patch(0, 0, 20, 20, -5.7f, patch: 1)),
                FloorFinder.Patch(Patch(0, 0, 12, 12, -7.8f, patch: 2)),
            };
            Assert.Equal(new[] { -3.2f, -5.7f, -7.8f }, FloorFinder.Floors(deeper, 3 * 20 * 20 * Cell, PlaceView.Storey));

            // A tower's storeys, a storey or more apart, stay floors of their own.
            var tower = new[] { 0f, 7f, 15f }.Select((h, i) => FloorFinder.Patch(Patch(0, 0, 12, 12, h, patch: i))).ToList();
            Assert.Equal(new[] { 15f, 7f, 0f }, FloorFinder.Floors(tower, 3 * 12 * 12 * Cell, PlaceView.Storey));
        }

        [Fact]
        public void ALongSlopeIsAFloorEveryTwoMetresSoNoneOfItStandsAboveItsCut()
        {
            // A frost cave's tunnel stepping down a metre at a time from -2 to -9 m: one floor for
            // all of it, cut 2.5 m over where most of it is, cut away its upper ground. Each floor
            // takes ground no more than two metres above it, so all of it stays under its cut.
            var heights = new[] { -2f, -3f, -4f, -5f, -6f, -7f, -8f, -9f };
            var rooms = heights.Select((h, i) => FloorFinder.Patch(Patch(0, 0, 12, 12, h, patch: i))).ToList();
            var floors = FloorFinder.Floors(rooms, heights.Length * 12 * 12 * Cell, PlaceView.Storey);
            Assert.Equal(new[] { -2f, -4f, -6f, -8f }, floors);

            // Every ground opened under some floor's cut, half a metre under it at the least.
            var cuts = PlaceView.CutHeights(floors);
            foreach (var ground in heights)
            {
                var floor = floors.FindLastIndex(f => f >= ground - 1e-4f);
                Assert.True(cuts[floor] - ground >= 0.5f - 1e-4f, $"ground at {ground} m under floor {floors[floor]} m, cut at {cuts[floor]} m");
            }
            Assert.Equal(2f, PlaceView.LevelSpan);
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
