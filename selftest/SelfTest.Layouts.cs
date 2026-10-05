using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for dungeons and camps laid out: the cut moved by hand, the floors
    /// found in towers and caves, a dungeon's and a camp's example, the camera on a floor, and
    /// every dungeon laid out and heading its group.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>Shift and the wheel move a room's cut; going away and back opens it afresh; a location keeps its roof until its chip takes it off.</summary>
        private static IEnumerator CutMoves(Probe p)
        {
            var rooms = X.Catalog.Where(e => PlaceOf(e)?.IsRoom == true).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            var room = rooms.FirstOrDefault(e => PlaceOf(e).Prefab.StartsWith("forestcrypt", StringComparison.OrdinalIgnoreCase)) ?? rooms.FirstOrDefault();
            var other = rooms.FirstOrDefault(e => e != room);
            if (room == null || other == null) p.Skip("there are not two dungeon rooms");
            Select(room);
            yield return Until(() => CopyOf(room) != null && Stage.HasFloors, 20);
            if (!p.Check(Stage.Cutting, $"{room.Name} opens cut", Stage.CutLabel)) yield break;
            var at = Stage.CutAt;
            Stage.CutBy(1f);
            p.Check(Mathf.Abs(Stage.CutAt - (at + 1f)) < 0.01f, "Shift and the wheel move the cut up", $"{Numbers.Fixed(at, 1)} to {Numbers.Fixed(Stage.CutAt, 1)} m");
            Stage.CutBy(-2f);
            p.Check(Mathf.Abs(Stage.CutAt - (at - 1f)) < 0.01f, "and down", $"{Numbers.Fixed(Stage.CutAt, 1)} m");
            Select(other);
            yield return Until(() => CopyOf(other) != null, 20);
            Select(room);
            yield return Until(() => CopyOf(room) != null, 20);
            p.Check(Stage.Cutting && Mathf.Abs(Stage.CutAt - at) < 0.01f, "going away and back opens it afresh on its top floor", $"{Numbers.Fixed(Stage.CutAt, 1)} m");

            var location = LocationNamed("WoodHouse1", "WoodHouse2", "Ruin1", "StoneHouse3");
            if (location == null) yield break;
            Select(location);
            yield return Until(() => CopyOf(location) != null, 20);
            p.Check(Stage.HasFloors && !Stage.Cutting, $"{location.Name} keeps its roof on", Stage.CutLabel);
            Stage.ToggleRoof();
            p.Check(Stage.Cutting, "until its chip takes it off", Stage.CutLabel);
            var height = (Stage.ModelBottom + Stage.ModelTop) / 2f;
            Stage.CutTo(height);
            p.Check(Mathf.Abs(Stage.CutAt - height) < 0.01f && Stage.CutLevel == PlaceView.LevelAt(Stage.FloorHeights, height), "the ruler sets the cut anywhere, over the floor below it", $"{Numbers.Fixed(Stage.CutAt, 1)} m, {Stage.CutLabel}");
            Stage.ToggleRoof();
            p.Check(!Stage.Cutting, "and the chip puts the roof back on", Stage.CutLabel);
        }

        /// <summary>
        /// Floors are found in the places themselves: a spread of locations and rooms, and the
        /// towers and caves among them, each with floors from the top down and a cut over each,
        /// no collider left on its copy once read, and the floor ruler drawn beside the stage.
        /// </summary>
        private static IEnumerator FloorsFound(Probe p)
        {
            var places = X.Catalog.Where(e => PlaceOf(e) != null).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            if (places.Count == 0) p.Skip("there are no locations");
            var picks = Spread(places, 16);
            picks.AddRange(places.Where(e => e.Name.IndexOf("Tower", StringComparison.OrdinalIgnoreCase) >= 0 || e.Name.IndexOf("Cave", StringComparison.OrdinalIgnoreCase) >= 0).Take(8));
            picks.AddRange(places.Where(e => e.Name == "Hildir_cave"));
            var wrong = new List<string>();
            var several = new List<string>();
            foreach (var entry in picks.Distinct().ToList())
            {
                var place = PlaceOf(entry);
                Select(entry);
                yield return Until(() => PlaceAssets.State(place) != PlaceLoad.Loading && CopyOf(entry) != null, 20);
                if (CopyOf(entry) == null)
                {
                    if (PlaceAssets.State(place) == PlaceLoad.Ready) wrong.Add($"{entry.Name} stands nowhere");
                    continue;
                }
                yield return null;
                var floors = Stage.FloorHeights.ToList();
                var cuts = Stage.CutHeights.ToList();
                if (floors.Count == 0) wrong.Add($"{entry.Name} has no floor");
                else if (floors.Zip(floors.Skip(1), (above, below) => above > below).Any(ordered => !ordered)) wrong.Add($"{entry.Name}'s floors are out of order: {string.Join(", ", floors.Select(f => Numbers.Amount(f)))}");
                else if (cuts.Zip(floors, (cut, floor) => cut > floor).Any(over => !over)) wrong.Add($"{entry.Name} is cut below a floor");
                var left = CopyOf(entry) != null ? CopyOf(entry).GetComponentsInChildren<Collider>(true).Length : 0;
                if (left > 0) wrong.Add($"{entry.Name} keeps {Numbers.Count(left)} colliders");
                // Each floor with what its rays landed on, to see what is taken for a floor.
                var makers = Stage.FloorMakersNow();
                if (floors.Count > 1) several.Add($"{entry.Name} ({string.Join("; ", floors.Select((f, i) => $"{Numbers.Fixed(f, 1)} m on {(i < makers.Count ? makers[i] : "?")}"))})");
                // Kevin found the Howling Cavern's lowest floor a slab running out of its rock: every part near each floor.
                if (entry.Name == "Hildir_cave") p.Note($"{entry.Name}'s floors by part: " + string.Join("; ", Stage.FloorPartsTold()));
            }
            p.Note(several.Count > 0 ? "with several floors: " + string.Join("; ", several) : "none with several floors");
            p.Check(wrong.Count == 0, "each has floors from the top down, cut over each, with no collider left on its copy", string.Join("; ", wrong.Take(8)));
            var drawn = ScryPanel.Drawn(PanelPart.Ruler);
            yield return Until(() => ScryPanel.Drawn(PanelPart.Ruler) > drawn, 3);
            p.Check(ScryPanel.Drawn(PanelPart.Ruler) > drawn, "the floor ruler draws beside the stage");
        }

        private static IEnumerator DungeonExample(Probe p) => Layout(p, Crypt(), "Dungeon");

        private static IEnumerator CampExample(Probe p) => Layout(p, Camp(), "CampRadial");

        private static IEnumerator Layout(Probe p, Entry entry, string algorithm)
        {
            if (entry == null) p.Skip("there is no such location");
            var place = PlaceOf(entry);
            p.Note($"{entry.Name} ({entry.DisplayName})");
            var asked = Time.unscaledTime;
            Select(entry);
            yield return Until(() => place.Contents?.Dungeon != null, 30);
            var plan = place.Contents?.Dungeon;
            if (!p.Check(plan != null, "how it is built has been read")) yield break;
            p.Check(plan.Algorithm == algorithm, $"it is built as a {algorithm}", plan.Algorithm);

            yield return Until(() => ExampleLayouts.Of(entry) && ExampleLayouts.Example != null, 150);
            var example = ExampleLayouts.Of(entry) ? ExampleLayouts.Example : null;
            if (!p.Check(example != null, "an example has been laid out", $"{Numbers.Count(ExampleLayouts.Read)} of {Numbers.Count(ExampleLayouts.Total)} kinds of room read")) yield break;
            p.Note($"{DungeonWords.Example(example, ExampleLayouts.Failed)}, its {Numbers.Count(ExampleLayouts.Total)} kinds of room read and laid out {Numbers.Fixed(Time.unscaledTime - asked, 1)} s after selecting it");
            p.Check(ExampleLayouts.Failed == 0, "every kind of room could be loaded", $"{Numbers.Count(ExampleLayouts.Failed)} could not");
            p.Check(example.Rooms.Count > 1, "it has rooms", $"{Numbers.Count(example.Rooms.Count)}");
            if (algorithm == "Dungeon")
            {
                p.Check(example.Rooms[0].Room.Entrance, "it starts at an entrance", example.Rooms[0].Room.Name);
                var start = example.Rooms[0];
                p.Check(example.RoomAt(start.Position.X, start.Position.Z) != null, "a point on the plan finds a room there");
            }

            ExampleLayouts.Another();
            var another = ExampleLayouts.Example;
            p.Check(another != null && another != example && another.Rooms.Count > 0, "Another layout lays out a new one");
            if (another == null) yield break;
            example = another;

            // Its page tells what its rooms hold, the loot in them and what their spawn points place.
            var page = Facts.For(entry);
            var holds = page.Rows.Where(r => PlaceParts.Roles.Any(role => r.Title.StartsWith(PlaceParts.Title(role, true), StringComparison.Ordinal))).ToList();
            p.Check(holds.Sum(r => r.Items.Count) > 0, "its page tells what its rooms hold", holds.Count > 0 ? string.Join("; ", holds.Select(r => $"{r.Title} {Numbers.Count(r.Items.Count)}")) : string.Join("; ", page.Rows.Select(r => r.Title)));
            var loot = page.Rows.FirstOrDefault(r => r.Title.StartsWith("Loot in its rooms", StringComparison.Ordinal));
            p.Note(loot != null ? $"loot in its rooms: {string.Join(", ", loot.Items.Take(12).Select(i => i.Prefab + " " + i.Amount))}" : "no loot in its rooms");
            if (algorithm == "Dungeon") p.Check(loot != null && loot.Items.Count > 0, "and the loot in them");

            // It is built on the stage, room by room: a dungeon inside, opened on its top floor,
            // with its entrance outside; a camp around the location's own parts.
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            yield return Until(() => Stage.ExampleRoomsTotal == example.Rooms.Count && Stage.ExampleRoomsShown == example.Rooms.Count, 30);
            p.Check(Stage.ExampleRoomsShown == example.Rooms.Count, "it is built on the stage, room by room", $"{Numbers.Count(Stage.ExampleRoomsShown)} of {Numbers.Count(example.Rooms.Count)}");
            yield return null;
            var size = Stage.SubjectSize;
            p.Note($"the stage shows it {Numbers.Amount(size.x, 0)} × {Numbers.Amount(size.z, 0)} m, {Numbers.Amount(size.y, 0)} m high");
            if (algorithm == "Dungeon")
            {
                p.Check(Stage.HasInside && Stage.Inside, "it shows the dungeon inside");

                // Its plan is of the inside: none while its entrance is shown from outside.
                Stage.Inside = false;
                var planned = ScryPanel.Drawn(PanelPart.Plan) + ScryPanel.Drawn(PanelPart.PlanTab);
                var outsideFrom = Time.frameCount;
                yield return Until(() => Time.frameCount > outsideFrom + 2, 3);
                p.Check(ScryPanel.Drawn(PanelPart.Plan) + ScryPanel.Drawn(PanelPart.PlanTab) == planned, "shown from outside, it draws no plan of its inside");
                Stage.Inside = true;
                yield return null;

                // Its row holds only View, Inside and Creatures; the roof is over the ruler.
                var chips = ScryPanel.Drawn(PanelPart.StageChip);
                var roofs = ScryPanel.Drawn(PanelPart.RoofButton);
                var frames = Time.frameCount;
                yield return Until(() => Time.frameCount > frames + 1 && ScryPanel.Drawn(PanelPart.RoofButton) > roofs, 3);
                var perFrame = (ScryPanel.Drawn(PanelPart.StageChip) - chips) / Mathf.Max(1, ScryPanel.Drawn(PanelPart.RoofButton) - roofs);
                p.Check(perFrame > 0 && perFrame <= 3 && ScryPanel.Drawn(PanelPart.RoofButton) > roofs, "its stage shows three chips at the most, the roof over its ruler", $"{Numbers.Count(perFrame)} chips a frame");
                p.Check(Stage.Cutting, "opened on its top floor", Stage.CutLabel);
                var cuts = new List<float> { Stage.CutAt };
                while (Stage.CutLevel < Stage.FloorHeights.Count - 1 && cuts.Count < 40)
                {
                    Stage.StepCut(true);
                    cuts.Add(Stage.CutAt);
                }
                var down = cuts.Zip(cuts.Skip(1), (above, below) => below < above).All(lower => lower);
                p.Note($"{Numbers.Count(Stage.FloorHeights.Count)} floors found in its rooms");
                p.Check(cuts.Count > 0 && down, "it steps down floor by floor", string.Join(", ", cuts.Select(c => Numbers.Fixed(c, 1))) + " m");
                if (Stage.FloorHeights.Count > 1)
                {
                    // On its lowest floor the rooms above are put away; with the roof on, all stand again.
                    yield return Until(() => Stage.ExampleRoomsAway > 0, 3);
                    var label = PlaceView.FloorLabel(Stage.CutLevel, Stage.FloorHeights.Count, Stage.ExampleRoomsOnFloor);
                    p.Check(Stage.ExampleRoomsAway > 0 && label != null, "on its lowest floor the rooms above are put away, the floor named by the ruler", $"{Numbers.Count(Stage.ExampleRoomsAway)} rooms put away; {label ?? "no label"}");
                    Stage.OpenLevel(Stage.FloorHeights.Count);
                    yield return Until(() => Stage.ExampleRoomsAway == 0, 3);
                    p.Check(Stage.ExampleRoomsAway == 0, "with the roof on every room stands", $"{Numbers.Count(Stage.ExampleRoomsAway)} still away");
                }
                Stage.OpenLevel(0);
                yield return Until(() => Stage.ExampleRoomsKept, 3);
                if (Stage.FloorHeights.Count > 1) p.Check(Stage.ExampleRoomsDimmed > 0, "on its top floor the rooms below stand dimmed", $"{Numbers.Count(Stage.ExampleRoomsDimmed)} dimmed, {Numbers.Count(Stage.ExampleRoomsAway)} put away");
                // A room of the top floor, which stands while it is opened.
                var top = Stage.ExampleShown?.Rooms.FindIndex(r => !r.Room.EndCap && !r.Room.Divider && Stage.ExampleRoomShown(r) == PlanRoomShown.Whole) ?? -1;
                var point = Stage.ExamplePointOf(Math.Max(0, top));
                p.Check(point.HasValue && Stage.ExampleRoomAt(point.Value) != null, "a room on the stage is found under the mouse", (point.HasValue ? Figures.Point(point.Value) : "not in view"));
                Stage.Inside = false;
                yield return null;
                var outside = Stage.SubjectSize;
                p.Check(Mathf.Max(outside.x, outside.z) < Mathf.Max(size.x, size.z), "outside it shows its entrance", $"{Numbers.Amount(outside.x, 0)} × {Numbers.Amount(outside.z, 0)} m");
                Stage.Inside = true;
            }
            else
            {
                p.Check(!Stage.HasInside, "a camp has no inside to go into");
                p.Check(Mathf.Max(size.x, size.z) > 15f, "the camp stands around the location", $"{Numbers.Amount(size.x, 0)} × {Numbers.Amount(size.z, 0)} m");
            }
            Stage.Inside = wasInside;

            // Every room of it goes to an entry of its own.
            var keys = new HashSet<string>(X.Catalog.Select(e => e.Key));
            var missing = example.Rooms.Select(r => EntryKeys.For(Kind.Location, r.Room.Name)).Where(k => !keys.Contains(k)).Distinct().ToList();
            p.Check(missing.Count == 0, "every room on it has an entry to go to", string.Join(", ", missing.Take(5)));

            // Its plan in the stage's corner draws its rooms.
            ScryPanel.PlanFolded = false;
            // Folded, the plan leaves a tab that brings it back.
            var tabs = ScryPanel.Drawn(PanelPart.PlanTab);
            ScryPanel.PlanFolded = true;
            yield return Until(() => ScryPanel.Drawn(PanelPart.PlanTab) > tabs, 3);
            p.Check(ScryPanel.Drawn(PanelPart.PlanTab) > tabs, "folded, the plan leaves a tab to bring it back");
            ScryPanel.PlanFolded = false;
            var drawn = ScryPanel.Drawn(PanelPart.Plan);
            yield return Until(() => ScryPanel.Drawn(PanelPart.Plan) > drawn, 3);
            p.Check(ScryPanel.Drawn(PanelPart.Plan) > drawn, "the plan draws its rooms");

            if (algorithm != "Dungeon") yield break;

            // What a click on a room does, then back.
            var target = example.Rooms.FirstOrDefault(r => !r.Room.Entrance && !r.Room.EndCap && !r.Room.Divider) ?? example.Rooms[0];
            var key = EntryKeys.For(Kind.Location, target.Room.Name);
            p.Check(X.Jump(key) && X.Selected?.Key == key, "a room on the plan goes to its entry", key);
            p.Check(X.Back() && X.Selected == entry, "and Back returns to the dungeon");

            // Its rooms are read now; one with a vegvisir points to a location with an entry.
            var withVegvisir = X.Catalog.FirstOrDefault(e => PlaceOf(e)?.IsRoom == true && PlaceOf(e).Contents?.Vegvisirs.Count > 0);
            if (withVegvisir == null)
            {
                p.Note("none of its rooms has a vegvisir");
                yield break;
            }
            var to = EntryKeys.For(Kind.Location, PlaceOf(withVegvisir).Contents.Vegvisirs[0]);
            p.Check(keys.Contains(to), $"the vegvisir in {withVegvisir.Name} points to a location with an entry", to);
            if (keys.Contains(to))
            {
                X.Jump(withVegvisir.Key);
                p.Check(X.Jump(to) && X.Selected?.Key == to, "and goes there");
                p.Check(X.Back() && X.Selected == withVegvisir, "and Back returns to the room");
            }
        }

        /// <summary>The dungeons whose floors Kevin found right, which must find the floors they did before 2026-10-05's rules.</summary>
        private static readonly string[] Unchanged = { "MorkBorg", "Hildir_plainsfortress", "Crypt2" };

        /// <summary>
        /// The dungeons whose rooms are rock or stacked high, the Frost Caves, the M&#xF6;rkhalla ones and
        /// Hildir's sealed tower: each one's example is built and opened floor by floor, told with
        /// how many rooms stand on each floor; a floor no room stands on is a floor found where
        /// there is none, some room always stands on the stage, and every room stands whole on
        /// some floor, so it can be opened and gone to. M&#xF6;rkhalla, whose floors come out
        /// differently example to example, is laid out three times. Its creatures are told: how
        /// many dropped to the ground under their points and how many fly. The floors found are
        /// told beside those the rules before 2026-10-05 found; M&#xF6;rkhalla's and the sealed
        /// tower's, which Kevin found right, must be the same.
        /// </summary>
        private static IEnumerator CaveFloors(Probe p)
        {
            var named = new[] { "MountainCave02", "MorkBorg", "TheHole01", "Hildir_plainsfortress", "SunkenCrypt4", "Crypt2" };
            var caves = X.Catalog.Where(e => PlaceOf(e) != null && !PlaceOf(e).IsRoom
                                             && (named.Contains(e.Name) || e.DisplayName.IndexOf("rkhalla", StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            if (caves.Count == 0) p.Skip("none of " + string.Join(", ", named) + " is in the catalog");
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            var empty = new List<string>();
            var blank = new List<string>();
            var unreached = new List<string>();
            var changed = new List<string>();
            string Told(List<float> floors) => floors == null ? "not tried" : string.Join(", ", floors.Select(f => Numbers.Fixed(f, 1)));
            // Morkhalla's middle floors come and go: its rooms' ground with nothing of the room over it is tried as ground too.
            StageExample.TryOpenGround = true;
            foreach (var entry in caves)
            {
                Select(entry);
                for (var example = 0; example < (entry.Name == "MorkBorg" ? 3 : 1); example++)
                {
                    if (example > 0)
                    {
                        ExampleLayouts.Another();
                        yield return null;
                        yield return null;
                    }
                    yield return Until(() => CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.CreaturesWaiting == 0, 60);
                    if (CopyOf(entry) == null || Stage.ExampleRoomsTotal == 0)
                    {
                        p.Note($"{entry.Name}: no example stood on the stage");
                        break;
                    }
                    var (now, before, withOpen) = Stage.ExampleFloorsFoundThreeWays();
                    p.Note($"{entry.Name}: floors found {Told(now)}; by the rules before {Told(before)}; with open ground within reach of the doorways {Told(withOpen)}");
                    if (example == 0 && entry.Name == "MorkBorg") p.Note("its rooms with open ground within reach: " + string.Join("; ", Stage.ExampleRoomGroundTold(withOpen: true)));
                    if (now.Count != before.Count || now.Where((f, i) => Mathf.Abs(f - before[i]) > 0.05f).Any())
                    {
                        if (Unchanged.Contains(entry.Name))
                        {
                            changed.Add($"{entry.Name} {Told(now)}, before {Told(before)}");
                            p.Note("its rooms: " + string.Join("; ", Stage.ExampleRoomGroundTold()));
                        }
                    }
                    var told = new List<string>();
                    var reached = new HashSet<PlacedRoom>();
                    for (var level = 0; level < Stage.FloorHeights.Count; level++)
                    {
                        Stage.OpenLevel(level);
                        yield return Until(() => Stage.ExampleRoomsKept, 5);
                        var rooms = Stage.ExampleRoomsOnFloor;
                        told.Add($"{Numbers.Fixed(Stage.FloorHeights[level], 1)} m, {Numbers.Count(rooms)} rooms, {Numbers.Count(Stage.ExampleRoomsShown - Stage.ExampleRoomsAway)} standing, {Numbers.Count(Stage.CreaturesAboveCut)} creatures above the cut");
                        if (rooms == 0) empty.Add($"{entry.Name} at {Numbers.Fixed(Stage.FloorHeights[level], 1)} m");
                        if (Stage.ExampleRoomsAway >= Stage.ExampleRoomsShown) blank.Add($"{entry.Name} at {Numbers.Fixed(Stage.FloorHeights[level], 1)} m");
                        if (rooms > 0) foreach (var room in Stage.ExampleShown.Rooms) if (Stage.ExampleRoomShown(room) == PlanRoomShown.Whole) reached.Add(room);
                    }
                    // What stands above the top floor's cut is reached only by moving the cut up by hand.
                    if (Stage.CutHeights.Count > 0) p.Note($"{entry.Name}: top floor's cut {Numbers.Fixed(Stage.CutHeights[0], 1)} m, the place's top {Numbers.Fixed(Stage.ModelTop, 1)} m, the ruler's roof above {Numbers.Fixed(PlaceView.RoofAbove(Stage.CutHeights[0], Stage.ModelTop), 1)} m");
                    var never = Stage.ExampleShown?.Rooms.Where(r => !r.Room.EndCap && !r.Room.Divider && !reached.Contains(r)).Select(r => $"{r.Room.Name} at {Numbers.Fixed(r.Position.Y, 1)} m").ToList() ?? new List<string>();
                    if (never.Count > 0) unreached.Add($"{entry.Name}: {string.Join(", ", never.Take(6))}");
                    p.Note($"{entry.Name} ({entry.DisplayName}), {Numbers.Count(Stage.ExampleRoomsTotal)} rooms, {Numbers.Count(Stage.FloorHeights.Count)} floors: " + string.Join("; ", told)
                           + $"; creatures {Numbers.Count(Stage.CreaturesMade)}, {Numbers.Count(Stage.CreaturesDropped)} dropped to the ground under their points, {Numbers.Count(Stage.CreaturesFlying)} flying ({Stage.FlyersTold()})");
                    if (example == 0 && entry.Name == "MorkBorg") p.Note("its rooms' own floors, as each is shown alone: " + string.Join("; ", Stage.ExampleRoomFloorsTold()));
                    // Where floors are found that Kevin finds odd (a sunken crypt's one level told as several, a frost
                    // cave's floor with a room that is none): each room's box, doorways, ground and floors.
                    if (example == 0 && (entry.Name == "SunkenCrypt4" || entry.Name == "MountainCave02")) p.Note("its rooms: " + string.Join("; ", Stage.ExampleRoomGroundTold()));
                }
            }
            Stage.Inside = wasInside;
            p.Check(empty.Count == 0, "every floor found in their examples has rooms on it", string.Join("; ", empty));
            p.Check(blank.Count == 0, "and some room stands on the stage on every floor", string.Join("; ", blank));
            p.Check(unreached.Count == 0, "and every room stands whole on some floor", string.Join("; ", unreached));
            StageExample.TryOpenGround = false;
            p.Check(changed.Count == 0, "M\u00f6rkhalla, the sealed tower and the burial chambers find the floors they did before", string.Join("; ", changed));
        }

        /// <summary>
        /// The stage's camera with a floor opened in a tall dungeon's example: it looks at that
        /// floor, every room on it in the picture, and goes down with it a floor down; the wheel
        /// zooms toward what is under the pointer, which stays under it, and a drag with the right
        /// button keeps what was grabbed under the pointer; with the roof on, the wheel too.
        /// </summary>
        private static IEnumerator CameraMoves(Probe p)
        {
            var named = new[] { "MorkBorg", "TheHole01", "Crypt2" };
            var entry = named.Select(n => X.Catalog.FirstOrDefault(e => e.Name == n && PlaceOf(e) != null && !PlaceOf(e).IsRoom)).FirstOrDefault(e => e != null);
            if (entry == null) p.Skip("none of " + string.Join(", ", named) + " is in the catalog");
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            Select(entry);
            yield return Until(() => CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 60);
            if (!p.Check(CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.HasFloors, "its example stands on the stage", $"{entry.Name}, {Numbers.Count(Stage.ExampleRoomsShown)} rooms"))
            {
                Stage.Inside = wasInside;
                yield break;
            }
            p.Note($"{entry.Name} ({entry.DisplayName}), {Numbers.Count(Stage.ExampleRoomsTotal)} rooms, {Numbers.Count(Stage.FloorHeights.Count)} floors");

            Stage.OpenLevel(0);
            yield return Until(() => Stage.ExampleRoomsKept, 5);
            yield return Settled();
            p.Check(Stage.LookedFloor is float top && Mathf.Abs(top - Stage.FloorOnStage(0)) < 0.01f && Mathf.Abs(Stage.LookAt.y - top) < 0.05f,
                "with a floor opened the camera looks at that floor", $"looks at {Numbers.Fixed(Stage.LookAt.y, 1)} m, the floor at {Numbers.Fixed(Stage.FloorOnStage(0), 1)} m");
            var outside = RoomsOutOfPicture();
            p.Check(outside.Count == 0, "and every room on it is in the picture", string.Join(", ", outside.Take(8)));

            if (Stage.FloorHeights.Count > 1)
            {
                Stage.StepCut(true);
                yield return Until(() => Stage.ExampleRoomsKept, 5);
                yield return Settled();
                p.Check(Mathf.Abs(Stage.LookAt.y - Stage.FloorOnStage(Stage.CutLevel)) < 0.05f, "a floor down, it looks at that floor",
                    $"looks at {Numbers.Fixed(Stage.LookAt.y, 1)} m, the floor at {Numbers.Fixed(Stage.FloorOnStage(Stage.CutLevel), 1)} m");
                outside = RoomsOutOfPicture();
                p.Check(outside.Count == 0, "and every room on that one is in the picture", string.Join(", ", outside.Take(8)));
            }

            var at = new Vector2(0.7f, 0.35f);
            var under = Stage.PointUnder(at);
            var far = Stage.LookDistance;
            for (var i = 0; i < 5; i++) Stage.ZoomBy(-3f, at);
            yield return null;
            yield return null;
            var seen = under is Vector3 point ? Stage.PictureOf(point) : null;
            p.Check(seen is Vector2 there && (there - at).magnitude < 0.02f && Stage.LookDistance < far * 0.5f,
                "the wheel zooms toward what is under the pointer, which stays under it", $"{(seen is Vector2 s ? Figures.Point(s, 3) : "not in view")} for {Figures.Point(at, 3)}; {Numbers.Fixed(far, 1)} m to {Numbers.Fixed(Stage.LookDistance, 1)} m");

            var from = new Vector2(0.5f, 0.5f);
            var to = new Vector2(0.62f, 0.44f);
            var grabbed = Stage.PointUnder(from);
            Stage.Pan(from, to);
            yield return null;
            yield return null;
            var followed = grabbed is Vector3 held ? Stage.PictureOf(held) : null;
            p.Check(followed is Vector2 now && (now - to).magnitude < 0.02f, "a drag with the right button keeps what was grabbed under the pointer",
                $"{(followed is Vector2 f ? Figures.Point(f, 3) : "not in view")} for {Figures.Point(to, 3)}");

            // With the roof on, the wheel zooms toward what is under the pointer as well.
            Stage.OpenLevel(Stage.FloorHeights.Count);
            yield return Until(() => Stage.ExampleRoomsAway == 0, 5);
            yield return Settled();
            p.Check(Stage.LookedFloor == null, "with the roof on it looks at no floor in particular");
            under = Stage.PointUnder(at);
            far = Stage.LookDistance;
            for (var i = 0; i < 4; i++) Stage.ZoomBy(-3f, at);
            yield return null;
            yield return null;
            seen = under is Vector3 point2 ? Stage.PictureOf(point2) : null;
            p.Check(seen is Vector2 there2 && (there2 - at).magnitude < 0.02f && Stage.LookDistance < far * 0.5f,
                "and the wheel zooms toward what is under the pointer there too", $"{(seen is Vector2 s2 ? Figures.Point(s2, 3) : "not in view")} for {Figures.Point(at, 3)}");

            Stage.ResetView();
            Stage.Inside = wasInside;
        }

        /// <summary>The example's rooms standing whole on the floor opened that are not in the stage's picture.</summary>
        private static List<string> RoomsOutOfPicture()
        {
            var rooms = Stage.ExampleShown?.Rooms;
            var outside = new List<string>();
            if (rooms == null) return outside;
            for (var i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].Room.EndCap || rooms[i].Room.Divider || Stage.ExampleRoomShown(rooms[i]) != PlanRoomShown.Whole) continue;
                var point = Stage.ExamplePointOf(i);
                if (!(point is Vector2 at) || at.x < -0.02f || at.x > 1.02f || at.y < -0.02f || at.y > 1.02f) outside.Add($"{rooms[i].Room.Name} at {(point.HasValue ? Figures.Point(point.Value) : "behind")}");
            }
            return outside;
        }

        /// <summary>Every dungeon and camp read lays out an example, a dungeon from its entrance and with no two rooms in each other.</summary>
        private static IEnumerator EveryDungeonLaysOut(Probe p)
        {
            if (Locations.Now != Locations.State.Read) p.Skip("the locations are not read");
            var rooms = X.Catalog.Select(PlaceOf).Where(s => s != null && s.IsRoom && s.Contents?.Room != null).ToList();
            var dungeons = X.Catalog.Where(e => PlaceOf(e)?.Contents?.Dungeon != null && !PlaceOf(e).IsRoom).ToList();
            if (dungeons.Count == 0) p.Skip("no location read builds a dungeon or camp");
            var wrong = new List<string>();
            var told = new List<string>();
            foreach (var entry in dungeons)
            {
                var place = PlaceOf(entry);
                var plan = place.Contents.Dungeon;
                var shapes = rooms.Where(r => ((int)r.Room.m_theme & plan.Themes) != 0).Select(r => r.Contents.Room).ToList();
                var rules = place.Rules.FirstOrDefault();
                var radius = rules != null ? Mathf.Max(rules.m_exteriorRadius, rules.m_interiorRadius) : 0f;
                var turned = rules == null || rules.m_randomRotation || rules.m_slopeRotation;
                var counts = new List<int>();
                for (var seed = 1; seed <= 3; seed++)
                {
                    var dice = new Dice(seed * 7919);
                    var example = DungeonLayout.Build(plan, shapes, DungeonLayout.Site(plan, radius, turned, dice), dice);
                    counts.Add(example.Rooms.Count);
                    if (example.Rooms.Count == 0) wrong.Add($"{entry.Name} laid out nothing");
                    else if (plan.Algorithm == "Dungeon" && !example.Rooms[0].Room.Entrance) wrong.Add($"{entry.Name} does not start at an entrance");
                    if (plan.Algorithm != "Dungeon") continue;
                    var solid = example.Rooms.Where(r => !r.Room.EndCap && !r.Room.Divider).ToList();
                    for (var i = 0; i < solid.Count; i++)
                    {
                        for (var j = i + 1; j < solid.Count; j++)
                        {
                            var a = solid[i];
                            var b = solid[j];
                            var shrunkA = new Vec3(a.Room.Size.X - 0.1f, a.Room.Size.Y - 0.1f, a.Room.Size.Z - 0.1f);
                            var shrunkB = new Vec3(b.Room.Size.X - 0.1f, b.Room.Size.Y - 0.1f, b.Room.Size.Z - 0.1f);
                            if (Boxes.Overlap(a.Position, a.Rotation, shrunkA, b.Position, b.Rotation, shrunkB)) wrong.Add($"{entry.Name}: {a.Room.Name} in {b.Room.Name}");
                        }
                    }
                }
                told.Add($"{entry.Name} {string.Join("/", counts.Select(c => Numbers.Count(c)))}");
            }
            p.Note($"{Numbers.Count(dungeons.Count)} dungeons and camps, rooms in three examples each: " + string.Join(", ", told));
            p.Check(wrong.Count == 0, "every one lays out, dungeons from an entrance with no room in another", string.Join("; ", wrong.Distinct().Take(8)));
            yield break;
        }

        /// <summary>Every dungeon or camp read heads a group of its own, tagged, with every room under it indented.</summary>
        private static IEnumerator EveryDungeonHeadsItsGroup(Probe p)
        {
            if (Locations.Now != Locations.State.Read) p.Skip("the locations are not read");
            var places = X.Catalog.Where(e => PlaceOf(e) != null).ToList();
            var wrong = new List<string>();
            foreach (var entry in places.Where(e => !PlaceOf(e).IsRoom && PlaceOf(e).Contents?.Dungeon != null))
            {
                if (!entry.Group.Contains(" · ") || entry.GroupRank != 0 || entry.Indent || string.IsNullOrEmpty(entry.Tag)) wrong.Add($"{entry.Name} is listed as \"{entry.Group}\", tagged \"{entry.Tag}\"");
            }
            foreach (var group in places.Where(e => e.Indent).GroupBy(e => e.Group))
            {
                if (!places.Any(e => e.Group == group.Key && !e.Indent && !PlaceOf(e).IsRoom)) wrong.Add($"\"{group.Key}\" has rooms but no location heading it");
            }
            p.Note($"{Numbers.Count(places.Count(e => e.Indent))} rooms under {Numbers.Count(places.Where(e => e.Indent).Select(e => e.Group).Distinct().Count())} dungeons and camps");
            p.Check(wrong.Count == 0, "every dungeon and camp heads its group, its rooms under it", string.Join("; ", wrong.Take(6)));
            yield break;
        }
    }
}
