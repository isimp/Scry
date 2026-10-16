using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The plan box of a dungeon's example inside, in the stage's lower left corner: its rooms
    /// seen from above, turned with the view and shaded by depth (<see cref="PlanView.Above"/>);
    /// each room across against its depth, a storey tall from its ground, turned to any angle
    /// (<see cref="PlanMap"/>); or a tree of its ways, a row for each room more in from the
    /// entrance (<see cref="TreeRows"/>). In every view the ways go through the doorways they
    /// pass, and the way from the entrance to the room under the mouse, else the room gone into,
    /// is amber. A room under the mouse is named and lit on the stage, and a click goes into it;
    /// a trail of names under the plan leads to the room gone into, each going into its room. It
    /// grows and names its rooms, the stage framed beside it; which view and whether grown are
    /// remembered with the panel.
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
        private static readonly Dictionary<PlacedRoom, Vector2> TreeAt = new Dictionary<PlacedRoom, Vector2>();

        /// <summary>Each room's place among the rooms, lowest first, to draw each way once.</summary>
        private static readonly Dictionary<PlacedRoom, int> BoxIndex = new Dictionary<PlacedRoom, int>();

        /// <summary>The example's rooms and their grounds, worked out again as the example or what is read of it changes.</summary>
        private static DungeonWays _boxWays;
        private static int _boxRead = -1, _boxShown = -1;
        private static readonly List<PlacedRoom> BoxRooms = new List<PlacedRoom>();
        private static readonly Dictionary<PlacedRoom, float> BoxGround = new Dictionary<PlacedRoom, float>();
        private static readonly List<float> BoxGrounds = new List<float>();
        private static float _boxHighest, _boxDeepest, _boxWidest;

        /// <summary>The tree as last laid out (<see cref="TreeRows"/>), kept until the rooms or the view's turn change.</summary>
        private static TreeRows _treeRows;
        private static float _treeYaw = float.NaN;
        private static int _treeRead = -1;
        private static DungeonWays _treeWays;

        /// <summary>The room the way from the entrance is drawn to: the one under the mouse in the box when last drawn, else the room gone into.</summary>
        private static PlacedRoom _planHoverShown;

        /// <summary>How many ways the box drew the last time it was drawn, for the self-test.</summary>
        [Diagnostic]
        public static int PlanBoxWaysDrawn { get; private set; }

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
            _mapAngle = PlanMap.LeastOverlap(BoxRooms, BoxGrounds, MapAngleShown);
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
            BoxGrounds.Clear();
            for (var i = 0; i < BoxRooms.Count; i++)
            {
                BoxIndex[BoxRooms[i]] = i;
                BoxGrounds.Add(BoxGround[BoxRooms[i]]);
            }
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
            // The header holds the three views, grow and fold, and the caption beside them where
            // it has room; else the caption shares a second row with grow and fold.
            var segmentsW = 0f;
            foreach (var view in ViewsRightToLeft) segmentsW += Skin.Width(Skin.SegmentOn, PlanBoxWords.ViewName(view)) + U(12f);
            var controlsW = U(6f) + rowH + U(4f) + U(16f);
            var oneRow = width - pad * 2f >= segmentsW + controlsW + U(90f);
            var headerH = oneRow ? rowH : rowH * 2f + U(2f);
            var rows = pad + headerH + gap + gap + rowH + gap + rowH + (mapBar ? rowH + gap : 0f) + pad;
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

            // The header: the three views, grow and fold, and the caption.
            var controlsY = oneRow ? y : y + rowH + U(2f);
            var fold = new Rect(box.xMax - pad - U(16f), controlsY + U(3f), U(16f), U(16f));
            var grow = new Rect(fold.x - U(4f) - rowH, controlsY, rowH, rowH);
            var segX = oneRow ? grow.x - U(6f) : x + segmentsW;
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
            var area = new Rect(x, y + headerH + gap, w, planH);
            BoxSpots.Clear();
            var gone = Stage.RoomGoneInto;
            var route = gone != null && _boxWays != null ? _boxWays.Route(gone) : null;
            // The way from the entrance drawn in the plan: to the room under the mouse, else to the room gone into.
            KnowRoute(_planHoverShown ?? gone);
            switch (_planView)
            {
                case PlanView.Map:
                    MapView(explorer, area, gone);
                    break;
                case PlanView.Tree:
                    TreeView(explorer, area, gone);
                    break;
                default:
                    AboveView(explorer, area, gone);
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
            if (repaint) _planHoverShown = hovered;
            var caption = hovered != null ? BoxRoomName(explorer, hovered) : PlanBoxWords.Caption(BoxRooms.Count);
            var captionRect = oneRow ? new Rect(x, y, Mathf.Max(U(20f), segX - x - U(4f)), rowH) : new Rect(x, controlsY, Mathf.Max(U(20f), grow.x - U(6f) - x), rowH);
            FitLabel(captionRect, caption, Skin.FaintLabel, 10f);
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
        private static void AboveView(Explorer explorer, Rect area, PlacedRoom gone)
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
            Ways(room => At(room.Position), At);
            foreach (var spot in BoxSpots) if (BoxLit(spot.Room) || spot.Room == gone) BoxTile(spot, null, Skin.Accent, 2f);
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

        /// <summary>The room the way from the entrance is drawn to, and the ways it takes, worked out again only as either changes.</summary>
        private static PlacedRoom _routeTo;
        private static DungeonWays _routeOf;
        private static readonly HashSet<(PlacedRoom, PlacedRoom)> RouteWays = new HashSet<(PlacedRoom, PlacedRoom)>();

        private static void KnowRoute(PlacedRoom to)
        {
            if (ReferenceEquals(to, _routeTo) && ReferenceEquals(_routeOf, _boxWays)) return;
            _routeTo = to;
            _routeOf = _boxWays;
            RouteWays.Clear();
            if (to == null || _boxWays == null) return;
            var route = _boxWays.Route(to);
            for (var i = 1; i < route.Count; i++)
            {
                RouteWays.Add((route[i - 1], route[i]));
                RouteWays.Add((route[i], route[i - 1]));
            }
        }

        /// <summary>
        /// The ways between the rooms: each a line from one room's spot through the doorway they
        /// share, where <paramref name="door"/> places it, to the other's, with a dot at the
        /// doorway; those on the way from the entrance in the accent and bolder, loops in dashes.
        /// </summary>
        private static void Ways(Func<PlacedRoom, Vector2> at, Func<Vec3, Vector2> door)
        {
            var drawn = 0;
            foreach (var room in BoxRooms)
            {
                foreach (var other in _boxWays.WaysOf(room))
                {
                    if (!BoxIndex.TryGetValue(other, out var index) || index <= BoxIndex[room]) continue;
                    drawn++;
                    var onRoute = RouteWays.Contains((room, other));
                    var colour = onRoute ? Skin.Accent : Skin.Alpha(Skin.Text, 0.8f);
                    var width = U(onRoute ? 2.5f : 1.6f);
                    var dash = _boxWays.IsLoop(room, other) ? U(4f) : 0f;
                    var a = at(room);
                    var b = at(other);
                    if (door != null && _boxWays.DoorBetween(room, other) is Vec3 shared)
                    {
                        var through = door(shared);
                        PlanLine(a, through, colour, width, dash);
                        PlanLine(through, b, colour, width, dash);
                        var dot = U(onRoute ? 5f : 4f);
                        Skin.Icon(new Rect(through.x - dot / 2f, through.y - dot / 2f, dot, dot), Skin.Circle, onRoute ? Skin.Accent : Skin.Text);
                    }
                    else PlanLine(a, b, colour, width, dash);
                }
            }
            PlanBoxWaysDrawn = drawn;
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
        /// Each room across against its depth at the map's angle, the ground at the top: a tile a
        /// storey tall from its ground (<see cref="PlanMap.Tile"/>), the ways between rooms lines
        /// through the doorways they share, loops in dashes, crossroads outlined, a scale of metres
        /// from the entrance's ground down the left, and a dial telling the angle.
        /// </summary>
        private static void MapView(Explorer explorer, Rect area, PlacedRoom gone)
        {
            var repaint = Event.current.type == EventType.Repaint;
            var angle = MapAngleShown;
            if (!ReferenceEquals(_overlapsOf, _boxWays) || !Mathf.Approximately(_overlapsAt, angle))
            {
                _overlapsOf = _boxWays;
                _overlapsAt = angle;
                _mapOverlaps = PlanMap.Overlaps(BoxRooms, BoxGrounds, angle);
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
            var (top, bottom) = PlanMap.Heights(BoxGrounds);
            var inset = U(6f);
            var ruler = U(24f);
            var dial = U(30f);
            float X(float v) => area.x + ruler + (v - lo) / Mathf.Max(0.01f, hi - lo) * (area.width - ruler - inset);
            float Y(float h) => area.y + inset + (top - h) / Mathf.Max(0.01f, top - bottom) * (area.height - inset * 2f - dial * 0.5f);

            for (var i = 0; i < BoxRooms.Count; i++)
            {
                var (ground, roof) = PlanMap.Tile(BoxGrounds[i]);
                var rect = Rect.MinMaxRect(X(across[i].Min), Y(roof), Mathf.Max(X(across[i].Max), X(across[i].Min) + U(3f)), Y(ground));
                BoxSpots.Add(new BoxSpot { Room = BoxRooms[i], Centre = rect.center, Half = rect.size / 2f });
            }
            if (!repaint) return;

            // Every ten metres from the entrance's ground, its own dashed.
            var entranceGround = _boxWays.Entrance != null ? GroundOf(_boxWays.Entrance) : 0f;
            for (var metres = Mathf.CeilToInt((bottom - entranceGround) / 10f) * 10; metres <= top - entranceGround; metres += 10)
            {
                var lineY = Y(entranceGround + metres);
                PlanLine(new Vector2(area.x + ruler - U(3f), lineY), new Vector2(area.xMax, lineY), Skin.Alpha(Skin.Faint, metres == 0 ? 0.5f : 0.18f), 1f, metres == 0 ? U(3f) : 0f);
                var said = PlanBoxWords.Depth(metres);
                var saidW = Skin.Width(Skin.FaintLabel, said);
                GUI.Label(new Rect(area.x + ruler - U(4f) - saidW, lineY - U(8f), saidW, U(16f)), said, Skin.FaintLabel);
            }
            foreach (var spot in BoxSpots)
            {
                var crossroads = _boxWays.IsCrossroads(spot.Room);
                var rect = new Rect(spot.Centre - spot.Half, spot.Half * 2f);
                Skin.Fill(rect, spot.Room == gone ? Skin.Alpha(Skin.Accent, 0.75f) : DepthTone(spot.Room));
                PlanFrame(rect, crossroads ? Skin.Alpha(Skin.Text, 0.85f) : Skin.Alpha(Skin.Text, 0.3f), crossroads ? 2f : 1f);
            }
            var middle = PlanMap.Storey / 2f;
            Ways(room =>
            {
                var (min, max) = PlanMap.Across(room, angle);
                return new Vector2((X(min) + X(max)) / 2f, Y(GroundOf(room) + middle));
            }, door => new Vector2(X(PlanMap.AcrossAt(door, angle)), Y(door.Y + middle)));
            foreach (var spot in BoxSpots) if (BoxLit(spot.Room) || spot.Room == gone) PlanFrame(new Rect(spot.Centre - spot.Half, spot.Half * 2f), Skin.Accent, 2f);
            if (_planBig) Names(explorer, area);

            // The dial, in the corner: a circle, a needle at the angle, the angle over it.
            var radius = U(12f);
            var centre = new Vector2(area.xMax - radius - U(4f), area.yMax - radius - U(4f));
            Skin.Icon(new Rect(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f), Skin.Circle, Skin.Raised);
            var a = (angle - 90f) * Mathf.Deg2Rad;
            PlanLine(centre, centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius - U(3f)), Skin.Accent, U(2f));
            var angleSaid = PlanBoxWords.Angle(angle);
            var angleW = Skin.Width(Skin.FaintLabel, angleSaid);
            GUI.Label(new Rect(centre.x - angleW / 2f, centre.y - radius - U(16f), angleW, U(16f)), angleSaid, Skin.FaintLabel);
        }

        // ----- Tree -----

        /// <summary>
        /// The tree of the ways (<see cref="TreeRows"/>): a row for each room more in from the
        /// entrance, the entrance at the top, each room under the room it is reached from and
        /// rooms under one room in the order they lie across the plan turned with the view; the
        /// ways lines between the nodes, loops in dashes, crossroads outlined, and each node's depth
        /// beside it, and grown, its name.
        /// </summary>
        private static void TreeView(Explorer explorer, Rect area, PlacedRoom gone)
        {
            var repaint = Event.current.type == EventType.Repaint;
            var yaw = Stage.ExampleViewYaw;
            if (_treeRows == null || !ReferenceEquals(_treeWays, _boxWays) || _treeRead != _boxRead || !Mathf.Approximately(_treeYaw, yaw))
            {
                _treeWays = _boxWays;
                _treeRead = _boxRead;
                _treeYaw = yaw;
                _treeRows = TreeRows.Of(_boxWays, room => ExamplePlan.Turn(room.Position.X, room.Position.Z, yaw).Right);
            }
            var node = new Vector2(U(18f), U(12f));
            var inner = new Rect(area.x + node.x, area.y + node.y, area.width - node.x * 2f, area.height - node.y * 2f);
            var rowGap = _treeRows.Rows > 1 ? inner.height / (_treeRows.Rows - 1) : 0f;
            var at = TreeAt;
            at.Clear();
            foreach (var room in BoxRooms)
            {
                var row = _treeRows.RowOf(room);
                var spot = new Vector2(inner.x + _treeRows.AcrossOf(room) * inner.width, _treeRows.Rows > 1 ? inner.y + row * rowGap : inner.center.y);
                at[room] = spot;
                BoxSpots.Add(new BoxSpot { Room = room, Centre = spot, Half = node / 2f });
            }
            if (!repaint) return;

            Ways(room => at[room], null);
            var entranceGround = _boxWays.Entrance != null ? GroundOf(_boxWays.Entrance) : 0f;
            foreach (var spot in BoxSpots)
            {
                var rect = new Rect(spot.Centre - spot.Half, spot.Half * 2f);
                var crossroads = _boxWays.IsCrossroads(spot.Room);
                var lit = BoxLit(spot.Room) || spot.Room == gone;
                Skin.Fill(rect, spot.Room == gone ? Skin.Alpha(Skin.Accent, 0.75f) : DepthTone(spot.Room));
                PlanFrame(rect, lit ? Skin.Accent : crossroads ? Skin.Alpha(Skin.Text, 0.85f) : Skin.Alpha(Skin.Text, 0.3f), lit || crossroads ? 2f : 1f);
            }

            // Each node's label beside it, the room gone into's and those lit first: right of it, else
            // left, where it covers no other label or node; else left out, the caption naming the
            // room under the mouse.
            WrittenNames.Clear();
            foreach (var spot in BoxSpots) if (spot.Room == gone || BoxLit(spot.Room)) TreeLabel(explorer, spot, area, entranceGround);
            foreach (var spot in BoxSpots) if (spot.Room != gone && !BoxLit(spot.Room)) TreeLabel(explorer, spot, area, entranceGround);
        }

        /// <summary>A node's label, its depth and, grown, its room's name, where it covers nothing already drawn.</summary>
        private static void TreeLabel(Explorer explorer, BoxSpot spot, Rect area, float entranceGround)
        {
            var depth = PlanBoxWords.Depth(GroundOf(spot.Room) - entranceGround);
            var label = _planBig ? PlanBoxWords.Node(BoxRoomName(explorer, spot.Room), depth) : depth;
            var labelW = Skin.Width(Skin.FaintLabel, label);
            var right = new Rect(spot.Centre.x + spot.Half.x + U(3f), spot.Centre.y - U(8f), labelW, U(16f));
            var left = new Rect(spot.Centre.x - spot.Half.x - U(3f) - labelW, right.y, labelW, U(16f));
            bool Clear(Rect r)
            {
                if (r.x < area.x || r.xMax > area.xMax) return false;
                foreach (var written in WrittenNames) if (written.Overlaps(r)) return false;
                foreach (var other in BoxSpots) if (new Rect(other.Centre - other.Half, other.Half * 2f).Overlaps(r)) return false;
                return true;
            }
            var at = Clear(right) ? right : Clear(left) ? left : (Rect?)null;
            if (!(at is Rect place)) return;
            WrittenNames.Add(place);
            GUI.Label(place, label, Skin.FaintLabel);
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
            BoxGrounds.Clear();
            RouteWays.Clear();
            _treeRows = null;
            _boxWays = null;
            _treeWays = null;
            _routeOf = null;
            _routeTo = null;
            _planHoverShown = null;
            _overlapsOf = null;
            _planLit = null;
            _planLitForTest = null;
            _boxRead = _boxShown = _treeRead = -1;
        }
    }
}
