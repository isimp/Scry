using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        /// <summary>How many rooms of examples had their rays cast with their copy switched off this session, for the self-test.</summary>
        [Diagnostic]
        public static int ExampleReadsSwitchedOff => TheExample.ReadSwitchedOff;

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
            if (TheExample.Focused != null)
            {
                // The rooms above come back as the room's cut rises past their ground.
                TheExample.FocusCut = TheCut.At - ExampleLift;
                TheExample.FocusCutFrom = TheCut.HeldFrom - ExampleLift;
                KeepRoomBounds();
            }
            TheExample.KeepToFloor(ExamplePlanFloor, ExampleFloors);
            // A room's floors are read in a frame of their own: reading a big room takes as long
            // as a frame should, and making one beside it ran a frame past a quarter of a second.
            if (TheExample.IsDungeon && TheExample.ReadNext(_subject.transform, _layer))
            {
                if (InsideWanted && TheExample.Copies > 1) RefreshExampleFloors();
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
                    TheExample.BuildRoll = new PlaceRoll { WantsLoot = TheExample.IsDungeon };
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
                if (TheExample.IsDungeon) TheExample.KeepLoot(placed, TheExample.BuildRoll.Loot);
                TheExample.BuildRoll = null;
                // A camp's rooms paint the ground around it; a dungeon's stand under it.
                TakeGroundPaints(TheExample.BuildPaints, copy);
                TheExample.BuildPaints = null;
                TheExample.Copies++;

                if (TheExample.Copies == 1 && TheExample.IsDungeon) ShowInsideOrOut();
                else if (TheExample.Holder.activeInHierarchy) _bounds.Encapsulate(Unscaled(Measure(copy)));
            }
            if (placedAny && TheExample.IsDungeon && InsideWanted && TheExample.Copies > 1) RefreshExampleFloors();
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

        /// <summary>
        /// Where a room's middle across falls in the stage camera's view a metre over its level
        /// open, or over its ground (x and y from 0 to 1 across it, z in front of the camera), for
        /// the self-test: what is seen of it under the cut, as a tall room's box reaches far above
        /// it. Null without a camera or example.
        /// </summary>
        [Diagnostic]
        public static Vector3? ExampleRoomInView(PlacedRoom room)
        {
            if (_camera == null || TheExample.Holder == null || room == null) return null;
            var level = room == TheExample.Focused ? TheExample.FocusLevel : TheExample.GroundOf(room);
            var middle = TheExample.Holder.transform.TransformPoint(new Vector3(room.Position.X, level + 1f, room.Position.Z));
            return _camera.WorldToViewportPoint(middle);
        }

        /// <summary>The room of a dungeon's example gone into; none while the whole example or a floor of it is shown.</summary>
        public static PlacedRoom RoomGoneInto => TheExample.Focused;

        /// <summary>A room's ground in the example's own space (<see cref="StageExample.GroundOf"/>), for the self-test.</summary>
        public static float ExampleRoomGround(PlacedRoom room) => TheExample.GroundOf(room);

        /// <summary>
        /// Where the stage cuts with a room gone into from above, above the location's root: for a
        /// room with no real floor that climbs, a head's height over its lowest doorways, where
        /// its slope starts, or over its top where it is a shaft (<see cref="SlopeCut"/>); else
        /// head height over its ground, under its own ceiling where lower (<see cref="RoomFocus.Cut"/>).
        /// </summary>
        public static float RoomCut(PlacedRoom room)
        {
            var lift = TheExample.Holder != null ? TheExample.Holder.transform.localPosition.y : 0f;
            if (TheExample.SlopeOf(room) is RoomCutPlane plane) return plane.Low.Y + lift;
            var ground = TheExample.GroundOf(room);
            return RoomFocus.Cut(ground, TheExample.CeilingOver(room, ground)) + lift;
        }

        /// <summary>The cut over a room as it would be gone into (<see cref="SlopeCut"/>), in the example's own space, for the self-test; none for one cut over its ground.</summary>
        [Diagnostic]
        public static RoomCutPlane? ExampleRoomSlope(PlacedRoom room) => TheExample.SlopeOf(room);

        /// <summary>The sloping cut over the room gone into, in the example's own space, before the cut is moved by hand; none while it is cut flat or from the side.</summary>
        private static RoomCutPlane? _slope;
        private static PlacedRoom _slopeOf;

        /// <summary>
        /// Holds the cut over a room gone into: from the side, none; from above, along its slope
        /// where it climbs with no real floor, else at the height <see cref="RoomCut"/> or a level
        /// of it gives.
        /// </summary>
        private static void HoldRoomCut(PlacedRoom room, float level)
        {
            _slope = null;
            _slopeOf = null;
            if (CutFromSide)
            {
                TheCut.Hold(null);
                return;
            }
            var lift = ExampleLift;
            // Within the room's ruler: no lower than over its lowest level, raised past its top its roof on.
            var ruler = RoomRulerNow;
            var least = ruler?.Least ?? float.NegativeInfinity;
            var most = ruler?.High ?? float.PositiveInfinity;
            if (TheExample.SlopeOf(room) is RoomCutPlane plane)
            {
                if (plane.Sloping)
                {
                    _slope = plane;
                    _slopeOf = room;
                }
                TheCut.Hold(plane.Low.Y + lift, least, most);
                return;
            }
            TheCut.Hold(RoomLevelCut(room, level), least, most);
        }

        /// <summary>Lets go of the cut over a room gone into, the roof back on.</summary>
        private static void LetGoOfRoomCut()
        {
            _slope = null;
            _slopeOf = null;
            TheCut.Hold(null);
        }

        /// <summary>
        /// The sloping cut laid now, in the example's own space, moved up or down as the cut has
        /// been by hand; false while the room gone into is cut flat, from the side, or not at all.
        /// </summary>
        private static bool SlopeLaid(out RoomCutPlane plane)
        {
            plane = default;
            if (!(_slope is RoomCutPlane slope) || _slopeOf == null || _slopeOf != TheExample.Focused || !TheCut.Held || TheCut.HeldRoofOn || CutFromSide || TheExample.Holder == null) return false;
            plane = slope.Raised(CutAt - TheExample.Holder.transform.localPosition.y - slope.Low.Y);
            return true;
        }

        /// <summary>Whether the cut laid over the room gone into slopes, for the self-test.</summary>
        [Diagnostic]
        public static bool SlopeCutLaid => SlopeLaid(out _);

        /// <summary>How many of a room's drawn parts reach under the cut where each stands, of how many it has, for the self-test to tell a room cut away from one shown.</summary>
        [Diagnostic]
        public static (int Under, int All) ExampleRoomPartsUnderCut(PlacedRoom room)
        {
            var copy = TheExample.CopyOf(room);
            if (copy == null) return (0, 0);
            var under = 0;
            var all = 0;
            foreach (var part in copy.GetComponentsInChildren<Renderer>())
            {
                if (part == null || !part.enabled) continue;
                all++;
                var bounds = part.bounds;
                if (bounds.min.y < CutHeightAt(bounds.center)) under++;
            }
            return (under, all);
        }

        /// <summary>A room's levels from the lowest up, its ground among them (<see cref="RoomLevels"/>).</summary>
        public static List<float> ExampleRoomLevels(PlacedRoom room) => TheExample.LevelsOf(room);

        /// <summary>The ways between a dungeon example's rooms (<see cref="DungeonWays"/>); none for a camp's or with no example.</summary>
        public static DungeonWays ExampleWays => TheExample.Ways;

        /// <summary>How many of the example's rooms have had their rays read, which their grounds and levels come from.</summary>
        public static int ExampleRoomsRead => TheExample.GroundRead;

        /// <summary>The creatures this copy rolled in a room of the example, by their prefab's name, made or still waiting.</summary>
        public static IReadOnlyList<string> RoomCreatures(PlacedRoom room) => Creatures.RolledIn(TheExample.CopyOf(room));

        /// <summary>The chests and pickups this copy's rolls left in a room of a dungeon's example, each with the chance its page gives it.</summary>
        public static IReadOnlyList<(string Prefab, float Chance)> RoomLoot(PlacedRoom room) => TheExample.LootOf(room);

        /// <summary>The level of the room gone into the cut stands over, as its ruler lights it: the highest of its own levels under the cut; none with its roof on or outside a room.</summary>
        public static float? RoomLevelOpen
        {
            get
            {
                if (TheExample.Focused == null || TheCut.HeldRoofOn) return null;
                if (RoomRulerNow == null || !TheCut.Cutting) return TheExample.FocusLevel;
                var cut = TheCut.At - ExampleLift;
                float? open = null;
                foreach (var level in _roomRulerLevels) if (level <= cut + 0.01f) open = level;
                return open ?? TheExample.FocusLevel;
            }
        }

        /// <summary>
        /// The floors the ruler opens the example by, above the location's root as the cut holds
        /// them, with the ceiling over each: a dungeon's, the marks of its plan's levels, its floors
        /// only until its levels are known; a camp's, its floors.
        /// </summary>
        private static (List<float> Floors, System.Func<float, float?> Ceiling) ExampleMarksNow()
        {
            if (!TheExample.IsDungeon) return (TheExample.FloorsNow(), TheExample.CeilingOver);
            var lift = ExampleLift;
            var marks = TheExample.LevelMarks();
            var floors = marks.Select(m => m.Floor + lift).ToList();
            var ceilings = marks.Select(m => m.Ceiling + lift).ToList();
            float? CeilingOf(float floor)
            {
                for (var i = 0; i < floors.Count; i++) if (Mathf.Abs(floors[i] - floor) < 0.001f) return ceilings[i];
                return null;
            }
            if (floors.Count == 0) return (TheExample.FloorsNow(), TheExample.CeilingOver);
            return (floors, CeilingOf);
        }

        /// <summary>Hands the cut the example's floors anew, as more of it is read or how it opens changes.</summary>
        private static void RefreshExampleFloors()
        {
            var (floors, ceiling) = ExampleMarksNow();
            RefreshFloors(floors, ceiling, TheExample.Top);
        }

        /// <summary>The lowest ground of the rooms of the level opened, in the example's own space, for the plan to leave out only what lies below them; none for a camp or with none opened.</summary>
        public static float? ExampleOpenMarkLow => ExampleOpenFloor is float floor ? TheExample.MarkLow(floor, ExampleFloors) : null;

        /// <summary>The words of a mark of the ruler over a dungeon's example: its level by its depth from the entrance; none for a camp or a place.</summary>
        public static string ExampleMark(int index)
        {
            if (!TheExample.IsDungeon || TheExample.Ways?.Entrance == null) return null;
            var cuts = TheExample.LevelCutsNow();
            if (cuts == null) return null;
            // Worked out again only as the groups change, as the ruler asks each frame.
            if (!ReferenceEquals(cuts, _marksOf))
            {
                _marksOf = cuts;
                Marks.Clear();
                var entrance = TheExample.GroundOf(TheExample.Ways.Entrance);
                foreach (var mark in TheExample.LevelMarks()) Marks.Add(StageWords.LevelMark(PlanBoxWords.LevelDepth(mark.Floor - entrance)));
            }
            return index >= 0 && index < Marks.Count ? Marks[index] : null;
        }

        private static readonly List<string> Marks = new List<string>();
        private static LevelCuts _marksOf;

        /// <summary>The films a level opened takes, from its lowest group up: each group's cut above the location's root; none where one cut opens the level, in a room gone into, cut from the side, or for a camp.</summary>
        private static List<(int Group, float Cut)> FilmsNow()
        {
            if (!TheExample.IsDungeon || TheCut.Held || CutFromSide || !(ExampleOpenFloor is float floor)) return null;
            var groups = TheExample.FilmGroups(floor, ExampleFloors);
            var cuts = TheExample.LevelCutsNow();
            if (groups == null || cuts == null) return null;
            var lift = ExampleLift;
            var films = new List<(int, float)>();
            foreach (var group in groups) films.Add((group, cuts.Groups[group].Cut + lift));
            return films;
        }

        /// <summary>By the plan's levels, the level the mark opened opens; none by floors, with a room gone into, or none opened.</summary>
        private static int? ExampleLevelOpen => TheExample.Focused == null && ExampleOpenFloor is float floor ? TheExample.LevelOpenBy(floor, ExampleFloors) : null;

        /// <summary>
        /// The height the camera looks at with a floor opened, above the location's root: by the
        /// plan's levels, the highest ground of the level the mark opens, so every step of a level
        /// keeps one frame; else the floor opened.
        /// </summary>
        private static float LookedFloorHeight()
        {
            if (TheExample.Focused == null && ExamplePlanFloor is float floor && TheExample.LevelTop(floor, ExampleFloors) is float top) return top + ExampleLift;
            return Floors[TheCut.Level];
        }

        /// <summary>The groups of rooms one cut opens on each of the example's levels, for the self-test.</summary>
        [Diagnostic]
        public static LevelCuts ExampleLevelCuts => TheExample.LevelCutsNow();

        /// <summary>How high the example stands over the location's root, its own space to the stage's.</summary>
        private static float ExampleLift => TheExample.Holder != null ? TheExample.Holder.transform.localPosition.y : 0f;

        /// <summary>The ruler of the room gone into, cut from above, in the stage's heights; worked out again as the room or what is read of the example changes; none outside a room or cut from the side.</summary>
        public static RoomRuler RoomRulerNow
        {
            get
            {
                var room = TheExample.Focused;
                if (room == null || CutFromSide || TheExample.Placed == null) return null;
                if (ReferenceEquals(room, _roomRulerOf) && _roomRulerRead == ExampleRoomsRead) return _roomRuler;
                _roomRulerOf = room;
                _roomRulerRead = ExampleRoomsRead;
                _roomRulerLevels = TheExample.LevelsOf(room);
                _roomRuler = RulerOf(room, _roomRulerLevels);
                return _roomRuler;
            }
        }

        private static RoomRuler _roomRuler;
        private static PlacedRoom _roomRulerOf;
        private static int _roomRulerRead = -1;
        private static List<float> _roomRulerLevels = new List<float>();

        /// <summary>The ruler of a room: its ground and own levels, its top, and the ground and top of each room above it, which come back as the cut rises past them.</summary>
        private static RoomRuler RulerOf(PlacedRoom room, List<float> levels)
        {
            var lift = ExampleLift;
            var ground = TheExample.GroundOf(room);
            var above = new List<(float, float)>();
            foreach (var other in TheExample.Placed.Rooms)
            {
                if (other == room || other.Room.EndCap || other.Room.Divider) continue;
                var otherGround = TheExample.GroundOf(other);
                if (RoomFocus.Look(false, otherGround, ground) != RoomLook.Away) continue;
                above.Add((otherGround + lift, other.Position.Y + other.Room.Size.Y / 2f + lift));
            }
            return RoomRuler.Of(ground + lift, levels.Select(l => l + lift), room.Position.Y + room.Room.Size.Y / 2f + lift, above);
        }

        /// <summary>Keeps the room's cut within its ruler as more of the example is read.</summary>
        private static void KeepRoomBounds()
        {
            var before = _roomRuler;
            var ruler = RoomRulerNow;
            if (ruler != null && !ReferenceEquals(ruler, before) && TheCut.Held) TheCut.Bound(ruler.Least, ruler.High);
        }

        /// <summary>Where the stage cuts over a tick of the room's ruler: a level of its own as its chip cuts it, the rooms above on one at head height over it, and past where the room was first cut, so they come back.</summary>
        private static float RoomTickCut(RoomRuler.Tick tick) =>
            tick.Own ? RoomLevelCut(TheExample.Focused, tick.Height - ExampleLift)
                : Mathf.Max(RoomFocus.Cut(tick.Height, null), TheCut.HeldFrom + RoomFocus.BackOver + 0.1f);

        /// <summary>Opens a tick of the room's ruler: a level of its own as its chip does, or the rooms above on one, cut at head height over it as they come back.</summary>
        public static void OpenRoomTick(int index)
        {
            var ruler = RoomRulerNow;
            if (ruler == null || index < 0 || index >= ruler.Ticks.Count) return;
            var tick = ruler.Ticks[index];
            if (tick.Own) OpenRoomLevel(tick.Height - ExampleLift);
            else TheCut.HoldAt(RoomTickCut(tick));
        }

        /// <summary>Cuts the room gone into at a height set on its ruler: kept within it, over its top its roof on.</summary>
        public static void CutRoomAt(float height)
        {
            if (RoomRulerNow != null && TheCut.Held) TheCut.HoldAt(height);
        }

        /// <summary>Steps the room's cut to the tick above or below, as the wheel over its ruler and Page Up and Down do: over the top tick its roof on, down from there the top tick again.</summary>
        public static void StepRoomCut(bool down)
        {
            var ruler = RoomRulerNow;
            if (ruler == null || !TheCut.Held || ruler.Ticks.Count == 0) return;
            if (TheCut.HeldRoofOn)
            {
                if (down) OpenRoomTick(ruler.Ticks.Count - 1);
                return;
            }
            var at = TheCut.At;
            if (down)
            {
                for (var i = ruler.Ticks.Count - 1; i >= 0; i--)
                {
                    if (RoomTickCut(ruler.Ticks[i]) < at - 0.01f)
                    {
                        OpenRoomTick(i);
                        return;
                    }
                }
                return;
            }
            for (var i = 0; i < ruler.Ticks.Count; i++)
            {
                if (RoomTickCut(ruler.Ticks[i]) > at + 0.01f)
                {
                    OpenRoomTick(i);
                    return;
                }
            }
            TheCut.HoldAt(float.PositiveInfinity);
        }

        /// <summary>The height the room gone into is cut at, in the example's own space, as the rooms above come back by, for the self-test.</summary>
        [Diagnostic]
        public static float ExampleCutForTest => TheExample.FocusCut;

        /// <summary>Where the room gone into was first cut, in the example's own space, for the self-test.</summary>
        [Diagnostic]
        public static float ExampleCutFromForTest => TheExample.FocusCutFrom;

        /// <summary>Where the room gone into was first cut, in the stage's heights, as its ruler lights the rooms above by.</summary>
        public static float RoomCutFrom => TheCut.HeldFrom;

        /// <summary>Whether the room gone into has its roof on, the rooms above it back.</summary>
        public static bool RoomRoofOn => TheExample.Focused != null && TheCut.HeldRoofOn;

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
            HoldRoomCut(room, level);
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
            // Cut from the side, the cut goes through the room's middle, any floor opened let go
            // of; from above, at head height over its ground, or along its slope where it climbs with no real floor.
            HoldRoomCut(room, TheExample.FocusLevel);
            TurnTo(Yaw, CutFromSide ? SidePitch : CutPitch);
            FreshView();
        }

        /// <summary>Comes out of the room gone into, back to the whole example with the roof on.</summary>
        public static void ComeOut()
        {
            if (TheExample.Focused == null) return;
            TheExample.Unfocus();
            LetGoOfRoomCut();
            FreshView();
        }

        /// <summary>Takes down the example's copies, as another entry or another example is shown, its rooms' creatures and paints with them.</summary>
        private static void ForgetExample()
        {
            if (TheExample.Focused != null) LetGoOfRoomCut();
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
            var (insideFloors, insideCeiling) = ExampleMarksNow();
            var floors = floorsInside ? insideFloors : new List<float>(_placeFloors);
            if (floors.Count == 0) floors.Add(0f);
            // Inside, the example stands on its lowest floor; outside, the location stands on the
            // ground the game stands it on, not on a sunken part of it.
            var outside = _lastShown?.Source is PlaceSource place ? PlaceView.Ground(place.Contents, place.IsRoom) : 0f;
            MeasureStanding(Origin.y + (inside ? floors[floors.Count - 1] : outside) * _baseScale.y);
            TheCut.ForgetEntry();
            if (floorsInside) SetFloors(_lastShown, floors, true, insideCeiling, TheExample.Top);
            else SetFloors(_lastShown, floors, false, PlaceCeiling, _placeTop);
            FrameAnew();
            CenterView();
        }

        /// <summary>Whether a room of a dungeon's example can be chosen on the stage (<see cref="RoomLight.Choosable"/>), only what decides it looked at.</summary>
        private static readonly Func<PlacedRoom, bool> ChoosableOnStage = r => TheExample.Focused != null
            ? RoomLight.Choosable(true, ExampleRoomLook(r), PlanRoomShown.Whole)
            : RoomLight.Choosable(false, RoomLook.Whole, ExampleRoomShown(r));

        /// <summary>
        /// The room of the example at a point of the stage's picture (0 to 1 across, 0 to 1 up),
        /// what is cut away left out, an end cap or a divider taken as the room it closes; null
        /// where there is none.
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
            var below = Cutting && !SlopeLaid(out _) ? holder.InverseTransformPoint(new Vector3(eye.position.x, Origin.y + CutAt * _scale, eye.position.z)).y : float.PositiveInfinity;
            // Rooms put away are passed by; those dimmed, around a room gone into or below a floor opened, can be gone into next.
            var room = placed.Pick(new Vec3(from.x, from.y, from.z), new Vec3(direction.x, direction.y, direction.z), below,
                TheExample.IsDungeon ? ChoosableOnStage : null);
            // An end cap or a divider is part of the room it closes: that room is the one under the mouse.
            return room != null && placed.Rooms.IndexOf(room) < TheExample.Next ? ExamplePlan.LooksAs(room) : null;
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
