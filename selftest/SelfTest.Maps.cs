using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for the maps of dungeons a player is in or has been in: the world's
    /// own dungeon read from its saved rooms and shown in place of an example, and with the
    /// compendium on as far as it was walked, the player's room and arrow marked.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// A rolled example, saved as the game saves a dungeon's rooms (each room's name's hash, its
        /// place and its turn as angles) and with a room of a kind not read among them, handed in as
        /// the world's own: it stands as saved, room by room and turned the same, under a tag at the
        /// head of the breadcrumb, the menu switching to an example and back.
        /// </summary>
        private static IEnumerator ThisDungeon(Probe p)
        {
            var entry = Crypt();
            if (entry == null) p.Skip("there is no such location");
            // With the compendium off, as the fog would hide the rooms not walked.
            var noCompendium = new CompendiumView(X.Entries);
            noCompendium.Tell(new CompendiumBook(), CompendiumRule.Off);
            Session.ShowKnownInstead(noCompendium);
            Select(entry);
            Stage.Inside = true;
            yield return Until(() => ReferenceEquals(X.Compendium, noCompendium), 3);
            yield return Until(() => ExampleLayouts.Of(entry) && ExampleLayouts.Example != null && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.ExampleRoomsTotal > 0, 150);
            var rolled = ExampleLayouts.Of(entry) ? ExampleLayouts.Example : null;
            if (!p.Check(rolled != null && !ExampleLayouts.ShowsWorlds, "an example has been laid out")) yield break;

            var saved = SavedAsTheGameSaves(rolled);
            saved.Add(("not a room Scry has read".GetStableHashCode(), rolled.Rooms[0].Position, new Vec3(0f, 0f, 0f)));
            var world = new WorldDungeon { Saved = SavedRooms.Read(SavedRooms.Write(saved)), Generator = rolled.Site.Generator, Turn = rolled.Site.Turn };
            ExampleLayouts.StandInForTest(world);
            yield return Until(() => ExampleLayouts.ShowsWorlds && ExampleLayouts.Example != rolled && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 60);
            var shown = ExampleLayouts.Example;
            if (!p.Check(ExampleLayouts.ShowsWorlds && shown != rolled, "the world's own dungeon is shown in place of the example"))
            {
                ThisDungeonAsItWas();
                yield break;
            }
            p.Check(ExampleLayouts.Unread == 1 && shown.Rooms.Count == rolled.Rooms.Count, "each room stands by its hash, the one of a kind not read left out and counted",
                $"{Numbers.Count(shown.Rooms.Count)} of {Numbers.Count(rolled.Rooms.Count)} rooms, {Numbers.Count(ExampleLayouts.Unread)} not read");

            var off = new List<string>();
            for (var i = 0; i < Mathf.Min(shown.Rooms.Count, rolled.Rooms.Count); i++)
            {
                var a = rolled.Rooms[i];
                var b = shown.Rooms[i];
                var moved = (b.Position - a.Position).Length;
                var turnedX = (b.Rotation * new Vec3(1f, 0f, 0f) - a.Rotation * new Vec3(1f, 0f, 0f)).Length;
                var turnedZ = (b.Rotation * new Vec3(0f, 0f, 1f) - a.Rotation * new Vec3(0f, 0f, 1f)).Length;
                if (a.Room != b.Room || moved > 0.01f || turnedX > 0.001f || turnedZ > 0.001f)
                    off.Add($"{a.Room.Name} as {b.Room.Name}, {Numbers.Fixed(moved, 3)} m off, turned {Numbers.Fixed(Mathf.Max(turnedX, turnedZ), 4)} off");
                else if (a.Room.EndCap && a.JoinedTo != null && (b.JoinedTo == null || b.JoinedTo.Room != a.JoinedTo.Room || (b.JoinedTo.Position - a.JoinedTo.Position).Length > 0.01f))
                    off.Add($"end cap {a.Room.Name} joined to {b.JoinedTo?.Room.Name ?? "nothing"}, not {a.JoinedTo.Room.Name}");
            }
            p.Check(off.Count == 0, "every room stands where it was saved, turned as it was, each end cap joined to the room it closes", string.Join("; ", off.Take(6)));
            p.Check(DungeonWays.Of(shown.Rooms).Entrance?.Room == rolled.Rooms[0].Room, "its way in is the room saved first", DungeonWays.Of(shown.Rooms).Entrance?.Room.Name ?? "none");

            // The breadcrumb's head says so; the menu switches to an example and back.
            var trails = ScryPanel.Drawn(PanelPart.Trail);
            yield return Until(() => ScryPanel.Drawn(PanelPart.Trail) > trails + 1 && ScryPanel.TrailTaggedForTest, 5);
            if (p.Check(ScryPanel.TrailTaggedForTest, "a tag at the head of the breadcrumb says it is this dungeon")) yield return Picture(p, $"this dungeon {entry.Name} tagged");
            ScryPanel.ChooseTrailMenuForTest(2);
            yield return Until(() => !ExampleLayouts.ShowsWorlds && ExampleLayouts.Example != shown, 5);
            var example = ExampleLayouts.Example;
            trails = ScryPanel.Drawn(PanelPart.Trail);
            yield return Until(() => ScryPanel.Drawn(PanelPart.Trail) > trails + 1, 5);
            p.Check(!ExampleLayouts.ShowsWorlds && example != shown && !ScryPanel.TrailTaggedForTest, "the menu's Show an example lays out an example, the tag gone");
            ScryPanel.ChooseTrailMenuForTest(2);
            yield return Until(() => ExampleLayouts.ShowsWorlds, 5);
            p.Check(ExampleLayouts.ShowsWorlds && ExampleLayouts.Example != example && ExampleLayouts.Example.Rooms.Count == shown.Rooms.Count, "and its Show this dungeon brings the world's own back");
            ThisDungeonAsItWas();
        }

        /// <summary>An example's rooms as the game saves a dungeon's: each room's name's hash, its place and its turn as angles, in the order placed.</summary>
        private static List<(int Hash, Vec3 Position, Vec3 Angles)> SavedAsTheGameSaves(DungeonExample example) => example.Rooms.Select(r =>
        {
            var angles = new Quaternion(r.Rotation.X, r.Rotation.Y, r.Rotation.Z, r.Rotation.W).eulerAngles;
            return (r.Room.Name.GetStableHashCode(), r.Position, new Vec3(angles.x, angles.y, angles.z));
        }).ToList();

        /// <summary>
        /// A rolled example handed in as the world's own, with a made-up character for whom the
        /// compendium is on, on maps of its own: walked in its entrance and the room next to it, only
        /// those stand, their ways on to the rest marked; stood in the second, the breadcrumb ends at
        /// it and goes into it, the plan box and the stage mark where the player stands; peeking,
        /// every room stands, those not walked dimmed and dashed under the peek's warning.
        /// </summary>
        private static IEnumerator DungeonFogShows(Probe p)
        {
            var entry = Crypt();
            if (entry == null) p.Skip("there is no such location");
            var book = new CompendiumBook();
            book.Raise(entry.Key, KnownLevel.Known);
            var loose = new CompendiumView(X.Entries);
            loose.Tell(book, CompendiumRule.Loose);
            Session.ShowKnownInstead(loose);
            Select(entry);
            Stage.Inside = true;
            yield return Until(() => ReferenceEquals(X.Compendium, loose) && ExampleLayouts.Of(entry) && ExampleLayouts.Example != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 150);
            var rolled = ExampleLayouts.Of(entry) ? ExampleLayouts.Example : null;
            if (!p.Check(rolled != null && X.CompendiumOn, "an example has been laid out, the compendium on")) yield break;

            var ways = DungeonWays.Of(rolled.Rooms);
            var onward = ways.Entrance != null ? ways.WaysOf(ways.Entrance) : null;
            var next = onward != null && onward.Count > 0 ? onward[0] : null;
            if (!p.Check(next != null, "its entrance leads on to a room")) yield break;
            var first = rolled.Rooms.IndexOf(ways.Entrance);
            var second = rolled.Rooms.IndexOf(next);
            const string id = "self-test:fog";
            var maps = new DungeonMaps();
            var saved = SavedAsTheGameSaves(rolled);
            maps.Keep(id, PlaceOf(entry).Prefab, saved, new Vec3(0f, 0f, 0f), rolled.Site.Generator, rolled.Site.Turn);
            maps.Walk(id, first);
            maps.Walk(id, second);
            DungeonWalks.MapsInsteadForTest(maps);
            ExampleLayouts.StandInForTest(new WorldDungeon { Saved = saved, Generator = rolled.Site.Generator, Turn = rolled.Site.Turn, Id = id, Location = PlaceOf(entry).Prefab });
            yield return Until(() => ExampleLayouts.ShowsWorlds && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 60);
            var fogged = ExampleLayouts.Example;
            var shownAt = fogged.Rooms.Where(r => !r.Room.EndCap && !r.Room.Divider).Select(r => r.SavedAt).OrderBy(i => i).ToList();
            var rooms = rolled.Rooms.Count(r => !r.Room.EndCap && !r.Room.Divider);
            p.Check(ExampleLayouts.Fogged && shownAt.SequenceEqual(new[] { first, second }.OrderBy(i => i)), "with the compendium on, only the rooms walked stand", string.Join(", ", shownAt.Select(i => Numbers.Count(i))));
            p.Check(ExampleLayouts.Walked == (2, rooms), "it counts the rooms walked of all", $"{Numbers.Count(ExampleLayouts.Walked.Walked)} of {Numbers.Count(ExampleLayouts.Walked.Rooms)}");
            var open = ExampleLayouts.OpenWays;
            var walkedAt = new HashSet<int> { first, second };
            p.Check(open.Count > 0 && open.All(w => walkedAt.Contains(w.From.SavedAt) && !walkedAt.Contains(w.To.SavedAt)), "each way on to a room not walked is marked", $"{Numbers.Count(open.Count)} ways");
            var planned = ScryPanel.Drawn(PanelPart.PlanBox);
            yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > planned + 1, 5);
            p.Check(ScryPanel.PlanBoxOpenWaysDrawn > 0, "the plan box draws the ways on", $"{Numbers.Count(ScryPanel.PlanBoxOpenWaysDrawn)} drawn");
            // How much is walked stands in its own row under the plan, clear of the rooms, inside the box.
            var boxParts = ScryPanel.PlanBoxPartsForTest.ToList();
            var walkedRow = boxParts.Where(q => q.Part == "walked").Select(q => (Rect?)q.At).FirstOrDefault();
            var planArea = boxParts.Where(q => q.Part == "plan").Select(q => (Rect?)q.At).FirstOrDefault();
            var planBox = ScryPanel.PlanBoxRect;
            p.Check(walkedRow is Rect row && planArea is Rect plan && row.y >= plan.yMax - 0.5f && row.yMax <= planBox.yMax + 0.5f && row.x >= planBox.x && row.xMax <= planBox.xMax,
                "how much is walked stands in a row of its own under the plan, clear of its rooms", walkedRow is Rect r ? $"row {Said(r)}, plan {(planArea is Rect a ? Said(a) : "none")}, box {Said(planBox)}" : "no row");
            yield return Picture(p, $"fog {entry.Name} two rooms walked");

            // Stood in the second room: the breadcrumb ends at it and goes into it, the plan and the stage mark it.
            DungeonWalks.StandInForTest((id, second, rolled.Rooms[second].Position, 0f));
            var trails = ScryPanel.Drawn(PanelPart.Trail);
            yield return Until(() => ScryPanel.Drawn(PanelPart.Trail) > trails + 1 && ScryPanel.TrailForTest.Any(t => t.Part == TrailPart.You), 5);
            p.Check(ScryPanel.TrailForTest.Any(t => t.Part == TrailPart.You && t.Text.EndsWith(PlanBoxWords.YouMark, System.StringComparison.Ordinal)), "the breadcrumb ends at the room the player stands in", string.Join(" › ", ScryPanel.TrailForTest.Select(t => t.Text)));
            p.Check(ScryPanel.PlanBoxYouDrawn && Stage.YouMarkShown, "the plan box and the stage mark where the player stands", $"plan {ScryPanel.PlanBoxYouDrawn}, stage {Stage.YouMarkShown}");
            yield return Picture(p, $"fog {entry.Name} you are here");
            ScryPanel.ClickTrailForTest(TrailPart.You);
            yield return Until(() => Stage.RoomGoneInto != null && !Stage.Gliding, 5);
            trails = ScryPanel.Drawn(PanelPart.Trail);
            yield return Until(() => ScryPanel.Drawn(PanelPart.Trail) > trails + 1, 5);
            var trail = ScryPanel.TrailForTest;
            var into = trail.Count > 0 ? trail[trail.Count - 1] : default;
            p.Check(Stage.RoomGoneInto?.SavedAt == second && into.Part == TrailPart.Room && into.Text.EndsWith(PlanBoxWords.YouMark, System.StringComparison.Ordinal), "a click on it goes into it, marked as the player's", into.Text ?? "none");
            Stage.ComeOut();
            yield return Until(() => Stage.RoomGoneInto == null && !Stage.Gliding, 5);

            // Peeking, every room stands, those not walked dimmed and dashed under the peek's warning.
            X.Peeking = true;
            yield return Until(() => ExampleLayouts.Example != fogged && Stage.ExampleRoomsTotal == rolled.Rooms.Count && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 60);
            var peekedRoom = Stage.ExampleWays?.Rooms.FirstOrDefault(ExampleLayouts.IsPeeked);
            p.Check(ExampleLayouts.PeekedRooms == rooms - 2 && peekedRoom != null, "peeking, the rooms not walked stand, marked as peeked", $"{Numbers.Count(ExampleLayouts.PeekedRooms)} peeked of {Numbers.Count(rooms)}");
            if (peekedRoom != null) p.Check(Stage.ExampleRoomLook(peekedRoom) != RoomLook.Whole, "and stand dimmed on the stage", Stage.ExampleRoomLook(peekedRoom).ToString());
            yield return Picture(p, $"fog {entry.Name} peeking");
            X.Peeking = false;
            yield return Until(() => ExampleLayouts.Example.Rooms.Count == fogged.Rooms.Count, 10);
            p.Check(ExampleLayouts.PeekedRooms == 0 && ExampleLayouts.Example.Rooms.Count == fogged.Rooms.Count, "the peek ended, the fog is back");
            // Laid out again, the dungeon still switches to its entrance, drawn.
            yield return Until(() => Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 30);
            Stage.Inside = false;
            yield return null;
            p.Check(Stage.HasOutside && !Stage.ShowsInside && Stage.OutsidePartsDrawn > 0, "after the peek, Outside still shows its entrance", $"has outside {Stage.HasOutside}, {Numbers.Count(Stage.OutsidePartsDrawn)} parts drawn");
            Stage.Inside = true;

            // A dungeon of the world nobody walked: the fog leaves no room, so its entrance shows,
            // before a peek and after one, never nothing.
            DungeonWalks.StandInForTest(null);
            var none = new DungeonMaps();
            none.Keep(id, PlaceOf(entry).Prefab, saved, new Vec3(0f, 0f, 0f), rolled.Site.Generator, rolled.Site.Turn);
            DungeonWalks.MapsInsteadForTest(none);
            yield return Until(() => ExampleLayouts.ShowsWorlds && ExampleLayouts.Example.Rooms.Count == 0, 15);
            yield return null;
            p.Check(ExampleLayouts.Walked == (0, rooms) && ExampleLayouts.Example.Rooms.Count == 0, "with no room walked, the fog leaves none standing", $"{Numbers.Count(ExampleLayouts.Example.Rooms.Count)} standing");
            p.Check(!Stage.ShowsInside && Stage.OutsidePartsDrawn > 0, "and the stage shows its entrance", $"{Numbers.Count(Stage.OutsidePartsDrawn)} parts drawn");
            X.Peeking = true;
            yield return Until(() => ExampleLayouts.PeekedRooms == rooms && Stage.ExampleRoomsTotal == rolled.Rooms.Count && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 60);
            p.Check(ExampleLayouts.PeekedRooms == rooms && Stage.ShowsInside, "peeking, every room stands inside", $"{Numbers.Count(ExampleLayouts.PeekedRooms)} peeked of {Numbers.Count(rooms)}");
            X.Peeking = false;
            yield return Until(() => ExampleLayouts.Example.Rooms.Count == 0, 10);
            yield return null;
            p.Check(!Stage.ShowsInside && Stage.OutsidePartsDrawn > 0, "the peek ended, the stage shows its entrance again", $"{Numbers.Count(Stage.OutsidePartsDrawn)} parts drawn");
            yield return Picture(p, $"fog {entry.Name} nobody walked after peeking");
            DungeonFogAsItWas();
        }

        /// <summary>
        /// Maps of a crypt kept in the world played, on maps of the self-test's own, lying at two
        /// distances from the player: the crypt's page has a heading and a line for each with a room
        /// walked, the nearer first, each saying where it lies and how far it was walked; Show it shows
        /// one on the stage though the player stands nowhere near, Forget takes it away, an example in
        /// its place, and On the map opens Valheim's large map where the other lies.
        /// </summary>
        private static IEnumerator MapsKept(Probe p)
        {
            var entry = Crypt();
            if (entry == null) p.Skip("there is no such location");
            var world = CompendiumMap.World;
            if (world == null) p.Skip("no world is played");
            var noCompendium = new CompendiumView(X.Entries);
            noCompendium.Tell(new CompendiumBook(), CompendiumRule.Off);
            Session.ShowKnownInstead(noCompendium);
            Select(entry);
            Stage.Inside = true;
            yield return Until(() => ReferenceEquals(X.Compendium, noCompendium) && ExampleLayouts.Of(entry) && ExampleLayouts.Example != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 150);
            var rolled = ExampleLayouts.Of(entry) ? ExampleLayouts.Example : null;
            if (!p.Check(rolled != null && !ExampleLayouts.ShowsWorlds, "an example has been laid out")) yield break;

            var maps = new DungeonMaps();
            var saved = SavedAsTheGameSaves(rolled);
            var first = DungeonMaps.IdOf(world, 1L, 1u);
            var second = DungeonMaps.IdOf(world, 1L, 2u);
            // The first far off, the second nearer, so the second's line stands first.
            var here = Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero;
            maps.Keep(first, PlaceOf(entry).Prefab, saved, new Vec3(here.x + 1500f, here.y, here.z), rolled.Site.Generator, rolled.Site.Turn);
            maps.Keep(second, PlaceOf(entry).Prefab, saved, new Vec3(here.x, here.y, here.z - 300f), rolled.Site.Generator, rolled.Site.Turn);
            maps.Walk(second, 0);
            for (var room = 0; room < 3; room++) maps.Walk(first, room);
            // A third with no room walked holds nothing to show, and has no line.
            maps.Keep(DungeonMaps.IdOf(world, 1L, 4u), PlaceOf(entry).Prefab, saved, new Vec3(0f, 0f, 0f), rolled.Site.Generator, rolled.Site.Turn);
            maps.Keep(DungeonMaps.IdOf("another world", 1L, 3u), PlaceOf(entry).Prefab, saved, new Vec3(0f, 0f, 0f), rolled.Site.Generator, rolled.Site.Turn);
            DungeonWalks.MapsInsteadForTest(maps);
            var lines = ScryPanel.Drawn(PanelPart.MapLine);
            yield return Until(() => ScryPanel.Drawn(PanelPart.MapLine) > lines + 1, 5);
            p.Check(ScryPanel.MapLinesDrawn == 2, "its page has a line for each map of this world with a room walked", $"{Numbers.Count(ScryPanel.MapLinesDrawn)} lines");
            var built = ScryPanel.WalkLinesForTest.Select(l => l.Id).ToList();
            p.Check(ScryPanel.WalkHeadingForTest == DungeonMapWords.Heading(2) && built.SequenceEqual(new[] { second, first }),
                "under a heading, the nearer stands first", $"{ScryPanel.WalkHeadingForTest}: {string.Join(", ", built)}");
            p.Check((ScryPanel.FirstMapLineForTest ?? "").StartsWith(DungeonMapWords.Where(300f, 4), System.StringComparison.Ordinal),
                "its line says where it lies from the player", ScryPanel.FirstMapLineForTest);
            yield return Picture(p, $"walks of {entry.Name} on its page");

            ScryPanel.OpenMapForTest(second);
            yield return Until(() => ExampleLayouts.ShownMap == second && Stage.ExampleRoomsTotal == rolled.Rooms.Count && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 60);
            p.Check(ExampleLayouts.ShownMap == second && ExampleLayouts.Example.Rooms.Count == rolled.Rooms.Count, "Open map shows it on the stage, the player nowhere near", ExampleLayouts.ShownMap ?? "an example");

            DungeonWalks.ForgetMap(second);
            yield return Until(() => !ExampleLayouts.ShowsWorlds, 5);
            lines = ScryPanel.Drawn(PanelPart.MapLine);
            yield return Until(() => ScryPanel.Drawn(PanelPart.MapLine) > lines + 1, 5);
            p.Check(!ExampleLayouts.ShowsWorlds && ScryPanel.MapLinesDrawn == 1 && maps.Of(second) == null, "Forget takes the map away, an example in its place", $"{Numbers.Count(ScryPanel.MapLinesDrawn)} lines");
            p.Check(maps.All.Count() == 3, "the map of another world and the one not walked stay");

            // On the map opens Valheim's large map where it lies, the panel put away; put back after.
            if (Minimap.instance != null && !Game.m_noMap)
            {
                ScryPanel.ShowOnTheMapForTest(first);
                yield return Until(() => Minimap.instance.m_mode == Minimap.MapMode.Large, 3);
                p.Check(Minimap.instance.m_mode == Minimap.MapMode.Large && !Session.IsOpen, "On the map opens Valheim's large map where it lies, the panel put away", Minimap.instance.m_mode.ToString());
                yield return Picture(p, $"the large map where a walked {entry.Name} lies");
                Minimap.instance.SetMapMode(Minimap.MapMode.Small);
                Session.Show(null);
            }
            else p.Note("this world has no map: On the map is left out");
            DungeonFogAsItWas();
        }

        /// <summary>
        /// A map of a crypt kept in the world played and one of another world, both where the player
        /// stands, walked today: the large map open, a mark stands for the one of this world alone, its
        /// words saying the last day you walked it; a click on it closes the map and opens Scry on the
        /// crypt's page, its map on the stage, its line saying the day walked.
        /// </summary>
        private static IEnumerator MarksOnTheMap(Probe p)
        {
            var entry = Crypt();
            if (entry == null) p.Skip("there is no such location");
            var world = CompendiumMap.World;
            var player = Player.m_localPlayer;
            if (world == null || player == null || Minimap.instance == null || Game.m_noMap) p.Skip("no map in this world");
            var noCompendium = new CompendiumView(X.Entries);
            noCompendium.Tell(new CompendiumBook(), CompendiumRule.Off);
            Session.ShowKnownInstead(noCompendium);
            Select(entry);
            Stage.Inside = true;
            yield return Until(() => ReferenceEquals(X.Compendium, noCompendium) && ExampleLayouts.Of(entry) && ExampleLayouts.Example != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 150);
            var rolled = ExampleLayouts.Of(entry) ? ExampleLayouts.Example : null;
            if (!p.Check(rolled != null, "an example has been laid out")) yield break;

            var here = player.transform.position;
            var at = new Vec3(here.x, here.y, here.z);
            var maps = new DungeonMaps();
            var saved = SavedAsTheGameSaves(rolled);
            var id = DungeonMaps.IdOf(world, 1L, 11u);
            var today = EnvMan.instance != null ? EnvMan.instance.GetDay() : -1;
            maps.Keep(id, PlaceOf(entry).Prefab, saved, at, rolled.Site.Generator, rolled.Site.Turn, today);
            maps.Walk(id, 0);
            maps.Keep(DungeonMaps.IdOf("another world", 1L, 12u), PlaceOf(entry).Prefab, saved, at, rolled.Site.Generator, rolled.Site.Turn);
            // A large map left open, by a part or the player, is put away first, so no mark of before is read.
            if (Minimap.instance.m_mode == Minimap.MapMode.Large) Minimap.instance.SetMapMode(Minimap.MapMode.Small);
            yield return Until(() => MapMarkButtons.MarksShown == 0, 3);
            DungeonWalks.MapsInsteadForTest(maps);
            // The dungeons a pinned ground of the player's shows stay off the map meanwhile, so the test's mark stands alone.
            DungeonsInGround.NoneForTest();
            // The panel put away, as it would cover the map; the click opens it again.
            Session.Hide();
            Minimap.instance.SetMapMode(Minimap.MapMode.Large);
            yield return Until(() => MapMarkButtons.MarksShown > 0 && MapMarkButtons.FirstListedForTest == id, 5);
            p.Check(MapMarkButtons.MarksShown == 1, "the large map has a mark for the map of this world alone", $"{Numbers.Count(MapMarkButtons.MarksShown)} marks");
            // A dungeon's end caps and dividers are no rooms to walk, so its words count without them.
            var walkable = rolled.Rooms.Count(r => !r.Room.EndCap && !r.Room.Divider);
            p.Check(MapMarkButtons.SaidForTest(0)?.Contains("Walked by you: 1 of " + Numbers.Count(walkable) + " room") ?? false,
                "its words count the room walked out of the rooms one can walk, its end caps and dividers left out", $"{MapMarkButtons.SaidForTest(0)}, {Numbers.Count(walkable)} rooms to walk of {Numbers.Count(rolled.Rooms.Count)}");
            // The pointer put on it, as a player's: what the game's event system meets there (the map,
            // which takes the click to hand on), the mark found there as the pointer is read, the
            // game's tip shown, a click taken.
            var center = MapMarkButtons.MarkCenterForTest(0);
            if (!p.Check(center != null, "the mark stands on the screen")) yield break;
            p.Note($"the game's event system at the mark's middle first meets: {MapMarkButtons.RaycastForTest(0) ?? "no event system"}");
            p.Check(MapMarkButtons.MarkAtForTest(center.Value) == 0, "the pointer at the mark's middle is read as on it, as the game's map and tips read it");
            PointerTo(center.Value);
            bool Arrived() => ((Vector2)ZInput.pointerPosition - center.Value).sqrMagnitude < 4f;
            yield return Until(Arrived, 3);
            bool clicked;
            if (Arrived())
            {
                yield return Until(() => MapMarkButtons.NameShownForTest, 3);
                p.Check(MapMarkButtons.NameShownForTest, "the pointer resting on it shows its name under it, as the game names a pin");
                var shownAt = Time.unscaledTime;
                yield return Until(() => Time.unscaledTime - shownAt > 1.2f, 3);
                yield return Picture(p, $"the mark of {entry.Name} on the map with its tip");
                clicked = MapMarkButtons.ClickAtForTest(ZInput.pointerPosition);
            }
            else
            {
                Vector2 read = ZInput.pointerPosition;
                p.Note($"the pointer was put at {Numbers.Fixed(center.Value.x, 0)}, {Numbers.Fixed(center.Value.y, 0)} but the game reads it at {Numbers.Fixed(read.x, 0)}, {Numbers.Fixed(read.y, 0)}, as while its window is not in front: the tip untried, the click made at the mark's middle");
                clicked = MapMarkButtons.ClickAtForTest(center.Value);
            }
            p.Check(clicked, "a click on the map at the mark goes to the mark");
            yield return Until(() => ExampleLayouts.ShownMap == id && Stage.ExampleRoomsTotal == rolled.Rooms.Count, 60);
            p.Check(Minimap.instance.m_mode != Minimap.MapMode.Large && Session.IsOpen && X.Selected == entry && ExampleLayouts.ShownMap == id,
                "a click on it closes the map and opens Scry on its page, its map on the stage", $"map {Minimap.instance.m_mode}, selected {X.Selected?.Name}, shown {ExampleLayouts.ShownMap ?? "an example"}");
            if (today >= 0)
            {
                var said = "day " + Numbers.Count(today);
                yield return Until(() => ScryPanel.FirstMapTipForTest?.Contains(said) ?? false, 3);
                p.Check((ScryPanel.FirstMapLineForTest?.StartsWith(DungeonMapWords.Where(0f, 0), System.StringComparison.Ordinal) ?? false) && ScryPanel.FirstMapLineForTest.Contains(" \u00b7 you ")
                    && (ScryPanel.FirstMapTipForTest?.Contains(said) ?? false),
                    "its page's line says it lies where you stand and how far you walked it, its hover the day", $"{ScryPanel.FirstMapLineForTest} / {ScryPanel.FirstMapTipForTest}");
            }
            yield return WalkedWhole(p, maps, id, rolled.Rooms.Count, entry.Name);
            MarksAsTheyWere();
        }

        /// <summary>Every room of a dungeon walked, its mark on the map is ticked and its words say all its rooms were.</summary>
        private static IEnumerator WalkedWhole(Probe p, DungeonMaps maps, string id, int rooms, string name)
        {
            for (var room = 0; room < rooms; room++) maps.Walk(id, room);
            Session.Hide();
            Minimap.instance.SetMapMode(Minimap.MapMode.Large);
            yield return Until(() => MapMarkButtons.LookForTest(0) == MarkLook.Whole, 5);
            p.Check(MapMarkButtons.LookForTest(0) == MarkLook.Whole && (MapMarkButtons.SaidForTest(0)?.Contains("Walked by you: all ") ?? false),
                "every room walked, its mark is ticked and its words say all its rooms were", MapMarkButtons.SaidForTest(0));
            yield return Picture(p, $"the mark of {name} walked whole");
            Minimap.instance.SetMapMode(Minimap.MapMode.Small);
        }

        /// <summary>
        /// The server's record of a crypt, two rooms walked, in place of what a server would send, with
        /// a made-up character whose floor reaches the crypt's tier and who never was in it: its page
        /// has the server's line, Open map shows the rooms walked on the server, peek the rest. Merging
        /// players' walks on a real server is not tried here, as it would write a made-up record into
        /// the server's file.
        /// </summary>
        private static IEnumerator GroupRecords(Probe p)
        {
            var entry = Crypt();
            if (entry == null) p.Skip("there is no such location");
            var world = CompendiumMap.World;
            if (world == null) p.Skip("no world is played");
            yield return Until(() => CompendiumTiers.Of(X.Entries) != null, 20);
            var tiers = CompendiumTiers.Of(X.Entries);
            var tier = 0;
            if (tiers == null || !tiers.TryGetValue(entry.Key, out tier)) p.Skip("the crypt has no tier");
            var noCompendium = new CompendiumView(X.Entries);
            noCompendium.Tell(new CompendiumBook(), CompendiumRule.Off);
            Session.ShowKnownInstead(noCompendium);
            Select(entry);
            Stage.Inside = true;
            yield return Until(() => ReferenceEquals(X.Compendium, noCompendium) && ExampleLayouts.Of(entry) && ExampleLayouts.Example != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 150);
            var rolled = ExampleLayouts.Of(entry) ? ExampleLayouts.Example : null;
            if (!p.Check(rolled != null, "an example has been laid out")) yield break;

            var ways = DungeonWays.Of(rolled.Rooms);
            var onward = ways.Entrance != null ? ways.WaysOf(ways.Entrance) : null;
            if (!p.Check(onward != null && onward.Count > 0, "its entrance leads on to a room")) yield break;
            var first = rolled.Rooms.IndexOf(ways.Entrance);
            var second = rolled.Rooms.IndexOf(onward[0]);
            var id = DungeonMaps.IdOf(world, 1L, 21u);
            var walked = new DungeonMaps();
            var whole = walked.Keep(id, PlaceOf(entry).Prefab, SavedAsTheGameSaves(rolled), new Vec3(0f, 0f, 0f), rolled.Site.Generator, rolled.Site.Turn);
            walked.Walk(id, first);
            walked.Walk(id, second);
            // As the server sends it: from its generator, no place of the world in it.
            var group = new DungeonMaps();
            group.Merge(walked.Only(world, new[] { PlaceOf(entry).Prefab }));
            p.Check(group.Of(id) != null && group.Of(id).Generator.Length == 0f && group.Of(id).Walked.Count == 2, "the record goes as it stands from its generator");
            GroupMapsLink.GroupInsteadForTest(group);
            DungeonWalks.MapsInsteadForTest(new DungeonMaps());
            var floored = new CompendiumView(X.Entries);
            floored.Milestones(tiers, tier);
            floored.Tell(new CompendiumBook(), CompendiumRule.Loose);
            Session.ShowKnownInstead(floored);
            var lines = ScryPanel.Drawn(PanelPart.MapLine);
            yield return Until(() => ReferenceEquals(X.Compendium, floored) && ScryPanel.Drawn(PanelPart.MapLine) > lines + 1, 5);
            p.Check(ExampleLayouts.GroupOpen && ScryPanel.GroupLinesDrawn == 1 && ScryPanel.MapLinesDrawn == 0, "with the floor at its tier, its page has the server's line and none of the character's",
                $"open {ExampleLayouts.GroupOpen}, {Numbers.Count(ScryPanel.GroupLinesDrawn)} server lines, {Numbers.Count(ScryPanel.MapLinesDrawn)} own");
            yield return Picture(p, $"the server's map of {entry.Name} on its page");

            ScryPanel.OpenMapForTest(id);
            yield return Until(() => ExampleLayouts.ShownMap == id && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 60);
            var rooms = rolled.Rooms.Count(r => !r.Room.EndCap && !r.Room.Divider);
            p.Check(ExampleLayouts.ShownMap == id && ExampleLayouts.Walked == (2, rooms), "Open map shows the rooms walked on the server", $"{Numbers.Count(ExampleLayouts.Walked.Walked)} of {Numbers.Count(ExampleLayouts.Walked.Rooms)}");
            yield return Picture(p, $"the server's map of {entry.Name}");
            X.Peeking = true;
            yield return Until(() => ExampleLayouts.PeekedRooms > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 60);
            p.Check(ExampleLayouts.PeekedRooms == rooms - 2, "peek shows the rooms nobody walked", $"{Numbers.Count(ExampleLayouts.PeekedRooms)} peeked");
            GroupRecordsAsTheyWere();
        }

        /// <summary>Ends the stand-in for the server's records and the fog's.</summary>
        private static void GroupRecordsAsTheyWere()
        {
            GroupMapsLink.GroupInsteadForTest(null);
            DungeonFogAsItWas();
        }

        /// <summary>Where the player's pointer stood before the self-test put it on a mark; none once put back.</summary>
        private static Vector2? _pointerBeforeMarks;

        /// <summary>Puts the pointer on a mark, as a player's, keeping where it stood to put it back (<see cref="MarksAsTheyWere"/>).</summary>
        private static void PointerTo(Vector2 at)
        {
            _pointerBeforeMarks = ZInput.pointerPosition;
            ZInput.SetMousePosition(at);
        }

        /// <summary>Closes the large map the self-test opened, puts the player's pointer back where it stood and ends the fog's stand-ins.</summary>
        private static void MarksAsTheyWere()
        {
            if (Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large) Minimap.instance.SetMapMode(Minimap.MapMode.Small);
            DungeonsInGround.WorldAgainForTest();
            if (!Session.IsOpen) Session.Show(null);
            if (_pointerBeforeMarks is Vector2 was && Cursor.lockState != CursorLockMode.Locked) ZInput.SetMousePosition(was);
            _pointerBeforeMarks = null;
            DungeonFogAsItWas();
        }

        /// <summary>Ends the fog's stand-ins: the peek, the made-up character, the maps and where the player stood.</summary>
        private static void DungeonFogAsItWas()
        {
            X.Peeking = false;
            DungeonWalks.StandInForTest(null);
            DungeonWalks.MapsInsteadForTest(null);
            ThisDungeonAsItWas();
        }

        /// <summary>Ends the stand-in, an example shown again in place of it, everything known again as the rest of the test has it.</summary>
        private static void ThisDungeonAsItWas()
        {
            ExampleLayouts.ShowExample();
            ExampleLayouts.StandInForTest(null);
            AllKnown();
        }
    }
}
