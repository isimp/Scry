using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's scenarios, in the order they run: the check and the catalog, each kind shown, the previews played, locations and their layouts, raids, and last the reading of every location.</summary>
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

            yield return S("a creature plays an attack", CreatureAttacks, 30);
            yield return S("a sound plays and stops", SoundPlays, 10);
            yield return S("an effect plays on you and stops", EffectOnYou, 10);
            yield return S("a status effect shows on you and comes off", StatusOnYou, 10);
            yield return S("a model stands in the world and goes again", ModelInWorld, 10);
            yield return S("a projectile flies where you look", ProjectileFlies, 15);

            yield return S("a raid tells what it brings and links its creatures", RaidLinks, 10);
            yield return S("a location loads, shows what it holds and rolls again", LocationLoads, 45);
            yield return S("a dungeon room shows its shape", RoomShows, 30);
            yield return S("a dungeon lays out an example", DungeonExample, 180);
            yield return S("a camp lays out an example", CampExample, 120);
            yield return S("placement details tell the woods and lava a location keeps to", PlacementDetails, 20);
            yield return S("reading every location fills their details", ReadLocations, 420);

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

        private static IEnumerator SoundPlays(Probe p)
        {
            var sound = Pick(Kind.Sound, "sfx_troll_idle", "sfx_greydwarf_idle");
            if (sound == null) p.Skip("there is no sound");
            p.Note(sound.Name);
            Select(sound);
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

        // ----- Raids, locations, rooms and layouts -----

        private static IEnumerator RaidLinks(Probe p)
        {
            var raid = Pick(Kind.Raid, "army_eikthyr");
            if (raid == null) p.Skip("there is no raid");
            p.Note($"{raid.Name} ({raid.DisplayName})");
            Select(raid);
            yield return null;
            var told = Facts.For(raid);
            p.Check(!told.IsEmpty, "its details tell something");

            var creature = (raid.Source as RandomEvent)?.m_spawn?.Select(s => s?.m_prefab).FirstOrDefault(prefab => prefab != null);
            if (!p.Check(creature != null, "it brings a creature")) yield break;
            p.Check(X.Jump(creature.name), $"going to what it brings ({creature.name})");
            p.Check(X.Selected?.Kind == Kind.Creature, "lands on the creature");
            p.Check(X.Back() && X.Selected == raid, "and Back returns to the raid");
        }

        private static Entry LocationNamed(params string[] names) => Pick(Kind.Location, names);

        private static PlaceSource PlaceOf(Entry entry) => entry?.Source as PlaceSource;

        private static IEnumerator LocationLoads(Probe p)
        {
            var crypt = LocationNamed("Crypt2", "Crypt3", "Crypt4");
            if (crypt == null) p.Skip("there is no location");
            var place = PlaceOf(crypt);
            p.Note($"{crypt.Name} ({crypt.DisplayName})");
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

            var contents = place.Contents;
            if (!p.Check(contents != null, "what it holds has been read")) yield break;
            p.Check(contents.GameName.Length > 0 || contents.Boss.Length > 0 || contents.Trader.Length > 0, "it goes by the game's name", crypt.DisplayName);
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

        private static IEnumerator RoomShows(Probe p)
        {
            var room = X.Catalog.FirstOrDefault(e => PlaceOf(e) is PlaceSource place && place.IsRoom && place.Prefab.StartsWith("forestcrypt", StringComparison.OrdinalIgnoreCase))
                       ?? X.Catalog.FirstOrDefault(e => PlaceOf(e)?.IsRoom == true);
            if (room == null) p.Skip("there is no dungeon room");
            var place = PlaceOf(room);
            p.Note(room.Name);
            Select(room);
            yield return Until(() => PlaceAssets.State(place) != PlaceLoad.Loading && CopyOf(room) != null, 20);
            p.Check(PlaceAssets.State(place) == PlaceLoad.Ready, "its model has loaded", PlaceAssets.State(place).ToString());
            p.Check(CopyOf(room) != null, "a copy stands on the stage");
            var shape = place.Contents?.Room;
            if (!p.Check(shape != null, "its shape has been read")) yield break;
            p.Check(shape.Size.X > 0 && shape.Size.Z > 0 || shape.EndCap || shape.Divider, "it has a floor", DungeonWords.Size(shape));
            p.Check(shape.Doorways.Count > 0, "it has doorways", DungeonWords.Doorways(shape));
            p.Note($"{DungeonWords.Role(shape)}, {DungeonWords.Size(shape)}, doorways {DungeonWords.Doorways(shape)}");
        }

        private static IEnumerator DungeonExample(Probe p) => Layout(p, LocationNamed("Crypt2", "Crypt3", "Crypt4"), "Dungeon");

        private static IEnumerator CampExample(Probe p) => Layout(p, LocationNamed("GoblinCamp2", "GoblinCamp2_1"), "CampRadial");

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
            p.Check(ExampleLayouts.Example != null && ExampleLayouts.Example != example, "Another example lays out a new one");
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
            var asked = Time.unscaledTime;
            p.Note(Locations.Start());
            yield return Until(() => Locations.Now == Locations.State.Read, 400);
            if (!p.Check(Locations.Now == Locations.State.Read, "every location and room has been read", $"{Locations.Done} of {Locations.Total}")) yield break;
            p.Note($"read in {Time.unscaledTime - asked:0} s");

            var places = X.Catalog.Where(e => PlaceOf(e) != null).ToList();
            var read = places.Count(e => PlaceOf(e).Contents != null);
            p.Check(read >= places.Count * 0.9, "nearly every location and room knows what it holds", $"{read} of {places.Count}");
            p.Check(Locations.Summons.Count > 0, "the altars' bosses are known", $"{Locations.Summons.Count}");
            p.Check(X.Catalog.Any(e => e.Kind == Kind.Item && e.FoundIn.Length > 0), "items tell where they are found");
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
