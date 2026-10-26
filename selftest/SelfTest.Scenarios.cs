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
            yield return S("held looking around with the panel open, the cursor stays captured and hidden, and is freed once let go", CursorHeldWhileLooking, 10, () => ScryPanel.LookForTest(false));
            yield return S("the gear opens the settings card, a fast stage is filmed at half the size, and the View box's speed steps", SettingsCardShown, 20, SettingsAsTheyWere);
            yield return S("the compendium's box shows its guide's ?, and the guide opens in the list's place, giving way to the list asked for", GuideShown, 10, () => { ScryPanel.ShowGuide(false); ScryPanel.HoldCompendiumBox(false); });
            yield return S("a lasting file set aside is told under the header, naming what it was set aside as, until put away", SetAsideTold, 10, () => { while (SetAsideNotice.Files.Count > 0) SetAsideNotice.Files.PutAway(); });
            yield return S("the question of keeping a compendium draws, the book left as it was", WelcomeShown, 10, () => ScryPanel.AskWelcomeForTest(false));
            yield return S("an example of the search's help is tried with a click", HelpExamplesTried, 5, () => { ScryPanel.ShowHelp(false); X.SearchEverything(""); });
            yield return S("the View box and the sliders open, draw, and close on another selection", BoxesOpen, 10, ScryPanel.CloseBoxes);
            yield return S("the line under a page's title names its sections and goes to each", SectionLineGoes, 10);
            yield return S("each page's header stands the same in the full view and the compact one", HeaderLaidOut, 90, () => ScryPanel.Compact = _before?.Compact ?? ScryPanel.Compact);
            yield return S("the compact view's header is three rows, the kinds and where the list is taken from each one chip opening a box", CompactHeader, 30, () =>
            {
                ScryPanel.OpenHeaderMenuForTest(ScryPanel.HeaderMenu.None);
                ScryPanel.Compact = _before?.Compact ?? ScryPanel.Compact;
            });
            yield return S("in the compact view the notices share one strip, the most pressing first, its step going to the next", NoticesShareOneStrip, 20, NoticesAsTheyWere);
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
            yield return S("every skill has a page telling what it changes at your level, and reading them leaves your save alone", SkillPages, 30);
            yield return S("pages tell, faint under the game's figures, what they come to at your skills", SkillLines, 30);
            yield return S("with the compendium on, a skill not learned is named nowhere and nothing is told at it", SkillsUnlearned, 30);
            yield return S("a trader's page sells all its store does, and with the compendium on counts what waits for a boss not yet fallen", TraderPage, 30);
            yield return S("every trader stands facing the camera on the stage", TradersFaceCamera, 30);
            yield return S("the last round of polish shows in the game, a jump rises as its page tells, and what the next look over the words needs is gathered", PolishSweep, 60);
            yield return S("build tools and their pieces lead to each other", ToolsAndPieces, 10);
            yield return S("mods hooking into drops, loot and spawning are named", HookingMods, 10);
            yield return S("the mod report links every mod station as the game does", ModReportLinks, 20);
            yield return S("what creatures drop in play is watched", DropsWatched, 5);
            yield return S("what equipping something changes in play is watched", EquippingWatched, 5);
            yield return S("what the character knows is kept, caught up with its records and with what it carries", CompendiumKept, 30);
            yield return S("the compendium's rules are chosen in the panel, for the character played", CompendiumChosen, 10, () => { ScryPanel.HoldCompendiumBox(false); PutRuleBack(); AllKnown(); });
            yield return S("what a world shows as known is chosen source by source in the box, and what a source shows is marked", WorldSourcesShown, 15, () => { ScryPanel.HoldCompendiumBox(false); PutSourcesBack(); Session.ShowKnownInstead(null); });
            yield return S("the compendium's box stands each level at one place down its rows, every pin in one column, nothing clipped", CompendiumBoxColumns, 10, () => ScryPanel.HoldCompendiumBox(false));
            yield return S("what the character learns waits for the panel to open before it is told again", CompendiumWaitsForPanel, 10, AllKnown);
            yield return S("the compendium watches fights, hits and sight without fault", CompendiumWatched, 10);
            yield return S("made-up events through the real hooks are recorded, the world and the character's book left alone", CompendiumHooksFed, 10, () => CompendiumKeeper.RecordInto(null));
            yield return S("each way of meeting a place, a rune stone and a station's upgrades makes known what it should, and the strict rules show what went together", LearningPathsTaught, 10, () => CompendiumKeeper.RecordInto(null));
            yield return S("every hook acting on a game method's answer can see that answer come true", HookGatesCanOpen, 5);
            yield return S("events cross between the players' games through the game running the world, each counted once by the one rule", CompendiumEventsCross, 20, EventsCrossAsItWas);
            yield return S("the players whose games lack Scry are named in a notice, and everyone played with in the compendium's box", PlayingWithShown, 15, PlayingWithAsItWas);
            yield return S("the compendium's box names the world's server by whether it runs Scry, and says what one without it leaves out", ServerNamed, 25, ServerAsItWas);
            yield return S("what the world's server allows the compendium holds over the character's choices, kept beneath, the box shows it locked, and a notice says it once", WorldRulesShown, 20, WorldRulesAsTheyWere);
            yield return S("the character's walks are kept by where each dungeon lies, and the server, sent a list of them, wants just those it lacks", WalksToServer, 20);
            yield return S("the server reads what was left inside the world's dungeons, and the rooms of traces alone count among the rooms walked", TracesReadOnServer, 330);
            yield return S("asks about ground go by the player's own connection, and the server answers a few at once, the rest passed over", GroundAsksPaced, 30);
            yield return S("the server's game runs made-up events as a friend's would, choosing who gets them by the rules for every event", CompendiumEventsRunByWorld, 20, EventsCrossAsItWas);
            yield return S("this game sends its own events to the games they concern, the server's among them, and to no other", CompendiumEventsSent, 15);
            yield return S("a player rising again after a death stays played with, one without Scry is named after a wait, one gone is forgotten", PlayersComeAndGo, 25, PlayersAsTheyWere);
            yield return S("a made-up character's pages read under the strict rules as a player sees them", CompendiumStrictRead, 30, () => { AllKnown(); ResetList(); });
            yield return S("one page of each kind is pictured under the strict, the loose and no rules, from one made-up book", PagesAudited, 240, () => { X.LearnedOnly = false; AllKnown(); ResetList(); });
            yield return S("a made-up character's You box tells its moments, Learned goes by day and the notice tells what happened", YouAndLearnedRead, 20, () => { X.LearnedOnly = false; AllKnown(); ResetList(); });
            yield return S("a status effect's look stands on the person where the game puts it, plays again on a click, and its text keeps plain words", StatusLookRead, 30, () => { AllKnown(); ResetList(); });
            yield return S("under the loose rules a known page names only what the character has met, and the stage stands no creature not met", CompendiumLooseRead, 90, () => { AllKnown(); ResetList(); RoomCardAsItWas(); StageAsItWas(); });
            yield return S("what the game leaves unnamed reads as words, givers alike but for their biome go into one chip, and creatures' attacks say whose they are", ReadableNames, 20, ResetList);
            yield return S("with the compendium on the list holds the world a player meets, names alike say which each is, and no page offers a spawn command", CompendiumPlayerView, 30, () => { AllKnown(); ResetList(); });
            yield return S("with the compendium on, a made-up character sees only what it met", CompendiumShown, 60, () => { AllKnown(); ResetList(); });
            yield return S("a dungeon only seen from outside shows its outside alone", SeenFromOutside, 60, () => { AllKnown(); ResetList(); });
            yield return S("with a ground shown with insides, a dungeon's page names what its rooms walked on this server hold", InsidesFromWalks, 30, () => { AllKnown(); ResetList(); });
            yield return S("a dungeon's page is told again quickly, however many parts its rooms hold", DungeonPagesQuick, 20);
            yield return S("the explored map tells which kinds of place were seen", CompendiumMapRead, 30);
            yield return S("with the whole world counted, the server's census tells how many of each kind this world holds, on a place's page and in the Locations tab", WorldCensus, 40, () => { CensusAsItWas(); AllKnown(); ResetList(); });
            yield return S("each boss's fall opens its tier, worked out from how things are come by", CompendiumTierRead, 30, () => { AllKnown(); ResetList(); });
            yield return S("every mod has a page of what it adds and changes", ModPages, 10);
            yield return S("a mod's page tells what its package says and how it ties to other mods", ModPackages, 10);
            yield return S("an item a mod's settings are named after shows them", ModSettingsOfItems, 10);
            yield return S("an item's tooltip shows as the game builds it, with what mods add marked", TooltipsFromMods, 30);
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
            yield return S("a creature's page is laid out by topic, its fight first, every fact shown once", CreatureTopics, 30);
            yield return S("a status effect's page says how you get it, and Linked does not say it again", HowYouGetIt, 10);
            yield return S("the effects damage puts on you say what they do", DamageEffects, 10);
            yield return S("a set piece shows the set's other pieces under its set bonus", SetPieces, 10);
            yield return S("each crafting station links every upgrade the game counts for it, by the station's name", StationUpgradesLinked, 10);
            yield return S("a rock and a tree say what breaks them, by tier and by the damage they take", WhatBreaksIt, 10);
            yield return S("breeding, planting and summoning show whole on each page of them", ChainsShown, 10);
            yield return S("with the compendium on, a chain stands whole, a step not met reading as a dash", ChainsWithStepsNotMet, 15, () => Session.ShowKnownInstead(null));
            yield return S("with the compendium on, what hinges on a thing not met is left out whole: a Lox's riding and saddled look before its saddle", HingedOnAThingNotMet, 45, () => Session.ShowKnownInstead(null));
            yield return S("every creature, piece of gear and buildable piece shows its standard rows, saying none where it has none", StandardRows, 60);
            yield return S("a spawner tells its pool and pace, and its creatures their share", SpawnerFacts, 10);
            yield return S("items tell their odds in the tables that give them", LootOdds, 10);
            yield return S("items, stations, smelters and beds tell what they are for", WhatThingsTell, 10);
            foreach (var each in StageAndPlaceParts()) yield return each;
        }

        /// <summary>The parts on the stage, the world and the places, in their turn after the pages'.</summary>
        private static IEnumerable<Scenario> StageAndPlaceParts()
        {
            yield return S("the stage's lighting, backdrops and views all draw", StageControls, 20);
            yield return S("a bigger size makes a bigger copy", SizeAdjusts, 10);
            yield return S("a creature stands with stars and in each look", StarsAndLooks, 30, () => X.Modifiers.Reset());
            yield return S("a slower animation slows it, and a piece stands in every wear", SpeedAndWear, 30, () => X.Modifiers.Reset());
            yield return S("loudness follows, and a new selection starts afresh", AdjustmentsStartAfresh, 10, () => X.Modifiers.Reset());
            yield return S("a spread of every kind stands on the stage", KindsOnStage, 320, bearsSkips: true);
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
            yield return S("a weapon's attacks play from its table, put on the person first", WeaponAttacksPlay, 40, () =>
            {
                Looks.OnPerson = _before?.OnPerson ?? false;
                Previews.StopClip();
            });
            yield return S("a greatsword's, an axe's and a knife's first attack begins with the combo's first step", CombosStartWithTheirFirstStep, 70,
                () => Looks.OnPerson = _before?.OnPerson ?? false);
            yield return S("a death that hurts is an attack, an attack without animation says so, and clips nothing starts are left out", DeathAttacksAndUnplayedClips, 160);
            yield return S("a person lists the clips of what it holds, the rest found by a filter", PersonClipsByWhatIsHeld, 260,
                () => Looks.OnPerson = _before?.OnPerson ?? false);
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
            yield return S("the places looked at for their floors open each floor, cut under its ceiling, pictured", FloorsPictured, 300, StageAsItWas, bearsSkips: true);
            yield return S("a dungeon lays out an example, drawn, its rooms going to their entries", DungeonExample, 200, bearsSkips: true);
            yield return S("a camp lays out an example, drawn", CampExample, 150, StageAsItWas, bearsSkips: true);
            yield return S("the dungeon the player is in stands as saved, tagged, the menu switching to an example and back", ThisDungeon, 200, () => { ThisDungeonAsItWas(); StageAsItWas(); }, bearsSkips: true);
            yield return S("with the compendium on, the dungeon the player is in shows as walked, where they stand marked, peek showing the rest", DungeonFogShows, 240, () => { DungeonFogAsItWas(); StageAsItWas(); }, bearsSkips: true);
            yield return S("a dungeon's maps kept are on its page, open from anywhere and are forgotten", MapsKept, 200, () => { DungeonFogAsItWas(); StageAsItWas(); }, bearsSkips: true);
            yield return S("Valheim's large map marks the dungeons been in, a click opening the map in Scry", MarksOnTheMap, 200, () => { MarksAsTheyWere(); StageAsItWas(); }, bearsSkips: true);
            yield return S("a ground's pin marks every dungeon lying in it on Valheim's large map, hollow where walked on this server and shown with insides, and With insides is dimmed where nothing has insides", DungeonPins, 240, () => { PinsAsTheyWere(); StageAsItWas(); });
            yield return S("Hildir's Sealed Tower is a place stood in, its visits kept, no dungeon lying inside", PlacesStoodIn, 10);
            yield return S("a creature's stars follow what in the world can give it a level", StarsFollowSources, 300);
            yield return S("a fish's page tells where it lives, when it comes, how it bites and fights, and keeps that back until one is caught", FishPageTells, 30);
            yield return S("a kind's list groups another way: creatures by taming, items by biome", ListsGroupOtherWays, 30);
            yield return S("with the compendium on, a family is one row and one page switching among its kinds met, its copies found by their prefabs", FamiliesFold, 60, StageAsItWas);
            yield return S("the server's record of a dungeon opens by the floor, showing what anyone on the server walked, peek the rest", GroupRecords, 240, () => { GroupRecordsAsTheyWere(); StageAsItWas(); }, bearsSkips: true);
            yield return S("the cave and tower dungeons open level by level with rooms on each", CaveFloors, 300, StageAsItWas, bearsSkips: true);
            yield return S("dimmed rooms show darker on the stage, and what the rooms are drawn with is told", DimShows, 300, () => { LookAsItWas(); StageAsItWas(); }, bearsSkips: true);
            yield return S("a dungeon's room is gone into with a click and come back out of", GoesIntoRooms, 300, () => { ComeOutOfRoom(); StageAsItWas(); }, bearsSkips: true);
            yield return S("a stack of rooms opens cut from the side, and a room is gone into so", SideCutShows, 180, () => { SideCutAsItWas(); StageAsItWas(); }, bearsSkips: true);
            yield return S("a room that climbs with no real floor is cut along its slope, a shaft flat over its top", SlopeCutShows, 240, () => { ComeOutOfRoom(); StageAsItWas(); }, bearsSkips: true);
            yield return S("a room gone into shows its card: where it is, what this copy rolled in it, its ways on and its levels", RoomCardShows, 240, () => { RoomCardAsItWas(); StageAsItWas(); }, bearsSkips: true);
            yield return S("the plan box shows a dungeon from above, as a map turned to any angle, and as a tree of its ways", PlanBoxShows, 360, () => { PlanBoxAsItWas(); StageAsItWas(); }, bearsSkips: true);
            yield return S("nothing drawn over the stage covers anything else, whatever its size and the plan box's", NothingOverlapsOnStage, 180, () => { OverlapsAsTheyWere(); StageAsItWas(); }, bearsSkips: true);
            yield return S("the whole of a dungeon opens by the plan's levels, each filmed in as many cuts as it needs", LevelsTried, 300, StageAsItWas, bearsSkips: true);
            yield return S("Shift and the wheel step through a dungeon's levels and rooms a notch at a time, the zoom kept from room to room", ScrollingThroughLevels, 240, () => { Stage.ComeOut(); StageAsItWas(); }, bearsSkips: true);
            yield return S("places' floors are kept and told again from what was kept, as their rays find them", FloorsKept, 300, KeptFloorsUnverified, bearsSkips: true);
            yield return S("a place whose kept floors are still being found waits for them over frames, none of them long", FloorsWaitedFor, 300, FloorFilesFreed, bearsSkips: true);
            yield return S("a place's creatures stand on the stage with it", PlaceCreatures, 60, bearsSkips: true);
            yield return S("the stage's camera looks at the floor opened, zooms toward the pointer and drags with it", CameraMoves, 90, LightsAsTheyWere, bearsSkips: true);
            yield return S("the world's lights stay off the stage", WorldLightsOff, 20);
            yield return S("the Ground backdrop lays each biome's own ground", GroundBackdrop, 120, bearsSkips: true);
            yield return S("a meadow's grass grows out to where its ground fades, and a big creature, a raid and a camp stand in it", GroundGrass, 120, GroundAsItWas);
            yield return S("a place's own modifiers dig its ground on the stage, its tar settled in it and its parts at rest", DugGround, 120, DugGroundAsItWas);
            yield return S("Scry costs next to nothing idling with the panel closed", IdleCost, 320);
            yield return S("the resource monitor shows what Scry costs and holds", ResourceMonitor, 10);
            yield return S("standing open on a page and on a dungeon, what Scry's own work costs and allocates a frame, by part", OpenCost, 240);
            yield return S("placement details tell the woods and lava a location keeps to", PlacementDetails, 20);
            // Reading every place again takes minutes: only a full run does it.
            yield return S("reading every location fills their details", ReadLocations, 450, bearsSkips: true, full: true);
            yield return S("what was read of the locations is kept and known at once the next time", KeptLocations, 90);
            yield return S("Look again forgets what was kept and reads every location afresh, told as never read meanwhile", ForgottenAndReadAfresh, 480, EveryKindListed, bearsSkips: true, full: true);
            yield return S("every dungeon and camp lays out", EveryDungeonLaysOut, 60, bearsSkips: true);
            yield return S("content lists come in their order, a place's parts by what they are", ContentOrders, 60, bearsSkips: true);
            yield return S("every dungeon heads its group, its rooms under it", EveryDungeonHeadsItsGroup, 10);
            yield return S("a spread of locations stands on the stage", LocationsOnStage, 400, bearsSkips: true);
            yield return S("a beacon's beam stays out of its fortress's frame, and a see-through body in its own", BeaconOutOfFrame, 60, bearsSkips: true);
            yield return S("every entry's details are told, every chip leading somewhere", EveryDetail, 300, bearsSkips: true);
            yield return S("a location plays its music on Enter and gives the game's back", PlaysMusic, 30);
            yield return S("every boss tells its altar, and every trader what it sells", AltarsAndTraders, 10);
            yield return S("every runestone's texts are told in words", EveryRunestone, 10);
            yield return S("every location with music of its own plays it, none of them built", EveryMusic, 300, () => { Previews.PlaceCopiesOffForTest = false; Previews.StopSound(); });
            yield return S("the largest locations are made over several frames", LargestLocations, 120);
            yield return S("a spread of rooms stands on its floor, opened", RoomsOnStage, 160);
            yield return S("closing the panel lets go of every bundle", ClosingLetsGo, 5);
            yield return S("a full run took each of the README's pictures clean, without the strip and the game's HUD", ReadmePictures, 5, full: true);

            yield return new Scenario("Scry's own work over the whole run", WholeRun) { Timeout = 5 };
        }

        // ----- The whole run -----

        private static IEnumerator WholeRun(Probe p)
        {
            p.Note(Frames.Line(Budget));
            var slowest = Frames.Slowest(5);
            var parts = Frames.PartsOfSlowest(5);
            for (var i = 0; i < slowest.Count; i++) p.Note(FrameStats.Told(slowest[i]) + (parts[i] != null ? "; " + parts[i] : ""));
            p.Note($"the self-test's own checks, left out of these figures, took up to {Numbers.Amount(Frames.TestMax, 0)} ms in a frame");
            p.Note($"at the end the scripts hold {Numbers.Count(System.GC.GetTotalMemory(false) >> 20)} MB, the facts of {Numbers.Count(Facts.Held)} pages kept");
            p.Check(Frames.Frames > 0, "Scry's own work was measured");
            p.Check(Frames.Max < 250, "no frame of Scry's own work took a quarter of a second", $"{Numbers.Amount(Frames.Max, 0)} ms at the most");
            yield break;
        }
    }
}
