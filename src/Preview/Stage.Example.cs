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
        private static bool _exampleIsDungeon;
        private static readonly List<GameObject> OutsideParts = new List<GameObject>();

        /// <summary>Whether a dungeon's example is shown rather than its entrance; kept from one dungeon to the next.</summary>
        private static bool _inside = true;

        /// <summary>How many of the example's rooms stand on the stage, and how many it has.</summary>
        public static int ExampleRoomsShown => _exampleHolder != null ? _exampleNext : 0;
        public static int ExampleRoomsTotal => _exampleHolder != null && _examplePlaced != null ? _examplePlaced.Rooms.Count : 0;

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
                    _exampleBuild = PlaceCopy.Begin(prefab, _exampleHolder.transform, at, turn, _layer, keepColliders: _exampleIsDungeon, local: true);
                }
                stepped = true;
                if (!_exampleBuild.Go(ExampleBudgetMs - watch.Elapsed.TotalMilliseconds)) break;
                var copy = _exampleBuild.Result;
                _exampleBuild = null;
                _exampleNext++;
                if (copy == null) continue;
                placedAny = true;

                // A dungeon room's floors; a camp keeps its location's.
                if (_exampleIsDungeon) ReadFloors(copy, _exampleCopies);
                Tune(copy, audible: false);
                _exampleCopies++;

                if (_exampleCopies == 1 && _exampleIsDungeon) ShowInsideOrOut();
                else if (_exampleHolder.activeInHierarchy) _bounds.Encapsulate(Unscaled(Measure(copy)));
            }
            if (placedAny && _exampleIsDungeon && _inside && _exampleCopies > 1) RefreshFloors(ExampleFloorsNow());
            Timing.Add("stage example", made);
        }

        /// <summary>
        /// Reads a dungeon room's floors, once, as it stands. While the entrance is shown, the
        /// example sleeps, and the room alone is woken for it beside the example: a dungeon's
        /// example stands where its location does, so the room stands the same in either.
        /// </summary>
        private static void ReadFloors(GameObject copy, int patch)
        {
            var read = Timing.Start();
            var asleep = !_exampleHolder.activeSelf;
            if (asleep) copy.transform.SetParent(_subject.transform, false);
            RoomHits.Clear();
            _exampleGround += FloorProbe.Read(copy, _subject.transform, _layer, RoomHits, patch);
            ExamplePatches.Add(FloorFinder.Patch(RoomHits));
            RoomHits.Clear();
            if (asleep) copy.transform.SetParent(_exampleHolder.transform, false);
            Timing.Add("example floors", read);
        }

        /// <summary>Takes down the example's copies, as another entry or another example is shown.</summary>
        private static void ForgetExample()
        {
            _exampleBuild?.Cancel();
            _exampleBuild = null;
            if (_exampleHolder != null) Object.Destroy(_exampleHolder);
            _exampleHolder = null;
            _exampleOf = null;
            _examplePlaced = null;
            _exampleNext = 0;
            _exampleCopies = 0;
            ExamplePatches.Clear();
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
            var found = FloorFinder.Floors(ExamplePatches, _exampleGround);
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
            var room = _examplePlaced.Pick(new Vec3(from.x, from.y, from.z), new Vec3(direction.x, direction.y, direction.z), below);
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
