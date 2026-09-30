using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class DungeonLayoutTests
    {
        // An example is laid out by the rules DungeonGenerator builds a dungeon or camp by in a new zone:
        // a dungeon from a random entrance, room by room at random open doorways, then end caps
        // and dividers on what is left open and doors in the doorways between rooms; a camp on a
        // grid or scattered in a ring and walled. Rooms may not overlap nor leave the zone's box.

        private static Doorway Door(string type, float x, float z, float yaw, bool entrance = false) =>
            new Doorway { Type = type, Position = new Vec3(x, 0f, z), Rotation = Quat.Yaw(yaw), Entrance = entrance };

        private static RoomShape Entrance() => new RoomShape
        {
            Name = "entrance", Entrance = true, Size = new Vec3(8f, 4f, 8f),
            Doorways = { Door("d", 0f, 4f, 0f), Door("d", 0f, -4f, 180f, entrance: true) },
        };

        private static RoomShape Hall(string name = "hall", int minPlaceOrder = 0) => new RoomShape
        {
            Name = name, Size = new Vec3(8f, 4f, 8f), MinPlaceOrder = minPlaceOrder,
            Doorways = { Door("d", 0f, -4f, 180f), Door("d", 0f, 4f, 0f), Door("d", 4f, 0f, 90f), Door("d", -4f, 0f, 270f) },
        };

        /// <summary>An end cap without depth, which the generator places without a collision test.</summary>
        private static RoomShape Cap(string name = "cap", int prio = 0) => new RoomShape
        {
            Name = name, EndCap = true, EndCapPrio = prio, Size = new Vec3(4f, 4f, 0f),
            Doorways = { Door("d", 0f, 0f, 180f) },
        };

        private static DungeonSite Site(float zone = 64f) => new DungeonSite { ZoneSize = new Vec3(zone, zone, zone) };

        private static DungeonPlan Plan(int maxRooms = 20, int minRooms = 0) => new DungeonPlan { Algorithm = "Dungeon", MaxRooms = maxRooms, MinRooms = minRooms };

        private static DungeonExample Build(DungeonPlan plan, IEnumerable<RoomShape> rooms, int seed, DungeonSite site = null) =>
            DungeonLayout.Build(plan, rooms.ToList(), site ?? Site(), new Dice(seed));

        private static readonly int[] Seeds = Enumerable.Range(1, 40).ToArray();

        private static void Near(Vec3 expected, Vec3 actual) => Assert.True(Vec3.Distance(expected, actual) < 0.01f, $"expected {expected}, was {actual}");

        private static float Yaw(Quat q) => q.YawDegrees;

        /// <summary>Whether a turn is a whole number of sixteenths, as rooms and locations are turned.</summary>
        private static bool OnSixteenth(Quat q) => Math.Abs(Yaw(q) / 22.5 - Math.Round(Yaw(q) / 22.5)) < 0.001;

        /// <summary>A room's box on the ground, exact for turns by quarters, as the tests' rooms are turned.</summary>
        private static (float MinX, float MaxX, float MinZ, float MaxZ) Box(PlacedRoom room, float shrink = 0f)
        {
            var quarter = (int)Math.Round(Yaw(room.Rotation) / 90f) % 2 == 1;
            var x = (quarter ? room.Room.Size.Z : room.Room.Size.X) - shrink;
            var z = (quarter ? room.Room.Size.X : room.Room.Size.Z) - shrink;
            return (room.Position.X - x / 2f, room.Position.X + x / 2f, room.Position.Z - z / 2f, room.Position.Z + z / 2f);
        }

        /// <summary>Whether a room has a doorway meeting one of the other's, of one type, the two facing each other.</summary>
        private static bool Meets(PlacedRoom room, PlacedRoom other)
        {
            for (var i = 0; i < room.Room.Doorways.Count; i++)
            {
                for (var j = 0; j < other.Room.Doorways.Count; j++)
                {
                    if (other.Room.Doorways[j].Type != room.Room.Doorways[i].Type) continue;
                    if (Vec3.Distance(other.DoorwayAt(j), room.DoorwayAt(i)) > 0.01f) continue;
                    var facing = other.DoorwayTurn(j) * new Vec3(0f, 0f, 1f);
                    var back = room.DoorwayTurn(i) * new Vec3(0f, 0f, 1f);
                    if (Vec3.Dot(facing, back) < -0.99f) return true;
                }
            }
            return false;
        }

        /// <summary>How far in a room is along the rooms it was joined to, the entrance the first.</summary>
        private static int Depth(PlacedRoom room) => room.JoinedTo == null ? 1 : 1 + Depth(room.JoinedTo);

        // ----- A dungeon -----

        [Fact]
        public void ADungeonStartsWithAnEntranceWhoseEntranceDoorwaySitsAtTheGenerator()
        {
            var site = Site();
            site.Generator = new Vec3(3f, 0f, 5f);
            site.Turn = Quat.Yaw(90f);
            var example = Build(Plan(), new[] { Hall(), Entrance(), Cap() }, 7, site);

            var first = example.Rooms[0];
            Assert.True(first.Room.Entrance);
            var entrance = first.Room.Doorways.FindIndex(d => d.Entrance);
            Near(site.Generator, first.DoorwayAt(entrance));
            Assert.Equal(90f, Yaw(first.DoorwayTurn(entrance)), 2);
        }

        [Fact]
        public void NothingIsJoinedAtTheEntrancesWayIn()
        {
            // A dungeon may wrap round until a room stands beyond its way in, but none is joined there.
            foreach (var seed in Seeds)
            {
                var example = Build(Plan(), new[] { Entrance(), Hall(), Cap() }, seed);
                var entrance = example.Rooms[0];
                var wayIn = entrance.DoorwayAt(entrance.Room.Doorways.FindIndex(d => d.Entrance));
                Assert.All(example.Rooms.Where(r => r.JoinedTo == entrance), room =>
                    Assert.DoesNotContain(Enumerable.Range(0, room.Room.Doorways.Count), i => Vec3.Distance(room.DoorwayAt(i), wayIn) < 0.01f));
            }
        }

        [Fact]
        public void EveryRoomAfterTheEntranceJoinsADoorwayOfItsTypeFacingIt()
        {
            foreach (var seed in Seeds)
            {
                var example = Build(Plan(), new[] { Entrance(), Hall(), Cap() }, seed);
                Assert.True(example.Rooms.Count > 1);
                Assert.Null(example.Rooms[0].JoinedTo);
                Assert.All(example.Rooms.Skip(1), room =>
                {
                    Assert.NotNull(room.JoinedTo);
                    Assert.True(example.Rooms.IndexOf(room.JoinedTo) < example.Rooms.IndexOf(room));
                    Assert.True(Meets(room, room.JoinedTo), $"seed {seed}: {room.Room.Name} at {room.Position} does not meet the room it joined");
                });
            }
        }

        [Fact]
        public void NoRoomOverlapsOneBeforeIt()
        {
            foreach (var seed in Seeds)
            {
                var example = Build(Plan(40), new[] { Entrance(), Hall(), Cap() }, seed);
                for (var k = 0; k < example.Rooms.Count; k++)
                {
                    var later = example.Rooms[k];
                    if (later.Room.Size.X == 0f || later.Room.Size.Z == 0f) continue;
                    var a = Box(later, 0.1f);
                    for (var m = 0; m < k; m++)
                    {
                        var b = Box(example.Rooms[m]);
                        var apart = a.MaxX <= b.MinX || b.MaxX <= a.MinX || a.MaxZ <= b.MinZ || b.MaxZ <= a.MinZ;
                        Assert.True(apart, $"seed {seed}: {later.Room.Name} at {later.Position} overlaps {example.Rooms[m].Room.Name} at {example.Rooms[m].Position}");
                    }
                }
            }
        }

        [Fact]
        public void ARoomMayReachATwentiethOfAMetreIntoTheOneItJoins()
        {
            // The room placed is tested a tenth of a metre smaller than it is.
            var close = new RoomShape { Name = "close", Size = new Vec3(8f, 4f, 8f), Doorways = { Door("d", 0f, -3.95f, 180f) } };
            var example = Build(Plan(maxRooms: 1), new[] { Entrance(), close }, 1);
            Assert.Contains(example.Rooms, r => r.Room.Name == "close");
        }

        [Fact]
        public void EachRoomGoesAtAnyOpenDoorwayNotOnlyTheNewest()
        {
            var branches = Seeds.Sum(seed =>
            {
                var rooms = Build(Plan(), new[] { Entrance(), Hall(), Cap() }, seed, Site(1000f)).Rooms.Where(r => !r.Room.EndCap).ToList();
                return Enumerable.Range(2, Math.Max(0, rooms.Count - 2)).Count(k => rooms[k].JoinedTo != rooms[k - 1]);
            });
            Assert.True(branches > 0);
        }

        [Fact]
        public void AWeightedDungeonPicksRoomsByTheirWeights()
        {
            double RareShare(bool weighted)
            {
                var plan = Plan(20);
                plan.Weighted = weighted;
                var common = Hall("common");
                common.Weight = 9f;
                var rooms = Seeds.SelectMany(seed => Build(plan, new[] { Entrance(), common, Hall("rare"), Cap() }, seed, Site(1000f)).Rooms).ToList();
                return rooms.Count(r => r.Room.Name == "rare") / (double)rooms.Count(r => r.Room.Name == "rare" || r.Room.Name == "common");
            }
            Assert.InRange(RareShare(true), 0.05, 0.2);
            Assert.InRange(RareShare(false), 0.4, 0.6);
        }

        [Fact]
        public void AWeightedDungeonTriesItsEndCapsByWeightBeforeByPriority()
        {
            var heavy = Cap("heavy");
            heavy.Weight = 99f;
            var plan = Plan(10);
            plan.Weighted = true;
            var caps = Seeds.SelectMany(seed => Build(plan, new[] { Entrance(), Hall(), Cap("plain", prio: 5), heavy }, seed).Rooms.Where(r => r.Room.EndCap)).ToList();
            Assert.True(caps.Count(c => c.Room.Name == "heavy") > caps.Count / 2);
        }

        [Fact]
        public void EveryRoomStaysInsideTheZonesBox()
        {
            foreach (var seed in Seeds)
            {
                var example = Build(Plan(60), new[] { Entrance(), Hall(), Cap() }, seed, Site(40f));
                Assert.All(example.Rooms.Skip(1).Where(r => r.Room.Size.Z > 0f), room =>
                {
                    var box = Box(room);
                    Assert.True(box.MinX >= -20.001f && box.MaxX <= 20.001f && box.MinZ >= -20.001f && box.MaxZ <= 20.001f, $"{room.Room.Name} at {room.Position}");
                });
            }

            // Up and down too: a zone lower than the rooms keeps all but the entrance out.
            var low = Site(1000f);
            low.ZoneSize = new Vec3(1000f, 3f, 1000f);
            Assert.DoesNotContain(Build(Plan(), new[] { Entrance(), Hall(), Cap() }, 2, low).Rooms, r => r.Room.Name == "hall");
        }

        [Fact]
        public void EachTryAddsAtMostOneRoom()
        {
            // A try fails only where the doorway it picked faces a room already there.
            var halls = Seeds.Select(seed => Build(Plan(maxRooms: 5), new[] { Entrance(), Hall(), Cap() }, seed, Site(1000f)).Rooms.Count(r => r.Room.Name == "hall")).ToList();
            Assert.All(halls, count => Assert.InRange(count, 1, 5));
            Assert.Equal(5, halls.Max());
            Assert.Equal(0, Build(Plan(maxRooms: 0), new[] { Entrance(), Hall(), Cap() }, 1).Rooms.Count(r => r.Room.Name == "hall"));
        }

        [Fact]
        public void ARoomThatMustBeFarInNeverComesNearerTheEntrance()
        {
            // m_minPlaceOrder 3: only off a doorway of a room three rooms in, the entrance the first.
            var depths = new List<int>();
            foreach (var seed in Seeds)
            {
                var example = Build(Plan(30), new[] { Entrance(), Hall(), Hall("deep", minPlaceOrder: 3), Cap() }, seed, Site(1000f));
                depths.AddRange(example.Rooms.Where(r => r.Room.Name == "deep").Select(Depth));
            }
            Assert.NotEmpty(depths);
            Assert.Equal(4, depths.Min());
        }

        [Fact]
        public void ItStopsOnceItsRequiredRoomsAndOverItsFewestRoomsAreIn()
        {
            var plan = Plan(maxRooms: 200, minRooms: 3);
            plan.MinRequiredRooms = 1;
            plan.RequiredRooms.Add("boss");
            foreach (var seed in Seeds)
            {
                var example = Build(plan, new[] { Entrance(), Hall(), Hall("boss"), Cap() }, seed, Site(1000f));
                var built = example.Rooms.Where(r => !r.Room.EndCap).ToList();
                bool Done(List<PlacedRoom> rooms) => rooms.Any(r => r.Room.Name == "boss") && rooms.Count > 3;
                Assert.True(Done(built));
                Assert.False(Done(built.Take(built.Count - 1).ToList()));
            }
        }

        [Fact]
        public void EveryDoorwayLeftOpenIsClosedByAnEndCap()
        {
            foreach (var seed in Seeds)
            {
                var example = Build(Plan(15), new[] { Entrance(), Hall(), Cap() }, seed, Site(1000f));
                foreach (var room in example.Rooms.Where(r => !r.Room.EndCap))
                {
                    for (var i = 0; i < room.Room.Doorways.Count; i++)
                    {
                        if (room.Room.Doorways[i].Entrance) continue;
                        var at = room.DoorwayAt(i);
                        var met = example.Rooms.Where(o => o != room).Any(o => Enumerable.Range(0, o.Room.Doorways.Count).Any(j => Vec3.Distance(o.DoorwayAt(j), at) < 0.01f));
                        Assert.True(met, $"seed {seed}: {room.Room.Name}'s doorway at {at} is open");
                    }
                }
            }
        }

        [Fact]
        public void AnEndCapNeverClosesADoorwayBetweenTwoRooms()
        {
            foreach (var seed in Seeds)
            {
                var example = Build(Plan(15), new[] { Entrance(), Hall(), Cap() }, seed, Site(1000f));
                var rooms = example.Rooms.Where(r => !r.Room.EndCap).ToList();
                foreach (var cap in example.Rooms.Where(r => r.Room.EndCap))
                {
                    var at = cap.DoorwayAt(0);
                    var there = rooms.Count(r => Enumerable.Range(0, r.Room.Doorways.Count).Any(i => !r.Room.Doorways[i].Entrance && Vec3.Distance(r.DoorwayAt(i), at) < 0.01f));
                    Assert.Equal(1, there);
                }
            }
        }

        [Fact]
        public void TheEndCapOfHighestPriorityIsTriedFirst()
        {
            var example = Build(Plan(10), new[] { Entrance(), Hall(), Cap("plain"), Cap("fancy", prio: 5) }, 3);
            Assert.Contains(example.Rooms, r => r.Room.EndCap);
            Assert.All(example.Rooms.Where(r => r.Room.EndCap), r => Assert.Equal("fancy", r.Room.Name));
        }

        [Fact]
        public void EndCapsOfOnePriorityAreTriedInTheOrderTheGamesShuffleLeaves()
        {
            // ShuffleClass.Shuffle with Unity's dice swaps each place with one before it, never
            // itself, so two end caps always swap.
            var example = Build(Plan(10), new[] { Entrance(), Hall(), Cap("first"), Cap("second") }, 3);
            Assert.Contains(example.Rooms, r => r.Room.EndCap);
            Assert.All(example.Rooms.Where(r => r.Room.EndCap), r => Assert.Equal("second", r.Room.Name));
        }

        private static RoomShape Side(string name, string type) => new RoomShape
        {
            Name = name, Size = new Vec3(8f, 4f, 8f), Doorways = { Door(type, 4f, 0f, 90f) },
        };

        private static RoomShape Divider() => new RoomShape
        {
            Name = "divider", Divider = true, Size = new Vec3(4f, 4f, 0f), Doorways = { Door("a", 0f, 0f, 0f), Door("b", 0f, 0f, 180f) },
        };

        [Fact]
        public void DoorwaysOfTwoTypesThatMeetGetADividerBetweenThem()
        {
            var layout = new DungeonLayout(Plan(), new[] { Divider() }, Site(), new Dice(1));
            layout.Place(Side("left", "a"), new Vec3(-4f, 0f, 0f), Quat.Identity);
            layout.Place(Side("right", "b"), new Vec3(4f, 0f, 0f), Quat.Yaw(180f));

            layout.PlaceEndCaps();

            var divider = Assert.Single(layout.Placed, r => r.Room.Divider);
            Near(new Vec3(0f, 0f, 0f), divider.DoorwayAt(0));
            Assert.Equal(90f, Yaw(divider.DoorwayTurn(0)), 2);
        }

        [Fact]
        public void DoorwaysOfOneTypeThatMeetAreLeftAsTheyAre()
        {
            var layout = new DungeonLayout(Plan(), new[] { Divider(), Cap() }, Site(), new Dice(1));
            layout.Place(Side("left", "d"), new Vec3(-4f, 0f, 0f), Quat.Identity);
            layout.Place(Side("right", "d"), new Vec3(4f, 0f, 0f), Quat.Yaw(180f));

            layout.PlaceEndCaps();

            Assert.Equal(2, layout.Placed.Count);
        }

        // ----- Doors -----

        private static DungeonPlan Doors(float dungeonChance, float ownChance, string type = "d")
        {
            var plan = Plan(8);
            plan.DoorChance = dungeonChance;
            plan.Doors.Add((type, ownChance));
            return plan;
        }

        [Fact]
        public void ADoorwayBetweenRoomsGetsADoorAtTheDungeonsChanceOrItsTypesOwn()
        {
            foreach (var seed in Seeds.Take(10))
            {
                var joins = Build(Doors(1f, 0f), new[] { Entrance(), Hall(), Cap() }, seed).Rooms.Count(r => r.Room.Name == "hall");
                Assert.Equal(joins, Build(Doors(1f, 0f), new[] { Entrance(), Hall(), Cap() }, seed).Doors.Count);
                Assert.Empty(Build(Doors(0f, 0f), new[] { Entrance(), Hall(), Cap() }, seed).Doors);
                Assert.Equal(joins, Build(Doors(0f, 1f), new[] { Entrance(), Hall(), Cap() }, seed).Doors.Count);
                Assert.Empty(Build(Doors(1f, 0f, type: "other"), new[] { Entrance(), Hall(), Cap() }, seed).Doors);
            }
        }

        [Fact]
        public void ADoorwayThatAllowsNoDoorGetsNone()
        {
            var closed = Hall();
            foreach (var doorway in closed.Doorways) doorway.AllowDoor = false;
            var entrance = Entrance();
            entrance.Doorways[0].AllowDoor = false;
            Assert.Empty(Build(Doors(1f, 0f), new[] { entrance, closed, Cap() }, 5).Doors);

            // One that allows a door only if the other side does too.
            var picky = Entrance();
            picky.Doorways[0].DoorOnlyIfOtherAllows = true;
            var example = Build(Doors(1f, 0f), new[] { picky, closed, Cap() }, 5);
            Assert.DoesNotContain(example.Doors, d => Vec3.Distance(d, example.Rooms[0].DoorwayAt(0)) < 0.01f);
        }

        // ----- Camps -----

        private static RoomShape Tent(string name = "tent", bool faceCenter = false, bool perimeter = false, float depth = 2f) => new RoomShape
        {
            Name = name, Size = new Vec3(2f, 2f, depth), FaceCenter = faceCenter, Perimeter = perimeter,
        };

        [Fact]
        public void AGridCampPutsARoomOnEverySquareAtItsChance()
        {
            var plan = new DungeonPlan { Algorithm = "CampGrid", GridSize = 3, TileWidth = 10f, SpawnChance = 1f };
            var rooms = new[] { Tent(), Tent("wall", perimeter: true), Cap() };

            var example = Build(plan, rooms, 4);

            Assert.Equal(9, example.Rooms.Count);
            Assert.All(example.Rooms, r => Assert.Equal("tent", r.Room.Name));
            var spots = example.Rooms.Select(r => (Math.Round(r.Position.X), Math.Round(r.Position.Z))).OrderBy(p => p).ToList();
            var squares = (from i in new[] { -15, -5, 5 } from j in new[] { -15, -5, 5 } select ((double)j, (double)i)).OrderBy(p => p).ToList();
            Assert.Equal(squares, spots);
            Assert.All(example.Rooms, r => Assert.True(OnSixteenth(r.Rotation)));

            plan.SpawnChance = 0f;
            Assert.Empty(Build(plan, rooms, 4).Rooms);
        }

        [Fact]
        public void ARingCampScattersRoomsInsideItsRingAndWallsItRound()
        {
            var plan = new DungeonPlan { Algorithm = "CampRadial", MinRooms = 5, MaxRooms = 8, CampRadiusMin = 20f, CampRadiusMax = 29f, PerimeterBuffer = 2f, PerimeterSections = 12 };
            var rooms = new[] { Tent(), Tent("hut", faceCenter: true), Tent("wall", faceCenter: true, perimeter: true, depth: 1f) };
            var walled = 0;
            foreach (var seed in Seeds)
            {
                var example = Build(plan, rooms, seed);
                var inside = example.Rooms.Where(r => !r.Room.Perimeter).ToList();
                var wall = example.Rooms.Where(r => r.Room.Perimeter).ToList();

                Assert.InRange(inside.Count, 1, 7);
                Assert.All(inside, r => Assert.True(Vec3.Distance(r.Position, new Vec3(0f, 0f, 0f)) <= 27.001f));
                Assert.InRange(wall.Count, 0, 12);
                if (wall.Count > 0)
                {
                    var ring = Vec3.Distance(wall[0].Position, new Vec3(0f, 0f, 0f));
                    Assert.InRange(ring, 20f, 29f);
                    Assert.All(wall, r => Assert.Equal(ring, Vec3.Distance(r.Position, new Vec3(0f, 0f, 0f)), 2));
                    walled++;
                }

                // A room that faces the centre does, to the nearest sixteenth of a turn.
                Assert.All(example.Rooms.Where(r => r.Room.FaceCenter), r =>
                {
                    var forward = r.Rotation * new Vec3(0f, 0f, 1f);
                    var toCentre = new Vec3(-r.Position.X, 0f, -r.Position.Z);
                    var cos = (forward.X * toCentre.X + forward.Z * toCentre.Z) / toCentre.Length;
                    Assert.True(cos >= Math.Cos(11.26 * Math.PI / 180.0), $"{r.Room.Name} at {r.Position} faces {forward}");
                });
            }
            Assert.True(walled > 0);
        }

        // ----- Where it is built -----

        [Fact]
        public void ADungeonWithAnInteriorOfItsOwnStandsWhereItsLocationPutsIt()
        {
            var plan = new DungeonPlan { CustomInterior = true, ZoneFromGenerator = new Vec3(-10f, -5f, 20f), GeneratorTurn = Quat.Yaw(90f) };

            var site = DungeonLayout.Site(plan, 12f, true, new Dice(1));

            Near(new Vec3(0f, 0f, 0f), site.Generator);
            Near(new Vec3(-10f, -5f, 20f), site.ZoneCenter);
            Assert.Equal(90f, Yaw(site.Turn), 2);
        }

        [Fact]
        public void AnyOtherIsSomewhereInItsZoneAtOneOfSixteenTurns()
        {
            var plan = new DungeonPlan { GeneratorAt = new Vec3(0f, 5000f, 3f) };
            var turns = new HashSet<float>();
            foreach (var seed in Seeds)
            {
                var site = DungeonLayout.Site(plan, 12f, true, new Dice(seed));
                var location = site.Generator - site.Turn * plan.GeneratorAt;
                Assert.InRange(location.X, -20f, 20f);
                Assert.InRange(location.Z, -20f, 20f);
                Near(new Vec3(0f, site.Generator.Y, 0f), site.ZoneCenter);
                Assert.True(OnSixteenth(site.Turn));
                turns.Add((float)Math.Round(Yaw(site.Turn)));
            }
            Assert.True(turns.Count > 4);
            Assert.Equal(0f, Yaw(DungeonLayout.Site(plan, 12f, false, new Dice(3)).Turn), 2);
        }

        // ----- Boxes and the plan -----

        [Fact]
        public void BoxesThatOnlyTouchDoNotOverlap()
        {
            var size = new Vec3(8f, 4f, 8f);
            Assert.False(Boxes.Overlap(new Vec3(0f, 0f, 0f), Quat.Identity, size, new Vec3(8f, 0f, 0f), Quat.Identity, size));
            Assert.True(Boxes.Overlap(new Vec3(0f, 0f, 0f), Quat.Identity, size, new Vec3(7.9f, 0f, 0f), Quat.Identity, size));
            Assert.False(Boxes.Overlap(new Vec3(0f, 0f, 0f), Quat.Identity, size, new Vec3(0f, 4.1f, 0f), Quat.Identity, size));
        }

        [Fact]
        public void ATurnedBoxReachesAsFarAsItsCorners()
        {
            var room = new Vec3(8f, 4f, 8f);
            var post = new Vec3(2f, 2f, 2f);
            Assert.False(Boxes.Overlap(new Vec3(0f, 0f, 0f), Quat.Identity, room, new Vec3(5.2f, 0f, 0f), Quat.Identity, post));
            Assert.True(Boxes.Overlap(new Vec3(0f, 0f, 0f), Quat.Yaw(45f), room, new Vec3(5.2f, 0f, 0f), Quat.Identity, post));
        }

        [Fact]
        public void BoxesKeptApartOnlyEdgeToEdgeDoNotOverlap()
        {
            // No face of either keeps these apart; an edge of each, crossing, does.
            var cube = new Vec3(1f, 1f, 1f);
            var half = 22.5 * Math.PI / 180.0;
            var tilted = Quat.Yaw(45f) * new Quat((float)Math.Sin(half), 0f, 0f, (float)Math.Cos(half));
            Assert.False(Boxes.Overlap(new Vec3(0f, 0f, 0f), Quat.Identity, cube, new Vec3(1.05f, 1.05f, 0f), tilted, cube));
            Assert.True(Boxes.Overlap(new Vec3(0f, 0f, 0f), Quat.Identity, cube, new Vec3(0.9f, 0.9f, 0f), tilted, cube));
        }

        [Fact]
        public void ATiltedPlateKeptApartOnlyByItsOwnFaceDoesNotOverlap()
        {
            // Only the plate's broad face keeps it off the cube's corner, by a few centimetres.
            var cube = new Vec3(1f, 1f, 1f);
            var plate = new Vec3(3f, 0.2f, 3f);
            var tilt = new Quat(0.476858f, 0.150353f, 0.264227f, 0.824733f);
            var off = new Vec3(-0.26904f, 0.37313f, 0.79673f);
            Assert.False(Boxes.Overlap(new Vec3(0f, 0f, 0f), Quat.Identity, cube, off, tilt, plate));
            Assert.True(Boxes.Overlap(new Vec3(0f, 0f, 0f), Quat.Identity, cube, off * 0.9f, tilt, plate));

            // Either box may be the one whose face does it.
            Assert.False(Boxes.Overlap(off, tilt, plate, new Vec3(0f, 0f, 0f), Quat.Identity, cube));
        }

        [Fact]
        public void APointOnThePlanFindsTheRoomItIsIn()
        {
            var low = new PlacedRoom { Room = new RoomShape { Name = "low", Size = new Vec3(8f, 4f, 8f) }, Position = new Vec3(0f, 0f, 0f), Rotation = Quat.Identity };
            var high = new PlacedRoom { Room = new RoomShape { Name = "high", Size = new Vec3(2f, 4f, 10f) }, Position = new Vec3(0f, 6f, 0f), Rotation = Quat.Yaw(90f) };
            var example = new DungeonExample { Site = Site(), Rooms = { low, high } };

            Assert.Same(low, example.RoomAt(3f, 3f));
            Assert.Same(high, example.RoomAt(4.5f, 0f));
            Assert.Same(high, example.RoomAt(0f, 0f));
            Assert.Null(example.RoomAt(5f, 5f));
        }

        [Fact]
        public void ThePlanTakesInTheZoneAndEveryRoom()
        {
            var outside = new PlacedRoom { Room = new RoomShape { Size = new Vec3(4f, 4f, 4f) }, Position = new Vec3(40f, 0f, 0f), Rotation = Quat.Identity };
            var example = new DungeonExample { Site = Site(), Rooms = { outside } };

            var extent = example.Extent();

            Assert.Equal((-32f, 42f, -32f, 32f), (extent.MinX, extent.MaxX, extent.MinZ, extent.MaxZ));
        }
    }
}
