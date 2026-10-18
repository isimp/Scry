using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The plan box of a dungeon's example inside, in the stage's lower left corner: its rooms
    /// seen from above, turned with the view and shaded by depth (<see cref="PlanView.Above"/>);
    /// each of its levels seen so, one under the other, rooms over each other lined up
    /// (<see cref="PlanLevels"/>); or a tree of its ways, a row for each room more in from the
    /// entrance, each room where it lies across (<see cref="TreeRows"/>). In every view the ways go through the doorways they
    /// pass, and the way from the entrance to the room lit, else the room gone into, is amber. A
    /// room under the mouse, in the box or on the stage, is lit in both (<see cref="RoomLight"/>):
    /// framed and named in the box and outlined on the stage; a click on it goes into it;
    /// a trail of names under the plan leads to the room gone into, each going into its room. It
    /// grows and names its rooms, the stage framed beside it; which view and whether grown are
    /// remembered with the panel.
    /// </summary>
    internal static partial class ScryPanel
    {
        private static PlanView _planView = PlanView.Above;
        private static bool _planBig;

        /// <summary>The room under the mouse in the box or its trail, as last looked; none.</summary>
        private static PlacedRoom _boxHover;

        /// <summary>The room lit as the box is drawn (<see cref="RoomLight"/>), from what was under the mouse when last looked; none.</summary>
        private static PlacedRoom _boxLit;

        /// <summary>A room as the box last drew it: its middle, half its size each way, how it is turned, and whether it stands only as dashes, above the room gone into.</summary>
        private struct BoxSpot
        {
            public PlacedRoom Room;
            public Vector2 Centre;
            public Vector2 Half;
            public float Angle;
            public bool Dashed;

            /// <summary>Its four corners where it is drawn as a four-sided shape, as on a plate seen at a slant; none for a turned rectangle.</summary>
            public Vector2[] Quad;

            public bool Contains(Vector2 point)
            {
                if (Quad != null) return InQuad(Quad, point);
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
        private static readonly PlanView[] Views = { PlanView.Above, PlanView.Levels, PlanView.Tree };

        /// <summary>Where the box's controls were last drawn, by name, for the self-test: the views, grow and fold, the plan's own switches, the plan.</summary>
        [Diagnostic]
        public static List<(string Part, Rect At)> PlanBoxPartsForTest { get; } = new List<(string, Rect)>();

        /// <summary>What each drawing of the box works with, kept between drawings rather than made each time.</summary>
        private static readonly List<Rect> WrittenNames = new List<Rect>();
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

        /// <summary>How many rooms each row of the tree holds, and how wide its rows were last drawn, for its names to fit.</summary>
        private static float _treeInnerWidth;
        private static int _treeRead = -1;
        private static DungeonWays _treeWays;

        /// <summary>How many ways the box drew the last time it was drawn, for the self-test.</summary>
        [Diagnostic]
        public static int PlanBoxWaysDrawn { get; private set; }

        /// <summary>The walkways the plan from above draws its ways with, for the self-test.</summary>
        [Diagnostic]
        public static PlanWalkways PlanBoxWalkwaysForTest => WalkwaysNow();

        /// <summary>The walkway along the way from the entrance to the room lit or gone into, for the self-test.</summary>
        [Diagnostic]
        public static IReadOnlyList<Vec3> PlanBoxRouteWalkForTest => RouteWalk.Select(p => p.Point).ToList();

        /// <summary>The marks of the ways down the plan from above last drew, each with the room left out below it leads to, for the self-test.</summary>
        [Diagnostic]
        public static IReadOnlyList<(Rect At, PlacedRoom Room)> PlanBoxWayDownMarksForTest => WayDownMarks;

        /// <summary>Where the box last stood, for the self-test; none while it is not drawn.</summary>
        [Diagnostic]
        public static Rect PlanBoxRect { get; private set; }

        [Diagnostic]
        public static PlanView PlanBoxView => _planView;

        [Diagnostic]
        public static bool PlanBoxBig => _planBig;

        /// <summary>Shows a view of the box as its segment does, for the self-test.</summary>
        [Diagnostic]
        public static void ShowPlanViewForTest(PlanView view) => ChoosePlanView(view);

        /// <summary>Grows or shrinks the box as its corner button does, for the self-test.</summary>
        [Diagnostic]
        public static void GrowPlanForTest(bool big) => GrowPlan(big);

        /// <summary>How the box last stood as the stage held it, whole, lean or folded (<see cref="CornerFit.Shape"/>), for the self-test.</summary>
        [Diagnostic]
        public static BoxShape PlanBoxShapeShown { get; private set; }

        /// <summary>
        /// The box folded to a small tab where it stands: folded by the player, a click brings it
        /// back; folded as the stage is too short for it under the card of the room gone into, it
        /// says so, unlit, and the box comes back as the stage grows or the room is come out of.
        /// </summary>
        private static Rect PlanTab(Rect inner, bool byPlayer)
        {
            var e = Event.current;
            Stage.ViewShift = 0f;
            var tabW = Skin.Width(Skin.Chip, PlanBoxWords.Tab) + U(4f);
            var tab = new Rect(inner.x + U(8f), inner.yMax - U(CornerFit.Bottom) - U(22f), tabW, U(22f));
            CountDrawn(PanelPart.PlanTab);
            if (byPlayer)
            {
                if (tab.Contains(e.mousePosition)) AskTip("plan:tab", PlanBoxWords.TabTip);
                if (GUI.Button(tab, PlanBoxWords.Tab, Skin.Fitted(Skin.Chip, tab))) PlanFolded = false;
            }
            else
            {
                if (tab.Contains(e.mousePosition)) AskTip("plan:short", PlanBoxWords.TooShortTip);
                var enabled = GUI.enabled;
                GUI.enabled = false;
                GUI.Button(tab, PlanBoxWords.Tab, Skin.Fitted(Skin.Chip, tab));
                GUI.enabled = enabled;
            }
            return tab;
        }

        /// <summary>How many levels the box last laid out, for the self-test.</summary>
        [Diagnostic]
        public static int PlanBoxLevelsShown => _levels?.Levels.Count ?? 0;

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

        /// <summary>Points the mouse at a point of the box, as the self-test does, until let go with none; the mouse on the stage counts for nothing meanwhile.</summary>
        [Diagnostic]
        public static void HoverPlanBoxForTest(Vector2? point) => _boxPointForTest = point;

        /// <summary>The rooms the box framed as lit the last time it was drawn, for the self-test.</summary>
        [Diagnostic]
        public static List<PlacedRoom> PlanBoxLitShown { get; } = new List<PlacedRoom>();

        /// <summary>The room the way from the entrance was last drawn to, for the self-test.</summary>
        [Diagnostic]
        public static PlacedRoom PlanBoxRouteShown => _routeTo;

        /// <summary>A room's name as the box gives it, for the self-test.</summary>
        [Diagnostic]
        public static string PlanBoxRoomNameForTest(Explorer explorer, PlacedRoom room) => BoxRoomName(explorer, room);

        /// <summary>Double-clicks a room in the box as the mouse does, going into it, for the self-test.</summary>
        [Diagnostic]
        public static void DoubleClickPlanRoomForTest(PlacedRoom room) => Stage.GoInto(room);

        /// <summary>What the panel's file remembers now, for the self-test.</summary>
        [Diagnostic]
        public static PanelPlace RememberedForTest() => File.Exists(RectFile) ? PanelPlace.Read(File.ReadAllLines(RectFile)) : new PanelPlace();

        /// <summary>The point of the box the self-test points the mouse at; none.</summary>
        private static Vector2? _boxPointForTest;

        /// <summary>Where the mouse is for finding the room under it in the box: the self-test's point while it points at the box, nowhere while it points at the stage, else the player's own.</summary>
        private static Vector2 BoxMouse(Event e) => _boxPointForTest ?? (_stagePointForTest.HasValue ? Nowhere : OwnMouse(e));

        /// <summary>Takes the view and size the box was left in, as remembered with the panel.</summary>
        private static void TakePlanBoxPlace(PanelPlace place)
        {
            if (place.PlanView.HasValue) _planView = place.PlanView.Value;
            if (place.PlanBig.HasValue) _planBig = place.PlanBig.Value;
            if (place.PlanBelow.HasValue) PlanBelowShown = place.PlanBelow.Value;
            if (place.PlanNames.HasValue) PlanNamesShown = place.PlanNames.Value;
        }

        /// <summary>Whether the grown plan names its rooms, in every view; else it names none.</summary>
        public static bool PlanNamesShown { get; set; } = true;

        /// <summary>Names the rooms on the grown plan, or leaves the names off, as the box's switch does; remembered with the panel.</summary>
        private static void ShowNames(bool shown)
        {
            if (shown == PlanNamesShown) return;
            PlanNamesShown = shown;
            SaveRects();
        }

        /// <summary>Names the rooms or leaves the names off, as the box's switch does, for the self-test.</summary>
        [Diagnostic]
        public static void ShowNamesForTest(bool shown) => ShowNames(shown);

        private static void ChoosePlanView(PlanView view)
        {
            if (view == _planView) return;
            _planView = view;
            SaveRects();
        }

        /// <summary>Whether the plan from above shows the rooms below the room gone into or the floor opened; else it leaves them out, their ways down marked. The stage shows them as it does either way.</summary>
        public static bool PlanBelowShown { get; set; } = true;

        /// <summary>Shows the rooms below in the plan from above, or leaves them out, as the box's switch does; remembered with the panel.</summary>
        private static void ShowBelow(bool shown)
        {
            if (shown == PlanBelowShown) return;
            PlanBelowShown = shown;
            SaveRects();
        }

        /// <summary>The level the plan from above leaves out the rooms below of, its switch off: the room gone into's ground, else the floor opened; none with neither, or with the switch on.</summary>
        private static float? PlanBelowLevel(PlacedRoom gone) =>
            PlanBelowShown ? (float?)null : gone != null ? GroundOf(gone) : Stage.ExampleOpenMarkLow ?? Stage.ExampleOpenFloor;

        /// <summary>Flips the switch for the rooms below as its button does, for the self-test.</summary>
        [Diagnostic]
        public static void ShowBelowForTest(bool shown) => ShowBelow(shown);

        private static void GrowPlan(bool big)
        {
            if (big == _planBig) return;
            _planBig = big;
            SaveRects();
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
            _levelsRead = -1;
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
            // The room lit as the box is drawn, from what was under the mouse when last looked, as the
            // box finds its own only once its rooms are drawn.
            _boxLit = RoomLight.Lit(_cardLit, _boxHover, _stageHover) ?? Stage.PickedRoom;
            _boxHover = null;
            if (!KnowBoxRooms() || BoxRooms.Count == 0) return PlanBoxRect = Rect.zero;

            // Folded, a small tab where it stands, which brings it back.
            if (PlanFolded) return PlanBoxRect = PlanTab(inner, byPlayer: true);

            var pad = U(6f);
            var rowH = U(22f);
            var gap = U(4f);
            var width = Mathf.Min(U(_planBig ? 400f : 236f), inner.width - U(16f));
            var planH = U(_planBig ? 330f : 150f);
            // The header holds the views from the left, and grow and fold in the box's top right corner
            // beside them, the views drawn tighter where that alone leaves them room, else on a row of
            // their own under them (PlanBoxHeader); a line under it, over the plan.
            var segmentsW = 0f;
            var tightW = 0f;
            foreach (var view in Views)
            {
                segmentsW += Skin.Width(Skin.SegmentOn, PlanBoxWords.ViewName(view)) + U(10f) + U(2f);
                tightW += Skin.Width(Skin.SegmentOnTight, PlanBoxWords.ViewName(view)) + U(2f);
            }
            var laid = PlanBoxHeader.Lay(width - pad * 2f, segmentsW, tightW, rowH, gap);
            var headerH = (laid.Rows == 1 ? rowH : rowH * 2f + U(2f)) + gap + U(1f);
            // Whole, with its trail and its Entrance and Deepest, where the stage holds it so; else
            // without them; else, with a room gone into, folded to its tab (CornerFit).
            var card = (Stage.RoomGoneInto != null || Stage.PickedRoom != null) && Stage.HasInside && Stage.ShowsInside;
            // The header and the plan, nothing under it: where the view is stands on the stage's breadcrumb.
            // While peeking at the world's own dungeon, the peek's warning between them.
            var peekH = PeekStripShown ? rowH + gap : 0f;
            // Over the world's own dungeon, how much of it is walked in a row under the plan, clear of its rooms.
            var walkedH = WalkedShown ? rowH : 0f;
            var leanRows = pad + headerH + gap + peekH + walkedH + pad;
            var wholeRows = leanRows;
            var shape = CornerFit.Shape(inner.height, wholeRows, leanRows, card, _s);
            if (repaint) PlanBoxShapeShown = shape;
            if (shape == BoxShape.Folded) return PlanBoxRect = PlanTab(inner, byPlayer: false);
            var rows = shape == BoxShape.Whole ? wholeRows : leanRows;
            // As tall as wanted where the stage has room; with a room gone into, the card over it gives way first.
            planH = CornerFit.Plan(inner.height, rows, planH, card, _s);
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

            if (repaint) PlanBoxPartsForTest.Clear();
            // The header: the views from the left; grow and fold in the box's top right corner.
            var controlsY = y + laid.ControlsRow * (rowH + U(2f));
            var fold = new Rect(x + laid.FoldX, controlsY, rowH, rowH);
            var grow = new Rect(x + laid.GrowX, controlsY, rowH, rowH);
            var segX = x;
            foreach (var view in Views)
            {
                var name = PlanBoxWords.ViewName(view);
                var on = view == _planView;
                var style = laid.Tight ? (on ? Skin.SegmentOnTight : Skin.SegmentTight) : on ? Skin.SegmentOn : Skin.Segment;
                var segW = laid.Tight ? Skin.Width(Skin.SegmentOnTight, name) : Skin.Width(Skin.SegmentOn, name) + U(10f);
                var segment = new Rect(segX, y, segW, rowH);
                segX += segW + U(2f);
                if (repaint) PlanBoxPartsForTest.Add(("view", segment));
                if (segment.Contains(e.mousePosition)) AskTip("plan-view:" + name, PlanBoxWords.ViewTip(view));
                if (GUI.Button(segment, name, Skin.Fitted(style, segment)) && !on) ChoosePlanView(view);
            }
            if (repaint)
            {
                PlanBoxPartsForTest.Add(("grow", grow));
                PlanBoxPartsForTest.Add(("fold", fold));
            }
            if (grow.Contains(e.mousePosition)) AskTip("plan:grow", PlanBoxWords.GrowTip(_planBig));
            if (GUI.Button(grow, GUIContent.none, _planBig ? Skin.On : Skin.IconButton)) GrowPlan(!_planBig);
            if (repaint) Skin.Icon(new Rect(grow.x + U(4f), grow.y + U(4f), grow.width - U(8f), grow.height - U(8f)), Skin.Resize, _planBig ? Skin.Accent : Skin.Dim);
            if (fold.Contains(e.mousePosition)) AskTip("plan:fold", PlanBoxWords.FoldTip);
            if (GUI.Button(fold, GUIContent.none, Skin.IconButton))
            {
                PlanFolded = true;
                return box;
            }
            if (repaint) Skin.Icon(new Rect(fold.x + U(4f), fold.y + U(4f), fold.width - U(8f), fold.height - U(8f)), Skin.FoldDown, fold.Contains(e.mousePosition) ? Skin.Text : Skin.Dim);
            // The line under the header, across the box, so the plan's own switches under it stand apart from grow and fold.
            var line = new Rect(x, y + headerH - U(1f), w, U(1f));
            Skin.Fill(line, Skin.Outline);
            if (repaint) PlanBoxPartsForTest.Add(("line", line));
            if (peekH > 0f) PeekStrip(new Rect(x, y + headerH + gap, w, rowH));

            // The plan itself.
            var area = new Rect(x, y + headerH + gap + peekH, w, planH);
            BoxSpots.Clear();
            var gone = Stage.RoomGoneInto;
            // The way from the entrance drawn in the plan: to the room lit, else to the room gone into.
            KnowRoute(RoomLight.RouteTo(_boxLit, gone));
            ViewLabels.Clear();
            switch (_planView)
            {
                case PlanView.Levels:
                    LevelsView(explorer, area, gone);
                    break;
                case PlanView.Tree:
                    TreeView(explorer, area, gone);
                    break;
                default:
                    AboveView(explorer, area, gone);
                    break;
            }
            if (repaint)
            {
                PlanBoxLitShown.Clear();
                foreach (var spot in BoxSpots) if (BoxLit(spot.Room)) PlanBoxLitShown.Add(spot.Room);
            }

            // The plan's own switches, joined over its top right corner: the rooms below (from above only) and the names.
            var corner = PlanSwitches(area, rowH);
            // How much of the world's own dungeon is walked, in its row under the plan.
            if (walkedH > 0f)
            {
                var walked = new Rect(area.x, area.yMax, area.width, walkedH);
                WalkedLabel(walked);
                if (repaint) PlanBoxPartsForTest.Add(("walked", walked));
            }
            if (repaint) PlanBoxPartsForTest.Add(("plan", area));

            // The room under the mouse: lit here and on the stage, picked on a click, gone into on a double-click.
            var mouse = BoxMouse(e);
            var hovered = area.Contains(mouse) && !corner.Contains(mouse) && (_drag == Drag.None || _drag == Drag.Orbit) ? BoxRoomAt(mouse) : null;
            if (hovered != null)
            {
                _boxHover = hovered;
                AskTip("planbox:" + hovered.Room.Name, Peek(explorer, hovered));
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (e.clickCount == 2) Stage.GoInto(hovered);
                    else Stage.Pick(hovered);
                    e.Use();
                }
            }

            // The box is not the stage: a click on it neither turns nor zooms it, and the wheel on it
            // steps the cut, a level or a room's mark a notch, as Shift and the wheel do on the stage.
            if (box.Contains(e.mousePosition) && e.type == EventType.ScrollWheel && Stage.HasFloors) WheelStep(StageCamera.WheelTurn(e.delta.x, e.delta.y));
            if (box.Contains(e.mousePosition) && (e.type == EventType.MouseDown || e.type == EventType.ScrollWheel)) e.Use();
            return box;
        }

        /// <summary>
        /// The plan's own switches over its top right corner (<see cref="PlanBoxHeader.Corner"/>):
        /// the rooms below the room gone into or the floor opened, from above only, and the rooms'
        /// names, on the grown plan only as only it names rooms; joined where both stand. Drawn over
        /// the plan and taking the mouse before it; where they stand, none where neither does.
        /// </summary>
        private static Rect PlanSwitches(Rect area, float size)
        {
            var e = Event.current;
            var repaint = e.type == EventType.Repaint;
            var belowOn = _planView == PlanView.Above;
            var namesOn = _planBig;
            var (namesX, belowX, left) = PlanBoxHeader.Corner(area.width, size, belowOn, namesOn);
            // Both shown, they are one piece, rounded at its ends only, a point's seam between them.
            var joined = belowOn && namesOn;
            if (belowOn)
            {
                var below = new Rect(area.x + belowX, area.y, joined ? size - U(1f) : size, size);
                var belowShown = PlanBelowShown;
                if (repaint) PlanBoxPartsForTest.Add(("below", below));
                if (below.Contains(e.mousePosition)) AskTip("plan:below", PlanBoxWords.BelowTip(belowShown));
                var belowStyle = joined ? (belowShown ? Skin.JoinedLeftOn : Skin.JoinedLeft) : belowShown ? Skin.On : Skin.IconButton;
                if (GUI.Button(below, GUIContent.none, belowStyle)) ShowBelow(!belowShown);
                if (repaint) Skin.Icon(new Rect(below.x + U(4f), below.y + U(4f), size - U(8f), below.height - U(8f)), Skin.Down, belowShown ? Skin.Accent : Skin.Dim);
            }
            if (namesOn)
            {
                var names = new Rect(area.x + namesX, area.y, size, size);
                var namesShown = PlanNamesShown;
                if (repaint) PlanBoxPartsForTest.Add(("names", names));
                if (names.Contains(e.mousePosition)) AskTip("plan:names", PlanBoxWords.NamesTip(namesShown));
                // Its mark drawn over the button as the rooms below's icon is, in the accent while on.
                var namesStyle = joined ? (namesShown ? Skin.JoinedRightOn : Skin.JoinedRight) : namesShown ? Skin.On : Skin.IconButton;
                if (GUI.Button(names, GUIContent.none, namesStyle)) ShowNames(!namesShown);
                if (repaint) Skin.LabelIn(names, PlanBoxWords.NamesMark, Skin.Glyph, namesShown ? Skin.Accent : Skin.Dim);
            }
            return new Rect(area.x + left, area.y, area.width - left, size);
        }

        /// <summary>A room's name as the plan box, the room card and the breadcrumb tell it: its page's, else its prefab's words; a kind not met, "a room not yet walked".</summary>
        private static string BoxRoomName(Explorer explorer, PlacedRoom room)
        {
            // Under the strict rules a kind of room not yet walked stands unnamed.
            var key = EntryKeys.For(Kind.Location, room.Room.Name);
            return explorer.NotMet(key) ? DungeonWords.RoomNotWalked : ShownName(explorer, key, LocationWords.RoomName(room.Room.Name));
        }

        /// <summary>Whether a point lies inside a four-sided shape with no corner bent in, its corners in order round it.</summary>
        private static bool InQuad(Vector2[] quad, Vector2 point)
        {
            var sign = 0f;
            for (var i = 0; i < 4; i++)
            {
                var a = quad[i];
                var b = quad[(i + 1) % 4];
                var cross = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
                if (Mathf.Abs(cross) < 1e-4f) continue;
                if (sign == 0f) sign = Mathf.Sign(cross);
                else if (Mathf.Sign(cross) != sign) return false;
            }
            return true;
        }

        /// <summary>The room under a point of the box: the topmost drawn whole there, else the topmost drawn in dashes.</summary>
        private static PlacedRoom BoxRoomAt(Vector2 point)
        {
            // A way down's mark stands for the room it leads to, left out below.
            foreach (var (mark, room) in WayDownMarks) if (mark.Contains(point)) return room;
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

        /// <summary>Whether a room is the one lit in the box: under the mouse there or on the stage, or a way chip of the card hovered for it (<see cref="RoomLight"/>).</summary>
        private static bool BoxLit(PlacedRoom room) => RoomLight.Lights(_boxLit, room);

        // ----- Above -----

        /// <summary>
        /// Seen from above, turned as the view is and at one scale whichever way, each room a
        /// tile shaded by its depth, the room gone into in the accent and those above it in
        /// dashes, so the rooms under them show; the ways between them walkways along the rooms' walls.
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
            var hideBelow = PlanBelowLevel(gone);
            foreach (var room in BoxRooms)
            {
                if (RoomFocus.HiddenBelow(GroundOf(room), hideBelow)) continue;
                // A room put away on the stage over the room gone into is dashed.
                var above = gone != null && room != gone && Stage.ExampleRoomAway(room);
                BoxSpots.Add(new BoxSpot
                {
                    Room = room, Centre = At(room.Position), Angle = room.Rotation.YawDegrees - yaw, Dashed = above,
                    Half = new Vector2(Mathf.Max(U(1f), room.Room.Size.X * scale / 2f), Mathf.Max(U(1f), room.Room.Size.Z * scale / 2f)),
                });
            }
            if (repaint)
            {
                foreach (var spot in BoxSpots) if (!spot.Dashed) RoomTile(spot, gone);
                foreach (var spot in BoxSpots) if (spot.Dashed) BoxTile(spot, null, Skin.Alpha(Skin.Faint, 0.6f), 1f, dashed: true);
                Walkways((room, p) => At(p), hideBelow, drops: false);
                foreach (var spot in BoxSpots) if (BoxOutlined(spot.Room, gone)) BoxTile(spot, null, Skin.Accent, 2f);
            }
            YouAndWays((room, p) => At(p));
            if (repaint && _planBig && PlanNamesShown) Names(explorer, area);
        }

        /// <summary>A room's tile, turned as it is: filled, outlined, or outlined in dashes.</summary>
        private static void BoxTile(BoxSpot spot, Color? fill, Color outline, float thickness, bool dashed = false)
        {
            if (spot.Quad != null)
            {
                if (fill is Color shade) QuadFill(spot.Quad, shade);
                for (var c = 0; c < 4; c++) PlanLine(spot.Quad[c], spot.Quad[(c + 1) % 4], outline, thickness, dashed ? U(3f) : 0f);
                return;
            }
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

        /// <summary>The walkway along the way from the entrance to that room, for the plan from above.</summary>
        private static readonly List<(Vec3 Point, PlacedRoom Room)> RouteWalk = new List<(Vec3, PlacedRoom)>();

        private static void KnowRoute(PlacedRoom to)
        {
            if (ReferenceEquals(to, _routeTo) && ReferenceEquals(_routeOf, _boxWays)) return;
            _routeTo = to;
            _routeOf = _boxWays;
            RouteWays.Clear();
            RouteWalk.Clear();
            if (to == null || _boxWays == null) return;
            var route = _boxWays.Route(to);
            for (var i = 1; i < route.Count; i++)
            {
                RouteWays.Add((route[i - 1], route[i]));
                RouteWays.Add((route[i], route[i - 1]));
            }
            RouteWalk.AddRange(WalkwaysNow().RouteIn(route));
        }

        /// <summary>The walkways of the box's example from above, worked out again only as its ways change.</summary>
        private static PlanWalkways _walkways;
        private static DungeonWays _walkwaysOf;

        private static PlanWalkways WalkwaysNow()
        {
            if (ReferenceEquals(_walkwaysOf, _boxWays)) return _walkways;
            _walkwaysOf = _boxWays;
            return _walkways = _boxWays != null ? PlanWalkways.Of(_boxWays) : null;
        }

        /// <summary>The marks of the ways down to rooms left out below, as the plan from above last drew them, each with the room it leads to: the mouse on one finds that room, a click goes into it.</summary>
        private static readonly List<(Rect At, PlacedRoom Room)> WayDownMarks = new List<(Rect, PlacedRoom)>();

        /// <summary>
        /// The ways between the rooms as walkways (<see cref="PlanWalkways"/>), from above or on the
        /// levels' plates: each room's hall along its longer side and each doorway it shares joined to
        /// it square to its walls, a dot at each such doorway, dropping between two plates where its
        /// rooms stand on different ones (<paramref name="drops"/>); the walkway from the entrance in
        /// the accent and bolder over them, ways closing a loop in dashes.
        /// </summary>
        private static void Walkways(Func<PlacedRoom, Vec3, Vector2> at, float? hideBelow, bool drops)
        {
            WayDownMarks.Clear();
            var walkways = WalkwaysNow();
            if (walkways == null) return;
            var colour = Skin.Alpha(Skin.Text, 0.8f);
            var width = U(1.6f);
            foreach (var line in walkways.Lines)
            {
                if (RoomFocus.HiddenBelow(GroundOf(line.Room), hideBelow)) continue;
                PlanLine(at(line.Room, line.From), at(line.Room, line.To), colour, width, line.Loop ? U(4f) : 0f);
            }
            // The way from the entrance, dropping from one plate to the next at the doorway it goes through.
            for (var i = 1; i < RouteWalk.Count; i++)
            {
                var (from, fromRoom) = RouteWalk[i - 1];
                var (to, toRoom) = RouteWalk[i];
                if (fromRoom != toRoom) PlanLine(at(fromRoom, from), at(toRoom, from), Skin.Accent, U(2.5f));
                PlanLine(at(toRoom, from), at(toRoom, to), Skin.Accent, U(2.5f));
            }
            // A dot at each doorway two rooms shown share, dropping between their plates where they
            // differ; where one of them is left out below, a mark of the way down.
            var drawn = 0;
            PlanBoxWaysDown = 0;
            foreach (var room in BoxRooms)
            {
                foreach (var other in _boxWays.WaysOf(room))
                {
                    if (!BoxIndex.TryGetValue(other, out var index) || index <= BoxIndex[room]) continue;
                    var hiddenHere = RoomFocus.HiddenBelow(GroundOf(room), hideBelow);
                    var hiddenThere = RoomFocus.HiddenBelow(GroundOf(other), hideBelow);
                    if (hiddenHere && hiddenThere) continue;
                    drawn++;
                    if (!(_boxWays.DoorBetween(room, other) is Vec3 shared)) continue;
                    if (hiddenHere != hiddenThere)
                    {
                        PlanBoxWaysDown++;
                        var door = at(hiddenHere ? other : room, shared);
                        // On a ground of the stage's colour, so it shows on the room gone into's own amber too.
                        var ground = U(11f);
                        var marked = new Rect(door.x - ground / 2f, door.y - ground / 2f, ground, ground);
                        Skin.Icon(marked, Skin.Circle, Skin.Stage);
                        var mark = U(9f);
                        Skin.Icon(new Rect(door.x - mark / 2f, door.y - mark / 2f, mark, mark), Skin.Down, Skin.Accent);
                        WayDownMarks.Add((marked, hiddenHere ? room : other));
                        continue;
                    }
                    var onRoute = RouteWays.Contains((room, other));
                    var dot = U(onRoute ? 5f : 4f);
                    var here = at(room, shared);
                    var there = at(other, shared);
                    if (drops && (there - here).sqrMagnitude > 1f)
                    {
                        PlanLine(here, there, onRoute ? Skin.Accent : colour, onRoute ? U(2.5f) : width, U(3f));
                        Skin.Icon(new Rect(there.x - dot / 2f, there.y - dot / 2f, dot, dot), Skin.Circle, onRoute ? Skin.Accent : Skin.Text);
                    }
                    Skin.Icon(new Rect(here.x - dot / 2f, here.y - dot / 2f, dot, dot), Skin.Circle, onRoute ? Skin.Accent : Skin.Text);
                }
            }
            PlanBoxWaysDrawn = drawn;
        }

        /// <summary>How many ways down to rooms left out below the plan from above last marked, for the self-test.</summary>
        [Diagnostic]
        public static int PlanBoxWaysDown { get; private set; }

        /// <summary>
        /// The ways of the tree drawn square, as the plan's walkways run (<see cref="TreeElbow"/>):
        /// down from the higher node, across, and down into the lower; two of one row joined under
        /// it, over it on the last; those on the way from the entrance in the accent and bolder,
        /// drawn over the rest, loops in dashes.
        /// </summary>
        private static void TreeWays(Dictionary<PlacedRoom, Vector2> at, float rowGap, float half)
        {
            var drawn = 0;
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var room in BoxRooms)
                {
                    foreach (var other in _boxWays.WaysOf(room))
                    {
                        if (!BoxIndex.TryGetValue(other, out var index) || index <= BoxIndex[room]) continue;
                        var onRoute = RouteWays.Contains((room, other));
                        if (onRoute != (pass == 1) || !at.TryGetValue(room, out var a) || !at.TryGetValue(other, out var b)) continue;
                        drawn++;
                        var colour = onRoute ? Skin.Accent : Skin.Alpha(Skin.Text, 0.8f);
                        var width = U(onRoute ? 2.5f : 1.6f);
                        var dash = _boxWays.IsLoop(room, other) ? U(4f) : 0f;
                        var below = _treeRows.RowOf(room) < _treeRows.Rows - 1;
                        var points = TreeElbow.Points(a.x, a.y, b.x, b.y, half, rowGap, below);
                        for (var i = 1; i < points.Length; i++) PlanLine(new Vector2(points[i - 1].X, points[i - 1].Y), new Vector2(points[i].X, points[i].Y), colour, width, dash);
                    }
                }
            }
            PlanBoxWaysDrawn = drawn;
        }

        /// <summary>Grown, each room's name on its spot, where it does not run into a name already written or a label of the view's own (<see cref="ViewLabels"/>).</summary>
        private static void Names(Explorer explorer, Rect area)
        {
            var written = WrittenNames;
            written.Clear();
            written.AddRange(ViewLabels);
            for (var i = BoxSpots.Count - 1; i >= 0; i--)
            {
                var spot = BoxSpots[i];
                if (spot.Dashed) continue;
                var name = BoxRoomName(explorer, spot.Room);
                // On a dark backing a little wider than the name, so it reads on the light level plates as on the dark plan.
                var pad = U(3f);
                var nameW = Skin.Width(Skin.DimLabel, name) + pad * 2f;
                var rect = new Rect(spot.Centre.x - nameW / 2f, spot.Centre.y - U(8f), nameW, U(16f));
                if (rect.x < area.x || rect.xMax > area.xMax || rect.y < area.y || rect.yMax > area.yMax || written.Exists(r => r.Overlaps(rect))) continue;
                written.Add(rect);
                CountDrawn(PanelPart.PlanName);
                Skin.Fill(rect, Skin.Alpha(Skin.Stage, 0.85f));
                GUI.Label(new Rect(rect.x + pad, rect.y, rect.width - pad * 2f, rect.height), name, Skin.DimLabel);
            }
        }

        // ----- Levels -----

        /// <summary>The levels as last laid out (<see cref="PlanLevels"/>), again only as the example or what is read of it changes.</summary>
        private static PlanLevels _levels;
        private static DungeonWays _levelsOf;
        private static int _levelsRead = -1;

        /// <summary>The whole example's and each level's reach across and up on the plan turned with the view, and each level's depth as said, again only as the levels or the view's turn change.</summary>
        private static float _levelsYaw = float.NaN;
        private static PlanLevels _levelsSpanned;
        private static (float MinRight, float MaxRight, float MinUp, float MaxUp) _levelsWhole;
        private static readonly List<(float MinUp, float MaxUp)> LevelSpans = new List<(float, float)>();
        private static readonly List<string> LevelSaid = new List<string>();

        /// <summary>The labels a view writes of its own, as each level's depth, which room names keep clear of.</summary>
        private static readonly List<Rect> ViewLabels = new List<Rect>();

        /// <summary>The corners of a level's plate on the turned plan, kept between drawings.</summary>
        private static readonly Vector2[] PlateCorners = new Vector2[4];

        /// <summary>The plates and the rooms' tiles on them as last laid out, and what they were laid out for.</summary>
        private static LevelPlates _plates;
        private static PlanLevels _platesOf;
        private static Rect _platesArea;
        private static float _platesYaw = float.NaN;
        private static readonly List<BoxSpot> PlateSpots = new List<BoxSpot>();

        /// <summary>
        /// Each level a floor plate seen at a slant (<see cref="PlanLevels"/>, <see cref="LevelPlates"/>),
        /// the highest first, one under the other at one scale and turned with the view as Above
        /// is, so a spot over another on the next level stands straight above it; each room a tile
        /// on its plate shaded by its depth, the room gone into in the accent; each level's depth
        /// beside its plate; the ways lines through the doorways they share, a way down dropping
        /// straight from its doorway on the plate above to the same doorway on the plate below.
        /// </summary>
        private static void LevelsView(Explorer explorer, Rect area, PlacedRoom gone)
        {
            var repaint = Event.current.type == EventType.Repaint;
            if (_levels == null || !ReferenceEquals(_levelsOf, _boxWays) || _levelsRead != _boxRead)
            {
                _levelsOf = _boxWays;
                _levelsRead = _boxRead;
                _levels = PlanLevels.Of(BoxRooms, BoxGrounds);
            }
            var levels = _levels.Levels;
            var yaw = Stage.ExampleViewYaw;
            if (!ReferenceEquals(_levelsSpanned, _levels) || !Mathf.Approximately(_levelsYaw, yaw))
            {
                _levelsSpanned = _levels;
                _levelsYaw = yaw;
                _levelsWhole = ExamplePlan.Extent(BoxRooms, yaw);
                LevelSpans.Clear();
                LevelSaid.Clear();
                var entranceGround = _boxWays.Entrance != null ? GroundOf(_boxWays.Entrance) : 0f;
                foreach (var level in levels)
                {
                    var span = ExamplePlan.Extent(level.Rooms, yaw);
                    LevelSpans.Add((span.MinUp, span.MaxUp));
                    LevelSaid.Add(PlanBoxWords.LevelDepth(level.Top - entranceGround));
                }
            }
            var labelW = 0f;
            foreach (var said in LevelSaid) labelW = Mathf.Max(labelW, Skin.Width(Skin.FaintLabel, said));
            labelW += U(8f);
            var inset = U(3f);
            // The plates and the rooms' tiles on them, again only as the levels, the turn or the box change.
            var fresh = !ReferenceEquals(_platesOf, _levels) || _platesArea != area || !Mathf.Approximately(_platesYaw, yaw) || _plates == null;
            if (fresh)
            {
                _platesOf = _levels;
                _platesArea = area;
                _platesYaw = yaw;
                _plates = LevelPlates.Lay(area.x + labelW, area.y + inset, area.width - labelW - inset, area.height - inset * 2f, U(10f), _levelsWhole, LevelSpans);
            }
            var plates = _plates;
            Vector2 OnPlate(int level, float right, float up)
            {
                var (x, y) = plates.At(level, right, up);
                return new Vector2(x, y);
            }
            Vector2 At(PlacedRoom room, Vec3 p)
            {
                var (right, up) = ExamplePlan.Turn(p.X, p.Z, yaw);
                return OnPlate(Mathf.Max(0, _levels.LevelOf(room)), right, up);
            }
            if (fresh)
            {
                PlateSpots.Clear();
                foreach (var room in BoxRooms)
                {
                    if (_levels.LevelOf(room) < 0) continue;
                    var corners = room.Corners();
                    var quad = new Vector2[4];
                    for (var i = 0; i < 4; i++) quad[i] = At(room, corners[i % corners.Length]);
                    var min = Vector2.Min(Vector2.Min(quad[0], quad[1]), Vector2.Min(quad[2], quad[3]));
                    var max = Vector2.Max(Vector2.Max(quad[0], quad[1]), Vector2.Max(quad[2], quad[3]));
                    PlateSpots.Add(new BoxSpot { Room = room, Centre = At(room, room.Position), Half = (max - min) / 2f, Quad = quad });
                }
            }
            BoxSpots.AddRange(PlateSpots);
            if (!repaint) return;

            // Each plate, faintly, its depth beside it.
            for (var i = 0; i < levels.Count; i++)
            {
                var (minUp, maxUp) = LevelSpans[i];
                PlateCorners[0] = OnPlate(i, _levelsWhole.MinRight, maxUp);
                PlateCorners[1] = OnPlate(i, _levelsWhole.MaxRight, maxUp);
                PlateCorners[2] = OnPlate(i, _levelsWhole.MaxRight, minUp);
                PlateCorners[3] = OnPlate(i, _levelsWhole.MinRight, minUp);
                QuadFill(PlateCorners, Skin.Alpha(Skin.Text, 0.04f));
                for (var c = 0; c < 4; c++) PlanLine(PlateCorners[c], PlateCorners[(c + 1) % 4], Skin.Alpha(Skin.Text, 0.12f), 1f);
                var saidW = Skin.Width(Skin.FaintLabel, LevelSaid[i]);
                var middleY = (plates.PlateTop(i) + plates.PlateBottom(i)) / 2f;
                var leftX = Mathf.Min(PlateCorners[0].x, PlateCorners[3].x);
                var said = new Rect(leftX - U(4f) - saidW, middleY - U(8f), saidW, U(16f));
                ViewLabels.Add(said);
                GUI.Label(said, LevelSaid[i], Skin.FaintLabel);
            }
            foreach (var spot in BoxSpots) RoomTile(spot, gone);
            Walkways(At, null, drops: true);
            foreach (var spot in BoxSpots) if (BoxOutlined(spot.Room, gone)) BoxTile(spot, null, Skin.Accent, 2f);
            YouAndWays(At);
            if (_planBig && PlanNamesShown) Names(explorer, area);
        }

        /// <summary>A four-sided shape filled, its sides two by two alike, as a tile seen at a slant is: from its first corner along the next and the last.</summary>
        private static void QuadFill(Vector2[] quad, Color colour)
        {
            const float side = 100f;
            var along = (quad[1] - quad[0]) / side;
            var across = (quad[3] - quad[0]) / side;
            // Laid about its first corner where the groups the panel draws in put it, as the GUI's own
            // turn about a point is, since the GUI's transform works on where things stand in them.
            var was = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            var corner = GUIUtility.GUIToScreenPoint(quad[0]);
            var shape = Matrix4x4.identity;
            shape.m00 = along.x;
            shape.m10 = along.y;
            shape.m01 = across.x;
            shape.m11 = across.y;
            shape.m03 = corner.x - (along.x * corner.x + across.x * corner.y);
            shape.m13 = corner.y - (along.y * corner.x + across.y * corner.y);
            GUI.matrix = shape * was;
            Skin.Fill(new Rect(quad[0].x, quad[0].y, side, side), colour);
            GUI.matrix = was;
        }

        // ----- Tree -----

        /// <summary>
        /// The tree of the ways (<see cref="TreeRows"/>): a row for each room more in from the
        /// entrance, the entrance at the top, each room hanging under the room it is first reached
        /// from, standing over the middle of its own, the same whichever way the view turns; the
        /// ways lines between the nodes, loops in dashes, crossroads outlined, and each node's depth
        /// beside it, and grown, its name.
        /// </summary>
        private static void TreeView(Explorer explorer, Rect area, PlacedRoom gone)
        {
            var repaint = Event.current.type == EventType.Repaint;
            if (_treeRows == null || !ReferenceEquals(_treeWays, _boxWays) || _treeRead != _boxRead)
            {
                _treeWays = _boxWays;
                _treeRead = _boxRead;
                _treeRows = TreeRows.Of(_boxWays);
            }
            var node = new Vector2(U(18f), U(12f));
            var inner = new Rect(area.x + node.x, area.y + node.y, area.width - node.x * 2f, area.height - node.y * 2f);
            _treeInnerWidth = inner.width;
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

            TreeWays(at, rowGap > 0f ? rowGap : node.y * 2f, node.y / 2f);
            TreeMarksForTest.Clear();
            foreach (var spot in BoxSpots)
            {
                var rect = new Rect(spot.Centre - spot.Half, spot.Half * 2f);
                var crossroads = _boxWays.IsCrossroads(spot.Room);
                var lit = BoxOutlined(spot.Room, gone);
                var peeked = ExampleLayouts.IsPeeked(spot.Room);
                var fill = spot.Room == gone ? Skin.Alpha(Skin.Accent, 0.75f) : DepthTone(spot.Room);
                Skin.Fill(rect, peeked ? Skin.Alpha(fill, fill.a * PeekedShare) : fill);
                if (peeked && !lit) PlanDashes(rect, Skin.Alpha(Skin.Faint, 0.6f));
                else PlanFrame(rect, lit ? Skin.Accent : crossroads ? Skin.Alpha(Skin.Text, 0.85f) : Skin.Alpha(Skin.Text, 0.3f), lit || crossroads ? 2f : 1f);
                // A dead end shut at its foot, a heavier edge along its bottom, so nothing under it is covered.
                if (_boxWays.IsDeadEnd(spot.Room)) Skin.Fill(new Rect(rect.x, rect.yMax - U(2f), rect.width, U(2f)), Skin.Alpha(Skin.Text, 0.75f));
            }
            TreeYouAndWays(at, node.y / 2f);

            // Each node's marks beside it, the room gone into's and those lit first: right of it, else
            // left, where it covers no other label or node; else left out, the caption naming the
            // room under the mouse.
            WrittenNames.Clear();
            foreach (var spot in BoxSpots) if (spot.Room == gone || BoxLit(spot.Room)) TreeLabel(explorer, spot, area);
            foreach (var spot in BoxSpots) if (spot.Room != gone && !BoxLit(spot.Room)) TreeLabel(explorer, spot, area);
        }

        /// <summary>What the tree marked of each room as it was last drawn: its word beside it (the entrance, the deepest), a dot for what this roll left in it, and whether it is a dead end, for the self-test.</summary>
        [Diagnostic]
        public static List<(PlacedRoom Room, string Mark, bool Loot, bool DeadEnd)> TreeMarksForTest { get; } = new List<(PlacedRoom, string, bool, bool)>();

        /// <summary>
        /// A node's marks, only what stands out: beside it the entrance and the deepest named, and a
        /// dot where this roll left chests or pickups in it; grown, its room's name; each where it
        /// covers nothing already drawn.
        /// </summary>
        private static void TreeLabel(Explorer explorer, BoxSpot spot, Rect area)
        {
            bool Clear(Rect r)
            {
                if (r.x < area.x || r.xMax > area.xMax || r.y < area.y - U(8f) || r.yMax > area.yMax + U(8f)) return false;
                var spaced = new Rect(r.x - U(3f), r.y, r.width + U(6f), r.height);
                foreach (var written in WrittenNames) if (written.Overlaps(spaced)) return false;
                foreach (var other in BoxSpots) if (new Rect(other.Centre - other.Half, other.Half * 2f).Overlaps(r)) return false;
                return true;
            }

            // Beside it, right of it else left: the entrance or the deepest named, and a dot where this
            // roll left chests or pickups.
            var mark = spot.Room == _boxWays.Entrance ? PlanBoxWords.Entrance : spot.Room == _boxWays.Deepest ? PlanBoxWords.Deepest : null;
            var loot = Stage.RoomLoot(spot.Room).Count > 0;
            TreeMarksForTest.Add((spot.Room, mark, loot, _boxWays.IsDeadEnd(spot.Room)));
            var markW = mark != null ? Skin.Width(Skin.FaintLabel, mark) : 0f;
            var dot = U(6f);
            var besideW = markW + (loot ? (mark != null ? U(4f) : 0f) + dot : 0f);
            Rect? beside = null;
            if (besideW > 0f)
            {
                var right = new Rect(spot.Centre.x + spot.Half.x + U(3f), spot.Centre.y - U(8f), besideW, U(16f));
                var left = new Rect(spot.Centre.x - spot.Half.x - U(3f) - besideW, right.y, besideW, U(16f));
                beside = Clear(right) ? right : Clear(left) ? left : (Rect?)null;
                if (beside is Rect at)
                {
                    if (mark != null) Skin.LabelIn(new Rect(at.x, at.y, markW, at.height), mark, Skin.FaintLabel, Skin.Accent);
                    if (loot) Skin.Icon(new Rect(at.xMax - dot, at.center.y - dot / 2f, dot, dot), Skin.Circle, Skin.Accent);
                }
            }

            // Grown, its name under it, or over it for every other node of its row, as wide as twice
            // the room it has in its row, its type made smaller to fit; left out where it still does not.
            // Its own marks, level with the node, are no label for the name to keep clear of, so they are
            // written down only once the name has its place.
            if (_planBig && _treeRows != null && PlanNamesShown)
            {
                var place = _treeRows.RoomInRow(spot.Room) * _treeInnerWidth;
                var order = _treeRows.OrderInRow(spot.Room);
                var name = BoxRoomName(explorer, spot.Room);
                var nameW = Skin.Width(Skin.DimLabel, name) + U(6f);
                var room = Mathf.Min(nameW, place * 2f - U(4f));
                var smallest = Mathf.Max(1, Mathf.RoundToInt(10f * _s)) / (float)Mathf.Max(1, Skin.DimLabel.fontSize);
                var over = order % 2 == 1;
                var under = new Rect(spot.Centre.x - room / 2f, over ? spot.Centre.y - spot.Half.y - U(15f) : spot.Centre.y + spot.Half.y + U(1f), room, U(14f));
                if (nameW * smallest <= room && Clear(under))
                {
                    WrittenNames.Add(under);
                    CountDrawn(PanelPart.PlanName);
                    // On the dark backing the other views' names stand on, so the tree's lines do not run through it.
                    Skin.Fill(under, Skin.Alpha(Skin.Stage, 0.85f));
                    FitLabel(new Rect(under.x + U(3f), under.y, under.width - U(6f), under.height), name, Skin.DimLabel, 10f);
                }
            }
            if (beside is Rect taken) WrittenNames.Add(taken);
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
            RouteWalk.Clear();
            _walkways = null;
            _walkwaysOf = null;
            _treeRows = null;
            _boxWays = null;
            _treeWays = null;
            _routeOf = null;
            _routeTo = null;
            _levels = null;
            _levelsOf = null;
            _levelsRead = -1;
            _plates = null;
            _platesOf = null;
            PlateSpots.Clear();
            _boxHover = null;
            _boxLit = null;
            _boxPointForTest = null;
            _boxRead = _boxShown = _treeRead = -1;
        }
    }
}
