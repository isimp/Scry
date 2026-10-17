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
            var unlevelled = new List<string>();
            var unlitBox = new List<string>();
            var unlitStage = new List<string>();
            var stayedLit = new List<string>();
            var slowFollow = new List<string>();
            var unchosen = new List<string>();
            var unwhole = new List<string>();
            var foundBelow = 0;
            var unfresh = new List<string>();
            var unwheeled = new List<string>();
            var apartCaps = new List<string>();
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

                    // Shift and the wheel: down with the roof on, the top floor opened; far down, no lower than the lowest.
                    Stage.OpenLevel(Stage.FloorHeights.Count);
                    Stage.WheelCut(-0.75f);
                    var topOpened = Stage.Cutting && Stage.CutLevel == 0;
                    Stage.WheelCut(-1000f);
                    if (!topOpened || Stage.CutLevel != Stage.FloorHeights.Count - 1) unwheeled.Add($"{entry.Name}: top floor opened {topOpened}, at floor {Numbers.Count(Stage.CutLevel + 1)} of {Numbers.Count(Stage.FloorHeights.Count)} after far down");
                    Stage.OpenLevel(Stage.FloorHeights.Count);
                }

                // A click goes into it, outlined whole however low the cut, framed afresh: a zoom
                // toward a point of the whole example before let go of.
                Stage.ZoomBy(-6f, new Vector2(0.3f, 0.6f));
                var zoomedTo = Stage.Zoom;
                var filmedOutlines = Stage.OutlinesFilmed;
                ScryPanel.ClickPlanRoomForTest(deepest);
                yield return Until(() => Stage.RoomGoneInto == deepest && Stage.OutlineShown, 3);
                if (Stage.RoomGoneInto != deepest) notIn.Add(entry.Name);
                else if (Mathf.Abs(Stage.Zoom - 1f) > 0.001f || Stage.PanOffset.sqrMagnitude > 1e-6f) unfresh.Add($"{entry.Name}: gone into at zoom {Numbers.Fixed(Stage.Zoom, 2)} (zoomed to {Numbers.Fixed(zoomedTo, 2)}), moved off {Numbers.Fixed(Stage.PanOffset.magnitude, 2)} m");
                else
                {
                    var deepestTop = deepest.Position.Y + deepest.Room.Size.Y / 2f;
                    if (Stage.OutlinesPastCutForTest && (Mathf.Abs(Stage.OutlineTopLaid - deepestTop) > 0.01f || Stage.OutlinesFilmed == filmedOutlines))
                        unwhole.Add($"{entry.Name}: {deepest.Room.Name} gone into, outlined to {Numbers.Fixed(Stage.OutlineTopLaid, 1)} m of its top at {Numbers.Fixed(deepestTop, 1)} m, outlines filmed {Numbers.Count(Stage.OutlinesFilmed - filmedOutlines)} times");
                }
                ScryPanel.ShowPlanViewForTest(PlanView.Tree);
                var drawnTree = ScryPanel.Drawn(PanelPart.PlanBox);
                yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnTree + 1 && !Stage.Gliding, 5);
                yield return Picture(p, $"plan {entry.Name} Tree deepest gone into");
                // Coming back out frames the whole example afresh, a zoom toward a point of the room let go of.
                Stage.ZoomBy(3f, new Vector2(0.6f, 0.4f));
                Stage.ComeOut();
                if (Mathf.Abs(Stage.Zoom - 1f) > 0.001f || Stage.PanOffset.sqrMagnitude > 1e-6f) unfresh.Add($"{entry.Name}: come out at zoom {Numbers.Fixed(Stage.Zoom, 2)}, moved off {Numbers.Fixed(Stage.PanOffset.magnitude, 2)} m");
            }

            // What was chosen is remembered in the panel's file.
            ScryPanel.ShowPlanViewForTest(PlanView.Tree);
            ScryPanel.GrowPlanForTest(true);
            var kept = ScryPanel.RememberedForTest();
            p.Check(kept.PlanView == PlanView.Tree && kept.PlanBig == true, "the plan box's view and size are remembered with the panel", $"{kept.PlanView}, grown {kept.PlanBig}");

            ScryPanel.HoverPlanBoxForTest(null);
            ScryPanel.HoverStageForTest(null);
            ScryPanel.ShowPlanViewForTest(wasView);
            ScryPanel.GrowPlanForTest(wasBig);
            ScryPanel.PlanFolded = wasFolded;
            Stage.Inside = wasInside;
            StageAsItWas();
            p.Check(unspotted.Count == 0, "each view of the plan box has a spot for every room, compact and grown", string.Join("; ", unspotted.Take(6)));
            p.Check(unwayed.Count == 0, "every view of the plan box draws every way between its rooms", string.Join("; ", unwayed.Take(6)));
            p.Check(unfound.Count == 0, "every room is found under the mouse at its spot, in the levels and the tree whole, from above unless a room drawn over it stands there", string.Join("; ", unfound.Take(6)));
            p.Check(unframed.Count == 0, "grown, the stage is framed beside the plan box; compact, in its middle", string.Join("; ", unframed.Take(6)));
            p.Check(unlevelled.Count == 0, "the Levels view shows as many levels as the rooms' grounds lay out", string.Join("; ", unlevelled));
            p.Check(unlitBox.Count == 0, "a room under the mouse in the plan box is framed there alone and named, the way drawn to it, and outlined on the stage", string.Join("; ", unlitBox.Take(6)));
            p.Check(unlitStage.Count == 0, "a room under the mouse on the stage is framed in the plan box alone and named in every view, the way drawn to it, and outlined on the stage", string.Join("; ", unlitStage.Take(6)));
            p.Check(stayedLit.Count == 0, "the room lit goes out as the mouse leaves it", string.Join("; ", stayedLit));
            p.Check(slowFollow.Count == 0, "going from room to room on the stage, the outline and the plan box follow within three frames", string.Join("; ", slowFollow));
            p.Check(foundBelow > 0 && unchosen.Count == 0, "with a floor opened, a room dimmed below it is found under the mouse on the stage and outlined", foundBelow == 0 ? "no room dimmed below a floor was found in any dungeon" : string.Join("; ", unchosen));
            p.Check(unfresh.Count == 0, "going into a room and coming back out frame it afresh, a zoom or drag on what was framed before let go of", string.Join("; ", unfresh));
            p.Check(unwheeled.Count == 0, "Shift and the wheel open the top floor from the roof and take the cut no lower than the lowest floor", string.Join("; ", unwheeled));
            p.Check(apartCaps.Count == 0, "with a floor opened, each end cap and divider is drawn as the room it closes", string.Join("; ", apartCaps));
            p.Check(unwhole.Count == 0, "the outlines of the room gone into and of the room lit stand their whole box however low the cut", string.Join("; ", unwhole));
            p.Check(notIn.Count == 0, "a click on a room in the plan box goes into it", string.Join("; ", notIn));
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
            if (ScryPanel.PlanBoxCaptionShown != ScryPanel.PlanBoxRoomNameForTest(X, room)) amiss.Add("caption " + ScryPanel.PlanBoxCaptionShown);
            if (ScryPanel.PlanBoxRouteShown != room) amiss.Add("way drawn to " + (ScryPanel.PlanBoxRouteShown?.Room.Name ?? "none"));
            if (Stage.LitRoom != room || !Stage.LitOutlineShown) amiss.Add("outlined on the stage: " + (Stage.LitOutlineShown ? Stage.LitRoom?.Room.Name : "none"));
            return amiss.Count == 0 ? null : room.Room.Name + ": " + string.Join("; ", amiss);
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
