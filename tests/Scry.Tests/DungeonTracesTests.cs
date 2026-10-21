using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class DungeonTracesTests
    {
        // What was left inside a dungeon tells that someone walked it, though not who: a pickup
        // taken, a chest taken from, a thing broken or a rock mined tells the room it lies in, a door
        // opened the room on its entrance side, a spawn point woken only that someone was inside.
        // Every visit starts at the entrance, so the way to each such room was walked too: the
        // way needing the fewest rooms nobody is known to have walked, of two as good the one
        // through the room placed first.

        private const float Up = 5000f;

        private static PlacedRoom Room(string name, float x, float z, int savedAt, params (float X, float Z)[] doorways)
        {
            var shape = new RoomShape { Name = name, Size = new Vec3(10f, 4f, 10f) };
            var at = new Vec3(x, Up, z);
            foreach (var (dx, dz) in doorways) shape.Doorways.Add(new Doorway { Position = new Vec3(dx, Up, dz) - at });
            return new PlacedRoom { Room = shape, Position = at, PlaceOrder = savedAt, SavedAt = savedAt };
        }

        /// <summary>
        /// Twelve rooms: the entrance, a hall and a crossroads; north of it a dead end two rooms
        /// long; east a square of four rooms, two ways round from its first to the one across
        /// (rooms 6 and 7), and a room beyond; south two rooms.
        /// </summary>
        private static DungeonWays Dungeon() => DungeonWays.Of(new[]
        {
            Room("entrance", 0f, 0f, 0, (5f, 0f)),
            Room("hall", 10f, 0f, 1, (5f, 0f), (15f, 0f)),
            Room("crossroads", 20f, 0f, 2, (15f, 0f), (25f, 0f), (20f, 5f), (20f, -5f)),
            Room("north", 20f, -10f, 3, (20f, -5f), (20f, -15f)),
            Room("north end", 20f, -20f, 4, (20f, -15f)),
            Room("square", 30f, 0f, 5, (25f, 0f), (30f, -5f), (35f, 0f)),
            Room("square north", 30f, -10f, 6, (30f, -5f), (35f, -10f)),
            Room("square east", 40f, 0f, 7, (35f, 0f), (40f, -5f)),
            Room("square across", 40f, -10f, 8, (35f, -10f), (40f, -5f), (45f, -10f)),
            Room("beyond", 50f, -10f, 9, (45f, -10f)),
            Room("south", 20f, 10f, 10, (20f, 5f), (20f, 15f)),
            Room("south end", 20f, 20f, 11, (20f, 15f)),
        });

        private static Vec3 In(float x, float z) => new Vec3(x, Up - 1f, z);

        [Fact]
        public void ATraceTellsTheRoomItLiesInADoorOpenedTheRoomOnItsEntranceSide()
        {
            var ways = Dungeon();
            var traces = DungeonTraces.Of(ways, new[]
            {
                new Trace(TraceKind.Chest, In(41f, -12f)),
                new Trace(TraceKind.Broken, In(21f, 20f)),
                new Trace(TraceKind.Broken, In(19f, 21f)),
                new Trace(TraceKind.Mined, In(20f, -10f)),
                // A door stands in the doorway two rooms share: whoever opened it came from the entrance's side.
                new Trace(TraceKind.Door, new Vec3(30f, Up, -5f)),
                // Outside every room: told nothing.
                new Trace(TraceKind.Pickup, In(300f, 300f)),
            }, -1);

            Assert.Equal(new[] { 3, 8, 11 }, traces.InRooms.OrderBy(i => i));
            Assert.Equal(new[] { 5 }, traces.UpTo);
            Assert.True(traces.Inside);
            var tally = traces.Tally;
            Assert.Equal((1, 2, 0, 1, 1, 0, 3), (tally.Chests, tally.Broken, tally.Pickups, tally.Mined, tally.Doors, tally.Spawners, tally.Rooms));
            // A door between rooms listed farther in first is still of the room nearer the entrance.
            var turned = DungeonWays.Of(Dungeon().Rooms.Reverse().ToList());
            Assert.Equal(new[] { 5 }, DungeonTraces.Of(turned, new[] { new Trace(TraceKind.Door, new Vec3(30f, Up, -5f)) }, -1).UpTo);
            // A door standing in no doorway is of the room it stands in.
            Assert.Equal(new[] { 11 }, DungeonTraces.Of(Dungeon(), new[] { new Trace(TraceKind.Door, In(21f, 21f)) }, -1).UpTo);
        }

        [Fact]
        public void ASpawnPointWokenTellsOnlyThatSomeoneWasInside()
        {
            // It wakes for anyone within 60 metres, which reaches over several rooms.
            var traces = DungeonTraces.Of(Dungeon(), new[] { new Trace(TraceKind.Spawner, In(20f, -20f)) }, -1);
            Assert.Empty(traces.InRooms);
            Assert.True(traces.Inside);
            Assert.Equal(new[] { 0 }, traces.Uncovered(new int[0]));
            Assert.Empty(DungeonTraces.Of(Dungeon(), new Trace[0], -1).Uncovered(new int[0]));
            // One outside every room is none of this dungeon's.
            Assert.False(DungeonTraces.Of(Dungeon(), new[] { new Trace(TraceKind.Spawner, In(300f, 300f)) }, -1).Inside);
        }

        [Fact]
        public void TheWayToEachRoomWithATraceIsUncoveredRoundALoopTheWayThroughTheRoomPlacedFirst()
        {
            var traces = DungeonTraces.Of(Dungeon(), new[] { new Trace(TraceKind.Chest, In(40f, -10f)), new Trace(TraceKind.Broken, In(20f, 20f)) }, -1);
            // Two ways round the square are as long: the one through room 6, placed first.
            Assert.Equal(new[] { 0, 1, 2, 5, 6, 8, 10, 11 }, traces.Uncovered(new int[0]));
            // A door opened uncovers the way up to it, not the room behind it.
            var door = DungeonTraces.Of(Dungeon(), new[] { new Trace(TraceKind.Door, new Vec3(30f, Up, -5f)) }, -1);
            Assert.Equal(new[] { 0, 1, 2, 5 }, door.Uncovered(new int[0]));
            // A way taken for one trace is known for the next: the door opened on the east side leads the way to the room across.
            var east = DungeonTraces.Of(Dungeon(), new[] { new Trace(TraceKind.Door, new Vec3(40f, Up, -5f)), new Trace(TraceKind.Chest, In(40f, -10f)) }, -1);
            Assert.Equal(new[] { 0, 1, 2, 5, 7, 8 }, east.Uncovered(new int[0]));
        }

        [Fact]
        public void WhereTwoWaysAsGoodPartTheOneGoingOnThroughTheRoomPlacedFirstIsTaken()
        {
            // Two ways round, three rooms each: east through the room placed second, south through the one placed third.
            var ways = DungeonWays.Of(new[]
            {
                Room("entrance", 0f, 0f, 0, (5f, 0f), (0f, 5f)),
                Room("east", 10f, 0f, 1, (5f, 0f), (15f, 0f)),
                Room("south", 0f, 10f, 2, (0f, 5f), (5f, 10f)),
                Room("south on", 10f, 10f, 3, (5f, 10f), (15f, 10f)),
                Room("east on", 20f, 0f, 4, (15f, 0f), (20f, 5f)),
                Room("across", 20f, 10f, 5, (20f, 5f), (15f, 10f)),
            });
            var traces = DungeonTraces.Of(ways, new[] { new Trace(TraceKind.Chest, In(20f, 10f)) }, -1);
            Assert.Equal(new[] { 0, 1, 4, 5 }, traces.Uncovered(new int[0]));
            // Known rooms first: the south way walked is the way.
            Assert.Equal(new[] { 0, 2, 3, 5 }, traces.Uncovered(new[] { 2, 3 }));
        }

        [Fact]
        public void ARoomWithATraceIsKnownWalkedForTheWayToAnotherAndOneNoWayReachesStandsAlone()
        {
            // The east side of the square was placed last, after the room across.
            var ways = DungeonWays.Of(new[]
            {
                Room("entrance", 0f, 0f, 0, (5f, 0f)),
                Room("square", 10f, 0f, 1, (5f, 0f), (10f, -5f), (15f, 0f)),
                Room("north", 10f, -10f, 2, (10f, -5f), (15f, -10f)),
                Room("across", 20f, -10f, 3, (15f, -10f), (20f, -5f)),
                Room("east", 20f, 0f, 4, (15f, 0f), (20f, -5f)),
                Room("cut off", 100f, 100f, 5),
            });
            var traces = DungeonTraces.Of(ways, new[] { new Trace(TraceKind.Chest, In(20f, -10f)), new Trace(TraceKind.Broken, In(20f, 0f)), new Trace(TraceKind.Pickup, In(100f, 100f)) }, -1);
            Assert.Equal(new[] { 0, 1, 3, 4, 5 }, traces.Uncovered(new int[0]));
        }

        [Fact]
        public void AWalkRoundTheOtherSideOfALoopTakesTheGuessBack()
        {
            var traces = DungeonTraces.Of(Dungeon(), new[] { new Trace(TraceKind.Chest, In(40f, -10f)) }, -1);
            Assert.Equal(new[] { 0, 1, 2, 5, 6, 8 }, traces.Uncovered(new int[0]));
            // Someone walked in round the east side: that is the way, nobody needs to have gone north.
            Assert.Equal(new[] { 0, 1, 2, 5, 7, 8 }, traces.Uncovered(new[] { 0, 1, 2, 5, 7 }));
            // A walk elsewhere changes nothing of the way.
            Assert.Equal(new[] { 0, 1, 2, 5, 6, 8 }, traces.Uncovered(new[] { 10, 11 }));
        }

        [Fact]
        public void AfterAForgetOnlyTracesOfALaterDayAreRead()
        {
            var traces = new[]
            {
                new Trace(TraceKind.Pickup, In(20f, 20f), 50),
                new Trace(TraceKind.Pickup, In(20f, -20f), 30),
                new Trace(TraceKind.Chest, In(40f, -10f)),
                new Trace(TraceKind.Spawner, In(10f, 0f)),
            };
            var never = DungeonTraces.Of(Dungeon(), traces, -1);
            Assert.Equal(new[] { 4, 8, 11 }, never.InRooms.OrderBy(i => i));
            Assert.Equal(50, never.Tally.LastDay);
            // Forgotten on day 40: a trace of no known day may be from before it, so only the day-50 pickup counts.
            var after = DungeonTraces.Of(Dungeon(), traces, 40);
            Assert.Equal(new[] { 11 }, after.InRooms);
            Assert.Equal((1, 0, 0), (after.Tally.Pickups, after.Tally.Chests, after.Tally.Spawners));
            Assert.False(DungeonTraces.Of(Dungeon(), traces, 60).Inside);
            // A trace of the very day of the forget counts.
            Assert.Equal(new[] { 11 }, DungeonTraces.Of(Dungeon(), traces, 50).InRooms);
        }

        [Fact]
        public void WhatWasReadStaysThoughTheDungeonIsMadeAgainAndItsTracesAreGone()
        {
            // A mod that resets dungeons makes all in one again, chests filled and pots whole; in a
            // world without one nothing goes. Either way what was read before stays.
            var record = new DungeonMaps();
            record.Keep("w:at:0:0", "Crypt2", Rooms(12), default, default, Quat.Identity);
            var first = DungeonTraces.Of(Dungeon(), new[] { new Trace(TraceKind.Chest, In(40f, -10f)), new Trace(TraceKind.Broken, In(40f, -11f)), new Trace(TraceKind.Door, new Vec3(30f, Up, -5f)) }, -1);
            record.SetTraced("w:at:0:0", first.Uncovered(new int[0]), first.Tally, first.InRooms, first.UpTo);
            var kept = record.Of("w:at:0:0");
            Assert.Equal(new[] { 8 }, kept.TraceRooms);
            Assert.Equal(new[] { 5 }, kept.TraceDoors);

            // Reset since: only a pot broken after it is left to read.
            var after = DungeonTraces.Of(Dungeon(), new[] { new Trace(TraceKind.Broken, In(20f, 20f)) }, -1);
            after.Keep(kept);
            Assert.Equal(new[] { 8, 11 }, after.InRooms.OrderBy(i => i));
            Assert.Equal(new[] { 5 }, after.UpTo);
            Assert.True(after.Inside);
            Assert.Equal(new[] { 0, 1, 2, 5, 6, 8, 10, 11 }, after.Uncovered(new int[0]));
            // Each kind as much as was ever read at once, the rooms all that held one.
            Assert.Equal((1, 1, 1, 2), (after.Tally.Chests, after.Tally.Broken, after.Tally.Doors, after.Tally.Rooms));
            // Every kind and the latest day kept as read before.
            var rich = DungeonTraces.Of(Dungeon(), new Trace[0], -1);
            rich.Keep(new DungeonMap { Rooms = Rooms(12), Tally = new TraceTally { Pickups = 3, Mined = 2, Spawners = 4, Broken = 5, LastDay = 9 } });
            Assert.Equal((3, 2, 4, 5, 9), (rich.Tally.Pickups, rich.Tally.Mined, rich.Tally.Spawners, rich.Tally.Broken, rich.Tally.LastDay));
            // Kept from nothing read, nothing changes.
            var none = DungeonTraces.Of(Dungeon(), new Trace[0], -1);
            none.Keep(new DungeonMap { Rooms = Rooms(12) });
            Assert.False(none.Inside);
            var only = DungeonTraces.Of(Dungeon(), new Trace[0], -1);
            only.Keep(kept);
            Assert.True(only.Inside);
            Assert.Equal(first.Uncovered(new int[0]), only.Uncovered(new int[0]));
        }

        [Fact]
        public void AReadingTellsADungeonMadeAgainOfOtherRoomsFromTheOneKept()
        {
            // Made again with more rooms, as a mod raising their number may, the rooms read are not the ones kept.
            var map = new DungeonMap { Rooms = Rooms(12) };
            Assert.True(map.SameRooms(Rooms(12)));
            Assert.False(map.SameRooms(Rooms(13)));
            var turned = Rooms(12);
            turned[4] = (99, turned[4].Item2, turned[4].Item3);
            Assert.False(map.SameRooms(turned));
            var moved = Rooms(12);
            moved[4] = (moved[4].Item1, moved[4].Item2 + new Vec3(0f, 0f, 1f), moved[4].Item3);
            Assert.False(map.SameRooms(moved));
            Assert.False(new DungeonMap { Rooms = new List<(int, Vec3, Vec3)>() }.SameRooms(Rooms(12)));
        }

        [Fact]
        public void AChestHoldingFewerItemsThanItsFillingEverGivesWasTakenFrom()
        {
            // A dungeon chest's filling gives each item once, two at the least for a burial chamber's:
            // one left holding a single item was taken from, though not emptied.
            Assert.True(DungeonTraces.TakenFrom(1, 2, true));
            Assert.True(DungeonTraces.TakenFrom(0, 2, true));
            Assert.False(DungeonTraces.TakenFrom(2, 2, true));
            Assert.False(DungeonTraces.TakenFrom(4, 2, true));
            // Where the same item may come twice, two may have stacked into one: only an empty chest tells.
            Assert.False(DungeonTraces.TakenFrom(1, 2, false));
            Assert.True(DungeonTraces.TakenFrom(0, 2, false));
        }

        [Fact]
        public void APartGoneFromWhereItsRoomMadeItIsATraceOneStillStandingIsNot()
        {
            var chest = "TreasureChest_x".GetHashCode();
            var pot = "Pot_x".GetHashCode();
            var made = new[]
            {
                (chest, new Vec3(40f, Up, -12f), TraceKind.Chest),
                (pot, new Vec3(21f, Up, 20f), TraceKind.Broken),
                (pot, new Vec3(22f, Up, 21f), TraceKind.Broken),
            };
            var standing = new[]
            {
                // Saved to the centimetre: still the same pot.
                (pot, new Vec3(22.05f, Up, 21.02f)),
                // Something else where the chest was made does not stand for it.
                ("Rock_x".GetHashCode(), new Vec3(40f, Up, -12f)),
            };
            var gone = DungeonTraces.Gone(made, standing);
            Assert.Equal(new[] { (TraceKind.Chest, 40f), (TraceKind.Broken, 21f) }, gone.Select(g => (g.Kind, g.At.X)));
            Assert.Empty(DungeonTraces.Gone(made.Take(1), new[] { (chest, new Vec3(40.1f, Up, -12.1f)) }));
        }

        [Fact]
        public void ARoomsPartsAreRolledFromItsOwnPlaceAsTheGameRollsThem()
        {
            // DungeonGenerator.PlaceRoom sums each of the room's coordinates, cut to a whole number,
            // times a factor of its own, from the generator where the location sets its own
            // interior, the base seed added where it says so.
            Assert.Equal(1234 * 4271 + 5010 * 9187 - 340 * 2134, DungeonTraces.RoomSeed(new Vec3(1234.7f, 5010.2f, -340.9f), new Vec3(1200f, 5000f, -300f), false, 0));
            Assert.Equal(34 * 4271 + 10 * 9187 - 40 * 2134 + 1000, DungeonTraces.RoomSeed(new Vec3(1234.7f, 5010.2f, -340.9f), new Vec3(1200f, 5000f, -300f), true, 1000));
            // It wraps round as the game's sum does.
            Assert.Equal(unchecked((int)(600000L * 4271)), DungeonTraces.RoomSeed(new Vec3(600000f, 0f, 0f), new Vec3(0f, 0f, 0f), false, 0));
        }

        private static List<(int, Vec3, Vec3)> Rooms(int count) =>
            Enumerable.Range(0, count).Select(i => (i + 1, new Vec3(i * 10f, Up, 0f), new Vec3(0f, 0f, 0f))).ToList();

        private static TraceTally Tally(int chests, int broken, int rooms, int day = -1) =>
            new TraceTally { Chests = chests, Broken = broken, Rooms = rooms, LastDay = day };

        [Fact]
        public void TheRoomsTracesUncoverAreKeptOnALineOfTheirOwnThatAnOlderReaderPassesOver()
        {
            var maps = new DungeonMaps();
            maps.Keep("w:at:1:1", "MountainCave02", Rooms(12), new Vec3(1f, 2f, 3f), new Vec3(1f, Up, 3f), Quat.Identity, 41);
            maps.Walk("w:at:1:1", 0);
            maps.ToSave();
            Assert.True(maps.SetTraced("w:at:1:1", new[] { 0, 1, 2, 5, 6, 8 }, Tally(1, 3, 2, 412)));
            Assert.True(maps.Changed);
            // The same again changes nothing.
            Assert.False(maps.SetTraced("w:at:1:1", new[] { 0, 1, 2, 5, 6, 8 }, Tally(1, 3, 2, 412)));
            Assert.False(maps.SetTraced("w:at:9:9", new[] { 0 }, Tally(1, 0, 1)));
            // A room past the dungeon's rooms is none of them.
            Assert.False(maps.SetTraced("w:at:1:1", new[] { 0, 1, 2, 5, 6, 8, 12, -1 }, Tally(1, 3, 2, 412)));
            // What was left changing alone changes it.
            Assert.True(maps.SetTraced("w:at:1:1", new[] { 0, 1, 2, 5, 6, 8 }, Tally(2, 3, 2, 412)));
            Assert.True(maps.SetTraced("w:at:1:1", new[] { 0, 1, 2, 5, 6, 8 }, Tally(1, 3, 2, 412)));

            maps.SetTraced("w:at:1:1", new[] { 0, 1, 2, 5, 6, 8 }, Tally(1, 3, 2, 412), new[] { 6, 8 }, new[] { 5 });
            var text = maps.ToSave()();
            var line = text.Split('\n').Single(l => l.StartsWith("traced\t", StringComparison.Ordinal));
            Assert.Equal(6, line.Split('\t').Length);
            var back = DungeonMaps.Load(text).Of("w:at:1:1");
            Assert.Equal(new[] { 0 }, back.Walked);
            Assert.Equal(new[] { 0, 1, 2, 5, 6, 8 }, back.Traced);
            Assert.Equal((1, 3, 2, 412), (back.Tally.Chests, back.Tally.Broken, back.Tally.Rooms, back.Tally.LastDay));
            Assert.Equal(new[] { 6, 8 }, back.TraceRooms);
            Assert.Equal(new[] { 5 }, back.TraceDoors);
            // A line of four fields, without the rooms that held traces, still reads.
            var four = DungeonMaps.Load("scry-maps 1\n" + text.Split('\n')[1] + "\ntraced\tw:at:1:1\t0,1\t1,0,0,0,0,0,1,-1").Of("w:at:1:1");
            Assert.Equal(new[] { 0, 1 }, four.Traced);
            Assert.Empty(four.TraceRooms);
            // Six fields are no dungeon's seven, so an older Scry passes the line over; one that cannot be read is passed over.
            Assert.Empty(DungeonMaps.Load("scry-maps 1\n" + text.Split('\n')[1] + "\ntraced\tw:at:1:1\tnot rooms\t1,2").Of("w:at:1:1").Traced);
            Assert.Empty(DungeonMaps.Load("scry-maps 1\n" + text.Split('\n')[1] + "\ntraced\tw:at:1:1\t0,1\t1,2,3,4,5,6,7").Of("w:at:1:1").Traced);
        }

        [Fact]
        public void TheServerAnswersWithTheRoomsTracesUncoverAsWalkedAndSaysWhichCameOfTracesAlone()
        {
            var records = new DungeonMaps();
            records.Keep("w:at:1:1", "MountainCave02", Rooms(12), new Vec3(1f, 2f, 3f), new Vec3(1f, Up, 3f), Quat.Identity, 41);
            foreach (var room in new[] { 0, 1, 9 }) records.Walk("w:at:1:1", room);
            records.SetTraced("w:at:1:1", new[] { 0, 1, 2, 5, 6, 8 }, Tally(1, 3, 2));
            var answer = DungeonMaps.Load(records.Only("w", new[] { "MountainCave02" }).ToSave()()).Of("w:at:1:1");
            Assert.Equal(new[] { 0, 1, 2, 5, 6, 8, 9 }, answer.Walked);
            Assert.Equal(new[] { 2, 5, 6, 8 }, answer.Traced);
            Assert.Equal(3, answer.Tally.Broken);
            // The records keep the walks apart: a walk sent counts against them alone.
            Assert.Equal(new[] { 0, 1, 9 }, records.Of("w:at:1:1").Walked);
            // A walk taken in keeps the traces as they were.
            var walk = new DungeonMaps();
            walk.Keep("w:at:1:1", "MountainCave02", Rooms(12), new Vec3(1f, 2f, 3f), new Vec3(1f, Up, 3f), Quat.Identity);
            walk.Walk("w:at:1:1", 7);
            records.Merge(walk, "w");
            Assert.Equal(new[] { 0, 1, 2, 5, 6, 8 }, records.Of("w:at:1:1").Traced);
        }

        [Fact]
        public void AnAnswerReplacesTheCopyOfEachDungeonItHolds()
        {
            // The server takes a guess round a loop back as a walk shows the other way, so its answer stands, not added to what was there.
            var copy = new DungeonMaps();
            copy.Keep("w:at:1:1", "MountainCave02", Rooms(12), new Vec3(1f, 2f, 3f), default, Quat.Identity);
            foreach (var room in new[] { 0, 1, 2, 5, 6, 8 }) copy.Walk("w:at:1:1", room);
            copy.SetTraced("w:at:1:1", new[] { 2, 5, 6, 8 }, Tally(1, 0, 1));
            copy.Keep("w:at:2:2", "MountainCave02", Rooms(3), new Vec3(2f, 2f, 2f), default, Quat.Identity);
            copy.Walk("w:at:2:2", 1);

            var answer = new DungeonMaps();
            answer.Keep("w:at:1:1", "MountainCave02", Rooms(12), new Vec3(1f, 2f, 3f), default, Quat.Identity, 50);
            foreach (var room in new[] { 0, 1, 2, 5, 7, 8 }) answer.Walk("w:at:1:1", room);
            answer.SetTraced("w:at:1:1", new[] { 8 }, Tally(1, 0, 1), new[] { 8 }, new int[0]);
            copy.ToSave();
            copy.Replace(answer);

            var map = copy.Of("w:at:1:1");
            Assert.Equal(new[] { 0, 1, 2, 5, 7, 8 }, map.Walked);
            Assert.Equal(new[] { 8 }, map.Traced);
            Assert.Equal(new[] { 8 }, map.TraceRooms);
            Assert.Equal(50, map.LastDay);
            Assert.True(copy.Changed);
            // A dungeon the answer does not hold stays as it was.
            Assert.Equal(new[] { 1 }, copy.Of("w:at:2:2").Walked);
        }

        [Fact]
        public void ReKeyedByPlaceAMapsTracesStayWithIt()
        {
            var maps = new DungeonMaps();
            maps.Keep("w:at:10:0", "MountainCave02", Rooms(12), new Vec3(10f, 30f, 0f), default, Quat.Identity);
            maps.SetTraced("w:at:10:0", new[] { 0, 3 }, Tally(0, 1, 1), new[] { 3 }, new[] { 2 });
            // The same cave walked under its generator's old number.
            maps.Keep("w:1:369448", "MountainCave02", Rooms(12), new Vec3(10f, 30f, 0f), default, Quat.Identity);
            maps.Walk("w:1:369448", 4);
            var map = maps.ByPlace().Of("w:at:10:0");
            Assert.Equal(new[] { 4 }, map.Walked);
            Assert.Equal(new[] { 0, 3 }, map.Traced);
            Assert.Equal(1, map.Tally.Broken);
            Assert.Equal(new[] { 3 }, map.TraceRooms);
            Assert.Equal(new[] { 2 }, map.TraceDoors);
        }

        [Fact]
        public void ThePageAndTheMarkSayHowManyRoomsCameOfTraces()
        {
            Assert.Equal("One you have not found · on this server 7 of 12 rooms, from traces", DungeonMapWords.Line(new WalkLine { Server = (7, 12, 7) }));
            Assert.Equal("One you have not found · on this server 9 of 12 rooms, 7 from traces", DungeonMapWords.Line(new WalkLine { Server = (9, 12, 7) }));
            Assert.Equal("One you have not found · on this server 9 of 12 rooms", DungeonMapWords.Line(new WalkLine { Server = (9, 12, 0) }));
            Assert.Equal("<size=130%>Frost Caves</size>\n<size=115%><color=#abc>Walked on this server: 7 of 12 rooms, from traces</color></size>", DungeonMapWords.ServerLabel("Frost Caves", 7, 12, "abc", 7));
            Assert.Equal("<size=130%>Frost Caves</size>\n<size=115%><color=#abc>Walked on this server: 9 of 12 rooms, 7 from traces</color></size>", DungeonMapWords.ServerLabel("Frost Caves", 9, 12, "abc", 7));
            Assert.Equal("<size=130%>Frost Caves</size>\n<size=115%><color=#abc>Walked on this server: 9 of 12 rooms</color></size>", DungeonMapWords.ServerLabel("Frost Caves", 9, 12, "abc", 0));
        }

        [Fact]
        public void TheLinesTipSaysWhatWasLeftInsideAndThatWhoWalkedItIsNotKnown()
        {
            Assert.Equal("Read from what was left inside: 2 chests taken from, 3 things broken, 1 pickup taken, in 2 rooms, and the rooms on the way to them. Who walked it is not known.",
                DungeonMapWords.TraceTip(new TraceTally { Chests = 2, Broken = 3, Pickups = 1, Rooms = 2 }));
            Assert.Equal("Read from what was left inside: 1 rock mined, 2 doors opened, 4 spawn points woken, in 1 room, and the rooms on the way to them. Who walked it is not known. The latest pickup was taken on day 412.",
                DungeonMapWords.TraceTip(new TraceTally { Mined = 1, Doors = 2, Spawners = 4, Rooms = 1, LastDay = 412 }));
            Assert.Equal("Read from what was left inside: 2 doors opened, and the rooms on the way to them. Who walked it is not known. The latest pickup was taken on day 0.",
                DungeonMapWords.TraceTip(new TraceTally { Doors = 2, LastDay = 0 }));
            // Spawn points alone tell no room.
            Assert.Equal("Read from what was left inside: 1 spawn point woken, so someone was inside. Who walked it is not known.",
                DungeonMapWords.TraceTip(new TraceTally { Spawners = 1 }));
            Assert.Null(DungeonMapWords.TraceTip(null));
            // The line's hover says the traces, then the days anyone walked it.
            Assert.Equal("Read from what was left inside: 1 chest taken from, in 1 room, and the rooms on the way to them. Who walked it is not known. Anyone on this server last walked it on day 412",
                DungeonMapWords.ServerTip(new TraceTally { Chests = 1, Rooms = 1 }, -1, 412));
            Assert.Equal("Anyone on this server last walked it on day 412", DungeonMapWords.ServerTip(null, -1, 412));
            Assert.Null(DungeonMapWords.ServerTip(null, -1, -1));
        }

        [Fact]
        public void ReadingTracesAnswersHowManyDungeonsAndRoomsTheyTold()
        {
            Assert.Equal("the server read the traces in 38 dungeons of this world: 214 rooms walked, 96 of them on the way to a trace.", DungeonMapWords.TracesRead(38, 214, 96));
            Assert.Equal("the server read the traces in 1 dungeon of this world: 1 room walked, none on the way to a trace.", DungeonMapWords.TracesRead(1, 1, 0));
            Assert.Equal("the server found no trace of a visit in the dungeons of this world.", DungeonMapWords.TracesRead(0, 0, 0));
            Assert.Equal(" 2 dungeons made again of other rooms than those kept were passed over.", DungeonMapWords.OtherRooms(2));
            Assert.Equal(" 1 dungeon made again of other rooms than those kept was passed over.", DungeonMapWords.OtherRooms(1));
            Assert.Equal("", DungeonMapWords.OtherRooms(0));
            Assert.Equal("chests in the dungeons read: 120 made with their rooms, 100 standing, 90 filled, 3 taken from, 20 gone.", DungeonMapWords.ChestsRead(120, 100, 90, 3, 20));
        }
    }
}
