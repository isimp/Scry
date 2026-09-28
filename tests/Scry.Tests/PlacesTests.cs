using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class PlacesTests
    {
        private static readonly Dictionary<string, string> Creatures = new Dictionary<string, string>
        {
            ["Goblin"] = "Fuling",
            ["Greydwarf"] = "Greydwarf",
            ["Boar"] = "Boar",
        };

        private static string Named(string prefab, string game = "", string boss = "", string trader = "", string biome = "") =>
            Places.LocationLabel(new PlaceFacts { Prefab = prefab, GameName = game, Boss = boss, Trader = trader, Biome = biome }, Creatures);

        [Fact]
        public void APlaceTheGameNamesGoesByThatName()
        {
            // The name shown on entering a dungeon or discovering a place.
            Assert.Equal("Burial Chambers", Named("Crypt2", game: "Burial Chambers"));
            Assert.Equal("Sealed Tower", Named("Hildir_plainsfortress", game: "Sealed Tower", trader: "Hildir"));
        }

        [Fact]
        public void AnAltarGoesByTheBossItSummonsAndACampByItsTrader()
        {
            Assert.Equal("The Elder's altar", Named("GDKing", boss: "The Elder"));
            Assert.Equal("Haldor's camp", Named("Vendor_BlackForest", trader: "Haldor"));
            Assert.Equal("The Bog Witch's camp", Named("BogWitch_Camp", trader: "The Bog Witch"));
        }

        [Theory]
        [InlineData("WoodHouse10", "Wood house")]
        [InlineData("GoblinCamp2_1", "Fuling camp")]
        [InlineData("GoblinHut03", "Fuling hut")]
        [InlineData("Greydwarf_camp1", "Greydwarf camp")]
        [InlineData("Mistlands_GuardTower1_new", "Mistlands guard tower")]
        [InlineData("Mistlands_GuardTower1_ruined_new2", "Mistlands guard tower ruined")]
        [InlineData("FrozenShip01_DN", "Frozen ship")]
        [InlineData("PlaceofMystery2", "Place of mystery")]
        [InlineData("DevRoof1", "Dev roof")]
        public void AnyOtherPlaceGoesByItsNameInWordsWithoutVariantsOrBuildTags(string prefab, string shown)
        {
            Assert.Equal(shown, Named(prefab));
        }

        [Fact]
        public void APlaceSaysItsBiomeAndDoesNotRepeatIt()
        {
            Assert.Equal("Wood house · Meadows", Named("WoodHouse3", biome: "Meadows"));
            Assert.Equal("Guard tower · Mistlands", Named("Mistlands_GuardTower2_new", biome: "Mistlands"));
            Assert.Equal("Runestone · Black Forest", Named("Runestone_BlackForest", biome: "Black Forest"));
            Assert.Equal("Runestone · Swamp", Named("Runestone_Swamps", biome: "Swamp"));
            Assert.Equal("Runestone boars · Meadows", Named("Runestone_Boars", biome: "Meadows"));
            // Only the whole biome's name goes: the forest in a crypt's name stays.
            Assert.Equal("Half burried forest crypt · Black Forest", Named("HalfBurried_ForestCrypt", biome: "Black Forest"));
            Assert.Equal("Burial Chambers · Black Forest", Named("Crypt3", game: "Burial Chambers", biome: "Black Forest"));
        }

        [Fact]
        public void ANameWithNoWordsInItKeepsItsOwnSoThePlaceIsNeverNameless()
        {
            Assert.Equal("2048", Named("2048"));
            // A name that is only its biome keeps it rather than go blank.
            Assert.Equal("Mistlands · Mistlands", Named("Mistlands1", biome: "Mistlands"));
        }

        [Fact]
        public void DungeonRoomsGoByTheDungeonsTheyAreBuiltInto()
        {
            var dungeons = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(8, "Burial Chambers · Black Forest"),
                new KeyValuePair<int, string>(4, "Frost Caves · Mountain"),
                new KeyValuePair<int, string>(1024, "Howling Cavern · Mountain"),
                new KeyValuePair<int, string>(262144, "Half burried forest crypt · Black Forest"),
                new KeyValuePair<int, string>(8, "Burial Chambers · Black Forest"),
            };

            Assert.Equal(new[] { "Burial Chambers · Black Forest" }, Places.RoomLabels(8, dungeons));
            // A room several dungeons build with is in each of them.
            Assert.Equal(new[] { "Frost Caves · Mountain", "Howling Cavern · Mountain" }, Places.RoomLabels(1028, dungeons));
            // A kind of room the game has no name for is still found through its dungeon.
            Assert.Equal(new[] { "Half burried forest crypt · Black Forest" }, Places.RoomLabels(262144, dungeons));
            // Rooms no dungeon here builds with (a mod's) are said to be a dungeon's.
            Assert.Equal(new[] { "Dungeon rooms" }, Places.RoomLabels(32768, dungeons));
            Assert.Equal(new[] { "Dungeon rooms" }, Places.RoomLabels(0, dungeons));
        }

        [Fact]
        public void APlaceIsSearchedForByItsNameAlone()
        {
            Assert.Equal("Wood house", Places.NameOf("Wood house · Meadows"));
            Assert.Equal("Crypt", Places.NameOf("Crypt"));

            var house = E("Beehive", Kind.Piece);
            house.FoundIn = new[] { "Wood house · Meadows" };
            Assert.True(Search.Matches(house, "in:woodhouse"));
            Assert.False(Search.Matches(house, "in:meadows"));
        }

        private static Dictionary<string, HashSet<string>> Found(params (string Prefab, string Place)[] finds)
        {
            var found = new Dictionary<string, HashSet<string>>();
            foreach (var (prefab, place) in finds)
            {
                if (!found.TryGetValue(prefab, out var places)) found[prefab] = places = new HashSet<string>();
                places.Add(place);
            }
            return found;
        }

        [Fact]
        public void APrefabFoundInPlacesListsEachOnceInOrder()
        {
            var catalog = Game();
            Places.Apply(catalog, Found(("Troll", "Troll cave"), ("Troll", "Crypt"), ("Troll", "Troll cave")));

            Assert.Equal(new[] { "Crypt", "Troll cave" }, catalog.Single(e => e.Name == "Troll").FoundIn);
            Assert.Empty(catalog.Single(e => e.Name == "TrollArmorChest").FoundIn);
        }

        [Fact]
        public void AnEffectNothingWasFoundToPlayButALocationNamesGoesUnderInLocationsBeforeTheUnplayed()
        {
            var unplayed = Groups.Purpose(new string[0], false, false);
            var music = E("Music_StoneHenge", Kind.Sound);
            music.Group = unplayed.Name;
            music.GroupOrder = unplayed.Order;
            var lost = E("sfx_bat_attack", Kind.Sound);
            lost.Group = unplayed.Name;
            lost.GroupOrder = unplayed.Order;
            var catalog = new List<Entry> { music, lost };

            Places.Apply(catalog, Found(("Music_StoneHenge", "Stone henge")));

            Assert.Equal("In locations", music.Group);
            Assert.True(music.GroupOrder < unplayed.Order);
            Assert.True(music.GroupOrder > Groups.Purpose(new[] { "m_effects" }, false, false).Order);
            Assert.Equal(unplayed.Name, lost.Group);
        }

        [Fact]
        public void AnEffectWithAPurposeKeepsItsGroupWhereverItIsFound()
        {
            var hit = Groups.Purpose(new[] { "m_hitEffects" }, false, false);
            var sparks = E("vfx_sparks", Kind.Effect);
            sparks.Group = hit.Name;
            sparks.GroupOrder = hit.Order;

            Places.Apply(new List<Entry> { sparks }, Found(("vfx_sparks", "Crypt")));

            Assert.Equal(hit.Name, sparks.Group);
            Assert.Equal(new[] { "Crypt" }, sparks.FoundIn);
        }

        [Fact]
        public void AProjectileNothingFiresButALocationNamesGoesUnderInLocationsBeforeTheRest()
        {
            var other = Groups.Projectile(new Shooter[0]);
            var bolt = E("FrozenKing_P2_Projectile_Eikthyr", Kind.Projectile);
            bolt.Group = other.Name;
            bolt.GroupOrder = other.Order;

            Places.Apply(new List<Entry> { bolt }, Found(("FrozenKing_P2_Projectile_Eikthyr", "DN bossroom")));

            Assert.Equal("In locations", bolt.Group);
            Assert.True(bolt.GroupOrder < other.Order);
            Assert.True(bolt.GroupOrder > Groups.Projectile(new[] { new Shooter(Kind.Creature, "", false) }).Order);
        }

        [Fact]
        public void AStatusEffectIsNeverFoundInAPlaceByTheNameOfAPrefab()
        {
            var rested = E("Rested", Kind.StatusEffect);
            Places.Apply(new List<Entry> { rested }, Found(("Rested", "Crypt")));

            Assert.Empty(rested.FoundIn);
        }
    }
}
