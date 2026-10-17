using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// The whole of a dungeon's example opened from above by the plan's levels, the two ways
        /// tried beside the floors: a group of rooms one cut opens at a time (steps), and a whole
        /// level filmed in as many cuts as it needs (films). For each reference dungeon it tells
        /// how its levels split into groups by its rooms' measured ceilings and how many rooms keep
        /// their roof, opens every mark each way, checks that the ruler has a mark for each group
        /// (steps) or each level (films), that each mark stands its rooms whole and that a level
        /// of several groups is filmed in as many films, notes how long the films take against
        /// the floors' one, and pictures each mark.
        /// </summary>
        private static IEnumerator LevelsTried(Probe p)
        {
            var places = ReferenceDungeons.Select(n => X.Catalog.FirstOrDefault(e => e.Name == n && PlaceOf(e) != null && !PlaceOf(e).IsRoom)).Where(e => e != null).ToList();
            if (places.Count == 0) p.Skip("none of " + string.Join(", ", ReferenceDungeons) + " is in the catalog");
            var wasInside = Stage.Inside;
            var wasMode = Stage.LevelsMode;
            Stage.Inside = true;
            BigStage();

            var unmarked = new List<string>();
            var empty = new List<string>();
            var unfilmed = new List<string>();
            var tried = 0;
            try
            {
                foreach (var entry in places)
                {
                    Stage.LevelsMode = InsideLevels.Floors;
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

                    // The floors' film, for its cost beside the others.
                    var floorsMs = 0.0;
                    if (Stage.FloorHeights.Count > 0)
                    {
                        Stage.OpenLevel(0);
                        yield return LevelSettled();
                        yield return FilmFrames(10);
                        floorsMs = AverageFilmMs;
                    }

                    foreach (var mode in new[] { InsideLevels.Steps, InsideLevels.Films })
                    {
                        Stage.LevelsMode = mode;
                        yield return FilmFrames(3);
                        var wanted = mode == InsideLevels.Steps ? cuts.Groups.Count : cuts.Levels;
                        if (Stage.FloorHeights.Count != wanted) unmarked.Add($"{entry.Name} {StageWords.LevelsChip(mode)}: {Numbers.Count(Stage.FloorHeights.Count)} marks for {Numbers.Count(wanted)}");
                        var costs = new List<string>();
                        for (var mark = 0; mark < Stage.FloorHeights.Count; mark++)
                        {
                            tried++;
                            Stage.OpenLevel(mark);
                            yield return LevelSettled();
                            yield return FilmFrames(10);
                            var words = Stage.ExampleMark(mark)?.Words ?? $"mark {Numbers.Count(mark + 1)}";
                            var whole = Stage.ExampleShown?.Rooms.Count(r => !r.Room.EndCap && !r.Room.Divider && Stage.ExampleRoomShown(r) == PlanRoomShown.Whole) ?? 0;
                            if (whole == 0) empty.Add($"{entry.Name} {StageWords.LevelsChip(mode)} {words}");
                            if (mode == InsideLevels.Films)
                            {
                                var level = cuts.Groups[cuts.FirstOf(mark)].Level;
                                var groups = cuts.GroupsOf(level);
                                var films = Stage.LastFilms.Films;
                                if (films != System.Math.Max(1, groups)) unfilmed.Add($"{entry.Name} {words}: {Numbers.Count(films)} films for {Numbers.Count(groups)} groups");
                            }
                            costs.Add($"{words} {Numbers.Fixed(AverageFilmMs, 1)} ms in {Numbers.Count(Stage.LastFilms.Films)} {(Stage.LastFilms.Films == 1 ? "film" : "films")}");
                            yield return Picture(p, $"levels {entry.Name} {StageWords.LevelsChip(mode)} {words}");
                        }
                        p.Note($"{entry.Name} {StageWords.LevelsChip(mode)}: {string.Join("; ", costs)}; the top floor by floors {Numbers.Fixed(floorsMs, 1)} ms");
                    }
                    Stage.LevelsMode = InsideLevels.Floors;
                    Stage.OpenLevel(Stage.FloorHeights.Count);
                }
            }
            finally
            {
                Stage.LevelsMode = wasMode;
                Stage.Inside = wasInside;
            }
            p.Check(tried > 0 && unmarked.Count == 0, "opened by the plan's levels, the ruler has a mark for each group stepped and for each level filmed", tried == 0 ? "no mark was tried" : string.Join("; ", unmarked));
            p.Check(empty.Count == 0, "every mark stands the rooms it opens whole", string.Join("; ", empty));
            p.Check(unfilmed.Count == 0, "a level opened whole that one cut cannot open is filmed in as many films as it has groups", string.Join("; ", unfilmed));
        }

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
