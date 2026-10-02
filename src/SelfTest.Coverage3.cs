using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for the Raids tab's groups, a raid's roll on the stage and the
    /// runestones' texts.
    /// </summary>
    internal static partial class SelfTest
    {
        private static Entry BossFight(Entry entry) => entry?.Source is RandomEvent raid && Knowledge.BossOfEvent(raid.m_name) != null ? entry : null;

        private static GameObject BossOf(Entry fight) => Knowledge.BossOfEvent(((RandomEvent)fight.Source).m_name);

        /// <summary>Bosses' own events are grouped apart from the raids, named for their bosses, in the order the bosses are fought, and linked both ways.</summary>
        private static IEnumerator BossFightsGrouped(Probe p)
        {
            var raids = X.Catalog.Where(e => e.Kind == Kind.Raid && e.Source is RandomEvent).ToList();
            var fights = raids.Where(e => BossFight(e) != null).ToList();
            p.Note(string.Join("; ", raids.GroupBy(e => e.Group).Select(g => $"{g.Key}: {g.Count()}")));
            if (fights.Count == 0) p.Skip("no boss names an event of its own");
            var astray = fights.Where(e => e.Group != "Boss fights").Select(e => $"{e.Name} under {e.Group}").ToList();
            p.Check(astray.Count == 0, "every boss's own event is under Boss fights", string.Join(", ", astray));
            var intruders = raids.Except(fights).Where(e => e.Group == "Boss fights").Select(e => e.Name).ToList();
            p.Check(intruders.Count == 0, "and nothing else is", string.Join(", ", intruders));
            p.Check(fights.All(e => !string.IsNullOrEmpty(e.DisplayName)), "each goes by a name", string.Join(", ", fights.Where(e => string.IsNullOrEmpty(e.DisplayName)).Select(e => e.Name)));

            X.SearchEverything("");
            X.KindFilter = Kind.Raid;
            yield return null;
            var listed = X.Results.ToList();
            X.KindFilter = null;
            var firstFight = listed.FindIndex(e => e.Group == "Boss fights");
            p.Check(firstFight >= 0 && listed.Take(firstFight).All(e => e.Group == "Raids"), "the tab lists the raids first, then the boss fights", string.Join(", ", listed.Select(e => e.Group).Distinct()));
            var inOrder = listed.Where(e => e.Group == "Boss fights").ToList();
            var health = inOrder.Select(e => BossOf(e).GetComponent<Character>()?.m_health ?? 0f).ToList();
            p.Check(health.Zip(health.Skip(1), (a, b) => a <= b).All(rising => rising), "the boss fights in the order the bosses are fought", string.Join(", ", inOrder.Select(e => e.DisplayName)));

            var fight = fights[0];
            var boss = BossOf(fight);
            p.Check(Facts.For(fight).Links.TryGetValue("On", out var toBoss) && toBoss == boss.name, $"{fight.DisplayName}'s page goes to its boss", toBoss);
            var bossEntry = Pick(Kind.Creature, boss.name);
            if (bossEntry != null) p.Check(Facts.For(bossEntry).Links.TryGetValue("Its fight", out var toFight) && toFight == fight.Key, "and the boss's page to its fight", toFight);
        }

        /// <summary>A raid stands on the stage as the first roll of its creatures, every one rolled there, and Roll again rolls them anew.</summary>
        private static IEnumerator RaidWave(Probe p)
        {
            var raid = Pick(Kind.Raid, "army_eikthyr") ?? X.Catalog.FirstOrDefault(e => e.Kind == Kind.Raid && Stage.IsStaged(e));
            if (raid == null || !Stage.IsStaged(raid)) p.Skip("no raid brings creatures");
            Select(raid);
            yield return Until(() => CopyOf(raid) != null, 15);
            var crowd = CopyOf(raid);
            if (!p.Check(crowd != null, $"{raid.DisplayName} stands on the stage")) yield break;
            p.Note(RaidWords.FirstRoll(RaidCrowd.LastRoll, name => name));
            p.Check(RaidCrowd.LastFor == raid && crowd.transform.childCount == RaidCrowd.LastRoll.Count, "every creature rolled stands there", $"{crowd.transform.childCount} stand, {RaidCrowd.LastRoll.Count} rolled");

            var spawns = ((RandomEvent)raid.Source).m_spawn.Where(s => s?.m_prefab != null).ToList();
            var counts = new List<int>();
            var wrong = new List<string>();
            for (var i = 0; i < 8; i++)
            {
                Previews.Rebuild();
                yield return Until(() => CopyOf(raid) != null && CopyOf(raid) != crowd, 5);
                crowd = CopyOf(raid);
                if (crowd == null)
                {
                    wrong.Add($"roll {i + 1} stands nowhere");
                    continue;
                }
                counts.Add(RaidCrowd.LastRoll.Count);
                if (crowd.transform.childCount != RaidCrowd.LastRoll.Count) wrong.Add($"roll {i + 1}: {crowd.transform.childCount} of {RaidCrowd.LastRoll.Count} stand");
                foreach (var creature in RaidCrowd.LastRoll)
                {
                    var spawn = spawns.FirstOrDefault(s => s.m_prefab.name == creature.Prefab);
                    if (spawn != null && (creature.Level < spawn.m_minLevel || creature.Level > Math.Max(spawn.m_minLevel, spawn.m_maxLevel))) wrong.Add($"{creature.Prefab} at level {creature.Level}");
                }
            }
            p.Note($"8 more rolls brought {string.Join(", ", counts)} creatures");
            p.Check(wrong.Count == 0, "Roll again rolls them anew each time, whole and at the levels it allows", string.Join("; ", wrong.Take(6)));
        }

        /// <summary>Every raid that brings creatures rolls them onto the stage, every creature rolled standing.</summary>
        private static IEnumerator EveryRaidWave(Probe p)
        {
            var raids = X.Catalog.Where(e => e.Kind == Kind.Raid && Stage.IsStaged(e)).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            if (raids.Count == 0) p.Skip("no raid brings creatures");
            var wrong = new List<string>();
            foreach (var raid in raids)
            {
                Select(raid);
                yield return Until(() => CopyOf(raid) != null, 10);
                var crowd = CopyOf(raid);
                if (crowd == null) wrong.Add($"{raid.Name} stands nowhere");
                else if (crowd.transform.childCount != RaidCrowd.LastRoll.Count) wrong.Add($"{raid.Name}: {crowd.transform.childCount} of {RaidCrowd.LastRoll.Count} stand");
            }
            p.Note($"{raids.Count} raids bring creatures");
            p.Check(wrong.Count == 0, "each stands as rolled", string.Join("; ", wrong.Take(8)));
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
                else if (floors.Zip(floors.Skip(1), (above, below) => above > below).Any(ordered => !ordered)) wrong.Add($"{entry.Name}'s floors are out of order: {string.Join(", ", floors)}");
                else if (cuts.Zip(floors, (cut, floor) => cut > floor).Any(over => !over)) wrong.Add($"{entry.Name} is cut below a floor");
                var left = CopyOf(entry) != null ? CopyOf(entry).GetComponentsInChildren<Collider>(true).Length : 0;
                if (left > 0) wrong.Add($"{entry.Name} keeps {left} colliders");
                // Each floor with what its rays landed on, to see what is taken for a floor.
                var makers = Stage.FloorMakersNow();
                if (floors.Count > 1) several.Add($"{entry.Name} ({string.Join("; ", floors.Select((f, i) => $"{f:0.0} m on {(i < makers.Count ? makers[i] : "?")}"))})");
            }
            p.Note(several.Count > 0 ? "with several floors: " + string.Join("; ", several) : "none with several floors");
            p.Check(wrong.Count == 0, "each has floors from the top down, cut over each, with no collider left on its copy", string.Join("; ", wrong.Take(8)));
            var drawn = ScryPanel.RulersDrawn;
            yield return Until(() => ScryPanel.RulersDrawn > drawn, 3);
            p.Check(ScryPanel.RulersDrawn > drawn, "the floor ruler draws beside the stage");
        }

        /// <summary>
        /// The dungeons whose rooms are rock or stacked high, the Frost Caves, the M&#xF6;rkhalla ones and
        /// Hildir's sealed tower: each one's example is built and opened floor by floor, told with
        /// how many rooms stand on each floor; a floor no room stands on is a floor found where
        /// there is none, some room always stands on the stage, and every room stands whole on
        /// some floor, so it can be opened and gone to. M&#xF6;rkhalla, whose floors come out
        /// differently example to example, is laid out three times. Its creatures are told: how
        /// many dropped to the ground under their points and how many fly.
        /// </summary>
        private static IEnumerator CaveFloors(Probe p)
        {
            var named = new[] { "MountainCave02", "MorkBorg", "TheHole01", "Hildir_plainsfortress" };
            var caves = X.Catalog.Where(e => PlaceOf(e) != null && !PlaceOf(e).IsRoom
                                             && (named.Contains(e.Name) || e.DisplayName.IndexOf("rkhalla", StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            if (caves.Count == 0) p.Skip("none of " + string.Join(", ", named) + " is in the catalog");
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            var empty = new List<string>();
            var blank = new List<string>();
            var unreached = new List<string>();
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
                    var told = new List<string>();
                    var reached = new HashSet<PlacedRoom>();
                    for (var level = 0; level < Stage.FloorHeights.Count; level++)
                    {
                        Stage.OpenLevel(level);
                        yield return Until(() => Stage.ExampleRoomsKept, 5);
                        var rooms = Stage.ExampleRoomsOnFloor;
                        told.Add($"{Stage.FloorHeights[level]:0.0} m, {rooms} rooms, {Stage.ExampleRoomsShown - Stage.ExampleRoomsAway} standing, {Stage.CreaturesAboveCut} creatures above the cut");
                        if (rooms == 0) empty.Add($"{entry.Name} at {Stage.FloorHeights[level]:0.0} m");
                        if (Stage.ExampleRoomsAway >= Stage.ExampleRoomsShown) blank.Add($"{entry.Name} at {Stage.FloorHeights[level]:0.0} m");
                        if (rooms > 0) foreach (var room in Stage.ExampleShown.Rooms) if (Stage.ExampleRoomShown(room) == PlanRoomShown.Whole) reached.Add(room);
                    }
                    var never = Stage.ExampleShown?.Rooms.Where(r => !r.Room.EndCap && !r.Room.Divider && !reached.Contains(r)).Select(r => $"{r.Room.Name} at {r.Position.Y:0.0} m").ToList() ?? new List<string>();
                    if (never.Count > 0) unreached.Add($"{entry.Name}: {string.Join(", ", never.Take(6))}");
                    p.Note($"{entry.Name} ({entry.DisplayName}), {Stage.ExampleRoomsTotal} rooms, {Stage.FloorHeights.Count} floors: " + string.Join("; ", told)
                           + $"; creatures {Stage.CreaturesMade}, {Stage.CreaturesDropped} dropped to the ground under their points, {Stage.CreaturesFlying} flying ({Stage.FlyersTold()})");
                    if (example == 0 && entry.Name == "MorkBorg") p.Note("its rooms' own floors, as each is shown alone: " + string.Join("; ", Stage.ExampleRoomFloorsTold()));
                }
            }
            Stage.Inside = wasInside;
            p.Check(empty.Count == 0, "every floor found in their examples has rooms on it", string.Join("; ", empty));
            p.Check(blank.Count == 0, "and some room stands on the stage on every floor", string.Join("; ", blank));
            p.Check(unreached.Count == 0, "and every room stands whole on some floor", string.Join("; ", unreached));
        }

        /// <summary>
        /// A location's spawn points put their creatures on the stage with it: they stand there,
        /// the chip puts them away and back, and Roll again rolls them anew with the copy.
        /// </summary>
        private static IEnumerator PlaceCreatures(Probe p)
        {
            // One with a creature always there, so the first roll brings one.
            var place = X.Catalog.Where(e => PlaceOf(e) != null && !PlaceOf(e).IsRoom && PlaceOf(e).Contents != null && PlaceOf(e).Contents.Dungeon == null
                                             && PlaceOf(e).Contents.Creatures.Any(c => c.Chance >= 0.999f))
                .OrderBy(e => e.Name, StringComparer.Ordinal).FirstOrDefault();
            if (place == null) p.Skip("no location read puts creatures there");
            var shown = Stage.CreaturesShown;
            Stage.CreaturesShown = true;
            Select(place);
            yield return Until(() => CopyOf(place) != null && Stage.CreaturesMade > 0 && Stage.CreaturesWaiting == 0, 20);
            p.Note($"{place.Name}: {Stage.CreaturesMade} creatures, {string.Join(", ", Stage.CreatureNames.Take(10))}; its spawn points can put {string.Join(", ", PlaceOf(place).Contents.Creatures.Select(c => c.Prefab))}");
            p.Check(Stage.CreaturesMade > 0, "its spawn points' creatures stand on the stage with it", $"{Stage.CreaturesMade} made, {Stage.CreaturesWaiting} waiting");
            p.Note("they stand on layer " + Stage.CreatureLayerTold);

            Stage.CreaturesShown = false;
            p.Check(Stage.CreaturesStanding == 0, "the chip puts them away", $"{Stage.CreaturesStanding} still standing");
            Stage.CreaturesShown = true;
            p.Check(Stage.CreaturesStanding == Stage.CreaturesMade, "and back", $"{Stage.CreaturesStanding} of {Stage.CreaturesMade}");

            // Cut through the tallest of them, what of it is above the cut is still drawn: the
            // picture there changes as the creatures are put away.
            var standing = Stage.CreatureBoundsNow();
            if (Stage.HasFloors && standing.Count > 0)
            {
                var tallest = standing.OrderByDescending(b => b.size.y).First();
                var cut = tallest.min.y + tallest.size.y * 0.4f;
                Stage.CutOnStage(cut);
                yield return Settled();
                if (Stage.PictureOf(tallest.center) is Vector2 middle)
                {
                    for (var i = 0; i < 3; i++) Stage.ZoomBy(-3f, middle);
                }
                yield return Settled();
                var part = AboveIn(tallest, cut);
                var with = part != null ? PictureWithin(part.Value, out _, out _) : null;
                Stage.CreaturesShown = false;
                yield return null;
                yield return null;
                var without = part != null ? PictureWithin(part.Value, out _, out _) : null;
                Stage.CreaturesShown = true;
                var differ = with != null && without != null && with.Length == without.Length ? with.Where((c, i) => Apart(c, without[i])).Count() : 0;
                p.Check(Stage.CutLaid && differ >= Math.Max(12, (with?.Length ?? 0) / 50),
                    "cut through by a floor's cut, a creature stands whole above it",
                    $"{differ} of {with?.Length ?? 0} points above the cut changed with it put away; cut laid {Stage.CutLaid}, {tallest.size.y:0.0} m tall, cut at {cut - tallest.min.y:0.0} m up it");
                Stage.OpenLevel(Stage.FloorHeights.Count);
                Stage.ResetView();
            }
            else p.Note("it has no floor to cut through its creatures");

            var before = CopyOf(place);
            Previews.Rebuild();
            yield return Until(() => CopyOf(place) != null && CopyOf(place) != before && Stage.CreaturesMade > 0 && Stage.CreaturesWaiting == 0, 20);
            p.Check(CopyOf(place) != before && Stage.CreaturesMade > 0, "Roll again rolls them anew with it", $"{Stage.CreaturesMade} made");
            Stage.CreaturesShown = shown;
        }

        /// <summary>
        /// The Ground backdrop: a piece stands on its own biome's ground, and each biome's ground,
        /// laid in turn with the sky, looks its own, each told by the colour of the ground before
        /// the model, its grass and water, and saved as a picture of the stage beside the log; the
        /// Meadows grow grass, the sea's floor has water over it, a place paints its paths on the
        /// ground, and a dungeon room keeps the plain floor.
        /// </summary>
        private static IEnumerator GroundBackdrop(Probe p)
        {
            var entry = Pick(Kind.Piece, "piece_workbench", "wood_wall_half");
            if (entry == null) p.Skip("there is no piece to stand on the stage");
            var backdrop = Stage.BackdropIndex;
            var spin = Stage.Spin;
            var choice = Stage.GroundChoice;
            Stage.Spin = false;
            Stage.GroundChoice = "Auto";
            Select(entry);
            yield return Until(() => CopyOf(entry) != null, 10);
            Stage.BackdropIndex = 4;
            yield return Until(() => Stage.GroundShown != null, 3);
            if (!p.Check(Stage.GroundShown != null, "with the Ground backdrop it stands on ground", "the world's terrain had no material to borrow"))
            {
                Stage.BackdropIndex = backdrop;
                Stage.Spin = spin;
                Stage.GroundChoice = choice;
                yield break;
            }
            p.Check(Stage.GroundShown == StageGround.BiomeFor(entry.Biomes), "the ground of its own biome", $"{Stage.GroundShown} for {string.Join(", ", entry.Biomes)}");
            p.Note("its material: " + Stage.GroundMaterialTold());

            // Whether it draws with each distance its shader may hide it by: the material's own, none, and far.
            var hide = Stage.GroundHideDistance;
            var tried = new List<string>();
            foreach (var distance in new float?[] { null, 0f, 200f, 100000f })
            {
                Stage.GroundHideDistance = distance;
                yield return null;
                yield return null;
                yield return null;
                var below = PictureWithin(new Rect(0f, 0f, 1f, 0.15f), out _, out _);
                var bare = below?.Count(c => !Apart(c, below[0])) ?? 0;
                tried.Add($"{(distance?.ToString() ?? "the material's own")}: {(below == null ? "no picture" : below.Length - bare > below.Length / 10 ? "draws" : "nothing")}");
            }
            // And with the stage's camera rendering each way.
            foreach (var path in new[] { RenderingPath.DeferredShading, RenderingPath.Forward })
            {
                Stage.PathOverride = path;
                Stage.GroundHideDistance = 100000f;
                yield return null;
                yield return null;
                yield return null;
                var below = PictureWithin(new Rect(0f, 0f, 1f, 0.15f), out _, out _);
                var bare = below?.Count(c => !Apart(c, below[0])) ?? 0;
                tried.Add($"{path}: {(below == null ? "no picture" : below.Length - bare > below.Length / 10 ? "draws" : "nothing")}");
            }
            Stage.PathOverride = null;
            Stage.GroundHideDistance = hide;
            p.Note("hidden by distance, and by the camera's path: " + string.Join("; ", tried));
            p.Note("after: " + Stage.GroundMaterialTold());

            var folder = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-selftest-ground");
            System.IO.Directory.CreateDirectory(folder);
            var told = new List<string>();
            var looks = new List<Color32>();
            var grass = new Dictionary<string, int>();
            var water = new Dictionary<string, bool>();
            Stage.BackdropIndex = 5;
            foreach (var biome in new[] { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands", "DeepNorth", "Ocean" })
            {
                Stage.GroundBiomeOverride = biome;
                yield return null;
                yield return null;
                yield return null;
                grass[biome] = Stage.GrassCount;
                water[biome] = Stage.WaterShown;
                var front = PictureWithin(new Rect(0f, 0f, 1f, 0.15f), out _, out _);
                var whole = PictureWithin(new Rect(0f, 0f, 1f, 1f), out var width, out var height);
                if (whole != null) WriteTga(System.IO.Path.Combine(folder, $"stage-{biome}.tga"), width, height, whole);
                if (front == null || front.Length == 0) continue;
                long r = 0, g = 0, b = 0;
                foreach (var pixel in front)
                {
                    r += pixel.r;
                    g += pixel.g;
                    b += pixel.b;
                }
                var look = new Color32((byte)(r / front.Length), (byte)(g / front.Length), (byte)(b / front.Length), 255);
                looks.Add(look);
                told.Add($"{biome} #{look.r:X2}{look.g:X2}{look.b:X2}, {grass[biome]} grass{(water[biome] ? ", water" : "")}");
            }
            Stage.GroundBiomeOverride = null;
            p.Check(grass["Meadows"] > 0, "grass grows on the Meadows' ground", $"{grass["Meadows"]}");
            p.Check(water["Ocean"] && !water["Meadows"], "the sea's floor has water over it, and only it");
            p.Note($"the ground before it, by biome: {string.Join("; ", told)}; pictures in {folder}");
            var distinct = looks.Where((look, i) => !looks.Take(i).Any(other => !Apart(look, other))).Count();
            p.Check(distinct >= 5, "each biome's ground looks its own", $"{distinct} looks among {looks.Count} biomes");

            // A place paints its paths, dirt and paving on the ground under it.
            var painted = new List<string>();
            foreach (var name in new[] { "Vendor_BlackForest", "WoodVillage1", "WoodFarm1", "StartTemple" })
            {
                var place = X.Catalog.FirstOrDefault(e => e.Name == name && PlaceOf(e) != null && !PlaceOf(e).IsRoom);
                if (place == null) continue;
                Select(place);
                yield return Until(() => CopyOf(place) != null && Stage.GroundShown != null, 20);
                yield return null;
                yield return null;
                painted.Add($"{name} {Stage.GroundPaintCount}");
                if (Stage.GroundPaintCount == 0) continue;
                var whole = PictureWithin(new Rect(0f, 0f, 1f, 1f), out var width, out var height);
                if (whole != null) WriteTga(System.IO.Path.Combine(folder, $"stage-paths-{name}.tga"), width, height, whole);
                // Another biome over the painted ground, its mask grown past its plain size.
                var faults = Faults.Count;
                Stage.GroundBiomeOverride = "Swamp";
                yield return null;
                yield return null;
                Stage.GroundBiomeOverride = null;
                yield return null;
                p.Check(Faults.Count == faults && Stage.GroundShown != null, "another biome over a place's painted ground lays it all the same", $"{Faults.Count - faults} faults");
                break;
            }
            p.Note("paints on their ground: " + string.Join(", ", painted));
            p.Check(painted.Any(t => !t.EndsWith(" 0", StringComparison.Ordinal)), "a place paints its paths on the ground under it", string.Join(", ", painted));

            // Underground there is no ground of the world's.
            var room = X.Catalog.Where(e => PlaceOf(e) != null && PlaceOf(e).IsRoom).OrderBy(e => e.Name, StringComparer.Ordinal).FirstOrDefault();
            if (room != null)
            {
                Select(room);
                yield return Until(() => CopyOf(room) != null, 20);
                yield return null;
                yield return null;
                p.Check(CopyOf(room) != null && Stage.GroundShown == null, "a dungeon room keeps the plain floor", $"{room.Name}: {Stage.GroundShown ?? "no ground"}");
            }

            Stage.BackdropIndex = backdrop;
            Stage.Spin = spin;
            Stage.GroundChoice = choice;
        }

        /// <summary>
        /// What Scry costs idling with the panel closed, as between looks: its own work each frame
        /// and what it allocates over five seconds, and what it keeps, the textures, render
        /// textures and meshes it made, beside the game's whole managed memory for scale.
        /// </summary>
        private static IEnumerator IdleCost(Probe p)
        {
            var wasOpen = Session.IsOpen;
            Session.Hide();
            yield return null;
            yield return null;
            var first = Timing.Measuring?.Frames ?? 0;
            var bytes = Timing.ScryBytes;
            var frames = Time.frameCount;
            yield return new Wait(5.0);
            var idle = Timing.Measuring?.Since(first);
            var count = Math.Max(1, Time.frameCount - frames);
            var allocated = Timing.ScryBytes - bytes;
            p.Note(idle != null
                ? $"idling {idle.Frames} frames: Scry's own work {idle.Mean:0.000} ms a frame on average, {idle.Percentile(0.95):0.000} ms at the 95th percentile, {idle.Max:0.000} ms at the most; it allocated about {allocated / 1024} KB, {allocated / count} bytes a frame"
                : "frames were not measured");
            p.Note("it keeps: " + KeptTold());
            if (idle != null) p.Check(idle.Mean < 0.25, "idling, Scry's own work is a quarter of a millisecond a frame at the most on average", $"{idle.Mean:0.000} ms");
            p.Check(allocated / count < 1024, "idling, Scry allocates under a kilobyte a frame", $"{allocated / count} bytes");
            if (wasOpen) Session.Show(null);
        }

        /// <summary>
        /// The resource monitor, switched on: it draws over the screen, measures Scry's frames,
        /// and tells what Scry holds, its catalog among it; its lines are noted.
        /// </summary>
        private static IEnumerator ResourceMonitor(Probe p)
        {
            var was = Plugin.ShowMonitor;
            Plugin.ShowMonitor = true;
            var drawn = ScryPanel.MonitorsDrawn;
            var from = Time.unscaledTime;
            yield return Until(() => Time.unscaledTime - from > 1.5f, 3);
            p.Check(ScryPanel.MonitorsDrawn > drawn, "it draws over the screen", $"{ScryPanel.MonitorsDrawn - drawn} times");
            p.Check(Monitor.Window.Count > 0 && Monitor.Window.Mean > 0, "it measures Scry's frames", $"{Monitor.Window.Count} frames, {Monitor.Window.Mean:0.000} ms on average");
            p.Check(Monitor.Lines.Any(l => l.StartsWith("holds ", StringComparison.Ordinal) && l.Contains($"{X.Catalog.Count} entries")), "it tells what Scry holds", string.Join(" | ", Monitor.Lines));
            p.Note(string.Join(" | ", Monitor.Lines));
            Plugin.ShowMonitor = was;
        }

        /// <summary>The textures, render textures and meshes Scry made, by kind with their memory, and the game's managed and native memory for scale.</summary>
        private static string KeptTold()
        {
            long Size(UnityEngine.Object thing) => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(thing);
            // The panel's own small shapes are left out: a few dozen kilobytes, unnamed.
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>().Where(t => t != null && t.name.StartsWith("Scry", StringComparison.Ordinal)).ToList();
            var renders = Resources.FindObjectsOfTypeAll<RenderTexture>().Where(t => t != null && t.name.StartsWith("Scry", StringComparison.Ordinal)).ToList();
            var meshes = Resources.FindObjectsOfTypeAll<Mesh>().Where(m => m != null && m.name.StartsWith("Scry", StringComparison.Ordinal)).ToList();
            var byName = textures.GroupBy(t => t.name).OrderByDescending(g => g.Sum(Size)).Take(6).Select(g => $"{g.Key} x{g.Count()} {g.Sum(Size) / 1024} KB");
            var mb = 1024.0 * 1024.0;
            return $"{textures.Count} textures, {textures.Sum(Size) / mb:0.0} MB ({string.Join(", ", byName)}); {renders.Count} render textures, {renders.Sum(Size) / mb:0.0} MB; {meshes.Count} meshes, {meshes.Sum(Size) / mb:0.0} MB; "
                   + $"{X.Catalog.Count} entries; the game's managed memory in use {GC.GetTotalMemory(false) / mb:0} MB, its native memory {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / mb:0} MB";
        }

        /// <summary>
        /// The world's lights stay off the stage: a directional light made in the world, as a
        /// lightning strike's flash is, leaves the stage's picture as it was.
        /// </summary>
        private static IEnumerator WorldLightsOff(Probe p)
        {
            var entry = Pick(Kind.Piece, "piece_workbench", "wood_wall_half");
            if (entry == null) p.Skip("there is no piece to stand on the stage");
            var spin = Stage.Spin;
            Stage.Spin = false;
            Select(entry);
            yield return Until(() => CopyOf(entry) != null, 10);
            var settled = Time.unscaledTime;
            yield return Until(() => Time.unscaledTime - settled > 0.6f && !Stage.Gliding, 4);
            var middle = new Rect(0.3f, 0.3f, 0.4f, 0.4f);
            var before = PictureWithin(middle, out _, out _);

            var flash = new GameObject("Scry self-test flash");
            var light = flash.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.red;
            light.intensity = 8f;
            flash.transform.rotation = Quaternion.Euler(30f, 200f, 0f);
            yield return null;
            yield return null;
            var lit = PictureWithin(middle, out _, out _);
            UnityEngine.Object.Destroy(flash);
            Stage.Spin = spin;

            var differ = before != null && lit != null && before.Length == lit.Length ? before.Where((c, i) => Apart(c, lit[i])).Count() : -1;
            var shown = before?.Count(c => c.r + c.g + c.b > 60) ?? 0;
            p.Check(before != null && shown > 0, "the piece shows in the picture", $"{shown} bright points");
            p.Check(differ >= 0 && differ <= (before?.Length ?? 0) / 100, "a directional light of the world, as a lightning flash is, leaves the stage's picture as it was", $"{differ} of {before?.Length ?? 0} points changed");
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
            if (!p.Check(CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.HasFloors, "its example stands on the stage", $"{entry.Name}, {Stage.ExampleRoomsShown} rooms"))
            {
                Stage.Inside = wasInside;
                yield break;
            }
            p.Note($"{entry.Name} ({entry.DisplayName}), {Stage.ExampleRoomsTotal} rooms, {Stage.FloorHeights.Count} floors");

            Stage.OpenLevel(0);
            yield return Until(() => Stage.ExampleRoomsKept, 5);
            yield return Settled();
            p.Check(Stage.LookedFloor is float top && Mathf.Abs(top - Stage.FloorOnStage(0)) < 0.01f && Mathf.Abs(Stage.LookAt.y - top) < 0.05f,
                "with a floor opened the camera looks at that floor", $"looks at {Stage.LookAt.y:0.0} m, the floor at {Stage.FloorOnStage(0):0.0} m");
            var outside = RoomsOutOfPicture();
            p.Check(outside.Count == 0, "and every room on it is in the picture", string.Join(", ", outside.Take(8)));

            if (Stage.FloorHeights.Count > 1)
            {
                Stage.StepCut(true);
                yield return Until(() => Stage.ExampleRoomsKept, 5);
                yield return Settled();
                p.Check(Mathf.Abs(Stage.LookAt.y - Stage.FloorOnStage(Stage.CutLevel)) < 0.05f, "a floor down, it looks at that floor",
                    $"looks at {Stage.LookAt.y:0.0} m, the floor at {Stage.FloorOnStage(Stage.CutLevel):0.0} m");
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
                "the wheel zooms toward what is under the pointer, which stays under it", $"{(seen is Vector2 s ? s.ToString("0.000") : "not in view")} for {at:0.000}; {far:0.0} m to {Stage.LookDistance:0.0} m");

            var from = new Vector2(0.5f, 0.5f);
            var to = new Vector2(0.62f, 0.44f);
            var grabbed = Stage.PointUnder(from);
            Stage.Pan(from, to);
            yield return null;
            yield return null;
            var followed = grabbed is Vector3 held ? Stage.PictureOf(held) : null;
            p.Check(followed is Vector2 now && (now - to).magnitude < 0.02f, "a drag with the right button keeps what was grabbed under the pointer",
                $"{(followed is Vector2 f ? f.ToString("0.000") : "not in view")} for {to:0.000}");

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
                "and the wheel zooms toward what is under the pointer there too", $"{(seen is Vector2 s2 ? s2.ToString("0.000") : "not in view")} for {at:0.000}");

            Stage.ResetView();
            Stage.Inside = wasInside;
        }

        /// <summary>Waits until the stage's camera stops gliding to what it frames.</summary>
        private static IEnumerator Settled()
        {
            var from = Time.unscaledTime;
            yield return Until(() => Time.unscaledTime - from > 0.2f && !Stage.Gliding, 8);
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
                if (!(point is Vector2 at) || at.x < -0.02f || at.x > 1.02f || at.y < -0.02f || at.y > 1.02f) outside.Add($"{rooms[i].Room.Name} at {(point?.ToString("0.00") ?? "behind")}");
            }
            return outside;
        }

        /// <summary>Where in the stage's picture the part of bounds above a height shows, 0 to 1 across and up; null where any of it is behind the camera.</summary>
        private static Rect? AboveIn(Bounds bounds, float above)
        {
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity, maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            foreach (var x in new[] { bounds.min.x, bounds.max.x })
            foreach (var y in new[] { above, bounds.max.y })
            foreach (var z in new[] { bounds.min.z, bounds.max.z })
            {
                if (!(Stage.PictureOf(new Vector3(x, y, z)) is Vector2 at)) return null;
                minX = Mathf.Min(minX, at.x);
                minY = Mathf.Min(minY, at.y);
                maxX = Mathf.Max(maxX, at.x);
                maxY = Mathf.Max(maxY, at.y);
            }
            return Rect.MinMaxRect(Mathf.Clamp01(minX), Mathf.Clamp01(minY), Mathf.Clamp01(maxX), Mathf.Clamp01(maxY));
        }

        /// <summary>The stage's picture within a part of it (0 to 1 across and up), as read back; null with no picture.</summary>
        private static Color32[] PictureWithin(Rect part, out int width, out int height)
        {
            width = height = 0;
            if (!(Stage.Texture is RenderTexture picture)) return null;
            var x = Mathf.Clamp(Mathf.FloorToInt(part.xMin * picture.width), 0, picture.width - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt(part.yMin * picture.height), 0, picture.height - 1);
            width = Mathf.Clamp(Mathf.CeilToInt(part.xMax * picture.width), x + 1, picture.width) - x;
            height = Mathf.Clamp(Mathf.CeilToInt(part.yMax * picture.height), y + 1, picture.height) - y;
            var was = RenderTexture.active;
            var read = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = picture;
                read.ReadPixels(new Rect(x, y, width, height), 0, 0, false);
                return read.GetPixels32();
            }
            finally
            {
                RenderTexture.active = was;
                UnityEngine.Object.Destroy(read);
            }
        }

        /// <summary>Whether two points of a picture differ to the eye.</summary>
        private static bool Apart(Color32 a, Color32 b) => Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) > 24;

        /// <summary>A runestone location tells its stone's texts, in words, under Runestone texts.</summary>
        private static IEnumerator RunestoneTexts(Probe p)
        {
            var stone = X.Catalog.Where(e => e.Kind == Kind.Location && e.Name.StartsWith("Runestone", StringComparison.OrdinalIgnoreCase)).OrderBy(e => e.Name, StringComparer.Ordinal).FirstOrDefault();
            if (stone == null) p.Skip("there is no runestone location");
            var place = PlaceOf(stone);
            Select(stone);
            yield return Until(() => place.Contents != null && PlaceAssets.State(place) != PlaceLoad.Loading, 30);
            var stones = place.Contents?.Runestones;
            if (!p.Check(stones != null && stones.Count > 0, $"{stone.Name} holds a runestone with texts")) yield break;
            var texts = stones.SelectMany(s => s.Texts).ToList();
            p.Note($"{stones.Count} stones, {texts.Count} texts; the first: \"{RuneWords.Title(texts[0], stones[0].Name)}\"");
            p.Check(texts.All(t => t.Text.Length > 0 && t.Text.IndexOf('$') < 0), "every text is in words, not the game's keys");
            ScryPanel.RunesFolded = false;
            var drawn = ScryPanel.RunesDrawn;
            yield return Until(() => ScryPanel.RunesDrawn > drawn, 3);
            p.Check(ScryPanel.RunesDrawn > drawn, "its page shows them under Runestone texts");
        }

        /// <summary>Once every location is read: the runestones they hold, each with its texts in words.</summary>
        private static IEnumerator EveryRunestone(Probe p)
        {
            if (Locations.Now != Locations.State.Read) p.Skip("the locations are not read");
            var holding = X.Catalog.Where(e => PlaceOf(e)?.Contents?.Runestones.Count > 0).ToList();
            var texts = holding.SelectMany(e => PlaceOf(e).Contents.Runestones.SelectMany(s => s.Texts.Select(t => (e.Name, Text: t)))).ToList();
            p.Note($"{holding.Count} locations and rooms hold runestones, with {texts.Select(t => t.Text.Text).Distinct().Count()} texts between them: " + string.Join(", ", holding.Take(12).Select(e => e.Name)));
            p.Check(holding.Count > 0, "some locations hold runestones");
            var raw = texts.Where(t => t.Text.Text.IndexOf('$') >= 0 || t.Text.Topic.IndexOf('$') >= 0).Select(t => t.Name).Distinct().ToList();
            p.Check(raw.Count == 0, "every text and title is in words", string.Join(", ", raw.Take(8)));
            yield break;
        }
    }
}
