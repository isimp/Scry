using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>
    /// The example layout of a dungeon or camp as it stands on the stage (<see cref="Stage"/>):
    /// the holder its rooms' copies stand under, the room being copied, the rooms standing, which
    /// of them are dimmed below the floor opened, what each dungeon room's own rays found, and
    /// the floors those make. It keeps all of it and lets go of it itself; the stage copies each
    /// room and frames the example.
    /// </summary>
    internal sealed class StageExample
    {
        /// <summary>A few milliseconds a frame for putting rooms away and back as floors are opened.</summary>
        private const double KeepBudgetMs = 4.0;

        /// <summary>The holder the rooms' copies stand under; null with no example.</summary>
        public GameObject Holder { get; private set; }

        /// <summary>The example it was begun for, and that example as it stands, from its generator.</summary>
        public DungeonExample Of { get; private set; }
        public DungeonExample Placed { get; private set; }

        /// <summary>How many of its rooms have been gone through, and how many of those stand.</summary>
        public int Next;
        public int Copies;

        /// <summary>Whether it is a dungeon's, standing in place of its entrance, rather than a camp's around its location.</summary>
        public bool IsDungeon { get; private set; }

        /// <summary>Whether the location has nothing to show outside, no part of it drawn but the example (the sealed tower's), so the inside shows however the switch is left.</summary>
        public bool NothingOutside { get; private set; }

        /// <summary>The room being copied, with the spawn points and paints read off it.</summary>
        public Ghost.Building Build;
        public List<Stage.SpawnHere> BuildSpawns;
        public List<GroundPaintAt> BuildPaints;

        /// <summary>The rooms standing, each with its copy, to dim or put away those off the floor opened.</summary>
        private readonly List<KeyValuePair<PlacedRoom, GameObject>> _rooms = new List<KeyValuePair<PlacedRoom, GameObject>>();

        /// <summary>The rooms' copies dimmed, below the floor opened (<see cref="Dim"/>).</summary>
        private readonly HashSet<GameObject> _dimmed = new HashSet<GameObject>();

        /// <summary>What each dungeon room's own rays found, to tell the floors it stands on (<see cref="FloorFinder.Holds"/>).</summary>
        private readonly Dictionary<PlacedRoom, FloorPatch> _roomGround = new Dictionary<PlacedRoom, FloorPatch>();

        /// <summary>The location's own parts beside the example, put away while a dungeon's example is shown.</summary>
        private readonly List<GameObject> _outside = new List<GameObject>();

        /// <summary>
        /// While the self-test compares them: each room's ground as the rules before read it, its
        /// open ground left out (<see cref="FloorRules.Before"/>), beside what is read now.
        /// </summary>
        public static bool KeepRulesBefore;

        private readonly List<FloorPatch> _patchesBefore = new List<FloorPatch>();

        /// <summary>What each room's rays found and the ground they were cast over; the rays of the room being read.</summary>
        private readonly List<FloorPatch> _patches = new List<FloorPatch>();
        private readonly List<FloorHit> _hits = new List<FloorHit>();
        private float _ground;

        /// <summary>The rooms counted on the floor opened, worked out once a frame.</summary>
        private int _countedAt = -1;
        private float? _countedFloor;
        private int _countedRooms;

        /// <summary>The middle across of the rooms on the floor opened, kept until the floor or the rooms change.</summary>
        private Vector3? _across;
        private float? _acrossFloor;
        private int _acrossRooms = -1;
        private DungeonExample _acrossOf;

        /// <summary>
        /// Begins standing an example under what is shown: a camp's rooms around its generator as
        /// the location has it, a dungeon's from the stage's middle, where they are shown in place
        /// of its entrance, which shows until its first room stands.
        /// </summary>
        public void Begin(DungeonExample example, DungeonPlan plan, Transform subject, int layer)
        {
            Of = example;
            Placed = example.FromGenerator();
            IsDungeon = plan.Algorithm == "Dungeon";

            Holder = new GameObject("Scry example") { layer = layer };
            var holder = Holder.transform;
            holder.SetParent(subject, false);
            if (!IsDungeon)
            {
                holder.localPosition = new Vector3(plan.GeneratorAt.X, plan.GeneratorAt.Y, plan.GeneratorAt.Z);
                holder.localRotation = new Quaternion(plan.GeneratorTurn.X, plan.GeneratorTurn.Y, plan.GeneratorTurn.Z, plan.GeneratorTurn.W);
            }
            foreach (Transform part in subject)
            {
                if (part != holder && part.gameObject.activeSelf) _outside.Add(part.gameObject);
            }
            NothingOutside = !_outside.Any(part => part.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled));
            if (IsDungeon) Holder.SetActive(false);
        }

        /// <summary>A room's copy standing.</summary>
        public void Add(PlacedRoom room, GameObject copy) => _rooms.Add(new KeyValuePair<PlacedRoom, GameObject>(room, copy));

        /// <summary>Whether a room's copy is dimmed, below the floor opened.</summary>
        public bool IsDimmed(GameObject room) => _dimmed.Contains(room);

        /// <summary>Shows a dungeon's example inside, its location's own parts put away, or its entrance outside.</summary>
        public void ShowInside(bool inside)
        {
            foreach (var part in _outside) if (part != null) part.SetActive(!inside);
            Holder.SetActive(inside);
        }

        /// <summary>
        /// With a floor opened inside a dungeon's example, that floor's rooms stand as they are,
        /// those below dimmed (<see cref="Dim"/>) and those above put away, as they are cut away;
        /// with none opened every room stands as it is. A few each frame, as a big example changes many at once.
        /// </summary>
        public void KeepToFloor(float? planFloor)
        {
            if (!IsDungeon || Holder == null || _rooms.Count == 0) return;
            var watch = Stopwatch.StartNew();
            foreach (var pair in _rooms)
            {
                var copy = pair.Value;
                if (copy == null) continue;
                var shown = RoomShown(pair.Key, planFloor);
                var standing = shown != PlanRoomShown.None;
                var dimmed = shown == PlanRoomShown.Faint;
                if (copy.activeSelf == standing && _dimmed.Contains(copy) == dimmed) continue;
                if (copy.activeSelf != standing) copy.SetActive(standing);
                if (_dimmed.Contains(copy) != dimmed)
                {
                    Dim.Set(copy, dimmed);
                    if (dimmed) _dimmed.Add(copy);
                    else _dimmed.Remove(copy);
                }
                if (watch.Elapsed.TotalMilliseconds >= KeepBudgetMs) break;
            }
        }

        /// <summary>How many of the rooms are dimmed, for the self-test.</summary>
        public int DimmedCount => _dimmed.Count;

        /// <summary>Whether every room stands, is dimmed or is put away as the floor opened has it, for the self-test.</summary>
        public bool Kept(float? planFloor)
        {
            foreach (var pair in _rooms)
            {
                if (pair.Value == null) continue;
                var shown = RoomShown(pair.Key, planFloor);
                if (pair.Value.activeSelf != (shown != PlanRoomShown.None) || _dimmed.Contains(pair.Value) != (shown == PlanRoomShown.Faint)) return false;
            }
            return true;
        }

        /// <summary>How many of the rooms are put away, above the floor opened, for the self-test.</summary>
        public int Away
        {
            get
            {
                var away = 0;
                foreach (var pair in _rooms) if (pair.Value != null && !pair.Value.activeSelf) away++;
                return away;
            }
        }

        /// <summary>How many rooms stand on a floor, end caps and dividers left out; worked out once a frame for one floor.</summary>
        public int OnFloorCount(float floor)
        {
            if (_countedAt == Time.frameCount && _countedFloor == floor) return _countedRooms;
            // A room by its box or its own ground; an end cap or divider only by ground of its own there.
            var count = 0;
            foreach (var room in Placed.Rooms)
            {
                if (_roomGround.TryGetValue(room, out var ground) && FloorFinder.Holds(ground, floor)) count++;
                else if (!room.Room.EndCap && !room.Room.Divider && ExamplePlan.Shown(room, floor) == PlanRoomShown.Whole) count++;
            }
            _countedAt = Time.frameCount;
            _countedFloor = floor;
            _countedRooms = count;
            return count;
        }

        /// <summary>Whether a room stands on a floor: its box says so (<see cref="ExamplePlan.Shown"/>), or its own rays found ground there (<see cref="FloorFinder.Holds"/>), as its meshes can reach past its box.</summary>
        public bool OnFloor(PlacedRoom room, float floor) =>
            ExamplePlan.Shown(room, floor) == PlanRoomShown.Whole || _roomGround.TryGetValue(room, out var ground) && FloorFinder.Holds(ground, floor);

        /// <summary>How a room shows with a floor opened: whole on it, else as its box says, below faintly or above not at all; whole with none.</summary>
        public PlanRoomShown RoomShown(PlacedRoom room, float? planFloor)
        {
            if (planFloor == null) return PlanRoomShown.Whole;
            return OnFloor(room, planFloor.Value) ? PlanRoomShown.Whole : ExamplePlan.Shown(room, planFloor);
        }

        /// <summary>
        /// The middle across of the rooms standing on the floor opened, on the stage, and how far
        /// they reach from it (x, z and reach); null for none. Worked out again as the floor or
        /// the rooms change.
        /// </summary>
        public Vector3? FloorAcross(float? planFloor)
        {
            if (planFloor == null) return null;
            if (_acrossFloor == planFloor && _acrossRooms == Copies && ReferenceEquals(_acrossOf, Placed)) return _across;

            var holder = Holder.transform;
            var corners = new List<Vec3>();
            foreach (var room in Placed.Rooms)
            {
                if (RoomShown(room, planFloor) != PlanRoomShown.Whole) continue;
                foreach (var corner in room.Corners())
                {
                    var at = holder.TransformPoint(new Vector3(corner.X, corner.Y, corner.Z));
                    corners.Add(new Vec3(at.x, at.y, at.z));
                }
            }
            var across = StageCamera.Across(corners);
            _across = across is (float x, float z, float reach) ? new Vector3(x, z, reach) : (Vector3?)null;
            _acrossFloor = planFloor;
            _acrossRooms = Copies;
            _acrossOf = Placed;
            return _across;
        }

        /// <summary>What the location draws outside, its parts with something drawn and how many, for the self-test to tell.</summary>
        [Diagnostic]
        public string OutsideTold()
        {
            var drawn = _outside.Where(part => part != null && part.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled)).Select(part => part.name).ToList();
            return drawn.Count == 0 ? "nothing" : $"{Numbers.Count(drawn.Count)} parts: {string.Join(", ", drawn.Take(6))}";
        }

        /// <summary>Each room read so far with the floors found in it alone, for the self-test to tell.</summary>
        [Diagnostic]
        public List<string> RoomFloorsTold()
        {
            var told = new List<string>();
            if (Placed == null) return told;
            foreach (var room in Placed.Rooms)
            {
                if (!_roomGround.TryGetValue(room, out var ground)) continue;
                var alone = FloorFinder.Floors(new[] { ground }, ground.Ground, PlaceView.Storey);
                told.Add($"{room.Room.Name} at {Numbers.Fixed(room.Position.Y, 1)} m: {(alone.Count > 0 ? string.Join(", ", alone.Select(f => Numbers.Fixed(f, 1))) : "none")}");
            }
            return told;
        }

        /// <summary>
        /// Each room read so far as the floors are found from it: the height its box stands over,
        /// its doorways' heights, the ground its own rays found (each height with its square
        /// metres, ! where that is a level of the room's own, ~ where no ray landed in the band
        /// itself), and the floors it stands on, by its box or by its ground, for the self-test.
        /// </summary>
        [Diagnostic]
        public List<string> RoomGroundTold(IReadOnlyList<float> floors)
        {
            var told = new List<string>();
            if (Placed == null) return told;
            var grounds = _roomGround;
            foreach (var room in Placed.Rooms)
            {
                var half = room.Room.Size.Y / 2f;
                var doors = string.Join("/", Enumerable.Range(0, room.Room.Doorways.Count).Select(i => Numbers.Fixed(room.DoorwayAt(i).Y, 1)));
                var line = $"{room.Room.Name} {Numbers.Fixed(room.Position.Y - half, 1)} to {Numbers.Fixed(room.Position.Y + half, 1)} m, doors {doors}";
                if (grounds.TryGetValue(room, out var ground))
                {
                    // Each band counts its neighbours' rays too: the most room within half a metre stands for them.
                    var own = System.Math.Max(FloorFinder.MinRoom, ground.Ground * FloorFinder.MinShare);
                    var kept = new List<KeyValuePair<int, FloorPatch.Band>>();
                    foreach (var band in ground.Bands.Where(b => b.Value.Room >= FloorFinder.MinRoom).OrderByDescending(b => b.Value.Room))
                    {
                        if (kept.All(k => System.Math.Abs(k.Key - band.Key) > 2)) kept.Add(band);
                    }
                    var bands = kept.OrderByDescending(b => b.Key).Select(b =>
                        $"{Numbers.Fixed((float)(b.Value.Sum / b.Value.Count), 1)}:{Numbers.Amount(b.Value.Room, 0)}{(b.Value.Room >= own ? "!" : "")}{(b.Value.Landed ? "" : "~")}");
                    line += $", ground {Numbers.Amount(ground.Ground, 0)} m\u00b2: {string.Join(" ", bands)}";
                }
                var on = floors.Where(f => OnFloor(room, f)).Select(f => Numbers.Fixed(f, 1) + (ExamplePlan.Shown(room, f) == PlanRoomShown.Whole ? "" : " by ground"));
                told.Add(line + $", on {string.Join(" ", on)}");
            }
            return told;
        }

        /// <summary>
        /// Reads a dungeon room's floors, once, as it stands. While the entrance is shown, the
        /// example sleeps, and the room alone is woken for it beside the example: a dungeon's
        /// example stands where its location does, so the room stands the same in either.
        /// </summary>
        public void ReadFloors(GameObject copy, PlacedRoom room, List<Stage.SpawnHere> spawns, Transform subject, int layer)
        {
            var read = Timing.Start();
            var asleep = !Holder.activeSelf;
            if (asleep) copy.transform.SetParent(subject, false);
            _hits.Clear();
            var cast = FloorProbe.Read(copy, subject, layer, _hits, Copies, spawns: spawns);
            _ground += cast;
            // Its highest doorway and its doorway out, the entrance's, where its ground was measured:
            // ground high over the one is out of its reach, open ground by the other the ground outside.
            float Measured(Vec3 at) => subject.InverseTransformPoint(Holder.transform.TransformPoint(new Vector3(at.X, at.Y, at.Z))).y;
            var door = float.PositiveInfinity;
            var outerDoor = float.PositiveInfinity;
            for (var i = 0; i < room.Room.Doorways.Count; i++)
            {
                var height = Measured(room.DoorwayAt(i));
                door = float.IsPositiveInfinity(door) ? height : Mathf.Max(door, height);
                if (room.Room.Doorways[i].Entrance) outerDoor = height;
            }
            // Read alone, a room whose ceiling is the room above has nothing of its own over its floor.
            var ground = FloorFinder.Patch(FloorFinder.Inside(_hits, door, outerDoor));
            ground.Ground = cast;
            ground.Door = door;
            _patches.Add(ground);
            _roomGround[room] = ground;
            if (KeepRulesBefore)
            {
                var before = FloorFinder.Patch(_hits);
                before.Ground = cast;
                _patchesBefore.Add(before);
            }
            _hits.Clear();
            if (asleep) copy.transform.SetParent(Holder.transform, false);
            Timing.Add("example floors", read);
        }

        /// <summary>The floors its rooms' ground makes now, and as the rules before read it (<see cref="FloorRules.Before"/>; null where it was not kept for every room), for the self-test to tell.</summary>
        [Diagnostic]
        public (List<float> Now, List<float> Before) FloorsFoundBothWays() =>
            (FloorFinder.Floors(_patches, _ground, PlaceView.Storey),
             _patchesBefore.Count == _patches.Count && _patches.Count > 0 ? FloorFinder.Floors(_patchesBefore, _ground, PlaceView.Storey, FloorRules.Before) : null);

        /// <summary>The example's floors: found in its rooms, with one for each room no floor reaches, or where their doorways are while none are found.</summary>
        public List<float> FloorsNow()
        {
            var found = FloorFinder.Floors(_patches, _ground, PlaceView.Storey);
            if (found.Count == 0) return PlaceView.ExampleFloors(Placed);
            // A room standing whole on none of them, a sloping corridor with no flat ground, gets
            // a floor where it is walked into, so every room built can be opened.
            return PlaceView.ReachingEveryRoom(found, Placed.Rooms.Take(Next), OnFloor);
        }

        /// <summary>Takes down the example's copies and lets go of everything kept of it, as another entry or another example is shown.</summary>
        public void Forget()
        {
            Build?.Cancel();
            Build = null;
            BuildSpawns = null;
            BuildPaints = null;
            if (Holder != null) Object.Destroy(Holder);
            Holder = null;
            Of = null;
            Placed = null;
            NothingOutside = false;
            Next = 0;
            Copies = 0;
            _patches.Clear();
            _patchesBefore.Clear();
            _rooms.Clear();
            _dimmed.Clear();
            _roomGround.Clear();
            _countedAt = -1;
            _ground = 0f;
            _outside.Clear();
        }
    }
}
