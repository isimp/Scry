using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The plan box's two other views of a dungeon's example: its levels, each floor a plate
    /// stacked under the last and turned as the stage is, and its tree, the rooms in rows by how
    /// many ways they lie from the entrance; each with its rooms' names and marks.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>The tree as last laid out (<see cref="TreeRows"/>), kept until the rooms or the view's turn change.</summary>
        private static TreeRows _treeRows;

        /// <summary>How many rooms each row of the tree holds, and how wide its rows were last drawn, for its names to fit.</summary>
        private static float _treeInnerWidth;
        private static int _treeRead = -1;
        private static DungeonWays _treeWays;

        /// <summary>The names the tree has written, which the next keeps clear of.</summary>
        private static readonly List<Rect> WrittenNames = new List<Rect>();

        /// <summary>The views laid out again at their next drawing, as the rooms read changed.</summary>
        private static void ViewsStale()
        {
            _treeRead = -1;
            _levelsRead = -1;
        }

        /// <summary>Lets go of everything the views laid out, as on leaving the stage.</summary>
        private static void ForgetViews()
        {
            _treeRows = null;
            _treeWays = null;
            _treeRead = -1;
            _levels = null;
            _levelsOf = null;
            _levelsRead = -1;
            _plates = null;
            _platesOf = null;
            PlateSpots.Clear();
        }

        /// <summary>No label of a view's written yet, before a view draws.</summary>
        private static void ClearViewLabels() => ViewLabels.Clear();

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
            AddSpots(PlateSpots);
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
                AddSpot(new BoxSpot { Room = room, Centre = spot, Half = node / 2f });
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
    }
}
