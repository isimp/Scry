using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// The whole of a dungeon's example opened from above by the plan's levels, a level filmed
        /// in as many cuts as it needs. For each reference dungeon, and the sealed tower and a
        /// forest crypt checked beside them, it tells how its levels split into groups by its
        /// rooms' measured ceilings and how many rooms keep their roof, opens every mark, checks
        /// that the ruler has a mark for each level, that each mark stands its rooms whole, that a
        /// level of several groups is filmed in as many films and that the tall rooms of the levels
        /// below rising into a level stand whole with it, that a room of a level above is put away
        /// only where it stands over the level or its floor is not under the cut, that opening
        /// another level keeps the view on the spot it looked at, at its zoom, until Fit frames it
        /// afresh, and that Shift and the wheel move every
        /// film's cut of a level filmed in several, notes how long the films take, and pictures
        /// each mark.
        /// </summary>
        private static IEnumerator LevelsTried(Probe p)
        {
            var places = ReferenceDungeons.Concat(LevelsAlso).Select(n => X.Catalog.FirstOrDefault(e => e.Name == n && PlaceOf(e) != null && !PlaceOf(e).IsRoom)).Where(e => e != null).ToList();
            if (places.Count == 0) p.Skip("none of " + string.Join(", ", ReferenceDungeons) + " is in the catalog");
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            BigStage();

            var unmarked = new List<string>();
            var empty = new List<string>();
            var unfilmed = new List<string>();
            var unrisen = new List<string>();
            var unmoved = new List<string>();
            var misplaced = new List<string>();
            var unghosted = new List<string>();
            var unkept = new List<string>();
            var keptTried = 0;
            var uncarried = new List<string>();
            var unstepped = new List<string>();
            var movedTried = 0;
            var tried = 0;
            try
            {
                foreach (var entry in places)
                {
                    Select(entry);
                    yield return Until(() => CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.ExampleFloorsToRead == 0, 60);
                    if (CopyOf(entry) == null || Stage.ExampleWays == null)
                    {
                        p.Note($"{entry.Name}: no example stood on the stage");
                        continue;
                    }
                    Stage.SetCutFromSide(false);
                    Stage.ComeOut();

                    // How its levels split, by its rooms' measured ceilings.
                    var cuts = Stage.ExampleLevelCuts;
                    if (cuts == null) continue;
                    var split = new List<string>();
                    for (var level = 0; level < cuts.Levels; level++)
                    {
                        var groups = cuts.Groups.Where(g => g.Level == level).ToList();
                        if (groups.Count == 0) continue;
                        var roofed = groups.Count(g => !g.OpensAll);
                        split.Add($"level {Numbers.Count(level + 1)}: {Numbers.Count(groups.Sum(g => g.Rooms.Count))} rooms in {Numbers.Count(groups.Count)} {(groups.Count == 1 ? "group" : "groups")}" + (roofed > 0 ? $", {Numbers.Count(roofed)} too low to open" : ""));
                    }
                    p.Note($"{entry.Name}: {Numbers.Count(cuts.Levels)} levels, {Numbers.Count(cuts.Groups.Count)} groups one cut opens each; {string.Join("; ", split)}");
                    // Each group's rooms by name, ground and ceiling over the entrance's ground, and its cut, to tell why a level stands where it does.
                    var rooms = Stage.ExampleWays.Rooms;
                    var entranceGround = Stage.ExampleRoomGround(Stage.ExampleWays.Entrance);
                    for (var g = 0; g < cuts.Groups.Count; g++)
                    {
                        var group = cuts.Groups[g];
                        var named = group.Rooms.Select(i => $"{rooms[i].Room.Name} {Numbers.Fixed(Stage.ExampleRoomGround(rooms[i]) - entranceGround, 1)} m");
                        p.Note($"{entry.Name} level {Numbers.Count(group.Level + 1)} group {Numbers.Count(g + 1)}: cut {Numbers.Fixed(group.Cut - entranceGround, 1)} m under a ceiling at {Numbers.Fixed(group.Ceiling - entranceGround, 1)} m; {string.Join(", ", named)}");
                    }

                    yield return FilmFrames(3);
                    if (Stage.FloorHeights.Count != cuts.Levels) unmarked.Add($"{entry.Name}: {Numbers.Count(Stage.FloorHeights.Count)} marks for {Numbers.Count(cuts.Levels)} levels");
                    var costs = new List<string>();
                    for (var mark = 0; mark < Stage.FloorHeights.Count; mark++)
                    {
                        tried++;
                        Stage.OpenLevel(mark);
                        yield return LevelSettled();
                        yield return FilmFrames(10);
                        var words = Stage.ExampleMark(mark) ?? $"mark {Numbers.Count(mark + 1)}";
                        var whole = Stage.ExampleShown?.Rooms.Count(r => !r.Room.EndCap && !r.Room.Divider && Stage.ExampleRoomShown(r) == PlanRoomShown.Whole) ?? 0;
                        if (whole == 0) empty.Add($"{entry.Name} {words}");
                        var first = cuts.FirstOf(mark);
                        var level = first >= 0 ? cuts.Groups[first].Level : -1;
                        var groups = cuts.GroupsOf(level);
                        var films = Stage.LastFilms.Films;
                        if (films != System.Math.Max(1, groups)) unfilmed.Add($"{entry.Name} {words}: {Numbers.Count(films)} films for {Numbers.Count(groups)} groups");
                        // The tall rooms of the levels below rising into the level stand whole with it.
                        var risen = new List<string>();
                        for (var i = 0; i < rooms.Count; i++)
                        {
                            if (!cuts.ReachesLevel(i, level)) continue;
                            risen.Add(rooms[i].Room.Name);
                            if (Stage.ExampleRoomShown(rooms[i]) != PlanRoomShown.Whole) unrisen.Add($"{entry.Name} {words}: {rooms[i].Room.Name} {Stage.ExampleRoomShown(rooms[i])}");
                        }
                        // A room of a level above stands dimmed beside the level, put away where it stands over it or its floor is not under the cut.
                        var beside = new List<string>();
                        var openCut = Stage.ExampleOpenCutForTest;
                        for (var i = 0; i < rooms.Count; i++)
                        {
                            var its = cuts.LevelOf(i);
                            if (its < 0 || its >= level) continue;
                            var over = Enumerable.Range(0, rooms.Count).Any(q => cuts.StandsWith(q, level) && PlanLevels.Covers(rooms[i], rooms[q]));
                            var want = RoomFocus.HidesFromAbove(over, Stage.ExampleRoomGround(rooms[i]), openCut) ? PlanRoomShown.None : PlanRoomShown.Faint;
                            var shown = Stage.ExampleRoomShown(rooms[i]);
                            if (shown != want) misplaced.Add($"{entry.Name} {words}: {rooms[i].Room.Name} {shown}, not {want}");
                            if (shown == PlanRoomShown.Faint) beside.Add(rooms[i].Room.Name);
                        }
                        costs.Add($"{words} {Numbers.Fixed(AverageFilmMs, 1)} ms in {Numbers.Count(films)} {(films == 1 ? "film" : "films")}" + (risen.Count > 0 ? $", {Numbers.Count(risen.Count)} risen from below: {string.Join(", ", risen.Take(4))}" : "") + (beside.Count > 0 ? $", {Numbers.Count(beside.Count)} of the levels above standing beside it: {string.Join(", ", beside.Take(4))}" : ""));
                        yield return Picture(p, $"levels {entry.Name} {words}");

                        // Every room put away stands as a ghost, its footprint at its floor.
                        var away = Stage.ExampleShown?.Rooms.Count(r => !r.Room.EndCap && !r.Room.Divider && Stage.ExampleRoomShown(r) == PlanRoomShown.None) ?? 0;
                        if (away > 0 && Stage.GhostsDrawn != away) unghosted.Add($"{entry.Name} {words}: {Numbers.Count(Stage.GhostsDrawn)} ghosts for {Numbers.Count(away)} rooms put away");

                        // Shift and the wheel raise the level's cut half a metre: its first group's film with
                        // it, every other as far as its own ceiling lets it.
                        if (films > 1 && Stage.FilmCutsForTest is List<float> before && before.Count == films)
                        {
                            var filmGroups = Enumerable.Range(0, cuts.Groups.Count).Where(g => cuts.Groups[g].Level == level).Reverse().ToList();
                            Stage.WheelCut(0.5f);
                            yield return null;
                            var after = Stage.FilmCutsForTest;
                            if (Stage.CutLevel == mark && after != null && after.Count == films)
                            {
                                movedTried++;
                                var told = new List<string>();
                                for (var f = 0; f < films; f++)
                                {
                                    var group = cuts.Groups[filmGroups[f]];
                                    var want = f == films - 1 ? 0.5f : group.CutBy(0.5f) - group.Cut;
                                    told.Add($"{Numbers.Fixed(after[f] - before[f], 2)} m of {Numbers.Fixed(want, 2)}");
                                    if (Mathf.Abs(after[f] - before[f] - want) > 0.01f) unmoved.Add($"{entry.Name} {words} film {Numbers.Count(f + 1)}: moved {Numbers.Fixed(after[f] - before[f], 2)} m for {Numbers.Fixed(want, 2)}");
                                }
                                p.Note($"{entry.Name} {words}, cut raised half a metre: films moved {string.Join(", ", told)}");
                            }
                            Stage.WheelCut(-0.5f);
                            yield return null;
                        }
                    }
                    p.Note($"{entry.Name}: {string.Join("; ", costs)}");

                    // Zoomed in on its top level, the next level opened keeps the spot looked at and
                    // the zoom, only the height following; Fit frames the level afresh.
                    if (Stage.FloorHeights.Count > 1)
                    {
                        keptTried++;
                        Stage.OpenLevel(0);
                        yield return LevelSettled();
                        Stage.ZoomBy(-3f);
                        var zoom = Stage.Zoom;
                        var before = Stage.FloorFrameForTest;
                        Stage.OpenLevel(1);
                        yield return LevelSettled();
                        var after = Stage.FloorFrameForTest;
                        if (!(before is Vector4 b) || !(after is Vector4 a)) unkept.Add($"{entry.Name}: no frame on a level");
                        else
                        {
                            var across = new Vector3(a.x - b.x, a.z - b.z, a.w - b.w).magnitude;
                            if (across > 0.01f || Mathf.Abs(Stage.Zoom - zoom) > 0.001f || Mathf.Approximately(a.y, b.y))
                                unkept.Add($"{entry.Name}: moved {Numbers.Fixed(across, 2)} across, zoom {Numbers.Fixed(zoom, 2)} became {Numbers.Fixed(Stage.Zoom, 2)}, height {Numbers.Fixed(b.y, 1)} to {Numbers.Fixed(a.y, 1)} m");
                        }
                        Stage.View("Fit");
                        yield return LevelSettled();
                        if (Stage.LevelFrameKept || Mathf.Abs(Stage.Zoom - 1f) > 0.001f) unkept.Add($"{entry.Name}: Fit kept the frame, zoom {Numbers.Fixed(Stage.Zoom, 2)}");

                        // In a room of the top level, Page Down past its ruler's lowest tick comes out to the
                        // level below, on the room; in one of the level below, Page Up past its roof to the top level.
                        var topRoom = rooms[cuts.Groups[cuts.FirstOf(0)].Rooms[0]];
                        Stage.GoInto(topRoom);
                        yield return LevelSettled();
                        for (var s = 0; s < 12 && Stage.RoomGoneInto != null; s++)
                        {
                            Stage.StepRoomCut(true);
                            yield return null;
                        }
                        if (Stage.RoomGoneInto != null || Stage.CutLevel != 1 || Stage.CameOutOf != topRoom) uncarried.Add($"{entry.Name}: down from {topRoom.Room.Name}, in a room {Stage.RoomGoneInto != null}, mark {Numbers.Count(Stage.CutLevel + 1)}");
                        var lowerRoom = rooms[cuts.Groups[cuts.FirstOf(1)].Rooms[0]];
                        Stage.GoInto(lowerRoom);
                        yield return LevelSettled();
                        for (var s = 0; s < 12 && Stage.RoomGoneInto != null; s++)
                        {
                            Stage.StepRoomCut(false);
                            yield return null;
                        }
                        if (Stage.RoomGoneInto != null || Stage.CutLevel != 0 || Stage.CameOutOf != lowerRoom) uncarried.Add($"{entry.Name}: up from {lowerRoom.Room.Name}, in a room {Stage.RoomGoneInto != null}, mark {Numbers.Count(Stage.CutLevel + 1)}");

                        // Back, as the mouse's back button, comes out of a room to the view it was gone
                        // into from, as it was left, the entry kept; Forward goes in again; Back after
                        // coming out goes back into the room.
                        var shownEntry = X.Selected;
                        Stage.OpenLevel(1);
                        yield return LevelSettled();
                        Stage.ZoomBy(-3f);
                        var zoomBefore = Stage.Zoom;
                        Stage.GoInto(lowerRoom);
                        yield return LevelSettled();
                        ScryPanel.Step(X, true);
                        yield return null;
                        if (Stage.RoomGoneInto != null || Stage.CutLevel != 1 || Mathf.Abs(Stage.Zoom - zoomBefore) > 0.001f || X.Selected != shownEntry)
                            unstepped.Add($"{entry.Name}: back from {lowerRoom.Room.Name} in a room {Stage.RoomGoneInto != null}, mark {Numbers.Count(Stage.CutLevel + 1)}, zoom {Numbers.Fixed(Stage.Zoom, 2)} for {Numbers.Fixed(zoomBefore, 2)}, entry {X.Selected?.Name}");
                        ScryPanel.Step(X, false);
                        yield return null;
                        if (Stage.RoomGoneInto != lowerRoom) unstepped.Add($"{entry.Name}: forward went to {Stage.RoomGoneInto?.Room.Name ?? "no room"}");
                        Stage.ComeOut();
                        yield return null;
                        ScryPanel.Step(X, true);
                        yield return null;
                        if (Stage.RoomGoneInto != lowerRoom || X.Selected != shownEntry) unstepped.Add($"{entry.Name}: back after coming out went to {Stage.RoomGoneInto?.Room.Name ?? "no room"}, entry {X.Selected?.Name}");
                        Stage.ComeOut();
                        yield return LevelSettled();
                    }
                    Stage.OpenLevel(Stage.FloorHeights.Count);
                }
            }
            finally
            {
                Stage.Inside = wasInside;
            }
            p.Check(tried > 0 && unmarked.Count == 0, "opened by the plan's levels, the ruler has a mark for each level", tried == 0 ? "no mark was tried" : string.Join("; ", unmarked));
            p.Check(empty.Count == 0, "every mark stands the rooms it opens whole", string.Join("; ", empty));
            p.Check(unfilmed.Count == 0, "a level one cut cannot open is filmed in as many films as it has groups", string.Join("; ", unfilmed));
            p.Check(unrisen.Count == 0, "a tall room of a level below rising into the level opened stands whole with it", string.Join("; ", unrisen));
            if (keptTried == 0) p.Note("no dungeon had two levels to keep the view across");
            else
            {
                p.Check(unkept.Count == 0, "opening another level keeps the view on the spot it looked at, at its zoom, until Fit frames the level afresh", string.Join("; ", unkept));
                p.Check(unstepped.Count == 0, "Back comes out of a room to the view it was gone into from, as it was left, Forward goes in again, the entry kept", string.Join("; ", unstepped));
                p.Check(uncarried.Count == 0, "Page Down past a room's lowest tick comes out to the level below, Page Up past its roof to the level above", string.Join("; ", uncarried));
            }
            p.Check(unghosted.Count == 0, "every room put away above a level stands as a ghost, its footprint at its floor", string.Join("; ", unghosted));
            p.Check(misplaced.Count == 0, "a room of a level above is put away only where it stands over the level or its floor is not under the cut, else it stands dimmed", string.Join("; ", misplaced));
            if (movedTried == 0) p.Note("no level of several cuts came up to move by hand");
            else p.Check(unmoved.Count == 0, "Shift and the wheel move every film's cut of a level together, each under its own ceiling", string.Join("; ", unmoved));
        }

        /// <summary>The dungeons whose floors are checked beside the reference dungeons, opened by the levels too.</summary>
        private static readonly string[] LevelsAlso = { "Hildir_plainsfortress", "Crypt2" };

        /// <summary>How long the stage's films took on average over the frames last counted, in milliseconds.</summary>
        private static double AverageFilmMs;

        /// <summary>Waits so many frames, averaging the stage's films' time over them into <see cref="AverageFilmMs"/>.</summary>
        private static IEnumerator FilmFrames(int count)
        {
            var sum = 0.0;
            var seen = 0;
            var last = Stage.Filmed;
            var start = Time.frameCount;
            while (Time.frameCount < start + count)
            {
                yield return null;
                if (Stage.Filmed == last) continue;
                last = Stage.Filmed;
                sum += Stage.LastFilms.Ms;
                seen++;
            }
            AverageFilmMs = seen > 0 ? sum / seen : 0.0;
        }

        /// <summary>Waits for the rooms to stand as the mark opened has them and the camera to come to rest.</summary>
        private static IEnumerator LevelSettled()
        {
            yield return Until(() => Stage.ExampleRoomsKept && !Stage.Gliding, 5);
        }
    }
}
