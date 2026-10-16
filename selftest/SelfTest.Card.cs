using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// The card of a dungeon's room gone into. For each reference dungeon, cut from above, its
        /// deepest room is gone into as a click does: the card stands and says where the room is
        /// as the ways have it, the creatures this copy rolled in it, counted, its chests and
        /// pickups, each at a chance the room's own page gives where that page was read, and a way
        /// chip for each way on. Hovering a way chip lights its room on the stage; clicking the
        /// first goes into it, and the card follows. A room with levels of its own offers them on
        /// its card. Whole dungeon comes back out and the card goes. Each card is pictured.
        /// </summary>
        private static IEnumerator RoomCardShows(Probe p)
        {
            var places = ReferenceDungeons.Select(n => X.Catalog.FirstOrDefault(e => e.Name == n && PlaceOf(e) != null && !PlaceOf(e).IsRoom)).Where(e => e != null).ToList();
            if (places.Count == 0) p.Skip("none of " + string.Join(", ", ReferenceDungeons) + " is in the catalog");
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            BigStage();
            var unshown = new List<string>();
            var misplaced = new List<string>();
            var miscounted = new List<string>();
            var misodds = new List<string>();
            var waysWrong = new List<string>();
            var unlit = new List<string>();
            var notThrough = new List<string>();
            var noLevels = new List<string>();
            var stayed = new List<string>();
            var oddsChecked = 0;
            foreach (var entry in places)
            {
                Select(entry);
                yield return Until(() => CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.ExampleFloorsToRead == 0 && Stage.CreaturesWaiting == 0, 60);
                var ways = Stage.ExampleWays;
                if (CopyOf(entry) == null || Stage.ExampleShown == null || ways == null)
                {
                    p.Note($"{entry.Name}: no example stood on the stage");
                    continue;
                }
                Stage.SetCutFromSide(false);
                Stage.OpenLevel(Stage.FloorHeights.Count);
                var room = ways.Deepest;
                if (room == null)
                {
                    p.Note($"{entry.Name}: no room to go into");
                    continue;
                }

                ScryPanel.ClickRoomForTest(X, room);
                var drawn = ScryPanel.Drawn(PanelPart.RoomCard);
                yield return Until(() => Stage.RoomGoneInto == room && ScryPanel.Drawn(PanelPart.RoomCard) > drawn + 1 && ScryPanel.RoomCardLines.Count > 0, 5);
                var name = $"{entry.Name}'s {room.Room.Name}";
                var lines = ScryPanel.RoomCardLines.ToList();
                if (lines.Count == 0)
                {
                    unshown.Add(name);
                    continue;
                }
                p.Note($"{name}'s card: " + string.Join(" | ", lines));

                // Where it is, as the ways and the rooms' grounds have it.
                var rise = Stage.ExampleRoomGround(room) - Stage.ExampleRoomGround(ways.Entrance);
                var where = RoomCardWords.Where(rise, ways.StepsIn(room), ways.WaysOf(room).Count);
                if (lines.Count < 2 || lines[1] != where) misplaced.Add($"{name}: \"{(lines.Count > 1 ? lines[1] : "")}\", not \"{where}\"");

                // The creatures rolled, counted together, every one of them on the card unless the list was cut short.
                var creatures = Section(lines, RoomCardWords.Creatures, RoomCardWords.Loot);
                var rolled = Stage.RoomCreatures(room).Count;
                if (!X.LeavingOut && !creatures.Any(l => l.StartsWith("and ", StringComparison.Ordinal)))
                {
                    var counted = creatures.Where(l => l != RoomCardWords.None).Sum(CountOf);
                    if (counted != rolled) miscounted.Add($"{name}: {Numbers.Count(counted)} on the card, {Numbers.Count(rolled)} rolled");
                }

                // Each chest and pickup at a chance its room's page gives, where the page was read.
                var page = PlaceOf(X.Find(EntryKeys.For(Kind.Location, room.Room.Name)))?.Contents;
                foreach (var (prefab, chance) in Stage.RoomLoot(room))
                {
                    if (page == null) break;
                    oddsChecked++;
                    if (!page.Parts.Any(part => part.Prefab == prefab && Math.Abs(part.Chance - chance) < 0.0005f))
                        misodds.Add($"{name}'s {prefab} at {DropWords.Share(chance)}, its page: " + string.Join(", ", page.Parts.Where(part => part.Prefab == prefab).Select(part => DropWords.Share(part.Chance))));
                }
                if (page == null) p.Note($"{name}: its page was not read, so its chests' chances go unchecked");

                // A chip for each way on.
                var chips = ScryPanel.RoomCardWays;
                if (chips.Count != ways.WaysOf(room).Count || chips.Any(w => !lines.Contains(w.Words)))
                    waysWrong.Add($"{name}: {Numbers.Count(chips.Count)} chips for {Numbers.Count(ways.WaysOf(room).Count)} ways");
                yield return Picture(p, $"room card {entry.Name} deepest room");

                if (chips.Count > 0)
                {
                    // Hovering the first chip lights its room on the stage.
                    var first = chips[0];
                    ScryPanel.LightCardWayForTest(first.To);
                    yield return Until(() => Stage.LitOutlineShown, 3);
                    if (!Stage.LitOutlineShown) unlit.Add($"{name}: {first.Words}");
                    else yield return Picture(p, $"room card {entry.Name} way lit");
                    ScryPanel.LightCardWayForTest(null);
                    yield return Until(() => !Stage.LitOutlineShown, 3);
                    if (Stage.LitOutlineShown) unlit.Add($"{name}: {first.Words} stayed lit");

                    // Clicking it goes into its room, and the card follows.
                    ScryPanel.ClickCardWayForTest(first);
                    drawn = ScryPanel.Drawn(PanelPart.RoomCard);
                    yield return Until(() => Stage.RoomGoneInto == first.To && ScryPanel.Drawn(PanelPart.RoomCard) > drawn + 1, 5);
                    var now = ScryPanel.RoomCardLines.ToList();
                    var whereNow = RoomCardWords.Where(Stage.ExampleRoomGround(first.To) - Stage.ExampleRoomGround(ways.Entrance), ways.StepsIn(first.To), ways.WaysOf(first.To).Count);
                    if (Stage.RoomGoneInto != first.To || now.Count < 2 || now[1] != whereNow) notThrough.Add($"{name} to {first.Words}");
                }

                // A room with levels of its own offers them, its ground among them.
                var tiered = ways.Rooms.FirstOrDefault(r => Stage.ExampleRoomLevels(r).Count > 1);
                if (tiered != null)
                {
                    ScryPanel.ClickRoomForTest(X, tiered);
                    var levelChips = ScryPanel.Drawn(PanelPart.RoomCardLevel);
                    yield return Until(() => Stage.RoomGoneInto == tiered && ScryPanel.Drawn(PanelPart.RoomCardLevel) > levelChips + 1, 5);
                    var offered = ScryPanel.RoomCardLines.ToList();
                    var levels = Stage.ExampleRoomLevels(tiered);
                    var ground = Stage.ExampleRoomGround(tiered);
                    var missing = levels.Select(l => RoomCardWords.Level(l, ground)).Where(words => !offered.Contains(words)).ToList();
                    if (ScryPanel.Drawn(PanelPart.RoomCardLevel) <= levelChips || missing.Count > 0)
                        noLevels.Add($"{entry.Name}'s {tiered.Room.Name}: " + (missing.Count > 0 ? "no " + string.Join(", ", missing) : "no level chips drawn"));
                    else yield return Picture(p, $"room card {entry.Name} room with levels");
                }

                // Whole dungeon comes back out, and the card goes.
                ScryPanel.ClickCardWholeForTest();
                yield return Until(() => Stage.RoomGoneInto == null && ScryPanel.RoomCardLines.Count == 0, 5);
                if (Stage.RoomGoneInto != null || ScryPanel.RoomCardLines.Count > 0) stayed.Add(entry.Name);
            }
            Stage.Inside = wasInside;
            StageAsItWas();
            p.Note($"{Numbers.Count(oddsChecked)} chests' and pickups' chances checked against their rooms' pages");
            p.Check(unshown.Count == 0, "a room gone into shows its card", string.Join("; ", unshown));
            p.Check(misplaced.Count == 0, "the card says how far below the entrance, how many rooms in and how many ways", string.Join("; ", misplaced));
            p.Check(miscounted.Count == 0, "the card counts every creature this copy rolled in the room", string.Join("; ", miscounted));
            p.Check(misodds.Count == 0, "each chest and pickup on the card is at a chance its room's page gives", string.Join("; ", misodds));
            p.Check(waysWrong.Count == 0, "the card has a chip for each way on", string.Join("; ", waysWrong));
            p.Check(unlit.Count == 0, "hovering a way chip lights its room on the stage, and only while hovered", string.Join("; ", unlit));
            p.Check(notThrough.Count == 0, "a way chip goes into its room, and the card follows", string.Join("; ", notThrough));
            p.Check(noLevels.Count == 0, "a room with levels of its own offers each on its card", string.Join("; ", noLevels));
            p.Check(stayed.Count == 0, "Whole dungeon comes back out and the card goes", string.Join("; ", stayed));
        }

        /// <summary>The card's lines between two headings.</summary>
        private static List<string> Section(List<string> lines, string from, string to)
        {
            var start = lines.IndexOf(from);
            var end = lines.IndexOf(to);
            return start < 0 || end <= start ? new List<string>() : lines.GetRange(start + 1, end - start - 1);
        }

        /// <summary>How many a line counts: its "×3", else one.</summary>
        private static int CountOf(string line)
        {
            var times = line.LastIndexOf('×');
            return times >= 0 && Stored.TryCount(line.Substring(times + 1), out var count) ? count : 1;
        }

        /// <summary>Comes out of a room and puts out a room lit from the card, after <see cref="RoomCardShows"/>.</summary>
        private static void RoomCardAsItWas()
        {
            ScryPanel.LightCardWayForTest(null);
            Stage.ComeOut();
        }
    }
}
