using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's scenarios, in the order they run: the check and the catalog, each kind
    /// shown and told, the previews played, raids, locations and their layouts, and last the
    /// reading of every location; and the check over the whole run. The cases themselves are in
    /// the files named for what they test (Search, Panel, Stage, Details, Mods, Previews, Raids,
    /// Locations, Layouts, Sweeps), and what they share in SelfTest.cs.
    /// </summary>
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
            yield return S("the View box and the sliders open, draw, and close on another selection", BoxesOpen, 10, ScryPanel.CloseBoxes);
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

        // ----- The whole run -----

        private static IEnumerator WholeRun(Probe p)
        {
            p.Note(Frames.Line(Budget));
            foreach (var frame in Frames.Slowest(5)) p.Note(FrameStats.Told(frame));
            p.Note($"the self-test's own checks, left out of these figures, took up to {Numbers.Amount(Frames.TestMax, 0)} ms in a frame");
            p.Check(Frames.Frames > 0, "Scry's own work was measured");
            p.Check(Frames.Max < 250, "no frame of Scry's own work took a quarter of a second", $"{Numbers.Amount(Frames.Max, 0)} ms at the most");
            yield break;
        }
    }
}
