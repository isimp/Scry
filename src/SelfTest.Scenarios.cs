using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's scenarios, in the order they run: the check and the catalog, each kind shown and told, the previews played, raids, locations and their layouts, and last the reading of every location.</summary>
    internal static partial class SelfTest
    {
        private static IEnumerable<Scenario> Scenarios()
        {
            yield return S("the startup check found every part it relies on", StartupCheck, 5);
            yield return S("the catalog holds every kind", Catalog, 5);
            yield return S("the search finds by name, kind and biome", Searching, 5);
            yield return S("the panel opens and draws", PanelDraws, 10);

            yield return S("a creature shows on the stage and tells its facts", p => Shows(p, Pick(Kind.Creature, "Troll", "Greydwarf"), true), 20);
            yield return S("an item shows on the stage and tells its facts", p => Shows(p, Pick(Kind.Item, "SwordIron", "AxeBronze"), true), 20);
            yield return S("a piece shows on the stage and tells its facts", p => Shows(p, Pick(Kind.Piece, "piece_workbench"), true), 20);
            yield return S("a resource shows on the stage and tells its facts", p => Shows(p, Pick(Kind.Resource, "Beech1", "Birch1", "Pine"), true), 20);
            yield return S("a projectile shows on the stage and tells its facts", p => Shows(p, Pick(Kind.Projectile, "bow_projectile", "bow_projectile_fire"), true), 20);
            yield return S("an effect shows on the stage", p => Shows(p, Pick(Kind.Effect, "vfx_HitSparks", "vfx_Place_workbench"), false), 20);
            yield return S("a status effect shows on the person and tells its facts", p => Shows(p, StatusOnPerson(), true), 20);

            yield return S("creatures tell their weak spots, when they turn on you and how far they chase", CreatureFacts, 60);
            yield return S("a piece tells its support and what wears it", PieceFacts, 10);
            yield return S("a spawner tells its pool and pace, and its creatures their share", SpawnerFacts, 10);
            yield return S("items tell their odds in the tables that give them", LootOdds, 10);

            yield return S("a creature plays an attack", CreatureAttacks, 30);
            yield return S("a sound plays and stops", SoundPlays, 10);
            yield return S("an effect plays on you and stops", EffectOnYou, 10);
            yield return S("a status effect shows on you and comes off", StatusOnYou, 10);
            yield return S("a model stands in the world and goes again", ModelInWorld, 10);
            yield return S("a projectile flies where you look", ProjectileFlies, 15);

            yield return S("a raid tells what it brings, and its creatures lead back to it", RaidLinks, 10);
            yield return S("a location loads, shows what it holds and rolls again", LocationLoads, 45);
            yield return S("switching between locations while they load holds only the one shown", Switching, 60);
            yield return S("a dungeon room shows its shape", RoomShows, 30);
            yield return S("a dungeon lays out an example, drawn, its rooms going to their entries", DungeonExample, 200, bearsSkips: true);
            yield return S("a camp lays out an example, drawn", CampExample, 150, bearsSkips: true);
            yield return S("placement details tell the woods and lava a location keeps to", PlacementDetails, 20);
            yield return S("reading every location fills their details", ReadLocations, 450, bearsSkips: true);
            yield return S("closing the panel lets go of every bundle", ClosingLetsGo, 5);

            yield return new Scenario("Scry's own work over the whole run", WholeRun) { Timeout = 5 };
        }

        // ----- The check, the catalog, the search, the panel -----

        private static IEnumerator StartupCheck(Probe p)
        {
            p.Check(Compatibility.Checked, "the startup check has run");
            var off = Compatibility.FeaturesOff();
            p.Check(off.Count == 0, "no feature is off", string.Join("; ", off));
            yield break;
        }

        private static IEnumerator Catalog(Probe p)
        {
            var counts = X.Catalog.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count());
            p.Note(string.Join(", ", counts.OrderBy(c => c.Key).Select(c => $"{c.Value} {Kinds.Label(c.Key).ToLowerInvariant()}")));
            foreach (Kind kind in Enum.GetValues(typeof(Kind)))
            {
                if (kind == Kind.Other) continue;
                p.Check(counts.TryGetValue(kind, out var n) && n > 0, $"there are {Kinds.Label(kind).ToLowerInvariant()}");
            }
            var rooms = X.Catalog.Count(e => e.Source is PlaceSource place && place.IsRoom);
            p.Check(rooms > 0, "the locations include dungeon rooms", $"{rooms} rooms");
            p.Note($"{rooms} of the locations are dungeon rooms");

            // What the game has of its own, as the origin switch tells it.
            foreach (var kind in new[] { Kind.Location, Kind.Raid })
            {
                var of = X.Catalog.Where(e => e.Kind == kind).ToList();
                p.Note($"{Kinds.Label(kind).ToLowerInvariant()}: {of.Count(e => e.Origin == Origin.Vanilla)} the game's, {of.Count(e => e.Origin == Origin.Mod)} mods', {of.Count(e => e.Origin == Origin.Unknown)} not told");
            }
            var temple = LocationNamed("StartTemple");
            if (temple != null) p.Check(temple.Origin == Origin.Vanilla, "the start temple counts as the game's own", temple.Origin.ToString());
            var eikthyr = Pick(Kind.Raid, "army_eikthyr");
            if (eikthyr != null) p.Check(eikthyr.Origin == Origin.Vanilla, "Eikthyr's raid counts as the game's own", eikthyr.Origin.ToString());
            yield break;
        }

        private static IEnumerator Searching(Probe p)
        {
            X.SearchEverything("troll");
            p.Check(X.Results.Any(e => e.Kind == Kind.Creature && e.Name == "Troll"), "\"troll\" finds the troll");

            X.SearchEverything("kind:location");
            p.Check(X.Results.Count > 0 && X.Results.All(e => e.Kind == Kind.Location), "\"kind:location\" finds locations only", $"{X.Results.Count} results");

            X.SearchEverything("kind:location biome:swamp");
            p.Check(X.Results.Count > 0 && X.Results.All(e => e.Kind == Kind.Location && e.Biomes.Contains("Swamp")), "\"kind:location biome:swamp\" finds the swamp's locations", $"{X.Results.Count} results");

            X.SearchEverything("kind:raid");
            p.Check(X.Results.Count > 0 && X.Results.All(e => e.Kind == Kind.Raid), "\"kind:raid\" finds raids only", $"{X.Results.Count} results");
            X.SearchEverything("");
            yield break;
        }

        private static IEnumerator PanelDraws(Probe p)
        {
            Session.Show(null);
            var from = ScryPanel.Repaints;
            yield return Until(() => ScryPanel.Repaints >= from + 5, 5);
            p.Check(Session.IsOpen, "the panel is open");
            p.Check(ScryPanel.Repaints >= from + 5, "it draws, frame after frame", $"{ScryPanel.Repaints - from} repaints");
        }

        // ----- Each kind on the stage -----

        private static Entry StatusOnPerson() =>
            X.Catalog.FirstOrDefault(e => e.Kind == Kind.StatusEffect && e.Name == "Burning" && Looks.ShowsOnPerson(e))
            ?? X.Catalog.FirstOrDefault(e => e.Kind == Kind.StatusEffect && Looks.ShowsOnPerson(e));

        private static IEnumerator Shows(Probe p, Entry entry, bool facts)
        {
            if (entry == null) p.Skip("there is no entry of that kind to show");
            p.Note($"{entry.Name} ({entry.DisplayName})");
            Select(entry);
            yield return Until(() => CopyOf(entry) != null, 10);

            var copy = CopyOf(entry);
            p.Check(copy != null, "a copy stands on the stage");
            if (entry.Kind != Kind.Effect) p.Check(Renderers(copy) > 0, "it has something to draw", $"{Renderers(copy)} renderers");
            if (ScryPanel.Compact) p.Note("the panel is in its compact view, which has no stage to draw");
            else
            {
                yield return Until(() => Stage.Texture != null, 3);
                p.Check(Stage.Texture != null, "the stage is drawn");
            }

            if (!facts) yield break;
            var told = Facts.For(entry);
            p.Check(!told.IsEmpty, "its details tell something");
            p.Note($"{told.Pairs.Count} facts, {told.Rows.Count} rows: {Pairs(told).Substring(0, Math.Min(160, Pairs(told).Length))}");
        }

        // ----- What the details tell -----

        private static IEnumerator CreatureFacts(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll");
            if (troll == null) p.Skip("there is no troll");
            var told = Facts.For(troll);
            var head = Value(told, "Hit on the head");
            if (p.Check(head != null, "the troll tells its head as a weak spot", Pairs(told))) p.Check(head.Contains("pierce"), "a hit there is told by its own resistances", head);

            // Counted over the game's own creatures, a few a frame, as each creature's details take a while.
            var creatures = X.Catalog.Where(e => e.Kind == Kind.Creature && e.Origin == Origin.Vanilla && e.Source is GameObject).ToList();
            int alert = 0, chase = 0, weak = 0, n = 0;
            foreach (var creature in creatures)
            {
                var facts = Facts.For(creature);
                if (Tells(facts, "Turns on you")) alert++;
                if (Tells(facts, "Gives up chasing")) chase++;
                if (facts.Pairs.Any(pair => pair.Key.StartsWith("Hit on the ", StringComparison.Ordinal))) weak++;
                if (++n % 3 == 0) yield return null;
            }
            p.Note($"of {creatures.Count} of the game's creatures, {alert} tell when they turn on you, {chase} how far they chase, {weak} a weak spot");
            p.Check(alert + chase > 0, "some tell when they turn on you or how far they chase");
            p.Check(weak > 0, "some tell a weak spot");
        }

        private static IEnumerator PieceFacts(Probe p)
        {
            var bench = Pick(Kind.Piece, "piece_workbench");
            if (bench == null) p.Skip("there is no workbench");
            var told = Facts.For(bench);
            foreach (var row in new[] { "Support", "Support lost", "Rain" }) p.Check(Tells(told, row), $"the workbench tells its {row.ToLowerInvariant()}", Pairs(told));
            p.Note($"support {Value(told, "Support")}; lost {Value(told, "Support lost")}; rain {Value(told, "Rain")}");
            var stone = Pick(Kind.Piece, "stone_wall_1x1", "stone_wall_2x1", "stone_wall_4x2");
            if (stone != null)
            {
                var wall = Facts.For(stone);
                p.Note($"{stone.Name}: support {Value(wall, "Support")}; rain {Value(wall, "Rain")}");
            }
            yield break;
        }

        private static IEnumerator SpawnerFacts(Probe p)
        {
            var nest = X.Catalog.FirstOrDefault(e => e.Name == "Spawner_GreydwarfNest")
                       ?? X.Catalog.FirstOrDefault(e => e.Source is GameObject prefab && prefab.GetComponentInChildren<SpawnArea>(true) != null);
            if (nest == null) p.Skip("there is no spawner");
            p.Note($"{nest.Name} ({nest.DisplayName})");
            var told = Facts.For(nest);
            foreach (var row in new[] { "Works", "Pace", "Keeps alive", "Puts them" }) p.Check(Tells(told, row), $"it tells {row.ToLowerInvariant()}", Pairs(told));
            p.Check(told.Pairs.Any(pair => pair.Key.StartsWith("Spawns ", StringComparison.Ordinal)), "it tells what it spawns and how often", Pairs(told));

            var area = ((GameObject)nest.Source).GetComponentInChildren<SpawnArea>(true);
            var first = area?.m_prefabs?.FirstOrDefault(d => d?.m_prefab != null)?.m_prefab;
            var creature = first != null ? X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && e.Name == first.name) : null;
            if (creature == null) yield break;
            var line = Facts.For(creature).Where.FirstOrDefault(s => s.Text.StartsWith("Comes from", StringComparison.Ordinal) && s.Prefab == nest.Name);
            if (p.Check(line.Text != null, $"{creature.Name} says it comes from it")) p.Check(line.Text.Contains("of the spawns") || line.Text.Contains("every spawn"), "with its share of the spawns", line.Text);
        }

        private static IEnumerator LootOdds(Probe p)
        {
            var items = X.Catalog.Where(e => e.Kind == Kind.Item && e.Origin == Origin.Vanilla).ToList();
            var withOdds = items.Where(e => Knowledge.SourceLines(e.Name).Any(s => s.Text.Contains("% a roll"))).ToList();
            p.Note($"{withOdds.Count} of {items.Count} of the game's items tell their share of a roll somewhere");
            p.Check(withOdds.Count > 0, "items tell their share of a roll in the tables that give them");
            var amber = items.FirstOrDefault(e => e.Name == "Amber");
            if (amber != null)
            {
                var lines = Knowledge.SourceLines("Amber").Select(s => s.Text).ToList();
                p.Check(lines.Any(t => t.Contains("% a roll")), "amber tells its odds in the chests that hold it", string.Join(" | ", lines.Take(3)));
            }
            yield break;
        }

        // ----- Previews played -----

        private static IEnumerator CreatureAttacks(Probe p)
        {
            var troll = Pick(Kind.Creature, "Troll", "Greydwarf");
            if (troll == null) p.Skip("there is no creature");
            Select(troll);
            yield return Until(() => CopyOf(troll) != null, 10);
            p.Check(CopyOf(troll) != null, "the creature stands on the stage");

            // Which clips are attacks is worked out by watching them, a little each frame.
            yield return Until(() => Previews.ClipTags().Values.Any(t => t.StartsWith("attack", StringComparison.Ordinal)), 20);
            var tags = Previews.ClipTags();
            var clips = Previews.Clips();
            var names = Previews.ClipNames();
            p.Note($"{clips.Count} clips, {tags.Count(t => t.Value.StartsWith("attack", StringComparison.Ordinal))} of them attacks");
            AnimationClip attack = null;
            for (var i = 0; i < clips.Count && attack == null; i++)
            {
                var name = i < names.Count ? names[i] : clips[i].name;
                if (tags.TryGetValue(name, out var tag) && tag.StartsWith("attack", StringComparison.Ordinal)) attack = clips[i];
            }
            if (!p.Check(attack != null, "its attacks are known")) yield break;

            Previews.PlayClip(attack);
            yield return Until(() => Previews.PlayingClip() != null, 3);
            p.Check(Previews.PlayingClip() == attack, "the attack plays", $"playing {Previews.PlayingClip()?.name ?? "nothing"}");
            yield return new Wait(1.5);
            Previews.StopClip();
        }

        // Each waits a frame after selecting, for the selection to be shown: showing it stops what
        // plays, and plays a sound by itself only when PlayOnSelect is on.

        private static IEnumerator SoundPlays(Probe p)
        {
            var sound = Pick(Kind.Sound, "sfx_troll_idle", "sfx_greydwarf_idle");
            if (sound == null) p.Skip("there is no sound");
            p.Note(sound.Name);
            Select(sound);
            yield return null;
            Previews.PlaySound(sound);
            yield return Until(() => Previews.SoundPlaying, 2);
            p.Check(Previews.SoundPlaying, "it plays");
            Previews.StopSound();
            p.Check(!Previews.SoundPlaying, "it stops");
        }

        private static IEnumerator EffectOnYou(Probe p)
        {
            var effect = Pick(Kind.Effect, "vfx_HitSparks", "vfx_Place_workbench");
            if (effect == null) p.Skip("there is no effect");
            p.Note(effect.Name);
            Select(effect);
            yield return null;
            var key = "on you:" + effect.Name;
            Previews.PlayEffect(effect, onYou: true);
            yield return Until(() => Previews.Playing.IsPlaying(key), 2);
            p.Check(Previews.Playing.IsPlaying(key), "it plays on you");
            Previews.Stop(key);
            p.Check(!Previews.Playing.IsPlaying(key), "it stops");
        }

        private static IEnumerator StatusOnYou(Probe p)
        {
            var status = StatusOnPerson();
            if (status == null) p.Skip("there is no status effect with a look of its own");
            p.Note(status.Name);
            Select(status);
            yield return null;
            Previews.ShowStatus(status);
            yield return Until(() => Previews.StatusShowing, 3);
            p.Check(Previews.StatusShowing, "its look is on you");
            Previews.StopStatus(false);
            p.Check(!Previews.StatusShowing, "and comes off");
        }

        private static IEnumerator ModelInWorld(Probe p)
        {
            yield return Until(() => !Previews.AnythingInWorld, 8);
            if (Previews.AnythingInWorld) p.Skip("something is already playing or standing in the world, and is left alone");
            var sword = Pick(Kind.Item, "SwordIron", "AxeBronze");
            if (sword == null) p.Skip("there is no sword or axe");
            Select(sword);
            yield return Until(() => CopyOf(sword) != null, 10);
            Previews.ToggleWorld();
            yield return Until(() => Previews.AnythingInWorld, 3);
            p.Check(Previews.InWorld && Previews.AnythingInWorld, "a copy stands where you look");
            Previews.ToggleWorld();
            yield return null;
            p.Check(!Previews.InWorld && !Previews.AnythingInWorld, "and is gone again");
        }

        private static IEnumerator ProjectileFlies(Probe p)
        {
            yield return Until(() => !Previews.AnythingInWorld, 8);
            if (Previews.AnythingInWorld) p.Skip("something is already playing or standing in the world, and is left alone");
            var arrow = Pick(Kind.Projectile, "bow_projectile", "bow_projectile_fire");
            if (arrow == null) p.Skip("there is no projectile");
            Select(arrow);
            yield return Until(() => CopyOf(arrow) != null, 10);
            Previews.Fire(arrow);
            p.Check(Previews.AnythingInWorld, "it flies");
            yield return Until(() => !Previews.AnythingInWorld, 14);
            p.Check(!Previews.AnythingInWorld, "and is gone once it has landed");
        }

        // ----- Raids -----

        private static IEnumerator RaidLinks(Probe p)
        {
            var raid = Pick(Kind.Raid, "army_eikthyr");
            if (raid == null) p.Skip("there is no raid");
            p.Note($"{raid.Name} ({raid.DisplayName})");
            Select(raid);
            yield return null;
            var told = Facts.For(raid);
            foreach (var row in new[] { "Comes for", "On the table", "Lasts" }) p.Check(Tells(told, row), $"it tells {row.ToLowerInvariant()}", Pairs(told));
            p.Check(told.Pairs.Any(pair => pair.Key.StartsWith("Brings ", StringComparison.Ordinal)), "it tells what it brings", Pairs(told));
            p.Check(!Stage.IsStaged(raid), "it has a card rather than a stage");

            var prefab = (raid.Source as RandomEvent)?.m_spawn?.Select(s => s?.m_prefab).FirstOrDefault(x => x != null);
            if (!p.Check(prefab != null, "it brings a creature")) yield break;
            p.Check(X.Jump(prefab.name), $"going to what it brings ({prefab.name})");
            var creature = X.Selected;
            p.Check(creature?.Kind == Kind.Creature, "lands on the creature");

            // The creature's own line about the raid goes back to it, by the raid's key.
            var line = Facts.For(creature).Where.FirstOrDefault(s => s.Prefab == raid.Key);
            if (p.Check(line.Text != null, "the creature tells the raid it comes in, linked to it"))
            {
                p.Check(X.Jump(line.Prefab) && X.Selected == raid, "that line goes to the raid", line.Text);
                p.Check(X.Back() && X.Selected == creature, "and Back returns to the creature");
            }
            p.Check(X.Back() && X.Selected == raid, "and Back again to the raid");
        }

        // ----- Locations, rooms and layouts -----

        private static Entry LocationNamed(params string[] names) => Pick(Kind.Location, names);

        private static PlaceSource PlaceOf(Entry entry) => entry?.Source as PlaceSource;

        private static Entry Crypt() => LocationNamed("Crypt2", "Crypt3", "Crypt4");

        private static Entry Camp() => LocationNamed("GoblinCamp2", "GoblinCamp2_1");

        private static IEnumerator LocationLoads(Probe p)
        {
            var crypt = Crypt();
            if (crypt == null) p.Skip("there is no location");
            var place = PlaceOf(crypt);
            p.Note($"{crypt.Name} ({crypt.DisplayName}), listed under {crypt.Group}");
            var forest = Knowledge.BiomeName("BlackForest");
            if (place.Biomes == Heightmap.Biome.BlackForest) p.Check(crypt.Group == forest, "it is listed under its biome", crypt.Group);

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
            // must not be what the stage frames.
            var size = Stage.SubjectSize;
            p.Note($"the stage shows it {size.x:0} × {size.z:0} m, {size.y:0} m high");
            p.Check(Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 300f, "the stage frames the place itself", $"{size.x:0} × {size.z:0} × {size.y:0} m");

            var contents = place.Contents;
            if (!p.Check(contents != null, "what it holds has been read")) yield break;
            p.Check(contents.GameName.Length > 0 || contents.Boss.Length > 0 || contents.Trader.Length > 0, "the game has a name for it", crypt.DisplayName);
            if (contents.GameName.Length > 0) p.Check(crypt.DisplayName == contents.GameName, "and it goes by it", crypt.DisplayName);
            p.Note($"{contents.Parts.Sum(x => x.Count)} parts of {contents.Parts.Count} kinds, {contents.Creatures.Count} kinds of creature, name \"{crypt.DisplayName}\"");
            var told = Facts.For(crypt);
            p.Check(Tells(told, "Per world"), "its details tell where the world places it");
            p.Check(Tells(told, "Holds") || Tells(told, "Its spawn points place"), "and what it holds");

            if (!contents.LeftToChance)
            {
                p.Note("nothing in it is left to chance, so there is nothing to roll again");
                yield break;
            }
            var before = Stage.Subject;
            Previews.Rebuild();
            yield return null;
            p.Check(Stage.Subject != null && Stage.Subject != before, "Roll again makes a new copy");
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
            if (!p.Check(example != null, "an example has been laid out", $"{ExampleLayouts.Read} of {ExampleLayouts.Total} kinds of room read")) yield break;
            p.Note($"{DungeonWords.Example(example, ExampleLayouts.Failed)}, its {ExampleLayouts.Total} kinds of room read and laid out {Time.unscaledTime - asked:0.0} s after selecting it");
            p.Check(ExampleLayouts.Failed == 0, "every kind of room could be loaded", $"{ExampleLayouts.Failed} could not");
            p.Check(example.Rooms.Count > 1, "it has rooms", $"{example.Rooms.Count}");
            if (algorithm == "Dungeon")
            {
                p.Check(example.Rooms[0].Room.Entrance, "it starts at an entrance", example.Rooms[0].Room.Name);
                var start = example.Rooms[0];
                p.Check(example.RoomAt(start.Position.X, start.Position.Z) != null, "a point on the plan finds a room there");
            }

            ExampleLayouts.Another();
            var another = ExampleLayouts.Example;
            p.Check(another != null && another != example && another.Rooms.Count > 0, "Another example lays out a new one");
            if (another == null) yield break;
            example = another;

            // Every room of it goes to an entry of its own.
            var keys = new HashSet<string>(X.Catalog.Select(e => e.Key));
            var missing = example.Rooms.Select(r => EntryKeys.For(Kind.Location, r.Room.Name)).Where(k => !keys.Contains(k)).Distinct().ToList();
            p.Check(missing.Count == 0, "every room on it has an entry to go to", string.Join(", ", missing.Take(5)));

            // The plan is far down the details; brought into view it draws its rooms.
            ScryPanel.PlanFolded = false;
            ScryPanel.RevealPlan();
            var drawn = ScryPanel.PlansDrawn;
            yield return Until(() => ScryPanel.PlansDrawn > drawn, 3);
            p.Check(ScryPanel.PlansDrawn > drawn, "the plan draws its rooms");

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

        private static IEnumerator ReadLocations(Probe p)
        {
            if (Locations.Now != Locations.State.NotRead) p.Skip($"the locations are {(Locations.Now == Locations.State.Read ? "already read" : "being read")} in this world");

            // A location stands on the stage throughout, from a bundle the reading loads and lets go of too.
            var shown = Camp() ?? Crypt();
            GameObject copy = null;
            var brokenBefore = 0;
            if (shown != null)
            {
                Select(shown);
                yield return Until(() => CopyOf(shown) != null, 30);
                copy = CopyOf(shown);
                brokenBefore = Broken(copy);
            }

            var asked = Time.unscaledTime;
            p.Note(Locations.Start());
            yield return Until(() => Locations.Now == Locations.State.Read, 400);
            if (!p.Check(Locations.Now == Locations.State.Read, "every location and room has been read", $"{Locations.Done} of {Locations.Total}")) yield break;
            p.Note($"read in {Time.unscaledTime - asked:0} s");

            if (copy != null)
            {
                p.Check(Stage.Subject == copy, $"{shown.Name} stayed on the stage meanwhile");
                p.Check(copy != null && Broken(copy) == brokenBefore, "and kept every mesh and material", $"{Broken(copy)} missing, {brokenBefore} before");
            }

            var places = X.Catalog.Where(e => PlaceOf(e) != null).ToList();
            var read = places.Count(e => PlaceOf(e).Contents != null);
            p.Check(read >= places.Count * 0.9, "nearly every location and room knows what it holds", $"{read} of {places.Count}");
            p.Check(Locations.Summons.Count > 0, "the altars' bosses are known", $"{Locations.Summons.Count}");
            p.Check(X.Catalog.Any(e => e.Kind == Kind.Item && e.FoundIn.Length > 0), "items tell where they are found");

            // Every place goes by its name, and rooms by the dungeons built with them.
            var crypt = Crypt();
            var contents = PlaceOf(crypt)?.Contents;
            if (crypt != null && contents != null && contents.GameName.Length > 0) p.Check(crypt.DisplayName == contents.GameName, "the crypt goes by the game's name", crypt.DisplayName);
            var rooms = places.Where(e => PlaceOf(e).IsRoom).ToList();
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

        /// <summary>How many meshes and materials a copy is missing, which a bundle unloaded under it would take.</summary>
        private static int Broken(GameObject copy)
        {
            if (copy == null) return 0;
            var missing = copy.GetComponentsInChildren<MeshFilter>(true).Count(m => m.sharedMesh == null);
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true)) missing += renderer.sharedMaterials.Count(m => m == null);
            return missing;
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

        // ----- The whole run -----

        private static IEnumerator WholeRun(Probe p)
        {
            p.Note(Frames.Line(Budget));
            foreach (var frame in Frames.Slowest(5)) p.Note($"a slow frame: {frame.Ms:0.#} ms, the slowest part {frame.Part} {frame.PartMs:0.#} ms");
            p.Check(Frames.Frames > 0, "Scry's own work was measured");
            p.Check(Frames.Max < 250, "no frame of Scry's own work took a quarter of a second", $"{Frames.Max:0} ms at the most");
            yield break;
        }
    }
}
