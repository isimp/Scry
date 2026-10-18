using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The example layout of a location's dungeon or camp (<see cref="ExampleLayouts"/>): what it
    /// holds and a button for another under the details, and its plan in a corner of the stage,
    /// as seen from above, turned with the view and framed on its rooms (<see cref="ExamplePlan"/>):
    /// each room a tile of one tone, overlaps not darker, its outline over every tile, the
    /// entrance marked, and the doors; with a floor opened, that floor named over it, only its
    /// rooms tiled, those of the floor under it outlined in dashes, and a door leading up or down
    /// to another floor marked so. A room under the mouse, on the plan or on the stage, is lit on
    /// the plan and named, and a click on it goes to its entry. The stage's *Plan* chip puts the
    /// plan away and back.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>The example drawn, with its rooms from the lowest up and how wide they are any way round, worked out once for it rather than every frame.</summary>
        private static DungeonExample _planOf;
        private static float _planWidest;
        private static readonly List<PlacedRoom> PlanRooms = new List<PlacedRoom>();

        /// <summary>How each room shows and where each door leads with the floor opened, worked out again only as the example, the floor or the rooms read change.</summary>
        private static readonly Dictionary<PlacedRoom, PlanTile> PlanTiles = new Dictionary<PlacedRoom, PlanTile>();
        private static readonly List<DoorLeads> PlanDoors = new List<DoorLeads>();
        private static DungeonExample _marksOf;
        private static int _marksLevel = -1;
        private static int _marksRead = -1;
        private static float _marksFloors = float.NaN;

        /// <summary>Whether a room has a floor of its own at a height, as the plan tiles it, made once.</summary>
        private static readonly Func<PlacedRoom, float, bool> HasFloor = Stage.ExampleRoomHasFloorAt;

        /// <summary>How many rooms the plan tiles on the floor opened, end caps and dividers left out; for the self-test.</summary>
        public static int PlanTilesOn { get; private set; }

        /// <summary>How many of the example's doors the plan marks as leading up or down from the floor opened; for the self-test.</summary>
        public static int PlanDoorsMarked { get; private set; }

        /// <summary>How many rooms the plan outlines in dashes, standing on the floor under the one opened; for the self-test.</summary>
        public static int PlanTilesBelow { get; private set; }

        /// <summary>Whether the plan was drawn for an example with a floor opened (its count from the top, the floors' count with the roof on), for the self-test to wait on what it worked out.</summary>
        public static bool PlanDrawnFor(DungeonExample example, int level) => example != null && ReferenceEquals(example, _marksOf) && level == _marksLevel;

        /// <summary>Whether the plan tiles a room with the floor last drawn opened, for the self-test.</summary>
        public static bool PlanTilesRoom(PlacedRoom room) => OnPlan(room);

        /// <summary>Whether the plan is put away, by the fold on it, and brought back by its tab; remembered.</summary>
        public static bool PlanFolded
        {
            get => IsFolded("plan");
            set => SetFolded("plan", value);
        }

        /// <summary>The example shown for the entry, or null.</summary>
        private static DungeonExample ExampleOf(Entry entry) =>
            entry?.Source is PlaceSource place && !place.IsRoom && place.Contents?.Dungeon != null && ExampleLayouts.Of(entry) ? ExampleLayouts.Example : null;

        /// <summary>
        /// The plan in the stage's lower left corner, over the stage's picture. Drawn whole where
        /// the stage is, which does not scroll, so its turned rooms stay inside its box.
        /// </summary>
        private static Rect PlanOverlay(Explorer explorer, Entry entry, Rect inner)
        {
            // The example as the stage holds it, so the plan turns as the stage is looked at; a
            // dungeon's plan is of its inside, so none while its entrance is shown from outside.
            var example = ExampleOf(entry) != null ? Stage.ExampleShown : null;
            if (example == null || example.Rooms.Count == 0 || Stage.HasInside && !Stage.ShowsInside || Stage.InsideUnknown) return Rect.zero;
            var e = Event.current;

            // Folded, a small tab where it stands, which brings it back.
            if (PlanFolded)
            {
                var tabW = Skin.Width(Skin.Chip, PlanBoxWords.Tab) + U(4f);
                var tab = new Rect(inner.x + U(8f), inner.yMax - U(34f) - U(22f), tabW, U(22f));
                if (tab.Contains(e.mousePosition)) AskTip("plan:tab", PlanBoxWords.TabTip);
                CountDrawn(PanelPart.PlanTab);
                if (GUI.Button(tab, PlanBoxWords.Tab, Skin.Fitted(Skin.Chip, tab))) PlanFolded = false;
                return tab;
            }

            if (_planOf != example)
            {
                _planOf = example;
                PlanRooms.Clear();
                PlanRooms.AddRange(example.BottomUp);
                _planWidest = Mathf.Max(1f, ExamplePlan.Widest(PlanRooms));
            }

            // At one scale whichever way it is turned: as wide as a third of the stage and no
            // taller than under half of it, its box hugging its rooms as they are turned now.
            var yaw = Stage.ExampleViewYaw;
            var floor = Stage.ExamplePlanFloor;
            var floors = Stage.ExampleFloors;
            var level = floor != null ? Stage.CutLevel : floors.Count;
            PlanMarks(example, floors, level);
            var extent = ExamplePlan.Extent(PlanRooms, yaw);
            var pad = U(6f);
            var scale = (Mathf.Min(Mathf.Min(inner.width * 0.34f, U(240f)), inner.height * 0.5f) - pad * 2f) / _planWidest;
            if (scale <= 0f) return Rect.zero;
            // The floor opened named over the rooms, as beside the ruler.
            var named = floor != null ? PlaceView.FloorLabel(level, floors.Count, Stage.ExampleRoomsOnFloor) : null;
            var line = named != null ? U(18f) : 0f;
            var size = new Vector2((extent.MaxRight - extent.MinRight) * scale + pad * 2f, (extent.MaxUp - extent.MinUp) * scale + pad * 2f + line);
            if (named != null) size.x = Mathf.Max(size.x, Skin.Width(Skin.FaintLabel, named) + pad * 2f + U(20f));
            // Above the stage's line of hints along its bottom.
            var area = new Rect(inner.x + U(8f), inner.yMax - U(34f) - size.y, size.x, size.y);
            Vector2 At(Vec3 p)
            {
                var (right, up) = ExamplePlan.Turn(p.X, p.Z, yaw);
                return new Vector2(area.x + pad + (right - extent.MinRight) * scale, area.y + pad + line + (extent.MaxUp - up) * scale);
            }

            Skin.Box(area, Skin.Alpha(Skin.Stage, 0.82f), Skin.Outline);

            // Its fold, in its top right corner.
            var fold = new Rect(area.xMax - U(18f), area.y + U(2f), U(16f), U(16f));
            if (fold.Contains(e.mousePosition))
            {
                AskTip("plan:fold", PlanBoxWords.FoldTip);
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    PlanFolded = true;
                    e.Use();
                    return area;
                }
            }
            if (e.type == EventType.Repaint) GUI.Label(fold, "\u2013", fold.Contains(e.mousePosition) ? Skin.Label : Skin.DimLabel);
            if (named != null) GUI.Label(new Rect(area.x + pad, area.y + U(2f), area.width - pad * 2f - U(18f), line), named, Skin.FaintLabel);

            PlacedRoom hovered = null;
            if (area.Contains(e.mousePosition) && (_drag == Drag.None || _drag == Drag.Orbit))
            {
                var right = extent.MinRight + (e.mousePosition.x - area.x - pad) / scale;
                var up = extent.MaxUp - (e.mousePosition.y - area.y - pad - line) / scale;
                var (x, z) = ExamplePlan.Back(right, up, yaw);
                hovered = ExamplePlan.TopmostAt(PlanRooms, x, z, OnPlanRoom);
            }

            // What lies under the floor first, then every tile of it, then their outlines over all
            // the tiles, so rooms overlapping show where each ends rather than a darker patch.
            if (e.type == EventType.Repaint)
            {
                foreach (var room in PlanRooms) if (PlanTiles.TryGetValue(room, out var tile) && tile == PlanTile.Below) PlanRoom(room, At(room.Position), scale, yaw, RoomDraw.Beneath);
                foreach (var room in PlanRooms) if (OnPlan(room)) PlanRoom(room, At(room.Position), scale, yaw, Lit(room, hovered) ? RoomDraw.LitTile : RoomDraw.Tile);
                foreach (var room in PlanRooms) if (OnPlan(room) && !Lit(room, hovered)) PlanRoom(room, At(room.Position), scale, yaw, RoomDraw.Outline);
                foreach (var room in PlanRooms) if (OnPlan(room) && Lit(room, hovered)) PlanRoom(room, At(room.Position), scale, yaw, RoomDraw.LitOutline);
            }
            CountDrawn(PanelPart.Plan);
            var door = Mathf.Max(U(2f), 0.8f * scale);
            var mark = Mathf.Max(U(8f), door * 2f);
            for (var i = 0; i < example.Doors.Count; i++)
            {
                var at = example.Doors[i];
                if (!ExamplePlan.DoorShown(at, floor)) continue;
                var p = At(at);
                var leads = i < PlanDoors.Count ? PlanDoors[i] : DoorLeads.Level;
                if (leads == DoorLeads.Level) Skin.Fill(new Rect(p.x - door / 2f, p.y - door / 2f, door, door), Skin.Alpha(Skin.Accent, 0.85f));
                else Skin.Icon(new Rect(p.x - mark / 2f, p.y - mark / 2f, mark, mark), leads == DoorLeads.Up ? Skin.Up : Skin.Down, Skin.Accent);
            }

            if (area.Contains(e.mousePosition))
            {
                if (hovered != null)
                {
                    var key = EntryKeys.For(Kind.Location, hovered.Room.Name);
                    var known = Goes(explorer, key);
                    // A room not met is named by its kind alone, as a closed page is.
                    var name = explorer.NotMet(key) ? CompendiumWords.Unknown(Kind.Location) : ShownName(explorer, key, LocationWords.RoomName(hovered.Room.Name));
                    AskTip("plan:" + hovered.Room.Name, StageWords.RoomTip(name, known));
                    if (known && e.type == EventType.MouseDown && e.button == 0)
                    {
                        e.Use();
                        Go(explorer, key);
                        return area;
                    }
                }

                // The plan is not the stage: a click or the wheel on it neither turns nor zooms it.
                if (e.type == EventType.MouseDown || e.type == EventType.ScrollWheel) e.Use();
            }
            return area;
        }

        /// <summary>Whether a room is a tile of the plan with the floor opened.</summary>
        private static bool OnPlan(PlacedRoom room) => PlanTiles.TryGetValue(room, out var tile) && tile == PlanTile.On;

        /// <summary>The same, made once, for finding the room under the mouse.</summary>
        private static readonly Func<PlacedRoom, bool> OnPlanRoom = OnPlan;

        /// <summary>
        /// Whether a room is lit on the plan: under the mouse on it; or while the mouse is on no
        /// room of the plan, the room a way chip of the room gone into's card is hovered for, else
        /// rooms of the name under the mouse on the stage.
        /// </summary>
        private static bool Lit(PlacedRoom room, PlacedRoom hovered)
        {
            if (hovered != null) return room == hovered;
            if (_cardLit != null) return room == _cardLit;
            return _stageRoom != null && _stageRoom == room.Room.Name;
        }

        /// <summary>What of a room the plan draws, in the order drawn: one under the floor, a tile of it, its outline over the tiles.</summary>
        private enum RoomDraw
        {
            Beneath,
            Tile,
            LitTile,
            Outline,
            LitOutline,
        }

        /// <summary>
        /// A room, turned as it is and as the view is: beneath the floor its outline in faint
        /// dashes; a tile of one tone over the stage's colour, so overlaps are no darker, the
        /// entrance in the accent colour and end caps and dividers fainter; or its outline.
        /// </summary>
        private static void PlanRoom(PlacedRoom room, Vector2 centre, float scale, float yaw, RoomDraw draw)
        {
            var shape = room.Room;
            var w = Mathf.Max(U(2f), shape.Size.X * scale);
            var h = Mathf.Max(U(2f), shape.Size.Z * scale);
            var closing = shape.EndCap || shape.Divider;
            var tone = shape.Entrance ? Skin.Accent : closing ? Skin.Faint : Skin.KindColor(Kind.Location);

            var was = GUI.matrix;
            GUIUtility.RotateAroundPivot(room.Rotation.YawDegrees - yaw, centre);
            var rect = new Rect(centre.x - w / 2f, centre.y - h / 2f, w, h);
            switch (draw)
            {
                case RoomDraw.Beneath:
                    PlanDashes(rect, Skin.Alpha(Skin.Faint, 0.4f));
                    break;
                case RoomDraw.Tile:
                case RoomDraw.LitTile:
                    Skin.Fill(rect, Skin.OverStage(tone, closing ? 0.3f : draw == RoomDraw.LitTile ? 0.7f : 0.4f));
                    break;
                case RoomDraw.Outline:
                    PlanFrame(rect, Skin.Alpha(Skin.Text, 0.45f), 1f);
                    break;
                default:
                    PlanFrame(rect, Skin.Accent, 2f);
                    break;
            }
            GUI.matrix = was;
        }

        /// <summary>A rectangle's outline in dashes, for a room under the floor opened.</summary>
        private static void PlanDashes(Rect rect, Color color)
        {
            var dash = U(3f);
            for (var x = rect.x; x < rect.xMax; x += dash * 2f)
            {
                var w = Mathf.Min(dash, rect.xMax - x);
                Skin.Fill(new Rect(x, rect.y, w, 1f), color);
                Skin.Fill(new Rect(x, rect.yMax - 1f, w, 1f), color);
            }
            for (var y = rect.y; y < rect.yMax; y += dash * 2f)
            {
                var h = Mathf.Min(dash, rect.yMax - y);
                Skin.Fill(new Rect(rect.x, y, 1f, h), color);
                Skin.Fill(new Rect(rect.xMax - 1f, y, 1f, h), color);
            }
        }

        /// <summary>
        /// How each room shows and where each door leads with floor <paramref name="level"/> opened
        /// (<see cref="ExamplePlan.Tile"/>, <see cref="ExamplePlan.Leads"/>), worked out again only
        /// when the example, the floor opened, the floors or the rooms whose ground is read change.
        /// </summary>
        private static void PlanMarks(DungeonExample example, IReadOnlyList<float> floors, int level)
        {
            var sum = 0f;
            for (var i = 0; i < floors.Count; i++) sum += floors[i] * (i + 1);
            var read = Stage.ExampleGroundRead;
            if (ReferenceEquals(example, _marksOf) && level == _marksLevel && read == _marksRead && sum.Equals(_marksFloors)) return;
            _marksOf = example;
            _marksLevel = level;
            _marksRead = read;
            _marksFloors = sum;
            PlanTiles.Clear();
            var on = 0;
            var below = 0;
            foreach (var room in PlanRooms)
            {
                var tile = ExamplePlan.Tile(room, floors, level, HasFloor);
                PlanTiles[room] = tile;
                if (tile == PlanTile.On && !room.Room.EndCap && !room.Room.Divider) on++;
                if (tile == PlanTile.Below) below++;
            }
            PlanTilesOn = on;
            PlanTilesBelow = below;
            PlanDoors.Clear();
            var marked = 0;
            foreach (var door in example.Doors)
            {
                var leads = ExamplePlan.Leads(door, PlanRooms, floors, level, HasFloor);
                PlanDoors.Add(leads);
                if (leads != DoorLeads.Level) marked++;
            }
            PlanDoorsMarked = marked;
        }

        /// <summary>A rectangle's outline, so many pixels thick.</summary>
        private static void PlanFrame(Rect rect, Color color, float thickness)
        {
            Skin.Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Skin.Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Skin.Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Skin.Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        /// <summary>Lets go of the dungeon plan last drawn and its rooms.</summary>
        private static void ForgetPlan()
        {
            _planOf = null;
            PlanRooms.Clear();
            _marksOf = null;
            PlanTiles.Clear();
            PlanDoors.Clear();
        }
    }
}
