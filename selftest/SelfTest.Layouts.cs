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
            // Taking it off leaves the camera where it was put.
            Stage.TurnTo(Stage.Yaw, 10f);
            Stage.ToggleRoof();
            p.Check(Stage.Cutting, "until its chip takes it off", Stage.CutLabel);
            p.Check(Mathf.Abs(Stage.Pitch - 10f) < 0.01f, "and the camera stays where it was put", $"{Numbers.Fixed(Stage.Pitch, 0)} degrees");
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
                // A dungeon's or camp's example is built room by room after it, each room's
                // colliders let go of only at the end of the frame it is read in: they are
                // counted once every room stands.
                if (PlaceOf(entry)?.Contents?.Dungeon != null)
                {
                    yield return Until(() => ExampleLayouts.Of(entry) && ExampleLayouts.Example != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 30);
                    yield return null;
                }
                var left = CopyOf(entry) != null ? CopyOf(entry).GetComponentsInChildren<Collider>(true) : Array.Empty<Collider>();
                if (left.Length > 0) wrong.Add($"{entry.Name} keeps {Numbers.Count(left.Length)} colliders, on {string.Join(", ", left.Take(3).Select(c => c.transform.parent != null ? c.transform.parent.name + "/" + c.name : c.name))}");
                // Each floor with what its rays landed on, to see what is taken for a floor.
                var makers = Stage.FloorMakersNow();
                if (floors.Count > 1) several.Add($"{entry.Name} ({string.Join("; ", floors.Select((f, i) => $"{Numbers.Fixed(f, 1)} m on {(i < makers.Count ? makers[i] : "?")}"))})");
            }
            p.Note(several.Count > 0 ? "with several floors: " + string.Join("; ", several) : "none with several floors");
            p.Check(wrong.Count == 0, "each has floors from the top down, cut over each, with no collider left on its copy", string.Join("; ", wrong.Take(8)));
            var drawn = ScryPanel.Drawn(PanelPart.Ruler);
            yield return Until(() => ScryPanel.Drawn(PanelPart.Ruler) > drawn, 3);
            p.Check(ScryPanel.Drawn(PanelPart.Ruler) > drawn, "the floor ruler draws beside the stage");
        }

        /// <summary>The places looked at for their floors, shown alone: an altar, towers, houses, a hut on stilts and a crypt's gate.</summary>
        private static readonly string[] PicturedPlaces = { "Bonemass", "CharredTowerRuins3", "WoodHouse12", "WoodHouse2", "Mistlands_GuardTower1_new", "SwampHut1", "SunkenCrypt4" };

        /// <summary>
        /// The places looked at for their floors, each shown alone on a stage as tall as it goes
        /// with every floor opened in turn and pictured: each floor is cut under the ceiling found
        /// over it; Bonemass's altar, whose top no one walks to, is no floor; the low roof of a
        /// house is found over its floor and the floor cut under it; and the swamp hut's raised
        /// floor is one, rolled again until the hut stands on its stilts, as a roll may leave only
        /// them. Each floor is told with its cut, its ceiling and what its rays landed on, and each
        /// place with its top and the ruler's roof over the top floor's cut.
        /// </summary>
        private static IEnumerator FloorsPictured(Probe p)
        {
            var places = X.Catalog.Where(e => PlaceOf(e) != null && PicturedPlaces.Contains(e.Name)).OrderBy(e => Array.IndexOf(PicturedPlaces, e.Name)).ToList();
            if (places.Count == 0) p.Skip("none of " + string.Join(", ", PicturedPlaces) + " is in the catalog");
            var wasInside = Stage.Inside;
            Stage.Inside = false;
            ScryPanel.Compact = false;
            BigStage();
            var over = new List<string>();
            List<float> altar = null;
            List<float> hut = null;
            var hutStood = false;
            string house = null;
            string Fixed(float metres) => Numbers.Fixed(metres, 1);
            foreach (var entry in places)
            {
                var place = PlaceOf(entry);
                Select(entry);
                yield return Until(() => PlaceAssets.State(place) != PlaceLoad.Loading && CopyOf(entry) != null, 20);
                if (CopyOf(entry) == null)
                {
                    p.Note($"{entry.Name} stood nowhere");
                    continue;
                }
                yield return null;
                // A roll of the swamp hut may stand only its stilts and ladder; the hut on them reaches past HutStands.
                for (var roll = 0; entry.Name == "SwampHut1" && Stage.ModelTop < HutStands && roll < 8; roll++)
                {
                    var was = CopyOf(entry);
                    Previews.Rebuild();
                    yield return Until(() => CopyOf(entry) != null && CopyOf(entry) != was, 10);
                    yield return null;
                }
                if (entry.Name == "SwampHut1") hutStood = Stage.ModelTop >= HutStands;
                var floors = Stage.FloorHeights.ToList();
                var cuts = Stage.CutHeights.ToList();
                var ceilings = Enumerable.Range(0, floors.Count).Select(Stage.CeilingOver).ToList();
                var makers = Stage.FloorMakersNow();
                for (var i = 0; i < floors.Count; i++)
                {
                    // Under the ceiling, unless that leaves less than a head's room over the floor.
                    if (ceilings[i] is float ceiling && cuts[i] > Math.Max(ceiling - PlaceView.UnderCeiling, floors[i] + PlaceView.LeastHeadroom) + 0.01f)
                        over.Add($"{entry.Name}'s floor at {Fixed(floors[i])} m cut at {Fixed(cuts[i])} m, its ceiling at {Fixed(ceiling)} m");
                }
                if (entry.Name == "Bonemass") altar = floors;
                if (entry.Name == "SwampHut1") hut = floors;
                if (entry.Name == "WoodHouse12" && floors.Count > 0)
                {
                    var ground = floors.Count - 1;
                    house = ceilings[ground] is float roof && cuts[ground] < roof ? null : $"its floor at {Fixed(floors[ground])} m cut at {Fixed(cuts[ground])} m, {(ceilings[ground] is float c ? $"its ceiling at {Fixed(c)} m" : "no ceiling found")}";
                }
                if (floors.Count == 0)
                {
                    p.Note($"{entry.Name}: no floor");
                    continue;
                }
                p.Note($"{entry.Name}: " + string.Join("; ", floors.Select((f, i) => $"{Fixed(f)} m cut at {Fixed(cuts[i])} m{(ceilings[i] is float c ? $" under a ceiling at {Fixed(c)} m" : "")} on {(i < makers.Count ? makers[i] : "?")}"))
                       + $"; its top {Fixed(Stage.ModelTop)} m, the ruler's roof above {Fixed(PlaceView.RoofAbove(cuts[0], Stage.ModelTop))} m");
                for (var level = 0; level < floors.Count; level++)
                {
                    Stage.OpenLevel(level);
                    yield return null;
                    yield return Picture(p, $"floors {entry.Name} {Numbers.Count(level + 1)} of {Numbers.Count(floors.Count)}");
                }
            }
            Stage.Inside = wasInside;
            StageAsItWas();
            p.Check(over.Count == 0, "each floor is cut under the ceiling found over it", string.Join("; ", over.Take(8)));
            if (altar != null) p.Check(!altar.Any(f => f > 3f), "Bonemass's altar, whose top no one walks to, is no floor", string.Join(", ", altar.Select(Fixed)) + " m");
            if (places.Any(e => e.Name == "WoodHouse12")) p.Check(house == null, "a house's low roof is found over its floor, and the floor cut under it", house);
            if (hut != null && hutStood) p.Check(hut.Any(f => Math.Abs(f - 3.1f) < 0.4f), "the swamp hut's raised floor, about 3 m up, is a floor", string.Join(", ", hut.Select(Fixed)) + " m");
            else if (hut != null) p.Note("no roll of the swamp hut of nine stood the hut on its stilts");
        }

        /// <summary>How high the swamp hut reaches over its root with the hut on its stilts; its stilts and ladder alone reach about 5 m.</summary>
        private const float HutStands = 6f;

        /// <summary>
        /// How the parts a floor check names stand in the copy: whether each is there, and each of
        /// its colliders on or off, a trigger or not, its layer and its top over the copy's root, to
        /// tell why a ray met a surface the record keeps, or not.
        /// </summary>
        private static string PartsNow(Transform root, IEnumerable<string> parts)
        {
            if (root == null) return "";
            var told = new List<string>();
            var all = root.GetComponentsInChildren<Transform>(true);
            foreach (var name in parts.Take(3))
            {
                var found = all.Where(t => t.parent != null && t.parent.name + "/" + t.name == name).ToList();
                if (found.Count == 0) told.Add(name + ": not in the copy");
                foreach (var t in found)
                {
                    var colliders = t.GetComponents<Collider>().Select(c => $"{c.GetType().Name} {(c.enabled ? "on" : "off")}{(c.isTrigger ? " trigger" : "")} layer {LayerMask.LayerToName(c.gameObject.layer)} top {Numbers.Fixed(root.InverseTransformPoint(c.bounds.max).y, 3)}");
                    told.Add($"{name}: {(t.gameObject.activeInHierarchy ? "there" : "not there")}, {string.Join(", ", colliders)}");
                }
            }
            return told.Count == 0 ? "" : "; in the copy now: " + string.Join("; ", told);
        }

        /// <summary>
        /// The floors of a spread of places and a dungeon's rooms, shown twice: the first time
        /// found by rays and kept, the second told from what was kept, which is checked against
        /// rays cast over the same roll; then a dungeon's next layout told from what was kept
        /// alone, waking no room.
        /// </summary>
        private static IEnumerator FloorsKept(Probe p)
        {
            var places = X.Catalog.Where(e => PlaceOf(e) != null).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            if (places.Count == 0) p.Skip("there are no locations");
            var picks = Spread(places, 10);
            picks.AddRange(places.Where(e => e.Name.IndexOf("Tower", StringComparison.OrdinalIgnoreCase) >= 0 || e.Name.IndexOf("Cave", StringComparison.OrdinalIgnoreCase) >= 0).Take(4));
            picks = picks.Distinct().ToList();
            var crypt = Crypt();
            var begun = DateTime.UtcNow.AddSeconds(-1);
            var verified = 0;
            var differed = 0;
            var lastDiffered = "";
            var minor = 0;
            var lastMinor = "";
            FloorProbe.Checked = (kept, rays, present, keys, met, root) =>
            {
                verified++;
                var check = FloorCheck.Of(kept, rays, present, keys, met);
                if (check.Same) return;
                if (check.Minor)
                {
                    minor++;
                    lastMinor = check.Told;
                    return;
                }
                differed++;
                lastDiffered = check.Told + PartsNow(root, check.Parts);
            };
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var entry in picks)
                {
                    var place = PlaceOf(entry);
                    Select(entry);
                    yield return Until(() => PlaceAssets.State(place) != PlaceLoad.Loading && CopyOf(entry) != null, 20);
                    yield return null;
                }
                if (crypt == null) continue;
                Select(crypt);
                yield return Until(() => CopyOf(crypt) != null && ExampleLayouts.Of(crypt) && ExampleLayouts.Example != null
                                         && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 90);
            }
            FloorProbe.Checked = null;
            p.Note($"{Numbers.Count(FloorRecords.Count)} kinds of place have their floors kept; {Numbers.Count(verified)} told from what was kept and checked against their rays");
            p.Check(FloorRecords.Count > 0, "the floors of the places shown are kept");
            p.Check(verified > 0, "shown again, places' floors are told from what was kept");
            p.Check(differed == 0, "what was kept tells each roll's floors and creatures' ground as its own rays find them", $"{Numbers.Count(differed)} of {Numbers.Count(verified)} differ; {lastDiffered}");
            if (minor > 0) p.Note($"{Numbers.Count(minor)} of {Numbers.Count(verified)} differ by a cell or two, as a ray meets a curved part's seam or not; {lastMinor}");

            // The dungeon's next layout: every room's floors from what was kept, none woken.
            if (crypt == null) yield break;
            var fromKept = FloorProbe.FromKept;
            var probed = FloorProbe.Probed;
            ExampleLayouts.Another();
            // The stage takes up the new layout on its next frames.
            yield return null;
            yield return null;
            yield return Until(() => Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 90);
            var told = FloorProbe.FromKept - fromKept;
            var cast = FloorProbe.Probed - probed;
            p.Note($"{crypt.Name}'s next layout: {Numbers.Count(told)} rooms' floors from what was kept, {Numbers.Count(cast)} probed (a roll bringing a part not yet seen)");
            p.Check(told > 0 && told >= cast, "a dungeon's next layout finds most of its rooms' floors in what was kept", $"{Numbers.Count(told)} kept, {Numbers.Count(cast)} probed");
            yield return Until(() => !FloorRecords.Busy, 10);
            var folder = System.IO.Path.Combine(Settings.CacheFolder, "floors");
            var files = System.IO.Directory.Exists(folder) ? System.IO.Directory.GetFiles(folder, "*.dat").Count(f => System.IO.File.GetLastWriteTimeUtc(f) >= begun) : 0;
            p.Check(files > 0, "and they are kept in the cache folder for the next time", $"{Numbers.Count(files)} files written");
            p.Note($"the floors of {Numbers.Count(FloorRecords.Count)} kinds of place kept, {Numbers.Count(FloorRecords.Held)} of them held now");
        }

        /// <summary>Puts the floor probe back to telling from what was kept alone.</summary>
        private static void KeptFloorsUnverified() => FloorProbe.Checked = null;

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

        /// <summary>
        /// The dungeons whose rooms are rock or stacked high, the Frost Caves, the M&#xF6;rkhalla ones and
        /// Hildir's sealed tower: each one's example is built and opened floor by floor, told with
        /// how many rooms stand on each floor; a floor no room stands on is a floor found where
        /// there is none, some room always stands on the stage, and every room stands whole on
        /// some floor, so it can be opened and gone to. M&#xF6;rkhalla, whose floors come out
        /// differently example to example, is laid out three times, each floor of its first
        /// example pictured, as each other's is. Its creatures are told: how
        /// many dropped to the ground under their points and how many fly. Every M&#xF6;rkhalla room
        /// has a floor at its foot and none lies over its gate, and the sealed tower's floors are
        /// a storey apart from its top room to its foot.
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
            BigStage();
            var empty = new List<string>();
            var blank = new List<string>();
            var unreached = new List<string>();
            var footless = new List<string>();
            var untiled = new List<string>();
            var unlike = new List<string>();
            ScryPanel.PlanFolded = false;
            var gaps = new List<string>();
            string Told(IEnumerable<float> floors) => string.Join(", ", floors.Select(f => Numbers.Fixed(f, 1)));
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
                    if (example == 0 && entry.Name == "Hildir_plainsfortress")
                    {
                        // With nothing outside, it shows its inside though Outside was left chosen.
                        Stage.Inside = false;
                        yield return null;
                        yield return null;
                        p.Check(Stage.ShowsInside && !Stage.HasOutside, "the sealed tower, with nothing outside, shows its inside though Outside is chosen", "outside: " + Stage.OutsideTold());
                        Stage.Inside = true;
                        yield return null;
                    }
                    var now = Stage.FloorHeights.ToList();
                    if (entry.Name == "Hildir_plainsfortress")
                    {
                        // Its rooms stand a storey (4 m) on another, each with a floor. A floor found
                        // under a deck's top rather than on it sits up to 0.7 m lower, so floors stand
                        // 3.3 to 4.7 m apart; a storey without one leaves 8 m.
                        var apart = now.Zip(now.Skip(1), (above, below) => (above, below)).Where(f => f.above - f.below > 6f).ToList();
                        if (apart.Count > 0) gaps.Add($"{entry.Name}: {string.Join(", ", apart.Select(f => $"{Numbers.Fixed(f.above, 1)} to {Numbers.Fixed(f.below, 1)} m"))}");
                    }
                    if (entry.Name == "MorkBorg")
                    {
                        // Each room stands on a floor at its foot, and none lies over its gate, the ground outside.
                        var missing = Stage.ExampleShown.Rooms.Where(r => !now.Any(f => f >= r.Position.Y - r.Room.Size.Y / 2f - 0.5f && f <= r.Position.Y - r.Room.Size.Y / 2f + 3f))
                            .Select(r => $"{r.Room.Name} from {Numbers.Fixed(r.Position.Y - r.Room.Size.Y / 2f, 1)} m").ToList();
                        if (missing.Count > 0) footless.Add($"layout {Numbers.Count(example + 1)}: {string.Join(", ", missing)}");
                        if (now.Any(f => f > 0.5f)) footless.Add($"layout {Numbers.Count(example + 1)}: a floor over its gate at {Told(now.Where(f => f > 0.5f).ToList())} m");
                        if (missing.Count > 0) p.Note("its rooms: " + string.Join("; ", Stage.ExampleRoomGroundTold()));
                    }
                    var told = new List<string>();
                    var reached = new HashSet<PlacedRoom>();
                    var tiled = new HashSet<PlacedRoom>();
                    for (var level = 0; level < Stage.FloorHeights.Count; level++)
                    {
                        Stage.OpenLevel(level);
                        yield return Until(() => Stage.ExampleRoomsKept, 5);
                        var rooms = Stage.ExampleRoomsOnFloor;
                        told.Add($"{Numbers.Fixed(Stage.FloorHeights[level], 1)} m, {Numbers.Count(rooms)} rooms, {Numbers.Count(Stage.ExampleRoomsShown - Stage.ExampleRoomsAway)} standing, {Numbers.Count(Stage.CreaturesAboveCut)} creatures above the cut");
                        if (rooms == 0) empty.Add($"{entry.Name} at {Numbers.Fixed(Stage.FloorHeights[level], 1)} m");
                        if (Stage.ExampleRoomsAway >= Stage.ExampleRoomsShown) blank.Add($"{entry.Name} at {Numbers.Fixed(Stage.FloorHeights[level], 1)} m");
                        if (rooms > 0) foreach (var room in Stage.ExampleShown.Rooms) if (Stage.ExampleRoomShown(room) == PlanRoomShown.Whole) reached.Add(room);
                        // The rooms the plan tiles on this floor, and those it dashes under it.
                        var opened = level;
                        yield return Until(() => ScryPanel.PlanDrawnFor(Stage.ExampleShown, opened), 2);
                        foreach (var room in Stage.ExampleShown.Rooms) if (ScryPanel.PlanTilesRoom(room)) tiled.Add(room);
                        // The stage stands whole the rooms the plan tiles, by one rule.
                        var odd = Stage.ExampleShown.Rooms.Where(r => ScryPanel.PlanTilesRoom(r) != (Stage.ExampleRoomShown(r) == PlanRoomShown.Whole)).Select(r => r.Room.Name).ToList();
                        if (odd.Count > 0) unlike.Add($"{entry.Name} at {Numbers.Fixed(Stage.FloorHeights[level], 1)} m: {string.Join(", ", odd.Take(4))}");
                        told[told.Count - 1] += $", plan {Numbers.Count(ScryPanel.PlanTilesOn)} tiled, {Numbers.Count(ScryPanel.PlanTilesBelow)} dashed under it, {Numbers.Count(ScryPanel.PlanDoorsMarked)} doors marked up or down";
                        if (example == 0) yield return Picture(p, $"floors {entry.Name} inside {Numbers.Count(level + 1)} of {Numbers.Count(Stage.FloorHeights.Count)}");
                    }
                    // What stands above the top floor's cut is reached only by moving the cut up by hand.
                    if (Stage.CutHeights.Count > 0) p.Note($"{entry.Name}: top floor's cut {Numbers.Fixed(Stage.CutHeights[0], 1)} m, the place's top {Numbers.Fixed(Stage.ModelTop, 1)} m, the ruler's roof above {Numbers.Fixed(PlaceView.RoofAbove(Stage.CutHeights[0], Stage.ModelTop), 1)} m");
                    var never = Stage.ExampleShown?.Rooms.Where(r => !r.Room.EndCap && !r.Room.Divider && !reached.Contains(r)).Select(r => $"{r.Room.Name} at {Numbers.Fixed(r.Position.Y, 1)} m").ToList() ?? new List<string>();
                    var notTiled = Stage.ExampleShown?.Rooms.Where(r => !r.Room.EndCap && !r.Room.Divider && !tiled.Contains(r)).Select(r => $"{r.Room.Name} at {Numbers.Fixed(r.Position.Y, 1)} m").ToList() ?? new List<string>();
                    if (notTiled.Count > 0) untiled.Add($"{entry.Name}: {string.Join(", ", notTiled.Take(6))}");
                    if (never.Count > 0) unreached.Add($"{entry.Name}: {string.Join(", ", never.Take(6))}");
                    p.Note($"{entry.Name} ({entry.DisplayName}), {Numbers.Count(Stage.ExampleRoomsTotal)} rooms, {Numbers.Count(Stage.FloorHeights.Count)} floors: " + string.Join("; ", told)
                           + $"; creatures {Numbers.Count(Stage.CreaturesMade)}, {Numbers.Count(Stage.CreaturesDropped)} dropped to the ground under their points, {Numbers.Count(Stage.CreaturesFlying)} flying ({Stage.FlyersTold()})");
                    if (example == 0 && entry.Name == "MorkBorg") p.Note("its rooms' own floors, as each is shown alone: " + string.Join("; ", Stage.ExampleRoomFloorsTold()));
                    // A sunken crypt's and a frost cave's rooms each with its box, doorways, ground and floors,
                    // which tell why each of their floors is found.
                    if (example == 0 && (entry.Name == "SunkenCrypt4" || entry.Name == "MountainCave02")) p.Note("its rooms: " + string.Join("; ", Stage.ExampleRoomGroundTold()));
                }
            }
            Stage.Inside = wasInside;
            StageAsItWas();
            p.Check(empty.Count == 0, "every floor found in their examples has rooms on it", string.Join("; ", empty));
            p.Check(blank.Count == 0, "and some room stands on the stage on every floor", string.Join("; ", blank));
            p.Check(unreached.Count == 0, "and every room stands whole on some floor", string.Join("; ", unreached));
            p.Check(footless.Count == 0, "every M\u00f6rkhalla room has a floor at its foot, none over its gate", string.Join("; ", footless));
            p.Check(gaps.Count == 0, "the sealed tower's floors are a storey apart, none missing", string.Join("; ", gaps));
            p.Check(untiled.Count == 0, "every room is tiled on the plan of some floor", string.Join("; ", untiled.Take(8)));
            p.Check(unlike.Count == 0, "on each floor the stage stands whole the rooms the plan tiles, the rest dimmed or put away", string.Join("; ", unlike.Take(8)));
        }

        /// <summary>The setting for the dimmed rooms' lights as it was before the self-test set it; null while it is as it was.</summary>
        private static bool? _lightsWere;

        /// <summary>Puts the setting for the dimmed rooms' lights back as it was.</summary>
        private static void LightsAsTheyWere()
        {
            if (_lightsWere is bool was) Settings.LightDimmedRooms = was;
            _lightsWere = null;
        }

        /// <summary>How long the stage's films took over ten frames, after three to settle.</summary>
        private sealed class FilmTimes
        {
            public double Mean { get; private set; }

            public IEnumerator Take()
            {
                for (var i = 0; i < 3; i++) yield return null;
                var sum = 0.0;
                for (var i = 0; i < 10; i++)
                {
                    yield return null;
                    sum += Stage.LastFilmMs;
                }
                Mean = sum / 10;
            }
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

            // The stage asks for the copy's animator every frame; a copy that stays as it is is
            // walked for it only when it gains or loses a part, as creatures still being placed
            // do, so it is counted once they all stand.
            var waiting = Stage.CreaturesWaiting;
            yield return Until(() => Stage.CreaturesWaiting == 0, 10);
            var walks = ClipPlayer.Walks;
            for (var i = 0; i < 30; i++) yield return null;
            p.Check(ClipPlayer.Walks - walks < 10, "a dungeon that stays as it is is not searched for its animator every frame",
                $"{Numbers.Count(ClipPlayer.Walks - walks)} times in 30 frames; {Numbers.Count(waiting)} creatures were still to be placed, {Numbers.Count(Stage.CreaturesWaiting)} after");

            // The rooms dimmed below the floor opened are filmed without their own lights, which
            // cost about half of each film, unless the setting keeps them; filmed ten frames each way.
            _lightsWere = Settings.LightDimmedRooms;
            Settings.LightDimmedRooms = false;
            var unlit = new FilmTimes();
            yield return unlit.Take();
            var lit = Stage.DimmedLights().Count;
            p.Check(Stage.ExampleRoomsDimmed > 0 && lit == 0, "the rooms dimmed below the floor opened are filmed without their own lights",
                $"{Numbers.Count(Stage.ExampleRoomsDimmed)} rooms dimmed, {Numbers.Count(lit)} lights on");
            Settings.LightDimmedRooms = true;
            var kept = new FilmTimes();
            yield return kept.Take();
            var relit = Stage.DimmedLights().Count;
            // Mörkhalla's rooms below its top floor hold lights; another dungeon's may hold none.
            if (entry.Name == "MorkBorg") p.Check(relit > 0, "with LightDimmedRooms on they keep them", "none came on again");
            LightsAsTheyWere();
            p.Note($"filming it: {Numbers.Fixed(unlit.Mean, 1)} ms with the dimmed rooms unlit, {Numbers.Fixed(kept.Mean, 1)} ms with their {Numbers.Count(relit)} lights");

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
