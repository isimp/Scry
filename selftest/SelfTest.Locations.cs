using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for locations: one shown and switched, its rooms, its runestone's
    /// texts, its spawn points' creatures and its placement; and once every location is read,
    /// what broke, their music, altars and traders, every runestone, and a spread of locations
    /// and rooms on the stage.
    /// </summary>
    internal static partial class SelfTest
    {
        // ----- Showing a location -----

        private static IEnumerator LocationLoads(Probe p)
        {
            var crypt = Crypt();
            if (crypt == null) p.Skip("there is no location");
            var place = PlaceOf(crypt);
            p.Note($"{crypt.Name} ({crypt.DisplayName}), listed under {crypt.Group}");
            var forest = Knowledge.BiomeName("BlackForest");
            if (place.Biomes == Heightmap.Biome.BlackForest) p.Check(crypt.Group == forest || crypt.Group.StartsWith(forest + " · ", StringComparison.Ordinal), "it is listed under its biome", crypt.Group);

            var asked = Time.unscaledTime;
            Select(crypt);
            yield return null;
            var state = PlaceAssets.State(place);
            p.Check(state == PlaceLoad.Loading || state == PlaceLoad.Ready, "its model is loading", state.ToString());
            yield return Until(() => PlaceAssets.State(place) != PlaceLoad.Loading, 30);
            p.Check(PlaceAssets.State(place) == PlaceLoad.Ready, "its model has loaded", PlaceAssets.State(place).ToString());
            yield return Until(() => CopyOf(crypt) != null, 5);
            p.Note($"loaded and shown in {Time.unscaledTime - asked:0.0} s");
            p.Check(CopyOf(crypt) != null, "a copy stands on the stage");

            // A location keeps far-off parts of its own (its dungeon is built high above it), which
            // must not be what the stage frames: framing both would measure too far to frame, and
            // leave the 2 m box of something with nothing to measure.
            var size = Stage.SubjectSize;
            p.Note($"the stage shows it {size.x:0} × {size.z:0} m, {size.y:0} m high");
            p.Check(Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 300f, "the stage frames the place itself", $"{size.x:0} × {size.z:0} × {size.y:0} m");
            p.Check(Mathf.Max(size.x, size.z) > 4f, "and takes in its entrance", $"{size.x:0.0} × {size.z:0.0} m");
            p.Check(Mathf.Abs(Stage.Ground) < 0.01f, "it stands on its own ground", $"{Stage.Ground:0.00} m");

            var contents = place.Contents;
            if (!p.Check(contents != null, "what it holds has been read")) yield break;
            p.Check(contents.GameName.Length > 0 || contents.Boss.Length > 0 || contents.Trader.Length > 0, "the game has a name for it", crypt.DisplayName);
            if (contents.GameName.Length > 0) p.Check(crypt.DisplayName == contents.GameName, "and it goes by it", crypt.DisplayName);
            p.Note($"{contents.Parts.Sum(x => x.Count)} parts of {contents.Parts.Count} kinds, {contents.Creatures.Count} kinds of creature, name \"{crypt.DisplayName}\"");
            var told = Facts.For(crypt);
            p.Check(Tells(told, "Per world"), "its details tell where the world places it");
            p.Check(PlaceParts.Roles.Any(r => Tells(told, PlaceParts.Title(r, false))) || Tells(told, "Its spawn points place"), "and what it holds");

            if (!contents.LeftToChance)
            {
                p.Note("nothing in it is left to chance, so there is nothing to roll again");
                yield break;
            }
            var before = Stage.Subject;
            Previews.Rebuild();
            // Made over a few frames, as every location is.
            yield return Until(() => Stage.Subject != null && Stage.Subject != before, 10);
            p.Check(Stage.Subject != null && Stage.Subject != before, "Roll again makes a new copy");
        }

        private static Entry Crypt() => LocationNamed("Crypt2", "Crypt3", "Crypt4");

        private static Entry Camp() => LocationNamed("GoblinCamp2", "GoblinCamp2_1");

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

        private static IEnumerator Switching(Probe p)
        {
            var crypt = Crypt();
            var camp = Camp();
            var room = X.Catalog.FirstOrDefault(e => PlaceOf(e)?.IsRoom == true);
            var raid = Pick(Kind.Raid, "army_eikthyr");
            if (crypt == null || camp == null || room == null || raid == null) p.Skip("there is no crypt, camp, room or raid to switch between");

            // A frame each, so each is asked for before the one before has loaded.
            Select(crypt);
            yield return null;
            Select(camp);
            yield return null;
            Select(room);
            yield return null;
            p.Check(PlaceAssets.Held == PlaceOf(room), "only the last one asked for is held", PlaceAssets.Held?.Prefab ?? "none");
            Select(raid);
            yield return null;
            p.Check(PlaceAssets.Held == null, "selecting a raid lets go of it", PlaceAssets.Held?.Prefab ?? "none");

            Select(crypt);
            yield return null;
            Session.Hide();
            yield return null;
            p.Check(PlaceAssets.Held == null, "closing the panel while it loads lets go of it", PlaceAssets.Held?.Prefab ?? "none");
            Session.Show(null);
            yield return Until(() => CopyOf(crypt) != null, 30);
            p.Check(CopyOf(crypt) != null, "opened again, it loads and shows");
            p.Check(PlaceAssets.Held == PlaceOf(crypt), "and is the one held", PlaceAssets.Held?.Prefab ?? "none");
        }

        private static IEnumerator RoomShows(Probe p)
        {
            var room = X.Catalog.FirstOrDefault(e => PlaceOf(e) is PlaceSource place && place.IsRoom && place.Prefab.StartsWith("forestcrypt", StringComparison.OrdinalIgnoreCase))
                       ?? X.Catalog.FirstOrDefault(e => PlaceOf(e)?.IsRoom == true);
            if (room == null) p.Skip("there is no dungeon room");
            var place = PlaceOf(room);
            p.Note($"{room.Name}, listed under {room.Group}");
            Select(room);
            yield return Until(() => PlaceAssets.State(place) != PlaceLoad.Loading && CopyOf(room) != null, 20);
            p.Check(PlaceAssets.State(place) == PlaceLoad.Ready, "its model has loaded", PlaceAssets.State(place).ToString());
            p.Check(CopyOf(room) != null, "a copy stands on the stage");
            var shape = place.Contents?.Room;
            if (!p.Check(shape != null, "its shape has been read")) yield break;
            p.Check(shape.Size.X > 0 && shape.Size.Z > 0 || shape.EndCap || shape.Divider, "it has a floor", DungeonWords.Size(shape));
            p.Check(shape.Doorways.Count > 0, "it has doorways", DungeonWords.Doorways(shape));
            p.Check(shape.Name == place.Prefab, "its prefab goes by the name its entry has", $"{shape.Name} and {place.Prefab}");
            p.Note($"{DungeonWords.Role(shape)}, {DungeonWords.Size(shape)}, doorways {DungeonWords.Doorways(shape)}");
            var floor = PlaceView.Ground(place.Contents, true);
            p.Check(Mathf.Abs(Stage.Ground - floor) < 0.01f, "it stands on the floor it is walked into on", $"{Stage.Ground:0.00} m, its lowest doorway {floor:0.00} m");

            // It opens on its top floor to be looked into; the arrows step down to the lowest and up to the roof, and the chip takes the roof off again.
            var floors = Stage.FloorHeights.ToList();
            p.Note($"floors found in it at {string.Join(", ", floors.Select(f => f.ToString("0.0")))} m; its doorways at {string.Join(", ", PlaceView.RoomFloors(shape).Select(f => f.ToString("0.0")))} m");
            p.Check(Stage.HasFloors && Stage.Cutting, "it is cut open", Stage.CutLabel);
            p.Check(floors.Count > 0 && Mathf.Abs(Stage.CutAt - Stage.CutHeights[0]) < 0.01f && Stage.CutAt > floors[0], "over its top floor", $"cut at {Stage.CutAt:0.0} m");
            var labels = new List<string>();
            for (var i = 0; i <= floors.Count; i++)
            {
                Stage.StepCut(true);
                labels.Add(Stage.CutLabel);
            }
            p.Check(Stage.CutLevel == floors.Count - 1, "a floor down at a time, staying on the lowest", string.Join("; ", labels));
            for (var i = 0; i <= floors.Count; i++) Stage.StepCut(false);
            p.Check(!Stage.Cutting, "a floor up at a time, to the roof on", Stage.CutLabel);
            Stage.ToggleRoof();
            p.Check(Stage.Cutting && Stage.CutLevel == 0, "and the chip takes the roof off its top floor again", Stage.CutLabel);
            yield return null;
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

        private static IEnumerator PlacementDetails(Probe p)
        {
            var temple = LocationNamed("StartTemple");
            if (temple != null)
            {
                var told = Facts.For(temple);
                p.Check(Tells(told, "Woods"), "the start temple tells the woods it keeps out of", Pairs(told));
            }
            var lava = X.Catalog.FirstOrDefault(e => PlaceOf(e) is PlaceSource place && !place.IsRoom && place.Rules.Count > 0
                && place.Rules.All(r => r.m_biome == Heightmap.Biome.AshLands) && place.Rules.Any(r => r.m_maximumVegetation < 1f || r.m_minimumVegetation > 0f));
            if (lava != null)
            {
                p.Note($"{lava.Name} ({lava.DisplayName})");
                var told = Facts.For(lava);
                p.Check(Tells(told, "Lava"), "an Ashlands location tells the lava it keeps to", Pairs(told));
            }
            if (temple == null && lava == null) p.Skip("neither the start temple nor an Ashlands location with a lava rule is in this world's list");
            yield break;
        }

        // ----- Once every location is read -----

        private static IEnumerator ReadLocations(Probe p)
        {
            if (Locations.Now != Locations.State.NotRead) p.Skip($"the locations are {(Locations.Now == Locations.State.Read ? "already read" : "being read")} in this world");

            // A location stands on the stage throughout, from a bundle the reading loads and lets go of too.
            var shown = Camp() ?? Crypt();
            GameObject copy = null;
            var brokenBefore = 0;
            if (shown != null)
            {
                // Counted once its example's rooms stand too, as they are built after the copy.
                Select(shown);
                yield return Until(() => CopyOf(shown) != null && ExampleLayouts.Of(shown) && ExampleLayouts.Example != null
                                         && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 60);
                copy = CopyOf(shown);
                brokenBefore = Broken(copy);
                if (brokenBefore > 0) p.Note($"before reading, {shown.Name} stands with {brokenBefore} missing: {string.Join(", ", BrokenParts(copy).Take(8))}; {OwnMissing(copy)}");
            }

            var asked = Time.unscaledTime;
            p.Note(Locations.Start());
            yield return Until(() => Locations.Now == Locations.State.Read, 400);
            if (!p.Check(Locations.Now == Locations.State.Read, "every location and room has been read", $"{Locations.Done} of {Locations.Total}")) yield break;
            p.Note($"read in {Time.unscaledTime - asked:0} s");

            if (copy != null)
            {
                p.Check(Stage.Subject == copy, $"{shown.Name} stayed on the stage meanwhile");
                p.Check(copy != null && Broken(copy) == brokenBefore, "and kept every mesh and material", $"{Broken(copy)} missing, {brokenBefore} before: {string.Join(", ", BrokenParts(copy).Take(8))}");
            }

            var places = X.Catalog.Where(e => PlaceOf(e) != null).ToList();
            var read = places.Count(e => PlaceOf(e).Contents != null);
            p.Check(read >= places.Count * 0.9, "nearly every location and room knows what it holds", $"{read} of {places.Count}");
            p.Check(Locations.Summons.Count > 0, "the altars' bosses are known", $"{Locations.Summons.Count}");
            p.Check(X.Catalog.Any(e => e.Kind == Kind.Item && e.FoundIn.Length > 0), "items tell where they are found");

            // Each place an item is found in goes to its location.
            var found = X.Catalog.Where(e => e.Kind == Kind.Item && e.FoundIn.Length > 0).SelectMany(e => e.FoundIn).Distinct().ToList();
            var named = found.Count(place => Places.LocationNamed(X.Catalog, place) != null);
            p.Note($"{named} of the {found.Count} places items are found in go to a location");
            p.Check(named >= found.Count * 0.9, "nearly every place items are found in goes to its location", $"{named} of {found.Count}");

            // Every place goes by its name, and rooms by the dungeons built with them.
            var crypt = Crypt();
            var contents = PlaceOf(crypt)?.Contents;
            if (crypt != null && contents != null && contents.GameName.Length > 0) p.Check(crypt.DisplayName == contents.GameName, "the crypt goes by the game's name", crypt.DisplayName);
            var rooms = places.Where(e => PlaceOf(e).IsRoom).ToList();

            // Each dungeon is one group in its biome: its location first, tagged, its rooms indented under it.
            if (crypt != null)
            {
                var group = X.Catalog.Where(e => e.Kind == Kind.Location && e.Group == crypt.Group).ToList();
                p.Note($"{crypt.Name} is listed in \"{crypt.Group}\" with {group.Count(e => e.Indent)} rooms, tagged \"{crypt.Tag}\"");
                p.Check(crypt.Group.Contains(" \u00b7 ") && (crypt.Tag ?? "").StartsWith("dungeon", StringComparison.Ordinal), "the crypt heads a group of its own, tagged a dungeon", crypt.Group);
                p.Check(group.Count(e => e.Indent) > 0 && group.Where(e => e.Indent).All(e => e.GroupRank > 0) && group.Where(e => !e.Indent).All(e => e.GroupRank == 0), "its rooms are indented under it and rank after it");
                var entrance = group.FirstOrDefault(e => PlaceOf(e)?.Contents?.Room?.Entrance == true);
                p.Check(entrance != null && entrance.GroupRank == 1 && (entrance.Tag ?? "").StartsWith("entrance room", StringComparison.Ordinal), "its entrance comes first among its rooms, tagged an entrance room", entrance?.Name ?? "none");
            }
            var waiting = rooms.Where(e => !e.Indent).ToList();
            p.Note($"{rooms.Count - waiting.Count} of {rooms.Count} rooms are under their dungeon; {waiting.Count} wait in theme groups: {string.Join(", ", waiting.Select(e => e.Group).Distinct().Take(6))}");
            var homes = rooms.Where(e => e.Indent).GroupBy(e => e.Name).Count(g => g.Select(e => e.Group).Distinct().Count() > 1);
            p.Check(homes == 0, "each room has one home");
            var placed = rooms.Count(e => e.FoundIn.Length > 0 && e.FoundIn[0] != Places.AnyDungeon);
            p.Note($"{placed} of {rooms.Count} rooms are listed under the dungeons built with them");
            p.Check(placed >= rooms.Count / 2, "most rooms are listed under their dungeons", $"{placed} of {rooms.Count}");

            if (crypt != null)
            {
                var term = "in:" + crypt.DisplayName.Replace(" ", "").ToLowerInvariant();
                X.SearchEverything("kind:location " + term);
                p.Check(X.Results.Any(e => PlaceOf(e)?.IsRoom == true), $"\"kind:location {term}\" finds its rooms", $"{X.Results.Count} results");
                X.SearchEverything(crypt.DisplayName);
                p.Check(X.Results.Contains(crypt), $"\"{crypt.DisplayName}\" finds it by name");
                X.SearchEverything("");
            }
        }

        private static int Broken(GameObject copy)
        {
            if (copy == null) return 0;
            var missing = copy.GetComponentsInChildren<MeshFilter>(true).Count(m => m.sharedMesh == null);
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true)) missing += renderer.sharedMaterials.Count(m => m == null);
            return missing;
        }

        /// <summary>How many meshes and materials a copy is missing, which a bundle unloaded under it would take.</summary>
        /// <summary>Where a copy's missing meshes and materials are, from its root, and whether the part is switched on.</summary>
        private static List<string> BrokenParts(GameObject copy)
        {
            var parts = new List<string>();
            if (copy == null) return parts;
            string Path(Transform t)
            {
                var path = t.name;
                for (var up = t.parent; up != null && up != copy.transform; up = up.parent) path = up.name + "/" + path;
                return path + (t.gameObject.activeInHierarchy ? "" : " (off)");
            }
            foreach (var filter in copy.GetComponentsInChildren<MeshFilter>(true)) if (filter.sharedMesh == null) parts.Add(Path(filter.transform) + " mesh");
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                var none = renderer.sharedMaterials.Count(m => m == null);
                if (none > 0) parts.Add(Path(renderer.transform) + $" {none} material{(none > 1 ? "s" : "")}");
            }
            return parts;
        }

        /// <summary>How many meshes and materials the room prefabs of an example's copies on the stage lack themselves, for those with any missing.</summary>
        private static string OwnMissing(GameObject copy)
        {
            var told = new List<string>();
            foreach (var room in BrokenParts(copy).Select(part => part.Split('/')).Where(path => path.Length > 2 && path[0] == "Scry example").Select(path => path[1]).Distinct())
            {
                var entry = X.Catalog.FirstOrDefault(e => PlaceOf(e) is PlaceSource place && place.IsRoom && place.Prefab == room);
                var asset = entry != null && PlaceOf(entry).Reference.IsLoaded ? PlaceOf(entry).Reference.Asset : null;
                told.Add(asset != null ? $"{room}'s own prefab lacks {Broken(asset)}" : $"{room}'s own prefab is not loaded");
            }
            return told.Count > 0 ? string.Join("; ", told) : "none in the example's rooms";
        }

        /// <summary>
        /// A location with music of its own plays it through the path Enter takes, mutes the
        /// game's music meanwhile, and gives it back when something else is selected; a place
        /// whose music is named for stepping inside plays it from the game's music list.
        /// </summary>
        private static IEnumerator PlaysMusic(Probe p)
        {
            var withMusic = X.Catalog.Where(e => PlaceOf(e)?.Contents?.Music.Count > 0).ToList();
            p.Note($"{withMusic.Count} locations and rooms play music: " + string.Join(", ", withMusic.Take(8).Select(e => e.Name)));
            if (withMusic.Count == 0) p.Skip("no location read plays music");
            var near = withMusic.FirstOrDefault(e => PlaceOf(e).Contents.Music[0].When == MusicWhen.Near) ?? withMusic[0];

            foreach (var entry in new[] { near, withMusic.FirstOrDefault(e => PlaceOf(e).Contents.Music[0].When != MusicWhen.Near) })
            {
                if (entry == null) continue;
                var place = PlaceOf(entry);
                Select(entry);
                yield return Until(() => PlaceAssets.State(place) != PlaceLoad.Loading, 20);
                var said = Previews.PlacesMusic(entry);
                p.Note($"{entry.Name}: {LocationWords.Music(place.Contents.Music)}; Enter said \"{said}\"");
                yield return null;
                p.Check(MusicPreview.PlayingFor == entry && MusicPreview.Sounding, $"{entry.Name} plays {place.Contents.Music[0].Name}", said);
                var game = MusicMan.instance != null ? MusicMan.instance.GetComponentsInChildren<AudioSource>(true) : new AudioSource[0];
                p.Check(game.All(s => s.mute), "the game's music is muted meanwhile", $"{game.Length} sources");

                Select(Pick(Kind.Creature, "Boar", "Greyling"));
                yield return Until(() => MusicPreview.PlayingFor == null, 2);
                p.Check(!MusicPreview.Sounding && MusicPreview.PlayingFor == null, "selecting something else stops it");
                p.Check(game.All(s => s == null || !s.mute), "and gives the game's music back");
            }
        }

        /// <summary>Every altar's boss tells where it is summoned and with what; every trader tells what it sells.</summary>
        private static IEnumerator AltarsAndTraders(Probe p)
        {
            if (Locations.Now != Locations.State.Read) p.Skip("the locations are not read");
            var bosses = Knowledge.Summons().Select(s => s.Boss).Where(b => b != null).Distinct().ToList();
            var wrong = new List<string>();
            foreach (var boss in bosses)
            {
                var entry = X.Catalog.FirstOrDefault(e => e.Name == boss && e.Kind == Kind.Creature);
                if (entry == null) continue;
                if (!Facts.For(entry).Rows.Any(r => r.Title.StartsWith("Summoned at", StringComparison.Ordinal))) wrong.Add($"{boss} tells no altar");
            }
            p.Note($"{bosses.Count} bosses summoned at altars");
            p.Check(bosses.Count > 0 && wrong.Count == 0, "every boss tells where it is summoned and with what", string.Join("; ", wrong));
            var traders = X.Catalog.Where(e => e.Source is GameObject g && g.GetComponent<Trader>() != null).ToList();
            var quiet = traders.Where(t => !Facts.For(t).Rows.Any(r => r.Title.StartsWith("Sells", StringComparison.Ordinal))).Select(t => t.Name).ToList();
            p.Note($"{traders.Count} traders: {string.Join(", ", traders.Select(t => t.Name))}");
            p.Check(quiet.Count == 0, "every trader tells what it sells", string.Join(", ", quiet));
            yield break;
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

        /// <summary>Every location and room that names music of its own plays it.</summary>
        private static IEnumerator EveryMusic(Probe p)
        {
            if (Locations.Now != Locations.State.Read) p.Skip("the locations are not read");
            var withMusic = X.Catalog.Where(e => PlaceOf(e)?.Contents?.Music.Count > 0).ToList();
            if (withMusic.Count == 0) p.Skip("no location or room plays music");
            var silent = new List<string>();
            foreach (var entry in withMusic)
            {
                var place = PlaceOf(entry);
                Select(entry);
                yield return Until(() => PlaceAssets.State(place) != PlaceLoad.Loading, 15);
                var said = Previews.PlacesMusic(entry);
                yield return null;
                if (MusicPreview.PlayingFor != entry || !MusicPreview.Sounding) silent.Add($"{entry.Name} ({place.Contents.Music[0].Name}: {said})");
                Previews.StopSound();
            }
            p.Note($"{withMusic.Count} play music: " + string.Join(", ", withMusic.Take(12).Select(e => e.Name)));
            p.Check(silent.Count == 0, "each plays its music", string.Join(", ", silent));
        }

        /// <summary>A spread of locations stands on the stage, framed to a sane size on its own ground.</summary>
        private static IEnumerator LocationsOnStage(Probe p)
        {
            var all = X.Catalog.Where(e => PlaceOf(e) != null && !PlaceOf(e).IsRoom).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            var wrong = new List<string>();
            var sizes = new List<string>();
            foreach (var entry in Spread(all, 10))
            {
                var place = PlaceOf(entry);
                Select(entry);
                yield return Until(() => PlaceAssets.State(place) != PlaceLoad.Loading && CopyOf(entry) != null, 20);
                if (PlaceAssets.State(place) == PlaceLoad.Failed)
                {
                    wrong.Add($"{entry.Name} could not be loaded");
                    continue;
                }
                if (CopyOf(entry) == null)
                {
                    wrong.Add($"{entry.Name} stands nowhere");
                    continue;
                }
                yield return null;
                var size = Stage.SubjectSize;
                sizes.Add($"{entry.Name} {Mathf.Max(size.x, size.z):0} m");
                if (Mathf.Max(size.x, size.y, size.z) > 300f) wrong.Add($"{entry.Name} frames {size}");
                // A location stands on its own ground; a dungeon's example, shown inside, on its lowest floor.
                var inside = Stage.HasInside && Stage.Inside && Stage.FloorHeights.Count > 0;
                var ground = inside ? Stage.FloorHeights[Stage.FloorHeights.Count - 1] : 0f;
                if (float.IsNaN(Stage.Ground) || Mathf.Abs(Stage.Ground - ground) > 0.01f) wrong.Add($"{entry.Name} stands at {Stage.Ground:0.0} m{(inside ? $", its example's lowest floor at {ground:0.0} m" : "")}");
            }
            p.Note(string.Join(", ", sizes));
            p.Check(wrong.Count == 0, "each loads, is framed to a sane size and stands on its own ground", string.Join("; ", wrong));
        }

        /// <summary>
        /// The locations with the most parts are made over several frames, none of them long:
        /// what each took, and its slowest frame with the part that took longest in it.
        /// </summary>
        private static IEnumerator LargestLocations(Probe p)
        {
            int PartsOf(Entry e) => PlaceOf(e)?.Contents?.Parts.Sum(part => part.Count) ?? 0;
            var largest = X.Catalog.Where(e => PlaceOf(e) != null && !PlaceOf(e).IsRoom && PartsOf(e) > 0).OrderByDescending(PartsOf).Take(3).ToList();
            if (largest.Count == 0) p.Skip("no location's parts are read");

            var wrong = new List<string>();
            foreach (var entry in largest)
            {
                var place = PlaceOf(entry);
                var from = Frames.Frames;
                Select(entry);
                // The whole frame too: what Unity does of the copy on its own is no work of Scry's.
                var whole = 0f;
                var until = Time.unscaledTime + 30f;
                while ((PlaceAssets.State(place) == PlaceLoad.Loading || CopyOf(entry) == null) && Time.unscaledTime < until)
                {
                    yield return null;
                    whole = Mathf.Max(whole, Time.unscaledDeltaTime * 1000f);
                }
                if (CopyOf(entry) == null)
                {
                    wrong.Add($"{entry.Name} stands nowhere");
                    continue;
                }
                var frames = Frames.Since(from);
                p.Note($"{entry.Name}, {PartsOf(entry)} pieces, {Stage.LastBuildParts} parts, made over {Stage.LastBuildFrames} frames, the slowest whole frame {whole:0} ms: {frames.Line(Budget)}{Slowest(frames, 1)}");
                if (frames.Max >= 250) wrong.Add($"{entry.Name} took {frames.Max:0} ms in a frame");
            }
            p.Check(wrong.Count == 0, "each is made over several frames, none of them a quarter of a second", string.Join("; ", wrong));
        }

        /// <summary>A spread of rooms stands on the stage on its lowest doorway, opened on its top floor.</summary>
        private static IEnumerator RoomsOnStage(Probe p)
        {
            var rooms = X.Catalog.Where(e => PlaceOf(e)?.IsRoom == true && PlaceOf(e).Contents?.Room != null && !PlaceOf(e).Contents.Room.EndCap).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            if (rooms.Count == 0) p.Skip("no room is read");
            var wrong = new List<string>();
            foreach (var entry in Spread(rooms, 8))
            {
                var place = PlaceOf(entry);
                Select(entry);
                yield return Until(() => PlaceAssets.State(place) != PlaceLoad.Loading && CopyOf(entry) != null, 15);
                if (CopyOf(entry) == null)
                {
                    wrong.Add($"{entry.Name} stands nowhere");
                    continue;
                }
                var floor = PlaceView.Ground(place.Contents, true);
                if (Mathf.Abs(Stage.Ground - floor) > 0.01f) wrong.Add($"{entry.Name} stands at {Stage.Ground:0.0}, its floor {floor:0.0}");
                if (!Stage.Cutting) wrong.Add($"{entry.Name} is not opened");
            }
            p.Check(wrong.Count == 0, "each stands on its floor, opened to look into", string.Join("; ", wrong));
        }

        private static IEnumerator ClosingLetsGo(Probe p)
        {
            var wasOpen = Session.IsOpen;
            Session.Hide();
            yield return null;
            p.Check(PlaceAssets.Held == null, "no location's bundle is held", PlaceAssets.Held?.Prefab ?? "none");
            p.Check(!ExampleLayouts.Holding, "no dungeon room's bundle is held");
            if (wasOpen) Session.Show(null);
        }
    }
}
