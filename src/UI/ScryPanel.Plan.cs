using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The example layout of a location's dungeon or camp (<see cref="ExampleLayouts"/>): what it
    /// holds and a button for another under the details, and its plan in a corner of the stage,
    /// as seen from above, turned with the view and framed on its rooms (<see cref="ExamplePlan"/>):
    /// each room, the entrance marked, and the doors; with a floor opened, only that floor's
    /// rooms and doors. A room under the mouse, on the plan or on the stage, is lit on the
    /// plan and named, and a click on it goes to its entry. The stage's *Plan* chip puts the plan
    /// away and back.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>The example drawn, with its rooms from the lowest up and how wide they are any way round, worked out once for it rather than every frame.</summary>
        private static DungeonExample _planOf;
        private static float _planWidest;
        private static readonly List<PlacedRoom> PlanRooms = new List<PlacedRoom>();

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
            // The example as the stage holds it, so the plan turns as the stage is looked at.
            var example = ExampleOf(entry) != null ? Stage.ExampleShown : null;
            if (example == null || example.Rooms.Count == 0) return Rect.zero;
            var e = Event.current;

            // Folded, a small tab where it stands, which brings it back.
            if (PlanFolded)
            {
                var tabW = Skin.Width(Skin.Chip, "Plan") + U(4f);
                var tab = new Rect(inner.x + U(8f), inner.yMax - U(34f) - U(22f), tabW, U(22f));
                if (tab.Contains(e.mousePosition)) AskTip("plan:tab", "Shows the example's plan");
                CountDrawn(PanelPart.PlanTab);
                if (GUI.Button(tab, "Plan", Skin.Chip)) PlanFolded = false;
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
            var extent = ExamplePlan.Extent(PlanRooms, yaw);
            var pad = U(6f);
            var scale = (Mathf.Min(Mathf.Min(inner.width * 0.34f, U(240f)), inner.height * 0.5f) - pad * 2f) / _planWidest;
            if (scale <= 0f) return Rect.zero;
            var size = new Vector2((extent.MaxRight - extent.MinRight) * scale + pad * 2f, (extent.MaxUp - extent.MinUp) * scale + pad * 2f);
            // Above the stage's line of hints along its bottom.
            var area = new Rect(inner.x + U(8f), inner.yMax - U(34f) - size.y, size.x, size.y);
            Vector2 At(Vec3 p)
            {
                var (right, up) = ExamplePlan.Turn(p.X, p.Z, yaw);
                return new Vector2(area.x + pad + (right - extent.MinRight) * scale, area.y + pad + (extent.MaxUp - up) * scale);
            }

            Skin.Box(area, Skin.Alpha(Skin.Stage, 0.82f), Skin.Outline);

            // Its fold, in its top right corner.
            var fold = new Rect(area.xMax - U(18f), area.y + U(2f), U(16f), U(16f));
            if (fold.Contains(e.mousePosition))
            {
                AskTip("plan:fold", "Puts the plan away");
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    PlanFolded = true;
                    e.Use();
                    return area;
                }
            }
            if (e.type == EventType.Repaint) GUI.Label(fold, "\u2013", fold.Contains(e.mousePosition) ? Skin.Label : Skin.DimLabel);

            PlacedRoom hovered = null;
            if (area.Contains(e.mousePosition) && (_drag == Drag.None || _drag == Drag.Orbit))
            {
                var right = extent.MinRight + (e.mousePosition.x - area.x - pad) / scale;
                var up = extent.MaxUp - (e.mousePosition.y - area.y - pad) / scale;
                var (x, z) = ExamplePlan.Back(right, up, yaw);
                hovered = ExamplePlan.TopmostAt(PlanRooms, x, z, r => Stage.ExampleRoomShown(r) == PlanRoomShown.Whole);
            }

            foreach (var room in PlanRooms)
            {
                // Only the floor opened: the stage dims what is below it.
                if (Stage.ExampleRoomShown(room) != PlanRoomShown.Whole) continue;
                var lit = room == hovered || (hovered == null && _stageRoom != null && _stageRoom == room.Room.Name);
                PlanRoom(room, At(room.Position), scale, yaw, lit);
            }
            CountDrawn(PanelPart.Plan);
            var door = Mathf.Max(U(2f), 0.8f * scale);
            foreach (var at in example.Doors)
            {
                if (!ExamplePlan.DoorShown(at, floor)) continue;
                var p = At(at);
                Skin.Fill(new Rect(p.x - door / 2f, p.y - door / 2f, door, door), Skin.Alpha(Skin.Accent, 0.85f));
            }

            if (area.Contains(e.mousePosition))
            {
                if (hovered != null)
                {
                    var key = EntryKeys.For(Kind.Location, hovered.Room.Name);
                    var known = InCatalog(explorer, key);
                    var name = ShownName(explorer, key, LocationWords.RoomName(hovered.Room.Name));
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

        /// <summary>A room's floor, turned as it is and as the view is: the entrance in the accent colour, end caps, dividers and walls fainter.</summary>
        private static void PlanRoom(PlacedRoom room, Vector2 centre, float scale, float yaw, bool hovered)
        {
            var shape = room.Room;
            var w = Mathf.Max(U(2f), shape.Size.X * scale);
            var h = Mathf.Max(U(2f), shape.Size.Z * scale);
            var tone = shape.Entrance ? Skin.Accent : shape.EndCap || shape.Divider ? Skin.Faint : Skin.KindColor(Kind.Location);
            var fill = Skin.Alpha(tone, shape.EndCap || shape.Divider ? 0.3f : hovered ? 0.7f : 0.4f);

            var was = GUI.matrix;
            GUIUtility.RotateAroundPivot(room.Rotation.YawDegrees - yaw, centre);
            var rect = new Rect(centre.x - w / 2f, centre.y - h / 2f, w, h);
            Skin.Fill(rect, fill);
            PlanFrame(rect, hovered ? Skin.Accent : Skin.Alpha(Skin.Text, 0.35f), hovered ? 2f : 1f);
            GUI.matrix = was;
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
        }
    }
}
