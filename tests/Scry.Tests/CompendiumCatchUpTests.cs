using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class CompendiumCatchUpTests
    {
        // A character played before the compendium does not start from nothing: what the game
        // itself keeps of it (items had, recipes unlocked, kills, things picked, biomes entered)
        // is read into what it knows, and read again whenever those records grow.

        private static Entry E(Kind kind, string name, params string[] tokens) => new Entry { Kind = kind, Name = name, Tokens = tokens };

        private static (CompendiumCatchUp CatchUp, CompendiumBook Book) Fresh(params Entry[] entries) =>
            (new CompendiumCatchUp(new EntryCatalog(entries)), new CompendiumBook());

        [Fact]
        public void ACreatureTheCharacterHasKilledIsKnownAndEveryPrefabGoingByItsNameWithIt()
        {
            var skeleton = E(Kind.Creature, "Skeleton", "$enemy_skeleton");
            var noArcher = E(Kind.Creature, "Skeleton_NoArcher", "$enemy_skeleton");
            var troll = E(Kind.Creature, "Troll", "$enemy_troll");
            var (catchUp, book) = Fresh(skeleton, noArcher, troll);
            var records = new CompendiumRecords();
            records.Killed.Add("$enemy_skeleton");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Known, book.Level(skeleton.Key));
            Assert.Equal(KnownLevel.Known, book.Level(noArcher.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(troll.Key));
            // Killed by the character, so the strict rules show its health, found out as news.
            Assert.True(book.Proved(skeleton.Key, EntryKeys.Did(Deed.Killed)));
            Assert.True(book.Proved(noArcher.Key, EntryKeys.Did(Deed.Killed)));
            Assert.False(book.Proved(troll.Key, EntryKeys.Did(Deed.Killed)));
            Assert.Equal(EntryKeys.Did(Deed.Killed), book.FoundOutOf(skeleton.Key));
            // Read as a character loads, its kills are news by the creatures learned, not by their health as well.
            var (first, firstBook) = Fresh(skeleton, noArcher, troll);
            first.Fill(records, firstBook, firstLoad: true);
            Assert.True(firstBook.Proved(skeleton.Key, EntryKeys.Did(Deed.Killed)));
            Assert.Null(firstBook.FoundOutOf(skeleton.Key));
            Assert.Contains(skeleton.Key, firstBook.Lately);
        }

        [Fact]
        public void AKillCountedUnderANameSeveralCreaturesShareIsTheOnesLivingWhereTheCharacterWent()
        {
            // The game counts kills by the name a creature shows, and the frozen greydwarf, living
            // only in the Deep North, shows the greydwarf's: killing greydwarfs in the Black Forest
            // says nothing of it. Where none of them lives where the character went, a kill was
            // still one of them, and all count.
            var greydwarf = E(Kind.Creature, "Greydwarf", "$enemy_greydwarf");
            var frozen = E(Kind.Creature, "Greydwarf_Frozen", "$enemy_greydwarf");
            var shaman = E(Kind.Creature, "Greydwarf_Shaman", "$enemy_greydwarfshaman");
            var frozenShaman = E(Kind.Creature, "Greydwarf_Shaman_Frozen", "$enemy_greydwarfshaman");
            var forest = E(Kind.Biome, "BlackForest", "$biome_blackforest");
            forest.Biomes = new[] { "BlackForest" };
            greydwarf.Biomes = new[] { "Meadows", "BlackForest" };
            shaman.Biomes = new[] { "BlackForest" };
            frozen.Biomes = new[] { "DeepNorth" };
            frozenShaman.Biomes = new[] { "DeepNorth" };
            var (catchUp, book) = Fresh(greydwarf, frozen, shaman, frozenShaman, forest);
            var records = new CompendiumRecords();
            records.Killed.Add("$enemy_greydwarf");
            records.Killed.Add("$enemy_greydwarfshaman");
            records.Biomes.Add("$biome_blackforest");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Known, book.Level(greydwarf.Key));
            Assert.True(book.Proved(greydwarf.Key, EntryKeys.Did(Deed.Killed)));
            Assert.Equal(KnownLevel.Known, book.Level(shaman.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(frozen.Key));
            Assert.False(book.Proved(frozen.Key, EntryKeys.Did(Deed.Killed)));
            Assert.Equal(KnownLevel.Unknown, book.Level(frozenShaman.Key));

            // Only creatures tell where the kill was: a ragdoll going by the name, in the forest, does not.
            var ragdoll = E(Kind.Other, "Greydwarf_ragdoll", "$enemy_greydwarf");
            ragdoll.Biomes = new[] { "BlackForest" };
            var (withRagdoll, ragdollBook) = Fresh(frozen, ragdoll, forest);
            var forestKill = new CompendiumRecords();
            forestKill.Killed.Add("$enemy_greydwarf");
            forestKill.Biomes.Add("$biome_blackforest");
            withRagdoll.Fill(forestKill, ragdollBook, firstLoad: false);
            Assert.Equal(KnownLevel.Known, ragdollBook.Level(frozen.Key));

            // Before the character entered the Black Forest, a greydwarf killed was still one of them.
            var (before, beforeBook) = Fresh(greydwarf, frozen, forest);
            var early = new CompendiumRecords();
            early.Killed.Add("$enemy_greydwarf");
            before.Fill(early, beforeBook, firstLoad: false);
            Assert.Equal(KnownLevel.Known, beforeBook.Level(greydwarf.Key));
            Assert.Equal(KnownLevel.Known, beforeBook.Level(frozen.Key));
        }

        [Fact]
        public void AKillCountedUnderANameIsNeverOfACreatureNothingInTheWorldBringsAbout()
        {
            // The Elder, in the Black Forest, summons its roots; the game also holds the bosses'
            // aspects, which nothing in the game spawns, and the Elder's aspect summons roots going
            // by the same name. A kill of the roots is the Elder's, never the aspect's.
            var elder = E(Kind.Creature, "gd_king", "$enemy_gdking");
            elder.Biomes = new[] { "BlackForest" };
            var roots = E(Kind.Creature, "TentaRoot", "$enemy_tentaroot");
            var aspect = E(Kind.Creature, "Aspect_Elder", "$enemy_gdking");
            var aspectRoots = E(Kind.Creature, "Aspect_TentaRoot", "$enemy_tentaroot");
            elder.AddTermLink("spawns", roots);
            aspect.AddTermLink("spawns", aspectRoots);
            var records = new CompendiumRecords();
            records.Killed.Add("$enemy_tentaroot");
            records.Killed.Add("$enemy_gdking");

            var (catchUp, book) = Fresh(elder, roots, aspect, aspectRoots);
            catchUp.Fill(records, book, firstLoad: true);
            Assert.Equal(KnownLevel.Known, book.Level(roots.Key));
            Assert.Equal(KnownLevel.Known, book.Level(elder.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(aspectRoots.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(aspect.Key));

            // Counted before by the rule that took every one, the aspect's roots are taken back.
            var (again, older) = Fresh(elder, roots, aspect, aspectRoots);
            older.Raise(aspectRoots.Key, KnownLevel.Known);
            older.Prove(aspectRoots.Key, EntryKeys.Did(Deed.Killed));
            again.Fill(records, older, firstLoad: false);
            Assert.Equal(KnownLevel.Unknown, older.Level(aspectRoots.Key));
            Assert.False(older.Proved(aspectRoots.Key, EntryKeys.Did(Deed.Killed)));

            // Where nothing going by the name comes about in the world, a kill was still one of them.
            var (alone, aloneBook) = Fresh(aspect, aspectRoots);
            alone.Fill(records, aloneBook, firstLoad: true);
            Assert.Equal(KnownLevel.Known, aloneBook.Level(aspectRoots.Key));
        }

        [Fact]
        public void AKillOnceCountedForACreatureItNeverStoodForIsTakenBack()
        {
            // Read before kills counted under a shared name stood only for the creatures living
            // where the character went, a book holds the frozen greydwarf known and killed. It is
            // taken back, but not one the character met in play: fought (its way kept), or seen
            // in a biome after the records had made it known (paired with that biome).
            var killed = EntryKeys.Did(Deed.Killed);
            var greydwarf = E(Kind.Creature, "Greydwarf", "$enemy_greydwarf");
            var frozen = E(Kind.Creature, "Greydwarf_Frozen", "$enemy_greydwarf");
            var shaman = E(Kind.Creature, "Greydwarf_Shaman", "$enemy_greydwarfshaman");
            var frozenShaman = E(Kind.Creature, "Greydwarf_Shaman_Frozen", "$enemy_greydwarfshaman");
            var skeleton = E(Kind.Creature, "Skeleton", "$enemy_skeleton");
            var northSkeleton = E(Kind.Creature, "Skeleton_DeepNorth", "$enemy_skeleton");
            var hildirSkeleton = E(Kind.Creature, "Skeleton_Hildir", "$enemy_skeleton");
            var forest = E(Kind.Biome, "BlackForest", "$biome_blackforest");
            forest.Biomes = new[] { "BlackForest" };
            greydwarf.Biomes = shaman.Biomes = skeleton.Biomes = new[] { "BlackForest" };
            frozen.Biomes = frozenShaman.Biomes = northSkeleton.Biomes = new[] { "DeepNorth" };
            var (catchUp, book) = Fresh(greydwarf, frozen, shaman, frozenShaman, skeleton, northSkeleton, hildirSkeleton, forest);
            // Known some other way the records no longer tell, never counted a kill: not the kill's to take back.
            book.Raise(hildirSkeleton.Key, KnownLevel.Known);
            foreach (var creature in new[] { greydwarf, frozen, northSkeleton })
            {
                book.Raise(creature.Key, KnownLevel.Known);
                book.Prove(creature.Key, killed);
            }
            book.Raise(frozenShaman.Key, KnownLevel.Known, way: CameBy.Fought);
            book.Prove(frozenShaman.Key, killed);
            book.Witness(northSkeleton.Key, forest.Key);
            var records = new CompendiumRecords();
            records.Killed.Add("$enemy_greydwarf");
            records.Killed.Add("$enemy_greydwarfshaman");
            records.Killed.Add("$enemy_skeleton");
            records.Biomes.Add("$biome_blackforest");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Unknown, book.Level(frozen.Key));
            Assert.False(book.Proved(frozen.Key, killed));
            Assert.Equal(KnownLevel.Known, book.Level(greydwarf.Key));
            Assert.Equal(KnownLevel.Known, book.Level(frozenShaman.Key));
            Assert.True(book.Proved(frozenShaman.Key, killed));
            Assert.Equal(KnownLevel.Known, book.Level(northSkeleton.Key));
            Assert.Equal(KnownLevel.Known, book.Level(hildirSkeleton.Key));

            // Where none sharing the name lives where the character went, the kill was still one of them: none is taken back.
            var (early, earlyBook) = Fresh(greydwarf, frozen, forest);
            earlyBook.Raise(frozen.Key, KnownLevel.Known);
            earlyBook.Prove(frozen.Key, killed);
            var before = new CompendiumRecords();
            before.Killed.Add("$enemy_greydwarf");
            early.Fill(before, earlyBook, firstLoad: false);
            Assert.Equal(KnownLevel.Known, earlyBook.Level(frozen.Key));
        }

        [Fact]
        public void AnItemPickedUpCraftedEatenOrHeldIsKnownAndOneOnlyATraderOffersIsSeen()
        {
            // The game's known materials (Player.m_knownMaterial) hold everything ever in the
            // inventory, and also what a trader offers (Trader.DiscoverItems): so one only there
            // is known unless a trader sells it, as a sword taken out of a chest.
            var wood = E(Kind.Item, "Wood", "$item_wood");
            var club = E(Kind.Item, "Club", "$item_club");
            var berries = E(Kind.Item, "Raspberry", "$item_raspberries");
            var sword = E(Kind.Item, "SwordIron", "$item_sword_iron");
            var lantern = E(Kind.Item, "Lantern", "$item_lantern");
            var catalog = new EntryCatalog(new[] { wood, club, berries, sword, lantern });
            var catchUp = new CompendiumCatchUp(catalog, soldByTrader: name => name == "Lantern");
            var book = new CompendiumBook();
            var records = new CompendiumRecords();
            records.PickedUp.Add("$item_wood");
            records.Crafted.Add("$item_club");
            records.Eaten.Add("$item_raspberries");
            records.Materials.Add("$item_sword_iron");
            records.Materials.Add("$item_wood");
            records.Materials.Add("$item_lantern");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Known, book.Level(wood.Key));
            Assert.Equal(KnownLevel.Known, book.Level(club.Key));
            Assert.Equal(KnownLevel.Known, book.Level(berries.Key));
            Assert.Equal(KnownLevel.Known, book.Level(sword.Key));
            Assert.Equal(KnownLevel.Seen, book.Level(lantern.Key));
        }

        [Fact]
        public void WithoutKnowingWhatTradersSellAnItemOnlyHeldIsSeen()
        {
            var sword = E(Kind.Item, "SwordIron", "$item_sword_iron");
            var (catchUp, book) = Fresh(sword);
            var records = new CompendiumRecords();
            records.Materials.Add("$item_sword_iron");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Seen, book.Level(sword.Key));
        }

        [Fact]
        public void APieceUnlockedUsedOrPlacedIsKnownAndSoIsAnItemWhoseRecipeIsUnlocked()
        {
            var bench = E(Kind.Piece, "piece_workbench", "$piece_workbench");
            var forge = E(Kind.Piece, "forge", "$piece_forge");
            var wall = E(Kind.Piece, "wood_wall_half", "$piece_woodwallhalf");
            var bow = E(Kind.Item, "Bow", "$item_bow");
            var (catchUp, book) = Fresh(bench, forge, wall, bow);
            var records = new CompendiumRecords();
            records.Recipes.Add("$piece_workbench");
            records.Recipes.Add("$item_bow");
            records.Stations.Add("$piece_forge");
            records.Placed.Add("$piece_woodwallhalf");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Known, book.Level(bench.Key));
            Assert.Equal(KnownLevel.Known, book.Level(forge.Key));
            Assert.Equal(KnownLevel.Known, book.Level(wall.Key));
            // The crafting menu shows an unlocked recipe's item whole: its name, look, stats and what it takes.
            Assert.Equal(KnownLevel.Known, book.Level(bow.Key));
        }

        [Fact]
        public void AnEntryWithoutANameOfItsOwnIsPassedOver()
        {
            var odd = E(Kind.Item, "ModThing", null, "");
            var wood = E(Kind.Item, "Wood", "$item_wood");
            var (catchUp, book) = Fresh(odd, wood);
            var records = new CompendiumRecords();
            records.PickedUp.Add("");
            records.PickedUp.Add("$item_wood");

            Assert.Equal(1, catchUp.Fill(records, book, firstLoad: false));
            Assert.Equal(KnownLevel.Unknown, book.Level(odd.Key));
        }

        [Fact]
        public void ARecordOnlyCountsForTheKindsItIsAbout()
        {
            // A mod's creature and item sharing a name token: a kill names the creature only.
            var creature = E(Kind.Creature, "ModWolf", "$shared_name");
            var item = E(Kind.Item, "ModWolfPelt", "$shared_name");
            var (catchUp, book) = Fresh(creature, item);
            var records = new CompendiumRecords();
            records.Killed.Add("$shared_name");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Known, book.Level(creature.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(item.Key));
        }

        [Fact]
        public void ATrophyHeldIsKnownAndTheCreatureItFallsFromSeenAndPairedWithNothing()
        {
            // Play would only see the creature before a fight: the records make it seen, with no sighting kept, and pair nothing with it.
            // Not the chest it can also be found in, nor a creature it falls from that lives in
            // no biome the character entered: the game's own TheHive, placed nowhere in the world,
            // drops the drake's trophy too.
            var trophy = E(Kind.Item, "TrophyHatchling", "$item_trophy_hatchling");
            var drake = E(Kind.Creature, "Hatchling", "$enemy_drake");
            var hive = E(Kind.Creature, "TheHive", "$enemy_thehive");
            var chest = E(Kind.Piece, "TreasureChest_mountains", "$piece_chest");
            var mountain = E(Kind.Biome, "Mountain", "$biome_mountain");
            mountain.Biomes = new[] { "Mountain" };
            drake.Biomes = new[] { "Mountain" };
            trophy.AddTermLink("from", drake);
            trophy.AddTermLink("from", hive);
            trophy.AddTermLink("from", chest);
            var (catchUp, book) = Fresh(trophy, drake, hive, chest, mountain);
            book.Now = () => new Moment(3, "Mountain");
            var records = new CompendiumRecords();
            records.Trophies.Add("TrophyHatchling");
            records.Biomes.Add("$biome_mountain");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Known, book.Level(trophy.Key));
            Assert.Equal(KnownLevel.Seen, book.Level(drake.Key));
            Assert.Null(book.FirstSeen(drake.Key));
            Assert.Null(book.Came(drake.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(chest.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(hive.Key));
            Assert.False(book.Witnessed(trophy.Key, drake.Key));
            Assert.False(book.Witnessed(trophy.Key, hive.Key));
        }

        [Fact]
        public void WhatGivesAnItemTheRecordsKnowIsSeenWhenItIsAResource()
        {
            // Play would only see a tree before it was hit: the records make a giver seen, not known, and pair nothing with it.
            // Things picked are counted by the prefab of what they give.
            var berries = E(Kind.Item, "Raspberry", "$item_raspberries");
            var bush = E(Kind.Resource, "RaspberryBush", "$item_raspberries");
            var wood = E(Kind.Item, "Wood", "$item_wood");
            var beech = E(Kind.Resource, "Beech1", "$prop_beech");
            var greydwarf = E(Kind.Creature, "Greydwarf", "$enemy_greydwarf");
            var resin = E(Kind.Item, "Resin", "$item_resin");
            var fir = E(Kind.Resource, "FirTree", "$prop_fir");
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            meadows.Biomes = new[] { "Meadows" };
            bush.Biomes = new[] { "Meadows" };
            beech.Biomes = new[] { "Meadows" };
            fir.Biomes = new[] { "Meadows", "BlackForest" };
            berries.AddTermLink("from", bush);
            wood.AddTermLink("from", beech);
            wood.AddTermLink("from", greydwarf);
            resin.AddTermLink("from", fir);
            var (catchUp, book) = Fresh(berries, bush, wood, beech, greydwarf, resin, fir, meadows);
            book.Now = () => new Moment(3, "Meadows");
            var records = new CompendiumRecords();
            records.Biomes.Add("$biome_meadows");
            records.Picked.Add("Raspberry");
            records.Picked.Add("Greydwarf");
            records.PickedUp.Add("$item_wood");
            records.Materials.Add("$item_resin");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Known, book.Level(berries.Key));
            Assert.Equal(KnownLevel.Seen, book.Level(bush.Key));
            Assert.Equal(KnownLevel.Seen, book.Level(beech.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(greydwarf.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(fir.Key));
        }

        [Fact]
        public void WhatGivesAnItemIsSeenOnlyWhereItGrowsInABiomeTheCharacterEntered()
        {
            // Wood picked up in the Meadows does not make the Ashlands' logs and rocks known: the
            // game drops Wood from some 84 kinds of thing, across every biome.
            var wood = E(Kind.Item, "Wood", "$item_wood");
            var beech = E(Kind.Resource, "Beech1", "$prop_beech");
            var ashLog = E(Kind.Resource, "AshlandsTreeLogHalf1", "$prop_ashlog");
            var nowhere = E(Kind.Resource, "dvergrprops_wood_beam", "$prop_beam");
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            var ashlands = E(Kind.Biome, "AshLands", "$biome_ashlands");
            meadows.Biomes = new[] { "Meadows" };
            ashlands.Biomes = new[] { "AshLands" };
            beech.Biomes = new[] { "Meadows", "BlackForest" };
            ashLog.Biomes = new[] { "AshLands" };
            foreach (var giver in new[] { beech, ashLog, nowhere }) wood.AddTermLink("from", giver);
            var (catchUp, book) = Fresh(wood, beech, ashLog, nowhere, meadows, ashlands);
            var records = new CompendiumRecords();
            records.PickedUp.Add("$item_wood");
            records.Biomes.Add("$biome_meadows");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Seen, book.Level(beech.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(ashLog.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(nowhere.Key));
            // Nothing was seen to give it: no giver is paired with it.
            Assert.False(book.Witnessed(wood.Key, beech.Key));
            Assert.False(book.Witnessed(wood.Key, ashLog.Key));
            Assert.False(book.Witnessed(wood.Key, nowhere.Key));

            // Once the character enters the Ashlands, its logs count.
            records.Biomes.Add("$biome_ashlands");
            catchUp.Fill(records, book, firstLoad: false);
            Assert.Equal(KnownLevel.Seen, book.Level(ashLog.Key));
            Assert.False(book.Witnessed(wood.Key, ashLog.Key));
        }

        [Fact]
        public void ABiomeEnteredIsKnownByTheNameTheGameShowsInThePlayersLanguage()
        {
            // Player.AddKnownBiome keeps BiomeSector.GetName(), which is translated, an area's
            // prefix before the biome's own name: "Schwarzwald", "Dunkler Sumpf". What grows
            // there then counts as in a biome entered.
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            var forest = E(Kind.Biome, "BlackForest", "$biome_blackforest");
            var swamp = E(Kind.Biome, "Swamp", "$biome_swamp");
            var plains = E(Kind.Biome, "Plains", "$biome_plains");
            foreach (var biome in new[] { meadows, forest, swamp, plains }) biome.Biomes = new[] { biome.Name };
            var wood = E(Kind.Item, "Wood", "$item_wood");
            var beech = E(Kind.Resource, "Beech1", "$prop_beech");
            beech.Biomes = new[] { "BlackForest" };
            wood.AddTermLink("from", beech);
            var shown = new Dictionary<string, string> { ["Meadows"] = "Wiesen", ["BlackForest"] = "Schwarzwald", ["Swamp"] = "Sumpf", ["Plains"] = "Ebenen" };
            var catchUp = new CompendiumCatchUp(new EntryCatalog(new[] { meadows, forest, swamp, plains, wood, beech }), biomesShown: shown);
            var book = new CompendiumBook();
            var records = new CompendiumRecords();
            records.Biomes.Add("Wiesen");
            records.Biomes.Add("Schwarzwald");
            records.Biomes.Add("Dunkler Sumpf");
            records.PickedUp.Add("$item_wood");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Known, book.Level(meadows.Key));
            Assert.Equal(KnownLevel.Known, book.Level(forest.Key));
            Assert.Equal(KnownLevel.Known, book.Level(swamp.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(plains.Key));
            Assert.Equal(KnownLevel.Seen, book.Level(beech.Key));
        }

        [Fact]
        public void ABiomeEnteredIsKnownUnderTheNameItsAreaWasGiven()
        {
            // The game records an area by its biome's name, a prefix or a name of its own put
            // before it where the world gives one.
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            var swamp = E(Kind.Biome, "Swamp", "$biome_swamp");
            var plains = E(Kind.Biome, "Plains", "$biome_plains");
            var (catchUp, book) = Fresh(meadows, swamp, plains);
            var records = new CompendiumRecords();
            records.Biomes.Add("$biome_meadows");
            records.Biomes.Add("$area_dark $biome_swamp");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Known, book.Level(meadows.Key));
            Assert.Equal(KnownLevel.Known, book.Level(swamp.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(plains.Key));
        }

        [Fact]
        public void EveryAreaACharacterSavedBeforeBiomesWereKeptByTheirShownNameEnteredIsKnown()
        {
            // A save from before the game kept biomes by their shown names has each turned into its
            // untranslated name as the character loads (BiomeSector.GetBiomeName), the no-biome
            // among them, and keeps them so. Entering a biome again adds its shown name as well, as the game
            // compares shown names (Player.IsBiomeKnown). Every area of both kinds is known.
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            var forest = E(Kind.Biome, "BlackForest", "$biome_blackforest");
            var swamp = E(Kind.Biome, "Swamp", "$biome_swamp");
            var plains = E(Kind.Biome, "Plains", "$biome_plains");
            foreach (var biome in new[] { meadows, forest, swamp, plains }) biome.Biomes = new[] { biome.Name };
            var shown = new Dictionary<string, string> { ["Meadows"] = "Meadows", ["BlackForest"] = "Black Forest", ["Swamp"] = "Swamp", ["Plains"] = "Plains" };
            var catchUp = new CompendiumCatchUp(new EntryCatalog(new[] { meadows, forest, swamp, plains }), biomesShown: shown);
            var book = new CompendiumBook();
            var records = new CompendiumRecords();
            records.Biomes.Add("$biome_meadows");
            records.Biomes.Add("$biome_blackforest");
            records.Biomes.Add("$biome_none");
            records.Biomes.Add("Meadows");
            records.Biomes.Add("Dark Swamp");
            // Before the records are read in, every area naming a biome is one the book lacks, but the no-biome.
            Assert.Equal(new[] { "$biome_meadows", "$biome_blackforest", "Meadows", "Dark Swamp" }, catchUp.AreasNotKnown(records, book));

            Assert.Equal(3, catchUp.Fill(records, book, firstLoad: false));

            Assert.Equal(KnownLevel.Known, book.Level(meadows.Key));
            Assert.Equal(KnownLevel.Known, book.Level(forest.Key));
            Assert.Equal(KnownLevel.Known, book.Level(swamp.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(plains.Key));
            Assert.Empty(catchUp.AreasNotKnown(records, book));
        }

        [Fact]
        public void TheNoBiomeAnOlderSaveKeepsIsNoBiomeEnteredAndAnAreaNamingNoneOfTheCatalogsIsTold()
        {
            // The game's Heightmap.Biome.None, kept as "$biome_none" by older saves, is no biome a
            // character enters: nothing is learned from it and no area is missing for it. An area
            // naming no biome of the catalog (a name of its own replacing the biome's, or one shown
            // in a language the catch-up has no names in) is told as one it could not place.
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            meadows.Biomes = new[] { "Meadows" };
            var shown = new Dictionary<string, string> { ["Meadows"] = "Meadows" };
            var catchUp = new CompendiumCatchUp(new EntryCatalog(new[] { meadows }), biomesShown: shown);
            var book = new CompendiumBook();
            var records = new CompendiumRecords();
            records.Biomes.Add("$biome_none");

            Assert.Equal(0, catchUp.Fill(records, book, firstLoad: false));
            Assert.Empty(catchUp.AreasNotKnown(records, book));

            records.Biomes.Add("Wiesen");
            catchUp.Fill(records, book, firstLoad: false);
            Assert.Equal(KnownLevel.Unknown, book.Level(meadows.Key));
            Assert.Equal(new[] { "Wiesen" }, catchUp.AreasNotKnown(records, book));
        }

        private static Dictionary<string, IReadOnlyList<string>> Every(params (string Biome, string[] Names)[] biomes)
        {
            var all = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var (biome, names) in biomes) all[biome] = names;
            return all;
        }

        [Fact]
        public void ABiomeEnteredWhileTheGameRanInAnotherLanguageIsKnownByThatLanguagesName()
        {
            // The game keeps a biome entered by its name in the language played in then
            // (BiomeSector.GetName): a character who entered the Meadows in German keeps
            // "Grasland", and the game played in English now shows "Meadows". The game's tables
            // name every biome in every language it ships, so the area is placed by them.
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            var swamp = E(Kind.Biome, "Swamp", "$biome_swamp");
            var plains = E(Kind.Biome, "Plains", "$biome_plains");
            foreach (var biome in new[] { meadows, swamp, plains }) biome.Biomes = new[] { biome.Name };
            var raspberry = E(Kind.Item, "Raspberry", "$item_raspberries");
            var bush = E(Kind.Resource, "RaspberryBush", "$prop_raspberrybush");
            bush.Biomes = new[] { "Meadows" };
            raspberry.AddTermLink("from", bush);
            var shown = new Dictionary<string, string> { ["Meadows"] = "Meadows", ["Swamp"] = "Swamp", ["Plains"] = "Plains" };
            var every = Every(("Meadows", new[] { "Meadows", "Äng", "Grasland" }), ("Swamp", new[] { "Swamp", "Träsk", "Sumpfland" }), ("Plains", new[] { "Plains", "Slätt", "Ebenen" }));
            var catchUp = new CompendiumCatchUp(new EntryCatalog(new[] { meadows, swamp, plains, raspberry, bush }), biomesShown: shown, biomesInEveryLanguage: () => every);
            var book = new CompendiumBook();
            var records = new CompendiumRecords();
            records.Biomes.Add("Grasland");
            records.Biomes.Add("Dunkles Sumpfland");
            records.PickedUp.Add("$item_raspberries");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Known, book.Level(meadows.Key));
            Assert.Equal(KnownLevel.Known, book.Level(swamp.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(plains.Key));
            Assert.Equal(KnownLevel.Seen, book.Level(bush.Key));
            Assert.Empty(catchUp.AreasNotKnown(records, book));
        }

        [Fact]
        public void ABiomesNameStandingInsideAnotherBiomesLongerNameIsPartOfThatName()
        {
            // The Black Forest in Bulgarian is "Черна Гора", and "Гора" is the Mountain in Russian
            // and Ukrainian: an area kept as "Черна Гора" is the Black Forest only.
            var forest = E(Kind.Biome, "BlackForest", "$biome_blackforest");
            var mountain = E(Kind.Biome, "Mountain", "$biome_mountain");
            foreach (var biome in new[] { forest, mountain }) biome.Biomes = new[] { biome.Name };
            var shown = new Dictionary<string, string> { ["BlackForest"] = "Black Forest", ["Mountain"] = "Mountain" };
            var every = Every(("BlackForest", new[] { "Black Forest", "Черна Гора" }), ("Mountain", new[] { "Mountain", "Гора", "Планина" }));
            var catchUp = new CompendiumCatchUp(new EntryCatalog(new[] { forest, mountain }), biomesShown: shown, biomesInEveryLanguage: () => every);
            var book = new CompendiumBook();
            var records = new CompendiumRecords();
            records.Biomes.Add("Черна Гора");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Known, book.Level(forest.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(mountain.Key));

            // Its own name in a language that has it alone is the Mountain still.
            records.Biomes.Add("Гора");
            catchUp.Fill(records, book, firstLoad: false);
            Assert.Equal(KnownLevel.Known, book.Level(mountain.Key));
        }

        [Fact]
        public void AnAreaWhoseWordsNameDifferentBiomesInDifferentLanguagesIsLeftToBeTold()
        {
            // "Eng" is the Meadows in Danish and Norwegian, "Berg" the Mountain in Swedish: an
            // area holding both names neither biome for certain, so nothing is learned from it
            // and it is told as one the catch-up could not place.
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            var mountain = E(Kind.Biome, "Mountain", "$biome_mountain");
            foreach (var biome in new[] { meadows, mountain }) biome.Biomes = new[] { biome.Name };
            var shown = new Dictionary<string, string> { ["Meadows"] = "Meadows", ["Mountain"] = "Mountain" };
            var every = Every(("Meadows", new[] { "Meadows", "Eng", "Äng" }), ("Mountain", new[] { "Mountain", "Berg" }));
            var catchUp = new CompendiumCatchUp(new EntryCatalog(new[] { meadows, mountain }), biomesShown: shown, biomesInEveryLanguage: () => every);
            var book = new CompendiumBook();
            var records = new CompendiumRecords();
            records.Biomes.Add("Eng Berg");

            Assert.Equal(0, catchUp.Fill(records, book, firstLoad: false));

            Assert.Equal(KnownLevel.Unknown, book.Level(meadows.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(mountain.Key));
            Assert.Equal(new[] { "Eng Berg" }, catchUp.AreasNotKnown(records, book));

            // Words naming one biome in two languages are that biome.
            records.Biomes.Add("Äng Eng");
            catchUp.Fill(records, book, firstLoad: false);
            Assert.Equal(KnownLevel.Known, book.Level(meadows.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(mountain.Key));
        }

        [Fact]
        public void ABiomesNameThatIsOnlyPartOfAWordInAnAreaDoesNotCount()
        {
            // "Hav" is the Ocean in Swedish, and only a part of a prefix such as "Havsnära" put
            // before "Slätt", the Plains: the area is the Plains.
            var ocean = E(Kind.Biome, "Ocean", "$biome_ocean");
            var plains = E(Kind.Biome, "Plains", "$biome_plains");
            foreach (var biome in new[] { ocean, plains }) biome.Biomes = new[] { biome.Name };
            var shown = new Dictionary<string, string> { ["Ocean"] = "Ocean", ["Plains"] = "Plains" };
            var every = Every(("Ocean", new[] { "Ocean", "Hav" }), ("Plains", new[] { "Plains", "Slätt" }));
            var catchUp = new CompendiumCatchUp(new EntryCatalog(new[] { ocean, plains }), biomesShown: shown, biomesInEveryLanguage: () => every);
            var book = new CompendiumBook();
            var records = new CompendiumRecords();
            records.Biomes.Add("Havsnära Slätt");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Known, book.Level(plains.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(ocean.Key));
        }

        [Fact]
        public void TheGamesLanguageTablesAreReadOnlyForAnAreaThePlayersLanguageCannotPlaceAndThenOnce()
        {
            // The tables hold every word of the game in every language, so reading them costs:
            // a character whose areas are all named by token or in the language played now never
            // has them read, and one with an area kept in another language has them read once.
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            meadows.Biomes = new[] { "Meadows" };
            var shown = new Dictionary<string, string> { ["Meadows"] = "Meadows" };
            var reads = 0;
            var catchUp = new CompendiumCatchUp(new EntryCatalog(new[] { meadows }), biomesShown: shown,
                biomesInEveryLanguage: () => { reads++; return Every(("Meadows", new[] { "Meadows", "Grasland" })); });
            var book = new CompendiumBook();
            var records = new CompendiumRecords();
            records.Biomes.Add("$biome_meadows");
            records.Biomes.Add("Meadows");
            records.Biomes.Add("$biome_none");

            catchUp.Fill(records, book, firstLoad: true);
            catchUp.AreasNotKnown(records, book);
            Assert.Equal(0, reads);

            records.Biomes.Add("Grasland");
            catchUp.Fill(records, book, firstLoad: false);
            catchUp.Fill(records, book, firstLoad: false);
            Assert.Empty(catchUp.AreasNotKnown(records, book));
            Assert.Equal(1, reads);
        }

        [Fact]
        public void ReadingTheRecordsNeverLowersWhatIsKnownAndTellsHowManyWereLearnedMoreOf()
        {
            var sword = E(Kind.Item, "SwordIron", "$item_sword_iron");
            var bow = E(Kind.Item, "Bow", "$item_bow");
            var (catchUp, book) = Fresh(sword, bow);
            book.Raise(sword.Key, KnownLevel.Known);
            var records = new CompendiumRecords();
            records.Materials.Add("$item_sword_iron");
            records.Materials.Add("$item_bow");

            Assert.Equal(1, catchUp.Fill(records, book, firstLoad: false));
            Assert.Equal(KnownLevel.Known, book.Level(sword.Key));
            Assert.Equal(0, catchUp.Fill(records, book, firstLoad: false));
        }

        [Fact]
        public void WhatTheCharacterMeetsInPlayIsLearnedAsNewsAndWhatTheCatalogLacksIsPassedOver()
        {
            var troll = E(Kind.Creature, "Troll", "$enemy_troll");
            var rested = new Entry { Kind = Kind.StatusEffect, Name = "Rested" };
            var (catchUp, book) = Fresh(troll, rested);

            Assert.True(catchUp.Met(troll.Key, KnownLevel.Seen, book));
            Assert.True(catchUp.Met(rested.Key, KnownLevel.Known, book));
            Assert.False(catchUp.Met(troll.Key, KnownLevel.HeardOf, book));
            Assert.False(catchUp.Met("SomethingNoCatalogHas", KnownLevel.Known, book));
            Assert.False(catchUp.Met(null, KnownLevel.Known, book));

            Assert.Equal(KnownLevel.Seen, book.Level(troll.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level("SomethingNoCatalogHas"));
            Assert.Equal(new[] { rested.Key, troll.Key }, book.Lately.ToArray());
            // What sight looks at in the world is let go at once where nothing could be learned of it.
            Assert.True(catchUp.Lists(troll.Key));
            Assert.False(catchUp.Lists("SomethingNoCatalogHas"));
            Assert.False(catchUp.Lists(null));
        }

        [Fact]
        public void WhatAFirstLoadBringsIsNewsThoughNotWhenOrWhereItCameToBeKnown()
        {
            // The first reading as a character loads: what it brings the book did not hold is news
            // all the same, in Learned; when and where it came to be known the records do not
            // say, so none is kept. What grows later is news, with the day and biome of now.
            var wood = E(Kind.Item, "Wood", "$item_wood");
            var stone = E(Kind.Item, "Stone", "$item_stone");
            var (catchUp, book) = Fresh(wood, stone);
            book.Now = () => new Moment(12, "Meadows");
            var records = new CompendiumRecords();
            records.PickedUp.Add("$item_wood");

            var mark = book.Learned;
            catchUp.Fill(records, book, firstLoad: true);
            Assert.Equal(KnownLevel.Known, book.Level(wood.Key));
            Assert.Equal(new[] { wood.Key }, book.LearnedSince(mark).ToArray());
            Assert.Null(book.Came(wood.Key));
            Assert.True(book.Changed);

            records.PickedUp.Add("$item_stone");
            catchUp.Fill(records, book, firstLoad: false);
            Assert.Equal(new[] { stone.Key, wood.Key }, book.Lately.ToArray());
            Assert.Equal((CameBy.Held, new Moment(12, "Meadows")), book.Came(stone.Key));
        }

        [Fact]
        public void ARecipeUnlockedPairsWhatItMakesWithItsIngredientsAndStation()
        {
            // For the strict rules: the character knows what goes into it, as the game showed it.
            var sword = E(Kind.Item, "SwordIron", "$item_sword_iron");
            var iron = E(Kind.Item, "Iron", "$item_iron");
            var forge = E(Kind.Piece, "forge", "$piece_forge");
            var wall = E(Kind.Piece, "wood_wall", "$piece_woodwall");
            var wood = E(Kind.Item, "Wood", "$item_wood");
            sword.AddTermLink("needs", iron);
            sword.Stations = new[] { new StationUse("forge", "Forge", 1) };
            wall.AddTermLink("needs", wood);
            var (catchUp, book) = Fresh(sword, iron, forge, wall, wood);
            var records = new CompendiumRecords();
            records.Recipes.Add("$item_sword_iron");
            records.Placed.Add("$piece_woodwall");

            catchUp.Fill(records, book, firstLoad: true);

            Assert.True(book.Witnessed(sword.Key, iron.Key));
            Assert.True(book.Witnessed(sword.Key, forge.Key));
            Assert.True(book.Witnessed(wall.Key, wood.Key));
            Assert.False(book.Witnessed(iron.Key, forge.Key));
        }

        [Fact]
        public void TheStatusEffectsWhatTheRecordsMakeKnownGivesAreKnownAndPairedWithIt()
        {
            // A character played before rested by its bed and got wet in its biomes long before
            // the compendium watched; the game keeps no record of status effects themselves.
            var bed = E(Kind.Piece, "bed", "$piece_bed");
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            var throne = E(Kind.Piece, "piece_throne", "$piece_throne");
            var fire = E(Kind.Piece, "fire_pit", "$piece_firepit");
            var rested = new Entry { Kind = Kind.StatusEffect, Name = "Rested" };
            var wet = new Entry { Kind = Kind.StatusEffect, Name = "Wet" };
            var royal = new Entry { Kind = Kind.StatusEffect, Name = "Royal" };
            var smoked = new Entry { Kind = Kind.StatusEffect, Name = "Smoked" };
            rested.Links.Add(new Link { Group = LinkWords.GivenBy, Target = bed.Key, Back = true });
            rested.Links.Add(new Link { Group = LinkWords.GivenBy, Target = throne.Key, Back = true });
            wet.Links.Add(new Link { Group = LinkWords.GivenBy, Target = meadows.Key, Back = true });
            royal.Links.Add(new Link { Group = LinkWords.GivenBy, Target = throne.Key, Back = true });
            smoked.Links.Add(new Link { Group = LinkWords.Upgrades, Target = fire.Key, Back = true });
            var (catchUp, book) = Fresh(bed, meadows, throne, fire, rested, wet, royal, smoked);
            var records = new CompendiumRecords();
            records.Placed.Add("$piece_bed");
            records.Placed.Add("$piece_firepit");
            records.Biomes.Add("$biome_meadows");

            catchUp.Fill(records, book, firstLoad: true);

            Assert.Equal(KnownLevel.Known, book.Level(rested.Key));
            Assert.Equal(KnownLevel.Known, book.Level(wet.Key));
            // Only what gives it counts, and only what the records show.
            Assert.Equal(KnownLevel.Unknown, book.Level(royal.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(smoked.Key));
            // News as the character loads, as everything the first reading brings.
            Assert.Contains(rested.Key, book.Lately);
            Assert.Contains(wet.Key, book.Lately);
            Assert.True(book.Witnessed(rested.Key, bed.Key));
            Assert.False(book.Witnessed(rested.Key, throne.Key));
            Assert.True(book.Witnessed(wet.Key, meadows.Key));
        }

        [Fact]
        public void AFoodEatenAndAThingPickedPairWithWhereTheyCameFromATrophyWithNothing()
        {
            var troll = E(Kind.Creature, "Troll", "$enemy_troll");
            var trophy = E(Kind.Item, "TrophyFrostTroll", "$item_trophy_troll");
            var berries = E(Kind.Item, "Raspberry", "$item_raspberries");
            var bush = E(Kind.Resource, "RaspberryBush", "$piece_raspberrybush");
            var far = E(Kind.Resource, "RaspberryBush_far", "$piece_raspberrybush_far");
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            meadows.Biomes = new[] { "Meadows" };
            bush.Biomes = new[] { "Meadows" };
            far.Biomes = new[] { "Meadows" };
            troll.Biomes = new[] { "Meadows" };
            var mead = E(Kind.Item, "MeadHealthMinor", "$item_mead_hp_minor");
            var rested = new Entry { Kind = Kind.StatusEffect, Name = "Potion_health_minor" };
            var chest = E(Kind.Piece, "TreasureChest_forest", "$piece_chest");
            var axe = E(Kind.Item, "AxeStone", "$item_axe_stone");
            trophy.AddTermLink("from", troll);
            trophy.AddTermLink("from", chest);
            berries.AddTermLink("from", bush);
            berries.AddTermLink("from", far);
            berries.AddTermLink("from", troll);
            mead.Links.Add(new Link { Group = LinkWords.StatusEffects, Target = rested.Key });
            mead.Links.Add(new Link { Group = LinkWords.GivenBy, Target = troll.Key, Back = true });
            mead.Links.Add(new Link { Group = LinkWords.Upgrades, Target = axe.Key });
            var (catchUp, book) = Fresh(troll, trophy, berries, bush, far, mead, rested, chest, axe, meadows);
            var records = new CompendiumRecords();
            records.Biomes.Add("$biome_meadows");
            records.Trophies.Add("TrophyFrostTroll");
            records.Picked.Add("Raspberry");
            records.Eaten.Add("$item_mead_hp_minor");

            catchUp.Fill(records, book, firstLoad: true);

            // A trophy held makes its creature seen, as 26A has it, and pairs nothing with it.
            Assert.False(book.Witnessed(trophy.Key, troll.Key));
            Assert.Equal(KnownLevel.Seen, book.Level(troll.Key));
            Assert.False(book.Witnessed(trophy.Key, chest.Key));
            // The records cannot tell which bush: each that gives it, as the loose rules know them all.
            Assert.False(book.Witnessed(berries.Key, bush.Key));
            Assert.False(book.Witnessed(berries.Key, far.Key));
            Assert.False(book.Witnessed(berries.Key, troll.Key));
            Assert.True(book.Witnessed(mead.Key, rested.Key));
            Assert.False(book.Witnessed(mead.Key, troll.Key));
            Assert.False(book.Witnessed(mead.Key, axe.Key));
        }

        [Fact]
        public void TheGuardianPowerChosenAtAStoneIsKnownAndPairedWithTheStoneSeen()
        {
            // Chosen at its boss stone, a power is the character's own even before its first use.
            var eikthyrStone = E(Kind.Piece, "BossStone_Eikthyr");
            var elderStone = E(Kind.Piece, "BossStone_TheElder");
            var eikthyr = new Entry { Kind = Kind.StatusEffect, Name = "GP_Eikthyr" };
            var elder = new Entry { Kind = Kind.StatusEffect, Name = "GP_TheElder" };
            eikthyr.Links.Add(new Link { Group = LinkWords.GivenBy, Target = eikthyrStone.Key, Back = true });
            elder.Links.Add(new Link { Group = LinkWords.GivenBy, Target = elderStone.Key, Back = true });
            var (catchUp, book) = Fresh(eikthyrStone, elderStone, eikthyr, elder);
            book.Raise(eikthyrStone.Key, KnownLevel.Seen);
            var records = new CompendiumRecords();
            records.Powers.Add("GP_Eikthyr");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(KnownLevel.Known, book.Level(eikthyr.Key));
            Assert.Equal(eikthyr.Key, book.Lately.First());
            Assert.True(book.Witnessed(eikthyr.Key, eikthyrStone.Key));
            // Only the power chosen, and its stone stays as it was.
            Assert.Equal(KnownLevel.Unknown, book.Level(elder.Key));
            Assert.Equal(KnownLevel.Seen, book.Level(eikthyrStone.Key));
        }

        [Fact]
        public void TradingWithATraderKnowsItAndShowsItsWaresPairedWithIt()
        {
            var haldor = E(Kind.Creature, "Haldor");
            var hat = E(Kind.Item, "HelmetYule");
            var lantern = E(Kind.Item, "Lantern");
            var (catchUp, book) = Fresh(haldor, hat, lantern);

            Assert.True(catchUp.Traded(haldor.Key, new[] { hat.Key, lantern.Key, "ModsSecretWare" }, book));

            Assert.Equal(KnownLevel.Known, book.Level(haldor.Key));
            Assert.Equal(KnownLevel.Seen, book.Level(hat.Key));
            Assert.Equal(KnownLevel.Seen, book.Level(lantern.Key));
            Assert.True(book.Witnessed(haldor.Key, hat.Key));
            Assert.True(book.Witnessed(lantern.Key, haldor.Key));
            Assert.False(book.Witnessed(haldor.Key, "ModsSecretWare"));
            // Trading again is no news; a trader the catalog lacks gives nothing.
            Assert.False(catchUp.Traded(haldor.Key, new[] { hat.Key }, book));
            Assert.False(catchUp.Traded("ModsTrader", new[] { hat.Key }, book));
            Assert.False(book.Witnessed("ModsTrader", hat.Key));
        }

        [Fact]
        public void APiecePlacedIsPairedWithTheOneToolThatBuildsIt()
        {
            // The records keep what was placed, not with what: only a piece one tool alone builds is sure.
            var hammer = E(Kind.Item, "Hammer");
            var hoe = E(Kind.Item, "Hoe");
            var wall = E(Kind.Piece, "wood_wall_half", "$piece_woodwallhalf");
            var path = E(Kind.Piece, "path", "$piece_path");
            var tools = new Dictionary<string, IReadOnlyList<string>>
            {
                ["wood_wall_half"] = new[] { "Hammer" },
                ["path"] = new[] { "Hoe", "Hammer" },
            };
            var catchUp = new CompendiumCatchUp(new EntryCatalog(new[] { hammer, hoe, wall, path }), piece => tools.TryGetValue(piece, out var t) ? t : null);
            var book = new CompendiumBook();
            var records = new CompendiumRecords();
            records.Placed.Add("$piece_woodwallhalf");
            records.Placed.Add("$piece_path");

            catchUp.Fill(records, book, firstLoad: true);

            Assert.True(book.Witnessed(wall.Key, hammer.Key));
            Assert.False(book.Witnessed(path.Key, hoe.Key));
            Assert.False(book.Witnessed(path.Key, hammer.Key));
        }

        [Fact]
        public void WhatTheCharacterUsesIsKnown()
        {
            // A portal, bed, ship or chest a friend built: using it (Player.Interact, or an item
            // used on it) shows what it does, as building it would.
            var portal = E(Kind.Piece, "portal_wood", "$piece_portal");
            var vegvisir = E(Kind.Piece, "Vegvisir_Eikthyr", "$piece_vegvisir");
            var bush = E(Kind.Resource, "RaspberryBush", "$piece_raspberrybush");
            var haldor = E(Kind.Creature, "Haldor", "$npc_haldor");
            var wolf = E(Kind.Creature, "Wolf", "$enemy_wolf");
            var berries = E(Kind.Item, "Raspberry", "$item_raspberries");
            var (catchUp, book) = Fresh(portal, vegvisir, bush, haldor, wolf, berries);

            Assert.True(catchUp.Used("portal_wood", book));
            Assert.True(catchUp.Used("Vegvisir_Eikthyr", book));
            Assert.True(catchUp.Used("RaspberryBush", book));
            Assert.False(catchUp.Used("portal_wood", book));
            Assert.Equal(KnownLevel.Known, book.Level(portal.Key));
            Assert.Equal(KnownLevel.Known, book.Level(vegvisir.Key));
            Assert.Equal(KnownLevel.Known, book.Level(bush.Key));

            // A creature is known by fighting or taming it, an item by holding it: talking to one
            // or touching the other is not using it.
            Assert.False(catchUp.Used("Haldor", book));
            Assert.False(catchUp.Used("Wolf", book));
            Assert.False(catchUp.Used("Raspberry", book));
            Assert.Equal(KnownLevel.Unknown, book.Level(wolf.Key));
            Assert.False(catchUp.Used("NotInTheCatalog", book));
        }

        [Fact]
        public void AnItemWhoseCardTheGameShowedIsKnown()
        {
            // Hovering an item in a chest, the inventory or a trader's store shows its whole
            // tooltip card, by its name token: every item going by that name is known.
            var sword = E(Kind.Item, "SwordIron", "$item_sword_iron");
            var lantern = E(Kind.Item, "Lantern", "$item_lantern");
            var piece = E(Kind.Piece, "piece_lantern", "$item_lantern");
            var (catchUp, book) = Fresh(sword, lantern, piece);

            Assert.True(catchUp.CardShown("$item_sword_iron", book));
            Assert.False(catchUp.CardShown("$item_sword_iron", book));
            Assert.True(catchUp.CardShown("$item_lantern", book));
            Assert.Equal(KnownLevel.Known, book.Level(sword.Key));
            Assert.Equal(KnownLevel.Known, book.Level(lantern.Key));
            // Only items: a piece or a skill going by a name is not an item's card.
            Assert.Equal(KnownLevel.Unknown, book.Level(piece.Key));
            Assert.False(catchUp.CardShown("$skill_swords", book));
            Assert.False(catchUp.CardShown("", book));
            Assert.False(catchUp.CardShown(null, book));
        }

        [Fact]
        public void ADungeonIsKnownOnlyOnceInsideAndSeenFromItsEntrance()
        {
            // A known dungeon makes all its rooms known, so standing by its door is not enough.
            var crypt = new Entry { Kind = Kind.Location, Name = "Crypt2" };
            var (catchUp, book) = Fresh(crypt);

            Assert.True(catchUp.StoodIn(crypt.Key, hasInside: true, inside: false, book));
            Assert.Equal(KnownLevel.Seen, book.Level(crypt.Key));

            Assert.True(catchUp.StoodIn(crypt.Key, hasInside: true, inside: true, book));
            Assert.Equal(KnownLevel.Known, book.Level(crypt.Key));
            Assert.Equal(crypt.Key, book.Lately.First());
        }

        [Fact]
        public void APlaceWithoutAnInsideIsKnownByStandingInIt()
        {
            var dolmen = new Entry { Kind = Kind.Location, Name = "Dolmen01" };
            var (catchUp, book) = Fresh(dolmen);

            Assert.True(catchUp.StoodIn(dolmen.Key, hasInside: false, inside: false, book));
            Assert.Equal(KnownLevel.Known, book.Level(dolmen.Key));
            // Again it is no news, and a place the catalog lacks is passed over.
            Assert.False(catchUp.StoodIn(dolmen.Key, hasInside: false, inside: false, book));
            Assert.False(catchUp.StoodIn("loc:ModsPlace", hasInside: true, inside: true, book));
        }

        [Fact]
        public void APlaceStoodInIsPairedWithTheBiomeThere()
        {
            // The strict rules show a place's biome once the two were seen together.
            var dolmen = new Entry { Kind = Kind.Location, Name = "Dolmen01" };
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            var (catchUp, book) = Fresh(dolmen, meadows);
            Assert.True(catchUp.StoodIn(dolmen.Key, hasInside: false, inside: false, book, meadows.Key));
            Assert.True(book.Witnessed(dolmen.Key, meadows.Key));
            // So is a dungeon seen at its entrance.
            var crypt = new Entry { Kind = Kind.Location, Name = "Crypt2" };
            var forest = E(Kind.Biome, "BlackForest", "$biome_blackforest");
            (catchUp, book) = Fresh(crypt, forest);
            catchUp.StoodIn(crypt.Key, hasInside: true, inside: false, book, forest.Key);
            Assert.True(book.Witnessed(crypt.Key, forest.Key));
            // A place the catalog lacks, or a biome not known, pairs nothing.
            catchUp.StoodIn("loc:ModsPlace", hasInside: false, inside: false, book, forest.Key);
            Assert.False(book.Witnessed("loc:ModsPlace", forest.Key));
            catchUp.StoodIn(crypt.Key, hasInside: true, inside: true, book, null);
            Assert.Equal(1, book.Pairs);
        }

        [Fact]
        public void AStationIsPairedWithEachUpgradeStandingNearIt()
        {
            // The strict rules show a station's upgrades once the station was seen with them near it.
            var workbench = E(Kind.Piece, "piece_workbench");
            var block = E(Kind.Piece, "piece_workbench_ext1");
            var rack = E(Kind.Piece, "piece_workbench_ext2");
            var (catchUp, book) = Fresh(workbench, block, rack);
            catchUp.Upgrades(workbench.Key, new[] { block.Key, rack.Key, "mod_missing" }, book);
            Assert.True(book.Witnessed(workbench.Key, block.Key));
            Assert.True(book.Witnessed(workbench.Key, rack.Key));
            Assert.False(book.Witnessed(workbench.Key, "mod_missing"));
            Assert.Equal(2, book.Pairs);
            // A station the catalog lacks pairs nothing.
            catchUp.Upgrades("mod_station", new[] { block.Key }, book);
            Assert.Equal(2, book.Pairs);
        }

        [Fact]
        public void ARaidLivedThroughIsKnownAndPairedWithNothingUntilACreatureIsSeenInIt()
        {
            // The pairs come of the creatures seen while the raid runs (CompendiumSight.Look), not of every creature met before.
            var greydwarf = E(Kind.Creature, "Greydwarf");
            var shaman = E(Kind.Creature, "Greydwarf_Shaman");
            var raid = new Entry { Kind = Kind.Raid, Name = "foresttrolls" };
            raid.AddTermLink("spawns", greydwarf);
            raid.AddTermLink("spawns", shaman);
            var (catchUp, book) = Fresh(greydwarf, shaman, raid);
            book.Raise(greydwarf.Key, KnownLevel.Seen);

            Assert.True(catchUp.LivedThrough(raid.Key, book));

            Assert.Equal(KnownLevel.Known, book.Level(raid.Key));
            Assert.Equal(raid.Key, book.Lately.First());
            Assert.False(book.Witnessed(raid.Key, greydwarf.Key));
            Assert.False(book.Witnessed(raid.Key, shaman.Key));
            // Lived through again, it is no news.
            Assert.False(catchUp.LivedThrough(raid.Key, book));
            // The sight asks whether the raid brings a creature before pairing the two.
            Assert.True(catchUp.RaidBrings(raid.Key, greydwarf.Key));
            Assert.False(catchUp.RaidBrings(raid.Key, "Troll"));
            Assert.False(catchUp.RaidBrings("raid:none", greydwarf.Key));
        }

        [Fact]
        public void ARaidTheCatalogLacksIsPassedOver()
        {
            var (catchUp, book) = Fresh(E(Kind.Creature, "Greydwarf"));

            Assert.False(catchUp.LivedThrough("raid:modsraid", book));
            Assert.Equal(0, book.Pairs);
            Assert.Empty(book.Lately);
        }

        [Fact]
        public void APowerChosenWhoseStoneWasNeverSeenIsKnownButPairedWithNothing()
        {
            var stone = E(Kind.Piece, "BossStone_Bonemass");
            var power = new Entry { Kind = Kind.StatusEffect, Name = "GP_Bonemass" };
            power.Links.Add(new Link { Group = LinkWords.GivenBy, Target = stone.Key, Back = true });
            var (catchUp, book) = Fresh(stone, power);
            var records = new CompendiumRecords();
            records.Powers.Add("GP_Bonemass");
            records.Powers.Add("GP_NotInTheCatalog");

            catchUp.Fill(records, book, firstLoad: true);

            Assert.Equal(KnownLevel.Known, book.Level(power.Key));
            Assert.False(book.Witnessed(power.Key, stone.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(stone.Key));
        }
    }
}
