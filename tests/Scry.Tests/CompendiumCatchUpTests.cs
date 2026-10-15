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

            catchUp.Fill(records, book, quietly: false);

            Assert.Equal(KnownLevel.Known, book.Level(skeleton.Key));
            Assert.Equal(KnownLevel.Known, book.Level(noArcher.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(troll.Key));
        }

        [Fact]
        public void AnItemPickedUpCraftedOrEatenIsKnownAndOneOnlyHeldOrOfferedIsSeen()
        {
            var wood = E(Kind.Item, "Wood", "$item_wood");
            var club = E(Kind.Item, "Club", "$item_club");
            var berries = E(Kind.Item, "Raspberry", "$item_raspberries");
            var sword = E(Kind.Item, "SwordIron", "$item_sword_iron");
            var (catchUp, book) = Fresh(wood, club, berries, sword);
            var records = new CompendiumRecords();
            records.PickedUp.Add("$item_wood");
            records.Crafted.Add("$item_club");
            records.Eaten.Add("$item_raspberries");
            records.Materials.Add("$item_sword_iron");
            records.Materials.Add("$item_wood");

            catchUp.Fill(records, book, quietly: false);

            Assert.Equal(KnownLevel.Known, book.Level(wood.Key));
            Assert.Equal(KnownLevel.Known, book.Level(club.Key));
            Assert.Equal(KnownLevel.Known, book.Level(berries.Key));
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

            catchUp.Fill(records, book, quietly: false);

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

            Assert.Equal(1, catchUp.Fill(records, book, quietly: false));
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

            catchUp.Fill(records, book, quietly: false);

            Assert.Equal(KnownLevel.Known, book.Level(creature.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(item.Key));
        }

        [Fact]
        public void ATrophyHeldIsKnownAndSoIsTheCreatureItFallsFrom()
        {
            // Not the chest it can also be found in.
            var trophy = E(Kind.Item, "TrophyTroll", "$item_trophy_troll");
            var troll = E(Kind.Creature, "Troll", "$enemy_troll");
            var chest = E(Kind.Piece, "TreasureChest_trollcave", "$piece_chest");
            trophy.AddTermLink("from", troll);
            trophy.AddTermLink("from", chest);
            var (catchUp, book) = Fresh(trophy, troll, chest);
            var records = new CompendiumRecords();
            records.Trophies.Add("TrophyTroll");

            catchUp.Fill(records, book, quietly: false);

            Assert.Equal(KnownLevel.Known, book.Level(trophy.Key));
            Assert.Equal(KnownLevel.Known, book.Level(troll.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(chest.Key));
        }

        [Fact]
        public void WhatGivesAnItemTheRecordsKnowIsKnownWhenItIsAResource()
        {
            // Things picked are counted by the prefab of what they give.
            var berries = E(Kind.Item, "Raspberry", "$item_raspberries");
            var bush = E(Kind.Resource, "RaspberryBush", "$item_raspberries");
            var wood = E(Kind.Item, "Wood", "$item_wood");
            var beech = E(Kind.Resource, "Beech1", "$prop_beech");
            var greydwarf = E(Kind.Creature, "Greydwarf", "$enemy_greydwarf");
            var resin = E(Kind.Item, "Resin", "$item_resin");
            var fir = E(Kind.Resource, "FirTree", "$prop_fir");
            berries.AddTermLink("from", bush);
            wood.AddTermLink("from", beech);
            wood.AddTermLink("from", greydwarf);
            resin.AddTermLink("from", fir);
            var (catchUp, book) = Fresh(berries, bush, wood, beech, greydwarf, resin, fir);
            var records = new CompendiumRecords();
            records.Picked.Add("Raspberry");
            records.Picked.Add("Greydwarf");
            records.PickedUp.Add("$item_wood");
            records.Materials.Add("$item_resin");

            catchUp.Fill(records, book, quietly: false);

            Assert.Equal(KnownLevel.Known, book.Level(berries.Key));
            Assert.Equal(KnownLevel.Known, book.Level(bush.Key));
            Assert.Equal(KnownLevel.Known, book.Level(beech.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(greydwarf.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(fir.Key));
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

            catchUp.Fill(records, book, quietly: false);

            Assert.Equal(KnownLevel.Known, book.Level(meadows.Key));
            Assert.Equal(KnownLevel.Known, book.Level(swamp.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(plains.Key));
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

            Assert.Equal(1, catchUp.Fill(records, book, quietly: false));
            Assert.Equal(KnownLevel.Known, book.Level(sword.Key));
            Assert.Equal(0, catchUp.Fill(records, book, quietly: false));
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
        }

        [Fact]
        public void WhatTheCharacterKnewBeforeIsNoNewsWhenReadQuietly()
        {
            // The first reading after a character loads: all of it was known before, none of it
            // is learned lately; what grows later is.
            var wood = E(Kind.Item, "Wood", "$item_wood");
            var stone = E(Kind.Item, "Stone", "$item_stone");
            var (catchUp, book) = Fresh(wood, stone);
            var records = new CompendiumRecords();
            records.PickedUp.Add("$item_wood");

            catchUp.Fill(records, book, quietly: true);
            Assert.Equal(KnownLevel.Known, book.Level(wood.Key));
            Assert.Empty(book.Lately);
            Assert.True(book.Changed);

            records.PickedUp.Add("$item_stone");
            catchUp.Fill(records, book, quietly: false);
            Assert.Equal(new[] { stone.Key }, book.Lately.ToArray());
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

            catchUp.Fill(records, book, quietly: true);

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

            catchUp.Fill(records, book, quietly: true);

            Assert.Equal(KnownLevel.Known, book.Level(rested.Key));
            Assert.Equal(KnownLevel.Known, book.Level(wet.Key));
            // Only what gives it counts, and only what the records show.
            Assert.Equal(KnownLevel.Unknown, book.Level(royal.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(smoked.Key));
            Assert.Empty(book.Lately);
            Assert.True(book.Witnessed(rested.Key, bed.Key));
            Assert.False(book.Witnessed(rested.Key, throne.Key));
            Assert.True(book.Witnessed(wet.Key, meadows.Key));
        }

        [Fact]
        public void ATrophyAFoodEatenAndAThingPickedPairWithWhereTheyCameFrom()
        {
            var troll = E(Kind.Creature, "Troll", "$enemy_troll");
            var trophy = E(Kind.Item, "TrophyFrostTroll", "$item_trophy_troll");
            var berries = E(Kind.Item, "Raspberry", "$item_raspberries");
            var bush = E(Kind.Resource, "RaspberryBush", "$piece_raspberrybush");
            var far = E(Kind.Resource, "RaspberryBush_far", "$piece_raspberrybush_far");
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
            var (catchUp, book) = Fresh(troll, trophy, berries, bush, far, mead, rested, chest, axe);
            var records = new CompendiumRecords();
            records.Trophies.Add("TrophyFrostTroll");
            records.Picked.Add("Raspberry");
            records.Eaten.Add("$item_mead_hp_minor");

            catchUp.Fill(records, book, quietly: true);

            Assert.True(book.Witnessed(trophy.Key, troll.Key));
            Assert.False(book.Witnessed(trophy.Key, chest.Key));
            // The records cannot tell which bush: each that gives it, as the loose rules know them all.
            Assert.True(book.Witnessed(berries.Key, bush.Key));
            Assert.True(book.Witnessed(berries.Key, far.Key));
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

            catchUp.Fill(records, book, quietly: false);

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

            catchUp.Fill(records, book, quietly: true);

            Assert.True(book.Witnessed(wall.Key, hammer.Key));
            Assert.False(book.Witnessed(path.Key, hoe.Key));
            Assert.False(book.Witnessed(path.Key, hammer.Key));
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
        public void ARaidLivedThroughIsKnownAndPairedWithTheCreaturesItBringsThatWereMet()
        {
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
            Assert.True(book.Witnessed(raid.Key, greydwarf.Key));
            // A creature it brings that the character never met is no part of what it lived through.
            Assert.False(book.Witnessed(raid.Key, shaman.Key));
            // Lived through again, it is no news, and what was met meanwhile is paired.
            book.Raise(shaman.Key, KnownLevel.Known);
            Assert.False(catchUp.LivedThrough(raid.Key, book));
            Assert.True(book.Witnessed(raid.Key, shaman.Key));
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

            catchUp.Fill(records, book, quietly: true);

            Assert.Equal(KnownLevel.Known, book.Level(power.Key));
            Assert.False(book.Witnessed(power.Key, stone.Key));
            Assert.Equal(KnownLevel.Unknown, book.Level(stone.Key));
        }
    }
}
