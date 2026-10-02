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
        /// there is none, and some room always stands on the stage.
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
            foreach (var entry in caves)
            {
                Select(entry);
                yield return Until(() => CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 60);
                if (CopyOf(entry) == null || Stage.ExampleRoomsTotal == 0)
                {
                    p.Note($"{entry.Name}: no example stood on the stage");
                    continue;
                }
                var told = new List<string>();
                for (var level = 0; level < Stage.FloorHeights.Count; level++)
                {
                    Stage.OpenLevel(level);
                    yield return Until(() => Stage.ExampleRoomsKept, 5);
                    var rooms = Stage.ExampleRoomsOnFloor;
                    told.Add($"{Stage.FloorHeights[level]:0.0} m, {rooms} rooms, {Stage.ExampleRoomsShown - Stage.ExampleRoomsAway} standing");
                    if (rooms == 0) empty.Add($"{entry.Name} at {Stage.FloorHeights[level]:0.0} m");
                    if (Stage.ExampleRoomsAway >= Stage.ExampleRoomsShown) blank.Add($"{entry.Name} at {Stage.FloorHeights[level]:0.0} m");
                }
                p.Note($"{entry.Name} ({entry.DisplayName}), {Stage.ExampleRoomsTotal} rooms, {Stage.FloorHeights.Count} floors: " + string.Join("; ", told));
            }
            Stage.Inside = wasInside;
            p.Check(empty.Count == 0, "every floor found in their examples has rooms on it", string.Join("; ", empty));
            p.Check(blank.Count == 0, "and some room stands on the stage on every floor", string.Join("; ", blank));
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
