using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class SearchFlagsTests
    {
        // is: asks what a thing is, in one word: a boss, something to tame, to wear, to eat, to
        // fight or shoot with, to build or craft, a dungeon, a camp or one of their rooms, or what
        // Scry has nothing to show of or is not sure of. Each flag is read as the details or the
        // Locations list tell the same thing.

        [Fact]
        public void ACreatureIsABossTameableOrFlyingAsItsDetailsSay()
        {
            Assert.Equal(new[] { "boss", "flying" }, SearchFlags.Of(new FlagFacts { Boss = true, Flying = true }));
            Assert.Equal(new[] { "tameable" }, SearchFlags.Of(new FlagFacts { Tameable = true }));
            Assert.Empty(SearchFlags.Of(new FlagFacts()));
        }

        [Fact]
        public void AnItemIsWornWieldedShotOrEatenByItsType()
        {
            foreach (var worn in new[] { "Helmet", "Chest", "Legs", "Hands", "Shoulder", "Utility", "Trinket" })
            {
                Assert.Equal(new[] { "wearable" }, SearchFlags.Of(new FlagFacts { ItemType = worn }));
            }
            foreach (var weapon in new[] { "OneHandedWeapon", "TwoHandedWeapon", "TwoHandedWeaponLeft", "Bow", "Attach_Atgeir" })
            {
                Assert.Equal(new[] { "weapon" }, SearchFlags.Of(new FlagFacts { ItemType = weapon }));
            }
            Assert.Equal(new[] { "ammo" }, SearchFlags.Of(new FlagFacts { ItemType = "Ammo" }));
            Assert.Equal(new[] { "ammo" }, SearchFlags.Of(new FlagFacts { ItemType = "AmmoNonEquipable" }));
            Assert.Equal(new[] { "food", "craftable" }, SearchFlags.Of(new FlagFacts { ItemType = "Consumable", Food = true, Craftable = true }));
            // A shield, a tool, a torch or a material is none of these.
            foreach (var other in new[] { "Shield", "Tool", "Torch", "Material", "Trophy" }) Assert.Empty(SearchFlags.Of(new FlagFacts { ItemType = other }));
        }

        [Fact]
        public void APieceIsBuildableWhereAToolBuildsItAndAnythingSilentOrUnsure()
        {
            Assert.Equal(new[] { "buildable" }, SearchFlags.Of(new FlagFacts { Buildable = true }));
            Assert.Equal(new[] { "silent", "unsure" }, SearchFlags.Of(new FlagFacts { Silent = true, Unsure = true }));
        }

        [Fact]
        public void APlaceIsADungeonACampOrARoomAsTheLocationsListTagsIt()
        {
            Assert.Equal(new[] { "dungeon" }, SearchFlags.Of(new FlagFacts { Place = "dungeon" }));
            Assert.Equal(new[] { "camp" }, SearchFlags.Of(new FlagFacts { Place = "camp" }));
            Assert.Equal(new[] { "room", "silent" }, SearchFlags.Of(new FlagFacts { Place = "room", Silent = true }));
            // Only those three words: anything else a place could be called is no flag.
            Assert.Empty(SearchFlags.Of(new FlagFacts { Place = "village" }));
        }

        [Fact]
        public void APlaceReadLaterIsToldWhatItIsWithItsOtherFlagsKept()
        {
            // A location's dungeon or camp is known once it is read, after the catalog's flags.
            Assert.Equal(new[] { "camp", "silent" }, SearchFlags.WithPlace(new[] { "silent" }, "camp"));
            Assert.Equal(new[] { "dungeon", "unsure" }, SearchFlags.WithPlace(new[] { "camp", "unsure" }, "dungeon"));
            Assert.Equal(new[] { "unsure" }, SearchFlags.WithPlace(new[] { "room", "unsure" }, null));
            Assert.Empty(SearchFlags.WithPlace(new string[0], null));
        }

        [Fact]
        public void TheHelpNamesEveryFlag()
        {
            var line = SearchFlags.HelpLine();
            foreach (var flag in SearchFlags.All) Assert.Contains(flag, line);
            Assert.StartsWith("What it is: boss, tameable,", line);
            Assert.Contains("silent (nothing to see or hear)", line);
        }

        private static List<Entry> Catalog()
        {
            var troll = E("Troll", Kind.Creature, "Troll");
            var eikthyr = E("Eikthyr", Kind.Creature, "Eikthyr");
            eikthyr.SetTermWords("is", new[] { "boss", "flying" });
            var boar = E("Boar", Kind.Creature, "Boar");
            boar.SetTermWords("is", new[] { "tameable" });
            var helmet = E("HelmetBronze", Kind.Item, "Bronze helmet");
            helmet.SetTermWords("is", new[] { "wearable", "craftable" });
            var spark = E("vfx_spark", Kind.Effect);
            spark.SetTermWords("is", new[] { "silent" });
            var crypt = E("SunkenCrypt4", Kind.Location, "Sunken Crypts");
            crypt.SetTermWords("is", new[] { "dungeon" });
            var camp = E("GoblinCamp2", Kind.Location, "Fuling camp");
            camp.SetTermWords("is", new[] { "camp" });
            var room = E("sunken_crypt_room1", Kind.Location);
            room.SetTermWords("is", new[] { "room" });
            return new List<Entry> { troll, eikthyr, boar, helmet, spark, crypt, camp, room };
        }

        private static List<string> Find(string text) =>
            Search.Run(Catalog(), new Query { Text = text }, new List<string>()).Select(e => e.Name).ToList();

        [Fact]
        public void IsFindsWhatAThingIsByTheStartOfItsWord()
        {
            Assert.Equal(new[] { "Eikthyr" }, Find("is:boss"));
            Assert.Equal(new[] { "Boar" }, Find("is:tame"));
            Assert.Equal(new[] { "Boar", "Eikthyr" }, Find("is:boss,tameable"));
            Assert.Equal(new[] { "Troll" }, Find("kind:creature -is:boss,tameable"));
            // The start of the word, not any part of it: "oss" is no flag's start.
            Assert.Empty(Find("is:oss"));
        }

        [Fact]
        public void IsFindsTheDungeonsCampsAndRoomsTheLocationsListTags()
        {
            Assert.Equal(new[] { "SunkenCrypt4" }, Find("is:dungeon"));
            Assert.Equal(new[] { "GoblinCamp2" }, Find("is:camp"));
            Assert.Equal(new[] { "sunken_crypt_room1" }, Find("is:room"));
            Assert.Equal(new[] { "GoblinCamp2", "SunkenCrypt4" }, Find("is:dungeon,camp").OrderBy(n => n).ToList());
        }

        [Fact]
        public void FlagsReadAgainTakeThePlaceOfThoseBefore()
        {
            var boar = E("Boar", Kind.Creature, "Boar");
            boar.SetTermWords("is", new[] { "tameable" });
            boar.SetTermWords("is", new[] { "flying" });
            Assert.Equal(new[] { "flying" }, boar.TermWords("is"));
            boar.SetTermWords("is", new string[0]);
            Assert.Empty(boar.TermWords("is"));
        }

        [Fact]
        public void IsIsSuggestedWithEveryFlagTheCatalogHas()
        {
            var index = new TermIndex(Catalog());
            var key = SearchHelp.Suggest("i", index).First(s => s.Insert == "is:");
            Assert.Equal("what it is: a boss, food, a weapon", key.Note);
            var flags = SearchHelp.Suggest("is:", index);
            Assert.Contains(flags, s => s.Insert == "is:boss" && s.Count == 1);
            Assert.Contains(flags, s => s.Insert == "is:craftable" && s.Count == 1);
            Assert.Equal("is:wearable", SearchHelp.Suggest("is:we", index).First().Insert);
        }
    }
}
