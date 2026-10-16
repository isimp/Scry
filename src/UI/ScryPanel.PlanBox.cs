using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The plan box of a dungeon's example inside, in the stage's lower left corner: its rooms
    /// seen from above, turned with the view and shaded by depth (<see cref="PlanView.Above"/>);
    /// each room across against its depth, turned to any angle (<see cref="PlanMap"/>); or a tree
    /// of its ways, each room at its spot from above, pushed apart where rooms crowd
    /// (<see cref="PinnedLayout"/>). A room under the mouse is named and lit on the stage, and a
    /// click goes into it; the way from the entrance to the room gone into is an amber line and a
    /// trail of names under the plan, each going into its room. It grows and names its rooms, the
    /// stage framed beside it; which view and whether grown are remembered with the panel.
    /// </summary>
    internal static partial class ScryPanel
    {
        private static PlanView _planView = PlanView.Above;
        private static bool _planBig;

        /// <summary>The map's angle, and whether it turns with the stage instead.</summary>
        private static float _mapAngle;
        private static bool _mapFollow;

        /// <summary>The room under the mouse in the box, lit on the stage; none.</summary>
        private static PlacedRoom _planLit;

        /// <summary>A room as the box last drew it: its middle, half its size each way, how it is turned, and whether it stands only as dashes, above the room gone into.</summary>
        private struct BoxSpot
        {
            public PlacedRoom Room;
            public Vector2 Centre;
            public Vector2 Half;
            public float Angle;
            public bool Dashed;

            public bool Contains(Vector2 point)
            {
                var d = point - Centre;
                var a = -Angle * Mathf.Deg2Rad;
                var x = d.x * Mathf.Cos(a) - d.y * Mathf.Sin(a);
                var y = d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a);
                return Mathf.Abs(x) <= Half.x && Mathf.Abs(y) <= Half.y;
            }
        }

        /// <summary>The rooms as last laid out, in the order drawn, the topmost last.</summary>
        private static readonly List<BoxSpot> BoxSpots = new List<BoxSpot>();

        /// <summary>The segments in the header, from the right.</summary>
        private static readonly PlanView[] ViewsRightToLeft = { PlanView.Tree, PlanView.Map, PlanView.Above };

        /// <summary>What each drawing of the box works with, kept between drawings rather than made each time.</summary>
        private static readonly List<Rect> WrittenNames = new List<Rect>();
        private static readonly List<(float Min, float Max)> MapAcross = new List<(float, float)>();
        private static readonly List<int> MapOrder = new List<int>();
        private static readonly Dictionary<PlacedRoom, Vector2> TreeAt = new Dictionary<PlacedRoom, Vector2>();

        /// <summary>Each room's place among the rooms, lowest first, to draw each way once.</summary>
        private static readonly Dictionary<PlacedRoom, int> BoxIndex = new Dictionary<PlacedRoom, int>();

        /// <summary>The example's rooms and their grounds, worked out again as the example or what is read of it changes.</summary>
        private static DungeonWays _boxWays;
        private static int _boxRead = -1, _boxShown = -1;
        private static readonly List<PlacedRoom> BoxRooms = new List<PlacedRoom>();
        private static readonly Dictionary<PlacedRoom, float> BoxGround = new Dictionary<PlacedRoom, float>();
        private static float _boxHighest, _boxDeepest, _boxWidest;

        /// <summary>The tree as last laid out, kept until the rooms, the view's turn or the box's size change.</summary>
        private static readonly List<(float X, float Y)> TreeHomes = new List<(float, float)>();
        private static List<(float X, float Y)> _treeLaid = new List<(float, float)>();
        private static Rect _treeArea;
        private static float _treeYaw = float.NaN;
        private static int _treeRead = -1;
        private static DungeonWays _treeWays;

        /// <summary>The map's overlaps at the angle they were last counted at.</summary>
        private static int _mapOverlaps;
        private static float _overlapsAt = float.NaN;
        private static DungeonWays _overlapsOf;

        /// <summary>Where the box last stood, for the self-test; none while it is not drawn.</summary>
        [Diagnostic]
        public static Rect PlanBoxRect { get; private set; }

        [Diagnostic]
        public static PlanView PlanBoxView => _planView;

        [Diagnostic]
        public static bool PlanBoxBig => _planBig;

        /// <summary>The map's angle shown, for the self-test.</summary>
        [Diagnostic]
        public static float MapAngleShown => _mapFollow ? PlanMap.Following(Stage.ExampleViewYaw) : _mapAngle;

        /// <summary>How many rooms cover each other on the map at the angle shown, for the self-test.</summary>
        [Diagnostic]
        public static int MapOverlapsShown => _mapOverlaps;

        /// <summary>Shows a view of the box as its segment does, for the self-test.</summary>
        [Diagnostic]
        public static void ShowPlanViewForTest(PlanView view) => ChoosePlanView(view);

        /// <summary>Grows or shrinks the box as its corner button does, for the self-test.</summary>
        [Diagnostic]
        public static void GrowPlanForTest(bool big) => GrowPlan(big);

        /// <summary>Turns the map as dragging it across by so many pixels does, for the self-test.</summary>
        [Diagnostic]
        public static void DragMapForTest(float pixels) => TurnMap(pixels);

        /// <summary>Turns the map to an angle, for the self-test.</summary>
        [Diagnostic]
        public static void SetMapAngleForTest(float angle)
        {
            _mapFollow = false;
            _mapAngle = PlanMap.Normal(angle);
        }

        /// <summary>Presses Least overlap, for the self-test.</summary>
        [Diagnostic]
        public static void LeastOverlapForTest() => LeastOverlap();

        /// <summary>Each room's middle as the box last drew it, for the self-test to point at.</summary>
        [Diagnostic]
        public static List<(PlacedRoom Room, Vector2 At)> PlanBoxSpotsForTest()
        {
            var spots = new List<(PlacedRoom, Vector2)>();
            foreach (var spot in BoxSpots) spots.Add((spot.Room, spot.Centre));
            return spots;
        }

        /// <summary>The room the box finds under a point, as the mouse does, for the self-test.</summary>
        [Diagnostic]
        public static PlacedRoom PlanBoxRoomAtForTest(Vector2 point) => BoxRoomAt(point);

        /// <summary>Lights a room as the mouse over it in the box does, kept lit until put out with none, for the self-test.</summary>
        [Diagnostic]
        public static void LightPlanRoomForTest(PlacedRoom room) => _planLitForTest = room;

        /// <summary>Clicks a room in the box as the mouse does, for the self-test.</summary>
        [Diagnostic]
        public static void ClickPlanRoomForTest(PlacedRoom room) => Stage.GoInto(room);

        /// <summary>What the panel's file remembers now, for the self-test.</summary>
        [Diagnostic]
        public static PanelPlace RememberedForTest() => File.Exists(RectFile) ? PanelPlace.Read(File.ReadAllLines(RectFile)) : new PanelPlace();

        /// <summary>The room the self-test lights as the mouse over the box would; none.</summary>
        private static PlacedRoom _planLitForTest;

        /// <summary>Takes the view and size the box was left in, as remembered with the panel.</summary>
        private static void TakePlanBoxPlace(PanelPlace place)
        {
            if (place.PlanView.HasValue) _planView = place.PlanView.Value;
            if (place.PlanBig.HasValue) _planBig = place.PlanBig.Value;
        }

        private static void ChoosePlanView(PlanView view)
        {
            if (view == _planView) return;
            _planView = view;
            SaveRects();
        }

        private static void GrowPlan(bool big)
        {
            if (big == _planBig) return;
            _planBig = big;
            SaveRects();
        }

        private static void TurnMap(float pixels)
        {
            if (_mapFollow) _mapAngle = PlanMap.Following(Stage.ExampleViewYaw);
            _mapFollow = false;
            _mapAngle = PlanMap.Turned(_mapAngle, pixels);
        }

        private static void LeastOverlap()
        {
            KnowBoxRooms();
            _mapAngle = PlanMap.LeastOverlap(BoxRooms, MapAngleShown);
            _mapFollow = false;
        }

        /// <summary>The rooms of the example and their grounds, again only as the example or what is read of it changes.</summary>
        private static bool KnowBoxRooms()
        {
            var ways = Stage.ExampleWays;
            if (ways == null) return false;
            if (ReferenceEquals(ways, _boxWays) && _boxRead == Stage.ExampleRoomsRead && _boxShown == Stage.ExampleRoomsShown) return true;
            _boxWays = ways;
            _boxRead = Stage.ExampleRoomsRead;
            _boxShown = Stage.ExampleRoomsShown;
            BoxRooms.Clear();
            BoxRooms.AddRange(ways.Rooms);
            BoxGround.Clear();
            _boxHighest = float.NegativeInfinity;
            _boxDeepest = float.PositiveInfinity;
            foreach (var room in BoxRooms)
            {
                var ground = Stage.ExampleRoomGround(room);
                BoxGround[room] = ground;
                _boxHighest = Mathf.Max(_boxHighest, ground);
                _boxDeepest = Mathf.Min(_boxDeepest, ground);
            }
            // Lowest first, so the higher rooms are drawn over them.
            BoxRooms.Sort((a, b) => BoxGround[a].CompareTo(BoxGround[b]));
            BoxIndex.Clear();
            for (var i = 0; i < BoxRooms.Count; i++) BoxIndex[BoxRooms[i]] = i;
            _boxWidest = Mathf.Max(1f, ExamplePlan.Widest(BoxRooms));
            _treeRead = -1;
            _overlapsAt = float.NaN;
            return true;
        }

        private static float GroundOf(PlacedRoom room) => BoxGround.TryGetValue(room, out var ground) ? ground : 0f;

        /// <summary>A room's tone by its depth: the deepest darkest, the highest lightest; the entrance in the accent.</summary>
        private static Color DepthTone(PlacedRoom room)
        {
            if (room.Room.Entrance) return Skin.OverStage(Skin.Accent, 0.4f);
            var span = Mathf.Max(1f, _boxHighest - _boxDeepest);
            var high = Mathf.Clamp01((GroundOf(room) - _boxDeepest) / span);
            return Skin.OverStage(Skin.KindColor(Kind.Location), 0.18f + 0.42f * high);
        }

        /// <summary>
        /// The box of a dungeon's example inside, folded to a tab or drawn whole; returns where it
        /// stands, which takes the mouse.
        /// </summary>
        private static Rect DungeonPlanBox(Explorer explorer, Rect inner)
        {
            var e = Event.current;
            var repaint = e.type == EventType.Repaint;
            if (repaint) _planLit = _planLitForTest;
            if (!KnowBoxRooms() || BoxRooms.Count == 0) return PlanBoxRect = Rect.zero;

            // Folded, a small tab where it stands, which brings it back.
            if (PlanFolded)
            {
                Stage.ViewShift = 0f;
                var tabW = Skin.Width(Skin.Chip, "Plan") + U(4f);
                var tab = new Rect(inner.x + U(8f), inner.yMax - U(34f) - U(22f), tabW, U(22f));
                if (tab.Contains(e.mousePosition)) AskTip("plan:tab", "Shows the example's plan");
                CountDrawn(PanelPart.PlanTab);
                if (GUI.Button(tab, "Plan", Skin.Fitted(Skin.Chip, tab))) PlanFolded = false;
                return PlanBoxRect = tab;
            }

            var pad = U(6f);
            var rowH = U(22f);
            var gap = U(4f);
            var width = Mathf.Min(U(_planBig ? 400f : 236f), inner.width - U(16f));
            var planH = U(_planBig ? 330f : 150f);
            var mapBar = _planView == PlanView.Map;
            var rows = pad + rowH + gap + gap + rowH + gap + rowH + (mapBar ? rowH + gap : 0f) + pad;
            planH = Mathf.Max(U(60f), Mathf.Min(planH, inner.height - U(48f) - rows));
            var height = rows + planH;
            var box = new Rect(inner.x + U(8f), inner.yMax - U(34f) - height, width, height);
            PlanBoxRect = box;
            if (repaint) Stage.ViewShift = _planBig ? (box.xMax - inner.x) / (2f * Mathf.Max(1f, inner.width)) : 0f;

            CountDrawn(PanelPart.Plan);
            CountDrawn(PanelPart.PlanBox);
            Skin.Box(box, Skin.Alpha(Skin.Stage, 0.9f), Skin.Outline);
            var x = box.x + pad;
            var w = box.width - pad * 2f;
            var y = box.y + pad;

            // The header: the caption, the three views, grow and fold.
            var fold = new Rect(box.xMax - pad - U(16f), y + U(3f), U(16f), U(16f));
            var grow = new Rect(fold.x - U(4f) - rowH, y, rowH, rowH);
            var segX = grow.x - U(6f);
            foreach (var view in ViewsRightToLeft)
            {
                var name = PlanBoxWords.ViewName(view);
                var on = view == _planView;
                var style = on ? Skin.SegmentOn : Skin.Segment;
                var segW = Skin.Width(Skin.SegmentOn, name) + U(10f);
                segX -= segW;
                var segment = new Rect(segX, y, segW, rowH);
                segX -= U(2f);
                if (segment.Contains(e.mousePosition)) AskTip("plan-view:" + name, PlanBoxWords.ViewTip(view));
                if (GUI.Button(segment, name, Skin.Fitted(style, segment)) && !on) ChoosePlanView(view);
            }
            if (grow.Contains(e.mousePosition)) AskTip("plan:grow", PlanBoxWords.GrowTip(_planBig));
            if (GUI.Button(grow, GUIContent.none, _planBig ? Skin.On : Skin.IconButton)) GrowPlan(!_planBig);
            if (repaint) Skin.Icon(new Rect(grow.x + U(4f), grow.y + U(4f), grow.width - U(8f), grow.height - U(8f)), Skin.Resize, _planBig ? Skin.Accent : Skin.Dim);
            if (fold.Contains(e.mousePosition))
            {
                AskTip("plan:fold", "Puts the plan away");
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    PlanFolded = true;
                    e.Use();
                    return box;
                }
            }
            if (repaint) GUI.Label(fold, "–", fold.Contains(e.mousePosition) ? Skin.Label : Skin.DimLabel);

            // The plan itself.
            var area = new Rect(x, y + rowH + gap, w, planH);
            BoxSpots.Clear();
            var gone = Stage.RoomGoneInto;
            var route = gone != null && _boxWays != null ? _boxWays.Route(gone) : null;
            switch (_planView)
            {
                case PlanView.Map:
                    MapView(explorer, area, gone, route);
                    break;
                case PlanView.Tree:
                    TreeView(explorer, area, gone, route);
                    break;
                default:
                    AboveView(explorer, area, gone, route);
                    break;
            }

            // The room under the mouse: named in the caption, lit on the stage, gone into on a click.
            var hovered = area.Contains(e.mousePosition) && (_drag == Drag.None || _drag == Drag.Orbit) ? BoxRoomAt(e.mousePosition) : null;
            if (hovered != null)
            {
                _planLit = hovered;
                AskTip("planbox:" + hovered.Room.Name, StageWords.GoIntoTip(BoxRoomName(explorer, hovered), hovered == gone));
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    Stage.GoInto(hovered);
                    e.Use();
                }
            }
            else if (_planView == PlanView.Map && area.Contains(e.mousePosition) && e.type == EventType.MouseDown && e.button == 0)
            {
                // A drag across the map, not on a room, turns it.
                StartDrag(Drag.MapTurn);
                e.Use();
            }
            var caption = hovered != null ? BoxRoomName(explorer, hovered) : PlanBoxWords.Caption(BoxRooms.Count);
            FitLabel(new Rect(x, y, Mathf.Max(U(20f), segX - x - U(4f)), rowH), caption, Skin.FaintLabel, 10f);
            y = area.yMax + gap;

            // The map's own controls: the angle with the fewest rooms over each other, following the view, and how many overlap now.
            if (mapBar)
            {
                var least = new Rect(x, y, Skin.Width(Skin.Segment, PlanBoxWords.LeastOverlap) + U(10f), rowH);
                var follow = new Rect(least.xMax + U(4f), y, Skin.Width(Skin.SegmentOn, PlanBoxWords.Follow) + U(10f), rowH);
                if (least.Contains(e.mousePosition)) AskTip("plan:least", PlanBoxWords.LeastOverlapTip);
                if (follow.Contains(e.mousePosition)) AskTip("plan:follow", PlanBoxWords.FollowTip);
                if (GUI.Button(least, PlanBoxWords.LeastOverlap, Skin.Fitted(Skin.Segment, least))) LeastOverlap();
                if (GUI.Button(follow, PlanBoxWords.Follow, Skin.Fitted(_mapFollow ? Skin.SegmentOn : Skin.Segment, follow))) _mapFollow = !_mapFollow;
                var counted = PlanBoxWords.Overlapping(_mapOverlaps);
                var countW = Skin.Width(Skin.FaintLabel, counted);
                if (follow.xMax + U(6f) + countW <= x + w) GUI.Label(new Rect(x + w - countW, y, countW, rowH), counted, Skin.FaintLabel);
                y += rowH + gap;
            }

            // The way from the entrance to the room gone into, each name going into its room.
            Trail(explorer, new Rect(x, y, w, rowH), route);
            y += rowH + gap;

            // The entrance and the deepest room, a click away.
            var entrance = new Rect(x, y, Skin.Width(Skin.Segment, PlanBoxWords.Entrance) + U(12f), rowH);
            var deepest = new Rect(entrance.xMax + U(4f), y, Skin.Width(Skin.Segment, PlanBoxWords.Deepest) + U(12f), rowH);
            if (entrance.Contains(e.mousePosition)) AskTip("plan:entrance", PlanBoxWords.EntranceTip);
            if (deepest.Contains(e.mousePosition)) AskTip("plan:deepest", PlanBoxWords.DeepestTip);
            if (GUI.Button(entrance, PlanBoxWords.Entrance, Skin.Fitted(Skin.Segment, entrance)) && _boxWays?.Entrance != null) Stage.GoInto(_boxWays.Entrance);
            if (GUI.Button(deepest, PlanBoxWords.Deepest, Skin.Fitted(Skin.Segment, deepest)) && _boxWays?.Deepest != null) Stage.GoInto(_boxWays.Deepest);

            if (repaint && Stage.RoomGoneInto == null) Stage.LitRoom = _cardLit ?? _planLit;

            // The box is not the stage: a click or the wheel on it neither turns nor zooms it.
            if (box.Contains(e.mousePosition) && (e.type == EventType.MouseDown || e.type == EventType.ScrollWheel)) e.Use();
            return box;
        }

        private static string BoxRoomName(Explorer explorer, PlacedRoom room) =>
            ShownName(explorer, EntryKeys.For(Kind.Location, room.Room.Name), LocationWords.RoomName(room.Room.Name));

        /// <summary>The room under a point of the box: the topmost drawn whole there, else the topmost drawn in dashes.</summary>
        private static PlacedRoom BoxRoomAt(Vector2 point)
        {
            PlacedRoom dashed = null;
            for (var i = BoxSpots.Count - 1; i >= 0; i--)
            {
                var spot = BoxSpots[i];
                if (!spot.Contains(point)) continue;
                if (!spot.Dashed) return spot.Room;
                if (dashed == null) dashed = spot.Room;
            }
            return dashed;
        }

        /// <summary>Whether a room is lit in the box: under the mouse there, a way chip of the card is hovered for it, or rooms of its name are under the mouse on the stage.</summary>
        private static bool BoxLit(PlacedRoom room) =>
            room == _planLit || room == _cardLit || _cardLit == null && _planLit == null && _stageRoom != null && _stageRoom == room.Room.Name;

        // ----- Above -----

        /// <summary>
        /// Seen from above, turned as the view is and at one scale whichever way, each room a
        /// tile shaded by its depth, the room gone into in the accent and those above it in
        /// dashes, so the rooms under them show.
        /// </summary>
        private static void AboveView(Explorer explorer, Rect area, PlacedRoom gone, List<PlacedRoom> route)
        {
            var repaint = Event.current.type == EventType.Repaint;
            var yaw = Stage.ExampleViewYaw;
            var inset = U(6f);
            var scale = (Mathf.Min(area.width, area.height) - inset * 2f) / _boxWidest;
            var extent = ExamplePlan.Extent(BoxRooms, yaw);
            var midRight = (extent.MinRight + extent.MaxRight) / 2f;
            var midUp = (extent.MinUp + extent.MaxUp) / 2f;
            Vector2 At(Vec3 p)
            {
                var (right, up) = ExamplePlan.Turn(p.X, p.Z, yaw);
                return new Vector2(area.center.x + (right - midRight) * scale, area.center.y - (up - midUp) * scale);
            }
            var goneGround = gone != null ? GroundOf(gone) : 0f;
            foreach (var room in BoxRooms)
            {
                var above = gone != null && room != gone && RoomFocus.Look(false, GroundOf(room), goneGround) == RoomLook.Away;
                BoxSpots.Add(new BoxSpot
                {
                    Room = room, Centre = At(room.Position), Angle = room.Rotation.YawDegrees - yaw, Dashed = above,
                    Half = new Vector2(Mathf.Max(U(1f), room.Room.Size.X * scale / 2f), Mathf.Max(U(1f), room.Room.Size.Z * scale / 2f)),
                });
            }
            if (!repaint) return;
            foreach (var spot in BoxSpots) if (!spot.Dashed) BoxTile(spot, spot.Room == gone ? Skin.Alpha(Skin.Accent, 0.75f) : DepthTone(spot.Room), Skin.Alpha(Skin.Text, 0.3f), 1f);
            foreach (var spot in BoxSpots) if (spot.Dashed) BoxTile(spot, null, Skin.Alpha(Skin.Faint, 0.6f), 1f, dashed: true);
            foreach (var spot in BoxSpots) if (BoxLit(spot.Room) || spot.Room == gone) BoxTile(spot, null, Skin.Accent, 2f);
            RouteLine(route, room => At(room.Position));
            if (_planBig) Names(explorer, area);
        }

        /// <summary>A room's tile, turned as it is: filled, outlined, or outlined in dashes.</summary>
        private static void BoxTile(BoxSpot spot, Color? fill, Color outline, float thickness, bool dashed = false)
        {
            var was = GUI.matrix;
            GUIUtility.RotateAroundPivot(spot.Angle, spot.Centre);
            var rect = new Rect(spot.Centre - spot.Half, spot.Half * 2f);
            if (fill is Color colour) Skin.Fill(rect, colour);
            if (dashed) PlanDashes(rect, outline);
            else PlanFrame(rect, outline, thickness);
            GUI.matrix = was;
        }

        /// <summary>The way from the entrance to the room gone into, an amber line through each room's spot.</summary>
        private static void RouteLine(List<PlacedRoom> route, Func<PlacedRoom, Vector2> at)
        {
            if (route == null || route.Count < 2) return;
            for (var i = 1; i < route.Count; i++) PlanLine(at(route[i - 1]), at(route[i]), Skin.Accent, U(2f));
        }

        /// <summary>Grown, each room's name on its spot, where it does not run into a name already written.</summary>
        private static void Names(Explorer explorer, Rect area)
        {
            var written = WrittenNames;
            written.Clear();
            for (var i = BoxSpots.Count - 1; i >= 0; i--)
            {
                var spot = BoxSpots[i];
                if (spot.Dashed) continue;
                var name = BoxRoomName(explorer, spot.Room);
                var nameW = Skin.Width(Skin.FaintLabel, name);
                var rect = new Rect(spot.Centre.x - nameW / 2f, spot.Centre.y - U(8f), nameW, U(16f));
                if (rect.x < area.x || rect.xMax > area.xMax || rect.y < area.y || rect.yMax > area.yMax || written.Exists(r => r.Overlaps(rect))) continue;
                written.Add(rect);
                Skin.Fill(rect, Skin.Alpha(Skin.Stage, 0.6f));
                GUI.Label(rect, name, Skin.FaintLabel);
            }
        }

        // ----- Map -----

        /// <summary>
        /// Each room across against its depth at the map's angle, the ground at the top: a tile
        /// from its foot to its top, the ways between rooms lines from ground to ground, loops in
        /// dashes, crossroads outlined, and a dial telling the angle.
        /// </summary>
        private static void MapView(Explorer explorer, Rect area, PlacedRoom gone, List<PlacedRoom> route)
        {
            var repaint = Event.current.type == EventType.Repaint;
            var angle = MapAngleShown;
            if (!ReferenceEquals(_overlapsOf, _boxWays) || !Mathf.Approximately(_overlapsAt, angle))
            {
                _overlapsOf = _boxWays;
                _overlapsAt = angle;
                _mapOverlaps = PlanMap.Overlaps(BoxRooms, angle);
            }
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
            var across = MapAcross;
            across.Clear();
            for (var i = 0; i < BoxRooms.Count; i++)
            {
                across.Add(PlanMap.Across(BoxRooms[i], angle));
                lo = Mathf.Min(lo, across[i].Min);
                hi = Mathf.Max(hi, across[i].Max);
            }
            var (top, bottom) = PlanMap.Heights(BoxRooms);
            var inset = U(6f);
            var dial = U(30f);
            float X(float v) => area.x + inset + (v - lo) / Mathf.Max(0.01f, hi - lo) * (area.width - inset * 2f);
            float Y(float h) => area.y + inset + (top - h) / Mathf.Max(0.01f, top - bottom) * (area.height - inset * 2f - dial * 0.5f);

            // Tall rooms first, so the small ones are drawn over them.
            var order = MapOrder;
            order.Clear();
            for (var i = 0; i < BoxRooms.Count; i++) order.Add(i);
            order.Sort((a, b) => BoxRooms[b].Room.Size.Y.CompareTo(BoxRooms[a].Room.Size.Y));
            foreach (var i in order)
            {
                var (foot, roof) = PlanMap.Upright(BoxRooms[i]);
                var rect = Rect.MinMaxRect(X(across[i].Min), Y(roof), Mathf.Max(X(across[i].Max), X(across[i].Min) + U(3f)), Y(foot));
                BoxSpots.Add(new BoxSpot { Room = BoxRooms[i], Centre = rect.center, Half = rect.size / 2f });
            }
            if (!repaint) return;

            // The entrance's ground, dashed across.
            var entrance = _boxWays.Entrance;
            if (entrance != null)
            {
                var groundY = Y(GroundOf(entrance));
                PlanLine(new Vector2(area.x, groundY), new Vector2(area.xMax, groundY), Skin.Alpha(Skin.Faint, 0.5f), 1f, U(3f));
            }
            Vector2 Spot(PlacedRoom room)
            {
                var (min, max) = PlanMap.Across(room, angle);
                return new Vector2((X(min) + X(max)) / 2f, Y(GroundOf(room) + 0.8f));
            }
            foreach (var spot in BoxSpots)
            {
                var crossroads = _boxWays.IsCrossroads(spot.Room);
                var rect = new Rect(spot.Centre - spot.Half, spot.Half * 2f);
                Skin.Fill(rect, spot.Room == gone ? Skin.Alpha(Skin.Accent, 0.75f) : DepthTone(spot.Room));
                PlanFrame(rect, crossroads ? Skin.Alpha(Skin.Text, 0.85f) : Skin.Alpha(Skin.Text, 0.3f), crossroads ? 2f : 1f);
            }
            Ways(Spot);
            foreach (var spot in BoxSpots) if (BoxLit(spot.Room) || spot.Room == gone) PlanFrame(new Rect(spot.Centre - spot.Half, spot.Half * 2f), Skin.Accent, 2f);
            RouteLine(route, Spot);
            if (_planBig) Names(explorer, area);

            // The dial, in the corner: a circle, a needle at the angle, the angle over it.
            var radius = U(12f);
            var centre = new Vector2(area.xMax - radius - U(4f), area.yMax - radius - U(4f));
            Skin.Icon(new Rect(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f), Skin.Circle, Skin.Raised);
            var a = (angle - 90f) * Mathf.Deg2Rad;
            PlanLine(centre, centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius - U(3f)), Skin.Accent, U(2f));
            var said = PlanBoxWords.Angle(angle);
            var saidW = Skin.Width(Skin.FaintLabel, said);
            GUI.Label(new Rect(centre.x - saidW / 2f, centre.y - radius - U(16f), saidW, U(16f)), said, Skin.FaintLabel);
        }

        /// <summary>The ways between the rooms, each a line between their spots, a loop in dashes.</summary>
        private static void Ways(Func<PlacedRoom, Vector2> at)
        {
            foreach (var room in BoxRooms)
            {
                foreach (var other in _boxWays.WaysOf(room))
                {
                    if (!BoxIndex.TryGetValue(other, out var index) || index <= BoxIndex[room]) continue;
                    var loop = _boxWays.IsLoop(room, other);
                    PlanLine(at(room), at(other), Skin.Alpha(Skin.Text, loop ? 0.35f : 0.5f), U(1.2f), loop ? U(3f) : 0f);
                }
            }
        }

        // ----- Tree -----

        /// <summary>
        /// Each room a node at its spot seen from above, pushed apart where rooms crowd, with a
        /// dotted line back to its spot when moved off it: the ways between them lines, loops
        /// dashed and bent, crossroads outlined, and each node's depth beside it.
        /// </summary>
        private static void TreeView(Explorer explorer, Rect area, PlacedRoom gone, List<PlacedRoom> route)
        {
            var repaint = Event.current.type == EventType.Repaint;
            var yaw = Stage.ExampleViewYaw;
            var node = new Vector2(U(18f), U(12f));
            var inner = new Rect(area.x + node.x, area.y + node.y, area.width - node.x * 2f, area.height - node.y * 2f);
            if (!ReferenceEquals(_treeWays, _boxWays) || _treeRead != _boxRead || !Mathf.Approximately(_treeYaw, yaw) || _treeArea != inner)
            {
                _treeWays = _boxWays;
                _treeRead = _boxRead;
                _treeYaw = yaw;
                _treeArea = inner;
                var extent = ExamplePlan.Extent(BoxRooms, yaw);
                var midRight = (extent.MinRight + extent.MaxRight) / 2f;
                var midUp = (extent.MinUp + extent.MaxUp) / 2f;
                var scale = Mathf.Min(inner.width, inner.height) / _boxWidest;
                TreeHomes.Clear();
                foreach (var room in BoxRooms)
                {
                    var (right, up) = ExamplePlan.Turn(room.Position.X, room.Position.Z, yaw);
                    TreeHomes.Add((inner.center.x + (right - midRight) * scale, inner.center.y - (up - midUp) * scale));
                }
                _treeLaid = PinnedLayout.Of(TreeHomes, U(_planBig ? 34f : 24f), inner.x, inner.y, inner.xMax, inner.yMax);
            }
            var at = TreeAt;
            at.Clear();
            for (var i = 0; i < BoxRooms.Count; i++)
            {
                var spot = new Vector2(_treeLaid[i].X, _treeLaid[i].Y);
                at[BoxRooms[i]] = spot;
                BoxSpots.Add(new BoxSpot { Room = BoxRooms[i], Centre = spot, Half = node / 2f });
            }
            if (!repaint) return;

            // Back to its spot, where moved off it.
            for (var i = 0; i < BoxRooms.Count; i++)
            {
                if (!PinnedLayout.Moved(TreeHomes[i], _treeLaid[i])) continue;
                var home = new Vector2(TreeHomes[i].X, TreeHomes[i].Y);
                PlanLine(home, at[BoxRooms[i]], Skin.Alpha(Skin.Faint, 0.45f), 1f, U(1.5f));
                Skin.Icon(new Rect(home.x - U(2f), home.y - U(2f), U(4f), U(4f)), Skin.Circle, Skin.Alpha(Skin.Faint, 0.7f));
            }
            Ways(room => at[room]);
            RouteLine(route, room => at[room]);
            var entranceGround = _boxWays.Entrance != null ? GroundOf(_boxWays.Entrance) : 0f;
            foreach (var spot in BoxSpots)
            {
                var rect = new Rect(spot.Centre - spot.Half, spot.Half * 2f);
                var crossroads = _boxWays.IsCrossroads(spot.Room);
                var lit = BoxLit(spot.Room) || spot.Room == gone;
                Skin.Fill(rect, spot.Room == gone ? Skin.Alpha(Skin.Accent, 0.75f) : DepthTone(spot.Room));
                PlanFrame(rect, lit ? Skin.Accent : crossroads ? Skin.Alpha(Skin.Text, 0.85f) : Skin.Alpha(Skin.Text, 0.3f), lit || crossroads ? 2f : 1f);
                var depth = PlanBoxWords.Depth(GroundOf(spot.Room) - entranceGround);
                var label = _planBig ? PlanBoxWords.Node(BoxRoomName(explorer, spot.Room), depth) : depth;
                var labelW = Skin.Width(Skin.FaintLabel, label);
                var labelX = rect.xMax + U(3f);
                if (labelX + labelW > area.xMax) labelX = rect.x - U(3f) - labelW;
                GUI.Label(new Rect(labelX, spot.Centre.y - U(8f), labelW, U(16f)), label, Skin.FaintLabel);
            }
        }

        // ----- The trail -----

        /// <summary>The way from the entrance to the room gone into, its names going into their rooms; as many of the last as fit.</summary>
        private static void Trail(Explorer explorer, Rect line, List<PlacedRoom> route)
        {
            var e = Event.current;
            if (route == null || route.Count == 0)
            {
                FitLabel(line, PlanBoxWords.NoTrail, Skin.FaintLabel, 10f);
                return;
            }
            var stepW = Skin.Width(Skin.FaintLabel, PlanBoxWords.TrailStep);
            var first = route.Count - 1;
            var total = Skin.Width(Skin.Small, BoxRoomName(explorer, route[first]));
            while (first > 0)
            {
                var more = total + stepW + Skin.Width(Skin.Small, BoxRoomName(explorer, route[first - 1]));
                if (more > line.width) break;
                total = more;
                first--;
            }
            var x = line.x;
            for (var i = first; i < route.Count; i++)
            {
                if (i > first)
                {
                    GUI.Label(new Rect(x, line.y, stepW, line.height), PlanBoxWords.TrailStep, Skin.FaintLabel);
                    x += stepW;
                }
                var name = BoxRoomName(explorer, route[i]);
                var nameW = Skin.Width(Skin.Small, name);
                var rect = new Rect(x, line.y, nameW, line.height);
                LinkLabel(rect, name, Skin.Small, i == route.Count - 1 ? Skin.Accent : Skin.Text);
                if (rect.Contains(e.mousePosition))
                {
                    _planLit = route[i];
                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        Stage.GoInto(route[i]);
                        e.Use();
                    }
                }
                x += nameW;
            }
        }

        /// <summary>A straight line between two points, in dashes where a dash's length is given.</summary>
        private static void PlanLine(Vector2 a, Vector2 b, Color colour, float width, float dash = 0f)
        {
            var d = b - a;
            var length = d.magnitude;
            if (length < 0.5f) return;
            var was = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            if (dash <= 0f) Skin.Fill(new Rect(a.x, a.y - width / 2f, length, width), colour);
            else
            {
                for (var along = 0f; along < length; along += dash * 2f) Skin.Fill(new Rect(a.x + along, a.y - width / 2f, Mathf.Min(dash, length - along), width), colour);
            }
            GUI.matrix = was;
        }

        /// <summary>Lets go of the box's example, as the stage goes or another world is shown.</summary>
        private static void ForgetPlanBox()
        {
            BoxSpots.Clear();
            BoxRooms.Clear();
            BoxGround.Clear();
            BoxIndex.Clear();
            TreeAt.Clear();
            TreeHomes.Clear();
            _treeLaid = new List<(float, float)>();
            _boxWays = null;
            _treeWays = null;
            _overlapsOf = null;
            _planLit = null;
            _planLitForTest = null;
            _boxRead = _boxShown = _treeRead = -1;
        }
    }
}
