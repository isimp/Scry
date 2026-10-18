using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// The plan box of a dungeon's example inside. For each reference dungeon, each of its
        /// views, Above, Levels and Tree, is drawn compact and grown, with a spot for every room;
        /// each room is found under the mouse at its spot (in the levels and the tree always; from
        /// above unless a room drawn over it stands there), and every way is drawn; grown, the stage
        /// is framed beside it. The levels are as many as the model lays out. A room under the mouse, pointed at in the box and then on the stage, is framed in
        /// the box alone and named there, the way drawn to it, and outlined on the stage with no
        /// room gone into; it goes out as the mouse leaves; a click on one goes into it. The view and size chosen are remembered in the panel's file.
        /// The panel's font holds every sign the box and card draw. Each view is pictured.
        /// </summary>
        private static IEnumerator PlanBoxShows(Probe p)
        {
            var places = ReferenceDungeons.Select(n => X.Catalog.FirstOrDefault(e => e.Name == n && PlaceOf(e) != null && !PlaceOf(e).IsRoom)).Where(e => e != null).ToList();
            if (places.Count == 0) p.Skip("none of " + string.Join(", ", ReferenceDungeons) + " is in the catalog");
            var wasInside = Stage.Inside;
            var wasView = ScryPanel.PlanBoxView;
            var wasBig = ScryPanel.PlanBoxBig;
            var wasBelow = ScryPanel.PlanBelowShown;
            var wasNames = ScryPanel.PlanNamesShown;
            var wasFolded = ScryPanel.PlanFolded;
            ScryPanel.PlanFolded = false;
            Stage.Inside = true;
            BigStage();

            var missing = string.Concat("×·›°–".Where(c => !Skin.FontHolds(c)));
            p.Check(missing.Length == 0, "the panel's font holds every sign the plan box and the card draw", $"{Skin.FontName} lacks {missing}");

            var unspotted = new List<string>();
            var unwayed = new List<string>();
            var unfound = new List<string>();
            var unframed = new List<string>();
            var unlaid = new List<string>();
            var unlevelled = new List<string>();
            var unlitBox = new List<string>();
            var unlitStage = new List<string>();
            var stayedLit = new List<string>();
            var slowFollow = new List<string>();
            var unchosen = new List<string>();
            var unwhole = new List<string>();
            var foundBelow = 0;
            var unfresh = new List<string>();
            var untrailed = new List<string>();
            var unreturned = new List<string>();
            var unswitched = new List<string>();
            var switchTried = 0;
            var unwheeled = new List<string>();
            var apartCaps = new List<string>();
            var capsFound = new List<string>();
            var unwalked = new List<string>();
            var noWayBack = new List<string>();
            var unruled = new List<string>();
            var roomRulerTried = 0;
            p.Note(Stage.OutlinesPastCutForTest ? "the outlines are drawn past the cut, on a layer of their own" : "the outlines are drawn with the cut, to just under it, for want of a free layer");
            var notIn = new List<string>();
            foreach (var entry in places)
            {
                Select(entry);
                yield return Until(() => CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.ExampleFloorsToRead == 0, 60);
                var ways = Stage.ExampleWays;
                if (CopyOf(entry) == null || ways == null)
                {
                    p.Note($"{entry.Name}: no example stood on the stage");
                    continue;
                }
                Stage.SetCutFromSide(false);
                Stage.OpenLevel(Stage.FloorHeights.Count);
                Stage.ComeOut();

                foreach (var big in new[] { false, true })
                {
                    foreach (var view in new[] { PlanView.Above, PlanView.Levels, PlanView.Tree })
                    {
                        ScryPanel.GrowPlanForTest(big);
                        ScryPanel.ShowPlanViewForTest(view);
                        var drawn = ScryPanel.Drawn(PanelPart.PlanBox);
                        yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawn + 1, 3);
                        var name = $"{entry.Name} {PlanBoxWords.ViewName(view)} {(big ? "grown" : "compact")}";
                        var spots = ScryPanel.PlanBoxSpotsForTest();
                        if (spots.Count != ways.Rooms.Count) unspotted.Add($"{name}: {Numbers.Count(spots.Count)} spots for {Numbers.Count(ways.Rooms.Count)} rooms");
                        var pairs = ways.Rooms.Sum(r => ways.WaysOf(r).Count(o => ways.Rooms.Contains(o))) / 2;
                        if (ScryPanel.PlanBoxWaysDrawn != pairs) unwayed.Add($"{name}: {Numbers.Count(ScryPanel.PlanBoxWaysDrawn)} of {Numbers.Count(pairs)} ways drawn");

                        // Each room under the mouse at its spot; from above one drawn over it may stand there, in the levels and the tree none.
                        var found = 0;
                        var covered = 0;
                        foreach (var (room, at) in spots)
                        {
                            var under = ScryPanel.PlanBoxRoomAtForTest(at);
                            if (under == room) found++;
                            else if (view == PlanView.Above && under != null && spots.FindIndex(s => s.Room == under) > spots.FindIndex(s => s.Room == room)) covered++;
                            else unfound.Add($"{name}: {room.Room.Name} found as {(under != null ? under.Room.Name : "nothing")}");
                        }
                        p.Note($"{name}: {Numbers.Count(found)} of {Numbers.Count(spots.Count)} rooms found at their spots, {Numbers.Count(covered)} under a room drawn over them" + (view == PlanView.Levels ? $", on {Numbers.Count(ScryPanel.PlanBoxLevelsShown)} levels" : ""));

                        if (big != Stage.ViewShift > 0f) unframed.Add($"{name}: the stage framed {Numbers.Fixed(Stage.ViewShift, 2)} of its width aside");
                        unlaid.AddRange(PlanBoxLaidOut(name, view, big));
                        yield return Picture(p, $"plan {name}");
                    }
                }

                // The levels: as many as the model lays out from the rooms' grounds.
                ScryPanel.GrowPlanForTest(false);
                ScryPanel.ShowPlanViewForTest(PlanView.Levels);
                var drawnLevels = ScryPanel.Drawn(PanelPart.PlanBox);
                yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnLevels + 1, 3);
                var placedRooms = ways.Rooms.ToList();
                var laidOut = PlanLevels.Of(placedRooms, placedRooms.Select(Stage.ExampleRoomGround).ToList()).Levels;
                p.Note($"{entry.Name}: {Numbers.Count(ScryPanel.PlanBoxLevelsShown)} levels shown, of {string.Join(", ", laidOut.Select(l => Numbers.Count(l.Rooms.Count)))} rooms");
                if (ScryPanel.PlanBoxLevelsShown != laidOut.Count) unlevelled.Add($"{entry.Name}: {Numbers.Count(ScryPanel.PlanBoxLevelsShown)} levels shown, {Numbers.Count(laidOut.Count)} laid out");

                // A room under the mouse in the box is framed there alone and named, the way drawn to
                // it, and outlined on the stage with no room gone into; the mouse is pointed through
                // the box's own finding of the room under it.
                var deepest = ways.Deepest;
                var deepestAt = ScryPanel.PlanBoxSpotsForTest().FirstOrDefault(s => s.Room == deepest);
                if (deepestAt.Room == null) unlitBox.Add($"{entry.Name}: the deepest room has no spot");
                else
                {
                    ScryPanel.HoverPlanBoxForTest(deepestAt.At);
                    var drawnHover = ScryPanel.Drawn(PanelPart.PlanBox);
                    yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnHover + 1 && Stage.LitOutlineShown, 3);
                    var miss = LitMiss(deepest);
                    if (miss != null) unlitBox.Add($"{entry.Name}: {miss}");
                    else yield return Picture(p, $"plan {entry.Name} room lit");

                    // From above, the walkway to it runs from the entrance's middle to its own, and
                    // every line of the ways runs along its room's walls, inside it.
                    var walk = ScryPanel.PlanBoxRouteWalkForTest;
                    if (walk.Count == 0 || Vec3.Distance(walk[0], ways.Entrance.Position) > 0.01f || Vec3.Distance(walk[walk.Count - 1], deepest.Position) > 0.01f)
                        unwalked.Add($"{entry.Name}: the walkway to {deepest.Room.Name} runs from {(walk.Count > 0 ? walk[0].ToString() : "nowhere")} to {(walk.Count > 0 ? walk[walk.Count - 1].ToString() : "nowhere")}");
                    var astray = (ScryPanel.PlanBoxWalkwaysForTest?.Lines ?? new List<PlanWalkways.Line>())
                        .Where(l => !InRoom(l.Room, l.From) || !InRoom(l.Room, l.To) || !AlongWalls(l.Room, l.From, l.To))
                        .Select(l => $"{l.Room.Room.Name} {l.From} to {l.To}").ToList();
                    if (astray.Count > 0) unwalked.Add($"{entry.Name}: {Numbers.Count(astray.Count)} lines astray, as {string.Join(", ", astray.Take(3))}");
                    ScryPanel.HoverPlanBoxForTest(null);
                }

                // A room under the mouse on the stage is so too, in every view: the first room the
                // stage finds under the middle of one in view, outside the box; and a second one,
                // for the mouse to go from one to the other.
                ScryPanel.ShowPlanViewForTest(PlanView.Above);
                // The camera settled first, as the box's size moves what it frames.
                yield return Until(() => !Stage.Gliding, 5);
                var settling = Time.frameCount;
                yield return Until(() => Time.frameCount >= settling + 10, 2);
                PlacedRoom onStage = null, other = null;
                Vector2? onStageAt = null, otherAt = null;
                var standing = Stage.ExampleShown?.Rooms;
                for (var i = 0; standing != null && i < standing.Count && other == null; i++)
                {
                    var point = Stage.ExamplePointOf(i);
                    if (!InPicture(point)) continue;
                    ScryPanel.HoverStageForTest(point);
                    var drawnStage = ScryPanel.Drawn(PanelPart.PlanBox);
                    yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnStage + 1, 3);
                    var found = ScryPanel.StageRoomHovered;
                    if (found == null) continue;
                    if (onStage == null)
                    {
                        onStage = found;
                        onStageAt = point;
                    }
                    else if (found != onStage)
                    {
                        other = found;
                        otherAt = point;
                    }
                }
                ScryPanel.HoverStageForTest(onStageAt);
                if (onStage == null) unlitStage.Add($"{entry.Name}: no room found under the mouse on the stage");
                else
                {
                    foreach (var view in new[] { PlanView.Levels, PlanView.Tree, PlanView.Above })
                    {
                        ScryPanel.ShowPlanViewForTest(view);
                        var drawnView = ScryPanel.Drawn(PanelPart.PlanBox);
                        yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnView + 1 && Stage.LitOutlineShown, 3);
                        var miss = LitMiss(onStage);
                        if (miss != null) unlitStage.Add($"{entry.Name} {PlanBoxWords.ViewName(view)}: {miss}");
                    }
                    yield return Picture(p, $"plan {entry.Name} stage room lit");
                }

                // Going from room to room on the stage, the outline and the box follow within a
                // few frames; Scry's own work and what it allocates meanwhile are told by part.
                if (other != null)
                {
                    var firstFrame = Timing.Measuring?.Frames ?? 0;
                    var bytesBefore = Timing.ScryBytes;
                    var framesBefore = Time.frameCount;
                    var timeBefore = Time.unscaledTime;
                    Timing.ForgetBytesByPart();
                    var slowest = 0;
                    for (var i = 0; i < 8; i++)
                    {
                        ScryPanel.HoverStageForTest(i % 2 == 0 ? otherAt : onStageAt);
                        var from = Time.frameCount;
                        // Whatever room the stage finds there, lit on the stage and framed in the box.
                        yield return Until(() => ScryPanel.StageRoomHovered is PlacedRoom hovered && Stage.LitRoom == hovered && Stage.LitOutlineShown && ScryPanel.PlanBoxLitShown.Contains(hovered), 3);
                        var took = Time.frameCount - from;
                        if (took > 3 && slowFollow.Count < 6) slowFollow.Add($"{entry.Name}: {Numbers.Count(took)} frames, found {ScryPanel.StageRoomHovered?.Room.Name ?? "nothing"}, lit {Stage.LitRoom?.Room.Name ?? "nothing"}, outlined {Stage.LitOutlineShown}, framed in the box {Numbers.Count(ScryPanel.PlanBoxLitShown.Count)}");
                        slowest = System.Math.Max(slowest, took);
                        var resting = Time.frameCount;
                        yield return Until(() => Time.frameCount >= resting + 10, 2);
                    }
                    var hovering = Timing.Measuring?.Since(firstFrame);
                    var frameCount = System.Math.Max(1, Time.frameCount - framesBefore);
                    p.Note($"{entry.Name}: going from room to room on the stage, the outline and the box followed within {Numbers.Count(slowest)} frames; frames took {Numbers.Fixed((Time.unscaledTime - timeBefore) * 1000f / frameCount, 1)} ms on average"
                        + (hovering != null ? $", Scry's own work {Numbers.Fixed(hovering.Mean, 2)} ms of it, {Numbers.Fixed(hovering.Percentile(0.95), 2)} ms at the 95th percentile, {Numbers.Fixed(hovering.Max, 2)} ms at the most" : "")
                        + $"; Scry allocated about {Numbers.Count((Timing.ScryBytes - bytesBefore) / frameCount)} bytes a frame, by part: {Timing.BytesByPartTold(6)}");
                }

                ScryPanel.HoverStageForTest(null);
                var drawnLeft = ScryPanel.Drawn(PanelPart.PlanBox);
                yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnLeft + 1 && !Stage.LitOutlineShown, 3);
                if (Stage.LitOutlineShown || ScryPanel.PlanBoxLitShown.Count > 0) stayedLit.Add(entry.Name);

                // With a floor opened, a room dimmed below it can be chosen on the stage too, and
                // its outline stands its whole box.
                if (Stage.FloorHeights.Count > 1)
                {
                    Stage.OpenLevel(0);
                    yield return Until(() => Stage.Cutting && Stage.ExampleRoomsKept, 3);
                    // Each end cap and divider drawn as the room it closes: standing, dimmed or put away with it.
                    var capsApart = (Stage.ExampleShown?.Rooms ?? new List<PlacedRoom>())
                        .Where(r => (r.Room.EndCap || r.Room.Divider) && r.JoinedTo != null && !Equals(Stage.ExampleRoomDrawn(r), Stage.ExampleRoomDrawn(r.JoinedTo)))
                        .Select(r => r.Room.Name + " beside " + r.JoinedTo.Room.Name).ToList();
                    if (capsApart.Count > 0) apartCaps.Add($"{entry.Name}: {string.Join(", ", capsApart.Take(4))}");
                    PlacedRoom below = null;
                    var placedNow = Stage.ExampleShown?.Rooms;
                    // Each dimmed room's middle first, then points across the whole picture, as a
                    // stack's lower rooms stand only where the floor opened leaves them in view.
                    var points = new List<Vector2>();
                    for (var i = 0; placedNow != null && i < placedNow.Count; i++)
                    {
                        if (Stage.ExampleRoomShown(placedNow[i]) == PlanRoomShown.Faint && Stage.ExamplePointOf(i) is Vector2 middle && InPicture(middle)) points.Add(middle);
                    }
                    for (var gx = 1; gx < 10; gx++)
                    {
                        for (var gy = 1; gy < 8; gy++) points.Add(new Vector2(gx / 10f, gy / 8f));
                    }
                    foreach (var point in points)
                    {
                        if (below != null) break;
                        ScryPanel.HoverStageForTest(point);
                        var drawnBelow = ScryPanel.Drawn(PanelPart.PlanBox);
                        yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnBelow + 1, 3);
                        var found = ScryPanel.StageRoomHovered;
                        if (found != null && (found.Room.EndCap || found.Room.Divider) && capsFound.Count < 6) capsFound.Add($"{entry.Name}: {found.Room.Name}");
                        if (found != null && Stage.ExampleRoomShown(found) == PlanRoomShown.Faint) below = found;
                    }
                    if (below == null) p.Note($"{entry.Name}: no room dimmed below the top floor stands in view outside the floor opened");
                    else
                    {
                        foundBelow++;
                        yield return Until(() => Stage.LitRoom == below && Stage.LitOutlineShown, 3);
                        var top = below.Position.Y + below.Room.Size.Y / 2f;
                        if (Stage.LitRoom != below || !Stage.LitOutlineShown) unchosen.Add($"{entry.Name}: {below.Room.Name} found but not outlined");
                        else
                        {
                            if (Stage.OutlinesPastCutForTest && Mathf.Abs(Stage.LitOutlineTopLaid - top) > 0.01f) unwhole.Add($"{entry.Name}: {below.Room.Name} lit to {Numbers.Fixed(Stage.LitOutlineTopLaid, 1)} m of its top at {Numbers.Fixed(top, 1)} m");
                            yield return Picture(p, $"plan {entry.Name} room below the floor lit");
                        }
                    }
                    ScryPanel.HoverStageForTest(null);
                    Stage.ComeOut();

                    // Shift and the wheel: down with the roof on, the top floor opened; far down, a turn at
                    // a time over each gap between levels, the lowest, no lower.
                    Stage.OpenLevel(Stage.FloorHeights.Count);
                    Stage.WheelCut(-0.75f);
                    var topOpened = Stage.Cutting && Stage.CutLevel == 0;
                    for (var turn = 0; turn < 2 * Stage.FloorHeights.Count + 2; turn++) Stage.WheelCut(-1000f);
                    if (!topOpened || Stage.CutLevel != Stage.FloorHeights.Count - 1) unwheeled.Add($"{entry.Name}: top floor opened {topOpened}, at floor {Numbers.Count(Stage.CutLevel + 1)} of {Numbers.Count(Stage.FloorHeights.Count)} after far down");
                    Stage.OpenLevel(Stage.FloorHeights.Count);
                }

                // The switch for the rooms below, in the entrance, which has rooms below it: off, the
                // plan from above leaves them out, its ways down marked, the stage showing them as
                // before; on, back. Whole dungeon stands under the plan meanwhile.
                ScryPanel.DoubleClickPlanRoomForTest(ways.Entrance);
                yield return Until(() => Stage.RoomGoneInto == ways.Entrance && !Stage.Gliding, 5);
                ScryPanel.ShowPlanViewForTest(PlanView.Above);
                var belowRooms = ways.Rooms.Where(r => Stage.ExampleRoomLook(r) == RoomLook.Below).ToList();
                if (belowRooms.Count > 0)
                {
                    switchTried++;
                    ScryPanel.ShowBelowForTest(false);
                    var drawnOff = ScryPanel.Drawn(PanelPart.PlanBox);
                    yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnOff + 1 && belowRooms.All(r => Stage.ExampleRoomDrawn(r)?.Standing == true), 3);
                    var spotted = ScryPanel.PlanBoxSpotsForTest().Select(s => s.Room).ToList();
                    var leftIn = belowRooms.Count(spotted.Contains);
                    var belowStanding = belowRooms.Count(r => Stage.ExampleRoomDrawn(r)?.Standing == true);
                    var remembered = ScryPanel.RememberedForTest().PlanBelow;
                    if (leftIn > 0 || belowStanding != belowRooms.Count || ScryPanel.PlanBoxWaysDown == 0 || remembered != false)
                        unswitched.Add($"{entry.Name}: {Numbers.Count(leftIn)} of {Numbers.Count(belowRooms.Count)} rooms below still in the plan, {Numbers.Count(belowStanding)} standing on the stage, {Numbers.Count(ScryPanel.PlanBoxWaysDown)} ways down marked, the file saying {remembered?.ToString() ?? "nothing"}");
                    else yield return Picture(p, $"plan {entry.Name} rooms below left out of the plan");
                    // A way down's mark stands for the room it leads to: the mouse on it finds that room.
                    var marks = ScryPanel.PlanBoxWayDownMarksForTest.ToList();
                    if (marks.Count > 0 && ScryPanel.PlanBoxRoomAtForTest(marks[0].At.center) != marks[0].Room)
                        unswitched.Add($"{entry.Name}: the mark of the way down to {marks[0].Room.Room.Name} finds {ScryPanel.PlanBoxRoomAtForTest(marks[0].At.center)?.Room.Name ?? "nothing"}");
                    ScryPanel.ShowBelowForTest(true);
                    var drawnOn = ScryPanel.Drawn(PanelPart.PlanBox);
                    yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnOn + 1, 3);
                    var back = ScryPanel.PlanBoxSpotsForTest().Select(s => s.Room).ToList();
                    if (belowRooms.Any(r => !back.Contains(r))) unswitched.Add($"{entry.Name}: rooms below not back in the plan");
                }
                // Read once the box has been drawn with the room gone into.
                var drawnWhole = ScryPanel.Drawn(PanelPart.PlanBox);
                yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnWhole + 1 && TrailNamesRoom(ways.Entrance), 3);
                if (!TrailNamesRoom(ways.Entrance)) noWayBack.Add($"{entry.Name}: {ways.Entrance.Room.Name}, the breadcrumb {string.Join(" › ", ScryPanel.TrailForTest.Select(t => t.Text))}");
                var trailNamed = ScryPanel.TrailForTest.Count > 0 ? ScryPanel.TrailForTest[0].Text : "nothing";
                if (trailNamed != entry.ShownName) untrailed.Add($"{entry.Name}: the breadcrumb names it {trailNamed}, its page {entry.ShownName}");
                ScryPanel.ClickTrailForTest(TrailPart.Level);

                // Out on the level, its chip frames the level again, the view moved off it first.
                if (Stage.Cutting && Stage.RoomGoneInto == null)
                {
                    var levelOpen = Stage.CutLevel;
                    Stage.ZoomBy(-3f, new Vector2(0.3f, 0.6f));
                    ScryPanel.ClickTrailForTest(TrailPart.Level);
                    if (Mathf.Abs(Stage.Zoom - 1f) > 0.001f || Stage.PanOffset.sqrMagnitude > 1e-6f || Stage.CutLevel != levelOpen || Stage.RoomGoneInto != null)
                        untrailed.Add($"{entry.Name}: the level's chip left the view at zoom {Numbers.Fixed(Stage.Zoom, 2)}, {Stage.CutLabel}");
                }

                // The breadcrumb's menu goes into the deepest room; its dungeon shows the whole of it, the roof on.
                ScryPanel.ChooseTrailMenuForTest(1);
                yield return null;
                if (Stage.RoomGoneInto != ways.Deepest) untrailed.Add($"{entry.Name}: the menu's Deepest went to {Stage.RoomGoneInto?.Room.Name ?? "no room"}");
                ScryPanel.ClickTrailForTest(TrailPart.Dungeon);
                yield return null;
                if (Stage.RoomGoneInto != null || Stage.Cutting) untrailed.Add($"{entry.Name}: the dungeon left {(Stage.RoomGoneInto != null ? "a room gone into" : "a level open")}");

                // A click goes into it, outlined whole however low the cut, framed afresh: a zoom
                // toward a point of the whole example before let go of.
                Stage.ZoomBy(-6f, new Vector2(0.3f, 0.6f));
                var zoomedTo = Stage.Zoom;
                var filmedOutlines = Stage.OutlinesFilmed;
                var deepestTop = deepest.Position.Y + deepest.Room.Size.Y / 2f;
                ScryPanel.DoubleClickPlanRoomForTest(deepest);
                // Its own outline, not the one of the room left a moment before, laid and filmed.
                yield return Until(() => Stage.RoomGoneInto == deepest && Stage.OutlineShown
                    && (!Stage.OutlinesPastCutForTest || Mathf.Abs(Stage.OutlineTopLaid - deepestTop) <= 0.01f && Stage.OutlinesFilmed > filmedOutlines), 3);
                if (Stage.RoomGoneInto != deepest) notIn.Add(entry.Name);
                else if (Mathf.Abs(Stage.Zoom - 1f) > 0.001f || Stage.PanOffset.sqrMagnitude > 1e-6f) unfresh.Add($"{entry.Name}: gone into at zoom {Numbers.Fixed(Stage.Zoom, 2)} (zoomed to {Numbers.Fixed(zoomedTo, 2)}), moved off {Numbers.Fixed(Stage.PanOffset.magnitude, 2)} m");
                else
                {
                    if (Stage.OutlinesPastCutForTest && (Mathf.Abs(Stage.OutlineTopLaid - deepestTop) > 0.01f || Stage.OutlinesFilmed == filmedOutlines))
                        unwhole.Add($"{entry.Name}: {deepest.Room.Name} gone into, outlined to {Numbers.Fixed(Stage.OutlineTopLaid, 1)} m of its top at {Numbers.Fixed(deepestTop, 1)} m, outlines filmed {Numbers.Count(Stage.OutlinesFilmed - filmedOutlines)} times");
                }
                // From above, the walkway to it in the accent along the halls of the rooms on the way.
                ScryPanel.ShowPlanViewForTest(PlanView.Above);
                var drawnAbove = ScryPanel.Drawn(PanelPart.PlanBox);
                yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnAbove + 1 && !Stage.Gliding, 5);
                yield return Picture(p, $"plan {entry.Name} Above deepest gone into");

                // In the room the ruler is its own: Shift and the wheel keep the cut within it, raised
                // past its top put its roof on and bring the rooms above back, and down from there
                // cut it again; the roof button too; none of it leaves the room.
                if (Stage.RoomGoneInto == deepest && Stage.RoomRulerNow is RoomRuler ruler)
                {
                    roomRulerTried++;
                    var aboveRooms = ways.Rooms.Where(r => Stage.ExampleRoomLook(r) == RoomLook.Away).ToList();
                    Stage.WheelCut(-1000f);
                    var rulerDrawn = ScryPanel.Drawn(PanelPart.Ruler);
                    yield return Until(() => ScryPanel.Drawn(PanelPart.Ruler) > rulerDrawn + 1, 3);
                    var lowest = Stage.CutAt;
                    var bar = ScryPanel.RulerBarForTest;
                    var barOn = bar.Bar >= bar.Top - 0.5f && bar.Bar <= bar.Bottom + 0.5f;
                    var shown = ScryPanel.RoomRulerShown;
                    Stage.WheelCut(1000f);
                    var roofOn = Stage.RoomRoofOn;
                    yield return Until(() => aboveRooms.All(r => Stage.ExampleRoomDrawn(r)?.Standing == true), 3);
                    var back = aboveRooms.Count(r => Stage.ExampleRoomDrawn(r)?.Standing == true);
                    if (aboveRooms.Count > 0 && back == aboveRooms.Count) yield return Picture(p, $"plan {entry.Name} deepest with its roof on, the rooms above back");
                    Stage.WheelCut(-0.5f);
                    var cutAgain = !Stage.RoomRoofOn && Stage.Cutting && Mathf.Abs(Stage.CutAt - lowest) < 0.01f;
                    Stage.ToggleRoof();
                    var buttonRoof = Stage.RoomRoofOn;
                    Stage.ToggleRoof();
                    var stillIn = Stage.RoomGoneInto == deepest;
                    if (!shown || Mathf.Abs(lowest - ruler.Least) > 0.01f || !barOn || !roofOn || back != aboveRooms.Count || !cutAgain || !buttonRoof || !stillIn)
                        unruled.Add($"{entry.Name}: ruler shown {shown}, cut down to {Numbers.Fixed(lowest, 1)} m of its least {Numbers.Fixed(ruler.Least, 1)} m, bar on the ruler {barOn}, roof on {roofOn}, {Numbers.Count(back)} of {Numbers.Count(aboveRooms.Count)} rooms above back, cut again {cutAgain}, roof button {buttonRoof}, still in the room {stillIn}");

                    // Cut from the side, there is no height to move: the wheel leaves the room as it is, and no ruler stands.
                    Stage.SetCutFromSide(true);
                    Stage.WheelCut(-3f);
                    var sideRulerDrawn = ScryPanel.Drawn(PanelPart.Ruler);
                    var sideFrame = Time.frameCount;
                    yield return Until(() => Time.frameCount >= sideFrame + 3, 2);
                    if (Stage.RoomGoneInto != deepest || Stage.RoomRulerNow != null || ScryPanel.Drawn(PanelPart.Ruler) > sideRulerDrawn)
                        unruled.Add($"{entry.Name}: cut from the side, still in the room {Stage.RoomGoneInto == deepest}, a ruler {Stage.RoomRulerNow != null || ScryPanel.Drawn(PanelPart.Ruler) > sideRulerDrawn}");
                    Stage.SetCutFromSide(false);
                }
                ScryPanel.ShowPlanViewForTest(PlanView.Tree);
                var drawnTree = ScryPanel.Drawn(PanelPart.PlanBox);
                yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnTree + 1 && !Stage.Gliding, 5);
                yield return Picture(p, $"plan {entry.Name} Tree deepest gone into");
                // Coming back out opens the level the room is on, the view on the room at the zoom it
                // had before it was gone into, a zoom in the room let go of, the room outlined.
                Stage.ZoomBy(3f, new Vector2(0.6f, 0.4f));
                var mark = Stage.ExampleMarkOfRoom(deepest);
                Stage.ComeOut();
                if (Stage.RoomGoneInto != null || !Stage.Cutting || Stage.CutLevel != mark || Mathf.Abs(Stage.Zoom - zoomedTo) > 0.001f || Stage.PanOffset.sqrMagnitude > 1e-6f || Stage.CameOutOf != deepest || !Stage.LevelFrameKept)
                    unreturned.Add($"{entry.Name}: out to mark {Numbers.Count(Stage.CutLevel + 1)} of {Numbers.Count(mark + 1)}, zoom {Numbers.Fixed(Stage.Zoom, 2)} for {Numbers.Fixed(zoomedTo, 2)}, moved off {Numbers.Fixed(Stage.PanOffset.magnitude, 2)} m, outlined {Stage.CameOutOf?.Room.Name ?? "none"}, frame kept {Stage.LevelFrameKept}");
                else
                {
                    yield return Until(() => !Stage.Gliding, 5);
                    yield return Picture(p, $"plan {entry.Name} came out of the deepest room");
                }
                Stage.OpenLevel(Stage.FloorHeights.Count);
            }

            // What was chosen is remembered in the panel's file.
            ScryPanel.ShowPlanViewForTest(PlanView.Tree);
            ScryPanel.GrowPlanForTest(true);
            var kept = ScryPanel.RememberedForTest();
            p.Check(kept.PlanView == PlanView.Tree && kept.PlanBig == true, "the plan box's view and size are remembered with the panel", $"{kept.PlanView}, grown {kept.PlanBig}");

            // Grown, every view names its rooms with the names switch on, and none with it off; remembered.
            var unnamed = new List<string>();
            var namesTried = 0;
            foreach (var view in new[] { PlanView.Above, PlanView.Levels, PlanView.Tree })
            {
                ScryPanel.ShowPlanViewForTest(view);
                ScryPanel.ShowNamesForTest(true);
                var named = ScryPanel.Drawn(PanelPart.PlanName);
                var boxes = ScryPanel.Drawn(PanelPart.PlanBox);
                yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > boxes + 2, 3);
                if (ScryPanel.Drawn(PanelPart.PlanBox) <= boxes + 2) continue;
                namesTried++;
                if (ScryPanel.Drawn(PanelPart.PlanName) == named) unnamed.Add($"{PlanBoxWords.ViewName(view)}: no name with the names on");
                ScryPanel.ShowNamesForTest(false);
                yield return null;
                named = ScryPanel.Drawn(PanelPart.PlanName);
                boxes = ScryPanel.Drawn(PanelPart.PlanBox);
                yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > boxes + 2, 3);
                if (ScryPanel.Drawn(PanelPart.PlanName) != named) unnamed.Add($"{PlanBoxWords.ViewName(view)}: {Numbers.Count(ScryPanel.Drawn(PanelPart.PlanName) - named)} names with the names off");
                if (view == PlanView.Levels) yield return Picture(p, "plan Levels grown names off");
            }
            var namesKept = ScryPanel.RememberedForTest().PlanNames;
            ScryPanel.ShowNamesForTest(wasNames);
            if (namesTried == 0) p.Note("the plan box was not drawn to switch its names");
            else p.Check(unnamed.Count == 0 && namesKept == false, "grown, every view names its rooms with the names switch on and none with it off, remembered with the panel", string.Join("; ", unnamed) + (namesKept == false ? "" : " not remembered"));

            ScryPanel.HoverPlanBoxForTest(null);
            ScryPanel.HoverStageForTest(null);
            ScryPanel.ShowBelowForTest(wasBelow);
            ScryPanel.ShowPlanViewForTest(wasView);
            ScryPanel.GrowPlanForTest(wasBig);
            ScryPanel.PlanFolded = wasFolded;
            Stage.Inside = wasInside;
            StageAsItWas();
            p.Check(unspotted.Count == 0, "each view of the plan box has a spot for every room, compact and grown", string.Join("; ", unspotted.Take(6)));
            p.Check(unwayed.Count == 0, "every view of the plan box draws every way between its rooms", string.Join("; ", unwayed.Take(6)));
            p.Check(unfound.Count == 0, "every room is found under the mouse at its spot, in the levels and the tree whole, from above unless a room drawn over it stands there", string.Join("; ", unfound.Take(6)));
            p.Check(unframed.Count == 0, "grown, the stage is framed beside the plan box; compact, in its middle", string.Join("; ", unframed.Take(6)));
            p.Check(unlaid.Count == 0, "the plan box's views stand left, grow and fold beside them in its top right corner, a line under them, its own switches over the plan's top right corner under it, the names only grown, joined where both stand, none over another", string.Join("; ", unlaid.Take(6)));
            p.Check(unlevelled.Count == 0, "the Levels view shows as many levels as the rooms' grounds lay out", string.Join("; ", unlevelled));
            p.Check(unlitBox.Count == 0, "a room under the mouse in the plan box is framed there alone and named, the way drawn to it, and outlined on the stage", string.Join("; ", unlitBox.Take(6)));
            p.Check(unlitStage.Count == 0, "a room under the mouse on the stage is framed in the plan box alone and named in every view, the way drawn to it, and outlined on the stage", string.Join("; ", unlitStage.Take(6)));
            p.Check(stayedLit.Count == 0, "the room lit goes out as the mouse leaves it", string.Join("; ", stayedLit));
            p.Check(slowFollow.Count == 0, "going from room to room on the stage, the outline and the plan box follow within three frames", string.Join("; ", slowFollow));
            p.Check(foundBelow > 0 && unchosen.Count == 0, "with a floor opened, a room dimmed below it is found under the mouse on the stage and outlined", foundBelow == 0 ? "no room dimmed below a floor was found in any dungeon" : string.Join("; ", unchosen));
            p.Check(switchTried > 0 && unswitched.Count == 0, "the box's switch leaves the rooms below the room gone into out of the plan from above, its ways down marked, each mark finding the room it leads to, and back, the stage showing them as before; remembered with the panel", switchTried == 0 ? "no entrance had rooms below it to try the switch on" : string.Join("; ", unswitched));
            p.Check(noWayBack.Count == 0, "in a room, the stage's breadcrumb ends at the room, after its dungeon and level", string.Join("; ", noWayBack));
            p.Check(untrailed.Count == 0, "the breadcrumb names the dungeon as its page does, its level frames the level again, its menu goes into the deepest room, and its dungeon shows the whole of it with the roof on", string.Join("; ", untrailed));
            p.Check(roomRulerTried > 0 && unruled.Count == 0, "in a room the ruler is its own: Shift and the wheel keep the cut within it, past its top its roof on and the rooms above back, down from there cut again, the roof button too, never leaving the room; cut from the side, none", roomRulerTried == 0 ? "no deepest room was cut from above" : string.Join("; ", unruled.Take(6)));
            p.Check(unfresh.Count == 0, "going into a room frames it afresh, a zoom or drag on what was framed before let go of", string.Join("; ", unfresh));
            p.Check(unreturned.Count == 0, "coming back out opens the level the room is on, the view on it at the zoom from before it was gone into, the room outlined", string.Join("; ", unreturned));
            p.Check(unwheeled.Count == 0, "Shift and the wheel open the top floor from the roof and take the cut no lower than the lowest floor", string.Join("; ", unwheeled));
            p.Check(apartCaps.Count == 0, "with a floor opened, each end cap and divider is drawn as the room it closes", string.Join("; ", apartCaps));
            p.Check(unwalked.Count == 0, "from above, every way runs along its rooms' walls inside them, and the walkway to a room lit runs from the entrance's middle to its own", string.Join("; ", unwalked.Take(6)));
            p.Check(capsFound.Count == 0, "the mouse on an end cap or divider on the stage finds the room it closes", string.Join("; ", capsFound));
            p.Check(unwhole.Count == 0, "the outlines of the room gone into and of the room lit stand their whole box however low the cut", string.Join("; ", unwhole));
            p.Check(notIn.Count == 0, "a click on a room in the plan box goes into it", string.Join("; ", notIn));
        }

        /// <summary>Whether a point stands on a room's floor as seen from above, its doorways' width of leeway at its walls.</summary>
        private static bool InRoom(PlacedRoom room, Vec3 point)
        {
            var local = room.Rotation.Inverse() * (point - room.Position);
            return Mathf.Abs(local.X) <= room.Room.Size.X / 2f + RoomWalkway.Near && Mathf.Abs(local.Z) <= room.Room.Size.Z / 2f + RoomWalkway.Near;
        }

        /// <summary>Whether a line runs along one of a room's walls, as seen from above.</summary>
        private static bool AlongWalls(PlacedRoom room, Vec3 a, Vec3 b)
        {
            var d = room.Rotation.Inverse() * (b - a);
            return Mathf.Abs(d.X) < 0.01f || Mathf.Abs(d.Z) < 0.01f;
        }

        /// <summary>
        /// What is amiss with a room lit from the mouse: it should be framed in the plan box alone,
        /// named in its caption, the way from the entrance drawn to it, and outlined on the stage;
        /// null when nothing is.
        /// </summary>
        private static string LitMiss(PlacedRoom room)
        {
            var amiss = new List<string>();
            var framed = ScryPanel.PlanBoxLitShown;
            if (framed.Count != 1 || framed[0] != room) amiss.Add("framed in the box: " + (framed.Count == 0 ? "none" : string.Join(", ", framed.Select(r => r.Room.Name))));
            if (ScryPanel.PlanBoxRouteShown != room) amiss.Add("way drawn to " + (ScryPanel.PlanBoxRouteShown?.Room.Name ?? "none"));
            if (Stage.LitRoom != room || !Stage.LitOutlineShown) amiss.Add("outlined on the stage: " + (Stage.LitOutlineShown ? Stage.LitRoom?.Room.Name : "none"));
            return amiss.Count == 0 ? null : room.Room.Name + ": " + string.Join("; ", amiss);
        }

        /// <summary>Whether the stage's breadcrumb was last drawn ending at a room gone into, lit, after its dungeon and its level.</summary>
        private static bool TrailNamesRoom(PlacedRoom room)
        {
            var trail = ScryPanel.TrailForTest;
            return trail.Count >= 2 && trail[0].Part == TrailPart.Dungeon && trail[trail.Count - 1].Part == TrailPart.Room && trail[trail.Count - 1].Current
                   && trail[trail.Count - 1].Text == ScryPanel.PlanBoxRoomNameForTest(X, room);
        }

        /// <summary>Whether a point of the stage's picture lies well inside it, not at its edge.</summary>
        private static bool InPicture(Vector2? point) =>
            point.HasValue && point.Value.x >= 0.05f && point.Value.x <= 0.95f && point.Value.y >= 0.05f && point.Value.y <= 0.95f;

        /// <summary>Puts the plan box back as the player had it, after <see cref="PlanBoxShows"/>.</summary>
        private static void PlanBoxAsItWas()
        {
            ScryPanel.HoverPlanBoxForTest(null);
            ScryPanel.HoverStageForTest(null);
            ScryPanel.GrowPlanForTest(false);
            Stage.ComeOut();
        }
    }
}
