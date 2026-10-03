using Xunit;

namespace Scry.Tests
{
    public class EntryNameTests
    {
        // An entry is shown by the name the game gives it, as a player knows it; where the game
        // gives none, by its prefab's name, so nothing is listed nameless.

        [Fact]
        public void AnEntryGoesByTheNameTheGameShows()
        {
            var entry = new Entry { Name = "Wolf_cub", DisplayName = "Wolf cub", Kind = Kind.Creature };
            Assert.Equal("Wolf cub", entry.ShownName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AnEntryTheGameGivesNoNameGoesByItsPrefabsName(string shown)
        {
            var entry = new Entry { Name = "vfx_spawn", DisplayName = shown, Kind = Kind.Effect };
            Assert.Equal("vfx_spawn", entry.ShownName);
        }

        [Fact]
        public void ANameTheGameGivesLaterIsShownFromThen()
        {
            var entry = new Entry { Name = "Troll", Kind = Kind.Creature };
            Assert.Equal("Troll", entry.ShownName);
            entry.DisplayName = "Troll of the forest";
            Assert.Equal("Troll of the forest", entry.ShownName);
        }
    }
}
