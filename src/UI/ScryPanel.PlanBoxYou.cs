using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The plan box over the world's own dungeon (<see cref="ExampleLayouts.ShowsWorlds"/>): the room
    /// the player stands in outlined in the accent, their arrow where they stand and facing as they
    /// face, an accent arrow at each doorway leading on to a room not walked, the rooms shown only
    /// while peeking dashed and faint under the peek's warning, and how much is walked in a row under the plan.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>How much of a peeked room's fill is kept.</summary>
        private const float PeekedShare = 0.35f;

        /// <summary>How far an open way's arrow reaches out of its doorway, in metres.</summary>
        private const float OpenWayReach = 3f;

        /// <summary>Whether the player's arrow was last drawn on the plan, and how many open ways, for the self-test.</summary>
        [Diagnostic]
        public static bool PlanBoxYouDrawn { get; private set; }

        [Diagnostic]
        public static int PlanBoxOpenWaysDrawn { get; private set; }

        /// <summary>Whether the peek's warning stands over the plan.</summary>
        private static bool PeekStripShown => ExampleLayouts.ShowsWorlds && ExampleLayouts.PeekedRooms > 0;

        /// <summary>A room's tile in the plan from above or on its level's plate: the room gone into in the accent, one shown only while peeking dashed and faint.</summary>
        private static void RoomTile(BoxSpot spot, PlacedRoom gone)
        {
            var peeked = ExampleLayouts.IsPeeked(spot.Room);
            var fill = spot.Room == gone ? Skin.Alpha(Skin.Accent, 0.75f) : DepthTone(spot.Room);
            BoxTile(spot, peeked ? Skin.Alpha(fill, fill.a * PeekedShare) : fill, peeked ? Skin.Alpha(Skin.Faint, 0.6f) : Skin.Alpha(Skin.Text, 0.3f), 1f, dashed: peeked);
        }

        /// <summary>Whether a room's tile is outlined in the accent: lit, gone into, or the one the player stands in.</summary>
        private static bool BoxOutlined(PlacedRoom room, PlacedRoom gone) => BoxLit(room) || room == gone || ExampleLayouts.IsYours(room);

        /// <summary>Whether the box tells how much of the world's own dungeon is walked, in a row of its own under the plan.</summary>
        private static bool WalkedShown => ExampleLayouts.ShowsWorlds && ExampleLayouts.Fogged;

        /// <summary>How much of the world's own dungeon is walked, in its row under the plan, clear of the rooms; its type made smaller where the box is narrow.</summary>
        private static void WalkedLabel(Rect row)
        {
            if (!WalkedShown || Event.current.type != EventType.Repaint) return;
            FitLabel(row, PlanBoxWords.Walked(ExampleLayouts.Walked.Walked, ExampleLayouts.Walked.Rooms), Skin.FaintLabel, 10f);
        }

        /// <summary>The peek's warning over the plan, while rooms not walked show.</summary>
        private static void PeekStrip(Rect row)
        {
            if (Event.current.type != EventType.Repaint) return;
            Skin.Fill(row, Skin.WarnSoft);
            GUI.Label(new Rect(row.x + U(6f), row.y, row.width - U(12f), row.height), PlanBoxWords.PeekingRooms(ExampleLayouts.PeekedRooms), Skin.Fitted(Skin.WarnLabel, row));
        }

        /// <summary>
        /// Where the player stands and the ways on, in the plan from above or on the levels' plates,
        /// <paramref name="at"/> placing a point of a room's on the box: the arrow facing as the
        /// player faces, and each open way an accent line out of its doorway with its head.
        /// </summary>
        private static void YouAndWays(Func<PlacedRoom, Vec3, Vector2> at)
        {
            PlanBoxYouDrawn = false;
            PlanBoxOpenWaysDrawn = 0;
            if (!ExampleLayouts.ShowsWorlds) return;
            var example = ExampleLayouts.Example;
            OpenWaysOn((room, door, reach) => (at(room, door), at(room, reach)));
            var yours = YourRoom();
            if (yours == null || example == null || !DungeonWalks.YouStand(out var where, out var facing)) return;
            var here = example.FromGeneratorPoint(where);
            var ahead = here + Quat.Yaw(example.FromGeneratorYaw(facing)) * new Vec3(0f, 0f, 1f);
            var spot = at(yours, here);
            YouArrow(spot, at(yours, ahead) - spot);
        }

        /// <summary>Where the player stands and the ways on, in the tree: the arrow over the player's room's node, each open way a short line down out of its room's.</summary>
        private static void TreeYouAndWays(Dictionary<PlacedRoom, Vector2> at, float half)
        {
            PlanBoxYouDrawn = false;
            PlanBoxOpenWaysDrawn = 0;
            if (!ExampleLayouts.ShowsWorlds) return;
            OpenWaysOn((room, door, reach) =>
            {
                var from = at.TryGetValue(room, out var node) ? node : new Vector2(float.NaN, float.NaN);
                return (from + new Vector2(0f, half), from + new Vector2(0f, half + U(10f)));
            });
            var yours = YourRoom();
            if (yours != null && at.TryGetValue(yours, out var mine)) YouArrow(mine - new Vector2(0f, half + U(8f)), new Vector2(0f, 1f));
        }

        /// <summary>Each open way whose room the plan shows, its doorway and its reach placed by <paramref name="place"/>.</summary>
        private static void OpenWaysOn(Func<PlacedRoom, Vec3, Vec3, (Vector2 From, Vector2 To)> place)
        {
            var ways = ExampleLayouts.OpenWays;
            var example = ExampleLayouts.Example;
            if (ways.Count == 0 || example == null) return;
            var mouse = Event.current.mousePosition;
            foreach (var way in ways)
            {
                PlacedRoom room = null;
                foreach (var r in BoxRooms)
                {
                    if (r.SavedAt != way.From.SavedAt) continue;
                    room = r;
                    break;
                }
                if (room == null) continue;
                var door = example.FromGeneratorPoint(way.At);
                var outward = door - room.Position;
                outward = new Vec3(outward.X, 0f, outward.Z);
                var length = outward.Length;
                var reach = length > 1e-3f ? door + outward * (OpenWayReach / length) : door + new Vec3(0f, 0f, OpenWayReach);
                var (from, to) = place(room, door, reach);
                if (float.IsNaN(from.x)) continue;
                PlanBoxOpenWaysDrawn++;
                if ((mouse - to).sqrMagnitude <= U(8f) * U(8f)) AskTip("plan:openway", PlanBoxWords.OpenWayTip);
                if (Event.current.type != EventType.Repaint) continue;
                PlanLine(from, to, Skin.Accent, 1.5f);
                Head(to, to - from, U(7f), Skin.Accent);
            }
        }

        /// <summary>The player's arrow, a soft ring under it, pointing along <paramref name="ahead"/> on the box.</summary>
        private static void YouArrow(Vector2 at, Vector2 ahead)
        {
            PlanBoxYouDrawn = true;
            if (Event.current.type != EventType.Repaint) return;
            var ring = U(14f);
            Skin.Icon(new Rect(at.x - ring / 2f, at.y - ring / 2f, ring, ring), Skin.Circle, Skin.Alpha(Skin.Accent, 0.25f));
            Head(at, ahead, U(11f), Skin.Accent);
        }

        /// <summary>An arrow's head at a point, pointing along a direction on the box.</summary>
        private static void Head(Vector2 at, Vector2 along, float size, Color colour)
        {
            if (along.sqrMagnitude < 1e-6f) along = new Vector2(0f, -1f);
            var angle = Mathf.Atan2(along.x, -along.y) * Mathf.Rad2Deg;
            var was = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, at);
            Skin.Icon(new Rect(at.x - size / 2f, at.y - size / 2f, size, size), Skin.Up, colour);
            GUI.matrix = was;
        }
    }
}
