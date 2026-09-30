using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The example layout of a location's dungeon or camp (<see cref="ExampleLayouts"/>), as seen
    /// from above, north up: its zone's box, each room, the entrance marked, and the doors. A room
    /// under the mouse is named, and a click on it goes to its entry.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>The example drawn, with its extent and its rooms from the lowest up, worked out once for it rather than every frame.</summary>
        private static DungeonExample _planOf;
        private static (float MinX, float MaxX, float MinZ, float MaxZ) _planExtent;
        private static readonly List<PlacedRoom> PlanRooms = new List<PlacedRoom>();

        private static float PlanSection(Explorer explorer, Entry entry, float width, float y)
        {
            if (!(entry.Source is PlaceSource place) || place.IsRoom || place.Contents?.Dungeon == null) return y;

            y = SectionHeading("EXAMPLE LAYOUT", width, y, null, "plan");
            if (IsFolded("plan")) return y;

            var example = ExampleLayouts.Of(entry) ? ExampleLayouts.Example : null;
            if (example == null)
            {
                if (!ExampleLayouts.Of(entry)) return y + U(14f);
                var reading = DungeonWords.Reading(ExampleLayouts.Read, ExampleLayouts.Total);
                var readingH = Skin.Height(Skin.DimWrap, reading, width);
                GUI.Label(new Rect(0f, y, width, readingH), reading, Skin.DimWrap);
                return y + readingH + U(14f);
            }

            // What it holds, and a button for another; every world lays out its own.
            const string another = "Another example";
            var buttonW = Skin.Width(Skin.Button, another) + U(12f);
            if (GUI.Button(new Rect(width - buttonW, y, buttonW, U(28f)), another, Skin.Button)) ExampleLayouts.Another();
            var caption = DungeonWords.Example(example, ExampleLayouts.Failed) + (example.Rooms.Count > 0 ? ". One way it can come out; each world lays out its own." : ".");
            var captionW = width - buttonW - U(10f);
            var captionH = Skin.Height(Skin.DimWrap, caption, captionW);
            GUI.Label(new Rect(0f, y, captionW, captionH), caption, Skin.DimWrap);
            y += Mathf.Max(U(28f), captionH) + U(8f);
            if (example.Rooms.Count == 0) return y + U(6f);

            if (_planOf != example)
            {
                _planOf = example;
                _planExtent = example.Extent();
                PlanRooms.Clear();
                PlanRooms.AddRange(example.BottomUp);
            }

            // The whole of it at one scale, no taller than it is wide.
            var extent = _planExtent;
            var spanX = Mathf.Max(1f, extent.MaxX - extent.MinX);
            var spanZ = Mathf.Max(1f, extent.MaxZ - extent.MinZ);
            var pad = U(8f);
            var scale = Mathf.Min((width - pad * 2f) / spanX, (width - pad * 2f) / spanZ);
            var area = new Rect(0f, y, width, spanZ * scale + pad * 2f);
            var left = (width - spanX * scale) / 2f;
            Vector2 At(Vec3 p) => new Vector2(left + (p.X - extent.MinX) * scale, area.y + pad + (extent.MaxZ - p.Z) * scale);

            if (!OutOfSight(area))
            {
                Skin.Box(area, Skin.Stage);
                var site = example.Site;
                var zoneMin = At(new Vec3(site.ZoneCenter.X - site.ZoneSize.X / 2f, 0f, site.ZoneCenter.Z + site.ZoneSize.Z / 2f));
                PlanFrame(new Rect(zoneMin.x, zoneMin.y, site.ZoneSize.X * scale, site.ZoneSize.Z * scale), Skin.Outline, 1f);

                var e = Event.current;
                PlacedRoom hovered = null;
                if (area.Contains(e.mousePosition) && _drag == Drag.None)
                {
                    var x = extent.MinX + (e.mousePosition.x - left) / scale;
                    var z = extent.MaxZ - (e.mousePosition.y - area.y - pad) / scale;
                    hovered = example.RoomAt(x, z);
                }

                foreach (var room in PlanRooms) PlanRoom(room, At(room.Position), scale, room == hovered);
                var door = Mathf.Max(U(3f), 0.8f * scale);
                foreach (var at in example.Doors)
                {
                    var p = At(at);
                    Skin.Fill(new Rect(p.x - door / 2f, p.y - door / 2f, door, door), new Color(Skin.Accent.r, Skin.Accent.g, Skin.Accent.b, 0.85f));
                }

                if (hovered != null)
                {
                    var key = EntryKeys.For(Kind.Location, hovered.Room.Name);
                    var known = InCatalog(explorer, key);
                    var name = ShownName(explorer, key, LocationWords.RoomName(hovered.Room.Name));
                    AskTip("plan:" + hovered.Room.Name, known ? name + "\nClick to go to it" : name);
                    if (known && e.type == EventType.MouseDown && e.button == 0)
                    {
                        e.Use();
                        Go(explorer, key);
                    }
                }
            }
            return area.yMax + U(14f);
        }

        /// <summary>A room's floor, turned as it is: the entrance in the accent colour, end caps, dividers and walls fainter.</summary>
        private static void PlanRoom(PlacedRoom room, Vector2 centre, float scale, bool hovered)
        {
            var shape = room.Room;
            var w = Mathf.Max(U(2f), shape.Size.X * scale);
            var h = Mathf.Max(U(2f), shape.Size.Z * scale);
            var tone = shape.Entrance ? Skin.Accent : shape.EndCap || shape.Divider ? Skin.Faint : Skin.KindColor(Kind.Location);
            var fill = new Color(tone.r, tone.g, tone.b, shape.EndCap || shape.Divider ? 0.3f : hovered ? 0.6f : 0.4f);

            var was = GUI.matrix;
            GUIUtility.RotateAroundPivot(room.Rotation.YawDegrees, centre);
            var rect = new Rect(centre.x - w / 2f, centre.y - h / 2f, w, h);
            Skin.Fill(rect, fill);
            PlanFrame(rect, hovered ? Skin.Accent : new Color(Skin.Text.r, Skin.Text.g, Skin.Text.b, 0.35f), hovered ? 2f : 1f);
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
    }
}
