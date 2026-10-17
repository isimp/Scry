using System;
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

        /// <summary>Whether the location draws nothing outside but the example (the sealed tower's): then its outside would show an empty stage, so the inside shows however the switch is left.</summary>
        public bool NothingOutside { get; private set; }

        /// <summary>The room being copied, with what it was rolled to and the paints read off it.</summary>
        public Ghost.Building Build;
        public PlaceRoll BuildRoll;
        public List<GroundPaintAt> BuildPaints;

        /// <summary>The rooms standing, each with its copy, to dim or put away those off the floor opened.</summary>
        private readonly List<KeyValuePair<PlacedRoom, GameObject>> _rooms = new List<KeyValuePair<PlacedRoom, GameObject>>();

        /// <summary>The rooms' copies dimmed (<see cref="Dim"/>), each as it is: half, on the level of the room gone into, or below.</summary>
        private readonly Dictionary<GameObject, RoomLook> _dimmed = new Dictionary<GameObject, RoomLook>();

        /// <summary>How each room shows with the room gone into, worked out again as more rooms are read.</summary>
        private readonly Dictionary<PlacedRoom, RoomLook> _focusLooks = new Dictionary<PlacedRoom, RoomLook>();
        private int _focusRead = -1;
        private Vector3? _focusAcross;

        /// <summary>What each dungeon room's own rays found, to tell the floors it stands on (<see cref="FloorFinder.Holds"/>).</summary>
        private readonly Dictionary<PlacedRoom, FloorPatch> _roomGround = new Dictionary<PlacedRoom, FloorPatch>();

        /// <summary>The location's own parts beside the example, put away while a dungeon's example is shown.</summary>
        private readonly List<GameObject> _outside = new List<GameObject>();


        /// <summary>What each room's rays found; the rays of the room being read.</summary>
        private readonly List<FloorPatch> _patches = new List<FloorPatch>();
        private readonly List<FloorHit> _hits = new List<FloorHit>();

        /// <summary>The rooms counted on the floor opened, worked out once a frame.</summary>
        private int _countedAt = -1;
        private float? _countedFloor;
        private int _countedRooms;

        /// <summary>The rooms with a floor of their own at one of the floors (<see cref="HasFloorAt"/>), worked out again as the floors change or more rooms are read.</summary>
        private readonly HashSet<PlacedRoom> _ownFloor = new HashSet<PlacedRoom>();
        private readonly List<float> _ownFloorOf = new List<float>();
        private int _ownFloorRead = -1;

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
            Ways = IsDungeon ? DungeonWays.Of(Placed.Rooms) : null;

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

        /// <summary>A room's copy standing, its water drawn as a faint sheet (<see cref="HoldsWater"/>).</summary>
        public void Add(PlacedRoom room, GameObject copy)
        {
            _rooms.Add(new KeyValuePair<PlacedRoom, GameObject>(room, copy));
            if (WaterParts.MakeFaint(copy)) HoldsWater = true;
        }

        /// <summary>Whether a room's copy holds the game's water, drawn as a faint sheet (<see cref="WaterParts"/>).</summary>
        public bool HoldsWater { get; private set; }

        /// <summary>The ways between a dungeon's rooms (<see cref="DungeonWays"/>); none for a camp's.</summary>
        public DungeonWays Ways { get; private set; }

        /// <summary>The chests and pickups each dungeon room's roll left in its copy, each with the chance its page gives it.</summary>
        private readonly Dictionary<PlacedRoom, List<(string Prefab, float Chance)>> _loot = new Dictionary<PlacedRoom, List<(string, float)>>();

        /// <summary>Keeps the chests and pickups a room's roll left in its copy.</summary>
        public void KeepLoot(PlacedRoom room, List<(string Prefab, float Chance)> loot) => _loot[room] = loot;

        /// <summary>The chests and pickups a room's roll left in its copy; none for a room not yet standing.</summary>
        public IReadOnlyList<(string Prefab, float Chance)> LootOf(PlacedRoom room) =>
            room != null && _loot.TryGetValue(room, out var loot) ? loot : (IReadOnlyList<(string, float)>)Array.Empty<(string, float)>();

        /// <summary>A room's copy standing, or none.</summary>
        public GameObject CopyOf(PlacedRoom room)
        {
            foreach (var pair in _rooms) if (pair.Key == room) return pair.Value;
            return null;
        }

        /// <summary>The rooms made whose floors are still to be read, each in a frame of its own, so no frame both makes a room and reads one's floors.</summary>
        private readonly Queue<(GameObject Copy, PlacedRoom Room, PlaceRoll Roll)> _toRead = new Queue<(GameObject, PlacedRoom, PlaceRoll)>();

        /// <summary>How many rooms made still have their floors to read.</summary>
        public int FloorsToRead => _toRead.Count;

        /// <summary>How many rooms had their rays cast with their copy switched off this session, which meet nothing; for the self-test, which expects none.</summary>
        public int ReadSwitchedOff { get; private set; }

        /// <summary>Has a room's floors read in a later frame (<see cref="ReadNext"/>).</summary>
        public void ReadLater(GameObject copy, PlacedRoom room, PlaceRoll roll) => _toRead.Enqueue((copy, room, roll));

        /// <summary>
        /// Reads the floors of the next room waiting (<see cref="ReadFloors"/>), once what is kept
        /// of its kind is found; false with none read, none waiting or its kind still being found,
        /// so rooms go on being made meanwhile.
        /// </summary>
        public bool ReadNext(Transform subject, int layer)
        {
            if (_toRead.Count == 0) return false;
            var (copy, room, roll) = _toRead.Peek();
            if (copy != null && !FloorRecords.Ready(room.Room.Name, true)) return false;
            _toRead.Dequeue();
            if (copy != null) ReadFloors(copy, room, roll, subject, layer);
            return true;
        }

        /// <summary>The share of its colours a room's copy keeps as dimmed (<see cref="Dim"/>); none where it is not.</summary>
        public float? DimShareOf(GameObject room) => _dimmed.TryGetValue(room, out var look) ? Dim.ShareOf(look) : null;

        /// <summary>The look every room is held to for the self-test, over what the room gone into or the floor opened has; null to follow them.</summary>
        private RoomLook? _heldLook;

        /// <summary>Holds every room to one look for the self-test to picture each; null lets the room gone into or the floor opened decide again.</summary>
        [Diagnostic]
        public void HoldLook(RoomLook? look) => _heldLook = look;

        /// <summary>The room gone into; none while the whole example or a floor of it is shown.</summary>
        public PlacedRoom Focused { get; private set; }

        /// <summary>Whether the example is cut from the side (<see cref="Stage.CutFromSide"/>), so the rooms above the room gone into are dimmed, not put away.</summary>
        public bool FromSide { get; set; }

        /// <summary>The ground of the room gone into, in the example's own space.</summary>
        public float FocusGround { get; private set; }

        /// <summary>The level of the room gone into it is cut over (<see cref="RoomLevels"/>), its ground on going in.</summary>
        public float FocusLevel { get; private set; }

        /// <summary>A room's levels from the lowest up, its ground among them (<see cref="RoomLevels.Of"/>).</summary>
        public List<float> LevelsOf(PlacedRoom room) => RoomLevels.Of(_roomGround.TryGetValue(room, out var patch) ? patch : null, GroundOf(room));

        /// <summary>Opens a level of the room gone into, which the cut and the camera follow; the rooms around keep their looks by its ground.</summary>
        public void OpenLevelOfFocused(float level) => FocusLevel = level;

        /// <summary>Goes into a room: it stands whole and the rest as <see cref="RoomFocus.Look"/> has them.</summary>
        public void Focus(PlacedRoom room)
        {
            Focused = room;
            FocusGround = GroundOf(room);
            FocusLevel = FocusGround;
            _focusLooks.Clear();
            _focusRead = -1;
            _focusAcross = null;
        }

        /// <summary>Comes out of the room gone into.</summary>
        public void Unfocus()
        {
            Focused = null;
            _focusLooks.Clear();
            _focusRead = -1;
            _focusAcross = null;
        }

        /// <summary>A room's ground in the example's own space (<see cref="RoomFocus.Ground"/>): its main floor where its rays were read and found one to stand about on, else its lowest doorway.</summary>
        public float GroundOf(PlacedRoom room)
        {
            var read = _roomGround.TryGetValue(room, out var patch);
            var main = read ? FloorFinder.MainFloor(patch) : null;
            var doorways = new List<float>();
            for (var i = 0; i < room.Room.Doorways.Count; i++) doorways.Add(room.DoorwayAt(i).Y);
            return RoomFocus.Ground(main, read ? FloorFinder.MainFloorRoom(patch) : 0f, doorways, room.Position.Y - room.Room.Size.Y / 2f);
        }

        /// <summary>
        /// The cut over a room gone into that has no real floor and climbs (<see cref="SlopeCut"/>),
        /// in the example's own space; none for a room cut over its ground, or one not yet read.
        /// </summary>
        public RoomCutPlane? SlopeOf(PlacedRoom room)
        {
            if (!_roomGround.TryGetValue(room, out var patch)) return null;
            var doorways = new List<Vec3>();
            for (var i = 0; i < room.Room.Doorways.Count; i++) doorways.Add(room.DoorwayAt(i));
            return SlopeCut.Of(doorways, RoomFocus.RealFloor(FloorFinder.MainFloor(patch), FloorFinder.MainFloorRoom(patch)));
        }

        /// <summary>The ceiling over a level of a room, from its own rays; none where they found none or it was not read.</summary>
        public float? CeilingOver(PlacedRoom room, float level) => _roomGround.TryGetValue(room, out var patch) ? FloorFinder.CeilingOver(new[] { patch }, level) : null;

        /// <summary>How a room shows: as held for the self-test, else as the room gone into has it, else as the floor opened has it (<see cref="RoomShown"/>).</summary>
        public RoomLook LookOf(PlacedRoom room, float? planFloor, IReadOnlyList<float> floors)
        {
            if (_heldLook is RoomLook held) return held;
            return RoomFocus.WithBelow(OwnLookOf(room, planFloor, floors), BelowShown);
        }

        /// <summary>Whether the rooms below the floor opened or the room gone into stand dimmed; else they are put away (<see cref="RoomFocus.WithBelow"/>).</summary>
        public bool BelowShown { get; set; } = true;

        /// <summary>How a room shows before the switch for the rooms below: as the room gone into has it, else as the floor opened has it.</summary>
        private RoomLook OwnLookOf(PlacedRoom room, float? planFloor, IReadOnlyList<float> floors)
        {
            room = ExamplePlan.LooksAs(room);
            if (Focused != null)
            {
                KnowFocusLooks();
                var look = _focusLooks.TryGetValue(room, out var known) ? known : RoomLook.Whole;
                // Cut from the side, the rooms above stand in the section too, dimmed.
                return FromSide && look == RoomLook.Away ? RoomLook.Below : look;
            }
            switch (RoomShown(room, planFloor, floors))
            {
                case PlanRoomShown.Whole: return RoomLook.Whole;
                case PlanRoomShown.None: return RoomLook.Away;
                default: return RoomLook.Below;
            }
        }

        /// <summary>Works out again how each room shows with the room gone into, as more rooms are read.</summary>
        private void KnowFocusLooks()
        {
            if (_focusRead == _roomGround.Count || Placed == null) return;
            _focusRead = _roomGround.Count;
            _focusLooks.Clear();
            foreach (var room in Placed.Rooms) _focusLooks[room] = RoomFocus.Look(room == Focused, GroundOf(room), FocusGround);
        }

        /// <summary>The middle across of the room gone into, on the stage, and how far it reaches from it (x, z and reach); null with none.</summary>
        public Vector3? FocusAcross()
        {
            if (Focused == null || Holder == null) return null;
            if (_focusAcross != null) return _focusAcross;
            var holder = Holder.transform;
            var corners = new List<Vec3>();
            foreach (var corner in Focused.Corners())
            {
                var at = holder.TransformPoint(new Vector3(corner.X, corner.Y, corner.Z));
                corners.Add(new Vec3(at.x, at.y, at.z));
            }
            _focusAcross = StageCamera.Across(corners) is (float x, float z, float reach) ? new Vector3(x, z, reach) : (Vector3?)null;
            return _focusAcross;
        }

        /// <summary>Whether a room's copy stands and the share of its colours it keeps as dimmed (none where it is not), for the self-test; null for a room with no copy yet.</summary>
        [Diagnostic]
        public (bool Standing, float? Keep)? Drawn(PlacedRoom room)
        {
            foreach (var pair in _rooms)
            {
                if (pair.Key == room && pair.Value != null) return (pair.Value.activeSelf, DimShareOf(pair.Value));
            }
            return null;
        }

        /// <summary>The rooms' copies standing, for the self-test to tell what they are drawn with.</summary>
        [Diagnostic]
        public List<GameObject> RoomCopies() => _rooms.Select(pair => pair.Value).Where(copy => copy != null).ToList();

        /// <summary>The lights switched on in the rooms dimmed below the floor opened.</summary>
        public List<Light> DimmedLights()
        {
            var lights = new List<Light>();
            foreach (var room in _dimmed.Keys) if (room != null) lights.AddRange(room.GetComponentsInChildren<Light>());
            lights.RemoveAll(l => !l.enabled);
            return lights;
        }

        /// <summary>Shows a dungeon's example inside, its location's own parts put away, or its entrance outside.</summary>
        public void ShowInside(bool inside)
        {
            foreach (var part in _outside) if (part != null) part.SetActive(!inside);
            Holder.SetActive(inside);
        }

        /// <summary>
        /// Inside a dungeon's example, each room stands as it is, dimmed (<see cref="Dim"/>), half
        /// on the level of the room gone into, or put away (<see cref="LookOf"/>): as the room gone
        /// into has it, else as the floor opened has it; with neither every room stands as it is.
        /// A few each frame, as a big example changes many at once.
        /// </summary>
        public void KeepToFloor(float? planFloor, IReadOnlyList<float> floors)
        {
            if (!IsDungeon || Holder == null || _rooms.Count == 0) return;
            KeepDimmedLights();
            var watch = Stopwatch.StartNew();
            foreach (var pair in _rooms)
            {
                var copy = pair.Value;
                if (copy == null) continue;
                var look = LookOf(pair.Key, planFloor, floors);
                var standing = look != RoomLook.Away;
                var dimAs = Dimmed(look);
                var was = _dimmed.TryGetValue(copy, out var dimmedAs) ? dimmedAs : (RoomLook?)null;
                if (copy.activeSelf == standing && was == dimAs) continue;
                if (copy.activeSelf != standing) copy.SetActive(standing);
                if (was != dimAs)
                {
                    Dim.Set(copy, dimAs is RoomLook dim ? Dim.ShareOf(dim) : (float?)null);
                    if (dimAs is RoomLook now)
                    {
                        _dimmed[copy] = now;
                        if (!_lightsKept) Unlight(copy);
                    }
                    else
                    {
                        _dimmed.Remove(copy);
                        Relight(copy);
                    }
                }
                if (watch.Elapsed.TotalMilliseconds >= KeepBudgetMs) break;
            }
        }

        /// <summary>How a room is dimmed in a look: half on the level of the room gone into, more below; not at all whole or put away.</summary>
        private static RoomLook? Dimmed(RoomLook look) => look == RoomLook.Near || look == RoomLook.Below ? look : (RoomLook?)null;

        /// <summary>
        /// The lights each dimmed room had switched on, switched off while it is dimmed: a big
        /// dungeon's rooms below the floor opened hold more than a hundred lights, and the stage
        /// draws a part again for each light on it, so they took about half of each film. The
        /// rooms keep them where the setting says so (<see cref="Settings.LightDimmedRooms"/>).
        /// </summary>
        private readonly Dictionary<GameObject, List<Light>> _unlit = new Dictionary<GameObject, List<Light>>();

        /// <summary>Whether the dimmed rooms keep their lights, as the setting was when they were last dimmed or lit.</summary>
        private bool _lightsKept;

        /// <summary>Follows the setting: as it changes, the dimmed rooms' lights go off or on again.</summary>
        private void KeepDimmedLights()
        {
            var kept = Settings.LightDimmedRooms;
            if (kept == _lightsKept) return;
            _lightsKept = kept;
            foreach (var room in _dimmed.Keys)
            {
                if (room == null) continue;
                if (kept) Relight(room);
                else Unlight(room);
            }
        }

        private void Unlight(GameObject room)
        {
            if (_unlit.ContainsKey(room)) return;
            var lights = new List<Light>();
            foreach (var light in room.GetComponentsInChildren<Light>())
            {
                if (!light.enabled) continue;
                light.enabled = false;
                lights.Add(light);
            }
            if (lights.Count > 0) _unlit[room] = lights;
        }

        private void Relight(GameObject room)
        {
            if (!_unlit.TryGetValue(room, out var lights)) return;
            foreach (var light in lights) if (light != null) light.enabled = true;
            _unlit.Remove(room);
        }

        /// <summary>How many of the rooms are dimmed, for the self-test.</summary>
        public int DimmedCount => _dimmed.Count;

        /// <summary>Whether every room stands, is dimmed as it should be or is put away as <see cref="LookOf"/> has it, for the self-test.</summary>
        public bool Kept(float? planFloor, IReadOnlyList<float> floors)
        {
            foreach (var pair in _rooms)
            {
                if (pair.Value == null) continue;
                var look = LookOf(pair.Key, planFloor, floors);
                var was = _dimmed.TryGetValue(pair.Value, out var dimmedAs) ? dimmedAs : (RoomLook?)null;
                if (pair.Value.activeSelf != (look != RoomLook.Away) || was != Dimmed(look)) return false;
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

        /// <summary>How many rooms stand on a floor by the plan's rule (<see cref="RoomShown"/>), end caps and dividers left out; worked out once a frame for one floor.</summary>
        public int OnFloorCount(float floor, IReadOnlyList<float> floors)
        {
            if (_countedAt == Time.frameCount && _countedFloor == floor) return _countedRooms;
            var count = 0;
            foreach (var room in Placed.Rooms)
            {
                if (!room.Room.EndCap && !room.Room.Divider && RoomShown(room, floor, floors) == PlanRoomShown.Whole) count++;
            }
            _countedAt = Time.frameCount;
            _countedFloor = floor;
            _countedRooms = count;
            return count;
        }

        /// <summary>Whether a floor reaches a room: its box says the room stands on it (<see cref="ExamplePlan.Shown"/>), or its own rays found any ground there (<see cref="FloorFinder.Holds"/>), as its meshes can reach past its box; a room no floor reaches gets one of its own.</summary>
        public bool Reaches(PlacedRoom room, float floor) =>
            ExamplePlan.Shown(room, floor) == PlanRoomShown.Whole || _roomGround.TryGetValue(room, out var ground) && FloorFinder.Holds(ground, floor);

        /// <summary>
        /// Whether a room has a floor of its own at a height, as its rays found it
        /// (<see cref="FloorFinder.StandsOn"/>), not merely its box reaching there, as a tall hall
        /// reaches up through the floors over the one it stands on; a room not read yet by its box.
        /// What the plan tiles as a room of a floor.
        /// </summary>
        public bool HasFloorAt(PlacedRoom room, float floor) =>
            _roomGround.TryGetValue(room, out var ground) ? FloorFinder.StandsOn(ground, floor) : ExamplePlan.Shown(room, floor) == PlanRoomShown.Whole;

        /// <summary>How many of the example's rooms have had their ground read.</summary>
        public int GroundRead => _roomGround.Count;

        /// <summary>
        /// How a room shows with a floor of <paramref name="floors"/> opened, by the plan's rule
        /// (<see cref="ExamplePlan.OnStage"/>): whole on a floor of its own there (<see cref="HasFloorAt"/>),
        /// or with none of its own at any floor where its box reaches it; else dimmed, or put
        /// away above the floor; whole with none opened.
        /// </summary>
        public PlanRoomShown RoomShown(PlacedRoom room, float? planFloor, IReadOnlyList<float> floors)
        {
            if (planFloor == null) return PlanRoomShown.Whole;
            KnowOwnFloors(floors);
            return ExamplePlan.OnStage(room, planFloor, _ownFloor.Contains(room), HasFloor);
        }

        private System.Func<PlacedRoom, float, bool> _hasFloor;
        private System.Func<PlacedRoom, float, bool> HasFloor => _hasFloor ?? (_hasFloor = HasFloorAt);

        /// <summary>Works out again which rooms have a floor of their own at one of the floors, as the floors change or more rooms are read.</summary>
        private void KnowOwnFloors(IReadOnlyList<float> floors)
        {
            var same = _ownFloorRead == _roomGround.Count && _ownFloorOf.Count == floors.Count;
            for (var i = 0; same && i < floors.Count; i++) same = _ownFloorOf[i] == floors[i];
            if (same) return;
            _ownFloor.Clear();
            _ownFloorOf.Clear();
            for (var i = 0; i < floors.Count; i++) _ownFloorOf.Add(floors[i]);
            _ownFloorRead = _roomGround.Count;
            if (Placed == null) return;
            foreach (var room in Placed.Rooms)
            {
                for (var i = 0; i < floors.Count; i++)
                {
                    if (!HasFloorAt(room, floors[i])) continue;
                    _ownFloor.Add(room);
                    break;
                }
            }
        }

        /// <summary>
        /// The middle across of the rooms standing on the floor opened, on the stage, and how far
        /// they reach from it (x, z and reach); null for none. Worked out again as the floor or
        /// the rooms change.
        /// </summary>
        public Vector3? FloorAcross(float? planFloor, IReadOnlyList<float> floors)
        {
            if (planFloor == null) return null;
            if (_acrossFloor == planFloor && _acrossRooms == Copies && ReferenceEquals(_acrossOf, Placed)) return _across;

            var holder = Holder.transform;
            var corners = new List<Vec3>();
            foreach (var room in Placed.Rooms)
            {
                if (RoomShown(room, planFloor, floors) != PlanRoomShown.Whole) continue;
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
                var alone = FloorFinder.RoomFloors(new[] { ground });
                told.Add($"{room.Room.Name} at {Numbers.Fixed(room.Position.Y, 1)} m: {(alone.Count > 0 ? string.Join(", ", alone.Select(f => Numbers.Fixed(f, 1))) : "none")}");
            }
            return told;
        }

        /// <summary>
        /// Each room read so far as the floors are found from it: the height its box stands over,
        /// its doorways' heights, the ground its own rays found (each height with its square
        /// metres, ! where that is a level of the room's own, ~ where no ray landed in the band
        /// itself), and the floors it stands on by the plan's rule, by its box where it has no
        /// floor of its own, for the self-test.
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
                var on = floors.Where(f => RoomShown(room, f, floors) == PlanRoomShown.Whole).Select(f => Numbers.Fixed(f, 1) + (_ownFloor.Contains(room) ? "" : " by box"));
                var touched = floors.Where(f => Reaches(room, f) && RoomShown(room, f, floors) != PlanRoomShown.Whole).Select(f => Numbers.Fixed(f, 1));
                told.Add(line + $", on {string.Join(" ", on)}" + (touched.Any() ? $", touching {string.Join(" ", touched)}" : ""));
            }
            return told;
        }

        /// <summary>
        /// Reads a dungeon room's floors, once, as it stands, from what was kept of its kind where
        /// that tells its roll (<see cref="FloorProbe"/>). Probed while the entrance is shown, the
        /// example sleeps, and the room alone is woken for it beside the example: a dungeon's
        /// example stands where its location does, so the room stands the same in either.
        /// </summary>
        public void ReadFloors(GameObject copy, PlacedRoom room, PlaceRoll roll, Transform subject, int layer)
        {
            var read = Timing.Start();
            var asleep = !Holder.activeSelf;
            // Read a frame after it was made, a room the floor opened has put away meanwhile is
            // switched on for its rays, which meet only what is switched on, and off again after.
            var away = !copy.activeSelf;
            void Wake()
            {
                var woken = Timing.Start();
                if (asleep) copy.transform.SetParent(subject, false);
                if (away) copy.SetActive(true);
                if (!copy.activeInHierarchy) ReadSwitchedOff++;
                Timing.Add("example floors wake", woken);
            }
            void Sleep()
            {
                var slept = Timing.Start();
                if (away) copy.SetActive(false);
                if (asleep) copy.transform.SetParent(Holder.transform, false);
                Timing.Add("example floors sleep", slept);
            }
            _hits.Clear();
            // Walked into by its doorways, in its own frame as its rays were kept.
            var start = room.Room.Doorways.Count > 0 ? FloorWalk.Start.At(room.Room.Doorways.Select(d => d.Position)) : null;
            var cast = FloorProbe.Read(copy, subject, layer, _hits, Copies, room.Room.Name, true, roll, wake: Wake, sleep: Sleep,
                start: start, top: t => Top = float.IsNaN(Top) ? t : Mathf.Max(Top, t));
            // Its doorway out, the entrance's, and its highest, where its ground was measured: by
            // and high over them the entrance's ground is the world's.
            float Measured(Vec3 at) => subject.InverseTransformPoint(Holder.transform.TransformPoint(new Vector3(at.X, at.Y, at.Z))).y;
            var outerDoor = float.PositiveInfinity;
            var highestDoor = float.NegativeInfinity;
            for (var i = 0; i < room.Room.Doorways.Count; i++)
            {
                var height = Measured(room.DoorwayAt(i));
                highestDoor = Mathf.Max(highestDoor, height);
                if (room.Room.Doorways[i].Entrance) outerDoor = height;
            }
            // Read alone, a room whose ceiling is the room above has nothing of its own over its floor.
            var step = Timing.Start();
            var ground = FloorFinder.Patch(FloorFinder.Inside(_hits, outerDoor, highestDoor));
            Timing.Add("example floors found", step);
            ground.Ground = cast;
            _patches.Add(ground);
            _roomGround[room] = ground;
            _hits.Clear();
            Timing.Add("example floors", read);
        }

        /// <summary>The highest the rays met over the rooms read so far, above the location's root; NaN for none.</summary>
        public float Top { get; private set; } = float.NaN;

        /// <summary>The ceiling over a floor of the example, from its rooms' rays (<see cref="FloorFinder.CeilingOver(IEnumerable{FloorPatch}, float)"/>).</summary>
        public float? CeilingOver(float floor) => FloorFinder.CeilingOver(_patches, floor);

        /// <summary>
        /// The example's floors: one at each room's main ground and at each big level of one
        /// (<see cref="FloorFinder.RoomFloors"/>), end caps and dividers left out, with one for
        /// each room no floor reaches, or where their doorways are while none are found.
        /// </summary>
        public List<float> FloorsNow()
        {
            var found = FloorFinder.RoomFloors(_roomGround.Where(pair => !pair.Key.Room.EndCap && !pair.Key.Room.Divider).Select(pair => pair.Value));
            if (found.Count == 0) return PlaceView.ExampleFloors(Placed);
            // A room standing whole on none of them, a sloping corridor with no flat ground, gets
            // a floor where it is walked into, so every room built can be opened.
            return PlaceView.ReachingEveryRoom(found, Placed.Rooms.Take(Next), Reaches);
        }

        /// <summary>Takes down the example's copies and lets go of everything kept of it, as another entry or another example is shown.</summary>
        public void Forget()
        {
            Build?.Cancel();
            Build = null;
            BuildRoll = null;
            BuildPaints = null;
            if (Holder != null) Object.Destroy(Holder);
            Holder = null;
            Of = null;
            Placed = null;
            Ways = null;
            _loot.Clear();
            NothingOutside = false;
            Next = 0;
            Copies = 0;
            _patches.Clear();
            _rooms.Clear();
            HoldsWater = false;
            _dimmed.Clear();
            _unlit.Clear();
            _roomGround.Clear();
            _countedAt = -1;
            _ownFloor.Clear();
            _ownFloorOf.Clear();
            _ownFloorRead = -1;
            _heldLook = null;
            _toRead.Clear();
            Unfocus();
            Top = float.NaN;
            _outside.Clear();
        }
    }
}
