using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>Dice rolled as Unity's <c>Random</c> rolls them, from a seed of their own.</summary>
    internal sealed class Dice
    {
        private readonly Random _random;

        public Dice(int seed) => _random = new Random(seed);

        /// <summary>From 0 to 1, as <c>Random.value</c>.</summary>
        public float Value => (float)_random.NextDouble();

        /// <summary>A whole number from min up to but not max, as <c>Random.Range(int, int)</c>; min when max is not above it.</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(_random.NextDouble() * (max - min));

        /// <summary>A number from min to max, as <c>Random.Range(float, float)</c>.</summary>
        public float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
    }

    /// <summary>Where a dungeon or camp is built: its generator's place and turn, and the box of its zone that its rooms must stay inside.</summary>
    internal sealed class DungeonSite
    {
        public Vec3 Generator;
        public Quat Turn = Quat.Identity;
        public Vec3 ZoneCenter;
        public Vec3 ZoneSize = new Vec3(64f, 64f, 64f);
    }

    /// <summary>A room as an example places it: where, how it is turned, and how far in from the entrance.</summary>
    internal sealed class PlacedRoom
    {
        public RoomShape Room;
        public Vec3 Position;
        public Quat Rotation = Quat.Identity;

        /// <summary>How many rooms in from the entrance, the entrance the first (<c>Room.m_placeOrder</c>); 0 in a camp.</summary>
        public int PlaceOrder;

        /// <summary>The room it was joined to at a doorway of that room's; none for the entrance and a camp's rooms.</summary>
        public PlacedRoom JoinedTo;

        public Vec3 DoorwayAt(int index) => Position + Rotation * Room.Doorways[index].Position;

        public Quat DoorwayTurn(int index) => Rotation * Room.Doorways[index].Rotation;

        /// <summary>Its floor's corners as seen from above, in order round it.</summary>
        public Vec3[] Corners()
        {
            var x = Room.Size.X / 2f;
            var z = Room.Size.Z / 2f;
            var rotation = Rotation;
            var position = Position;
            return new[] { new Vec3(-x, 0f, -z), new Vec3(x, 0f, -z), new Vec3(x, 0f, z), new Vec3(-x, 0f, z) }
                .Select(c => position + rotation * c).ToArray();
        }

        /// <summary>Whether a point on the ground is on its floor, as seen from above.</summary>
        public bool Covers(float x, float z)
        {
            var local = Rotation.Inverse() * new Vec3(x - Position.X, 0f, z - Position.Z);
            return Math.Abs(local.X) <= Room.Size.X / 2f && Math.Abs(local.Z) <= Room.Size.Z / 2f;
        }
    }

    /// <summary>One way a dungeon or camp can come out: its rooms in the order placed, and where doors went.</summary>
    internal sealed class DungeonExample
    {
        public DungeonSite Site = new DungeonSite();
        public List<PlacedRoom> Rooms = new List<PlacedRoom>();
        public List<Vec3> Doors = new List<Vec3>();

        /// <summary>Its rooms from the lowest up, the order a plan draws them in; of rooms at one height, the later placed on top.</summary>
        public IEnumerable<PlacedRoom> BottomUp => Rooms.OrderBy(r => r.Position.Y);

        /// <summary>The room a point on the plan is in: the one drawn on top of the others there, or null.</summary>
        public PlacedRoom RoomAt(float x, float z) => BottomUp.LastOrDefault(r => r.Covers(x, z));

        /// <summary>
        /// The same example as it stands from its generator: the generator at the origin and
        /// unturned, each room and door where it is from there, as a stage that holds the
        /// generator's place shows it, and its plan with it. The zone's box moves with it but keeps
        /// its size; a turned site leaves it turned.
        /// </summary>
        public DungeonExample FromGenerator()
        {
            var back = Site.Turn.Inverse();
            var generator = Site.Generator;
            Vec3 Local(Vec3 p) => back * (p - generator);

            var staged = new DungeonExample
            {
                Site = new DungeonSite { ZoneCenter = Local(Site.ZoneCenter), ZoneSize = Site.ZoneSize },
                Doors = Doors.Select(Local).ToList(),
            };
            var moved = new Dictionary<PlacedRoom, PlacedRoom>();
            foreach (var room in Rooms)
            {
                var placed = new PlacedRoom { Room = room.Room, Position = Local(room.Position), Rotation = back * room.Rotation, PlaceOrder = room.PlaceOrder };
                moved[room] = placed;
                staged.Rooms.Add(placed);
            }
            foreach (var room in Rooms)
            {
                if (room.JoinedTo != null && moved.TryGetValue(room.JoinedTo, out var to)) moved[room].JoinedTo = to;
            }
            return staged;
        }

        /// <summary>
        /// The room a ray meets first, going by each room's box (end caps and dividers, which have
        /// no depth, a little deep), leaving out what is above <paramref name="below"/>, where the
        /// stage cuts the example open, and rooms <paramref name="shown"/> says are put away; null
        /// when it meets none.
        /// </summary>
        public PlacedRoom Pick(Vec3 from, Vec3 direction, float below = float.PositiveInfinity, Func<PlacedRoom, bool> shown = null)
        {
            // Where the ray is below the cut: from a least t when it goes down, up to a most t when it goes up.
            var least = 0.0;
            var most = double.PositiveInfinity;
            if (Math.Abs(direction.Y) < 1e-6f)
            {
                if (from.Y > below) return null;
            }
            else if (!float.IsPositiveInfinity(below))
            {
                var at = (below - from.Y) / (double)direction.Y;
                if (direction.Y < 0f) least = Math.Max(least, at);
                else most = Math.Min(most, at);
            }

            PlacedRoom nearest = null;
            var nearestT = double.PositiveInfinity;
            foreach (var room in Rooms)
            {
                if (shown != null && !shown(room)) continue;
                var back = room.Rotation.Inverse();
                var o = back * (from - room.Position);
                var d = back * direction;
                var enter = least;
                var leave = most;
                if (!Slab(o.X, d.X, Math.Max(0.25f, room.Room.Size.X / 2f), ref enter, ref leave)) continue;
                if (!Slab(o.Y, d.Y, Math.Max(0.25f, room.Room.Size.Y / 2f), ref enter, ref leave)) continue;
                if (!Slab(o.Z, d.Z, Math.Max(0.25f, room.Room.Size.Z / 2f), ref enter, ref leave)) continue;
                if (enter > leave || enter >= nearestT) continue;
                nearest = room;
                nearestT = enter;
            }
            return nearest;
        }

        /// <summary>Narrows where a ray is inside a box to where it is between two of its faces; false when it never is.</summary>
        private static bool Slab(float origin, float direction, float half, ref double enter, ref double leave)
        {
            if (Math.Abs(direction) < 1e-6f) return Math.Abs(origin) <= half;
            var a = (-half - origin) / (double)direction;
            var b = (half - origin) / (double)direction;
            enter = Math.Max(enter, Math.Min(a, b));
            leave = Math.Min(leave, Math.Max(a, b));
            return enter <= leave;
        }
    }

    /// <summary>
    /// Whether two boxes centred on their pivots overlap, as <c>Physics.ComputePenetration</c> tells
    /// of two box colliders: boxes that only touch do not.
    /// </summary>
    internal static class Boxes
    {
        private static readonly Vec3 Right = new Vec3(1f, 0f, 0f), Up = new Vec3(0f, 1f, 0f), Forward = new Vec3(0f, 0f, 1f);

        public static bool Overlap(Vec3 aCentre, Quat aTurn, Vec3 aSize, Vec3 bCentre, Quat bTurn, Vec3 bSize)
        {
            // Two boxes are apart when some axis keeps them apart: one of either's faces, or one
            // square to an edge of each (the separating axis test).
            var a = new[] { aTurn * Right, aTurn * Up, aTurn * Forward };
            var b = new[] { bTurn * Right, bTurn * Up, bTurn * Forward };
            var ha = new[] { Math.Abs(aSize.X) / 2f, Math.Abs(aSize.Y) / 2f, Math.Abs(aSize.Z) / 2f };
            var hb = new[] { Math.Abs(bSize.X) / 2f, Math.Abs(bSize.Y) / 2f, Math.Abs(bSize.Z) / 2f };
            var between = bCentre - aCentre;

            bool Apart(Vec3 axis)
            {
                var length = axis.Length;
                if (length < 1e-4f) return false;
                axis = axis * (1f / length);
                var reachA = 0f;
                var reachB = 0f;
                for (var i = 0; i < 3; i++)
                {
                    reachA += ha[i] * Math.Abs(Vec3.Dot(a[i], axis));
                    reachB += hb[i] * Math.Abs(Vec3.Dot(b[i], axis));
                }
                return Math.Abs(Vec3.Dot(between, axis)) >= reachA + reachB - 1e-4f;
            }

            for (var i = 0; i < 3; i++)
            {
                if (Apart(a[i]) || Apart(b[i])) return false;
            }
            for (var i = 0; i < 3; i++)
            {
                for (var j = 0; j < 3; j++)
                {
                    if (Apart(Vec3.Cross(a[i], b[j]))) return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// One example of a dungeon or camp, laid out as <c>DungeonGenerator</c> lays one out in a new
    /// zone, from the rooms of its kinds and with dice of its own, so never a world's real one. A
    /// dungeon starts at a random entrance (<c>PlaceStartRoom</c>), tries its most rooms times to
    /// put a room at a random open doorway (<c>PlaceRooms</c>), closes what is left open with end
    /// caps and dividers (<c>PlaceEndCaps</c>) and rolls a door for each doorway between rooms
    /// (<c>PlaceDoors</c>); a camp is laid on a grid (<c>GenerateCampGrid</c>) or scattered in a
    /// ring and walled (<c>GenerateCampRadial</c>, <c>PlaceWall</c>). A room may not overlap one
    /// placed before it nor leave its zone's box (<c>TestCollision</c>). The ground's tilt and
    /// height, which keep camp rooms off steep or low ground in a world, have no part in an example.
    /// </summary>
    internal sealed class DungeonLayout
    {
        /// <summary>A doorway of a placed room (<c>RoomConnection</c>): where it is, which way it faces, and how far in its room is.</summary>
        private sealed class Opening
        {
            public PlacedRoom Room;
            public Doorway Doorway;
            public Vec3 Position;
            public Quat Rotation;
            public int PlaceOrder;

            public string Type => Doorway.Type;
        }

        private readonly DungeonPlan _plan;
        private readonly List<RoomShape> _rooms;
        private readonly DungeonSite _site;
        private readonly Dice _dice;

        /// <summary>The doorways nothing is joined to yet (<c>m_openConnections</c>).</summary>
        private readonly List<Opening> _open = new List<Opening>();

        /// <summary>The doorways between two rooms that may get a door (<c>m_doorConnections</c>).</summary>
        private readonly List<Opening> _doorways = new List<Opening>();

        public readonly List<PlacedRoom> Placed = new List<PlacedRoom>();
        public readonly List<Vec3> Doors = new List<Vec3>();

        /// <summary>A layout with the rooms of its kinds, in the order the game lists them (<c>DungeonDB</c>).</summary>
        public DungeonLayout(DungeonPlan plan, IEnumerable<RoomShape> rooms, DungeonSite site, Dice dice)
        {
            _plan = plan;
            _rooms = rooms.Where(r => r != null).ToList();
            _site = site;
            _dice = dice;
        }

        public static DungeonExample Build(DungeonPlan plan, IEnumerable<RoomShape> rooms, DungeonSite site, Dice dice)
        {
            var layout = new DungeonLayout(plan, rooms, site, dice);
            switch (plan.Algorithm)
            {
                case "CampGrid":
                    layout.CampGrid();
                    break;
                case "CampRadial":
                    layout.CampRadial();
                    break;
                default:
                    if (!layout.PlaceStartRoom()) break;
                    layout.PlaceRooms();
                    layout.PlaceEndCaps();
                    layout.PlaceDoors();
                    break;
            }
            return new DungeonExample { Site = site, Rooms = layout.Placed, Doors = layout.Doors };
        }

        /// <summary>
        /// Where a new zone puts the generator (<c>ZoneSystem.SpawnLocation</c>). One whose location
        /// has an interior of its own stands at a set place from its zone's centre, unturned. Any
        /// other goes with its location, which stands anywhere in its zone at least its radius from
        /// the zone's edges (<c>GetRandomPointInZone</c>), turned by a random sixteenth of a turn
        /// when it is turned at all (<c>PlaceLocations</c>; one turned down its slope is turned
        /// the same way in an example, as it has no ground); its zone's box is centred on the zone
        /// and as high as the generator.
        /// </summary>
        public static DungeonSite Site(DungeonPlan plan, float locationRadius, bool randomTurn, Dice dice)
        {
            if (plan.CustomInterior)
            {
                return new DungeonSite { Turn = plan.GeneratorTurn, ZoneCenter = plan.ZoneFromGenerator, ZoneSize = plan.ZoneSize };
            }
            var at = new Vec3(dice.Range(-32f + locationRadius, 32f - locationRadius), 0f, dice.Range(-32f + locationRadius, 32f - locationRadius));
            var turn = randomTurn ? Quat.Yaw(dice.Range(0, 16) * 22.5f) : Quat.Identity;
            var generator = at + turn * plan.GeneratorAt;
            return new DungeonSite { Generator = generator, Turn = turn * plan.GeneratorTurn, ZoneCenter = new Vec3(0f, generator.Y, 0f), ZoneSize = plan.ZoneSize };
        }

        // ----- A dungeon -----

        /// <summary>A random entrance, its entrance doorway at the generator and facing as it does; false when there is none.</summary>
        public bool PlaceStartRoom()
        {
            var entrances = _rooms.Where(r => r.Entrance).ToList();
            if (entrances.Count == 0) return false;
            var room = entrances[_dice.Range(0, entrances.Count)];
            var entrance = room.Doorways.FirstOrDefault(d => d.Entrance);
            if (entrance == null) return false;
            PosRot(entrance, _site.Generator, _site.Turn, out var pos, out var rot);
            // The game joins it to the entrance doorway of the room's prefab, whose place order is 0.
            Add(room, pos, rot, 1, null, fromDoorway: true, null);
            return true;
        }

        /// <summary>Tries its most rooms times, stopping once its required rooms and over its fewest rooms are in.</summary>
        public void PlaceRooms()
        {
            for (var i = 0; i < _plan.MaxRooms; i++)
            {
                PlaceOneRoom();
                if (RequiredIn() && Placed.Count > _plan.MinRooms) break;
            }
        }

        /// <summary>A random open doorway, and up to ten rooms that fit it tried there.</summary>
        private bool PlaceOneRoom()
        {
            if (_open.Count == 0) return false;
            var open = _open[_dice.Range(0, _open.Count)];
            for (var i = 0; i < 10; i++)
            {
                var room = _plan.Weighted ? RandomWeightedRoom(open) : RandomRoom(open);
                if (room == null) break;
                if (Join(open, room)) return true;
            }
            return false;
        }

        /// <summary>The rooms that can go at a doorway: no entrance, end cap or divider, one with a doorway of its type, and not one that must be further in.</summary>
        private List<RoomShape> Fitting(Opening open) =>
            _rooms.Where(r => !r.Entrance && !r.EndCap && !r.Divider && r.Fits(open.Doorway) && open.PlaceOrder >= r.MinPlaceOrder).ToList();

        private RoomShape RandomRoom(Opening open)
        {
            var rooms = Fitting(open);
            return rooms.Count == 0 ? null : rooms[_dice.Range(0, rooms.Count)];
        }

        private RoomShape RandomWeightedRoom(Opening open)
        {
            var rooms = Fitting(open);
            return rooms.Count == 0 ? null : Weighted(rooms);
        }

        /// <summary>One of the rooms by weight (<c>GetWeightedRoom</c>).</summary>
        private RoomShape Weighted(List<RoomShape> rooms)
        {
            var at = PlaceParts.Pick(rooms.Select(r => r.Weight).ToList(), _dice.Value);
            return rooms[at >= 0 ? at : 0];
        }

        /// <summary>
        /// Puts a room at an open doorway (<c>PlaceRoom</c>), by a random doorway of its of the
        /// same type, facing it; false when it would overlap a room or leave the zone. A room
        /// that is not an end cap closes the doorway, which then may get a door.
        /// </summary>
        private bool Join(Opening open, RoomShape room)
        {
            var facing = open.Rotation * Quat.Yaw(180f);
            var matching = room.Doorways.Where(d => d.Type == open.Type).ToList();
            if (matching.Count == 0) return false;
            var other = matching[_dice.Range(0, matching.Count)];
            PosRot(other, open.Position, facing, out var pos, out var rot);
            if (room.Size.X != 0f && room.Size.Z != 0f && Collides(room, pos, rot)) return false;
            Add(room, pos, rot, open.PlaceOrder + 1, open.Position, fromDoorway: true, open.Room);
            if (!room.EndCap)
            {
                if (open.Doorway.AllowDoor && (!open.Doorway.DoorOnlyIfOtherAllows || other.AllowDoor)) _doorways.Add(open);
                _open.Remove(open);
            }
            return true;
        }

        /// <summary>
        /// Closes every doorway left open: one that meets another open doorway stays as it is when
        /// both are of one type and gets a divider when not; any other gets the first end cap of
        /// its type that fits, the highest priority first, or with weighted rooms by weight first.
        /// </summary>
        public void PlaceEndCaps()
        {
            for (var i = 0; i < _open.Count; i++)
            {
                var open = _open[i];
                Opening meets = null;
                for (var j = 0; j < _open.Count; j++)
                {
                    if (j != i && Vec3.Distance(open.Position, _open[j].Position) < 0.1f)
                    {
                        meets = _open[j];
                        break;
                    }
                }
                if (meets != null)
                {
                    if (open.Type != meets.Type) Divide(open);
                    continue;
                }

                var caps = Shuffled(_rooms.Where(r => r.EndCap && r.Fits(open.Doorway)));
                var capped = false;
                if (_plan.Weighted && caps.Count > 0)
                {
                    for (var k = 0; k < 5 && !capped; k++) capped = Join(open, Weighted(caps));
                }
                if (capped) continue;
                foreach (var cap in caps.OrderByDescending(c => c.EndCapPrio))
                {
                    if (Join(open, cap)) break;
                }
            }
        }

        /// <summary>A divider by weight at a doorway, its first doorway there, unless one is there already.</summary>
        private void Divide(Opening open)
        {
            var dividers = Shuffled(_rooms.Where(r => r.Divider));
            if (dividers.Count == 0) return;
            var divider = Weighted(dividers);
            if (divider.Doorways.Count == 0) return;
            PosRot(divider.Doorways[0], open.Position, open.Rotation, out var pos, out var rot);
            if (Placed.Any(p => p.Room.Divider && Vec3.Distance(p.Position, pos) < 0.5f)) return;
            Add(divider, pos, rot, open.PlaceOrder + 1, open.Position, fromDoorway: true, open.Room);
        }

        /// <summary>
        /// The rooms in the order the game's shuffle leaves them (<c>ShuffleClass.Shuffle</c> with
        /// Unity's dice), which swaps each place, from the last, with one before it and never with
        /// itself.
        /// </summary>
        private List<RoomShape> Shuffled(IEnumerable<RoomShape> rooms)
        {
            var list = rooms.ToList();
            for (var n = list.Count - 1; n > 0; n--)
            {
                var j = _dice.Range(0, n);
                var kept = list[n];
                list[n] = list[j];
                list[j] = kept;
            }
            return list;
        }

        /// <summary>A door in each doorway between rooms that allows one, by a door of its type, at that door's chance or else the dungeon's.</summary>
        public void PlaceDoors()
        {
            foreach (var doorway in _doorways)
            {
                var kinds = _plan.Doors.Where(d => d.Type == doorway.Type).ToList();
                if (kinds.Count == 0) continue;
                var kind = kinds[_dice.Range(0, kinds.Count)];
                var chance = kind.Chance > 0f ? kind.Chance : _plan.DoorChance;
                if (_dice.Value > chance) continue;
                Doors.Add(doorway.Position);
            }
        }

        private bool RequiredIn()
        {
            if (_plan.MinRequiredRooms == 0 || _plan.RequiredRooms.Count == 0) return false;
            return Placed.Count(p => _plan.RequiredRooms.Contains(p.Room.Name)) >= _plan.MinRequiredRooms;
        }

        // ----- A camp -----

        /// <summary>A room by weight on each square of the grid round the generator, at the camp's chance, turned by a random sixteenth.</summary>
        private void CampGrid()
        {
            var half = _plan.GridSize * _plan.TileWidth * 0.5f;
            var corner = _site.Generator + new Vec3(-half, 0f, -half);
            for (var i = 0; i < _plan.GridSize; i++)
            {
                for (var j = 0; j < _plan.GridSize; j++)
                {
                    if (_dice.Value > _plan.SpawnChance) continue;
                    var at = corner + new Vec3(j * _plan.TileWidth, 0f, i * _plan.TileWidth);
                    var room = CampRoom(false);
                    if (room == null) continue;
                    Place(room, at, Quat.Yaw(_dice.Range(0, 16) * 22.5f));
                }
            }
        }

        /// <summary>
        /// A ring of a random radius, and a random number of rooms (twenty tries each) by weight
        /// anywhere inside it short of its buffer, then its wall, if it has one, on the ring.
        /// </summary>
        private void CampRadial()
        {
            var radius = _dice.Range(_plan.CampRadiusMin, _plan.CampRadiusMax);
            var count = _dice.Range(_plan.MinRooms, _plan.MaxRooms);
            var placed = 0;
            for (var i = 0; i < count * 20; i++)
            {
                var at = _site.Generator + Quat.Yaw(_dice.Range(0, 360)) * Forward * _dice.Range(0f, radius - _plan.PerimeterBuffer);
                var room = CampRoom(false);
                if (room == null) continue;
                var turn = CampTurn(room, at);
                if (Collides(room, at, turn)) continue;
                Place(room, at, turn);
                if (++placed >= count) break;
            }
            if (_plan.PerimeterSections > 0) Wall(radius, _plan.PerimeterSections);
        }

        /// <summary>Up to so many wall rooms by weight on the ring, twenty tries each.</summary>
        private void Wall(float radius, int sections)
        {
            var placed = 0;
            for (var i = 0; i < sections * 20; i++)
            {
                var room = CampRoom(true);
                if (room == null) continue;
                var at = _site.Generator + Quat.Yaw(_dice.Range(0, 360)) * Forward * radius;
                var turn = CampTurn(room, at);
                if (Collides(room, at, turn)) continue;
                Place(room, at, turn);
                if (++placed >= sections) break;
            }
        }

        private static readonly Vec3 Forward = new Vec3(0f, 0f, 1f);

        /// <summary>A camp's room by weight, of its wall or not (<c>GetRandomWeightedRoom</c>); null when it has none.</summary>
        private RoomShape CampRoom(bool perimeter)
        {
            var rooms = _rooms.Where(r => !r.Entrance && !r.EndCap && !r.Divider && r.Perimeter == perimeter).ToList();
            return rooms.Count == 0 ? null : Weighted(rooms);
        }

        /// <summary>
        /// Facing the generator to the nearest sixteenth of a turn when the room faces the centre,
        /// else turned by a random sixteenth (<c>GetCampRoomRotation</c>).
        /// </summary>
        private Quat CampTurn(RoomShape room, Vec3 at)
        {
            if (!room.FaceCenter) return Quat.Yaw(_dice.Range(0, 16) * 22.5f);
            var toCentre = new Vec3(_site.Generator.X - at.X, 0f, _site.Generator.Z - at.Z);
            if (toCentre.Length == 0f) toCentre = Forward;
            var yaw = Math.Atan2(toCentre.X, toCentre.Z) * 180.0 / Math.PI;
            if (yaw < 0.0) yaw += 360.0;
            return Quat.Yaw((float)(Math.Round(yaw / 22.5) * 22.5));
        }

        // ----- Placing -----

        /// <summary>Puts a room where it is told, as a camp does, every doorway of it left open.</summary>
        public PlacedRoom Place(RoomShape room, Vec3 pos, Quat rot) => Add(room, pos, rot, 0, null, fromDoorway: false, null);

        /// <summary>
        /// Adds a room and its open doorways (<c>AddOpenConnections</c>): one joined from a doorway
        /// leaves out its entrances and the doorway it was joined at, which is taken.
        /// </summary>
        private PlacedRoom Add(RoomShape room, Vec3 pos, Quat rot, int placeOrder, Vec3? joinedAt, bool fromDoorway, PlacedRoom joinedTo)
        {
            var placed = new PlacedRoom { Room = room, Position = pos, Rotation = rot, PlaceOrder = placeOrder, JoinedTo = joinedTo };
            Placed.Add(placed);
            for (var i = 0; i < room.Doorways.Count; i++)
            {
                var doorway = room.Doorways[i];
                var at = placed.DoorwayAt(i);
                if (fromDoorway && (doorway.Entrance || (joinedAt.HasValue && Vec3.Distance(at, joinedAt.Value) < 0.1f))) continue;
                _open.Add(new Opening { Room = placed, Doorway = doorway, Position = at, Rotation = placed.DoorwayTurn(i), PlaceOrder = placeOrder });
            }
            return placed;
        }

        /// <summary>Where a room goes so its doorway is at a point and faces a way (<c>CalculateRoomPosRot</c>).</summary>
        private static void PosRot(Doorway doorway, Vec3 at, Quat facing, out Vec3 pos, out Quat rot)
        {
            rot = facing * doorway.Rotation.Inverse();
            pos = at - rot * doorway.Position;
        }

        /// <summary>Whether a room would leave the zone's box or overlap a room placed before it, itself a tenth of a metre smaller (<c>TestCollision</c>).</summary>
        private bool Collides(RoomShape room, Vec3 pos, Quat rot)
        {
            if (!Inside(room, pos, rot)) return true;
            var smaller = new Vec3(room.Size.X - 0.1f, room.Size.Y - 0.1f, room.Size.Z - 0.1f);
            return Placed.Any(p => Boxes.Overlap(pos, rot, smaller, p.Position, p.Rotation, p.Room.Size));
        }

        /// <summary>Whether every corner of a room is inside its zone's box (<c>IsInsideDungeon</c>).</summary>
        private bool Inside(RoomShape room, Vec3 pos, Quat rot)
        {
            var min = _site.ZoneCenter - _site.ZoneSize * 0.5f;
            var max = _site.ZoneCenter + _site.ZoneSize * 0.5f;
            var half = room.Size * 0.5f;
            foreach (var x in new[] { -half.X, half.X })
            {
                foreach (var y in new[] { -half.Y, half.Y })
                {
                    foreach (var z in new[] { -half.Z, half.Z })
                    {
                        var corner = pos + rot * new Vec3(x, y, z);
                        if (corner.X < min.X || corner.X > max.X || corner.Y < min.Y || corner.Y > max.Y || corner.Z < min.Z || corner.Z > max.Z) return false;
                    }
                }
            }
            return true;
        }
    }
}
