using System;
using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class RecordsGrownTests
    {
        // The game's records of a character grow as it plays: a first kill of a kind, a new item
        // picked up, a recipe unlocked. Reading only what was added must leave the book exactly as
        // reading every record again would, while costing only what was added.

        private static Entry E(Kind kind, string name, params string[] tokens) => new Entry { Kind = kind, Name = name, Tokens = tokens };

        /// <summary>A small world with every way the records tell things: kills under shared names, recipes with ingredients and stations, a piece one tool builds, food and its effect, trophies, things picked, givers of status effects, wares and a power.</summary>
        private static CompendiumCatchUp World()
        {
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            meadows.Biomes = new[] { "Meadows" };
            var forest = E(Kind.Biome, "BlackForest", "$biome_blackforest");
            forest.Biomes = new[] { "BlackForest" };
            var greydwarf = E(Kind.Creature, "Greydwarf", "$enemy_greydwarf");
            greydwarf.Biomes = new[] { "BlackForest" };
            var frozen = E(Kind.Creature, "Greydwarf_Frozen", "$enemy_greydwarf");
            frozen.Biomes = new[] { "DeepNorth" };
            var boar = E(Kind.Creature, "Boar", "$enemy_boar");
            boar.Biomes = new[] { "Meadows" };
            var troll = E(Kind.Creature, "Troll", "$enemy_troll");
            troll.Biomes = new[] { "BlackForest" };
            var wood = E(Kind.Item, "Wood", "$item_wood");
            var stone = E(Kind.Item, "Stone", "$item_stone");
            var beech = E(Kind.Resource, "Beech1", "$prop_beech");
            beech.Biomes = new[] { "Meadows" };
            var pine = E(Kind.Resource, "Pinetree_01", "$prop_pine");
            pine.Biomes = new[] { "BlackForest" };
            wood.AddTermLink("from", beech);
            wood.AddTermLink("from", pine);
            var club = E(Kind.Item, "Club", "$item_club");
            club.AddTermLink("needs", wood);
            var bench = E(Kind.Piece, "piece_workbench", "$piece_workbench");
            club.Stations = new[] { new StationUse("piece_workbench", "Workbench", 1) };
            var wall = E(Kind.Piece, "wood_wall", "$piece_woodwall");
            wall.AddTermLink("needs", wood);
            var hammer = E(Kind.Item, "Hammer", "$item_hammer");
            var berries = E(Kind.Item, "Raspberry", "$item_raspberries");
            var bush = E(Kind.Resource, "RaspberryBush", "$piece_raspberrybush");
            bush.Biomes = new[] { "Meadows" };
            berries.AddTermLink("from", bush);
            var fed = new Entry { Kind = Kind.StatusEffect, Name = "Raspberry_fed" };
            berries.Links.Add(new Link { Group = LinkWords.StatusEffects, Target = fed.Key });
            var trophy = E(Kind.Item, "TrophyTroll", "$item_trophy_troll");
            trophy.AddTermLink("from", troll);
            var bed = E(Kind.Piece, "bed", "$piece_bed");
            var rested = new Entry { Kind = Kind.StatusEffect, Name = "Rested" };
            rested.Links.Add(new Link { Group = LinkWords.GivenBy, Target = bed.Key, Back = true });
            var wet = new Entry { Kind = Kind.StatusEffect, Name = "Wet" };
            wet.Links.Add(new Link { Group = LinkWords.GivenBy, Target = meadows.Key, Back = true });
            var lantern = E(Kind.Item, "Lantern", "$item_lantern");
            var stoneOfEikthyr = E(Kind.Piece, "BossStone_Eikthyr");
            var eikthyr = new Entry { Kind = Kind.StatusEffect, Name = "GP_Eikthyr" };
            eikthyr.Links.Add(new Link { Group = LinkWords.GivenBy, Target = stoneOfEikthyr.Key, Back = true });
            var catalog = new EntryCatalog(new[]
            {
                meadows, forest, greydwarf, frozen, boar, troll, wood, stone, beech, pine, club, bench, wall, hammer,
                berries, bush, fed, trophy, bed, rested, wet, lantern, stoneOfEikthyr, eikthyr,
            });
            return new CompendiumCatchUp(catalog, toolsOf: piece => piece == "wood_wall" ? new[] { "Hammer" } : null, soldByTrader: item => item == "Lantern");
        }

        /// <summary>
        /// Plays the growth step by step into two books, one reading every record each time and
        /// one only what was added, and holds them to the same saved text after every step.
        /// </summary>
        private static List<bool> SameAsWhole(params Action<CompendiumRecords>[] growth) => SameAsWhole(false, growth);

        /// <summary>The same, the first step read as a character loads, the way a book is first caught up.</summary>
        private static List<bool> SameAsWhole(bool firstReading, params Action<CompendiumRecords>[] growth)
        {
            var whole = World();
            var grown = World();
            var wholeBook = new CompendiumBook();
            var grownBook = new CompendiumBook();
            var now = new CompendiumRecords();
            var before = new CompendiumRecords();
            var wereWhole = new List<bool>();
            foreach (var grow in growth)
            {
                grow(now);
                if (firstReading && wereWhole.Count == 0)
                {
                    whole.Fill(now, wholeBook, firstLoad: true);
                    grown.Fill(now, grownBook, firstLoad: true);
                    wereWhole.Add(true);
                }
                else
                {
                    whole.Fill(now, wholeBook, firstLoad: false);
                    wereWhole.Add(grown.FillGrown(now, before, grownBook));
                }
                before.CopyFrom(now);
                Assert.Equal(wholeBook.ToSave()(), grownBook.ToSave()());
            }
            return wereWhole;
        }

        [Fact]
        public void KillsCaughtUpAsTheCharacterLoadedStayProvedAsLaterKillsAreReadAlone()
        {
            // Read as the character loads, its kills are proved with no moment; a later kill read
            // alone must not take them back as strays, as every kill the records hold still tells them.
            var wereWhole = SameAsWhole(true,
                r => { r.Biomes.Add("$biome_meadows"); r.Biomes.Add("$biome_blackforest"); r.Killed.Add("$enemy_greydwarf"); r.Killed.Add("$enemy_troll"); },
                r => r.Killed.Add("$enemy_boar"),
                r => r.Crafted.Add("$item_club"));

            Assert.Equal(new[] { true, false, false }, wereWhole.ToArray());
        }

        [Fact]
        public void ReadingOnlyWhatWasAddedLeavesTheBookAsReadingEverythingWould()
        {
            var wereWhole = SameAsWhole(
                r => r.Biomes.Add("$biome_meadows"),
                r => r.Killed.Add("$enemy_boar"),
                r => r.PickedUp.Add("$item_wood"),
                r => r.Recipes.Add("$item_club"),
                r => r.Crafted.Add("$item_club"),
                r => r.Placed.Add("$piece_woodwall"),
                r => r.Stations.Add("$piece_workbench"),
                r => r.Picked.Add("Raspberry"),
                r => r.Eaten.Add("$item_raspberries"),
                r => r.Materials.Add("$item_lantern"),
                r => r.Materials.Add("$item_stone"),
                r => r.Placed.Add("$piece_bed"),
                r => r.Uniques.Add("defeated_eikthyr"));

            // After the first biome, every step was read from what it added alone.
            Assert.All(wereWhole.GetRange(1, wereWhole.Count - 1), Assert.False);
        }

        [Fact]
        public void AKillUnderANameSharedAcrossBiomesTellsTheSameReadAlone()
        {
            // The greydwarf's name is shared with the frozen one of the Deep North: which one the
            // kill was is told by the biomes entered, whether read alone or with every record.
            var wereWhole = SameAsWhole(
                r => r.Biomes.Add("$biome_meadows"),
                r => r.Killed.Add("$enemy_greydwarf"),
                r => r.Trophies.Add("TrophyTroll"),
                r => r.Killed.Add("$enemy_troll"));

            Assert.All(wereWhole.GetRange(1, wereWhole.Count - 1), Assert.False);
        }

        [Fact]
        public void ABiomeEnteredReadsEveryRecordAgainAsItChangesWhatTheOldOnesTell()
        {
            // Wood picked up in the Meadows says nothing of the Black Forest's pines until the
            // character goes there; then the old record tells of them too.
            var wereWhole = SameAsWhole(
                r => r.Biomes.Add("$biome_meadows"),
                r => r.PickedUp.Add("$item_wood"),
                r => r.Killed.Add("$enemy_greydwarf"),
                r => r.Biomes.Add("$biome_blackforest"));

            Assert.Equal(new[] { true, false, false, true }, wereWhole.ToArray());
        }

        [Fact]
        public void APowerChosenReadsEveryRecordAgain()
        {
            var wereWhole = SameAsWhole(
                r => r.Biomes.Add("$biome_meadows"),
                r => r.Powers.Add("GP_Eikthyr"));

            Assert.Equal(new[] { true, true }, wereWhole.ToArray());
        }

        [Fact]
        public void ARecordGoneReadsEveryRecordAgain()
        {
            // The game can take a key back; what it told may be taken back with it.
            var wereWhole = SameAsWhole(
                r => r.Biomes.Add("$biome_meadows"),
                r => r.Uniques.Add("defeated_eikthyr"),
                r => { r.Uniques.Remove("defeated_eikthyr"); r.Killed.Add("$enemy_boar"); },
                r => r.Crafted.Add("$item_club"),
                r => { r.Uniques.Add("defeated_gdking"); r.Uniques.Remove("defeated_eikthyr"); },
                r => { r.Uniques.Add("defeated_eikthyr"); r.Uniques.Remove("defeated_gdking"); });

            // Once read again, the next growth is read alone; one record swapped for another, as
            // many as before, is read whole too.
            Assert.Equal(new[] { true, false, true, false, false, true }, wereWhole.ToArray());
        }

        [Fact]
        public void WhatWasAddedHoldsOnlyTheNewRecordsOfEachKind()
        {
            var before = new CompendiumRecords();
            before.Killed.Add("$enemy_boar");
            before.Biomes.Add("$biome_meadows");
            var now = new CompendiumRecords();
            now.CopyFrom(before);
            now.Killed.Add("$enemy_troll");
            now.Crafted.Add("$item_club");
            var added = new CompendiumRecords();

            Assert.True(now.AddedSince(before, added));
            Assert.Equal(new[] { "$enemy_troll" }, added.Killed);
            Assert.Equal(new[] { "$item_club" }, added.Crafted);
            Assert.Empty(added.Biomes);
            Assert.Empty(added.Materials);
        }
    }
}
