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
        /// below rising into a level stand whole with it, notes how long the films take, and
        /// pictures each mark.
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
                        costs.Add($"{words} {Numbers.Fixed(AverageFilmMs, 1)} ms in {Numbers.Count(films)} {(films == 1 ? "film" : "films")}" + (risen.Count > 0 ? $", {Numbers.Count(risen.Count)} risen from below: {string.Join(", ", risen.Take(4))}" : ""));
                        yield return Picture(p, $"levels {entry.Name} {words}");
                    }
                    p.Note($"{entry.Name}: {string.Join("; ", costs)}");
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
