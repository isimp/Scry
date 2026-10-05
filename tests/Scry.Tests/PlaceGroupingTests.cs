using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PlaceGroupingTests
    {
        // A dungeon or camp and its rooms are one group inside its biome, right after the biome's
        // other places: the location first, tagged dungeon or camp with its prefab, then its rooms,
        // indented and tagged, entrances first, then rooms, then end caps, dividers and walls. A
        // room has one home, the dungeon of its lowest theme flag, so a room two dungeons share is
        // under the first. Until the location that builds them is read, a theme's rooms wait
        // together after every biome.

        private static readonly Group Forest = LocationWords.Group(new[] { "BlackForest" }, b => "Black Forest");
        private static readonly Group Swamp = LocationWords.Group(new[] { "Swamp" }, b => "Swamp");

        private static PlaceItem Place(string prefab, string shown, Group biome, int themes = 0, string algorithm = "Dungeon") =>
            new PlaceItem { Key = "loc:" + prefab, Prefab = prefab, Shown = shown, Biome = biome, Theme = themes, Algorithm = algorithm };

        private static PlaceItem Room(string prefab, int theme, string words, RoomShape shape = null) =>
            new PlaceItem { Key = "loc:" + prefab, Prefab = prefab, Room = true, Theme = theme, ThemeWords = words, Shape = shape };

        private static RoomShape Shape(bool entrance = false, bool endCap = false, bool divider = false, bool perimeter = false) =>
            new RoomShape { Entrance = entrance, EndCap = endCap, Divider = divider, Perimeter = perimeter };

        [Theory]
        [InlineData(8, 8)]
        [InlineData(520, 8)]
        [InlineData(1028, 4)]
        [InlineData(12288, 4096)]
        [InlineData(0, 0)]
        public void ARoomsHomeIsItsLowestThemeFlag(int theme, int home)
        {
            Assert.Equal(home, PlaceGrouping.Home(theme));
        }

        [Fact]
        public void ALocationIsADungeonOrACampOnceReadAndARoomIsARoom()
        {
            // As the search's is: takes them, from what tags them in the list.
            var placed = PlaceGrouping.Arrange(new[]
            {
                Place("Crypt2", "Burial Chambers", Forest, 8),
                Place("GoblinCamp2", "Fuling camp", Forest, 16, "CampRadial"),
                Place("Runestone", "Runestone", Forest),
                Room("forestcrypt_Bend1", 8, "Forest crypt", Shape(entrance: true)),
                Room("forestcrypt_wall", 8, "Forest crypt", Shape(perimeter: true)),
                Room("sunkencrypt_room1", 4096, "Sunken crypt"),
            });

            Assert.Equal("dungeon", placed["loc:Crypt2"].Is);
            Assert.Equal("camp", placed["loc:GoblinCamp2"].Is);
            // A location building neither, or not read yet, is no place word.
            Assert.Null(placed["loc:Runestone"].Is);
            Assert.Equal("room", placed["loc:forestcrypt_Bend1"].Is);
            Assert.Equal("room", placed["loc:forestcrypt_wall"].Is);
            Assert.Equal("room", placed["loc:sunkencrypt_room1"].Is);
        }

        [Fact]
        public void ADungeonAndItsRoomsAreOneGroupInItsBiome()
        {
            var placed = PlaceGrouping.Arrange(new[]
            {
                Place("Crypt2", "Burial Chambers", Forest, 8),
                Place("TrollCave02", "Troll cave", Forest),
                Room("forestcrypt_room1", 8, "Forest crypt", Shape()),
            });

            var group = placed["loc:Crypt2"].Group;
            Assert.Equal("Black Forest · Burial Chambers", group.Name);
            Assert.Equal(group, placed["loc:forestcrypt_room1"].Group);
            Assert.Equal(Forest, placed["loc:TrollCave02"].Group);
            Assert.True(group.Order > Forest.Order && group.Order < Swamp.Order);
        }

        [Fact]
        public void TheLocationComesFirstThenEntrancesRoomsAndEndCaps()
        {
            var placed = PlaceGrouping.Arrange(new[]
            {
                Room("forestcrypt_EndCap", 8, "Forest crypt", Shape(endCap: true)),
                Room("forestcrypt_room1", 8, "Forest crypt", Shape()),
                Room("forestcrypt_entrance_large", 8, "Forest crypt", Shape(entrance: true)),
                Place("Crypt2", "Burial Chambers", Forest, 8),
                Room("forestcrypt_unread", 8, "Forest crypt"),
            });

            Assert.Equal(0, placed["loc:Crypt2"].Rank);
            Assert.Equal(1, placed["loc:forestcrypt_entrance_large"].Rank);
            Assert.Equal(2, placed["loc:forestcrypt_room1"].Rank);
            Assert.Equal(2, placed["loc:forestcrypt_unread"].Rank);
            Assert.Equal(3, placed["loc:forestcrypt_EndCap"].Rank);

            var closing = PlaceGrouping.Arrange(new[]
            {
                Place("GoblinCamp2", "Fuling camp", Forest, 16, "CampRadial"),
                Room("gobvill_wall", 16, "Goblin camp", Shape(perimeter: true)),
                Room("forestcrypt_divider", 16, "Goblin camp", Shape(divider: true)),
            });
            Assert.Equal(3, closing["loc:gobvill_wall"].Rank);
            Assert.Equal(3, closing["loc:forestcrypt_divider"].Rank);
        }

        [Fact]
        public void TheLocationIsTaggedAndItsRoomsIndentedAndTagged()
        {
            var placed = PlaceGrouping.Arrange(new[]
            {
                Place("Crypt2", "Burial Chambers", Forest, 8),
                Place("GoblinCamp2", "Fuling camp", Forest, 16, "CampRadial"),
                Room("forestcrypt_entrance_large", 8, "Forest crypt", Shape(entrance: true)),
                Room("forestcrypt_room1", 8, "Forest crypt", Shape()),
                Room("forestcrypt_EndCap", 8, "Forest crypt", Shape(endCap: true)),
                Room("forestcrypt_divider", 8, "Forest crypt", Shape(divider: true)),
                Room("gobvill_wall", 16, "Goblin camp", Shape(perimeter: true)),
            });

            Assert.Equal(("dungeon · Crypt2", false), (placed["loc:Crypt2"].Tag, placed["loc:Crypt2"].Indent));
            Assert.Equal("camp · GoblinCamp2", placed["loc:GoblinCamp2"].Tag);
            Assert.Equal(("entrance room · forestcrypt_entrance_large", true), (placed["loc:forestcrypt_entrance_large"].Tag, placed["loc:forestcrypt_entrance_large"].Indent));
            Assert.Equal("room · forestcrypt_room1", placed["loc:forestcrypt_room1"].Tag);
            Assert.Equal("end cap · forestcrypt_EndCap", placed["loc:forestcrypt_EndCap"].Tag);
            Assert.Equal("divider · forestcrypt_divider", placed["loc:forestcrypt_divider"].Tag);
            Assert.Equal("wall · gobvill_wall", placed["loc:gobvill_wall"].Tag);
        }

        [Fact]
        public void VariantsOfADungeonShareItsGroupEachTaggedByItsPrefab()
        {
            var placed = PlaceGrouping.Arrange(new[] { Place("Crypt2", "Burial Chambers", Forest, 8), Place("Crypt3", "Burial Chambers", Forest, 8) });

            Assert.Equal(placed["loc:Crypt2"].Group, placed["loc:Crypt3"].Group);
            Assert.Equal("dungeon · Crypt3", placed["loc:Crypt3"].Tag);
        }

        [Fact]
        public void TheFirstLocationBuildingATHemeNamesItsGroup()
        {
            var placed = PlaceGrouping.Arrange(new[] { Place("Crypt2", "Burial Chambers", Forest, 8), Place("ModCrypt", "Old tomb", Swamp, 8) });

            Assert.Equal("Black Forest · Burial Chambers", placed["loc:ModCrypt"].Group.Name);
        }

        [Fact]
        public void ARoomTwoDungeonsShareIsUnderTheDungeonOfItsLowestTheme()
        {
            var placed = PlaceGrouping.Arrange(new[]
            {
                Place("Hildir_crypt", "Smouldering Tomb", Forest, 512),
                Place("Crypt2", "Burial Chambers", Forest, 8),
                Room("forestcrypt_Stairs1", 520, "Forest crypt", Shape()),
                Room("forestcrypt_chamber_hildir", 512, "Forest crypt hildir", Shape()),
            });

            Assert.Equal(placed["loc:Crypt2"].Group, placed["loc:forestcrypt_Stairs1"].Group);
            Assert.Equal(placed["loc:Hildir_crypt"].Group, placed["loc:forestcrypt_chamber_hildir"].Group);
        }

        [Fact]
        public void RoomsOfADungeonNotReadYetWaitTogetherAfterEveryBiome()
        {
            var placed = PlaceGrouping.Arrange(new[]
            {
                Place("Crypt2", "Crypt", Forest),
                Room("forestcrypt_room1", 8, "Forest crypt"),
                Room("forestcrypt_Stairs1", 520, "Forest crypt"),
            });

            var waiting = placed["loc:forestcrypt_room1"].Group;
            Assert.Equal("Forest crypt rooms", waiting.Name);
            Assert.Equal(waiting, placed["loc:forestcrypt_Stairs1"].Group);
            Assert.True(waiting.Order > LocationWords.Group(new[] { "Meadows", "Swamp" }).Order);
            Assert.False(placed["loc:forestcrypt_room1"].Indent);
            Assert.Equal(Forest, placed["loc:Crypt2"].Group);
            Assert.Null(placed["loc:Crypt2"].Tag);
        }
    }
}
