using Xunit;

namespace Scry.Tests
{
    public class EntryNameTests
    {
        // An entry is shown by the name the game gives it, as a player knows it, starting with a
        // capital; where the game gives none, by its prefab's words, so nothing is listed nameless
        // and nothing reads as code. The prefab's own name stands beside it in the list.

        [Fact]
        public void AnEntryGoesByTheNameTheGameShows()
        {
            var entry = new Entry { Name = "Wolf_cub", DisplayName = "Wolf cub", Kind = Kind.Creature };
            Assert.Equal("Wolf cub", entry.ShownName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AnEntryTheGameGivesNoNameGoesByItsPrefabsWords(string shown)
        {
            var entry = new Entry { Name = "arbalest_projectile_blackmetal", DisplayName = shown, Kind = Kind.Projectile };
            Assert.Equal("Arbalest projectile blackmetal", entry.ShownName);
            var wisp = new Entry { Name = "FrostWisp", DisplayName = shown, Kind = Kind.Creature };
            Assert.Equal("Frost wisp", wisp.ShownName);
            // A prefab named in words already goes by them.
            var stone = new Entry { Name = "Stone", DisplayName = shown, Kind = Kind.Resource };
            Assert.Equal("Stone", stone.ShownName);
        }

        [Fact]
        public void ARenamedPrefabGoesByItsNewWords()
        {
            var entry = new Entry { Name = "vfx_troll_hit", Kind = Kind.Effect };
            Assert.Equal("Troll hit", entry.ShownName);
            entry.Name = "sfx_boar_idle";
            Assert.Equal("Boar idle", entry.ShownName);
        }

        [Fact]
        public void AGameNameInSmallLettersStartsWithACapital()
        {
            // The game names some creatures' attacks in its own small letters ("bjorn bite").
            var entry = new Entry { Name = "bjorn_bite", DisplayName = "bjorn bite", Kind = Kind.Item };
            Assert.Equal("Bjorn bite", entry.ShownName);
        }

        [Fact]
        public void AGameNameRunTogetherReadsAsWords()
        {
            var entry = new Entry { Name = "GrapplingHook", DisplayName = "GrapplingHook", Kind = Kind.StatusEffect };
            Assert.Equal("Grappling hook", entry.ShownName);
        }

        [Fact]
        public void AModGoesByItsNameAsItWritesIt()
        {
            var mod = new Entry { Name = "odinplus", DisplayName = "odinplus", Kind = Kind.Mod };
            Assert.Equal("odinplus", mod.ShownName);
            Assert.Equal("PlantEverything", new Entry { Name = "PlantEverything", DisplayName = "PlantEverything", Kind = Kind.Mod }.ShownName);
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
