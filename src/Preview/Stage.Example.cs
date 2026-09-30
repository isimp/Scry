using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>
    /// The example layout of the location shown, built on the stage from copies of its rooms
    /// (<see cref="ExampleLayouts"/>), a few each frame in the order they were placed, the
    /// entrance first. A camp's rooms stand around the location's own parts, from where its
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
            while (_exampleNext < _examplePlaced.Rooms.Count && (!placedAny || watch.Elapsed.TotalMilliseconds < ExampleBudgetMs))
            {
                var room = _examplePlaced.Rooms[_exampleNext];
                if (!model(room.Room.Name, out var prefab)) break;
                _exampleNext++;
                placedAny = true;
                if (prefab == null) continue;

                var holder = _exampleHolder.transform;
                var at = holder.TransformPoint(new Vector3(room.Position.X, room.Position.Y, room.Position.Z));
                var turn = holder.rotation * new Quaternion(room.Rotation.X, room.Rotation.Y, room.Rotation.Z, room.Rotation.W);
                var copy = PlaceCopy.Make(prefab, holder, at, turn, _layer);
                if (copy == null) continue;
                Tune(copy, audible: false);
                _exampleCopies++;

                if (_exampleCopies == 1 && _exampleIsDungeon) ShowInsideOrOut();
                else if (_exampleHolder.activeInHierarchy) _bounds.Encapsulate(Unscaled(Measure(copy)));
            }
            Timing.Add("stage example", made);
        }

        /// <summary>Takes down the example's copies, as another entry or another example is shown.</summary>
        private static void ForgetExample()
        {
            if (_exampleHolder != null) Object.Destroy(_exampleHolder);
            _exampleHolder = null;
            _exampleOf = null;
            _examplePlaced = null;
            _exampleNext = 0;
            _exampleCopies = 0;
            OutsideParts.Clear();
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

            var floors = inside ? PlaceView.ExampleFloors(_examplePlaced) : new List<float> { 0f };
            if (floors.Count == 0) floors.Add(0f);
            _bodyMinY = Origin.y + floors[floors.Count - 1] * _baseScale.y;
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
