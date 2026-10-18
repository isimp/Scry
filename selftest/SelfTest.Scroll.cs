using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's cases for stepping through a dungeon's levels and rooms with Shift and the wheel, and for where the plan box's controls stand.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// What is wrong with where the plan box's controls were last drawn: grow and fold not in the
        /// box's top right corner on the views' row, no line under the header across the box over the
        /// plan, the plan's own switches not in its top right corner under that line (the rooms below
        /// from above only, the names grown only, joined where both stand), or two controls over each
        /// other.
        /// </summary>
        private static List<string> PlanBoxLaidOut(string name, PlanView view, bool big)
        {
            var wrong = new List<string>();
            var parts = ScryPanel.PlanBoxPartsForTest.ToList();
            var box = ScryPanel.PlanBoxRect;
            Rect? Of(string part) => parts.Any(q => q.Part == part) ? parts.First(q => q.Part == part).At : (Rect?)null;
            if (!(Of("fold") is Rect fold) || !(Of("grow") is Rect grow) || !(Of("plan") is Rect plan))
            {
                wrong.Add($"{name}: {string.Join(", ", parts.Select(q => q.Part))} drawn");
                return wrong;
            }
            if (fold.xMax > box.xMax || box.xMax - fold.xMax > fold.width || fold.y - box.y > fold.height * 2.5f) wrong.Add($"{name}: fold at {Said(fold)} in the box {Said(box)}");
            if (grow.xMax > fold.x || fold.x - grow.xMax > grow.width || Mathf.Abs(grow.y - fold.y) > 0.5f) wrong.Add($"{name}: grow at {Said(grow)} beside fold at {Said(fold)}");
            var views = parts.Where(q => q.Part == "view").Select(q => q.At).ToList();
            if (views.Count > 0 && (Mathf.Abs(grow.y - views[0].y) > 0.5f || views.Max(v => v.xMax) > grow.x)) wrong.Add($"{name}: grow at {Said(grow)} not beside the views, the last at {Said(views[views.Count - 1])}");

            // The plan's own switches: the rooms below from above only, the names grown only, the outer one in the plan's corner.
            var names = Of("names");
            var below = Of("below");
            if (big != names.HasValue) wrong.Add($"{name}: the names' switch {(names.HasValue ? "shown" : "missing")}");
            if ((view == PlanView.Above) != below.HasValue) wrong.Add($"{name}: the rooms below's switch {(below.HasValue ? "shown" : "missing")}");
            var outer = names ?? below;
            if (outer is Rect corner && (Mathf.Abs(corner.xMax - plan.xMax) > 1f || Mathf.Abs(corner.y - plan.y) > 1f)) wrong.Add($"{name}: the corner's switch at {Said(corner)} in the plan {Said(plan)}");
            if (names is Rect n && below is Rect b && (Mathf.Abs(b.xMax - n.x) > 1f || Mathf.Abs(b.y - n.y) > 0.5f)) wrong.Add($"{name}: the rooms below's switch at {Said(b)} not joined to the names' at {Said(n)}");

            var header = parts.Where(q => q.Part == "view" || q.Part == "grow" || q.Part == "fold").Max(q => q.At.yMax);
            if (!(Of("line") is Rect line)) wrong.Add($"{name}: no line under the header");
            else if (line.xMin > plan.xMin + 1f || line.xMax < plan.xMax - 1f || line.yMin < header - 0.5f || line.yMax > plan.y + 0.5f || (outer is Rect under && line.yMax > under.y + 0.5f))
                wrong.Add($"{name}: the line at {Said(line)}, the header to {Numbers.Fixed(header, 0)}, the plan at {Said(plan)}");
            var controls = parts.Where(q => q.Part != "plan" && q.Part != "line").ToList();
            for (var i = 0; i < controls.Count; i++)
            {
                for (var j = i + 1; j < controls.Count; j++)
                {
                    var overlap = Rect.MinMaxRect(Mathf.Max(controls[i].At.xMin, controls[j].At.xMin), Mathf.Max(controls[i].At.yMin, controls[j].At.yMin), Mathf.Min(controls[i].At.xMax, controls[j].At.xMax), Mathf.Min(controls[i].At.yMax, controls[j].At.yMax));
                    if (overlap.width > 0.5f && overlap.height > 0.5f) wrong.Add($"{name}: {controls[i].Part} over {controls[j].Part}");
                }
            }
            return wrong;
        }

        /// <summary>A rectangle as a part of the panel: its corner and size in whole points.</summary>
        private static string Said(Rect at) => $"{Numbers.Fixed(at.x, 0)}, {Numbers.Fixed(at.y, 0)} ({Numbers.Fixed(at.width, 0)} by {Numbers.Fixed(at.height, 0)})";

        /// <summary>
        /// The Frost Caves, laid out again until one has two levels: Shift and the wheel step through
        /// its levels a notch at a time, each opened at its own cut, down and up alike, the lowest
        /// no lower and over the top the roof on; small turns, as a touchpad's, add up to a step. A
        /// room with a floor of its own on another level stands whole there. In a room each notch
        /// steps its marks, and past its ends the level below or above; from room to room the zoom
        /// stays.
        /// </summary>
        private static IEnumerator ScrollingThroughLevels(Probe p)
        {
            var entry = LocationNamed("MountainCave02");
            if (entry == null) p.Skip("there are no Frost Caves");
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            Select(entry);
            yield return Until(() => CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.ExampleFloorsToRead == 0, 90);
            // Some rolls of the caves lie on one level: another layout, as the player's button lays one, until one has two.
            for (var roll = 0; roll < 5 && CopyOf(entry) != null && Stage.FloorHeights.Count < 2; roll++)
            {
                // Waited for until the stage stands the new layout, not the old one still standing the frame it is asked for.
                var standing = Stage.ExampleShown;
                ExampleLayouts.Another();
                yield return Until(() => Stage.ExampleShown != null && Stage.ExampleShown != standing && CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.ExampleFloorsToRead == 0, 90);
            }
            if (!p.Check(CopyOf(entry) != null && Stage.ExampleWays != null && Stage.FloorHeights.Count >= 2, "its example stands with two levels or more, laid out again where one has fewer", $"{Numbers.Count(Stage.FloorHeights.Count)} levels"))
            {
                Stage.Inside = wasInside;
                yield break;
            }
            Stage.SetCutFromSide(false);
            Stage.ComeOut();
            Stage.OpenLevel(Stage.FloorHeights.Count);
            yield return LevelSettled();

            // A notch at a time from the roof down, each level at its own cut, the lowest no lower;
            // then up again, over the top level the roof back on.
            var cuts = Stage.CutHeights.ToList();
            var count = cuts.Count;
            var unstepped = new List<string>();
            for (var notch = 0; notch <= count; notch++)
            {
                ScryPanel.WheelForTest(3f);
                var want = Mathf.Min(notch, count - 1);
                if (Stage.CutLevel != want || Mathf.Abs(Stage.CutAt - cuts[want]) > 0.01f)
                    unstepped.Add($"down, notch {Numbers.Count(notch + 1)}: {Stage.CutLabel} at {Numbers.Fixed(Stage.CutAt, 2)} m, its cut {Numbers.Fixed(cuts[want], 2)} m");
            }
            yield return LevelSettled();
            var whole = Stage.ExampleShown.Rooms.Count(r => Stage.ExampleRoomShown(r) == PlanRoomShown.Whole);
            p.Check(whole > 0, "rooms stand whole on the lowest level", $"{Numbers.Count(whole)} rooms");
            yield return Picture(p, $"scroll {entry.Name} down to the lowest level");
            for (var notch = count - 2; notch >= -1; notch--)
            {
                ScryPanel.WheelForTest(-3f);
                if (notch >= 0 ? Stage.CutLevel != notch || Mathf.Abs(Stage.CutAt - cuts[notch]) > 0.01f : Stage.Cutting)
                    unstepped.Add($"up to {(notch >= 0 ? "level " + Numbers.Count(notch + 1) : "the roof")}: {Stage.CutLabel} at {Numbers.Fixed(Stage.CutAt, 2)} m");
            }
            p.Check(unstepped.Count == 0, "Shift and the wheel step a level a notch, each at its own cut, down and up alike, the lowest no lower, over the top the roof on", string.Join("; ", unstepped));

            // Small turns, as a touchpad's, add up to a step.
            Stage.OpenLevel(0);
            ScryPanel.WheelForTest(-0.5f);
            ScryPanel.WheelForTest(0.4f);
            ScryPanel.WheelForTest(0.4f);
            var held = Stage.CutLevel;
            ScryPanel.WheelForTest(0.4f);
            p.Check(held == 0 && Stage.CutLevel == 1, "small turns of the wheel add up to one step", $"level {Numbers.Count(held + 1)}, then {Numbers.Count(Stage.CutLevel + 1)}");

            // A room with a floor of its own on another level stands whole there.
            var levelCuts = Stage.ExampleLevelCuts;
            var rooms = Stage.ExampleWays.Rooms;
            var reaching = new List<(PlacedRoom Room, int Level)>();
            for (var i = 0; i < rooms.Count && levelCuts != null; i++)
            {
                for (var level = 0; level < levelCuts.Levels; level++)
                {
                    if (level != levelCuts.LevelOf(i) && levelCuts.OnLevelByFloor(i, level)) reaching.Add((rooms[i], level));
                }
            }
            var unstood = new List<string>();
            foreach (var (room, level) in reaching.Take(4))
            {
                Stage.OpenLevel(Stage.ExampleMarkOfLevelForTest(level));
                yield return LevelSettled();
                if (Stage.ExampleRoomShown(room) != PlanRoomShown.Whole) unstood.Add($"{room.Room.Name} on level {Numbers.Count(level)}: {Stage.ExampleRoomShown(room)}");
            }
            if (reaching.Count == 0) p.Note($"{entry.Name}: no room of this example has a floor of its own on another level");
            else p.Check(unstood.Count == 0, "a room with a floor of its own on another level stands whole there", string.Join("; ", unstood));

            // In a room each notch steps its marks, the cut lower each time, and past its lowest the level below.
            var top = rooms.FirstOrDefault(r => Stage.ExampleLevelOfRoomForTest(r) == 0);
            var below = rooms.FirstOrDefault(r => Stage.ExampleLevelOfRoomForTest(r) == 1);
            if (top != null)
            {
                Stage.GoInto(top);
                yield return Until(() => Stage.RoomGoneInto == top && !Stage.Gliding, 5);
                var marks = new List<float>();
                for (var notch = 0; notch < 12 && Stage.RoomGoneInto == top; notch++)
                {
                    marks.Add(Stage.CutAt);
                    ScryPanel.WheelForTest(3f);
                }
                var falling = marks.Zip(marks.Skip(1), (a, b) => b < a - 0.01f).All(f => f);
                p.Check(Stage.RoomGoneInto == null && Stage.CutLevel == Stage.ExampleMarkOfLevelForTest(1) && falling,
                    "in a room each notch steps its marks down, and past its lowest the level below", $"{Numbers.Count(marks.Count)} marks, {Stage.RoomGoneInto?.Room.Name ?? Stage.CutLabel}");
            }
            if (below != null)
            {
                Stage.GoInto(below);
                yield return Until(() => Stage.RoomGoneInto == below && !Stage.Gliding, 5);
                for (var notch = 0; notch < 12 && Stage.RoomGoneInto == below; notch++) ScryPanel.WheelForTest(-3f);
                p.Check(Stage.RoomGoneInto == null && Stage.CutLevel == Stage.ExampleMarkOfLevelForTest(0), "and up past its roof the level above", Stage.RoomGoneInto?.Room.Name ?? Stage.CutLabel);
            }

            // From room to room the zoom stays.
            var other = rooms.FirstOrDefault(r => r != top && Stage.ExampleLevelOfRoomForTest(r) == 0);
            if (top != null && other != null)
            {
                Stage.GoInto(top);
                yield return Until(() => Stage.RoomGoneInto == top && !Stage.Gliding, 5);
                Stage.ZoomBy(-3f);
                var zoom = Stage.Zoom;
                Stage.GoInto(other);
                yield return Until(() => Stage.RoomGoneInto == other, 5);
                p.Check(Mathf.Abs(Stage.Zoom - zoom) < 0.001f, "going from room to room keeps the zoom", $"{Numbers.Fixed(zoom, 2)} became {Numbers.Fixed(Stage.Zoom, 2)}");
                Stage.ComeOut();
            }
            Stage.Inside = wasInside;
        }
    }
}
