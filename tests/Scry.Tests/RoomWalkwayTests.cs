using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class RoomWalkwayTests
    {
        /// <summary>A room 12 m long (along its own X) and 6 m across, its middle at a point, turned as given, with doorways at points of its own.</summary>
        private static PlacedRoom Hall(float yaw = 0f, float x = 0f, float z = 0f, float wide = 12f, float deep = 6f, params Vec3[] doorways)
        {
            var shape = new RoomShape { Name = "hall", Size = new Vec3(wide, 4f, deep) };
            foreach (var at in doorways) shape.Doorways.Add(new Doorway { Position = at });
            return new PlacedRoom { Room = shape, Position = new Vec3(x, 0f, z), Rotation = Quat.Yaw(yaw) };
        }

        private static Vec3 At(float x, float z) => new Vec3(x, 0f, z);

        /// <summary>A point of a room's own, where it stands in the example.</summary>
        private static Vec3 In(PlacedRoom room, float x, float z) => room.Position + room.Rotation * At(x, z);

        private static void Same(Vec3 expected, Vec3 actual)
        {
            Assert.True(Math.Abs(expected.X - actual.X) < 0.001f && Math.Abs(expected.Z - actual.Z) < 0.001f, $"expected {expected}, found {actual}");
        }

        private static void Same(IReadOnlyList<Vec3> expected, IReadOnlyList<Vec3> actual)
        {
            Assert.True(expected.Count == actual.Count, $"expected {string.Join(" ", expected)}, found {string.Join(" ", actual)}");
            for (var i = 0; i < expected.Count; i++) Same(expected[i], actual[i]);
        }

        /// <summary>Whether a line runs along one of a room's walls, as seen from above.</summary>
        private static bool AlongWalls(PlacedRoom room, Vec3 a, Vec3 b)
        {
            var back = room.Rotation.Inverse();
            var d = back * (b - a);
            return Math.Abs(d.X) < 0.001f || Math.Abs(d.Z) < 0.001f;
        }

        // ----- A doorway joined to the room's hall -----

        [Fact]
        public void ADoorwayInALongWallGoesStraightInToTheHall()
        {
            var room = Hall();
            Same(new[] { At(3f, 3f), At(3f, 0f) }, RoomWalkway.Stub(room, At(3f, 3f)));
            Same(At(3f, 0f), RoomWalkway.Foot(room, At(3f, 3f)));
        }

        [Fact]
        public void ADoorwayAtTheHallsEndIsOnItAlready()
        {
            var room = Hall();
            Same(new[] { At(6f, 0f) }, RoomWalkway.Stub(room, At(6f, 0f)));
            Same(At(6f, 0f), RoomWalkway.Foot(room, At(6f, 0f)));
        }

        [Fact]
        public void ADoorwayInAnEndWallBesideTheHallStepsInThenTurnsToIt()
        {
            // At most three metres in, so the turn stands inside the room, not in the doorway.
            var room = Hall();
            Same(new[] { At(6f, 2f), At(3f, 2f), At(3f, 0f) }, RoomWalkway.Stub(room, At(6f, 2f)));
            Same(new[] { At(-6f, -2f), At(-3f, -2f), At(-3f, 0f) }, RoomWalkway.Stub(room, At(-6f, -2f)));
        }

        [Fact]
        public void InASmallRoomTheStepInIsHalfWayToTheMiddle()
        {
            var room = Hall(wide: 4f, deep: 3f);
            Same(new[] { At(2f, 1f), At(1f, 1f), At(1f, 0f) }, RoomWalkway.Stub(room, At(2f, 1f)));
        }

        [Fact]
        public void AHoleInTheFloorJoinsTheHallSquareToItsWalls()
        {
            var room = Hall();
            Same(new[] { At(2f, 1.5f), At(2f, 0f) }, RoomWalkway.Stub(room, At(2f, 1.5f)));
        }

        [Fact]
        public void TheHallRunsAlongTheLongerSideOfARoomHoweverItIsTurned()
        {
            // Turned a quarter, its longer side runs the other way in the example.
            var room = Hall(yaw: 90f, x: 10f, z: -4f);
            Same(new[] { In(room, 3f, 3f), In(room, 3f, 0f) }, RoomWalkway.Stub(room, In(room, 3f, 3f)));
            var (from, to) = RoomWalkway.HallOf(room, new[] { In(room, -6f, 0f), In(room, 6f, 0f) });
            Same(In(room, -6f, 0f), from);
            Same(In(room, 6f, 0f), to);
        }

        [Fact]
        public void ADeepRoomsHallRunsAlongItsDepth()
        {
            var room = Hall(wide: 6f, deep: 12f);
            Same(new[] { At(3f, 2f), At(0f, 2f) }, RoomWalkway.Stub(room, At(3f, 2f)));
            Same(new[] { At(0f, 6f) }, RoomWalkway.Stub(room, At(0f, 6f)));
        }

        // ----- The hall -----

        [Fact]
        public void TheHallReachesTheFarthestFeetEitherWay()
        {
            var room = Hall();
            var (from, to) = RoomWalkway.HallOf(room, new[] { At(-4f, 3f), At(2f, -3f), At(6f, 0f) });
            Same(At(-4f, 0f), from);
            Same(At(6f, 0f), to);
        }

        [Fact]
        public void TheHallAlwaysReachesTheRoomsMiddle()
        {
            // A dead end's one doorway still leads to where the room is.
            var room = Hall();
            var (from, to) = RoomWalkway.HallOf(room, new[] { At(6f, 0f) });
            Same(At(0f, 0f), from);
            Same(At(6f, 0f), to);
        }

        [Fact]
        public void ASquareCrossroadsWithADoorwayInEachWallIsACrossThroughItsMiddle()
        {
            var room = Hall(wide: 12f, deep: 12f);
            Same(new[] { At(0f, 6f), At(0f, 0f) }, RoomWalkway.Stub(room, At(0f, 6f)));
            Same(new[] { At(0f, -6f), At(0f, 0f) }, RoomWalkway.Stub(room, At(0f, -6f)));
            var (from, to) = RoomWalkway.HallOf(room, new[] { At(0f, 6f), At(-6f, 0f), At(0f, -6f), At(6f, 0f) });
            Same(At(-6f, 0f), from);
            Same(At(6f, 0f), to);
        }

        // ----- Through a room -----

        [Fact]
        public void TheWayThroughARoomGoesInAlongTheHallAndOut()
        {
            var room = Hall();
            Same(new[] { At(-3f, 3f), At(-3f, 0f), At(3f, 0f), At(3f, -3f) }, RoomWalkway.Through(room, At(-3f, 3f), At(3f, -3f)));
        }

        [Fact]
        public void TheWayIntoTheRoomItEndsInEndsAtItsMiddle()
        {
            var room = Hall();
            Same(new[] { At(6f, 2f), At(3f, 2f), At(3f, 0f), At(0f, 0f) }, RoomWalkway.Through(room, At(6f, 2f), null));
        }

        [Fact]
        public void TheWayFromTheRoomItStartsInStartsAtItsMiddle()
        {
            var room = Hall();
            Same(new[] { At(0f, 0f), At(-3f, 0f), At(-3f, 3f) }, RoomWalkway.Through(room, null, At(-3f, 3f)));
        }

        [Fact]
        public void AWayInAndOutThroughTheSameFootDoesNotDoubleBack()
        {
            var room = Hall(wide: 12f, deep: 12f);
            Same(new[] { At(0f, 6f), At(0f, 0f), At(0f, -6f) }, RoomWalkway.Through(room, At(0f, 6f), At(0f, -6f)));
        }

        [Fact]
        public void EveryLineOfAWalkwayRunsAlongTheRoomsWallsHoweverTheRoomIsTurned()
        {
            foreach (var yaw in new[] { 0f, 90f, 180f, 270f, 30f })
            {
                var room = Hall(yaw: yaw, x: 5f, z: 7f);
                var doors = new[] { In(room, 6f, 2f), In(room, -6f, 0f), In(room, 2f, 3f), In(room, -4f, -3f), In(room, 1f, 0.5f) };
                foreach (var a in doors)
                {
                    var stub = RoomWalkway.Stub(room, a);
                    for (var i = 1; i < stub.Count; i++) Assert.True(AlongWalls(room, stub[i - 1], stub[i]), $"turned {yaw}: {stub[i - 1]} to {stub[i]}");
                    foreach (var b in doors)
                    {
                        var way = RoomWalkway.Through(room, a, b);
                        for (var i = 1; i < way.Count; i++) Assert.True(AlongWalls(room, way[i - 1], way[i]), $"turned {yaw}: {way[i - 1]} to {way[i]}");
                    }
                }
                var (from, to) = RoomWalkway.HallOf(room, doors);
                Assert.True(AlongWalls(room, from, to));
            }
        }

        // ----- A dungeon's walkways -----

        /// <summary>An entrance 12 by 6 with a doorway east into a corridor and one north into a room, and a third room joined to both, closing a loop.</summary>
        private static (DungeonWays Ways, PlacedRoom Entrance, PlacedRoom East, PlacedRoom North, PlacedRoom Corner) Loop()
        {
            var entrance = Hall(doorways: new[] { At(0f, -3f), At(6f, 0f), At(0f, 3f) });
            entrance.PlaceOrder = 0;
            var east = Hall(x: 12f, doorways: new[] { At(-6f, 0f), At(-1.5f, 3f) });
            east.PlaceOrder = 1;
            var north = Hall(z: 6f, wide: 6f, deep: 6f, doorways: new[] { At(0f, -3f), At(3f, 0f) });
            north.PlaceOrder = 2;
            var corner = Hall(x: 7.5f, z: 6f, wide: 9f, deep: 6f, doorways: new[] { At(-4.5f, 0f), At(3f, -3f) });
            corner.PlaceOrder = 3;
            var rooms = new[] { entrance, east, north, corner };
            return (DungeonWays.Of(rooms), entrance, east, north, corner);
        }

        [Fact]
        public void EveryWayIsDrawnThroughTheDoorwayItsRoomsShare()
        {
            var (ways, entrance, east, north, corner) = Loop();
            var walkways = PlanWalkways.Of(ways);
            foreach (var (a, b) in new[] { (entrance, east), (entrance, north), (north, corner), (east, corner) })
            {
                var door = ways.DoorBetween(a, b);
                Assert.True(door.HasValue, $"{a.Room.Name} and {b.Room.Name} share no doorway");
                // A doorway at the end of a hall is reached by the hall itself.
                Assert.Contains(walkways.Lines, line => (line.Room == a || line.Room == b)
                    && (Vec3.Distance(line.From, door.Value) < 0.001f || Vec3.Distance(line.To, door.Value) < 0.001f));
            }
        }

        [Fact]
        public void EachRoomHasItsHallOnceAndEveryLineAlongItsWalls()
        {
            var (ways, entrance, east, north, corner) = Loop();
            var walkways = PlanWalkways.Of(ways);
            foreach (var room in new[] { entrance, east, corner }) Assert.Single(walkways.Lines, line => line.Room == room && line.Way == (null, null));
            // The north room's one way not closing a loop meets its hall at its middle: a hall of no length, drawn as none.
            Assert.DoesNotContain(walkways.Lines, line => line.Room == north && line.Way == (null, null));
            foreach (var room in new[] { entrance, east, north, corner })
            {
                Assert.All(walkways.Lines.Where(line => line.Room == room), line => Assert.True(AlongWalls(room, line.From, line.To), $"{room.Room.Name}: {line.From} to {line.To}"));
            }
        }

        [Fact]
        public void AWayClosingALoopIsMarkedAsOne()
        {
            var (ways, entrance, east, north, corner) = Loop();
            var walkways = PlanWalkways.Of(ways);
            var loop = ways.IsLoop(north, corner) ? (north, corner) : (east, corner);
            Assert.True(ways.IsLoop(loop.Item1, loop.Item2));
            Assert.Contains(walkways.Lines, line => line.Way == loop || line.Way == (loop.Item2, loop.Item1));
            Assert.All(walkways.Lines.Where(line => line.Way == loop || line.Way == (loop.Item2, loop.Item1)), line => Assert.True(line.Loop));
            Assert.All(walkways.Lines.Where(line => line.Way == (entrance, east) || line.Way == (east, entrance)), line => Assert.False(line.Loop));
        }

        [Fact]
        public void AHallRunsOnlyAsFarAsTheWaysNotClosingALoopTheRestDrawnAsTheLoop()
        {
            // The corner room's way west closes the loop: its doorway lies beyond the hall its way south gives it.
            var (ways, _, _, north, corner) = Loop();
            Assert.True(ways.IsLoop(north, corner));
            var walkways = PlanWalkways.Of(ways);
            var hall = walkways.Lines.Single(line => line.Room == corner && line.Way == (null, null));
            Same(corner.Position, hall.From);
            Same(In(corner, 3f, 0f), hall.To);
            var beyond = walkways.Lines.Single(line => line.Room == corner && line.Way == (corner, north));
            Assert.True(beyond.Loop);
            Same(corner.Position, beyond.From);
            Same(In(corner, -4.5f, 0f), beyond.To);

            // The north room's way east closes it too, past the other end of its hall.
            var north_ = walkways.Lines.Single(line => line.Room == north && line.Way == (north, corner));
            Assert.True(north_.Loop);
            Same(north.Position, north_.From);
            Same(In(north, 3f, 0f), north_.To);
        }

        [Fact]
        public void AWayClosingALoopThroughASideWallIsDashedAllTheWayToEachHall()
        {
            // A long room north of two side by side, joined to each through its south wall.
            var a = Hall(doorways: new[] { At(6f, 0f), At(3f, 3f) });
            a.PlaceOrder = 0;
            var b = Hall(x: 12f, doorways: new[] { At(-6f, 0f), At(-3f, 3f) });
            b.PlaceOrder = 1;
            var c = Hall(x: 6f, z: 6f, wide: 18f, doorways: new[] { At(-3f, -3f), At(3f, -3f) });
            c.PlaceOrder = 2;
            var ways = DungeonWays.Of(new[] { a, b, c });
            Assert.True(ways.IsLoop(b, c));
            var walkways = PlanWalkways.Of(ways);
            var loop = walkways.Lines.Where(line => line.Way == (b, c) || line.Way == (c, b)).ToList();
            // Each room's stub, and the north room's hall on past its way in to the loop's foot.
            Assert.Equal(3, loop.Count);
            Assert.All(loop, line => Assert.True(line.Loop));
        }

        [Fact]
        public void TheWayFromTheEntranceRunsFromItsMiddleThroughEachSharedDoorwayToTheRoomsMiddle()
        {
            var (ways, entrance, east, _, corner) = Loop();
            var route = new List<PlacedRoom> { entrance, east, corner };
            var walkway = PlanWalkways.Of(ways).Route(route);
            Same(At(0f, 0f), walkway[0]);
            Same(corner.Position, walkway[walkway.Count - 1]);
            foreach (var (a, b) in new[] { (entrance, east), (east, corner) })
            {
                var door = ways.DoorBetween(a, b).Value;
                Assert.Contains(walkway, p => Vec3.Distance(p, door) < 0.001f);
            }
            for (var i = 1; i < walkway.Count; i++) Assert.True(Vec3.Distance(walkway[i - 1], walkway[i]) > 0.001f, "no point twice in a row");
            // A room passed through is walked along its hall from doorway to doorway, not by way of its middle.
            Assert.DoesNotContain(walkway, p => Vec3.Distance(p, east.Position) < 0.001f);
        }

        [Fact]
        public void TheWayToTheEntranceItselfIsItsMiddle()
        {
            var (ways, entrance, _, _, _) = Loop();
            var walkway = PlanWalkways.Of(ways).Route(new List<PlacedRoom> { entrance });
            Same(new[] { entrance.Position }, walkway);
        }

        [Fact]
        public void EachPointOfTheWayFromTheEntranceKnowsTheRoomItRunsIn()
        {
            // So the levels can drop the way from one plate to the next at the doorway it goes through.
            var (ways, entrance, east, _, corner) = Loop();
            var route = new List<PlacedRoom> { entrance, east, corner };
            var walkways = PlanWalkways.Of(ways);
            var walkway = walkways.RouteIn(route);
            Same(walkways.Route(route), walkway.Select(p => p.Point).ToList());
            Assert.Same(entrance, walkway[0].Room);
            Assert.Same(corner, walkway[walkway.Count - 1].Room);
            // The doorway two rooms share is the first room's, where the way leaves it.
            var door = ways.DoorBetween(entrance, east).Value;
            Assert.Same(entrance, walkway.First(p => Vec3.Distance(p.Point, door) < 0.001f).Room);
            // The rooms come in the route's order, each once, a run of points each.
            var runs = new List<PlacedRoom>();
            foreach (var point in walkway) if (runs.Count == 0 || runs[runs.Count - 1] != point.Room) runs.Add(point.Room);
            Assert.Equal(route, runs);
        }
    }
}
