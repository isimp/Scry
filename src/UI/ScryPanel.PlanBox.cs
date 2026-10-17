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
        private static readonly PlanView[] ViewsRightToLeft = { PlanView.Tree, PlanView.Levels, PlanView.Above };

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

        /// <summary>Where the box's row of Entrance and Deepest last ended, for the self-test to tell it stays inside the box; none without the row.</summary>
        [Diagnostic]
        public static float PlanBoxRowRight { get; private set; }

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
            var tabW = Skin.Width(Skin.Chip, "Plan") + U(4f);
            var tab = new Rect(inner.x + U(8f), inner.yMax - U(CornerFit.Bottom) - U(22f), tabW, U(22f));
            CountDrawn(PanelPart.PlanTab);
            if (byPlayer)
            {
                if (tab.Contains(e.mousePosition)) AskTip("plan:tab", "Shows the example's plan");
                if (GUI.Button(tab, "Plan", Skin.Fitted(Skin.Chip, tab))) PlanFolded = false;
            }
            else
            {
                if (tab.Contains(e.mousePosition)) AskTip("plan:short", PlanBoxWords.TooShortTip);
                var enabled = GUI.enabled;
                GUI.enabled = false;
                GUI.Button(tab, "Plan", Skin.Fitted(Skin.Chip, tab));
                GUI.enabled = enabled;
            }
            // Folded, the way back out of a room gone into stands beside the tab.
            if (Stage.RoomGoneInto == null || !Stage.HasInside || !Stage.ShowsInside) return tab;
            var whole = new Rect(tab.xMax + U(4f), tab.y, tab.height, tab.height);
            WholeButton(whole);
            return Rect.MinMaxRect(tab.x, tab.y, whole.xMax, tab.yMax);
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

        /// <summary>What the box's caption said the last time it was drawn, for the self-test.</summary>
        [Diagnostic]
        public static string PlanBoxCaptionShown { get; private set; }

        /// <summary>The room the way from the entrance was last drawn to, for the self-test.</summary>
        [Diagnostic]
        public static PlacedRoom PlanBoxRouteShown => _routeTo;

        /// <summary>A room's name as the box gives it, for the self-test.</summary>
        [Diagnostic]
        public static string PlanBoxRoomNameForTest(Explorer explorer, PlacedRoom room) => BoxRoomName(explorer, room);

        /// <summary>Clicks a room in the box as the mouse does, for the self-test.</summary>
        [Diagnostic]
        public static void ClickPlanRoomForTest(PlacedRoom room) => Stage.GoInto(room);

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

        /// <summary>Comes back out of the room gone into, as the box's Whole dungeon does, for the self-test.</summary>
        [Diagnostic]
        public static void ClickPlanWholeForTest() => Stage.ComeOut();

        /// <summary>Whether the box drew its Whole dungeon last, in whatever shape it stood, for the self-test.</summary>
        [Diagnostic]
        public static bool PlanWholeShown { get; private set; }

        /// <summary>The way back out to the whole dungeon, under the plan or beside its tab, while a room is gone into.</summary>
        private static void WholeButton(Rect at)
        {
            var repaint = Event.current.type == EventType.Repaint;
            if (repaint) PlanWholeShown = true;
            if (at.Contains(Event.current.mousePosition)) AskTip("plan:whole", PlanBoxWords.WholeTip);
            if (GUI.Button(at, GUIContent.none, Skin.IconButton)) Stage.ComeOut();
            if (repaint) Skin.Icon(new Rect(at.x + U(4f), at.y + U(4f), at.width - U(8f), at.height - U(8f)), Skin.Up, Skin.Text);
        }

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
            _boxLit = RoomLight.Lit(_cardLit, _boxHover, _stageHover);
            _boxHover = null;
            if (repaint)
            {
                PlanWholeShown = false;
                PlanBoxRowRight = 0f;
            }
            if (!KnowBoxRooms() || BoxRooms.Count == 0) return PlanBoxRect = Rect.zero;

            // Folded, a small tab where it stands, which brings it back.
            if (PlanFolded) return PlanBoxRect = PlanTab(inner, byPlayer: true);

            var pad = U(6f);
            var rowH = U(22f);
            var gap = U(4f);
            var width = Mathf.Min(U(_planBig ? 400f : 236f), inner.width - U(16f));
            var planH = U(_planBig ? 330f : 150f);
            // The header holds the three views, grow and fold, and the caption beside them where
            // it has room; else the caption shares a second row with grow and fold.
            var segmentsW = 0f;
            foreach (var view in ViewsRightToLeft) segmentsW += Skin.Width(Skin.SegmentOn, PlanBoxWords.ViewName(view)) + U(12f);
            var controlsW = U(6f) + rowH + U(4f) + rowH + U(4f) + rowH + U(4f) + U(16f);
            var oneRow = width - pad * 2f >= segmentsW + controlsW + U(90f);
            var headerH = oneRow ? rowH : rowH * 2f + U(2f);
            // Whole, with its trail and its Entrance and Deepest, where the stage holds it so; else
            // without them; else, with a room gone into, folded to its tab (CornerFit).
            var card = Stage.RoomGoneInto != null && Stage.HasInside && Stage.ShowsInside;
            // Lean, a room gone into keeps the way back out under the plan.
            var leanRows = pad + headerH + gap + pad + (card ? gap + rowH : 0f);
            var wholeRows = pad + headerH + gap + pad + gap + rowH + gap + rowH;
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

            // The header: the three views, the names, grow and fold, and the caption.
            var controlsY = oneRow ? y : y + rowH + U(2f);
            var fold = new Rect(box.xMax - pad - U(16f), controlsY + U(3f), U(16f), U(16f));
            var grow = new Rect(fold.x - U(4f) - rowH, controlsY, rowH, rowH);
            var names = new Rect(grow.x - U(4f) - rowH, controlsY, rowH, rowH);
            // The switch for the rooms below acts on the plan from above, and stands only with it.
            var switchShown = _planView == PlanView.Above;
            var below = switchShown ? new Rect(names.x - U(4f) - rowH, controlsY, rowH, rowH) : new Rect(names.x - U(4f), controlsY, 0f, rowH);
            var segX = oneRow ? below.x - U(6f) : x + segmentsW;
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
            // The rooms below the room gone into or the floor opened, shown in the plan from above or left out of it.
            if (switchShown)
            {
                var belowShown = PlanBelowShown;
                if (below.Contains(e.mousePosition)) AskTip("plan:below", PlanBoxWords.BelowTip(belowShown));
                if (GUI.Button(below, GUIContent.none, belowShown ? Skin.On : Skin.IconButton)) ShowBelow(!belowShown);
                if (repaint) Skin.Icon(new Rect(below.x + U(4f), below.y + U(4f), below.width - U(8f), below.height - U(8f)), Skin.Down, belowShown ? Skin.Accent : Skin.Dim);
            }
            // The rooms' names on the grown plan, in every view.
            var namesShown = PlanNamesShown;
            if (names.Contains(e.mousePosition)) AskTip("plan:names", PlanBoxWords.NamesTip(namesShown));
            if (GUI.Button(names, PlanBoxWords.NamesMark, namesShown ? Skin.On : Skin.IconButton)) ShowNames(!namesShown);
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

            // The room under the mouse: lit here and on the stage, gone into on a click.
            var mouse = BoxMouse(e);
            var hovered = area.Contains(mouse) && (_drag == Drag.None || _drag == Drag.Orbit) ? BoxRoomAt(mouse) : null;
            if (hovered != null)
            {
                _boxHover = hovered;
                AskTip("planbox:" + hovered.Room.Name, StageWords.GoIntoTip(BoxRoomName(explorer, hovered), hovered == gone));
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    Stage.GoInto(hovered);
                    e.Use();
                }
            }
            var caption = _boxLit != null ? BoxRoomName(explorer, _boxLit) : gone != null ? BoxRoomName(explorer, gone) : PlanBoxWords.Caption(BoxRooms.Count);
            if (repaint) PlanBoxCaptionShown = caption;
            var captionRect = oneRow ? new Rect(x, y, Mathf.Max(U(20f), segX - x - U(4f)), rowH) : new Rect(x, controlsY, Mathf.Max(U(20f), below.x - U(6f) - x), rowH);
            FitLabel(captionRect, caption, Skin.FaintLabel, 10f);
            y = area.yMax + gap;

            if (shape == BoxShape.Whole)
            {
                // The way from the entrance to the room gone into, each name going into its room.
                Trail(explorer, new Rect(x, y, w, rowH), route, gone != null);
                y += rowH + gap;

                // The entrance and the deepest room, a click away.
                var entrance = new Rect(x, y, Skin.Width(Skin.Segment, PlanBoxWords.Entrance) + U(12f), rowH);
                var deepest = new Rect(entrance.xMax + U(4f), y, Skin.Width(Skin.Segment, PlanBoxWords.Deepest) + U(12f), rowH);
                if (repaint) PlanBoxRowRight = deepest.xMax;
                if (entrance.Contains(e.mousePosition)) AskTip("plan:entrance", PlanBoxWords.EntranceTip);
                if (deepest.Contains(e.mousePosition)) AskTip("plan:deepest", PlanBoxWords.DeepestTip);
                if (GUI.Button(entrance, PlanBoxWords.Entrance, Skin.Fitted(Skin.Segment, entrance)) && _boxWays?.Entrance != null) Stage.GoInto(_boxWays.Entrance);
                if (GUI.Button(deepest, PlanBoxWords.Deepest, Skin.Fitted(Skin.Segment, deepest)) && _boxWays?.Deepest != null) Stage.GoInto(_boxWays.Deepest);
            }
            else if (gone != null) WholeButton(new Rect(x, y, rowH, rowH));

            // The box is not the stage: a click or the wheel on it neither turns nor zooms it.
            if (box.Contains(e.mousePosition) && (e.type == EventType.MouseDown || e.type == EventType.ScrollWheel)) e.Use();
            return box;
        }

        private static string BoxRoomName(Explorer explorer, PlacedRoom room) =>
            ShownName(explorer, EntryKeys.For(Kind.Location, room.Room.Name), LocationWords.RoomName(room.Room.Name));

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
            if (!repaint) return;
            foreach (var spot in BoxSpots) if (!spot.Dashed) BoxTile(spot, spot.Room == gone ? Skin.Alpha(Skin.Accent, 0.75f) : DepthTone(spot.Room), Skin.Alpha(Skin.Text, 0.3f), 1f);
            foreach (var spot in BoxSpots) if (spot.Dashed) BoxTile(spot, null, Skin.Alpha(Skin.Faint, 0.6f), 1f, dashed: true);
            Walkways((room, p) => At(p), hideBelow, drops: false);
            foreach (var spot in BoxSpots) if (BoxLit(spot.Room) || spot.Room == gone) BoxTile(spot, null, Skin.Accent, 2f);
            if (_planBig && PlanNamesShown) Names(explorer, area);
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
                var nameW = Skin.Width(Skin.FaintLabel, name);
                var rect = new Rect(spot.Centre.x - nameW / 2f, spot.Centre.y - U(8f), nameW, U(16f));
                if (rect.x < area.x || rect.xMax > area.xMax || rect.y < area.y || rect.yMax > area.yMax || written.Exists(r => r.Overlaps(rect))) continue;
                written.Add(rect);
                CountDrawn(PanelPart.PlanName);
                Skin.Fill(rect, Skin.Alpha(Skin.Stage, 0.6f));
                GUI.Label(rect, name, Skin.FaintLabel);
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
            foreach (var spot in BoxSpots) BoxTile(spot, spot.Room == gone ? Skin.Alpha(Skin.Accent, 0.75f) : DepthTone(spot.Room), Skin.Alpha(Skin.Text, 0.3f), 1f);
            Walkways(At, null, drops: true);
            foreach (var spot in BoxSpots) if (BoxLit(spot.Room) || spot.Room == gone) BoxTile(spot, null, Skin.Accent, 2f);
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
            bool Clear(Rect r)
            {
                if (r.x < area.x || r.xMax > area.xMax || r.y < area.y - U(8f) || r.yMax > area.yMax + U(8f)) return false;
                var spaced = new Rect(r.x - U(3f), r.y, r.width + U(6f), r.height);
                foreach (var written in WrittenNames) if (written.Overlaps(spaced)) return false;
                foreach (var other in BoxSpots) if (new Rect(other.Centre - other.Half, other.Half * 2f).Overlaps(r)) return false;
                return true;
            }

            // Its depth beside it, right of it else left.
            var depth = PlanBoxWords.Depth(GroundOf(spot.Room) - entranceGround);
            var depthW = Skin.Width(Skin.FaintLabel, depth);
            var right = new Rect(spot.Centre.x + spot.Half.x + U(3f), spot.Centre.y - U(8f), depthW, U(16f));
            var left = new Rect(spot.Centre.x - spot.Half.x - U(3f) - depthW, right.y, depthW, U(16f));
            if ((Clear(right) ? right : Clear(left) ? left : (Rect?)null) is Rect beside)
            {
                WrittenNames.Add(beside);
                GUI.Label(beside, depth, Skin.FaintLabel);
            }
            if (!_planBig || _treeRows == null || !PlanNamesShown) return;

            // Grown, its name under it, or over it for every other node of its row, as wide as twice
            // the room it has in its row, its type made smaller to fit; left out where it still does not.
            var place = _treeRows.RoomInRow(spot.Room) * _treeInnerWidth;
            var order = _treeRows.OrderInRow(spot.Room);
            var name = BoxRoomName(explorer, spot.Room);
            var nameW = Skin.Width(Skin.FaintLabel, name);
            var room = Mathf.Min(nameW, place * 2f - U(4f));
            var smallest = Mathf.Max(1, Mathf.RoundToInt(10f * _s)) / (float)Mathf.Max(1, Skin.FaintLabel.fontSize);
            if (nameW * smallest > room) return;
            var over = order % 2 == 1;
            var under = new Rect(spot.Centre.x - room / 2f, over ? spot.Centre.y - spot.Half.y - U(15f) : spot.Centre.y + spot.Half.y + U(1f), room, U(14f));
            if (!Clear(under)) return;
            WrittenNames.Add(under);
            CountDrawn(PanelPart.PlanName);
            FitLabel(under, name, Skin.FaintLabel, 10f);
        }

        // ----- The trail -----

        /// <summary>
        /// The way from the entrance to the room gone into, its names going into their rooms; as many
        /// of the last as fit, the last shrunk to fit where it must. In a room it begins with the
        /// way back out, a button of its own, so it stands under the plan however narrow the box.
        /// </summary>
        private static void Trail(Explorer explorer, Rect line, List<PlacedRoom> route, bool fromWhole)
        {
            var e = Event.current;
            var stepW = Skin.Width(Skin.FaintLabel, PlanBoxWords.TrailStep);
            if (fromWhole)
            {
                var whole = new Rect(line.x, line.y, line.height, line.height);
                WholeButton(whole);
                GUI.Label(new Rect(whole.xMax + U(2f), line.y, stepW, line.height), PlanBoxWords.TrailStep, Skin.FaintLabel);
                line = new Rect(whole.xMax + U(2f) + stepW, line.y, Mathf.Max(0f, line.xMax - whole.xMax - U(2f) - stepW), line.height);
            }
            if (route == null || route.Count == 0)
            {
                if (!fromWhole) FitLabel(line, PlanBoxWords.NoTrail, Skin.FaintLabel, 10f);
                return;
            }
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
                var nameW = Mathf.Min(Skin.Width(Skin.Small, name), line.xMax - x);
                var rect = new Rect(x, line.y, nameW, line.height);
                // The last, alone where even it is too wide, shrunk to fit and cut at the box's edge.
                var size = Skin.Small.fontSize;
                var clipping = Skin.Small.clipping;
                var floor = Mathf.Max(1, Mathf.RoundToInt(9f * _s));
                while (Skin.Width(Skin.Small, name) > rect.width && Skin.Small.fontSize > floor) Skin.Small.fontSize -= 1;
                Skin.Small.clipping = TextClipping.Clip;
                LinkLabel(rect, name, Skin.Small, i == route.Count - 1 ? Skin.Accent : Skin.Text);
                Skin.Small.fontSize = size;
                Skin.Small.clipping = clipping;
                if (rect.Contains(e.mousePosition))
                {
                    _boxHover = route[i];
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
