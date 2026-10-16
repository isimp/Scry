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

        private static bool _insideUnknown;

        /// <summary>
        /// Whether the place shown is only seen from outside (<see cref="Explorer.InsideKnown"/>):
        /// its entrance shows whatever the switch says, and a place with nothing outside, whose
        /// rooms are what is seen of it, shows them with no floors inside to open.
        /// </summary>
        public static bool InsideUnknown
        {
            get => _insideUnknown;
            set
            {
                if (value == _insideUnknown) return;
                _insideUnknown = value;
                ShowInsideOrOut();
            }
        }

        /// <summary>Whether the switch, and what is known of the place, show its inside.</summary>
        private static bool InsideWanted => _inside && !_insideUnknown;

        /// <summary>How many of the example's rooms stand on the stage, and how many it has.</summary>
        public static int ExampleRoomsShown => TheExample.Holder != null ? TheExample.Next : 0;
        public static int ExampleRoomsTotal => TheExample.Holder != null && TheExample.Placed != null ? TheExample.Placed.Rooms.Count : 0;

        /// <summary>How many of the example's rooms made still have their floors to read, one a frame.</summary>
        public static int ExampleFloorsToRead => TheExample.FloorsToRead;

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
                if (holder == null || !Cutting || TheCut.Held || TheExample.IsDungeon && !holder.activeSelf) return null;
                return Floors[TheCut.Level] - holder.transform.localPosition.y;
            }
        }

        /// <summary>Whether the location shown is a dungeon whose example can be gone into.</summary>
        public static bool HasInside => TheExample.Holder != null && TheExample.IsDungeon;

        /// <summary>Whether it has an outside to switch to as well: a location drawing nothing but its example has none.</summary>
        public static bool HasOutside => HasInside && !TheExample.NothingOutside;

        /// <summary>Whether its inside is shown: as the switch is left, or always where it has nothing outside.</summary>
        public static bool ShowsInside => HasInside && (InsideWanted || TheExample.NothingOutside);

        /// <summary>What the location draws outside, for the self-test to tell.</summary>
        public static string OutsideTold() => TheExample.OutsideTold();

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
                ChooseCutForExample();
                // What is kept of a dungeon's rooms' floors is found while they are made.
                if (TheExample.IsDungeon) foreach (var room in TheExample.Placed.Rooms) FloorRecords.Want(room.Room.Name, true);
            }
            TheExample.KeepToFloor(ExamplePlanFloor, ExampleFloors);
            // A room's floors are read in a frame of their own: reading a big room takes as long
            // as a frame should, and making one beside it ran a frame past a quarter of a second.
            if (TheExample.IsDungeon && TheExample.ReadNext(_subject.transform, _layer))
            {
                if (InsideWanted && TheExample.Copies > 1) RefreshFloors(TheExample.FloorsNow(), TheExample.CeilingOver, TheExample.Top);
                return;
            }
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
                    TheExample.BuildRoll = new PlaceRoll();
                    TheExample.BuildPaints = TheExample.IsDungeon ? null : new List<GroundPaintAt>();
                    var rules = _lastShown?.Source is PlaceSource shown ? PlaceAssets.Asset(shown).OrNull()?.GetComponent<Location>() : null;
                    TheExample.Build = PlaceCopy.Begin(prefab, TheExample.Holder.transform, at, turn, _layer, keepColliders: TheExample.IsDungeon, local: true,
                        roll: TheExample.BuildRoll, rules: rules, paints: TheExample.BuildPaints);
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

                // A dungeon room's floors, read in a later frame; a camp keeps its location's.
                if (TheExample.IsDungeon) TheExample.ReadLater(copy, placed, TheExample.BuildRoll);
                Tune(copy, audible: false);
                Populate(TheExample.BuildRoll.Spawns, copy);
                TheExample.BuildRoll = null;
                // A camp's rooms paint the ground around it; a dungeon's stand under it.
                TakeGroundPaints(TheExample.BuildPaints, copy);
                TheExample.BuildPaints = null;
                TheExample.Copies++;

                if (TheExample.Copies == 1 && TheExample.IsDungeon) ShowInsideOrOut();
                else if (TheExample.Holder.activeInHierarchy) _bounds.Encapsulate(Unscaled(Measure(copy)));
            }
            if (placedAny && TheExample.IsDungeon && InsideWanted && TheExample.Copies > 1) RefreshFloors(TheExample.FloorsNow(), TheExample.CeilingOver, TheExample.Top);
            Timing.Add("stage example", made);
        }

        /// <summary>How many of the example's rooms are dimmed, below the floor opened, for the self-test.</summary>
        public static int ExampleRoomsDimmed => TheExample.DimmedCount;

        /// <summary>Whether every room of the example stands, is dimmed or is put away as the floor opened has it, for the self-test.</summary>
        public static bool ExampleRoomsKept => TheExample.Kept(ExamplePlanFloor, ExampleFloors);

        /// <summary>Holds every room of the example whole, dimmed or put away, for the self-test to film each; null lets the floor opened decide again.</summary>
        [Diagnostic]
        public static void ExampleHoldLook(RoomLook? look) => TheExample.HoldLook(look);

        /// <summary>The example's rooms' copies standing, for the self-test to tell what they are drawn with.</summary>
        [Diagnostic]
        public static List<GameObject> ExampleRoomCopies() => TheExample.RoomCopies();

        /// <summary>How many of the example's rooms are put away, above the floor opened, for the self-test.</summary>
        public static int ExampleRoomsAway => TheExample.Away;

        /// <summary>How many rooms of the example stand on the floor opened, end caps and dividers left out; -1 with no floor of an example opened.</summary>
        public static int ExampleRoomsOnFloor
        {
            get
            {
                var floor = ExampleOpenFloor;
                if (floor == null || TheExample.Placed == null) return -1;
                return TheExample.OnFloorCount(floor.Value, ExampleFloors);
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
            return TheExample.FloorAcross(ExamplePlanFloor, ExampleFloors);
        }

        /// <summary>The lights switched on in the example's rooms dimmed below the floor opened, for the self-test.</summary>
        public static List<Light> DimmedLights() => TheExample.DimmedLights();

        /// <summary>Each room of the example read so far with the floors found in it alone, for the self-test to tell.</summary>
        public static List<string> ExampleRoomFloorsTold() => TheExample.RoomFloorsTold();

        /// <summary>Each room of the example read so far with its box, doorways, ground and the floors it stands on, for the self-test to tell.</summary>
        public static List<string> ExampleRoomGroundTold() => TheExample.RoomGroundTold(FloorHeights);

        /// <summary>Whether a room of the example has a floor of its own at a height in the example's space, as the plan tiles it (<see cref="StageExample.HasFloorAt"/>).</summary>
        public static bool ExampleRoomHasFloorAt(PlacedRoom room, float floor) => TheExample.HasFloorAt(room, floor);

        /// <summary>How many of the example's rooms have had their ground read, so what depends on it is worked out again as more are.</summary>
        public static int ExampleGroundRead => TheExample.GroundRead;

        private static readonly List<float> ExampleFloorsNow = new List<float>();

        /// <summary>The floors, from the top down, as heights in the example's own space, as the floor opened is (<see cref="ExampleOpenFloor"/>); none without an example standing.</summary>
        public static IReadOnlyList<float> ExampleFloors
        {
            get
            {
                ExampleFloorsNow.Clear();
                var holder = TheExample.Holder;
                if (holder == null) return ExampleFloorsNow;
                var lift = holder.transform.localPosition.y;
                for (var i = 0; i < Floors.Count; i++) ExampleFloorsNow.Add(Floors[i] - lift);
                return ExampleFloorsNow;
            }
        }

        /// <summary>How a room of the example shows with the floor opened, by the plan's rule (<see cref="StageExample.RoomShown"/>): whole on it, else dimmed, or put away above it.</summary>
        public static PlanRoomShown ExampleRoomShown(PlacedRoom room) => TheExample.RoomShown(room, ExamplePlanFloor, ExampleFloors);

        /// <summary>The share of its colours a room's copy of the example keeps as dimmed, for its creatures to be dimmed with it; none where it is not.</summary>
        private static float? ExampleRoomDim(GameObject room) => TheExample.DimShareOf(room);

        /// <summary>How a room of the example shows: as the room gone into has it, else as the floor opened has it (<see cref="StageExample.LookOf"/>).</summary>
        public static RoomLook ExampleRoomLook(PlacedRoom room) => TheExample.LookOf(room, ExamplePlanFloor, ExampleFloors);

        /// <summary>Whether a room's copy of the example stands and the share of its colours it keeps as dimmed, for the self-test.</summary>
        [Diagnostic]
        public static (bool Standing, float? Keep)? ExampleRoomDrawn(PlacedRoom room) => TheExample.Drawn(room);

        /// <summary>Where a room's middle falls in the stage camera's view (x and y from 0 to 1 across it, z in front of the camera), for the self-test; null without a camera or example.</summary>
        [Diagnostic]
        public static Vector3? ExampleRoomInView(PlacedRoom room)
        {
            if (_camera == null || TheExample.Holder == null || room == null) return null;
            var middle = TheExample.Holder.transform.TransformPoint(new Vector3(room.Position.X, room.Position.Y, room.Position.Z));
            return _camera.WorldToViewportPoint(middle);
        }

        /// <summary>The room of a dungeon's example gone into; none while the whole example or a floor of it is shown.</summary>
        public static PlacedRoom RoomGoneInto => TheExample.Focused;

        /// <summary>A room's ground in the example's own space (<see cref="StageExample.GroundOf"/>), for the self-test.</summary>
        public static float ExampleRoomGround(PlacedRoom room) => TheExample.GroundOf(room);

        /// <summary>Where the stage cuts with a room gone into, above the location's root: head height over its ground, under its own ceiling where lower (<see cref="RoomFocus.Cut"/>).</summary>
        public static float RoomCut(PlacedRoom room)
        {
            var ground = TheExample.GroundOf(room);
            var lift = TheExample.Holder != null ? TheExample.Holder.transform.localPosition.y : 0f;
            return RoomFocus.Cut(ground, TheExample.CeilingOver(room, ground)) + lift;
        }

        /// <summary>A room's levels from the lowest up, its ground among them (<see cref="RoomLevels"/>).</summary>
        public static List<float> ExampleRoomLevels(PlacedRoom room) => TheExample.LevelsOf(room);

        /// <summary>The level of the room gone into that is open; none outside a room.</summary>
        public static float? RoomLevelOpen => TheExample.Focused != null ? TheExample.FocusLevel : (float?)null;

        /// <summary>Where the stage cuts over a level of a room, above the location's root: head height over it, under the room's own ceiling over it where lower.</summary>
        public static float RoomLevelCut(PlacedRoom room, float level)
        {
            var lift = TheExample.Holder != null ? TheExample.Holder.transform.localPosition.y : 0f;
            return RoomFocus.Cut(level, TheExample.CeilingOver(room, level)) + lift;
        }

        /// <summary>Opens a level of the room gone into: the cut stands at head height over it and the camera looks at it.</summary>
        public static void OpenRoomLevel(float level)
        {
            var room = TheExample.Focused;
            if (room == null) return;
            TheExample.OpenLevelOfFocused(level);
            if (!CutFromSide) TheCut.Hold(RoomLevelCut(room, level));
        }

        /// <summary>
        /// Goes into a room of a dungeon's example: the room stands whole, cut at head height over
        /// its own ground, the rooms on its level half dimmed, those below dimmed and those above
        /// put away; the camera glides to frame it, looking down into it.
        /// </summary>
        public static void GoInto(PlacedRoom room)
        {
            if (room == null || !TheExample.IsDungeon || TheExample.Placed == null || !TheExample.Placed.Rooms.Contains(room)) return;
            TheExample.Focus(room);
            // Cut from the side, the cut goes through the room's middle; from above, at head height over its ground.
            if (!CutFromSide) TheCut.Hold(RoomCut(room));
            TurnTo(Yaw, CutFromSide ? SidePitch : CutPitch);
        }

        /// <summary>Comes out of the room gone into, back to the whole example with the roof on.</summary>
        public static void ComeOut()
        {
            if (TheExample.Focused == null) return;
            TheExample.Unfocus();
            TheCut.Hold(null);
        }

        /// <summary>Takes down the example's copies, as another entry or another example is shown, its rooms' creatures and paints with them.</summary>
        private static void ForgetExample()
        {
            if (TheExample.Focused != null) TheCut.Hold(null);
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
            var inside = (InsideWanted || TheExample.NothingOutside) && TheExample.Copies > 0;
            TheExample.ShowInside(inside);

            // Seen only from outside, a place with nothing outside shows its rooms as the outside
            // they are, with the floors of the place alone, none inside to open.
            var floorsInside = inside && !_insideUnknown;
            var floors = floorsInside ? TheExample.FloorsNow() : new List<float>(_placeFloors);
            if (floors.Count == 0) floors.Add(0f);
            // Inside, the example stands on its lowest floor; outside, the location stands on the
            // ground the game stands it on, not on a sunken part of it.
            var outside = _lastShown?.Source is PlaceSource place ? PlaceView.Ground(place.Contents, place.IsRoom) : 0f;
            MeasureStanding(Origin.y + (inside ? floors[floors.Count - 1] : outside) * _baseScale.y);
            TheCut.ForgetEntry();
            if (floorsInside) SetFloors(_lastShown, floors, true, TheExample.CeilingOver, TheExample.Top);
            else SetFloors(_lastShown, floors, false, PlaceCeiling, _placeTop);
            FrameAnew();
            CenterView();
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
            var direction = holder.InverseTransformVector(ray);
            var from = PickFrom(holder, holder.InverseTransformPoint(eye.position), direction);
            var below = Cutting ? holder.InverseTransformPoint(new Vector3(eye.position.x, Origin.y + CutAt * _scale, eye.position.z)).y : float.PositiveInfinity;
            // Rooms put away are passed by; with a room gone into, those dimmed around it can be gone into next.
            var room = placed.Pick(new Vec3(from.x, from.y, from.z), new Vec3(direction.x, direction.y, direction.z), below,
                TheExample.IsDungeon ? r => TheExample.Focused != null ? ExampleRoomLook(r) != RoomLook.Away : ExampleRoomShown(r) == PlanRoomShown.Whole : (Func<PlacedRoom, bool>)null);
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
