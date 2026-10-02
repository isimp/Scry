using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>
    /// The example layout of the location shown, built on the stage from copies of its rooms
    /// (<see cref="ExampleLayouts"/>), a few milliseconds each frame in the order they were
    /// placed, the entrance first, a large room over several frames (<see cref="Ghost.Building"/>). A camp's rooms stand around the location's own parts, from where its
    /// generator is. A dungeon's stand where the game builds them, far above its entrance, so
    /// the stage shows either: inside, the example with the entrance's own parts put away, or
    /// outside, the entrance. Inside, the example opens on its top floor like a room.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>What a room's model is to the stage: settled (loaded, or failed with no model) or still loading.</summary>
        public delegate bool RoomModel(string prefab, out GameObject model);

        /// <summary>A few milliseconds a frame for copying rooms, and at least one.</summary>
        private const double ExampleBudgetMs = 4.0;

        private static GameObject _exampleHolder;
        private static DungeonExample _exampleOf;
        private static DungeonExample _examplePlaced;
        private static int _exampleNext;
        private static int _exampleCopies;
        private static Ghost.Building _exampleBuild;
        private static List<SpawnHere> _exampleBuildSpawns;

        /// <summary>The example's rooms standing on the stage, each with its copy, to dim or put away those off the floor opened.</summary>
        private static readonly List<KeyValuePair<PlacedRoom, GameObject>> ExampleCopies = new List<KeyValuePair<PlacedRoom, GameObject>>();

        /// <summary>The rooms' copies dimmed, below the floor opened (<see cref="Dim"/>).</summary>
        private static readonly HashSet<GameObject> DimmedRooms = new HashSet<GameObject>();

        /// <summary>A few milliseconds a frame for putting rooms away and back as floors are opened.</summary>
        private const double KeepBudgetMs = 4.0;

        /// <summary>What each dungeon room's own rays found, to tell the floors it stands on (<see cref="FloorFinder.Holds"/>).</summary>
        private static readonly Dictionary<PlacedRoom, FloorPatch> RoomGround = new Dictionary<PlacedRoom, FloorPatch>();

        /// <summary>The rooms counted on the floor opened, worked out once a frame.</summary>
        private static int _countedAt = -1;
        private static float? _countedFloor;
        private static int _countedRooms;
        private static bool _exampleIsDungeon;
        private static readonly List<GameObject> OutsideParts = new List<GameObject>();

        /// <summary>Whether a dungeon's example is shown rather than its entrance; kept from one dungeon to the next.</summary>
        private static bool _inside = true;

        /// <summary>How many of the example's rooms stand on the stage, and how many it has.</summary>
        public static int ExampleRoomsShown => _exampleHolder != null ? _exampleNext : 0;
        public static int ExampleRoomsTotal => _exampleHolder != null && _examplePlaced != null ? _examplePlaced.Rooms.Count : 0;

        /// <summary>The example as it stands on the stage, in its own space, for its plan; null before it is begun.</summary>
        public static DungeonExample ExampleShown => _exampleHolder != null ? _examplePlaced : null;

        /// <summary>Which way the camera looks over the example, as a yaw in the example's own space, for its plan to turn with the view.</summary>
        public static float ExampleViewYaw
        {
            get
            {
                if (_exampleHolder == null || _camera == null) return 0f;
                var space = _exampleHolder.transform;
                var ahead = space.InverseTransformDirection(_camera.transform.forward);
                // Looking straight down, what is ahead is what is up in the picture.
                if (ahead.x * ahead.x + ahead.z * ahead.z < 0.01f) ahead = space.InverseTransformDirection(_camera.transform.up);
                return Mathf.Atan2(ahead.x, ahead.z) * Mathf.Rad2Deg;
            }
        }

        /// <summary>The floor opened over the example, as a height in its own space; null with the roof on, or while a dungeon's entrance is shown.</summary>
        public static float? ExampleOpenFloor
        {
            get
            {
                if (_exampleHolder == null || !Cutting || _exampleIsDungeon && !_exampleHolder.activeSelf) return null;
                return Floors[_cutLevel] - _exampleHolder.transform.localPosition.y;
            }
        }

        /// <summary>Whether the location shown is a dungeon whose example can be gone into.</summary>
        public static bool HasInside => _exampleHolder != null && _exampleIsDungeon;

        /// <summary>Whether the example is shown, as the stage's chip switches it.</summary>
        public static bool Inside
        {
            get => _inside;
            set
            {
                if (value == _inside) return;
                _inside = value;
                ShowInsideOrOut();
            }
        }

        /// <summary>
        /// Copies the next rooms of the example onto the stage, when the location it is of is
        /// shown; a new example, or a new copy of the location, starts it again.
        /// </summary>
        public static void StepExample(Entry entry, DungeonExample example, DungeonPlan plan, RoomModel model)
        {
            if (_subject == null || !ReferenceEquals(_lastShown, entry) || example == null || plan == null) return;
            if (_exampleHolder == null || !ReferenceEquals(_exampleOf, example)) BeginExample(example, plan);
            KeepToFloor();
            if (_exampleNext >= _examplePlaced.Rooms.Count) return;

            var made = Timing.Start();
            var watch = Stopwatch.StartNew();
            var placedAny = false;
            var stepped = false;
            while (_exampleNext < _examplePlaced.Rooms.Count && (!stepped || watch.Elapsed.TotalMilliseconds < ExampleBudgetMs))
            {
                if (_exampleBuild == null)
                {
                    var room = _examplePlaced.Rooms[_exampleNext];
                    if (!model(room.Room.Name, out var prefab)) break;
                    if (prefab == null)
                    {
                        _exampleNext++;
                        continue;
                    }
                    var at = new Vector3(room.Position.X, room.Position.Y, room.Position.Z);
                    var turn = new Quaternion(room.Rotation.X, room.Rotation.Y, room.Rotation.Z, room.Rotation.W);
                    // A room's spawn points go by the rules of the location it is built in.
                    _exampleBuildSpawns = new List<SpawnHere>();
                    var rules = _lastShown?.Source is PlaceSource shown ? PlaceAssets.Asset(shown)?.GetComponent<Location>() : null;
                    _exampleBuild = PlaceCopy.Begin(prefab, _exampleHolder.transform, at, turn, _layer, keepColliders: _exampleIsDungeon, local: true, spawns: _exampleBuildSpawns, rules: rules);
                }
                stepped = true;
                if (!_exampleBuild.Go(ExampleBudgetMs - watch.Elapsed.TotalMilliseconds)) break;
                var copy = _exampleBuild.Result;
                var placed = _examplePlaced.Rooms[_exampleNext];
                _exampleBuild = null;
                _exampleNext++;
                if (copy == null) continue;
                placedAny = true;
                ExampleCopies.Add(new KeyValuePair<PlacedRoom, GameObject>(placed, copy));

                // A dungeon room's floors; a camp keeps its location's.
                if (_exampleIsDungeon) ReadFloors(copy, placed, _exampleCopies, _exampleBuildSpawns);
                Tune(copy, audible: false);
                Populate(_exampleBuildSpawns, copy);
                _exampleBuildSpawns = null;
                _exampleCopies++;

                if (_exampleCopies == 1 && _exampleIsDungeon) ShowInsideOrOut();
                else if (_exampleHolder.activeInHierarchy) _bounds.Encapsulate(Unscaled(Measure(copy)));
            }
            if (placedAny && _exampleIsDungeon && _inside && _exampleCopies > 1) RefreshFloors(ExampleFloorsNow());
            Timing.Add("stage example", made);
        }

        /// <summary>
        /// With a floor opened inside a dungeon's example, that floor's rooms stand on the stage as
        /// they are (<see cref="ExampleRoomShown"/>), those below dimmed (<see cref="Dim"/>) and
        /// those above put away, as they are cut away; with the roof on, or its entrance shown,
        /// every room stands as it is. A few each frame, as a big example changes many at once.
        /// </summary>
        private static void KeepToFloor()
        {
            if (!_exampleIsDungeon || _exampleHolder == null || ExampleCopies.Count == 0) return;
            var watch = Stopwatch.StartNew();
            foreach (var pair in ExampleCopies)
            {
                var copy = pair.Value;
                if (copy == null) continue;
                var shown = ExampleRoomShown(pair.Key);
                var standing = shown != PlanRoomShown.None;
                var dimmed = shown == PlanRoomShown.Faint;
                if (copy.activeSelf == standing && DimmedRooms.Contains(copy) == dimmed) continue;
                if (copy.activeSelf != standing) copy.SetActive(standing);
                if (DimmedRooms.Contains(copy) != dimmed)
                {
                    Dim.Set(copy, dimmed);
                    if (dimmed) DimmedRooms.Add(copy);
                    else DimmedRooms.Remove(copy);
                }
                if (watch.Elapsed.TotalMilliseconds >= KeepBudgetMs) break;
            }
        }

        /// <summary>How many of the example's rooms are dimmed, below the floor opened, for the self-test.</summary>
        public static int ExampleRoomsDimmed => DimmedRooms.Count;

        /// <summary>Whether every room of the example stands, is dimmed or is put away as the floor opened has it, for the self-test.</summary>
        public static bool ExampleRoomsKept
        {
            get
            {
                foreach (var pair in ExampleCopies)
                {
                    if (pair.Value == null) continue;
                    var shown = ExampleRoomShown(pair.Key);
                    if (pair.Value.activeSelf != (shown != PlanRoomShown.None) || DimmedRooms.Contains(pair.Value) != (shown == PlanRoomShown.Faint)) return false;
                }
                return true;
            }
        }

        /// <summary>How many of the example's rooms are put away, above the floor opened, for the self-test.</summary>
        public static int ExampleRoomsAway
        {
            get
            {
                var away = 0;
                foreach (var pair in ExampleCopies) if (pair.Value != null && !pair.Value.activeSelf) away++;
                return away;
            }
        }

        /// <summary>How many rooms of the example stand on the floor opened, end caps and dividers left out; -1 with no floor of an example opened.</summary>
        public static int ExampleRoomsOnFloor
        {
            get
            {
                var floor = ExampleOpenFloor;
                if (floor == null || _examplePlaced == null) return -1;
                if (_countedAt == Time.frameCount && _countedFloor == floor) return _countedRooms;
                // A room by its box or its own ground; an end cap or divider only by ground of its own there.
                var count = 0;
                foreach (var room in _examplePlaced.Rooms)
                {
                    if (RoomGround.TryGetValue(room, out var ground) && FloorHolds(ground, floor.Value)) count++;
                    else if (!room.Room.EndCap && !room.Room.Divider && ExamplePlan.Shown(room, floor) == PlanRoomShown.Whole) count++;
                }
                _countedAt = Time.frameCount;
                _countedFloor = floor;
                _countedRooms = count;
                return count;
            }
        }

        /// <summary>Whether a room stands on a floor: its box says so (<see cref="ExamplePlan.Shown"/>), or its own rays found ground there (<see cref="FloorFinder.Holds"/>), as its meshes can reach past its box.</summary>
        private static bool OnFloor(PlacedRoom room, float floor) =>
            ExamplePlan.Shown(room, floor) == PlanRoomShown.Whole || RoomGround.TryGetValue(room, out var ground) && FloorHolds(ground, floor);

        private static bool FloorHolds(FloorPatch ground, float floor) => FloorFinder.Holds(ground, floor);

        /// <summary>
        /// The floor opened over the example as its plan and stage honour it: none with the roof
        /// on, or where no room stands on it, which then shows every room rather than none.
        /// </summary>
        public static float? ExamplePlanFloor => ExampleOpenFloor is float floor && ExampleRoomsOnFloor > 0 ? floor : (float?)null;

        /// <summary>
        /// The middle across of the rooms standing on the floor opened in a dungeon's example, on
        /// the stage, and how far they reach from it (x, z and reach); null with none opened or
        /// none standing on it. Worked out again as the floor or the rooms change.
        /// </summary>
        private static Vector3? ExampleFloorAcross()
        {
            if (!_exampleIsDungeon || _exampleHolder == null || !_exampleHolder.activeInHierarchy || _examplePlaced == null) return null;
            var floor = ExamplePlanFloor;
            if (floor == null) return null;
            if (_acrossFloor == floor && _acrossRooms == _exampleCopies && ReferenceEquals(_acrossOf, _examplePlaced)) return _across;

            var holder = _exampleHolder.transform;
            var corners = new List<Vec3>();
            foreach (var room in _examplePlaced.Rooms)
            {
                if (ExampleRoomShown(room) != PlanRoomShown.Whole) continue;
                foreach (var corner in room.Corners())
                {
                    var at = holder.TransformPoint(new Vector3(corner.X, corner.Y, corner.Z));
                    corners.Add(new Vec3(at.x, at.y, at.z));
                }
            }
            var across = StageCamera.Across(corners);
            _across = across is (float x, float z, float reach) ? new Vector3(x, z, reach) : (Vector3?)null;
            _acrossFloor = floor;
            _acrossRooms = _exampleCopies;
            _acrossOf = _examplePlaced;
            return _across;
        }

        private static Vector3? _across;
        private static float? _acrossFloor;
        private static int _acrossRooms = -1;
        private static DungeonExample _acrossOf;

        /// <summary>How a room of the example shows with the floor opened: whole on it, else as its box says, below faintly or above not at all.</summary>
        public static PlanRoomShown ExampleRoomShown(PlacedRoom room)
        {
            var floor = ExamplePlanFloor;
            if (floor == null) return PlanRoomShown.Whole;
            return OnFloor(room, floor.Value) ? PlanRoomShown.Whole : ExamplePlan.Shown(room, floor);
        }

        /// <summary>
        /// Reads a dungeon room's floors, once, as it stands. While the entrance is shown, the
        /// example sleeps, and the room alone is woken for it beside the example: a dungeon's
        /// example stands where its location does, so the room stands the same in either.
        /// </summary>
        private static void ReadFloors(GameObject copy, PlacedRoom room, int patch, List<SpawnHere> spawns)
        {
            var read = Timing.Start();
            var asleep = !_exampleHolder.activeSelf;
            if (asleep) copy.transform.SetParent(_subject.transform, false);
            RoomHits.Clear();
            _exampleGround += FloorProbe.Read(copy, _subject.transform, _layer, RoomHits, patch, spawns: spawns);
            var ground = FloorFinder.Patch(RoomHits);
            ExamplePatches.Add(ground);
            RoomGround[room] = ground;
            RoomHits.Clear();
            if (asleep) copy.transform.SetParent(_exampleHolder.transform, false);
            Timing.Add("example floors", read);
        }

        /// <summary>Takes down the example's copies, as another entry or another example is shown.</summary>
        private static void ForgetExample()
        {
            _exampleBuild?.Cancel();
            _exampleBuildSpawns = null;
            ForgetRoomCreatures();
            _exampleBuild = null;
            if (_exampleHolder != null) Object.Destroy(_exampleHolder);
            _exampleHolder = null;
            _exampleOf = null;
            _examplePlaced = null;
            _exampleNext = 0;
            _exampleCopies = 0;
            ExamplePatches.Clear();
            ExampleCopies.Clear();
            DimmedRooms.Clear();
            RoomGround.Clear();
            _countedAt = -1;
            _exampleGround = 0f;
            OutsideParts.Clear();
        }

        /// <summary>
        /// What each room's rays found, read once as the room stands, and the ground they were
        /// cast over (<see cref="FloorProbe"/>, <see cref="FloorPatch"/>); the rays of the room
        /// being read.
        /// </summary>
        private static readonly List<FloorPatch> ExamplePatches = new List<FloorPatch>();
        private static readonly List<FloorHit> RoomHits = new List<FloorHit>();
        private static float _exampleGround;

        /// <summary>The example's floors: found in its rooms, or where their doorways are while none are found.</summary>
        private static List<float> ExampleFloorsNow()
        {
            var found = FloorFinder.Floors(ExamplePatches, _exampleGround, PlaceView.Storey);
            return found.Count > 0 ? found : PlaceView.ExampleFloors(_examplePlaced);
        }

        private static void BeginExample(DungeonExample example, DungeonPlan plan)
        {
            ForgetExample();
            _exampleOf = example;
            _examplePlaced = example.FromGenerator();
            _exampleIsDungeon = plan.Algorithm == "Dungeon";

            // A camp's rooms stand around its generator as the location has it; a dungeon's from
            // the stage's middle, where they are shown in place of its entrance.
            _exampleHolder = new GameObject("Scry example") { layer = _layer };
            var holder = _exampleHolder.transform;
            holder.SetParent(_subject.transform, false);
            if (!_exampleIsDungeon)
            {
                holder.localPosition = new Vector3(plan.GeneratorAt.X, plan.GeneratorAt.Y, plan.GeneratorAt.Z);
                holder.localRotation = new Quaternion(plan.GeneratorTurn.X, plan.GeneratorTurn.Y, plan.GeneratorTurn.Z, plan.GeneratorTurn.W);
            }
            foreach (Transform part in _subject.transform)
            {
                if (part != holder && part.gameObject.activeSelf) OutsideParts.Add(part.gameObject);
            }

            // Until its first room stands, a dungeon shows its entrance.
            if (_exampleIsDungeon) _exampleHolder.SetActive(false);
        }

        /// <summary>
        /// Shows a dungeon's example or its entrance, framed afresh: inside it stands on its
        /// lowest floor and opens on its top floor; outside it is the location as before.
        /// </summary>
        private static void ShowInsideOrOut()
        {
            if (_exampleHolder == null || !_exampleIsDungeon || _subject == null) return;
            var inside = _inside && _exampleCopies > 0;
            foreach (var part in OutsideParts) if (part != null) part.SetActive(!inside);
            _exampleHolder.SetActive(inside);

            var floors = inside ? ExampleFloorsNow() : new List<float>(_placeFloors);
            if (floors.Count == 0) floors.Add(0f);
            // Inside, the example stands on its lowest floor; outside, the location stands on the
            // ground the game stands it on, not on a sunken part of it.
            var outside = _lastShown?.Source is PlaceSource place ? PlaceView.Ground(place.Contents, place.IsRoom) : 0f;
            _bodyMinY = Origin.y + (inside ? floors[floors.Count - 1] : outside) * _baseScale.y;
            _bounds = Unscaled(Measure(_subject));
            _cutFor = null;
            SetFloors(_lastShown, floors, open: inside);
            _frameRadius = -1f;
            _pan = Vector3.zero;
        }

        /// <summary>
        /// The room of the example at a point of the stage's picture (0 to 1 across, 0 to 1 up),
        /// what is cut away left out; null where there is none.
        /// </summary>
        public static PlacedRoom ExampleRoomAt(Vector2 point)
        {
            if (_exampleHolder == null || !_exampleHolder.activeInHierarchy || _camera == null || _examplePlaced == null || _exampleCopies == 0) return null;
            var eye = _camera.transform;
            var tan = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            var ray = eye.rotation * new Vector3((point.x * 2f - 1f) * tan * _camera.aspect, (point.y * 2f - 1f) * tan, 1f);

            var holder = _exampleHolder.transform;
            var from = holder.InverseTransformPoint(eye.position);
            var direction = holder.InverseTransformVector(ray);
            var below = Cutting ? holder.InverseTransformPoint(new Vector3(eye.position.x, Origin.y + CutAt * _scale, eye.position.z)).y : float.PositiveInfinity;
            // Rooms put away with a floor opened are passed by.
            var room = _examplePlaced.Pick(new Vec3(from.x, from.y, from.z), new Vec3(direction.x, direction.y, direction.z), below,
                _exampleIsDungeon ? r => ExampleRoomShown(r) == PlanRoomShown.Whole : (Func<PlacedRoom, bool>)null);
            return room != null && _examplePlaced.Rooms.IndexOf(room) < _exampleNext ? room : null;
        }

        /// <summary>Where a room of the example is in the stage's picture (0 to 1 across, 0 to 1 up), for the self-test to point at it; null when it is not in front of the camera.</summary>
        public static Vector2? ExamplePointOf(int index)
        {
            if (_exampleHolder == null || _camera == null || _examplePlaced == null || index < 0 || index >= _examplePlaced.Rooms.Count) return null;
            var at = _examplePlaced.Rooms[index].Position;
            var point = _camera.WorldToViewportPoint(_exampleHolder.transform.TransformPoint(new Vector3(at.X, at.Y, at.Z)));
            return point.z > 0f ? new Vector2(point.x, point.y) : (Vector2?)null;
        }

        /// <summary>Bounds measured on the stage, as if the model were at size one.</summary>
        private static Bounds Unscaled(Bounds measured) =>
            _scale <= 0f ? measured : new Bounds(Origin + (measured.center - Origin) / _scale, measured.size / _scale);
    }
}
