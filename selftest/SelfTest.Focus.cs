using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// A room of a dungeon's example is gone into with a click. For each reference dungeon,
        /// with the roof on, its deepest room (<see cref="DungeonWays.Deepest"/>) is clicked as the
        /// mouse does: it is gone into, the stage is cut at head height over its ground, each room
        /// stands, is dimmed or is put away as its ground has it (<see cref="RoomFocus.Look"/>),
        /// and the camera has the room's middle in view; pictured. Coming back out, as Esc does,
        /// opens the level the room is on, the room outlined.
        /// </summary>
        private static IEnumerator GoesIntoRooms(Probe p)
        {
            var places = ReferenceDungeons.Select(n => X.Catalog.FirstOrDefault(e => e.Name == n && PlaceOf(e) != null && !PlaceOf(e).IsRoom)).Where(e => e != null).ToList();
            if (places.Count == 0) p.Skip("none of " + string.Join(", ", ReferenceDungeons) + " is in the catalog");
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            BigStage();
            var notIn = new List<string>();
            var miscut = new List<string>();
            var misdrawn = new List<string>();
            var unseen = new List<string>();
            var cameOut = new List<string>();
            var unmarked = new List<string>();
            var unlevelled = new List<string>();
            foreach (var entry in places)
            {
                Select(entry);
                yield return Until(() => CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.ExampleFloorsToRead == 0 && Stage.CreaturesWaiting == 0, 60);
                if (CopyOf(entry) == null || Stage.ExampleShown == null)
                {
                    p.Note($"{entry.Name}: no example stood on the stage");
                    continue;
                }
                Stage.SetCutFromSide(false);
                Stage.OpenLevel(Stage.FloorHeights.Count);
                var rooms = Stage.ExampleShown.Rooms;
                var deepest = DungeonWays.Of(rooms).Deepest;
                if (deepest == null)
                {
                    p.Note($"{entry.Name}: no room to go into");
                    continue;
                }

                ScryPanel.DoubleClickRoomForTest(X, deepest);
                yield return Until(() => Stage.RoomGoneInto == deepest && Stage.ExampleRoomsKept, 5);
                yield return Until(() => !Stage.Gliding, 5);
                var name = $"{entry.Name}'s {deepest.Room.Name}";
                if (Stage.RoomGoneInto != deepest) notIn.Add(name);

                var ground = Stage.ExampleRoomGround(deepest);
                var cut = Stage.RoomCut(deepest);
                if (!Stage.Cutting || Math.Abs(Stage.CutAt - cut) > 0.01f) miscut.Add($"{name} cut at {Numbers.Fixed(Stage.CutAt, 2)} m, not {Numbers.Fixed(cut, 2)}");

                var wrong = 0;
                int whole = 0, near = 0, below = 0, away = 0;
                foreach (var room in rooms.Where(r => !r.Room.EndCap && !r.Room.Divider))
                {
                    // A room above is back, half dimmed, once the cut is over its ground; one over the room gone into only once the cut is raised from where the room was first cut too.
                    var roomGround = Stage.ExampleRoomGround(room);
                    var look = RoomFocus.Look(room == deepest, roomGround, ground);
                    if (!Stage.CutFromSide) look = RoomFocus.WithCut(look, roomGround, Stage.ExampleCutForTest, Stage.ExampleCutFromForTest, room != deepest && PlanLevels.Covers(room, deepest));
                    var drawn = Stage.ExampleRoomDrawn(room);
                    if (drawn == null) continue;
                    var (standing, keep) = drawn.Value;
                    var expected = look == RoomLook.Near ? DimColour.NearShare : look == RoomLook.Below ? DimColour.Share : (float?)null;
                    if (standing != (look != RoomLook.Away) || (standing && keep != expected)) wrong++;
                    if (look == RoomLook.Whole) whole++;
                    else if (look == RoomLook.Near) near++;
                    else if (look == RoomLook.Below) below++;
                    else away++;
                }
                if (wrong > 0) misdrawn.Add($"{name}: {Numbers.Count(wrong)} rooms not as their ground has them");

                var view = Stage.ExampleRoomInView(deepest);
                if (!(view is UnityEngine.Vector3 v && v.z > 0f && v.x > 0.05f && v.x < 0.95f && v.y > 0.05f && v.y < 0.95f)) unseen.Add($"{name} at " + (view is UnityEngine.Vector3 w ? Numbers.Point(w.x, w.y, 2) + $", {Numbers.Fixed(w.z, 1)} m ahead" : "no view"));
                p.Note($"{name}: ground {Numbers.Fixed(ground, 1)} m, cut {Numbers.Fixed(cut, 1)} m; {Numbers.Count(whole)} whole, {Numbers.Count(near)} half dimmed, {Numbers.Count(below)} dimmed, {Numbers.Count(away)} put away");
                yield return Picture(p, $"go into {entry.Name} deepest room");

                // Its outline: the amber it adds to the film, against the same film without it; counted
                // by colour, as water or flames moving between the two films change pixels too.
                yield return Until(() => Stage.OutlineShown, 3);
                var shown = Stage.OutlineShown;
                var filmed = Stage.Filmed;
                yield return Until(() => Stage.Filmed >= filmed + 2, 3);
                var withLine = StageColours();
                Stage.OutlineAwayForTest(true);
                filmed = Stage.Filmed;
                yield return Until(() => Stage.Filmed >= filmed + 2, 3);
                var withoutLine = StageColours();
                Stage.OutlineAwayForTest(false);
                var (changed, average) = withLine != null && withoutLine != null ? AccentPixels.Changed(withLine, withoutLine, 30) : (0, -1);
                var amber = withLine != null && withoutLine != null ? AccentPixels.Count(withLine, Accent, 40) - AccentPixels.Count(withoutLine, Accent, 40) : 0;
                var colour = average < 0 ? "none" : $"red {Numbers.Count((average >> 16) & 0xFF)}, green {Numbers.Count((average >> 8) & 0xFF)}, blue {Numbers.Count(average & 0xFF)}";
                p.Note($"{name}: outlined with {Stage.OutlineDrawnWith ?? "nothing"}, {Numbers.Count(amber)} amber pixels added; {Numbers.Count(changed)} pixels changed in all, on average {colour}");
                if (!shown || amber < 100) unmarked.Add($"{name}: {(shown ? "shown" : "not shown")}, {Numbers.Count(amber)} amber pixels added");

                // Coming back out, as Esc does: the level the room is on opened, the roof on for a room on none.
                var itsMark = Stage.ExampleMarkOfRoom(deepest);
                Stage.ComeOut();
                yield return Until(() => Stage.RoomGoneInto == null && Stage.ExampleRoomsKept, 5);
                if (Stage.RoomGoneInto != null || Stage.CutLevel != (itsMark >= 0 ? itsMark : Stage.FloorHeights.Count) || Stage.CameOutOf != deepest)
                    cameOut.Add($"{entry.Name}: {Stage.CutLabel}, its level's mark {(itsMark >= 0 ? Numbers.Count(itsMark) : "none")}, {(Stage.CameOutOf == deepest ? "outlined" : "not outlined")}");

                // A room with levels of its own: each is opened in turn, the cut at head height over it.
                var tiered = rooms.FirstOrDefault(r => !r.Room.EndCap && !r.Room.Divider && Stage.ExampleRoomLevels(r).Count > 1);
                if (tiered == null)
                {
                    p.Note($"{entry.Name}: no room of this layout has levels of its own");
                    continue;
                }
                var levels = Stage.ExampleRoomLevels(tiered);
                ScryPanel.DoubleClickRoomForTest(X, tiered);
                yield return Until(() => Stage.RoomGoneInto == tiered && Stage.ExampleRoomsKept, 5);
                foreach (var level in levels)
                {
                    Stage.OpenRoomLevel(level);
                    yield return Until(() => !Stage.Gliding, 5);
                    var expected = Stage.RoomLevelCut(tiered, level);
                    if (Stage.RoomLevelOpen != level || Math.Abs(Stage.CutAt - expected) > 0.01f)
                        unlevelled.Add($"{entry.Name}'s {tiered.Room.Name} at {Numbers.Fixed(level, 1)} m cut at {Numbers.Fixed(Stage.CutAt, 2)} m, not {Numbers.Fixed(expected, 2)}");
                }
                p.Note($"{entry.Name}'s {tiered.Room.Name}: levels at " + string.Join(", ", levels.Select(l => Numbers.Fixed(l, 1) + " m")));
                yield return Picture(p, $"go into {entry.Name} room's top level");
                Stage.ComeOut();
                yield return Until(() => Stage.RoomGoneInto == null && Stage.ExampleRoomsKept, 5);
            }
            Stage.Inside = wasInside;
            StageAsItWas();
            p.Check(unlevelled.Count == 0, "each level of a room gone into opens, cut at head height over it", string.Join("; ", unlevelled));
            p.Check(notIn.Count == 0, "a click on a dungeon's room goes into it", string.Join("; ", notIn));
            p.Check(miscut.Count == 0, "the stage is cut at head height over the room's ground", string.Join("; ", miscut));
            p.Check(misdrawn.Count == 0, "rooms on its level stand half dimmed, those below dimmed, those above put away until the cut stands over their ground", string.Join("; ", misdrawn));
            p.Check(unseen.Count == 0, "the camera has the room gone into in view", string.Join("; ", unseen));
            p.Check(unmarked.Count == 0, "the room gone into is outlined in amber", string.Join("; ", unmarked));
            p.Check(cameOut.Count == 0, "coming back out opens the level the room is on, the room outlined", string.Join("; ", cameOut));
        }

        /// <summary>Comes out of a room gone into, its outline back, after <see cref="GoesIntoRooms"/>.</summary>
        private static void ComeOutOfRoom()
        {
            Stage.OutlineAwayForTest(false);
            Stage.ComeOut();
        }

        /// <summary>The accent colour the outline is drawn in, as 0xRRGGBB.</summary>
        private static int Accent => (Channel(Stage.OutlineColour.r) << 16) | (Channel(Stage.OutlineColour.g) << 8) | Channel(Stage.OutlineColour.b);

        private static int Channel(float share) => (int)System.Math.Round(share * 255f);
    }
}
