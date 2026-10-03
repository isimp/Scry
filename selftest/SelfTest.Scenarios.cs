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
            yield return S(ProgressPart, ProgressShown, 5);
            yield return S("each kind of search term finds what it says", SearchTerms, 20);
            yield return S("favourites, recent and history keep what they should", FavouritesRecentHistory, 10);
            yield return S("every tab lists all it counts, group by group", TabsAndGroups, 20);
            yield return S("both views draw, and /scry searches", PanelViews, 10);
            yield return S("the arrow keys move, Game and Mods filter, and an empty tab shows every kind", ListKeysAndFilters, 10, ResetList);
            yield return S("the search's help opens and closes", HelpOpens, 5, () => ScryPanel.ShowHelp(false));
            yield return S("the catalog is sound: keys, names, groups and counts", CatalogSound, 10);
            yield return S("every search is quick enough to run as you type", SearchSpeed, 10, ResetList);

            yield return S("a creature shows on the stage and tells its facts", p => Shows(p, Pick(Kind.Creature, "Troll", "Greydwarf"), true), 20);
            yield return S("an item shows on the stage and tells its facts", p => Shows(p, Pick(Kind.Item, "SwordIron", "AxeBronze"), true), 20);
            yield return S("a piece shows on the stage and tells its facts", p => Shows(p, Pick(Kind.Piece, "piece_workbench"), true), 20);
            yield return S("a resource shows on the stage and tells its facts", p => Shows(p, Pick(Kind.Resource, "Beech1", "Birch1", "Pine"), true), 20);
            yield return S("a projectile shows on the stage and tells its facts", p => Shows(p, Pick(Kind.Projectile, "bow_projectile", "bow_projectile_fire"), true), 20);
            yield return S("an effect shows on the stage", p => Shows(p, Pick(Kind.Effect, "vfx_HitSparks", "vfx_Place_workbench"), false), 20);
            yield return S("a status effect shows on the person and tells its facts", p => Shows(p, StatusOnPerson(), true), 20);

            yield return S("creatures tell their weak spots, when they turn on you and how far they chase", CreatureFacts, 60);
            yield return S("a piece tells its support and what wears it", PieceFacts, 10);
            yield return S("build tools and their pieces lead to each other", ToolsAndPieces, 10);
            yield return S("mods hooking into drops, loot and spawning are named", HookingMods, 10);
            yield return S("the mod report links every mod station as the game does", ModReportLinks, 20);
            yield return S("what creatures drop in play is watched", DropsWatched, 5);
            yield return S("every mod has a page of what it adds and changes", ModPages, 10);
            yield return S("a mod's page tells what its package says and how it ties to other mods", ModPackages, 10);
            yield return S("which mod added what is found from every clue, and nothing of the game's is put down to a mod", ModClues, 10);
            yield return S("gear tells its resistances, what it changes while worn, and a weapon its second attack", GearFacts, 10);
            yield return S("tame creatures tell how they breed and are ridden, their young where they come from and what they grow into", BreedingFacts, 10);
            yield return S("fish tell their baits, doors their keys, and bosses and trophies their Forsaken powers, each both ways", BaitsKeysPowers, 10);
            yield return S("pieces tell where they may be placed, stations their reach, upgrades their station, areas what they do, and beds what they need", BuildingRules, 10);
            yield return S("ballistas, traps, ships, carts and catapults tell what they do", MachineFacts, 10);
            yield return S("a creature whose defeat sets a world key tells what then follows", AfterDefeat, 10);
            yield return S("each biome has a page of its weathers, music and what is there, and every biome named has one", BiomePages, 10);
            yield return S("the ground is drawn with the terrain's own material", TerrainMaterial, 5);
            yield return S("what Scry is not sure of is marked with why, and what it read from the game is not", UnsureMarks, 10);
            yield return S("resistances show as a grid of every damage type on every creature, piece and rock", ResistanceGrids, 10);
            yield return S("every creature, piece of gear and buildable piece shows its standard rows, saying none where it has none", StandardRows, 20);
            yield return S("a spawner tells its pool and pace, and its creatures their share", SpawnerFacts, 10);
            yield return S("items tell their odds in the tables that give them", LootOdds, 10);
            yield return S("items, stations, smelters and beds tell what they are for", WhatThingsTell, 10);

            yield return S("the stage's lighting, backdrops and views all draw", StageControls, 20);
            yield return S("a bigger size makes a bigger copy", SizeAdjusts, 10);
            yield return S("a creature stands with stars and in each look", StarsAndLooks, 30, () => X.Modifiers.Reset());
            yield return S("a slower animation slows it, and a piece stands in every wear", SpeedAndWear, 30, () => X.Modifiers.Reset());
            yield return S("loudness follows, and a new selection starts afresh", AdjustmentsStartAfresh, 10, () => X.Modifiers.Reset());
            yield return S("a spread of every kind stands on the stage", KindsOnStage, 240, bearsSkips: true);
            yield return S("switching every frame leaves the last one shown", FastSwitching, 60);
            yield return S("after many others, the stage holds no leftovers", NoLeftovers, 40);
            yield return S("every raid tells what it brings", EveryRaid, 10);
            yield return S("boss fights are grouped apart from raids, in the order they are fought", BossFightsGrouped, 10, ResetList);
            yield return S("a raid stands on the stage as the first roll of its creatures, and rolls again", RaidWave, 60);
            yield return S("every raid that brings creatures stands on the stage as rolled", EveryRaidWave, 120);
            yield return S("a creature plays an attack", CreatureAttacks, 30);
            yield return S("a clip plays, pauses, seeks, loops and stops", ClipControls, 20, Previews.StopClip);
            yield return S("a sound plays and stops", SoundPlays, 10);
            yield return S("a sound plays the variant chosen", SoundVariant, 10);
            yield return S("a sound pauses, seeks and goes on", SoundControls, 15, Previews.StopSound);
            yield return S("an effect loops until stopped", EffectLoops, 10);
            yield return S("an effect plays on you and stops", EffectOnYou, 10);
            yield return S("a status effect shows on you and comes off", StatusOnYou, 10);
            yield return S("an item is worn by the person and taken off", WearIt, 25, () => Looks.OnPerson = _before?.OnPerson ?? false);
            yield return S("a tree felled leaves what it leaves, which goes again", TreeFalls, 40);
            yield return S("a model stands in the world and goes again", ModelInWorld, 10);
            yield return S("a projectile flies where you look", ProjectileFlies, 15);
            yield return S("Clear world takes away what stands in the world", ClearWorld, 15);
            yield return S("closing the panel quiets it, and opening it brings the selection back", CloseAndOpen, 25, () => { if (!Session.IsOpen) Session.Show(null); });

            yield return S("a raid tells what it brings, and its creatures lead back to it", RaidLinks, 10);
            yield return S("a location loads, shows what it holds and rolls again", LocationLoads, 45);
            yield return S("a runestone location tells its texts", RunestoneTexts, 40);
            yield return S("switching between locations while they load holds only the one shown", Switching, 60);
            yield return S("a dungeon room shows its shape", RoomShows, 30);
            yield return S("the roof's cut moves, starts afresh, and a location keeps its roof", CutMoves, 60);
            yield return S("places' floors are found in the places themselves", FloorsFound, 240, bearsSkips: true);
            yield return S("a dungeon lays out an example, drawn, its rooms going to their entries", DungeonExample, 200, bearsSkips: true);
            yield return S("a camp lays out an example, drawn", CampExample, 150, bearsSkips: true);
            yield return S("the cave and tower dungeons find floors with rooms on each", CaveFloors, 300, bearsSkips: true);
            yield return S("a place's creatures stand on the stage with it", PlaceCreatures, 60, bearsSkips: true);
            yield return S("the stage's camera looks at the floor opened, zooms toward the pointer and drags with it", CameraMoves, 90, bearsSkips: true);
            yield return S("the world's lights stay off the stage", WorldLightsOff, 20);
            yield return S("the Ground backdrop lays each biome's own ground", GroundBackdrop, 120, bearsSkips: true);
            yield return S("Scry costs next to nothing idling with the panel closed", IdleCost, 20);
            yield return S("the resource monitor shows what Scry costs and holds", ResourceMonitor, 10);
            yield return S("placement details tell the woods and lava a location keeps to", PlacementDetails, 20);
            yield return S("reading every location fills their details", ReadLocations, 450, bearsSkips: true);
            yield return S("every dungeon and camp lays out", EveryDungeonLaysOut, 60, bearsSkips: true);
            yield return S("content lists come in their order, a place's parts by what they are", ContentOrders, 60, bearsSkips: true);
            yield return S("every dungeon heads its group, its rooms under it", EveryDungeonHeadsItsGroup, 10);
            yield return S("a spread of locations stands on the stage", LocationsOnStage, 200, bearsSkips: true);
            yield return S("every entry's details are told, every chip leading somewhere", EveryDetail, 300, bearsSkips: true);
            yield return S("a location plays its music on Enter and gives the game's back", PlaysMusic, 30);
            yield return S("every boss tells its altar, and every trader what it sells", AltarsAndTraders, 10);
            yield return S("every runestone's texts are told in words", EveryRunestone, 10);
            yield return S("every location with music of its own plays it", EveryMusic, 300, Previews.StopSound);
            yield return S("the largest locations are made over several frames", LargestLocations, 120);
            yield return S("a spread of rooms stands on its floor, opened", RoomsOnStage, 160);
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

            // The stage is drawn only in the full view; the view it was in is put back afterwards.
            KeepView();
            if (ScryPanel.Compact)
            {
                ScryPanel.Compact = false;
                p.Note("switched to the full view, which draws the stage");
                from = ScryPanel.Repaints;
                yield return Until(() => ScryPanel.Repaints >= from + 2, 5);
            }
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

        /// <summary>
        /// The workbench says the hammer builds it and on which tab, the hammer lists it there,
        /// and every tool, mods' too, lists what it builds.
        /// </summary>
        private static IEnumerator ToolsAndPieces(Probe p)
        {
            var bench = Pick(Kind.Piece, "piece_workbench");
            var hammer = Pick(Kind.Item, "Hammer");
            if (bench == null || hammer == null) p.Skip("there is no workbench or hammer");
            var built = Value(Facts.For(bench), "Built with") ?? "";
            p.Check(built.StartsWith("Hammer", StringComparison.Ordinal) || built.Contains(hammer.DisplayName), "the workbench says the hammer builds it", built);
            var builds = Facts.For(hammer).Rows.Where(r => r.Title.StartsWith("Builds on its", StringComparison.Ordinal)).ToList();
            p.Check(builds.Any(r => r.Items.Any(i => i.Prefab == "piece_workbench")), "the hammer lists the workbench among what it builds", string.Join("; ", builds.Select(r => r.Title)));

            var tools = X.Catalog.Where(e => e.Kind == Kind.Item && Knowledge.Tools.IsTool(e.Name)).ToList();
            p.Note($"{tools.Count} build tools: " + string.Join(", ", tools.Select(t => $"{t.Name} ({Knowledge.Tools.PiecesOf(t.Name).Sum(tab => tab.Pieces.Count)} pieces{(t.Origin == Origin.Vanilla ? "" : ", " + t.ModName)})")));
            var empty = tools.Where(t => !Facts.For(t).Rows.Any(r => r.Title.StartsWith("Builds on its", StringComparison.Ordinal))).Select(t => t.Name).ToList();
            p.Check(empty.Count == 0, "every tool lists what it builds", string.Join(", ", empty));
            var pieces = X.Catalog.Where(e => e.Kind == Kind.Piece).ToList();
            var withTool = pieces.Count(e => Knowledge.Tools.ToolsOf(e.Name).Count > 0);
            p.Note($"{withTool} of {pieces.Count} pieces are built with a tool; the rest are in no build menu");
            yield break;
        }

        /// <summary>
        /// The mods found hooking into drops, loot and spawning, and every hook looked at; a
        /// creature and a rock name the mods there are, and say nothing where there are none.
        /// </summary>
        private static IEnumerator HookingMods(Probe p)
        {
            foreach (HookedRule rule in Enum.GetValues(typeof(HookedRule)))
            {
                p.Note($"{rule}: {(ModHooks.Mods(rule).Count > 0 ? string.Join(", ", ModHooks.Mods(rule)) : "no mod")}");
            }
            foreach (var (mod, method, counted) in ModHooks.Seen) p.Note($"{mod} hooks {method}{(counted ? "" : ", not counted: its code names nothing that decides it there")}");
            p.Check(!ModHooks.AllMods.Contains("Scry"), "Scry itself is not named");

            var creature = Pick(Kind.Creature, "Greydwarf", "Boar");
            if (creature != null)
            {
                var note = HookNote(Facts.For(creature), HookedRule.Drops);
                var mods = ModHooks.Mods(HookedRule.Drops);
                p.Check(mods.Count == 0 ? note == null : note != null && mods.All(m => note.Contains(m)), $"{creature.Name} names the mods hooking into its drops", note ?? "no note");
            }
            var rock = Pick(Kind.Resource, "rock1_forest", "Rock_3", "MineRock_Copper", "Beech1");
            if (rock != null)
            {
                var note = HookNote(Facts.For(rock), HookedRule.Loot);
                var mods = ModHooks.Mods(HookedRule.Loot);
                p.Check(mods.Count == 0 ? note == null : note != null, $"{rock.Name} names the mods hooking into what it gives, if it gives anything", note ?? "no note");
            }

            // The rest, each on an entry that tells what the rule decides.
            foreach (var (rule, entry) in new[]
            {
                (HookedRule.Comfort, Pick(Kind.Piece, "bed", "piece_bed02")), (HookedRule.Wear, Pick(Kind.Piece, "wood_wall", "stone_wall_1x1")),
                (HookedRule.Smelting, Pick(Kind.Piece, "smelter")), (HookedRule.Cooking, Pick(Kind.Piece, "piece_cookingstation")),
                (HookedRule.Fermenting, Pick(Kind.Piece, "fermenter")), (HookedRule.Burning, Pick(Kind.Piece, "fire_pit", "piece_brazierfloor01")),
                (HookedRule.Producing, Pick(Kind.Piece, "piece_beehive")), (HookedRule.Crafting, Pick(Kind.Item, "AxeBronze", "SwordIron")),
                (HookedRule.ItemStats, Pick(Kind.Item, "SwordIron", "AxeBronze")), (HookedRule.Food, Pick(Kind.Item, "CookedMeat", "Raspberry")),
                (HookedRule.Growth, Pick(Kind.Piece, "sapling_carrot", "sapling_turnip")), (HookedRule.Taming, Pick(Kind.Creature, "Boar", "Wolf")),
                (HookedRule.Raids, X.Catalog.FirstOrDefault(e => e.Kind == Kind.Raid && e.Name == "army_eikthyr")), (HookedRule.Trading, X.Catalog.FirstOrDefault(e => e.Source is GameObject g && g.GetComponent<Trader>() != null)),
                (HookedRule.Storage, Pick(Kind.Piece, "piece_chest_wood", "piece_chest")),
            })
            {
                if (entry == null) continue;
                var note = HookNote(Facts.For(entry), rule);
                var mods = ModHooks.Mods(rule);
                p.Check(mods.Count == 0 ? note == null : note != null && mods.All(m => note.Contains(m)), $"{entry.Name} names the mods hooking into {rule}", note ?? "no note");
            }
            yield break;
        }

        /// <summary>A page's note on the mods hooking into a rule, as its one line's hover tells it; null for none.</summary>
        private static string HookNote(Facts facts, HookedRule rule) =>
            facts.Hooks.Where(h => h.Rule == rule).Select(h => ModHookWords.Note(h.Rule, h.Mods)).FirstOrDefault();

        /// <summary>
        /// The mod report as a player opens it, written out whole, and checked against the game's
        /// own lists: every piece a mod's station is named by (Piece.m_craftingStation) and every
        /// recipe made at it must be linked to it.
        /// </summary>
        private static IEnumerator ModReportLinks(Probe p)
        {
            var report = ScryPanel.Report(X);
            var mods = X.Catalog.Where(e => e.Origin == Origin.Mod && e.Kind != Kind.Mod).Select(e => e.ModName.Length > 0 ? e.ModName : ModReportReader.UnknownMod).Distinct().ToList();
            var missing = mods.Where(m => !report.Any(r => r.Mod == m)).ToList();
            p.Check(missing.Count == 0, "every mod that adds anything is in the report", string.Join(", ", missing));
            foreach (var mod in report)
            {
                var gaps = new List<string>();
                if (mod.IdleStations.Count > 0) gaps.Add($"{mod.IdleStations.Count} idle stations");
                if (mod.Sourceless.Count > 0) gaps.Add($"{mod.Sourceless.Count} items with no source ({string.Join(", ", mod.Sourceless.Take(4).Select(e => e.Name))})");
                if (mod.Unspawned.Count > 0) gaps.Add($"{mod.Unspawned.Count} creatures spawning nowhere seen ({string.Join(", ", mod.Unspawned.Take(4).Select(e => e.Name))})");
                if (mod.Unbuilt.Count > 0) gaps.Add($"{mod.Unbuilt.Count} pieces in no build menu");
                p.Note($"{mod.Mod}: {ModReportWords.Counts(mod)}" + (mod.Hooks.Count > 0 ? $"; hooks into {ModReportWords.Hooks(mod.Hooks)}" : "")
                       + string.Concat(mod.Stations.Select(s => $"; station {s.Name}: {ModReportWords.Station(s)}"))
                       + string.Concat(mod.Tools.Select(t => $"; tool {t.Name}: {ModReportWords.Tool(t)}"))
                       + (gaps.Count > 0 ? "; not placed: " + string.Join(", ", gaps) : ""));
            }

            // Scry's links against the game's own lists, station by station.
            var linked = X.Catalog.Where(e => e.Stations != null).ToList();
            var wrong = new List<string>();
            foreach (var station in report.SelectMany(m => m.Stations))
            {
                var pieces = ZNetScene.instance.m_prefabs.Where(g => g != null && g.GetComponent<Piece>() is Piece piece && piece.m_craftingStation != null && piece.m_craftingStation.gameObject.name == station.Name).Select(g => g.name).ToList();
                var recipes = ObjectDB.instance.m_recipes.Where(r => r != null && r.m_enabled && r.m_item != null && r.m_craftingStation != null && r.m_craftingStation.gameObject.name == station.Name).Select(r => r.m_item.gameObject.name).Distinct().ToList();
                var builtNear = new HashSet<string>(linked.Where(e => e.Kind == Kind.Piece && e.Stations.Any(s => s.Name == station.Name)).Select(e => e.Name));
                var madeHere = new HashSet<string>(linked.Where(e => e.Kind != Kind.Piece && e.Stations.Any(s => s.Name == station.Name)).Select(e => e.Name));
                var unlinked = pieces.Where(n => !builtNear.Contains(n)).Concat(recipes.Where(n => !madeHere.Contains(n))).ToList();
                p.Note($"{station.Name}: the game names it for {pieces.Count} pieces and {recipes.Count} recipes; Scry links {builtNear.Count} built near and {madeHere.Count} made here");
                if (unlinked.Count > 0) wrong.Add($"{station.Name} misses {string.Join(", ", unlinked.Take(5))}");
            }
            p.Check(wrong.Count == 0, "every piece and recipe the game names a mod's station for is linked to it", string.Join("; ", wrong));

            // As a player opens it: the report in the list's place, drawn.
            ScryPanel.ShowModReport();
            var drawn = ScryPanel.ModReportsDrawn;
            yield return Until(() => ScryPanel.ModReportsDrawn > drawn, 3);
            p.Check(ScryPanel.ModReportShown && ScryPanel.ModReportsDrawn > drawn, "the report opens and draws in the panel");
            ScryPanel.HideModReport();
        }

        /// <summary>
        /// The hooks watching the loot of deaths are in place, and what was seen so far is told:
        /// a creature seen dying shows its row, and each item it dropped names it. Nothing is
        /// killed for the test, so what it checks is what earlier play left.
        /// </summary>
        private static IEnumerator DropsWatched(Probe p)
        {
            foreach (var (type, name) in new[] { (typeof(CharacterDrop), "OnDeath"), (typeof(Ragdoll), "SpawnLoot"), (typeof(Ragdoll), "Setup"), (typeof(ItemDrop), "Awake") })
            {
                var method = HarmonyLib.AccessTools.DeclaredMethod(type, name);
                p.Check(method != null && HarmonyLib.Harmony.GetPatchInfo(method)?.Owners.Contains(Plugin.Guid) == true, $"{type.Name}.{name} is watched");
            }
            var creatures = X.Catalog.Where(e => e.Kind == Kind.Creature && DropWatch.Seen.Kills(e.Name) > 0).ToList();
            p.Note($"{creatures.Count} kinds of creature seen dying in play, {creatures.Sum(c => DropWatch.Seen.Kills(c.Name))} kills: " + string.Join(", ", creatures.Take(10).Select(c => $"{c.Name} {DropWatch.Seen.Kills(c.Name)}")));
            var seen = creatures.FirstOrDefault(c => DropWatch.Seen.Of(c.Name).Count > 0);
            if (seen == null)
            {
                p.Note("nothing seen dropping yet; kill something and run the test again to see it told");
                yield break;
            }
            var told = Facts.For(seen);
            p.Check(told.Rows.Any(r => r.Title == SeenWords.Title(DropWatch.Seen.Kills(seen.Name))), $"{seen.Name} shows what it was seen to drop");
            var item = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == DropWatch.Seen.Of(seen.Name)[0].Item);
            if (item != null) p.Check(Facts.For(item).Where.Any(l => l.Prefab == seen.Name && l.Text.StartsWith("Seen dropped by", StringComparison.Ordinal)), $"{item.Name} names {seen.Name} as seen dropping it");
        }

        /// <summary>
        /// Every mod loaded has an entry of its own; the one adding the most tells what it adds
        /// kind by kind, and one of its entries names it as a page to go to.
        /// </summary>
        private static IEnumerator ModPages(Probe p)
        {
            var loaded = BepInEx.Bootstrap.Chainloader.PluginInfos.Values.Select(i => i?.Metadata?.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            var pages = X.Catalog.Where(e => e.Kind == Kind.Mod).ToList();
            var missing = loaded.Where(n => !pages.Any(e => e.Name == n)).ToList();
            p.Check(missing.Count == 0, "every mod loaded has a page", string.Join(", ", missing.Take(5)));
            foreach (var group in pages.GroupBy(e => e.Group)) p.Note($"{group.Key}: {group.Count()}");

            var busiest = pages.OrderByDescending(e => X.Catalog.Count(c => c.ModName == e.Name && c.Kind != Kind.Mod)).FirstOrDefault();
            if (busiest == null || X.Catalog.All(c => c.ModName != busiest.Name || c.Kind == Kind.Mod))
            {
                p.Note("no mod adds anything, so no page tells what it adds");
                yield break;
            }
            var told = Facts.For(busiest);
            p.Check(Tells(told, "Adds") && told.Rows.Any(r => r.Title.StartsWith("Adds ", StringComparison.Ordinal)), $"{busiest.Name}'s page tells what it adds, kind by kind", string.Join("; ", told.Rows.Select(r => r.Title)));
            p.Note($"{busiest.Name}: {Pairs(told)}");
            var added = X.Catalog.First(c => c.ModName == busiest.Name && c.Kind != Kind.Mod);
            p.Check(X.Catalog.Any(e => e.Key == EntryKeys.For(Kind.Mod, added.ModName)), $"{added.Name} names its mod's page", added.ModName);
            p.Check(X.Jump(busiest.Key) && X.Selected == busiest, "the page can be gone to");
        }

        /// <summary>
        /// What mod managers put beside a mod is read: its package's description, author,
        /// website, icon and readme, each told on its page; and the mods each needs and works
        /// with are told both ways, as BepInEx and the packages declare them.
        /// </summary>
        private static IEnumerator ModPackages(Probe p)
        {
            var pages = X.Catalog.Where(e => e.Kind == Kind.Mod && e.Source is ModSource).ToList();
            var mods = pages.Select(e => (Entry: e, Mod: (ModSource)e.Source)).ToList();
            var packaged = mods.Where(m => m.Mod.Description.Length > 0 || m.Mod.IconPath.Length > 0 || m.Mod.ReadmePath.Length > 0).ToList();
            p.Note($"{mods.Count} mods, {packaged.Count} from a package: {mods.Count(m => m.Mod.Description.Length > 0)} described, {mods.Count(m => m.Mod.Author.Length > 0)} with an author, "
                   + $"{mods.Count(m => ModWords.IsWebsite(m.Mod.Website))} with a website, {mods.Count(m => m.Mod.IconPath.Length > 0)} with an icon, {mods.Count(m => m.Mod.ReadmePath.Length > 0)} with a readme");
            if (packaged.Count == 0) p.Skip("no mod was installed by a mod manager");

            var iconless = mods.Where(m => m.Mod.IconPath.Length > 0 && !(m.Entry.Icon is Sprite)).Select(m => m.Mod.Name).ToList();
            p.Check(iconless.Count == 0, "every package's icon is read", string.Join(", ", iconless.Take(5)));

            var described = mods.FirstOrDefault(m => m.Mod.Description.Length > 0 && m.Mod.Author.Length > 0);
            if (described.Mod != null)
            {
                var told = Facts.For(described.Entry);
                p.Check(told.Description == described.Mod.Description && Value(told, "By") == described.Mod.Author, $"{described.Mod.Name}'s page tells what it is and who made it", $"{told.Description} / {Value(told, "By")}");
                if (ModWords.IsWebsite(described.Mod.Website))
                {
                    p.Check(told.Links.TryGetValue("Website", out var web) && web == Facts.OpenWebsite + described.Mod.Website, "its website opens in the browser");
                }
            }

            // Both ways: a mod one needs names it as needing it, and the same for working with.
            var byName = mods.ToDictionary(m => m.Mod.Name, m => m.Mod.Relations);
            var oneWay = new List<string>();
            foreach (var (_, mod) in mods)
            {
                foreach (var other in mod.Relations.Needs) if (!byName.TryGetValue(other, out var them) || !them.NeededBy.Contains(mod.Name)) oneWay.Add($"{mod.Name} needs {other}");
                foreach (var other in mod.Relations.WorksWith) if (!byName.TryGetValue(other, out var them) || !them.WorkedWithBy.Contains(mod.Name)) oneWay.Add($"{mod.Name} works with {other}");
            }
            p.Check(oneWay.Count == 0, "every tie between mods is told on both", string.Join("; ", oneWay.Take(5)));
            var declared = BepInEx.Bootstrap.Chainloader.PluginInfos.Values.Where(i => i?.Metadata != null && i.Dependencies != null)
                .SelectMany(i => i.Dependencies.Where(d => (d.Flags & BepInEx.BepInDependency.DependencyFlags.HardDependency) != 0 && BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(d.DependencyGUID))
                    .Select(d => (Mod: i.Metadata.Name, Needs: BepInEx.Bootstrap.Chainloader.PluginInfos[d.DependencyGUID].Metadata.Name))).ToList();
            var missed = declared.Where(d => d.Mod != d.Needs && byName.TryGetValue(d.Mod, out var r) && !r.Needs.Contains(d.Needs)).Select(d => $"{d.Mod} needs {d.Needs}").ToList();
            p.Check(missed.Count == 0, $"every mod a mod declares it needs is told ({declared.Count} declared)", string.Join("; ", missed.Take(5)));
            var mostNeeded = mods.OrderByDescending(m => m.Mod.Relations.NeededBy.Count).First();
            p.Note($"{mostNeeded.Mod.Name} is needed by {mostNeeded.Mod.Relations.NeededBy.Count}; {mods.Count(m => m.Mod.Relations.WorksWith.Count > 0)} mods work with others when there; {mods.Count(m => m.Mod.Relations.WillNotRunWith.Count > 0)} will not run with some");

            var withReadme = mods.Where(m => m.Mod.ReadmePath.Length > 0).OrderBy(m => m.Mod.Name, StringComparer.Ordinal).Select(m => m.Entry).FirstOrDefault();
            if (withReadme == null)
            {
                p.Note("no mod has a readme");
                yield break;
            }
            var text = ScryPanel.ReadmeOf((ModSource)withReadme.Source);
            p.Note($"{withReadme.Name}'s readme: {text.Length} characters, starting \"{text.Substring(0, Math.Min(60, text.Length)).Replace('\n', ' ')}\"");
            p.Check(text.Length > 0 && text.IndexOf("](", StringComparison.Ordinal) < 0 && text.IndexOf("<img", StringComparison.OrdinalIgnoreCase) < 0, "its readme reads as plain text");
            Select(withReadme);
            ScryPanel.ReadmeFolded = false;
            var drawn = ScryPanel.ReadmesDrawn;
            yield return Until(() => ScryPanel.ReadmesDrawn > drawn, 3);
            p.Check(ScryPanel.ReadmesDrawn > drawn, "its page shows it under README");
        }

        /// <summary>
        /// Which mod added what: how many things each clue named (Jotunn's registry, a mod's
        /// scripts, its bundles), what is left unnamed kind by kind, that nothing the game has of
        /// its own is put down to a mod, and that a recipe or conversion another mod added for an
        /// item says so on the item.
        /// </summary>
        private static IEnumerator ModClues(Probe p)
        {
            p.Note("named by " + (Knowledge.NamedBy.Count == 0 ? "nothing" : string.Join(", ", Knowledge.NamedBy.OrderByDescending(n => n.Value).Select(n => $"{n.Key}: {n.Value}")))
                   + $"; recipes named {Knowledge.RecipesNamed}, conversions named {Knowledge.ConversionsNamed}");
            var unnamed = X.Catalog.Where(e => e.Origin == Origin.Mod && e.ModName.Length == 0 && e.Kind != Kind.Mod).ToList();
            foreach (var kind in unnamed.GroupBy(e => e.Kind).OrderByDescending(g => g.Count()))
            {
                p.Note($"still unnamed, {Kinds.Label(kind.Key).ToLowerInvariant()}: {kind.Count()} ({string.Join(", ", kind.Take(8).Select(e => e.Name))})");
            }
            var wrong = X.Catalog.Where(e => e.Origin == Origin.Vanilla && e.ModName.Length > 0 && e.Kind != Kind.Mod).Select(e => $"{e.Name} ({e.ModName})").ToList();
            p.Check(wrong.Count == 0, "nothing of the game's own is put down to a mod", string.Join(", ", wrong.Take(8)));
            var named = X.Catalog.Where(e => e.Kind == Kind.Location && e.ModName.Length > 0).Select(e => $"{e.Name} ({e.ModName})").ToList();
            if (named.Count > 0) p.Note($"locations named for their mod: {string.Join(", ", named.Take(8))}");

            // A recipe another mod added for an item, told on the item.
            var db = ObjectDB.instance;
            var recipe = db == null ? null : db.m_recipes.FirstOrDefault(r => r != null && r.m_enabled && r.m_item != null && Knowledge.RecipeMod(r.name).Length > 0
                                                                               && Knowledge.RecipeMod(r.name) != Knowledge.ModName(r.m_item.gameObject.name));
            if (recipe == null)
            {
                p.Note("no mod adds a recipe for another's item through Jotunn");
                yield break;
            }
            var item = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == recipe.m_item.gameObject.name);
            if (item == null) yield break;
            var told = Facts.For(item);
            p.Check(told.Rows.Any(r => r.Title.EndsWith(", added by " + Knowledge.RecipeMod(recipe.name), StringComparison.Ordinal)), $"{item.Name} says {Knowledge.RecipeMod(recipe.name)} added its recipe",
                string.Join("; ", told.Rows.Select(r => r.Title)));
        }

        /// <summary>
        /// For each kind of gear fact, the first item that has it shows it: armour's resistances
        /// while worn, a shield's while blocking, what gear changes while worn (heat, stamina,
        /// eitr, adrenaline), what full adrenaline gives, and a weapon's second attack.
        /// </summary>
        private static IEnumerator GearFacts(Probe p)
        {
            var items = X.Catalog.Where(e => e.Kind == Kind.Item && e.Source is GameObject).Select(e => (Entry: e, Shared: ((GameObject)e.Source).GetComponent<ItemDrop>()?.m_itemData?.m_shared))
                .Where(i => i.Shared != null).OrderBy(i => i.Entry.Name, StringComparer.Ordinal).ToList();
            bool Worn(ItemDrop.ItemData.ItemType t) => t == ItemDrop.ItemData.ItemType.Chest || t == ItemDrop.ItemData.ItemType.Legs || t == ItemDrop.ItemData.ItemType.Helmet || t == ItemDrop.ItemData.ItemType.Shoulder;
            var cases = new (string What, Func<ItemDrop.ItemData.SharedData, bool> Has, string Label)[]
            {
                ("armour resisting while worn", s => Worn(s.m_itemType) && s.m_damageModifiers.Any(m => m.m_modifier != HitData.DamageModifier.Normal), "Damage it takes while worn"),
                ("a shield resisting while blocking", s => s.m_itemType == ItemDrop.ItemData.ItemType.Shield && s.m_damageModifiers.Any(m => m.m_modifier != HitData.DamageModifier.Normal), "Damage it takes while blocking"),
                ("gear against heat", s => Math.Abs(s.m_heatResistanceModifier) >= 0.005f, "Heat resistance"),
                ("gear changing run stamina", s => Math.Abs(s.m_runStaminaModifier) >= 0.005f, "Run stamina"),
                ("gear changing eitr regeneration", s => Math.Abs(s.m_eitrRegenModifier) >= 0.005f, "Eitr regeneration"),
                ("gear adding adrenaline", s => s.m_maxAdrenaline >= 0.5f, "Most adrenaline"),
                ("gear giving something at full adrenaline", s => s.m_fullAdrenalineSE != null, "At full adrenaline"),
                ("a weapon with a second attack", s => s.m_secondaryAttack != null && !string.IsNullOrEmpty(s.m_secondaryAttack.m_attackAnimation) && s.m_attack != null
                    && (s.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon || s.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon), "Secondary attack"),
            };
            foreach (var (what, has, label) in cases)
            {
                var found = items.Where(i => has(i.Shared)).ToList();
                if (found.Count == 0)
                {
                    p.Note($"no {what} in this game");
                    continue;
                }
                var (entry, _) = found[0];
                var gearFacts = Facts.For(entry);
                var value = Value(gearFacts, label) ?? (gearFacts.Rows.Any(r => r.Title == label && r.Cells != null) ? string.Join(", ", gearFacts.Rows.First(r => r.Title == label).Cells.Where(c => c.Tone != Tone.Plain).Select(c => c.Type + " " + c.Value)) : null);
                p.Check(value != null, $"{entry.Name}, {what} ({found.Count} such), tells it under {label}", value ?? "not told");
                if (value != null) p.Note($"{entry.Name}: {label} {value}");
            }
            yield break;
        }

        /// <summary>
        /// Every creature that breeds tells how, and its young names it as where it comes from;
        /// every young one tells what it grows into; an egg tells what hatches from it and when;
        /// a creature with a saddle tells what it is ridden with and its stamina then.
        /// </summary>
        private static IEnumerator BreedingFacts(Probe p)
        {
            GameObject Of(Entry e) => e.Source as GameObject;
            var breeders = X.Catalog.Where(e => e.Kind == Kind.Creature && Of(e)?.GetComponent<Procreation>() != null && Of(e).GetComponent<Tameable>() != null).ToList();
            p.Note($"{breeders.Count} creatures breed: {string.Join(", ", breeders.Take(10).Select(e => e.Name))}");
            var untold = new List<string>();
            var unborn = new List<string>();
            foreach (var breeder in breeders)
            {
                var told = Facts.For(breeder);
                if (Value(told, "Breeds when") == null || Value(told, "Love") == null || !told.Rows.Any(r => r.Title.StartsWith("Has young", StringComparison.Ordinal))) untold.Add(breeder.Name);
                var young = Of(breeder).GetComponent<Procreation>().m_offspring;
                if (young == null) continue;
                var lines = Knowledge.WhereLines(young.name).Concat(Knowledge.SourceLines(young.name));
                if (!lines.Any(l => l.Prefab == breeder.Name && l.Text.StartsWith("Born to", StringComparison.Ordinal))) unborn.Add(young.name);
            }
            p.Check(untold.Count == 0, "every one tells how it breeds and its young", string.Join(", ", untold.Take(5)));
            p.Check(unborn.Count == 0, "every one's young names it as where it comes from", string.Join(", ", unborn.Take(5)));
            if (breeders.Count > 0) p.Note($"{breeders[0].Name}: {Pairs(Facts.For(breeders[0]))}");

            var growing = X.Catalog.Where(e => e.Kind == Kind.Creature && Of(e)?.GetComponent<Growup>() != null).ToList();
            var ungrown = growing.Where(e => Value(Facts.For(e), "Grows up in") == null || !Facts.For(e).Rows.Any(r => r.Title.StartsWith("Grows into", StringComparison.Ordinal))).Select(e => e.Name).ToList();
            p.Check(ungrown.Count == 0, $"every young one ({growing.Count}) tells what it grows into and when", string.Join(", ", ungrown.Take(5)));

            var egg = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && Of(e)?.GetComponent<EggGrow>()?.m_grownPrefab != null);
            if (egg == null) p.Note("no egg hatches in this game");
            else
            {
                var hatch = Of(egg).GetComponent<EggGrow>().m_grownPrefab.name;
                p.Check(Value(Facts.For(egg), "Hatches into") != null && Knowledge.WhereLines(hatch).Any(l => l.Prefab == egg.Name), $"{egg.Name} tells what hatches from it, and {hatch} that it hatches from it", Value(Facts.For(egg), "Hatches when") ?? "not told");
            }

            var ridden = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && Of(e)?.GetComponent<Tameable>()?.m_saddle != null);
            if (ridden == null) p.Note("no creature is ridden in this game");
            else p.Check(Value(Facts.For(ridden), "Stamina when ridden") != null, $"{ridden.Name} tells its stamina when ridden", Pairs(Facts.For(ridden)));
            yield break;
        }

        /// <summary>
        /// A fish names the baits it bites on and a bait the fish it catches; a locked door its
        /// key and the key the door; a boss its Forsaken power and its trophy the power it gives
        /// on its boss stone.
        /// </summary>
        private static IEnumerator BaitsKeysPowers(Probe p)
        {
            GameObject Of(Entry e) => e.Source as GameObject;
            var fish = X.Catalog.Where(e => Of(e)?.GetComponent<Fish>()?.m_baits?.Any(b => b?.m_bait != null) == true).ToList();
            p.Note($"{fish.Count} fish with baits");
            if (fish.Count > 0)
            {
                var one = fish[0];
                p.Check(Facts.For(one).Rows.Any(r => r.Title.StartsWith("Bites on", StringComparison.Ordinal)), $"{one.Name} names its baits");
                var bait = Of(one).GetComponent<Fish>().m_baits.First(b => b?.m_bait != null).m_bait.gameObject.name;
                var baitEntry = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == bait);
                if (baitEntry != null) p.Check(Facts.For(baitEntry).Rows.Any(r => r.Title.StartsWith("Catches", StringComparison.Ordinal) && r.Items.Any(i => i.Prefab == one.Name)), $"{bait} names {one.Name} among what it catches");
            }

            var doors = X.Catalog.Where(e => Of(e)?.GetComponent<Door>()?.m_keyItem != null).ToList();
            p.Note($"{doors.Count} locked doors: {string.Join(", ", doors.Take(6).Select(e => e.Name))}");
            if (doors.Count > 0)
            {
                var door = doors[0];
                var key = Of(door).GetComponent<Door>().m_keyItem.gameObject.name;
                p.Check(Value(Facts.For(door), "Opened with") != null, $"{door.Name} names its key");
                var keyEntry = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == key);
                if (keyEntry != null) p.Check(Facts.For(keyEntry).Rows.Any(r => r.Title == "Opens" && r.Items.Any(i => i.Prefab == door.Name)), $"{key} names {door.Name} among what it opens");
            }

            var bosses = X.Catalog.Where(e => e.Kind == Kind.Creature && Of(e)?.GetComponent<CharacterDrop>()?.m_drops?.Any(d => d?.m_prefab != null && Knowledge.PowerOf(d.m_prefab.name).Power != null) == true).ToList();
            p.Note($"{bosses.Count} creatures drop a trophy with a Forsaken power: {string.Join(", ", bosses.Take(8).Select(e => e.Name))}");
            var unpowered = bosses.Where(b => Value(Facts.For(b), "Forsaken power") == null).Select(b => b.Name).ToList();
            p.Check(bosses.Count > 0 && unpowered.Count == 0, "every one names its power", string.Join(", ", unpowered));
            var trophy = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && Knowledge.PowerOf(e.Name).Power != null);
            if (trophy != null) p.Check(Value(Facts.For(trophy), "On its boss stone") != null, $"{trophy.Name} names the power it gives on its boss stone");
            yield break;
        }

        /// <summary>
        /// The rows a player looks for show on every page of a type, with "none" or "no" where
        /// that is the answer: a creature's attacks, weak spots, taming and drops; gear's quality,
        /// portals and wear; armour's armour, movement and set; a weapon's block and second
        /// attack; a buildable piece's cost.
        /// </summary>
        private static IEnumerator StandardRows(Probe p)
        {
            GameObject Of(Entry e) => e.Source as GameObject;
            // A label may name alternatives, "Build cost|Built near", and one ending in a space or
            // colon is the start of labels ("Hit on the "): any one of them will do.
            bool Has(Facts told, string labels) => labels.Split('|').Any(label =>
                told.Pairs.Any(pair => pair.Key == label || ((label.EndsWith(" ") || label.EndsWith(":")) && pair.Key.StartsWith(label, StringComparison.Ordinal)))
                || told.Rows.Any(r => r.Title.StartsWith(label, StringComparison.Ordinal)));
            // Each kind's entries told a few a frame, so the check makes no long frame of its own.
            var checks = new List<(string What, Entry Entry, string[] Labels)>();
            var groups = new List<(string What, int Count, string[] Labels)>();
            void Every(string what, IEnumerable<Entry> entries, params string[] labels)
            {
                var list = entries.ToList();
                groups.Add((what, list.Count, labels));
                foreach (var entry in list) checks.Add((what, entry, labels));
            }

            var creatures = X.Catalog.Where(e => e.Kind == Kind.Creature && Of(e)?.GetComponent<Character>() is Character c && !(c is Player));
            Every("creature", creatures, "Health", "Attacks|Attack: ", "Weak spots|Hit on the ", "Tameable", "Drops");
            var shared = X.Catalog.Where(e => e.Kind == Kind.Item && Of(e)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared != null)
                .Select(e => (Entry: e, Type: Of(e).GetComponent<ItemDrop>().m_itemData.m_shared.m_itemType)).ToList();
            bool Is(ItemDrop.ItemData.ItemType t, params ItemDrop.ItemData.ItemType[] types) => types.Contains(t);
            Every("item", shared.Select(s => s.Entry), "Type", "Weight", "Portals");
            Every("piece of armour", shared.Where(s => Is(s.Type, ItemDrop.ItemData.ItemType.Helmet, ItemDrop.ItemData.ItemType.Chest, ItemDrop.ItemData.ItemType.Legs, ItemDrop.ItemData.ItemType.Shoulder)).Select(s => s.Entry),
                "Quality", "Durability", "Armour", "Movement", "Set bonus");
            Every("weapon", shared.Where(s => Is(s.Type, ItemDrop.ItemData.ItemType.OneHandedWeapon, ItemDrop.ItemData.ItemType.TwoHandedWeapon, ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft, ItemDrop.ItemData.ItemType.Bow)).Select(s => s.Entry),
                "Quality", "Durability", "Block", "Secondary attack");
            var built = X.Catalog.Where(e => e.Kind == Kind.Piece && Of(e)?.GetComponent<Piece>()?.enabled == true && Knowledge.Tools.ToolsOf(e.Name).Count > 0);
            Every("buildable piece", built, "Build cost|Built near", "Built with");

            var missing = new Dictionary<string, List<string>>();
            yield return Budgeted(checks, check =>
            {
                var told = Facts.For(check.Entry);
                foreach (var label in check.Labels)
                {
                    if (Has(told, label)) continue;
                    if (!missing.TryGetValue(check.What, out var list)) missing[check.What] = list = new List<string>();
                    list.Add($"{check.Entry.Name} {label}");
                }
            }, 8);
            foreach (var (what, count, labels) in groups)
            {
                var gaps = missing.TryGetValue(what, out var list) ? list : new List<string>();
                p.Check(gaps.Count == 0, $"every {what} ({count}) shows {string.Join(", ", labels)}", string.Join("; ", gaps.Take(6)));
            }
        }

        /// <summary>
        /// Every creature, every piece that can be damaged and every rock or tree tells its
        /// resistances as the same grid of ten damage types; one drawn on the page counts.
        /// </summary>
        private static IEnumerator ResistanceGrids(Probe p)
        {
            GameObject Of(Entry e) => e.Source as GameObject;
            bool HasGrid(Entry e) => Facts.For(e).Rows.Any(r => r.Cells != null && r.Cells.Count == ResistWords.Types.Length);
            var creatures = X.Catalog.Where(e => e.Kind == Kind.Creature && Of(e)?.GetComponent<Character>() != null && !(Of(e).GetComponent<Character>() is Player)).ToList();
            var gridless = new List<string>();
            yield return Budgeted(creatures, e => { if (!HasGrid(e)) gridless.Add(e.Name); }, 8);
            p.Check(creatures.Count > 0 && gridless.Count == 0, $"every creature ({creatures.Count}) has the grid", string.Join(", ", gridless.Take(5)));
            var pieces = Spread(X.Catalog.Where(e => e.Kind == Kind.Piece && Of(e)?.GetComponent<WearNTear>() != null && Of(e).GetComponent<Piece>()?.enabled == true).OrderBy(e => e.Name, StringComparer.Ordinal).ToList(), 40);
            var pieceless = pieces.Where(e => !HasGrid(e)).Select(e => e.Name).ToList();
            p.Check(pieceless.Count == 0, $"a spread of {pieces.Count} pieces each have it", string.Join(", ", pieceless.Take(5)));
            var rocks = Spread(X.Catalog.Where(e => e.Kind == Kind.Resource && (Of(e)?.GetComponent<Destructible>() != null || Of(e)?.GetComponent<MineRock5>() != null)).OrderBy(e => e.Name, StringComparer.Ordinal).ToList(), 20);
            p.Note($"{rocks.Count(HasGrid)} of a spread of {rocks.Count} rocks and trees have it");

            var shown = creatures.FirstOrDefault(e => e.Name == "Troll") ?? creatures.FirstOrDefault();
            if (shown == null) yield break;
            var cells = Facts.For(shown).Rows.First(r => r.Cells != null).Cells;
            p.Note($"{shown.Name}: {string.Join(", ", cells.Select(c => $"{c.Type} {c.Value}"))}");
            Select(shown);
            var drawn = ScryPanel.GridsDrawn;
            yield return Until(() => ScryPanel.GridsDrawn > drawn, 3);
            p.Check(ScryPanel.GridsDrawn > drawn, $"{shown.Name}'s page draws its grid");
        }

        /// <summary>
        /// A creature's health and drops are sure, a mods' note on them is not; a mod's things
        /// matched by clues are told apart from those its registry names; a line where Scry found
        /// nothing is marked; every mark has its reason.
        /// </summary>
        private static IEnumerator UnsureMarks(Probe p)
        {
            var creature = Pick(Kind.Creature, "Greydwarf", "Boar") ?? X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature);
            if (creature == null) p.Skip("there is no creature");
            var told = Facts.For(creature);
            p.Check(!told.Unsure.ContainsKey("Health") && told.Pairs.Any(pair => pair.Key == "Health"), $"{creature.Name}'s health is told as sure");
            if (ModHooks.Mods(HookedRule.Drops).Count > 0)
            {
                p.Check(ModHookWords.Line(told.Hooks) != null && HookNote(told, HookedRule.Drops) != null, $"{creature.Name} tells in one line after the rest that mods hook into its drops", ModHookWords.Line(told.Hooks) ?? "no line");
            }
            p.Check(told.Unsure.Values.All(why => !string.IsNullOrEmpty(why)), "every mark says why");

            var named = X.Catalog.Where(e => e.ModName.Length > 0 && e.Kind != Kind.Mod).ToList();
            var guessed = named.Where(e => !UnsureWords.IsSureClue(e.ModClue)).ToList();
            p.Note($"{named.Count} entries named for their mod: {named.Count - guessed.Count} by the mod's own word, {guessed.Count} by clues ({string.Join(", ", guessed.GroupBy(e => e.ModClue).Select(g => $"{g.Key}: {g.Count()}"))})");
            if (guessed.Count > 0)
            {
                var one = guessed[0];
                var page = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Mod && e.Name == one.ModName);
                if (page != null) p.Check(Facts.For(page).Rows.Any(r => r.Unsure != null && r.Items.Any(i => i.Prefab == one.Key)), $"{one.Name} is under its mod's clue-matched row, marked");
            }

            var nowhere = X.Catalog.FirstOrDefault(e => (e.Kind == Kind.Item || e.Kind == Kind.Creature) && Facts.For(e).Where.Any(l => l.Unsure == UnsureWords.NothingFound || l.Unsure == UnsureWords.NowhereFound));
            p.Note(nowhere == null ? "no item or creature without a source" : $"{nowhere.Name} says where Scry found nothing, marked");
            yield break;
        }

        /// <summary>
        /// Every biome is an entry whose page tells its weathers and what lives there; every biome
        /// an entry names has a page to go to; a biome's music plays and stops.
        /// </summary>
        /// <summary>
        /// What the world's ground is drawn with (Heightmap.m_material): its shader and every
        /// texture it takes, by name, for laying a biome's ground under the stage.
        /// </summary>
        private static IEnumerator TerrainMaterial(Probe p)
        {
            var ground = Player.m_localPlayer != null ? Heightmap.FindHeightmap(Player.m_localPlayer.transform.position) : null;
            if (ground == null) ground = UnityEngine.Object.FindAnyObjectByType<Heightmap>();
            if (!p.Check(ground != null && ground.m_material != null, "the world's ground has a material")) yield break;
            var material = ground.m_material;
            var textures = material.GetTexturePropertyNames()
                .Select(name => { var texture = material.GetTexture(name); return $"{name}: {(texture != null ? $"{texture.name} ({texture.GetType().Name}, {texture.width}x{texture.height})" : "none")}"; });
            p.Note($"shader {(material.shader != null ? material.shader.name : "none")}; textures {string.Join("; ", textures)}");
            yield break;
        }

        /// <summary>A picture saved as an uncompressed TGA, true colour of 32 bits, its rows from the bottom up as read.</summary>
        private static void WriteTga(string path, int width, int height, Color32[] pixels)
        {
            using (var file = new System.IO.BinaryWriter(System.IO.File.Create(path)))
            {
                file.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                file.Write((ushort)width);
                file.Write((ushort)height);
                file.Write((byte)32);
                file.Write((byte)8);
                foreach (var pixel in pixels) file.Write(new[] { pixel.b, pixel.g, pixel.r, pixel.a });
            }
        }

        private static IEnumerator BiomePages(Probe p)
        {
            var biomes = X.Catalog.Where(e => e.Kind == Kind.Biome).ToList();
            p.Note($"{biomes.Count} biomes: {string.Join(", ", biomes.Select(b => b.DisplayName))}");
            var bare = biomes.Where(b => !Facts.For(b).Pairs.Any(pair => pair.Key.EndsWith("of the time", StringComparison.Ordinal))).Select(b => b.Name).ToList();
            p.Check(biomes.Count > 0 && bare.Count == 0, "each tells its weathers", string.Join(", ", bare));
            var empty = biomes.Where(b => b.Name != "Ocean" && !Facts.For(b).Rows.Any(r => r.Title.StartsWith("Lives here", StringComparison.Ordinal))).Select(b => b.Name).ToList();
            p.Note(empty.Count == 0 ? "each but the ocean tells what lives there" : $"no creature told living in {string.Join(", ", empty)}");
            // What spans most biomes, and what of other kinds claims a biome at all, kind by kind, for a look at what is mixed in.
            var wide = X.Catalog.Where(e => e.Kind != Kind.Biome && e.Biomes.Length >= 6).ToList();
            foreach (var kind in wide.GroupBy(e => e.Kind)) p.Note($"{Kinds.Label(kind.Key).ToLowerInvariant()} in 6 or more biomes ({kind.Count()}): {string.Join(", ", kind.Take(12).Select(e => $"{e.Name} ({e.Biomes.Length}{(e.ModName.Length > 0 ? ", " + e.ModName : "")})"))}");
            var others = X.Catalog.Where(e => e.Biomes.Length > 0 && e.Kind != Kind.Biome && e.Kind != Kind.Creature && e.Kind != Kind.Resource && e.Kind != Kind.Location && e.Kind != Kind.Raid).ToList();
            foreach (var kind in others.GroupBy(e => e.Kind)) p.Note($"{Kinds.Label(kind.Key).ToLowerInvariant()} with biomes ({kind.Count()}): {string.Join(", ", kind.Take(12).Select(e => $"{e.Name} ({string.Join("/", e.Biomes)})"))}");
            var named = X.Catalog.Where(e => e.Kind != Kind.Biome).SelectMany(e => e.Biomes).Distinct().ToList();
            var pageless = named.Where(b => !X.Catalog.Any(e => e.Key == EntryKeys.For(Kind.Biome, b))).ToList();
            p.Check(pageless.Count == 0, "every biome an entry names has a page", string.Join(", ", pageless));
            if (biomes.Count > 0) p.Note($"{biomes[0].Name}: {Pairs(Facts.For(biomes[0]))}");

            var withMusic = biomes.FirstOrDefault(b => b.Source is BiomeSource s && BiomeWords.Music(s.Morning, s.Evening, s.Day, s.Night).Count > 0);
            if (withMusic == null)
            {
                p.Note("no biome has music of its own");
                yield break;
            }
            var said = Previews.PlacesMusic(withMusic);
            p.Check(said != null && said.StartsWith("Playing", StringComparison.Ordinal) && MusicPreview.PlayingFor == withMusic, $"{withMusic.Name}'s music plays", said);
            yield return null;
            said = Previews.PlacesMusic(withMusic);
            p.Check(MusicPreview.PlayingFor != withMusic, $"{withMusic.Name}'s music stops when asked again", said);
        }

        /// <summary>
        /// Every creature whose defeat sets a world key that something waits for tells it, and
        /// every raid the game's own list waits on that key for is among its raids.
        /// </summary>
        private static IEnumerator AfterDefeat(Probe p)
        {
            var keyed = X.Catalog.Where(e => e.Kind == Kind.Creature && e.Source is GameObject g && g.GetComponent<Character>() is Character c && !string.IsNullOrEmpty(c.m_defeatSetGlobalKey))
                .Select(e => (Entry: e, Key: ((GameObject)e.Source).GetComponent<Character>().m_defeatSetGlobalKey)).ToList();
            p.Note($"{keyed.Count} creatures set a world key when they fall: {string.Join(", ", keyed.Take(12).Select(k => $"{k.Entry.Name} ({k.Key})"))}");
            var silent = keyed.Where(k => Knowledge.Unlocks.Any(k.Key) && !Facts.For(k.Entry).Rows.Any(r => r.Title.StartsWith("After it falls", StringComparison.Ordinal))).Select(k => k.Entry.Name).ToList();
            p.Check(silent.Count == 0, "each one something waits for tells what follows", string.Join(", ", silent.Take(5)));
            foreach (var (entry, key) in keyed.Take(3))
            {
                p.Note($"{entry.Name}: " + string.Join("; ", Facts.For(entry).Rows.Where(r => r.Title.StartsWith("After it falls", StringComparison.Ordinal)).Select(r => $"{r.Title}: {string.Join(", ", r.Items.Take(6).Select(i => i.Name))}")));
            }

            var byPlayer = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.PlayerEvents);
            var missed = new List<string>();
            if (!byPlayer && RandEventSystem.instance != null)
            {
                foreach (var raid in RandEventSystem.instance.m_events)
                {
                    // A raid the game has switched off never comes, so nothing follows it.
                    if (raid?.m_requiredGlobalKeys == null || !raid.m_enabled || raid.m_spawn == null) continue;
                    foreach (var key in raid.m_requiredGlobalKeys)
                    {
                        if (!Knowledge.Unlocks.Of(key, Unlock.RaidStarts).Contains(EntryKeys.For(Kind.Raid, raid.m_name))) missed.Add($"{raid.m_name} ({key})");
                    }
                }
            }
            p.Check(missed.Count == 0, "every raid waiting on a key is told under it", string.Join(", ", missed.Take(5)));
            yield break;
        }

        /// <summary>Each kind of machine, the first of it in the catalog, tells what it does.</summary>
        private static IEnumerator MachineFacts(Probe p)
        {
            GameObject Of(Entry e) => e.Source as GameObject;
            var all = X.Catalog.Where(e => Of(e) != null).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            var cases = new (string What, Func<GameObject, bool> Has, string Label)[]
            {
                ("a ballista", g => g.GetComponent<Turret>() != null, "Shoots"),
                ("a trap", g => g.GetComponent<Trap>() != null, "Springs on"),
                ("a ship", g => g.GetComponent<Ship>() != null, "Ashlands seas"),
                ("a cart", g => g.GetComponent<Vagon>() != null, "Weighs"),
                ("a catapult", g => g.GetComponent<Catapult>() != null, "Loads"),
            };
            foreach (var (what, has, label) in cases)
            {
                var found = all.Where(e => has(Of(e))).ToList();
                if (found.Count == 0)
                {
                    p.Note($"no {what} in this game");
                    continue;
                }
                var told = Facts.For(found[0]);
                var value = Value(told, label);
                p.Check(value != null, $"{found[0].Name}, {what} ({found.Count} such), tells it under {label}", value ?? "not told");
                p.Note($"{found[0].Name}: {Pairs(told)}");
            }
            yield break;
        }

        /// <summary>For each building rule, the first piece having it tells it.</summary>
        private static IEnumerator BuildingRules(Probe p)
        {
            GameObject Of(Entry e) => e.Source as GameObject;
            var pieces = X.Catalog.Where(e => Of(e) != null && Of(e).GetComponent<Piece>() != null).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            var cases = new (string What, Func<GameObject, bool> Has, string Label)[]
            {
                ("a piece with placement rules", g => g.GetComponent<Piece>() is Piece piece && (piece.m_groundOnly || piece.m_cultivatedGroundOnly || piece.m_noInWater || piece.m_notOnWood || piece.m_onlyInBiome != 0), "Placed"),
                ("a station", g => g.GetComponent<CraftingStation>() != null, "Building reach"),
                ("a station's upgrade", g => g.GetComponent<StationExtension>()?.m_craftingStation != null, "Upgrades"),
                ("a bed", g => g.GetComponent<Bed>() != null, "Sleeping in it"),
                ("a warm piece", g => g.GetComponentsInChildren<EffectArea>(true).Any(a => (a.m_type & EffectArea.Type.Heat) != 0), "Warmth"),
                ("a base piece", g => g.GetComponentsInChildren<EffectArea>(true).Any(a => (a.m_type & EffectArea.Type.PlayerBase) != 0), "A base"),
            };
            foreach (var (what, has, label) in cases)
            {
                var found = pieces.Where(e => has(Of(e))).ToList();
                if (found.Count == 0)
                {
                    p.Note($"no {what} in this game");
                    continue;
                }
                var value = Value(Facts.For(found[0]), label);
                p.Check(value != null, $"{found[0].Name}, {what} ({found.Count} such), tells it under {label}", value ?? "not told");
                if (value != null) p.Note($"{found[0].Name}: {label} {value}");
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
            p.Check(Stage.IsStaged(raid), "it stands on the stage as the first roll of what it brings");

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

            // Its page tells what its rooms hold, the loot in them and what their spawn points place.
            var page = Facts.For(entry);
            var holds = page.Rows.Where(r => PlaceParts.Roles.Any(role => r.Title.StartsWith(PlaceParts.Title(role, true), StringComparison.Ordinal))).ToList();
            p.Check(holds.Sum(r => r.Items.Count) > 0, "its page tells what its rooms hold", holds.Count > 0 ? string.Join("; ", holds.Select(r => $"{r.Title} {r.Items.Count}")) : string.Join("; ", page.Rows.Select(r => r.Title)));
            var loot = page.Rows.FirstOrDefault(r => r.Title.StartsWith("Loot in its rooms", StringComparison.Ordinal));
            p.Note(loot != null ? $"loot in its rooms: {string.Join(", ", loot.Items.Take(12).Select(i => i.Prefab + " " + i.Amount))}" : "no loot in its rooms");
            if (algorithm == "Dungeon") p.Check(loot != null && loot.Items.Count > 0, "and the loot in them");

            // It is built on the stage, room by room: a dungeon inside, opened on its top floor,
            // with its entrance outside; a camp around the location's own parts.
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            yield return Until(() => Stage.ExampleRoomsTotal == example.Rooms.Count && Stage.ExampleRoomsShown == example.Rooms.Count, 30);
            p.Check(Stage.ExampleRoomsShown == example.Rooms.Count, "it is built on the stage, room by room", $"{Stage.ExampleRoomsShown} of {example.Rooms.Count}");
            yield return null;
            var size = Stage.SubjectSize;
            p.Note($"the stage shows it {size.x:0} × {size.z:0} m, {size.y:0} m high");
            if (algorithm == "Dungeon")
            {
                p.Check(Stage.HasInside && Stage.Inside, "it shows the dungeon inside");

                // Its row holds only View, Inside and Creatures; the roof is over the ruler.
                var chips = ScryPanel.StageChipsDrawn;
                var roofs = ScryPanel.RoofButtonsDrawn;
                var frames = Time.frameCount;
                yield return Until(() => Time.frameCount > frames + 1 && ScryPanel.RoofButtonsDrawn > roofs, 3);
                var perFrame = (ScryPanel.StageChipsDrawn - chips) / Mathf.Max(1, ScryPanel.RoofButtonsDrawn - roofs);
                p.Check(perFrame > 0 && perFrame <= 3 && ScryPanel.RoofButtonsDrawn > roofs, "its stage shows three chips at the most, the roof over its ruler", $"{perFrame} chips a frame");
                p.Check(Stage.Cutting, "opened on its top floor", Stage.CutLabel);
                var cuts = new List<float> { Stage.CutAt };
                while (Stage.CutLevel < Stage.FloorHeights.Count - 1 && cuts.Count < 40)
                {
                    Stage.StepCut(true);
                    cuts.Add(Stage.CutAt);
                }
                var down = cuts.Zip(cuts.Skip(1), (above, below) => below < above).All(lower => lower);
                p.Note($"{Stage.FloorHeights.Count} floors found in its rooms");
                p.Check(cuts.Count > 0 && down, "it steps down floor by floor", string.Join(", ", cuts.Select(c => c.ToString("0.0"))) + " m");
                if (Stage.FloorHeights.Count > 1)
                {
                    // On its lowest floor the rooms above are put away; with the roof on, all stand again.
                    yield return Until(() => Stage.ExampleRoomsAway > 0, 3);
                    var label = PlaceView.FloorLabel(Stage.CutLevel, Stage.FloorHeights.Count, Stage.ExampleRoomsOnFloor);
                    p.Check(Stage.ExampleRoomsAway > 0 && label != null, "on its lowest floor the rooms above are put away, the floor named by the ruler", $"{Stage.ExampleRoomsAway} rooms put away; {label ?? "no label"}");
                    Stage.OpenLevel(Stage.FloorHeights.Count);
                    yield return Until(() => Stage.ExampleRoomsAway == 0, 3);
                    p.Check(Stage.ExampleRoomsAway == 0, "with the roof on every room stands", $"{Stage.ExampleRoomsAway} still away");
                }
                Stage.OpenLevel(0);
                yield return Until(() => Stage.ExampleRoomsKept, 3);
                if (Stage.FloorHeights.Count > 1) p.Check(Stage.ExampleRoomsDimmed > 0, "on its top floor the rooms below stand dimmed", $"{Stage.ExampleRoomsDimmed} dimmed, {Stage.ExampleRoomsAway} put away");
                // A room of the top floor, which stands while it is opened.
                var top = Stage.ExampleShown?.Rooms.FindIndex(r => !r.Room.EndCap && !r.Room.Divider && Stage.ExampleRoomShown(r) == PlanRoomShown.Whole) ?? -1;
                var point = Stage.ExamplePointOf(Math.Max(0, top));
                p.Check(point.HasValue && Stage.ExampleRoomAt(point.Value) != null, "a room on the stage is found under the mouse", point?.ToString() ?? "not in view");
                Stage.Inside = false;
                yield return null;
                var outside = Stage.SubjectSize;
                p.Check(Mathf.Max(outside.x, outside.z) < Mathf.Max(size.x, size.z), "outside it shows its entrance", $"{outside.x:0} × {outside.z:0} m");
                Stage.Inside = true;
            }
            else
            {
                p.Check(!Stage.HasInside, "a camp has no inside to go into");
                p.Check(Mathf.Max(size.x, size.z) > 15f, "the camp stands around the location", $"{size.x:0} × {size.z:0} m");
            }
            Stage.Inside = wasInside;

            // Every room of it goes to an entry of its own.
            var keys = new HashSet<string>(X.Catalog.Select(e => e.Key));
            var missing = example.Rooms.Select(r => EntryKeys.For(Kind.Location, r.Room.Name)).Where(k => !keys.Contains(k)).Distinct().ToList();
            p.Check(missing.Count == 0, "every room on it has an entry to go to", string.Join(", ", missing.Take(5)));

            // Its plan in the stage's corner draws its rooms.
            ScryPanel.PlanFolded = false;
            // Folded, the plan leaves a tab that brings it back.
            var tabs = ScryPanel.PlanTabsDrawn;
            ScryPanel.PlanFolded = true;
            yield return Until(() => ScryPanel.PlanTabsDrawn > tabs, 3);
            p.Check(ScryPanel.PlanTabsDrawn > tabs, "folded, the plan leaves a tab to bring it back");
            ScryPanel.PlanFolded = false;
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

        private static int Broken(GameObject copy)
        {
            if (copy == null) return 0;
            var missing = copy.GetComponentsInChildren<MeshFilter>(true).Count(m => m.sharedMesh == null);
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true)) missing += renderer.sharedMaterials.Count(m => m == null);
            return missing;
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
            p.Note($"the self-test's own checks, left out of these figures, took up to {Frames.TestMax:0} ms in a frame");
            p.Check(Frames.Frames > 0, "Scry's own work was measured");
            p.Check(Frames.Max < 250, "no frame of Scry's own work took a quarter of a second", $"{Frames.Max:0} ms at the most");
            yield break;
        }
    }
}
