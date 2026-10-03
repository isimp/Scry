using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The example layout of the location shown, built on the stage from copies of its rooms
    /// (<see cref="ExampleLayouts"/>), a few milliseconds each frame in the order they were
    /// placed, the entrance first, a large room over several frames (<see cref="Ghost.Building"/>).
    /// A camp's rooms stand around the location's own parts, from where its generator is. A
    /// dungeon's stand where the game builds them, far above its entrance, so the stage shows
    /// either: inside, the example with the entrance's own parts put away, or outside, the
    /// entrance. Inside, the example opens on its top floor like a room. What stands is kept by
    /// <see cref="StageExample"/>; copying each room and framing it, here.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>What a room's model is to the stage: settled (loaded, or failed with no model) or still loading.</summary>
        public delegate bool RoomModel(string prefab, out GameObject model);

        /// <summary>A few milliseconds a frame for copying rooms, and at least one.</summary>
        private const double ExampleBudgetMs = 4.0;

        /// <summary>The example standing on the stage.</summary>
        private static readonly StageExample TheExample = new StageExample();

        /// <summary>Whether a dungeon's example is shown rather than its entrance; kept from one dungeon to the next.</summary>
        private static bool _inside = true;

        /// <summary>How many of the example's rooms stand on the stage, and how many it has.</summary>
        public static int ExampleRoomsShown => TheExample.Holder != null ? TheExample.Next : 0;
        public static int ExampleRoomsTotal => TheExample.Holder != null && TheExample.Placed != null ? TheExample.Placed.Rooms.Count : 0;

        /// <summary>The example as it stands on the stage, in its own space, for its plan; null before it is begun.</summary>
        public static DungeonExample ExampleShown => TheExample.Holder != null ? TheExample.Placed : null;

        /// <summary>Which way the camera looks over the example, as a yaw in the example's own space, for its plan to turn with the view.</summary>
        public static float ExampleViewYaw
        {
            get
            {
                if (TheExample.Holder == null || _camera == null) return 0f;
                var space = TheExample.Holder.transform;
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
                var holder = TheExample.Holder;
                if (holder == null || !Cutting || TheExample.IsDungeon && !holder.activeSelf) return null;
                return Floors[TheCut.Level] - holder.transform.localPosition.y;
            }
        }

        /// <summary>Whether the location shown is a dungeon whose example can be gone into.</summary>
        public static bool HasInside => TheExample.Holder != null && TheExample.IsDungeon;

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

        /// <summary>Whether what is shown is a dungeon's example inside, for the ground to keep the plain floor.</summary>
        private static bool ExampleInside => TheExample.IsDungeon && TheExample.Holder != null && TheExample.Holder.activeSelf;

        /// <summary>
        /// Copies the next rooms of the example onto the stage, when the location it is of is
        /// shown; a new example, or a new copy of the location, starts it again.
        /// </summary>
        public static void StepExample(Entry entry, DungeonExample example, DungeonPlan plan, RoomModel model)
        {
            if (_subject == null || !ReferenceEquals(_lastShown, entry) || example == null || plan == null) return;
            if (TheExample.Holder == null || !ReferenceEquals(TheExample.Of, example))
            {
                ForgetExample();
                TheExample.Begin(example, plan, _subject.transform, _layer);
            }
            TheExample.KeepToFloor(ExamplePlanFloor);
            var placedRooms = TheExample.Placed.Rooms;
            if (TheExample.Next >= placedRooms.Count) return;

            var made = Timing.Start();
            var watch = Stopwatch.StartNew();
            var placedAny = false;
            var stepped = false;
            while (TheExample.Next < placedRooms.Count && (!stepped || watch.Elapsed.TotalMilliseconds < ExampleBudgetMs))
            {
                if (TheExample.Build == null)
                {
                    var room = placedRooms[TheExample.Next];
                    if (!model(room.Room.Name, out var prefab)) break;
                    if (prefab == null)
                    {
                        TheExample.Next++;
                        continue;
                    }
                    var at = new Vector3(room.Position.X, room.Position.Y, room.Position.Z);
                    var turn = new Quaternion(room.Rotation.X, room.Rotation.Y, room.Rotation.Z, room.Rotation.W);
                    // A room's spawn points go by the rules of the location it is built in.
                    TheExample.BuildSpawns = new List<SpawnHere>();
                    TheExample.BuildPaints = TheExample.IsDungeon ? null : new List<GroundPaintAt>();
                    var rules = _lastShown?.Source is PlaceSource shown ? PlaceAssets.Asset(shown).OrNull()?.GetComponent<Location>() : null;
                    TheExample.Build = PlaceCopy.Begin(prefab, TheExample.Holder.transform, at, turn, _layer, keepColliders: TheExample.IsDungeon, local: true,
                        spawns: TheExample.BuildSpawns, rules: rules, paints: TheExample.BuildPaints);
                }
                stepped = true;
                if (!TheExample.Build.Go(ExampleBudgetMs - watch.Elapsed.TotalMilliseconds)) break;
                var copy = TheExample.Build.Result;
                var placed = placedRooms[TheExample.Next];
                TheExample.Build = null;
                TheExample.Next++;
                if (copy == null) continue;
                placedAny = true;
                TheExample.Add(placed, copy);

                // A dungeon room's floors; a camp keeps its location's.
                if (TheExample.IsDungeon) TheExample.ReadFloors(copy, placed, TheExample.BuildSpawns, _subject.transform, _layer);
                Tune(copy, audible: false);
                Populate(TheExample.BuildSpawns, copy);
                TheExample.BuildSpawns = null;
                // A camp's rooms paint the ground around it; a dungeon's stand under it.
                TakeGroundPaints(TheExample.BuildPaints, copy);
                TheExample.BuildPaints = null;
                TheExample.Copies++;

                if (TheExample.Copies == 1 && TheExample.IsDungeon) ShowInsideOrOut();
                else if (TheExample.Holder.activeInHierarchy) _bounds.Encapsulate(Unscaled(Measure(copy)));
            }
            if (placedAny && TheExample.IsDungeon && _inside && TheExample.Copies > 1) RefreshFloors(TheExample.FloorsNow());
            Timing.Add("stage example", made);
        }

        /// <summary>How many of the example's rooms are dimmed, below the floor opened, for the self-test.</summary>
        public static int ExampleRoomsDimmed => TheExample.DimmedCount;

        /// <summary>Whether every room of the example stands, is dimmed or is put away as the floor opened has it, for the self-test.</summary>
        public static bool ExampleRoomsKept => TheExample.Kept(ExamplePlanFloor);

        /// <summary>How many of the example's rooms are put away, above the floor opened, for the self-test.</summary>
        public static int ExampleRoomsAway => TheExample.Away;

        /// <summary>How many rooms of the example stand on the floor opened, end caps and dividers left out; -1 with no floor of an example opened.</summary>
        public static int ExampleRoomsOnFloor
        {
            get
            {
                var floor = ExampleOpenFloor;
                if (floor == null || TheExample.Placed == null) return -1;
                return TheExample.OnFloorCount(floor.Value);
            }
        }

        /// <summary>
        /// The floor opened over the example as its plan and stage honour it: none with the roof
        /// on, or where no room stands on it, which then shows every room rather than none.
        /// </summary>
        public static float? ExamplePlanFloor => ExampleOpenFloor is float floor && ExampleRoomsOnFloor > 0 ? floor : (float?)null;

        /// <summary>
        /// The middle across of the rooms standing on the floor opened in a dungeon's example, on
        /// the stage, and how far they reach from it; null with none opened or none standing on it.
        /// </summary>
        private static Vector3? ExampleFloorAcross()
        {
            if (!TheExample.IsDungeon || TheExample.Holder == null || !TheExample.Holder.activeInHierarchy || TheExample.Placed == null) return null;
            return TheExample.FloorAcross(ExamplePlanFloor);
        }

        /// <summary>Each room of the example read so far with the floors found in it alone, for the self-test to tell.</summary>
        public static List<string> ExampleRoomFloorsTold() => TheExample.RoomFloorsTold();

        /// <summary>How a room of the example shows with the floor opened: whole on it, else as its box says, below faintly or above not at all.</summary>
        public static PlanRoomShown ExampleRoomShown(PlacedRoom room) => TheExample.RoomShown(room, ExamplePlanFloor);

        /// <summary>Whether a room's copy of the example is dimmed, for its creatures to be dimmed with it.</summary>
        private static bool ExampleRoomDimmed(GameObject room) => TheExample.IsDimmed(room);

        /// <summary>Takes down the example's copies, as another entry or another example is shown, its rooms' creatures and paints with them.</summary>
        private static void ForgetExample()
        {
            TheExample.Forget();
            ForgetRoomCreatures();
            ForgetGroundPaints(rooms: true);
        }

        /// <summary>
        /// Shows a dungeon's example or its entrance, framed afresh: inside it stands on its
        /// lowest floor and opens on its top floor; outside it is the location as before.
        /// </summary>
        private static void ShowInsideOrOut()
        {
            if (TheExample.Holder == null || !TheExample.IsDungeon || _subject == null) return;
            var inside = _inside && TheExample.Copies > 0;
            TheExample.ShowInside(inside);

            var floors = inside ? TheExample.FloorsNow() : new List<float>(_placeFloors);
            if (floors.Count == 0) floors.Add(0f);
            // Inside, the example stands on its lowest floor; outside, the location stands on the
            // ground the game stands it on, not on a sunken part of it.
            var outside = _lastShown?.Source is PlaceSource place ? PlaceView.Ground(place.Contents, place.IsRoom) : 0f;
            _bodyMinY = Origin.y + (inside ? floors[floors.Count - 1] : outside) * _baseScale.y;
            _bounds = Unscaled(Measure(_subject));
            TheCut.ForgetEntry();
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
            var holderObject = TheExample.Holder;
            var placed = TheExample.Placed;
            if (holderObject == null || !holderObject.activeInHierarchy || _camera == null || placed == null || TheExample.Copies == 0) return null;
            var eye = _camera.transform;
            var ray = RayAt(point);

            var holder = holderObject.transform;
            var from = holder.InverseTransformPoint(eye.position);
            var direction = holder.InverseTransformVector(ray);
            var below = Cutting ? holder.InverseTransformPoint(new Vector3(eye.position.x, Origin.y + CutAt * _scale, eye.position.z)).y : float.PositiveInfinity;
            // Rooms put away with a floor opened are passed by.
            var room = placed.Pick(new Vec3(from.x, from.y, from.z), new Vec3(direction.x, direction.y, direction.z), below,
                TheExample.IsDungeon ? r => ExampleRoomShown(r) == PlanRoomShown.Whole : (Func<PlacedRoom, bool>)null);
            return room != null && placed.Rooms.IndexOf(room) < TheExample.Next ? room : null;
        }

        /// <summary>Where a room of the example is in the stage's picture (0 to 1 across, 0 to 1 up), for the self-test to point at it; null when it is not in front of the camera.</summary>
        public static Vector2? ExamplePointOf(int index)
        {
            var placed = TheExample.Placed;
            if (TheExample.Holder == null || _camera == null || placed == null || index < 0 || index >= placed.Rooms.Count) return null;
            var at = placed.Rooms[index].Position;
            var point = _camera.WorldToViewportPoint(TheExample.Holder.transform.TransformPoint(new Vector3(at.X, at.Y, at.Z)));
            return point.z > 0f ? new Vector2(point.x, point.y) : (Vector2?)null;
        }

        /// <summary>Bounds measured on the stage, as if the model were at size one.</summary>
        private static Bounds Unscaled(Bounds measured) =>
            _scale <= 0f ? measured : new Bounds(Origin + (measured.center - Origin) / _scale, measured.size / _scale);
    }
}
